# Environment
VERIFIED: no
## Installed
Unity6000.5.6f1 at C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe.
Unity MCP already installed and live; used for editor/scene/build/test/console operations.
Git local only. Mandatory packages pinned in Packages manifest+lock.
## Commands
RUN: powershell -File Tools/Run-Prototype.ps1
TEST: Unity MCP run_tests(mode=EditMode, assembly_names=[Raid.Progression.Tests])
BUILD: Unity MCP manage_build(action=build, target=windows64, scenes=[Assets/_Game/Scenes/FirstPlayable.unity])
CAPTURE: Unity MCP manage_camera(action=screenshot, output_folder=.studioigor/captures/playable)
EDITOR: open existing RAID project in Unity Hub
## Verification note
Actual tool-run results live in Docs/TEST_RESULTS.md. VERIFIED remains no because studio.py verify shell adapter is not configured; this does not replace actual editor checks.
