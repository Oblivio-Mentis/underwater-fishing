# HUD / physical terminal direction — 19 November 2026

User direction: a compact shared fish/hunger indicator at the top centre, physical ship computers, two real routes from the reservoir, minimal permanent UI. Extends the original RAID-011 brief.

Implemented HUD:
- One 700 × 108 instrument at top centre: real fish count, distinct technical-cargo count, hunger countdown, paid food / required food, current zone and diver depth. The food rail measures payment, never an invented fish capacity.
- Fish count follows the existing item classification (`offering > 0 || biomass > 0 || chitin > 0`); technical items are counted separately. No species or avatars are fabricated.
- Crew remains upper left, 310 px wide, only occupied rows; IDs match the existing network owners. Cyan = underwater, green = inside, red = trapped / helpless, with text as well as colour.
- Oxygen uses 310 × 96 inside; real pressure expands this to 140 px outside / while recovering. No pressure alarm inside a sealed compartment.
- Fish tension / stamina, rescue and cargo readouts appear only in their actual contexts. Dot reticle and a single bottom prompt keep the target area open.
- Alert below the central instrument; the existing toast at y = -254 remains clear. All significant text has a 16 px minimum at the 1080p reference scale.

Physical panels for the lead to implement:
1. Opposite sides of the reservoir: cyan processor valve and amber feeding valve. Their signed pipe route and destination remain visible before interaction.
2. Aim + E focuses the corresponding physical control. One confirmation states the authoritative eligible count and exact total materials / food. Accept sends one server batch command; cancel consumes nothing.
3. During flow, both route buttons show the shared busy state and reject a second batch. Fish disappear from stock once, while the client visual transfer follows a confirmed batch ID. Technical cargo never enters the feeding pipe.
4. Pipe inspection windows reveal the fish stream. At the processor it falls to a belt and enters the machine; rewards correspond to that confirmed batch. No infinite decorative fish loops or invented inventory.
5. Upgrades live on a side-mounted engineering computer; depth / return on a bridge computer. E enters pointer mode, Esc returns to movement. Keep one managed EventSystem; physical screens must use the owned player's camera for pointer projection.
6. Main engineering screen: five actual upgrade cards with level, next bonus and four-material cost; no tree, tabs or explanatory panels unrelated to the focused computer.
7. Stern exit: visible environmental hatch marking, one context prompt in reach. No permanent waypoint or fake radar.

Research / implementation:
- [Unity Canvas Scaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html): preserve the existing Expand mode and reference sizing so the instrument fits all required aspect ratios and 120% user scale.
- [Unity VertexHelper](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.VertexHelper.html), [MaskableGraphic](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.MaskableGraphic.html): the owned fish glyph uses a small native UI mesh; no raster asset, atlas dependency or external licence.

Verification: Roslyn compilation of changed HUD sources and the UiText partial family against the current project's Unity/uGUI/TMP/game assemblies succeeded (exit 0); `git diff --check` clean. Source-shadow warning 0436 suppressed for that standalone compilation. Layout sizes checked against the existing Expand scaler. The lead must still compile in Unity, inspect actual camera captures and exercise host/client states through Unity MCP. This agent did not access the live editor or OS input.
