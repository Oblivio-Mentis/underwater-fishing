# RAID-011 UI inventory and implementation contract

Base: `raid/010-visual-slice` @ `2c6274b`. Work: `raid/011-ui-ux`; PR targets the base branch. Original scenes/builds remain available; the UI scene will copy NEREID without rebuilding its world.

| Existing view | Data | Replacement |
|---|---|---|
| RaidSession.OnGUI | Status, Busy, Connected, SteamLobby | Main / connection / real crew slots |
| RaidWorld.OnGUI, RaidStatusHud | confirmed View, tuning, Command | Reservoir scroll list, upgrades, depths, cargo |
| DiverMotor.OnGUI | replicated Oxygen, Depth, Stress | compact left vitals / top depth / crew |
| HarpoonTool.OnGUI | actual hooked FishActor | conditional tension / stamina |
| RescueTool.OnGUI | server-local progress | replicated read-only rescue display |
| ThreatDirector state | WaveActive, Message, stock snapshot | compact feeding / threat alert / loss toast |

uGUI + TextMeshPro + InputSystemUIInputModule, one managed Canvas/EventSystem; PT Sans with Cyrillic (OFL 1.1). Palette: steel #142A34/#1D3740, edge #42616A, text #E5EFF1/#93ADB5, cyan #32B8C5, ready #6CC3A4, caution #E6A64C, danger #D75E58. Flat panels, restrained borders, compact geometric marks, no decorative hotbar/radar for nonexistent mechanics.

`UiKit` builds anchored, layout-driven components. `UiContext` reads spawned client state and confirmed `RaidWorld.View`; roster never uses server-only lists. `IUiController` routes all economic actions through existing server commands and owns confirmation/pending state. `TerminalView`, `HudView`, `MenuView` are independent presentation modules; `UiRoot` manages navigation, focus, settings and lifecycle. Strings live in `UiText` and module text tables for later translation.

Input: one local gate; menu/terminal blocks movement, firing, reel, rescue and world E, without pausing the server. Context priority: rescue F, terminal E, cargo E, loot E, harpoon assistance E, fish aim. Escape/RMB ownership moves to the root only in the new UI scene. Server authority, costs, item identity, colliders and scene IDs remain intact.

Read APIs only where missing: hooked fish; replicated rescue/loot carrier display; Steam initialization/overlay; local sensitivity; command response and travel presentation. No economy/save schema changes. Species names are not persisted, so stock labels honestly identify fish/technical cargo and depth.

Implementation order: foundation → menu/connection → HUD/context → reservoir/cargo → upgrades/depths → feeding/threats → settings/focus/errors → live two-peer and resolution checks. Capture real Play Mode and Windows states at 1920×1080; verify 1280×720, 2560×1440, 3440×1440. Do not mark human or two-account Steam tests passed without execution.
