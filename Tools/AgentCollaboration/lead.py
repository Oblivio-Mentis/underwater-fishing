"""Codex -> Kimi task runner. No automatic commit, merge, push, or deletion."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
STATE = Path.home() / '.local/share/codex-kimi/tasks'
KIMI = Path(os.environ.get('CODEX_KIMI_EXE', str(Path.home() / '.local/share/codex-kimi/bin/2.8.1/kimi.exe')))
PYTHON = Path.home() / '.local/share/codex-kimi/venv/Scripts/python.exe'


def git(repo, *args):
    return subprocess.run(['git', '-c', 'core.quotePath=false', '-C', str(repo), *args], capture_output=True, text=True, encoding='utf-8', errors='replace', check=True).stdout.strip()


def save(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def task_dir(name):
    if not re.fullmatch('[a-z0-9][a-z0-9-]{0,60}', name):
        raise ValueError('Use a short lowercase task identifier')
    return STATE / name


def create(args):
    for d in args.write_dir:
        if not d or Path(d).is_absolute() or any(p.startswith('.') or p == '..' for p in Path(d).parts) or ':' in d or '\\' in d:
            raise ValueError('Invalid write directory')
    dest = task_dir(args.task)
    dest.mkdir(parents=True, exist_ok=False)
    repo = Path(args.repo).resolve()
    root = repo / '.claude/worktrees' / ('kimi-' + args.task)
    base = git(repo, 'rev-parse', 'HEAD')
    # Never silently omit an uncommitted source snapshot: report it to Lead.
    baseline = git(repo, 'status', '--porcelain=v1', '-uall')
    git(repo, 'worktree', 'add', '--no-checkout', '-b', 'kimi/' + args.task, str(root), base)
    if args.sparse:
        git(root, 'sparse-checkout', 'set', '--no-cone', *args.sparse)
    git(root, 'checkout')
    policy = {
        'worktree': str(root), 'source': str(repo), 'base': base,
        'write_dirs': args.write_dir, 'checks': {},
        'audit': str(dest / 'audit.jsonl'),
        'unity_url': 'http://127.0.0.1:8080/mcp',
        'unity_instance': args.unity_instance, 'unity_project': str(repo),
    }
    save(dest / 'policy.json', policy)
    (dest / 'source-status-before.txt').write_text(baseline, encoding='utf-8')
    config = root / '.kimi-code'
    config.mkdir()
    save(config / 'mcp.json', {'mcpServers': {'codex_gateway': {'command': str(PYTHON), 'args': ['-X', 'utf8', str(HERE / 'gateway.py'), str(dest / 'policy.json')]}}})
    # Worktree-local excludes: never hide these paths in other worktrees.
    git(root, 'config', 'extensions.worktreeConfig', 'true')
    excludes = dest / 'exclude'
    excludes.write_text('.kimi-code/\n', encoding='utf-8')
    git(root, 'config', '--worktree', 'core.excludesFile', str(excludes))
    print(json.dumps({'task': args.task, 'policy': str(dest / 'policy.json'), 'worktree': str(root), 'base': base, 'uncommitted_source': bool(baseline)}, indent=2))


def run(args):
    dest = task_dir(args.task)
    policy = json.loads((dest / 'policy.json').read_text(encoding='utf-8'))
    lock = dest / 'running.lock'
    # Exclusive creation prevents two processes continuing the same conversation.
    fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    os.close(fd)
    try:
        session_file = dest / 'session.json'
        if args.resume and not session_file.exists():
            raise ValueError('No stored session to resume')
        if not args.resume and session_file.exists():
            raise ValueError('Session already exists; use --resume')
        root = Path(policy['worktree'])
        if git(root, 'rev-parse', 'HEAD') != policy['base']:
            raise ValueError('Worker HEAD moved; Lead must inspect it before continuing')
        prompt = Path(args.prompt).read_text(encoding='utf-8')
        prompt = f"Base: {policy['base']}\nAllowed write directories: {policy['write_dirs']}\n" + prompt
        (dest / 'current-task.txt').write_text(prompt, encoding='utf-8')
        command = [str(KIMI), '--output-format', 'stream-json']
        if args.resume:
            session = json.loads(session_file.read_text(encoding='utf-8'))['id']
            command += ['--session', session]
        else:
            command += ['--agent-file', str(HERE / 'kimi-worker.md')]
        # Windows has a command-line length limit. Full briefs travel via MCP.
        command += ['--prompt', 'Codex has assigned a task or review update. Call mcp__codex_gateway__task_context now to read the full brief, then execute it and return your report.']
        env = os.environ.copy()
        env['KIMI_CODE_TRUST_WORKSPACE'] = '1'
        stamp = time.strftime('%Y%m%d-%H%M%S')
        log = dest / (stamp + '.jsonl')
        err = dest / (stamp + '.stderr.txt')
        with log.open('w', encoding='utf-8') as output, err.open('w', encoding='utf-8') as errors:
            process = subprocess.Popen(command, cwd=root, env=env, stdout=output, stderr=errors)
            try:
                returncode = process.wait(timeout=args.timeout)
            except subprocess.TimeoutExpired:
                # Stop only this owned process tree, including its MCP child.
                subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
                process.wait(timeout=15)
                returncode = 124
        # The stream resume hint is authoritative; do not guess the session ID.
        session_id = json.loads(session_file.read_text(encoding='utf-8'))['id'] if args.resume else None
        for line in log.read_text(encoding='utf-8').splitlines():
            try:
                msg = json.loads(line)
            except ValueError:
                continue
            if msg.get('type') == 'session.resume_hint':
                session_id = msg['session_id']
        if session_id:
            save(session_file, {'id': session_id, 'agent': 'codex-worker'})
        print(json.dumps({'exit_code': returncode, 'session': session_id, 'log': str(log), 'stderr': str(err)}))
        if returncode:
            raise SystemExit(returncode)
    finally:
        lock.unlink()


def review(args, fail_on_reject=True):
    dest = task_dir(args.task)
    policy = json.loads((dest / 'policy.json').read_text(encoding='utf-8'))
    root = Path(policy['worktree'])
    tracked = git(root, 'diff', policy['base'], '--name-only').splitlines()
    untracked = git(root, 'ls-files', '--others', '--exclude-standard').splitlines()
    paths = sorted(set(tracked + untracked))
    outside = [p for p in paths if not any(p.startswith(d.rstrip('/') + '/') for d in policy['write_dirs'])]
    result = {
        'base': policy['base'], 'head': git(root, 'rev-parse', 'HEAD'),
        'changed_paths': paths, 'outside_scope': outside,
        'diff_check': git(root, 'diff', '--check'),
        'sha256': {p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in paths if (root / p).is_file()},
        'decision': 'REJECT' if outside or git(root, 'rev-parse', 'HEAD') != policy['base'] else 'AWAITING_CODEX_REVIEW',
    }
    save(dest / 'review.json', result)
    print(json.dumps(result, indent=2))
    if result['decision'] == 'REJECT' and fail_on_reject:
        raise SystemExit(1)
    return result


def decide(args):
    result = review(args, fail_on_reject=args.decision == 'accept')
    dest = task_dir(args.task)
    decision = {'decision': args.decision, 'reason': args.reason,
                'review': result, 'recorded_by': 'Codex',
                'time': time.strftime('%Y-%m-%d %H:%M:%S')}
    save(dest / 'decision.json', decision)
    print(json.dumps({'decision': args.decision, 'record': str(dest / 'decision.json')}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subs = parser.add_subparsers(dest='action', required=True)
    p = subs.add_parser('create')
    p.add_argument('task')
    p.add_argument('--repo', default='.')
    p.add_argument('--write-dir', action='append', required=True)
    p.add_argument('--sparse', action='append')
    p.add_argument('--unity-instance', required=True)
    p.set_defaults(func=create)
    p = subs.add_parser('run')
    p.add_argument('task')
    p.add_argument('--prompt', required=True)
    p.add_argument('--resume', action='store_true')
    p.add_argument('--timeout', type=int, default=600)
    p.set_defaults(func=run)
    p = subs.add_parser('review')
    p.add_argument('task')
    p.set_defaults(func=review)
    p = subs.add_parser('decide')
    p.add_argument('task')
    p.add_argument('decision', choices=['accept', 'reject'])
    p.add_argument('--reason', required=True)
    p.set_defaults(func=decide)
    args = parser.parse_args()
    args.func(args)


if __name__ == '__main__':
    main()
