using System;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        const string BioLiner = "Containment pressure jacket";
        static readonly Vector3 bioOrigin = new(0, 0, 6);

        [MenuItem("RAID/020/Fit containment pressure jacket")]
        public static void FitContainmentPressureJacket()
        {
            string path = RoomPath + "/BioContainment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (prefab.transform.Find(BioLiner)) return;
                Palette();
                BuildContainmentPressureJacket(prefab.transform);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void BuildContainmentPressureJacket(Transform parent)
        {
            var jacket = new GameObject(BioLiner).transform;
            jacket.SetParent(parent, false);
            foreach (int side in new[] { -1, 1 })
            {
                string name = (side < 0 ? "Port" : "Starboard") + " containment inspection cover";
                var center = new Vector3(side * 5.94f, 2.28f, 6.36f);
                var cover = new ShipSectionMesh(jacket, bioOrigin, name, center);
                Vector3 inward = Vector3.left * side;
                Vector3 along = Vector3.forward * side;
                // A continuous pressure plate fits inside the old lining and overlaps the
                // outer shell returns at 5.97. Both service faces cover the original glazing.
                cover.Slab(hull, new(side * 5.96f, 2.29f, 6.34f), new(.28f, 4.68f, 3.72f));
                var inside = center + inward * .123f;
                Casting(cover, inside, along, Vector3.up, Shield(3.34f, 2.89f, .30f), .075f, dark, steel, dark, .97f);
                Casting(cover, inside + inward * .073f, along, Vector3.up, Shield(3.10f, 2.65f, .32f),
                    .085f, enamel, hull, rubber, .93f);
                // Two forged stiffeners and four captive dogs hold the bolted cover in its seat.
                foreach (int end in new[] { -1, 1 })
                {
                    var stiffener = inside + along * (end * .77f) + inward * .155f;
                    Casting(cover, stiffener, along, Vector3.up, Shield(.18f, 2.01f, .84f), .072f, hull, steel, dark, .82f);
                    foreach (int row in new[] { -1, 1 })
                    {
                        var dog = inside + along * (end * 1.34f) + Vector3.up * (row * .87f) + inward * .13f;
                        Casting(cover, dog, along, Vector3.up, Shield(.44f, .18f, .38f), .094f, ochre, dark, rubber);
                        SocketBolt(cover, dog + inward * .094f, along, Vector3.up, .064f);
                    }
                }
                var boss = inside + inward * .158f;
                Band(cover, boss, along, Vector3.up, .35f, .25f, .046f, steel, dark, 0, 360, 12);
                cover.Tube(hull, boss + inward * .018f, boss + inward * .065f, .24f, .21f, 10);
                cover.Tube(steel, boss + inward * .062f, boss + inward * .101f, .092f, .082f, 6);

                var outside = center - inward * .17f;
                Casting(cover, outside, -along, Vector3.up, Shield(3.32f, 2.89f, .35f), .16f, hull, enamel, dark, .91f);
                foreach (int row in new[] { -1, 1 })
                {
                    var guard = outside - inward * .156f + Vector3.up * (row * .59f);
                    Casting(cover, guard, -along, Vector3.up, Shield(2.45f, .17f, .87f), .105f, enamel, dark, hull, .82f);
                }
                // Folded bilge-to-roof knuckle. The two ends run into the neighboring ribs.
                cover.Beam(enamel, new(side * 5.77f, 4.31f, 4.47f), new(side * 5.77f, 4.31f, 8.25f),
                    .72f, .63f);
                cover.Box(hull, new(side * 4.26f, 4.52f, 8.16f), new(3.50f, .39f, .32f));
                cover.Collision(new(side * 4.26f, 4.52f, 8.16f), new(3.50f, .39f, .32f));
                cover.Store(Root + "/Meshes/Bio_" + name + ".asset");
            }

            var origin = new Vector3(0, 1.75f, 8.17f);
            var gland = new ShipSectionMesh(jacket, bioOrigin, "Double-faced oral gland seat", origin);
            // A boxed annulus joins the circular clamp to the rectangular forward bulkhead.
            // Keep its throat open through both skins for the animated jaw and tracked catch.
            const int sectors = 32;
            for (int i = 0; i < sectors; i++)
            {
                float a = i * (Mathf.PI * 2 / sectors), b = (i + 1) * (Mathf.PI * 2 / sectors);
                Vector3 Inner(float t) => origin + new Vector3(Mathf.Cos(t) * 2.17f, Mathf.Sin(t) * 1.40f, 0);
                Vector3 Outer(float t)
                {
                    var p = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                    p /= Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    return origin + new Vector3(p.x * 2.63f, p.y * 1.80f, 0);
                }
                var lipA = Inner(a); var lipB = Inner(b);
                var edgeA = Outer(a); var edgeB = Outer(b);
                var back = Vector3.forward * .24f;
                gland.Face(dark, edgeA, lipA, lipB, edgeB);
                gland.Face(hull, edgeA + back, edgeB + back, lipB + back, lipA + back);
                gland.Face(rubber, lipA, lipA + back, lipB + back, lipB);
                gland.Face(hull, edgeA, edgeB, edgeB + back, edgeA + back);
            }
            gland.Store(Root + "/Meshes/Bio_Double-faced oral gland seat.asset");
            gland.node.gameObject.AddComponent<MeshCollider>().sharedMesh = gland.node.GetComponent<MeshFilter>().sharedMesh;
        }
    }
}
