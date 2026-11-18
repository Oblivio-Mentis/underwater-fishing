# Test results — 2026-11-18

| Check | Result | Evidence / limit |
|---|---|---|
| Initial audit through live Unity MCP | PASS | Unity6000.5.6f1, URP17.5, template scene3 roots; Docs/PROJECT_AUDIT.md |
| FishNet and mandatory packages import/compile | PASS | Live Console query after imports:0 errors |
| Foundation Windows x64 development build | PASS | MCP build-2bd72b0dac,76.08s,164.81MB,0errors/0warnings |
| Foundation Windows host + Editor client | PASS | Live client scene:owner0 local=False and owner1 local=True,2divers,O2=120; Logs/foundation-host.log |
| Foundation render capture | PASS | .studioigor/captures/foundation/host.png inspected; label scale subsequently adjusted |
| Progression EditMode | PASS | Job ba2830db0a914be3acb7d7fee216e8df:19/19 passed,0failed,0skipped |
| Integrated loop, isolated live RPC/FSM probe | PASS | 12 catches; actual cargo, process/feed commands, earned Reel/Fins/Hull, Depth2 catch; Validation/playable-loop.log |
| FirstPlayable Windows x64 build | PASS | Latest MCP build-32aee4b74e,13.40s,164.92MB,0errors/0warnings; code cf12346 |
| Final progression EditMode | PASS | 70e1c219408141c3ae846d46af46de65:23/23; Validation/editmode-results.json |
| Five separate network peers | PASS | Tested code f1157de: Editor host +4 Windows clients;5 distinct owners0-4,5divers; Validation/network.log and five-clients.log |
| Sixth connection rejected | PASS | Server remains5peers, extra client disconnects |
| Remote-player rescue | PASS | Host-owned rescue RPC; real remote owner1: O2=0/trapped -> O2=29.06/not trapped after held rescue;5peers connected |
| Two Windows executable peers | PASS | Windows listen host + Windows client, remote IDs0/1; logs/windows-host.log and windows-client.log |
| Saved progression after Windows host restart | PASS | Host loaded isolated test save, subsequent autosave retains Lifetime60,Reel1,Fins1,Hull1,Depth2 |
| Hunger wave stock loss | PASS | Fresh live wave against fixture stock10 ->7; Lifetime60 and all upgrades preserved |
| Native fish FSM graph | PASS | Native validation/build and actual capture;23rendered nodes,22edges,no rectangle overlaps; native render inspected |
| Steam between two devices/accounts | NOT RUN | Second participant available later per user |
| User playtest / fun / incremental measured ratio | NOT RUN | Requires gameplay review |

Foundation executable SHA256:95D0C3362277726ADCE36509ACE867138EDB472DA0D02DB64EB4BEEAFF0C29BC.
Passing rule tests is not evidence of full scene gameplay. Later checks are recorded below when actually run.

Probe limits: player placement and oxygen were controlled, predator paused during fishing isolation. First hoist ran default timers (17.13s); subsequent hoists used a temporary runtime tuning clone (about1.8s), restored to8/3/6seconds afterward. The initial probe timed out; a later probe completed. Range validation correctly rejected a terminal command from the travel spawn; the probe then moved to the terminal. These are integration checks, not a full human playthrough.

Measured small-fish capture samples: beforeReel 13.46/13.35s; afterReel typical8.50–9.80s with one13.70s sample. These controlled samples show an upgrade effect, but do not establish an equal-interval resources/minute balance result. That acceptance measurement remains NOT RUN.

Still NOT RUN: human co-op assistance/feel, two-device Steam lobby/transport, simultaneous competing actions from two human clients, long soak/performance acceptance, complete manual two-player acceptance script. Do not infer those from the smoke tests.

The executable is Unity's bootstrap and has the same hash across builds. Runtime code identity is recorded in Validation/build-dll-hashes.json; latest Assembly-CSharp.dll SHA256:D8DA54E0C438E095D61C18C33A15FDD895F4B120FF777D65C228AA94ADC53CD3.

Local review commits:004cfae Foundation; c3ef3db fishing; 2e8925f progression; a541152 integrated gameplay; f1157de network-tested build; cf12346 latest build with later-tier material costs and23passing tests. Networking code is unchanged by cf12346; its multi-process smoke was not repeated. No remote push or main merge.

Abrupt Windows host loss: PASS, client logged Timeout and an explicit ended-session message. Packaging initially encountered Unity's open data-file lock; closed test processes and successfully packaged afterward. The packaging script now rejects a running build.
