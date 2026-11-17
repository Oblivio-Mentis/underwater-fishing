# Cooperation — 2026-11-17

- FishNet owner ServerRpc: https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls
- Unity raycast/LOS: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Raycast.html
- Steam lobbies are discovery; gameplay needs its own transport connection: https://partner.steamgames.com/doc/features/multiplayer/matchmaking
- Installed source audit: FishySteamworks reusable sockets retained connection maps; channel-tail resize lost the resized array; client timeout mixed seconds and milliseconds and touched APIs from a worker. Fix before treating Steam as accepted.
- Rescue: owner sends held E/F, server chooses target by distance, look and LOS; progress resets when eligibility breaks. One emergency charge, refilled aboard.
- Trap: server pins movement, oxygen keeps draining and look remains available; a separate active player holds E. Visual debris lifts before movement is released, without a blocking slab collider that can trap the capsule afterward.
- Death: observer camera follows a surviving crew member until return; no underwater respawn.
- Development actors use ordinary movement and action RPCs. Fast oxygen config changes duration, never sets life/rescue outcomes directly.
