using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    internal static class NereidSurfaceFinish
    {
        internal enum Surface { Enamel, Deck, Ceramic, Hull }

        const string Textures = "NereidServiceFittings/Textures/";
        internal const string PanelAtlasPath = Textures + "T_Trim_01_BaseColor";
        internal static readonly Rect PressedPanelUv = new(.242f,.686f,.516f,.293f);
        static readonly int Atlas = Shader.PropertyToID("_SurfaceAtlas");
        static readonly int Region = Shader.PropertyToID("_SurfaceRegion");
        static readonly int Tiling = Shader.PropertyToID("_SurfaceTiling");
        static readonly int Detail = Shader.PropertyToID("_SurfaceDetail");
        static readonly int Marine = Shader.PropertyToID("_Marine");
        static readonly int Tread = Shader.PropertyToID("_DeckTread");
        static Shader coating;
        static Texture2D panels, doors;

        // An atlas tile is projected at constant scale over welded meshes, molded
        // parts and moving pressure leaves, including artwork that has no usable UVs.
        internal static void Apply(Material material, Surface surface, bool exterior = false)
        {
            if (coating == null) coating = Resources.Load<Shader>("NereidHullCoating");
            if (panels == null) panels = Resources.Load<Texture2D>(PanelAtlasPath);
            if (doors == null) doors = Resources.Load<Texture2D>(Textures + "T_Trim_02_BaseColor");
            if (coating == null || panels == null || doors == null) return;

            Vector4 region, detail;
            Vector2 tile;
            var atlas = panels;
            switch (surface)
            {
                case Surface.Deck:
                    region = new(.019f,.074f,.962f,.412f);
                    tile = new(2.8f,1.47f);
                    detail = new(.053f,.65f,.012f,1);
                    break;
                case Surface.Ceramic:
                    region = new(.024f,.702f,.211f,.267f);
                    tile = new(1.52f,1.76f);
                    detail = new(.165f,.54f,.009f,1);
                    break;
                case Surface.Hull:
                    atlas = doors;
                    region = new(.015f,.376f,.97f,.372f);
                    tile = new(5.35f,2.59f);
                    detail = new(.155f,.73f,.023f,1);
                    break;
                default:
                    region = new(.247f,.694f,.506f,.288f);
                    tile = new(2.22f,1.44f);
                    detail = new(.164f,.63f,.013f,1);
                    break;
            }
            var tint = material.GetColor("_BaseColor");
            var metal = material.GetFloat("_Metallic");
            var smoothness = material.GetFloat("_Smoothness");
            material.shader = coating;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture(Atlas, atlas);
            material.SetVector(Region, region);
            material.SetVector(Tiling, new Vector4(1 / tile.x, 1 / tile.y, 0, 0));
            material.SetVector(Detail, detail);
            material.SetFloat(Marine, exterior ? 1 : 0);
            material.SetFloat(Tread, surface == Surface.Deck ? 1 : 0);
        }

        internal static void Clad(Transform root, Transform exterior, Dictionary<Material, Material> finishes)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (exterior != null && renderer.transform.IsChildOf(exterior)) continue;
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || !TrySurface(source.name, out var surface)) continue;
                    var existingMap = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
                    if (existingMap != null && existingMap != Texture2D.whiteTexture) continue;
                    if (!finishes.TryGetValue(source, out var cladding))
                    {
                        cladding = new Material(source) { name = source.name + " / textured", hideFlags = HideFlags.DontSave };
                        Apply(cladding, surface);
                        finishes.Add(source, cladding);
                    }
                    materials[i] = cladding;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        static bool TrySurface(string name, out Surface surface)
        {
            switch (name)
            {
                case "Nereid_Hull":
                case "Pressure enamel":
                case "Muted equipment ochre":
                case "Nereid_Ochre":
                    surface = Surface.Enamel;
                    return true;
                case "Nereid_Grate":
                case "Nereid_Dark":
                case "Gunmetal frames":
                    surface = Surface.Deck;
                    return true;
                case "Nereid_Steel":
                case "Nereid_Trim":
                case "Worn machined steel":
                    surface = Surface.Ceramic;
                    return true;
                default:
                    surface = default;
                    return false;
            }
        }
    }
}
