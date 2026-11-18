# Stack research ? 2026-11-18
- User mandates existing Unity6000.5.6f1 + URP + FishNet + both FSM packages.
- FishNet docs: https://fish-networking.gitbook.io/docs/ ; https://github.com/FirstGearGames/FishNet
- PlayerSpawner and Multipass inspected in installed source. Direct replacement of Transport loses initialization/event wiring, so select client/server using Multipass.
- FishySteamworks official README requires two devices/accounts for two Steam clients: https://github.com/FirstGearGames/FishySteamworks
- Official release 4.1.1 imported through AssetDatabase.ImportPackage; Git UPM had no transport source: https://github.com/FirstGearGames/FishySteamworks/releases/tag/4.1.1
- Unity APIs checked using live unity_reflect / unity_docs and installed sources.
