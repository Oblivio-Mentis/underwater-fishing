# Pinned source

- Repository: https://github.com/FirstGearGames/FishySteamworks
- Commit: `0320459da446a95fc109aef66d42e1337e3b5fff`
- Sources: `FishNet/Plugins/FishySteamworks/FishySteamworks.cs` and `Core/`, with original Unity metadata and license.
- SteamManager example omitted; the game owns Steam API lifetime.

Local integration changes:

- Added a runtime assembly definition for Unity Package Manager compilation.
- Guard the unused transport's Update before initialization so LAN sessions do not require Steam.
- Keep maximum-client configuration when it is set before socket initialization or before hosting.
- Append the Steam channel byte correctly for offset and full-capacity packet segments; always release pinned send buffers.
- Drop empty, oversized, and invalid-channel received messages before dispatch; release native messages even when decoding fails.
- Clean up failed starts and all server connection maps, pending work, local packet buffers, and native sockets before reuse. Defer shutdown during receive enumeration and filter callbacks by the owning socket.
- Replace the connection-timeout worker and thread abortion with a 30-second deadline polled on the main thread. Unity's `OnDestroy` owns transport shutdown; no finalizer calls Steam.
- Regression coverage lives in `Assets/_Game/Network/Steam/Tests` in the consuming project. These checks use native-close test doubles and do not establish real Steam connectivity.

Steamworks.NET and FishNet revisions are pinned by the root package manifest and lock file.
