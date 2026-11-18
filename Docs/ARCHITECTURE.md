# Architecture

- FishNet listen server owns movement, vitals, fishing, cargo, stock, upgrades, feeding and save state. Owned input RPCs submit intent; world commands validate sender, state, distance and material costs.
- Multipass initializes Tugboat and FishySteamworks once; a session starts only its selected server transport. SteamLobby handles matchmaking and explicit host-loss termination, without host migration.
- DiverMotor simulates CharacterController on the server and replicates one snapshot. Owner camera reads Input Actions; remote positions interpolate.
- Fishing uses logical tension and one client LineRenderer. Fish AI runs UniversalFSM only on the server; presentation reads confirmed state.
- RaidWorld joins game systems. TeamProgress is pure host-owned rules; client snapshots are read-only UI data. Delivered catch has persistent unique identity and single consumption.
- TeamSave uses a versioned JSON envelope, checksum and atomic replacement. Corrupt saves are preserved and reported; a temporary session does not overwrite the corrupt file.
- User SampleScene is retained. Foundation/FirstPlayable are separate scenes. Generated assets and tuning are under Assets/_Game.
