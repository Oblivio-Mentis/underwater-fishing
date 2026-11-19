# UI research — 2026-11-19

- [Unity uGUI CanvasScaler source](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Layout/CanvasScaler.cs): ScaleWithScreenSize, Expand and layout anchors keep controls in view on wide screens; resolution checks remain necessary.
- [Unity Input System UI module](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/ui-input-module-reference.md): one InputSystemUIInputModule owns UI pointer/navigation actions; gameplay action maps require their own explicit gate.
- [PT Sans source](https://github.com/google/fonts/tree/main/ofl/ptsans): static Cyrillic-capable TTF, OFL 1.1 retained with the font; no large asset pack.
- Unity MCP reflection confirmed installed TMP_FontAsset.CreateFontAsset overload for Font, sampling size, padding, glyph render mode and atlas size. Runtime/state queries and editor assembly import will be used before relying on new APIs.
- UI samples authoritative snapshots at 10 Hz, with roster discovery at about 3 Hz; changed text only. List rebuilds depend on inventory/levels, not timer ticks.
