# RAID-017 · Level design — two depths, two compositions

All coordinates are world metres (x, z); floor heights come from `Seafloor(deep, x, z)`, the same
interpolated field as the collision mesh. Placement is deterministic: fixed tables plus the seeded
`System.Random(17291)`; no time or frame input. Nothing is in the scene files — `NereidReef` builds it.

## Depth I — The Pale Shoals (lit shelf, long sightlines)

**Route.** Hoist pad (23,-28) → sand runway bearing SE → limestone gate → far pocket (46,-53) →
wreck basin. The runway is a 0.3 m trough with 0.45 m shoulders, tinted `PathSand`, kept clear of
kelp, rubble and rock for 2.7 m either side of the centreline. Camera eye height −13; the gate
crown sits near y −3.5, so it reads as a gate ~28 m from the hoist.

**Branches.** N — Sand Slab (39,-24.5) and the survey skiff (24.5,-16.4). E — Pinnacle (49.5,-43),
east berth (52,-38), kelp grove (44,-33). W — Tilted Table (22,-46), west benches, survey beacon.
The trail itself continues past the far pocket to the submerged wreck (47.5,-68).

**Landmarks (named, three silhouettes).**
| name | at | form |
|---|---|---|
| Sand Slab | (39,-24.5) | 9.5 × 6.2 × 4.2, flat bedded top, slight tilt |
| Tilted Table | (22,-46) | 7.4 × 5.4 × 4.8, high edge leans over the lane |
| Pinnacle | (49.5,-43) | r 2.6, h 12, seven-sided spire with bedding bands; shoulder (54,-42.5) |
| Gate | (33.7,-47)–(53.5,-49.6) | thickened 2.3–2.75 m span, 11 m clearance over the lane |

**Fishing pockets.** (23,-28) unchanged — still flattened by `FishingFloorMask`. (34,-33) and
(46,-53) are flattened by `LandingPadMask` (flat to 6.2 m radius, blended out by 9.8 m) so two
harpoon lines share the floor; props, rubble and kelp skip these circles.

**Risk zones.** Open water above the runway (threat routes untouched); the escarpment gap behind
the wreck basin; the deep water past the gate.

**Way back.** Two amber work lamps (27.8,-37.2) and (38.5,-50.5); survey beacons keep amber lenses
on their first two posts; sun shafts land on the runway at (30.5,-34.5), (36,-41), (42,-47).

## Depth II — The Needle Rift (vertical, dark, local light)

**Route.** Hoist → lip pad (34.5,-39.5) → west-bank descent → berth terrace → throat drop → Lantern
Shelf → fissure. Level shapes: lip ≈ −17.5, terrace ≈ −17.5, throat floor ≈ −31…−37, shelf ≈ −22.5.

**Needle clusters (gaps 3–5 m, swim-through).**
| cluster | needles | slots |
|---|---|---|
| West Comb | (27.5,-41) (30,-47) (26.5,-52) (34,-53) | 3.0–3.8 m, tops below the fish lanes |
| East Palisade | (58,-52) (57.5,-43) (55.5,-60.5) | 4.2–4.5 m, cold light shafts (56.2,-56) etc. |
| Throat Gate | (39.5,-61.5) + (48.5,-60.5) | 5.0 m slot over the descent |
| Fallen column | (40.2,-57.6) → (34.2,-53.4) | closes the west side of the throat |
| North doorpost | (39.5,-28.5) | frames the first look east from the hoist |

**Terraces.** Berth terrace: causeway spur (44.6,-55.4)–(50.2,-49) at bed height, hex columns on
the rim, lanterns on the capitals. East Step (52.6,-38.4) — a lower shelf under the (52,-38) berth.
Lantern Shelf (37.5,-67), a 5.2 m-deep terrace cut into the throat's west bank, lit like a camp.

**Far landmark.** Beacon spire (53,-47.5), r 1.5, h 8, lantern crown at its top plus two foot
colonies — the one glow the hoist looks toward, ~34 m out. Threshold colonies (31.5,-43.2),
(40.6,-30.2), (38.2,-50.2) carry the eye from near to far.

**The pale cylinder is gone.** The (46,-53) berth island stood at bed height inside the ravine.
The causeway now makes it a spur off the east bank, edged with hex columns and lanterns; the
ravine surface it stood in is broken into the eastern wall, not a free-standing cylinder.

**Fishing pockets.** Lip pad (34,-33) and the causeway (46,-53), flat 6.2 m circles; the causeway
also holds the salvage berth.

**Way back.** Nine survey beacons (cyan, amber lens on the hull side) rethreaded through the
descent: (26,-29) (32,-36) (38.5,-44) (43,-51) (46.5,-58) (48.2,-64.5) (46.5,-74.5) (45,-83)
(45.5,-91); seven amber/cyan work lamps fill the throat and shelf. Longest stretch of route
without a light: 10.3 m. Beacons root on the terrain and their lenses cannot be occluded by the
new masses (checked against every needle footprint).
