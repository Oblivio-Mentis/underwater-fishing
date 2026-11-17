# Environment — Underwater Fishing
VERIFIED: no

## Installed
| tool | version | evidence |
|---|---|---|
| Unity Editor | 6000.5.7f1 | ProjectVersion + live MCP |
| Windows x64 support | same Editor | successful BuildPipeline |
| Input System / URP / UI | 1.20.0 / 17.5.0 / 2.5.0 | package lock |
| FishNet | 4.7.4 / pinned SHA | actual Windows 2/5 processes |
| Steamworks.NET / FishySteamworks | pinned SHA / embedded 4.1.1-raid.1 | actual Steam local lobby + host |
| Unity Test Framework | 1.7.0 | 23 EditMode tests passed |

## Agent integrations
Local Unity MCP http://127.0.0.1:8080/mcp; tools/unity_mcp.py stdlib client.
execute_code, scene generation, tests, screenshot and Windows BuildPipeline exercised.

## Commands
RUN: Builds/Windows/UnderwaterFishing.exe
TEST: powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Network.ps1 -Players 5
BUILD: Unity menu Underwater Fishing > Build Windows Development
CAPTURE: python tools/unity_mcp.py manage_camera; action=screenshot, output_folder=.studioigor/captures, include_image=false
EDITOR: C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe -projectPath C:/Users/evernthi/Documents/RAID

## Pitfalls
- Build/QA facts: docs/RAID-001-REPORT.md. VERIFIED remains no until full studio.py verify workflow is wired.
- Stop Play before editing scripts; queued forced compilation can otherwise reload a running session.
- Real Steam remote acceptance requires different accounts/devices; local process QA uses LAN.
- GitHub-specific SSL backend locally changed to OpenSSL after verified Schannel handshake failures.