# RAID-017 · Shaders, rendering and grading

All work landed in the existing URP stack (URP 17.5, Unity 6000.5.6f1). No new ocean system.

## Grading states

`Assets/_Game/Nereid/Resources/UnderwaterGrading.asset` (type `Raid.UnderwaterGrading`), applied by
`Assets/_Game/Factory/UnderwaterLook.Ocean.cs`. The asset is the only place to tune the look; the C# defaults
mirror it if the asset is missing.

| state | trigger | fog (ExponentialSq) | ocean fill | exposure | contrast | notes |
|---|---|---|---:|---:|---:|---|
| Dry NEREID | eye inside the hull, not in water | `.011` initial | 1.5 | +.12 | 8 | authored ambience restored |
| Flooding airlock | eye inside the airlock volume | `.030 → .034` | .35 | +.10 | 9 | no sun shafts, no caustics |
| Depth I — shelf | submerged, cruise depth 1 | `.042 → .052` | .20 | −.02 → −.16 | 11 → 13 | sun shafts and soft caustics allowed |
| Depth II — rift | submerged, cruise depth 2 | `.062 → .075` | .05 | +.14 → +.02 | 11 → 13 | no key light: local lanterns, beacons, helmet lamp |

The biggest single fix: the scene's `Ocean reflected fill` (a hard-coded unshadowed directional at 1.5) is now
scaled per state. It was flattening every form in the shallows and lighting the rift from nowhere.

## Shaders touched

| shader | change |
|---|---|
| `NereidWaterColumn.hlsl` | one far-water model for everything: `WaterColumnColor(dir, extinction)` desaturates the tint 28 % toward luminance, darkens downward, lifts upward and adds the sun halo **only when the extinction says Depth I**; new `NereidWaterFade()` decays chroma ~2.6× faster than luminance so backdrop, reef and kelp resolve to one colour; `NereidLuminance`, `NereidDeepWater` helpers |
| `NereidReef.shader` | `rift = _Rift or deep water` removes the sun key, shadow shafts, lit pools and wet specular; a wrap `.40+.36·wrap²` plus a cool top light keep needle crowns; rift basalt: cooler, darker seams, inter-seam bands, dull mineral speck; `_BasaltLayers`/`_StoneArch` bedding strengthened for 3–15 m; sand gets a soft-knee luminance compressor (ceiling ≈ .75) and caustics ×0.5 fading out by 14 m |
| `NereidWaterDistance.shader` | backdrop reads the same extinction → same colour as the fading geometry |
| `NereidLightColumns.shader` | shaft colour → blue-grey `(.30,.60,.72)`, alpha ×0.9; still mesh-only on Depth I |
| `NereidTallKelp.shader` | fades through `NereidWaterFade`, tint pulled to olive/palette |
| `NereidSuspension.shader` | open-water specks ×0.8, helmet beam and work-lamp cones ×1.5 — matter shows up in light, not as snow everywhere |
| `NereidHullCoating.shader` | metre-scale polish mask, grazing-angle edge wear toward bare steel, downward grime, ochre keeps chroma; interior enamel −15 % albedo, −.06 smoothness |
| `NereidFishSkin.shader` | wrapped key, cool dorsal / warm belly from the object normal, two-lobe sheen, fin-edge rim, distance fade via the reef clarity globals |
| `NereidShoal.shader` | same logic per vertex in the instanced path (one instanced draw) |
| `FactoryLivingTissue.shader` | albedo `#3A1220→#5A2036`, darker veins, two dull specular lobes, red wrap; no new emission |
| `FactoryFlowGlass.shader` | face-on opacity ×0.72, Fresnel rim tinted `#32B8C5` |
| `PressureLockWater.shader`, `NereidWorkBeam.shader`, `NereidSilt.shader`, `NereidBubbleShell.shader`, `NereidFishWake.shader` | audited, kept (they already match the palette and budget) |
| `NereidHullLighting.cs` | cradle searchlight 2100→1890 cd, hatch wall wash 250→225, saddle grazing 1950→1755; positions, colours, ranges, angles unchanged |

No duplicate shaders were found to delete. No new texture samples or passes were added; the ALU delta is a few
dot/lerp operations per fragment.

## Volume

One global volume `Underwater grading` (`SliceVolume`): Bloom (threshold/intensity), Tonemapping, Vignette.
`UnderwaterLook` overrides exposure, saturation, contrast, Bloom intensity/threshold and vignette intensity per
state (Lerp on the wet blend + depth). Float mode is used with custom shaders, so depth/normal are not required
by the water stack; the first-person camera is never inside the fog volume.

## Quality controls

- VFX keep their particle budgets; the suspension field is a single instanced mesh.
- Depth II removes the sun-key branch, which is cheaper than Depth I despite the extra bands.
- Effect levels (Low/Medium/High) are inherited from the quality asset; nothing game-critical depends on an
  effect, so disabling bloom or specks never hides a catch or a route.
