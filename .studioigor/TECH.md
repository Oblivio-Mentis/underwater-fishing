# Technology ? Underwater Fishing
ENGINE: unity
ENGINE VERSION: 6000.5.6f1
DIMENSION: 3D
LANGUAGE: C#
PLATFORMS: Windows x64 / Steam
## Decision
- Existing Unity project and mandatory stack explicitly selected in Docs/BRIEF.md; no engine migration.
- URP 17.5.0, Input System 1.20.0, uGUI 2.5.0, Test Framework 1.7.0.
- FishNet 4.7.4 pinned official commit; Multipass selects Tugboat or FishySteamworks 4.1.1.
- UniversalFSM 0.2.0 / UniversalFSMPresentation 0.3.2.
- Steamworks.NET 2025.165.0; AppID 480 development only, no store publishing.
## Budgets
- [default] Target 60fps, small greybox; measured budgets pending first playable.
## Project structure
- Assets/_Game: Player, Network, Fishing, Progression, Editor, Data, Scenes, Prefabs.
- Docs/BRIEF.md is the approved specification; .studioigor records decisions and playtests.
