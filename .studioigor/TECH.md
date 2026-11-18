# Technology
ENGINE: unity
ENGINE VERSION: 6000.5.6f1
DIMENSION: 3D
LANGUAGE: C#
PLATFORMS: Windows x64 / Steam
## Decision
User explicitly selected existing Unity project and mandatory stack in Docs/BRIEF.md.
URP17.5.0, Input1.20.0, uGUI2.5.0, TestFramework1.7.0 retained.
FishNet4.7.4 official pinned commit + Multipass/Tugboat/FishySteamworks4.1.1.
UniversalFSM0.2.0 + Presentation0.3.2; Steamworks.NET2025.165.0.
AppID480 development only; no store registration/publishing performed.
## Budgets
Target60fps for small greybox; hardware/performance acceptance pending.
## Project structure
Assets/_Game separates Network, Player, Fishing, Progression, Threats, Systems, data and Editor authoring.
Docs/BRIEF.md is authoritative specification. SampleScene preserved.
