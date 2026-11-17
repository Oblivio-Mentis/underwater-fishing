# Pinned source

- Repository: https://github.com/FirstGearGames/FishySteamworks
- Commit: `0320459da446a95fc109aef66d42e1337e3b5fff`
- Sources: `FishNet/Plugins/FishySteamworks/FishySteamworks.cs` and `Core/`, with original Unity metadata and license.
- SteamManager example omitted; the game owns Steam API lifetime.

Local integration changes:

- Added a runtime assembly definition for Unity Package Manager compilation.
- Guard the unused transport's Update before initialization so LAN sessions do not require Steam.
- Keep maximum-client configuration when it is set before socket initialization or before hosting.

Steamworks.NET and FishNet revisions are pinned by the root package manifest and lock file.
