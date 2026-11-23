using System;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        [MenuItem("NEREID/Fit recovery crane work lights")]
        public static void FitDavitWorkLights()
        {
            var prefab = PrefabUtility.LoadPrefabContents(DavitAsset);
            try
            {
                if (FitOperatingLights(prefab.GetComponent<NereidCargoDavit>()))
                    PrefabUtility.SaveAsPrefabAsset(prefab, DavitAsset);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
        }

        static bool FitOperatingLights(NereidCargoDavit drive)
        {
            if (drive.warningLens) return false;
            const string lightGlass = DavitParts + "/Davit optical glass.mat";
            var glass = AssetDatabase.LoadAssetAtPath<Material>(lightGlass);
            if (!glass)
            {
                glass = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                glass.SetColor("_BaseColor", new Color(.033f, .052f, .056f));
                AssetDatabase.CreateAsset(glass, lightGlass);
            }
            var wash = drive.bearing.Find("Guarded slew work lamps/Drawworks maintenance wash");
            var flood = drive.head.Find("Load watch lamp and sheave guard/Suspended load flood");
            if (!wash || !flood) throw new InvalidOperationException("The crane has no maintenance wash or suspended load flood.");
            drive.deckWash = wash.GetComponent<Light>();
            drive.workFlood = flood.GetComponent<Light>();
            drive.workFlood.range = 68f;
            drive.workFlood.enabled = drive.deckWash.enabled = false;

            Renderer Window(Transform mount, string label, Vector3 foot, Vector3 axis, float radius, float height)
            {
                var lens = new ShipSectionMesh(mount, Vector3.zero, label, Vector3.zero);
                lens.Tube(glass, Vector3.zero, Vector3.up, 1f, .98f, 10);
                lens.Store(DavitParts + "/Meshes/Prismatic work lens.asset");
                lens.node.localPosition = foot;
                lens.node.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
                lens.node.localScale = new Vector3(radius, height, radius);
                return lens.node.GetComponent<Renderer>();
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var normal = new Vector3(side * .58f, .68f, .44f).normalized;
                Vector3 at = new Vector3(side * .77f, -.18f, -.35f) + normal * .233f;
                var lens = Window(drive.bearing, side < 0 ? "Port task lens" : "Starboard task lens", at, normal, .164f, .013f);
                if (side < 0) drive.portGlass = lens;
                else drive.starboardGlass = lens;
            }
            drive.floodGlass = Window(drive.head, "Pendant task lens", new Vector3(.31f, -.381f, -.04f),
                Vector3.down, .150f, .014f);
            Vector3 seat = new(0, .01f, -1.33f);
            var baseMetal = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Machined titanium.mat");
            var housing = new ShipSectionMesh(drive.bearing, Vector3.zero, "Guarded slew beacon", seat);
            housing.Tube(baseMetal, seat, seat + Vector3.up * .19f, .22f, .15f, 10);
            housing.Tube(baseMetal, seat + Vector3.up * .19f, seat + Vector3.up * .22f, .19f, .18f, 10);
            housing.Tube(baseMetal, seat + Vector3.up * .51f, seat + Vector3.up * .56f, .165f, .13f, 10);
            for (int i = 0; i < 3; i++)
            {
                Vector3 foot = seat + Quaternion.AngleAxis(120f * i, Vector3.up) * Vector3.forward * .159f;
                housing.Tube(baseMetal, foot + Vector3.up * .21f, foot + Vector3.up * .52f, .016f, .016f, 6);
            }
            housing.Store(DavitParts + "/Meshes/Guarded slew beacon.asset");
            drive.warningLens = Window(drive.bearing, "Slew warning glass", seat + Vector3.up * .20f,
                Vector3.up, .143f, .315f);
            return true;
        }
    }
}
