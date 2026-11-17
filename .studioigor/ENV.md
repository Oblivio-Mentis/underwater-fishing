# Environment — Underwater Fishing
VERIFIED: yes

## Installed
| tool | version | evidence |
|---|---|---|
| Unity Editor | 6000.5.7f1 | ProjectVersion + live MCP |
| Windows x64 support | same Editor | successful BuildPipeline |
| Input System / URP / UI | 1.20.0 / 17.5.0 / 2.5.0 | package lock |
| FishNet | 4.7.4 / pinned SHA | actual Windows 2/5 processes |
| Steamworks.NET / FishySteamworks | pinned SHA / embedded 4.1.1-raid.1 | actual Steam local lobby + host |
| Unity Test Framework | 1.7.0 | 41 EditMode tests passed |

## Agent integrations
Local Unity MCP http://127.0.0.1:8080/mcp; tools/unity_mcp.py stdlib client.
execute_code, scene generation, tests, screenshot and Windows BuildPipeline exercised.

## Commands
RUN: Builds/Windows/UnderwaterFishing.exe
TEST: powershell -NoProfile -ExecutionPolicy Bypass -File tools/Run-LogicTests.ps1
BUILD: powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-Windows.ps1
CAPTURE: powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Fishing.ps1 -Capture -WaitForShutdown
EDITOR: C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe -projectPath C:/Users/evernthi/Documents/RAID

## Pitfalls
- Build/QA facts: docs/RAID-001-REPORT.md and docs/RAID-003-REPORT.md. VERIFIED describes local TEST/CAPTURE, not remote Steam acceptance.
- Windows script policy requires ExecutionPolicy Bypass on these child processes; the machine policy is unchanged.
- Stop Play before editing scripts; queued forced compilation can otherwise reload a running session.
- Real Steam remote acceptance requires different accounts/devices; local process QA uses LAN.
- GitHub-specific SSL backend locally changed to OpenSSL after verified Schannel handshake failures.
