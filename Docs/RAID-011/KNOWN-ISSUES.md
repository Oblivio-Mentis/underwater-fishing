# Existing gameplay issue, separate from RAID-011

`Assets/_Game/Systems/LootActor.cs`, `LateUpdate`: collider availability uses `carrier == null`, but `carrier` is server-local. On a remote client, carried technical cargo can retain its collider. This logic predates base `2c6274b`; RAID-011 adds a replicated carrier ID for truthful UI but does not rewrite this collider rule. Source-level finding, not claimed as a reproduced runtime collision incident. A separate gameplay fix should use confirmed carrier state and verify pickup/carry/drop on host and client.
