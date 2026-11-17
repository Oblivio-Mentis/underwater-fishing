# Expedition — 2026-11-18

- FishNet owner commands / authoritative replies: https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls
- Installed SyncVar/SyncList sources: phase snapshots and consent list support late observers; client cannot submit crew counts or final phase.
- Pure model takes confirmed alive/aboard/disconnected facts. Normal return waits for all living crew aboard or explicitly consenting. Dead/disconnected crew do not block the vote.
- Timeout and stalled-cargo watchdog must terminate independently of votes. Only completion of an in-flight expedition increments day.
- Lifecycle callback order matters: publish new phase before events, apply equipment before server relocation/refill, clear loose objects before return relocation.
- Reused scene coordinates host two real static environments: cave at base, wreck during expedition. NPC/loot spawn only in expedition; debug gyms intentionally bypass phase orchestration.
- Failure reset must remain callable by authenticated owners even when their diver cannot use a live terminal. Never require a living crew member to escape a defeated run.
- Runtime checks passed real owner movement/terminal start, pending water vote/consent and timeout emergency; both observed exactly one completed day.
