# Codex and Kimi in RAID / underwater-fishing

Codex is Lead Developer and architect. Kimi is Junior Developer, invoked by Codex
through `Tools/AgentCollaboration/lead.py`. Read that directory's README.md before
delegating. Use the pinned Kimi 2.8.1 runner (2.8.0 failed session continuation).

- Codex writes bounded tasks and acceptance criteria, reviews actual diffs/new files,
  sends corrections in the same Kimi session, and integrates only accepted results.
- Every modifying Kimi task gets a separate `kimi/<task>` branch and Git worktree,
  explicit allowed directories, a fixed base SHA and the restricted worker profile.
- Never give Kimi merge/push access or an unrestricted shell by default. Kimi cannot
  change main. Integration and GitHub writes remain Codex's responsibility.
- Preserve existing dirty user work. Worker worktrees start from committed HEAD and
  do not include uncommitted integration changes; account for that in every task.
- Parallel tasks need disjoint file ownership. Integrate and validate one at a time.
  Do not open two Unity Editors on one checkout or share Library across worktrees.
- The live integration Unity Editor and Blender belong to Codex. Kimi gets Unity
  read queries through the scoped gateway. Unity mutations, imports, builds and
  test runs are serialized by Codex. Reading live Unity does not validate worker edits.
- Keep OAuth, tokens, task policies, briefs, transcripts and generated worktrees out
  of version control. Local runtime state: `%USERPROFILE%/.local/share/codex-kimi/`.
- Infrastructure setup alone does not authorize new gameplay or technology-tree work.

Existing project conventions in CLAUDE.md remain relevant to game development.
