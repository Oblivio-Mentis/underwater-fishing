# Test results — 2026-11-18

| Check | Result | Evidence / limit |
|---|---|---|
| Initial audit through live Unity MCP | PASS | Unity6000.5.6f1, URP17.5, template scene3 roots; Docs/PROJECT_AUDIT.md |
| FishNet and mandatory packages import/compile | PASS | Live Console query after imports:0 errors |
| Foundation Windows x64 development build | PASS | MCP build-2bd72b0dac,76.08s,164.81MB,0errors/0warnings |
| Foundation Windows host + Editor client | PASS | Live client scene:owner0 local=False and owner1 local=True,2divers,O2=120; Logs/foundation-host.log |
| Foundation render capture | PASS | .studioigor/captures/foundation/host.png inspected; label scale subsequently adjusted |
| Progression EditMode | PASS | Job ba2830db0a914be3acb7d7fee216e8df:19/19 passed,0failed,0skipped |
| Full gameplay / final build | NOT RUN | Integration in progress |
| Five-player acceptance | NOT RUN | Pending integrated build |
| Steam between two devices/accounts | NOT RUN | Second participant available later per user |
| User playtest / fun / incremental measured ratio | NOT RUN | Requires gameplay review |

Foundation executable SHA256:95D0C3362277726ADCE36509ACE867138EDB472DA0D02DB64EB4BEEAFF0C29BC.
Passing rule tests is not evidence of full scene gameplay. Later checks are recorded below when actually run.
