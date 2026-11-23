# RAID-017 · Art direction

Stylized low/mid-poly deep-sea work horror. Large readable forms, few but strong materials, controlled
darkness. The palette starts from `.studioigor/ART_BIBLE.md`; it does not repaint every object the same hue.

## Palette

| role | value | use |
|---|---|---|
| hull steel | `#142A34` `#1D3740` | NEREID shell, bulkheads, cages |
| edge / trim | `#42616A` | seams, railings, brackets |
| instrument cyan | `#32B8C5` | displays, ready states, lenses |
| work amber | `#E6A64C` | practicals, hatch guides, warm pools |
| danger | `#D75E58` | real threats and alarms only |
| shallow water | `#1E6E8A` → `#0C2A38` with distance | Depth I |
| rift water | `#0A1F33` → `#04080F` with distance | Depth II |
| reef stone | `#3A4A3C` `#5C6B55` | limestone shelves, arch |
| rift basalt | `#111F29` `#26333C` | needles, terraces |
| sand | `#8A9C90` (lit) / `#3E4C48` (shaded) | shoal floor |
| parasite | `#3A1220` `#5A2036` | hide, gums, membranes |
| bone | `#C9BFA6` | teeth, plates |

Saturation rises only on things the player must act on: catch, companion, hatch, cage.

## States

Four authored grading states, stored in `Assets/_Game/Nereid/Resources/UnderwaterGrading.asset`
and applied by `UnderwaterLook`:

1. **Dry NEREID** — warm practicals, cool monitors, no fog, exposure `+0.12`, contrast `8`.
   The ship must read as a working space: rails, pipes, stencils, worn paint.
2. **Flooding airlock** — half water, half steel; same tint as Depth I but no caustics and no sun shafts.
3. **Depth I — reef shelf** — cool teal, long visibility (`fog 0.042`), a soft top-down key
   (directional `0.65`, colour `#4D9BC2`), low flat fill. Sun shafts and caustics allowed.
4. **Depth II — needle rift** — indigo-black (`fog 0.112`), no caustics, no sun shafts;
   local cold light from vents, survey beacons and the helmet lamp. Exposure never crushes
   silhouettes: the player must find the way home.

Rules:

- The sea never gets a flat white fill: the old `Ocean reflected fill` (intensity 1.5, unshadowed)
  is scaled to `0.20` on Depth I and `0.05` on Depth II, which restores form.
- Distance desaturation is stronger than distance darkening; the far water goes blue-grey, not black.
- Fog, exposure, saturation and contrast are all per-state. Never mix two states in one frame.

## Light

- Deck: amber pools over hatch, cage and workbench; cyan monitor glow; cold spill from the reservoir.
  No ceiling flood without a fixture mesh under it.
- Water: one key from above (Depth I only), a helmet lamp on `L`, and hull work lamps.
  Every work lamp must show a visible beam cone in suspended matter.
- Depth II: cold lantern colonies supply the only ambient; keep them rare (surface area matters).

## Form and material

- Big shapes with a few deliberate bevels. No uniform photoreal texture next to flat low-poly.
- Stone: bedding planes and colour banding from vertex data plus the shader's strata noise.
- Sand: soft ripples near the eye, flat tone at distance; never a pure white surface.
- Metal: roughness/smoothness contrast, edge wear along exposed rims, no mirror-chrome.
- Organic: wet, uneven, asymmetric; parasite hide is dark burgundy with dull specular,
  not glossy plastic.

## Forbidden

Single turquoise wash; identical empty floor; acid bioluminescence everywhere; bloom so strong
that lamps turn into discs; SSAO dirt; hundreds of identical rocks; trailer-only angles that
never occur while playing; UI-hidden cheats in comparison frames; fog removed for a screenshot.

## Check before any frame counts as done

1. The player's own eye at normal height, no extra lights added for the shot.
2. Catch, companion, hatch, cage and interaction prompts read first.
3. Depth II is navigable from the hoist back to NEREID without the map.
4. Interior reads warmer than the water outside the glass.
5. Nothing new hides a station, blocks a route, or covers a monitor.
