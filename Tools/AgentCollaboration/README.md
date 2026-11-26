# Codex leads Kimi

Codex owns architecture, task scope, review, tests in the integration Editor, and integration.
Kimi implements bounded tasks in its own worktree and returns results to Codex.
Do not start gameplay or technology-tree work merely because this infrastructure exists.

## Installed runner

Run `powershell -File Tools/AgentCollaboration/setup.ps1` to reproduce the local installation.
Kimi 2.8.1 lives outside the repository in `%USERPROFILE%/.local/share/codex-kimi/bin/2.8.1/`.
The user's original Kimi binary and OAuth configuration are preserved. The runner uses
the existing `%USERPROFILE%/.kimi-code/` profile. No keys are copied into this repository.
`CODEX_KIMI_EXE` can override the executable explicitly. MCP uses one shared Python venv
under the same storage root, with `mcp==1.30.0`.

## Task lifecycle

1. Inspect the integration checkout and record its current HEAD and dirty files.
   Worktrees contain committed HEAD, **not** uncommitted integration changes. Commit only
   reviewed changes explicitly or provide a reviewed context snapshot before delegating
   work that depends on uncommitted edits. Never blanket-stage the user's dirty checkout.
2. Find the live Editor ID using `probe_mcp.py` with the collaboration venv.
3. Create a task, using a unique lowercase ID and its allowed output directories:

   ```powershell
   python Tools/AgentCollaboration/lead.py create task-id --write-dir Assets/_Game/AssignedArea --unity-instance "RAID-Integrated@<actual-hash>"
   ```

   Optional repeated `--sparse /path/` flags produce a small research worktree.
   Omit sparse mode for implementation needing the complete source tree. Never copy
   `Library`, `Temp`, SDKs, credentials, or player profiles into worker worktrees.
4. Write the brief to a local text file: purpose, base, relevant documents, allowed
   files, requirements, acceptance criteria, approved checks, exclusions, report format.
   Store briefs and logs outside the repository. Start the worker:

   ```powershell
   python Tools/AgentCollaboration/lead.py run task-id --prompt C:/path/task.txt
   python Tools/AgentCollaboration/lead.py review task-id
   ```

   Full briefs travel via `task_context` MCP, avoiding Windows command-line limits.
   The JSONL result includes tool outputs and a session ID; logs, policies, hashes,
   reviews and briefs live in `%USERPROFILE%/.local/share/codex-kimi/tasks/<id>/`.
   Never expose these local logs without checking them for project-specific data.
5. Read the actual diff **and new files**, inspect test evidence, compare actual changes
   against the task, and return review comments without involving the user:

   ```powershell
   python Tools/AgentCollaboration/lead.py run task-id --resume --prompt C:/path/review.txt
   python Tools/AgentCollaboration/lead.py review task-id
   ```

   One task lock prevents two simultaneous runs in the same session. Timeouts stop the
   owned Kimi process tree. A stale lock after a killed lead process requires checking
   that its worker is stopped before removing it. A failed run is never acceptance.
6. `AWAITING_CODEX_REVIEW` is only a scope check; it is **not** acceptance. After reading
   the actual result, Codex records the decision with
   `lead.py decide task-id accept --reason "Reviewed diff and checks"` (or `reject`).
   This records current file hashes and never integrates anything. Codex stages only approved worker files,
   commits on `kimi/<id>`, reviews the resulting commit, then integrates one task at a
   time into the intended development branch and checks Unity compilation/tests.
   The runner never commits, merges, pushes, or alters `main` for Kimi.
   If the integration checkout contains overlapping dirty work, defer integration or
   use a clean integration worktree. Do not overwrite or stash unrelated user changes.
7. Before cleanup, retain necessary reports/diffs/hashes outside the worktree, check
   the exact absolute path and junctions, check no worker/Editor uses it, then remove
   that worktree with Git and delete only its disposable task branch. Never prune
   unrelated worktrees or delete user data. Keep reusable infrastructure and evidence.

## Enforced worker tools

`kimi-worker.md` exposes only `mcp__codex_gateway__*`; native Bash, Read/Write, Agent,
direct UnityMCP and other MCP tools are outside the profile. Kimi's internal tool
discovery may expose `select_tools`; it does not grant arbitrary tool execution.
`gateway.py` independently enforces the task policy stored outside the worktree:

- Read project text, list tracked paths, write UTF-8 files under approved directories.
  Traversal, hidden/config paths, alternate streams, symlinks, junctions and hardlinks
  are rejected. Binary authoring is not provided by this initial gateway.
- Run fixed terminal checks: Git status, whitespace diff check, remote HEAD, JSON
  validation. Codex may add exact argument arrays to the local policy's `checks` map
  after reviewing the executable/script; these commands are trusted Lead extensions,
  not a sandbox for arbitrary code. Arbitrary shell commands are unavailable.
  Child commands receive closed stdin (never the MCP protocol pipe), use the verified
  direct network route, and have a timeout that terminates their complete process tree.
- GitHub GET for this repository's metadata, branches and five recent commits.
  Existing Windows Git Credential Manager credentials are used only in memory; no
  token is returned to Kimi. No Git writes, PR creation, merge or push are exposed.
- Unity MCP reads of project/version, Editor state, scene/hierarchy, console,
  test inventory, prefab listing and prefab hierarchy/components. The live Editor
  instance and actual project root are checked on every request. A removed/restarted
  Editor may require updating the policy's instance ID, then retrying the task.
- Unity documentation lookup and public Context7 resolution/documentation queries.
  The gateway uses direct HTTP connections to its fixed MCP endpoints to avoid the
  malformed inherited IPv6 proxy setting seen in Kimi's child process environment.

The profile/gateway is a task boundary, not a separate Windows account or VM. Do not
give unreviewed worker-produced scripts unrestricted execution through `checks`.
The configured global Unity MCP may still connect during startup; the worker profile
does not expose its tools. Launch Kimi through this runner, not an unrestricted chat.

## Unity ownership and parallelism

The integration Editor belongs exclusively to Codex. Kimi's read-only Editor queries
describe that live checkout; its file changes belong to a different committed worktree.
Do not claim that reading the Editor validates unintegrated worker changes.

Codex and Kimi can work concurrently on disjoint tasks/files. Codex schedules ownership
of shared C# files, `.meta` GUIDs, scenes, prefabs, settings and package manifests; reviews
and integrates one task at a time. Worker text edits to scenes/assets require assigning
those paths and preserving YAML/GUID consistency; live Editor mutations are unavailable
to Kimi. Do not open two Editors on the same project directory, share Library caches,
or allow two agents to mutate one live Editor. If future work needs a worker-owned
Editor, use a separate full worktree, separate Library and explicit instance routing;
that mode has not been configured or tested here.

Unity tests/builds/imports run through Codex in an exclusive integration phase. Kimi can
run reviewed headless checks via the policy; starting Unity on the integration checkout
as a headless check is forbidden. Live Blender is likewise owned by Codex.

## Infrastructure checks

```powershell
& "$env:USERPROFILE/.local/share/codex-kimi/venv/Scripts/python.exe" Tools/AgentCollaboration/test_gateway.py -v
```

See `AUDIT-2026-11-26.md` for measured results and limitations.
