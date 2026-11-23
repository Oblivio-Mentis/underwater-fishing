# RAID-017 · Asset mapping

Honest inventory of what changed in the game and where it came from. Asset Store packs were **not** available
in this environment: there is no local copy of `Low-poly Industrial Pack`, `Stylized Low Poly Rocks`,
`Fish Alive`, `Pixit3D Fish`, `Fish — PolyPack`, `Low Poly Fish`, `Fish Shader Animation` or `Bubbles VFX — URP`
in the project or on disk. Nothing from them is claimed as imported.

| game object | what it is now | source | licence / fallback |
|---|---|---|---|
| Depth I floor, shelves, arch, coral garden | same procedural meshes, new bedding/caustic/sand response | project code `NereidReef*.cs` + `NereidReef.shader` | project-owned |
| Depth I rocks | Kenney `rock_largeC/smallFlatB/tallH` forms, repainted by vertex colour | Kenney Nature Kit 2.1 (already in RAID-016) | CC0 1.0 |
| Depth II floor, needles, terraces, vents | project procedural basalt, new strata bands | project code | project-owned |
| Survey wreck, expedition wreck | project procedural fuselage | project code | project-owned |
| Kelp banks | project procedural blades + `NereidTallKelp.shader` | project code | project-owned |
| Decorative shoals | Quaternius `Fish1/Fish2`, one instanced draw | Quaternius Animated Fish (already in RAID-016) | CC0 |
| Catchable Sailray etc. | unchanged meshes and hit surfaces, new skin shader | project code | project-owned |
| NEREID hull/interior modules | Quaternius modules placed in RAID-016, revised coating | Quaternius Ultimate Modular Sci-Fi / MegaKit | CC0 |
| Exterior work lamps, hatch guides | project procedural fittings | project code | project-owned |
| Parasite tissue | project mesh, `FactoryLivingTissue.shader` | project code | project-owned |
| Reservoir/pipe glass | project mesh, `FactoryFlowGlass.shader` | project code | project-owned |
| Bubbles, suspension, silt | project particle systems and instanced field | project code | project-owned |
| Gosha | project-authored creature (`art/nereid-creature/`) | project code | project-owned |

## Not done, and why

- **Asset Store models** — require a signed-in Unity ID and a local import; no local sources exist. To use them:
  Window → Package Manager → My Assets → Download → Import in a scratch URP project, then move only the meshes
  and materials into `Assets/_Game/Nereid/Art/` and register the licence line in `Docs/THIRD_PARTY.md`.
- **New catchable-fish meshes** — replacing the Sailray mesh would move the two harpoon impact surfaces and the
  FSM anchors, which RAID-014/016 locked. Kept the existing meshes and improved the skin.
- **Blender-authored props** — no new props were needed beyond the procedural set that already reads in frame.

Fallback for every row above: the procedural/project mesh is used, so the scene never depends on a missing import.
