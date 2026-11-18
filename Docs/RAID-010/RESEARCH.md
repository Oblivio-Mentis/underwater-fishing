# RAID-010 rendering decisions

- Baseline: Unity6000.5.6f1, URP17.5, RTX4070, Ryzen7700X,31GB reported RAM. Existing project inspected through live Unity MCP.
- Use project-authored modular geometry and existing URP Lit. No packages, downloaded assets, SDKs or changes to render-pipeline assets are needed.
- [Unity URP materials](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lit-shader.html): shared Lit materials with muted metal roughness; transparent tank has depth-write disabled and a transparent queue.
- [Unity Bloom](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/post-processing-bloom.html): modest Bloom and ACES on a new scene-only Volume profile. Added camera data only on the visual-scene player's camera.
- [Mesh API](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Mesh.html): small saved meshes for hull, tentacles, faceted rock modules; reuse shared meshes/materials.
- Fog is scene-local linear fog. Warm practical lights at short distances, cold ambient light outside; retain clear fish/container silhouettes.
- Gameplay has fixed sealed-room bounds and station positions. Visual parts have zero colliders. Existing collision surfaces and gameplay transforms remain in the copied scene.
- Animation changes child transforms only: tentacles/breathing, kelp, fish tails, local harpoon view model. No networked tentacle simulation or extra HUD.
- Baseline renderer sample:10s,1920x1080, static first-person-height pose with no networking. Actual player-camera samples are separately reported; results are observations on this PC, not minimum hardware promises.
