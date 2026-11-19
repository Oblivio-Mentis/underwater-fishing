# Design - Underwater Fishing
## Mechanics
| id | mechanic | tier | pillar | status | scene | notes |
|---|---|---|---|---|---|---|
| M01 | server swimming / vitals | mvp | authority | playtest | FirstPlayable | local host/client; user feel pending |
| M02 | harpoon / tension / help | mvp | active fishing | playtest | FirstPlayable | RPC/FSM capture exercised; human co-op pending |
| M03 | cargo / repeatable hoist | mvp | delivery | playtest | FirstPlayable | default17.13s observed,12deliveries in isolated probe |
| M04 | process/feed | mvp | choice | playtest | FirstPlayable | single consumption rules and live commands |
| M05 | upgrades / Depth2 | mvp | incremental | playtest | FirstPlayable | Reel/Fins/Hull purchased with actual catches in probe |
| M06 | hunger / reservoir waves | mvp | local risk | playtest | FirstPlayable | fixture stock10 to7; upgrades/lifetime preserved |
| M07 | predator / rescue | mvp | cooperation | gym | FirstPlayable | manual multiplayer feel pending |
| M08 | host persistence | mvp | permanent tech | playtest | FirstPlayable | checksum/atomic save tests, diskHull1 observed |
| M09 | Steam lobby / transport | mvp | cooperation | gym | FirstPlayable | two-account test deferred by user |
## Game designer ideas
No new hooks authorized; implement explicit brief first.
## Mechanic specs
See Docs/BRIEF.md sections3-12. Tuning in Assets/_Game/Data assets; no shop currency or reset.
## Changelog
2026-11-19 · По итогам плейтеста утвердили тон тёмной промысловой экспедиции. Приближение хищника читается по его позе и поведению, экипаж ориентируется по рельефу и рабочему свету.
2026-11-18: adopted supplied brief, Steam prioritized by explicit user request.
