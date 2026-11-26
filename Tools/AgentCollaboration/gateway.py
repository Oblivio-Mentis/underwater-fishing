"""Task-scoped MCP tools for Kimi. Policy lives outside the worker worktree."""
import asyncio
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time
import shutil
import urllib.request
import httpx

from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
from mcp.server.fastmcp import FastMCP

POLICY = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
ROOT = Path(POLICY['worktree']).resolve()
AUDIT = Path(POLICY['audit'])
GIT = POLICY.get('git', 'C:/Program Files/Git/cmd/git.exe')
NO_WINDOW = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
mcp = FastMCP('codex-task-gateway')


def http_client(headers=None, timeout=None, auth=None):
    # Kimi's child environment can contain an IPv6 NO_PROXY entry that httpx
    # cannot parse. These fixed development endpoints use a direct connection.
    return httpx.AsyncClient(headers=headers, timeout=timeout or 30, auth=auth, trust_env=False)


def direct_env():
    # Kimi injects its own localhost proxy into MCP children; Git/urllib must
    # use the same direct network route verified from the Lead's terminal.
    return {k: v for k, v in os.environ.items() if not k.lower().endswith('_proxy') and k != 'NODE_USE_ENV_PROXY'}


def command_run(command, timeout=45, input_text=None, env=None):
    process = subprocess.Popen(command, cwd=ROOT, env=env or direct_env(), stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True, encoding='utf-8', errors='replace',
                               stdin=subprocess.PIPE if input_text is not None else subprocess.DEVNULL,
                               creationflags=NO_WINDOW)
    try:
        out, err = process.communicate(input=input_text, timeout=timeout)
    except subprocess.TimeoutExpired:
        # Killing only Git's wrapper leaves mingw Git holding the pipe open.
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True, creationflags=NO_WINDOW)
        process.communicate(timeout=10)
        raise TimeoutError('Approved command exceeded its time limit') from None
    return subprocess.CompletedProcess(command, process.returncode, out, err)


def record(tool, details):
    with AUDIT.open('a', encoding='utf-8') as f:
        f.write(json.dumps({'tool': tool, 'details': details}, ensure_ascii=False) + '\n')


def checked_path(name, write=False):
    p = Path(name)
    if p.is_absolute() or '..' in p.parts or not p.parts:
        raise ValueError('Only relative paths within this worktree are allowed')
    if any(x.startswith('.') or x in ('Library', 'Temp', 'UserSettings') for x in p.parts):
        raise ValueError('Hidden/configuration/cache paths are not accessible')
    candidate = ROOT / p
    for parent in [ROOT, *candidate.parents, candidate]:
        if parent.exists() and (parent.is_symlink() or getattr(parent.lstat(), 'st_file_attributes', 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT):
            raise ValueError('Symlinks and junctions are not accessible')
        if parent == ROOT.parent:
            break
    resolved = candidate.resolve()
    if not resolved.is_relative_to(ROOT):
        raise ValueError('Path leaves the worktree')
    # On Windows, reject alternate streams and device paths as well.
    if ':' in name or '\\' in name:
        raise ValueError('Use ordinary slash-separated relative paths')
    if write and not any(resolved.is_relative_to(ROOT / d) for d in POLICY['write_dirs']):
        raise ValueError('Write outside task scope denied')
    if candidate.exists() and candidate.is_file() and candidate.stat().st_nlink > 1:
        raise ValueError('Hard-linked files are not accessible')
    return resolved


@mcp.tool()
def task_context() -> str:
    """Read the full current task or review comment assigned by Codex, outside the worker's writable scope."""
    record('task_context', 'read')
    return (AUDIT.parent / 'current-task.txt').read_text(encoding='utf-8')


@mcp.tool()
def read_file(path: str) -> str:
    """Read a UTF-8 project file inside the isolated worktree."""
    p = checked_path(path)
    if p.stat().st_size > 500_000:
        raise ValueError('Read smaller files')
    record('read_file', path)
    return p.read_text(encoding='utf-8-sig')


@mcp.tool()
def list_files(prefix: str = 'Docs') -> list[str]:
    """List tracked project paths below a relative prefix (including sparse paths)."""
    checked_path(prefix)
    out = command_run([GIT, 'ls-files', '--', prefix], timeout=30)
    out.check_returncode()
    record('list_files', prefix)
    return out.stdout.splitlines()[:500]


@mcp.tool()
def write_file(path: str, content: str) -> dict:
    """Create or replace a UTF-8 file only in the task's approved directories."""
    p = checked_path(path, write=True)
    if len(content.encode('utf-8')) > 500_000:
        raise ValueError('File too large')
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding='utf-8', newline='\n')
    record('write_file', path)
    return {'path': path, 'bytes': p.stat().st_size}


@mcp.tool()
def run_check(name: str, path: str = '') -> dict:
    """Run an approved terminal check: git_status, git_diff_check, git_remote, json, or a Lead-defined command."""
    fixed = {
        'git_status': [GIT, 'status', '--short'],
        'git_diff_check': [GIT, 'diff', '--check'],
        'git_remote': [GIT, 'ls-remote', 'origin', 'HEAD'],
    }
    if name == 'json':
        data = json.loads(checked_path(path).read_text(encoding='utf-8'))
        record('run_check', {'name': name, 'path': path})
        return {'valid': True, 'keys': list(data) if isinstance(data, dict) else None}
    command = fixed.get(name) or POLICY.get('checks', {}).get(name)
    if not command:
        raise ValueError('Command not approved by Lead')
    # No shell, arbitrary argv, stdin, Git writes, or worker-defined executable.
    started = time.monotonic()
    result = command_run(command)
    record('run_check', {'name': name, 'exit_code': result.returncode, 'seconds': round(time.monotonic() - started, 3), 'executable': shutil.which(command[0])})
    return {'exit_code': result.returncode, 'stdout': result.stdout[-20000:], 'stderr': result.stderr[-4000:]}


@mcp.tool()
def github_read(endpoint: str = '') -> dict:
    """Read public underwater-fishing metadata, branches, or the latest 5 commits. No GitHub writes or secrets."""
    if endpoint not in ('', '/branches', '/commits?per_page=5'):
        raise ValueError('Endpoint not allowed')
    headers = {'User-Agent': 'Codex-Kimi-audit'}
    env = dict(direct_env(), GIT_TERMINAL_PROMPT='0', GCM_INTERACTIVE='never')
    credential = command_run(
        [GIT, 'credential', 'fill'], input_text='protocol=https\nhost=github.com\npath=Oblivio-Mentis/underwater-fishing.git\n\n',
        timeout=15, env=env,
    )
    # Use the existing OS credential manager in memory only. Never log its output.
    values = dict(line.split('=', 1) for line in credential.stdout.splitlines() if '=' in line)
    if credential.returncode == 0 and values.get('password'):
        headers['Authorization'] = 'Bearer ' + values['password']
    request = urllib.request.Request('https://api.github.com/repos/Oblivio-Mentis/underwater-fishing' + endpoint, headers=headers)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with opener.open(request, timeout=30) as response:
        data = json.load(response)
    record('github_read', endpoint)
    if endpoint == '':
        return {k: data[k] for k in ('full_name', 'private', 'default_branch', 'html_url')}
    return data


async def unity_session(operation, arguments):
    async with streamablehttp_client(POLICY['unity_url'], timeout=30, httpx_client_factory=http_client) as (r, w, _):
        async with ClientSession(r, w) as s:
            await s.initialize()
            selected = await s.call_tool('set_active_instance', {'instance': POLICY['unity_instance']})
            if selected.isError:
                raise RuntimeError('Unity instance unavailable')
            info = await s.read_resource('mcpforunity://project/info')
            # Check actual project path, rather than accepting the instance name alone.
            combined = ''.join(getattr(c, 'text', '') for c in info.contents).replace('\\\\', '/').replace('\\', '/').lower()
            if POLICY['unity_project'].replace('\\', '/').lower() not in combined:
                raise RuntimeError('Unity is connected to a different project')
            if operation == 'resource':
                return (await s.read_resource(arguments['uri'])).model_dump(mode='json')
            return (await s.call_tool(operation, arguments)).model_dump(mode='json')


@mcp.tool()
async def unity_read(kind: str, path: str = '') -> dict:
    """Read the designated live Unity Editor only: project, state, scene, hierarchy, console, tests, prefabs, or assets. No mutations."""
    resources = {'project': 'mcpforunity://project/info', 'state': 'mcpforunity://editor/state', 'tests': 'mcpforunity://tests'}
    commands = {
        'scene': ('manage_scene', {'action': 'get_active'}),
        'hierarchy': ('manage_scene', {'action': 'get_hierarchy', 'max_nodes': 15, 'max_depth': 2, 'include_transform': True}),
        'console': ('read_console', {'action': 'get', 'types': ['error', 'warning'], 'count': 10, 'include_stacktrace': False}),
        'prefabs': ('manage_asset', {'action': 'search', 'path': 'Assets/_Game', 'filter_type': 'Prefab', 'page_size': 5}),
    }
    if kind == 'prefab':
        if not path.startswith('Assets/') or '..' in path or not path.endswith('.prefab'):
            raise ValueError('Use an Assets-relative prefab path')
        operation, args = 'manage_prefabs', {'action': 'get_hierarchy', 'prefab_path': path}
    elif kind in resources:
        operation, args = 'resource', {'uri': resources[kind]}
    elif kind in commands:
        operation, args = commands[kind]
    else:
        raise ValueError('Read operation not allowed')
    result = await unity_session(operation, args)
    record('unity_read', {'kind': kind, 'result': result})
    return result


@mcp.tool()
async def documentation(query: str) -> dict:
    """Search official Unity documentation via the Unity MCP documentation tool."""
    result = await unity_session('unity_docs', {'action': 'lookup', 'query': query})
    record('documentation', result)
    return result


@mcp.tool()
async def context7(action: str, query: str, library: str) -> dict:
    """Resolve a library or query current documentation through public Context7 MCP. action: resolve or query; library: name or resolved ID."""
    if action == 'resolve':
        tool, arguments = 'resolve-library-id', {'libraryName': library, 'query': query}
    elif action == 'query':
        tool, arguments = 'query-docs', {'libraryId': library, 'query': query}
    else:
        raise ValueError('Use resolve or query')
    async with streamablehttp_client('https://mcp.context7.com/mcp', timeout=30, httpx_client_factory=http_client) as (r, w, _):
        async with ClientSession(r, w) as s:
            await s.initialize()
            result = (await s.call_tool(tool, arguments)).model_dump(mode='json')
    record('context7', {'action': action, 'result': result})
    return result


if __name__ == '__main__':
    mcp.run(transport='stdio')
