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

## RAID-011 interface

| Component | Source / version | License |
|---|---|---|
| PT Sans Regular, unmodified static TTF; generated TMP SDF atlas | [Google Fonts / ParaType](https://github.com/google/fonts/tree/main/ofl/ptsans), TTF SHA256 `9cc831490532009bae2b3ce0d39c62adfc889060beb421593bfd9d2396d0f10a` | SIL OFL 1.1, copyright and full license retained in `Assets/_Game/UI/Overhaul/Fonts/OFL.txt`; permits commercial bundling |
| TMP Essential Resources | Imported from already installed `com.unity.ugui` 2.5.0 package | Unity package terms and included notices; Liberation Sans default font license retained in imported Fonts folder |
| UI panels, status marks and layout | Authored in this project, uGUI Images / TextMeshPro | Project-authored; no external icon pack |

## RAID-011 industrial prototype expansion

The reservoir, pipework, conveyors, processor, material outlets, physical control panels, aft exit, fish presentation meshes, organic mouth and its mounting hardware are authored in this project. `RAID011/Factory Flow Glass` and `RAID011/Living Tissue`, low-amplitude metal/tread textures, bubbles and procedural audio are also project-authored. No additional external 3D model, art pack, texture pack or audio recording was added for this expansion.

The custom URP shaders include the already installed Unity shader libraries; those libraries remain covered by their existing Unity package terms and notices. The PT Sans/TMP resources and notices listed above continue to apply. User-supplied concept images guide the visual direction; they are not imported as game textures or presented as captured gameplay.

## RAID-016 visual art pass

| Component | Use | License |
|---|---|---|
| [Quaternius Ultimate Modular Sci-Fi](https://quaternius.com/packs/ultimatemodularscifi.html) | Processor vent, workshop service plate and airlock ceiling pipes | CC0 1.0, notice in Docs/Licenses |
| [Quaternius Modular Sci-Fi MegaKit](https://quaternius.com/packs/modularscifimegakit.html) | Six selected fittings for NEREID, adapted to the ship palette | CC0 1.0, notice in Docs/Licenses |
| [Kenney Nature Kit 2.1](https://kenney.nl/assets/nature-kit) | rock_largeC, rock_smallFlatB and rock_tallH in the shallow reef and deep rift | CC0 1.0, notice in Docs/Licenses |
| [Quaternius Animated Fish](https://opengameart.org/content/animated-fish) | Fish1 and Fish2 in decorative shoals | CC0 1.0, notice in Docs/Licenses |

The new instrument glyphs, terrain, coral, kelp settings and the adapted bubble surfaces were authored in this project.
