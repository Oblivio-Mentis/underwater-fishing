# Steam prototype ? 2026-11-18
- https://partner.steamgames.com/doc/sdk/api/example ? Spacewar AppID480, steam_appid.txt for direct development launch.
- https://partner.steamgames.com/doc/store/testing ? own AppID testing before release; public store page and assigned AppID are different things.
- https://partner.steamgames.com/doc/api/ISteamMatchmaking ? lobby metadata and callbacks, owner transfer.
- https://github.com/FirstGearGames/FishySteamworks ? two devices/accounts; local host+client may share one process.
- https://github.com/rlabrecque/Steamworks.NET/tree/2025.165.0 ? package-matched DLLs, InitEx / RunCallbacks / Shutdown.
- Friends-only5 lobby, project+protocol metadata; foreign/mismatched lobby rejected, original host departure ends session.
- AppID480 is development only, not a store registration. Both testers launch this build manually before invites; direct Lobby ID works without overlay.
- Full Steam test NOT RUN until second participant available.
