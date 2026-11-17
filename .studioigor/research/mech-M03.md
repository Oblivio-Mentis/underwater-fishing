# M03 — fishing research, 2026-11-17

- Primary sources: installed pinned FishNet4.7.4, UniversalFSM0.2.0 and Presentation0.3.2 sources under Library/PackageCache.
- https://github.com/Oblivio-Mentis/Unity-UniversalFSM-Public — schema5, Main layer, typed private registry.
- Runtime rejects forced transitions inside Tick: queue state intent, apply immediately after Tick.
- Each concrete state requires UniversalFsmStateMetadata("Fishing"); inherited metadata is disabled. Verified by actual Unity tests.
- https://github.com/Oblivio-Mentis/Unity-UniversalFSMPresentation-Public — bind active driver/profile to confirmed replicated layer-state adapter; cue SFX/flash never mutate simulation.
- https://github.com/FirstGearGames/FishNet — owner ServerRpc input, server raycast and unique fish holder; client SyncVar snapshots.
- Tether uses actual extension and separating speed; reeling shortens length; bounded forces affect both bodies; helpers cap at2.
- Measured small-fish working loads0–0.06 of rope threshold made original0.12 fatigue gate unreachable.
- Tuned small threshold0.005, sqrt work response, stamina6, recovery0.15; true slack still cannot tire fish. Real remote capture passed.
- Unity SphereCollider uses maximum nonuniform scale axis; static cave ellipsoids require mesh geometry. Fish sweep uses actual capsule extents.
- Windows input bots exercise normal movement/fire/reel/help/pickup RPCs; no teleports or forced capture states.
