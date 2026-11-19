# NEREID rebuild — 19 November 2026

- User rejected the first RAID-011 vessel and explicitly requested full redesign. Keep functional server loop; replace architecture, improve equipment and inspect actual player-camera frames.
- Current scene audit: old interior is a 12×16m rectangular room; additional stern volume needs a scene-specific sealed-space definition. Root collider IDs and previous scene assets stay intact.
- Current PC_Renderer already enables SSAO; PC_RPAsset already enables reflection blending and box projection. Do not install duplicate render features or alter the shared renderer merely for a visual change.
- [Unity URP SSAO](https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.0/manual/post-processing-ssao.html): implemented as renderer feature; verify installed source/settings before changing.
- [Unity reflection probes](https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/reflections/class-reflection-probe): box projection requires URP asset support; use a bounded probe refreshed once, rather than every frame.
- Blender located locally; produce actual bevelled modular interior/exterior FBX assets headlessly. No asset download or new package needed. Own source geometry/materials remain commercially usable project assets.
- Quality gap from reference: add structural octagonal arches, thick clipped windows, dark grating, warm caged lamps, matte steel, strong cylindrical tank. Reduce repeated orange lines, floating pipe cuffs and flat uniform fill.
