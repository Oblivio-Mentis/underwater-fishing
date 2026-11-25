# Environment
VERIFIED: no
## Installed
Unity6000.5.6f1 at C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe.
Unity MCP already installed and live; used for editor/scene/build/test/console operations.
Git local only. Mandatory packages pinned in Packages manifest+lock.
## Commands
RUN: powershell -File Tools/Run-Prototype.ps1
TEST: Unity MCP run_tests(mode=EditMode, assembly_names=[Raid.Progression.Tests]); Play Mode Gosha: touch .studioigor/gosha-trial.on, enter play, read .studioigor/raid024/hunt.json
BUILD: Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod Raid.Editor.Raid014Validation.Build -raid-exe Builds/<Name>/UnderwaterFishing.exe -raid-flavor release -logFile .studioigor/raid024/build.log (scene InterfaceVerticalSlice)
CAPTURE: Unity MCP manage_camera(action=screenshot, output_folder=.studioigor/captures/playable)
EDITOR: open existing RAID project in Unity Hub
## Verification note
Actual tool-run results live in Docs/TEST_RESULTS.md. VERIFIED remains no because studio.py verify shell adapter is not configured; this does not replace actual editor checks.
