# Cargo — 2026-11-18

- FishNet SyncList implementation: pinned installed `Runtime/Object/Synchronizing/SyncList.cs`; supports IReadOnlyList and serializes full initial values for late observers.
- FishNet RPC ownership and server validation: https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls
- UniversalFSM pinned runtime/schema5: explicit domain metadata on each winch state, transitions outside Tick, large server timestep carries remaining time into next state.
- Cargo identity must outlive network object despawn and object-ID reuse. Assign an authority-only run sequence; keep an accepted-ID set after consumption/loss.
- Precheck capacity before taking an owner's object, then synchronously transfer once and despawn. No asynchronous gap between ownership removal and manifest insert.
- Protected hold receives cargo at the end of Unloading; repeated lift/reset clears only unprotected load. Reserved fish cannot be processed; processed/offered IDs cannot be reused.
- Actual two-process check passed two complete winch cycles with unique items and retained hold; fish delivery is checked separately.
