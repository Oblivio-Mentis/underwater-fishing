# Third-party components

| Component | Pin / source | License |
|---|---|---|
| FishNet 4.7.4 | [Official repository](https://github.com/FirstGearGames/FishNet), commit a2e2c3e12f5f4e18c96e402113e14030ae7849f0 | FishNet LICENSE.txt, permits no-charge use in games; bundled dependency licenses apply |
| FishySteamworks 4.1.1 | [Official release](https://github.com/FirstGearGames/FishySteamworks/releases/tag/4.1.1), unitypackage SHA256 5698D16BD29B8B08D35E12A9B817CE69992F70D7C14B64810961691ECD9AFC57 | BSD 2-Clause; notice retained in Docs/Licenses |
| Steamworks.NET 2025.165.0 | [Official repository](https://github.com/rlabrecque/Steamworks.NET/tree/2025.165.0), commit ea51b029a5dbcb47a99d3c1fac930b811672ed32 | MIT wrapper; Valve native SDK remains under Valve terms |
| UniversalFSM 0.2.0 | [Official repository](https://github.com/Oblivio-Mentis/Unity-UniversalFSM-Public/tree/v0.2.0) | MIT |
| UniversalFSMPresentation 0.3.2 | [Official repository](https://github.com/Oblivio-Mentis/Unity-UniversalFSMPresentation-Public/tree/v0.3.2) | MIT |
| Unity URP/Input/uGUI/Test Framework | Versions pinned in Packages/manifest.json and packages-lock.json | Unity package licenses, retained with installed packages |
| Existing Unity MCP | Existing development-only editor integration | Existing package license; not a gameplay dependency |
| Greybox meshes/materials | Created in this project through Unity primitives | Project-authored |

FishySteamworks Git UPM import contained no source code; removed it and imported the official release as documented. No fork or alternate networking framework used. Native Steam DLLs come from the Steamworks.NET package, without duplicate SDK installation.

## RAID-010 visual slice

Hull/interior modules, rocks, kelp, wreck, parasite, fish fins, harpoon, materials and `RAID010/World Text` shader were authored in this project. No external art pack, paid/trial asset, downloaded model or new package was used. The existing Unity font atlas is used by world labels under the installed Unity terms. New materials use the already installed URP 17.5 shaders. No additional third-party art attribution or license grant is required.
