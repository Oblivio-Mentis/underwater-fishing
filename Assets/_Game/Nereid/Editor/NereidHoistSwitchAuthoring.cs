using UnityEditor;
using UnityEngine;
using static Raid.Nereid.Editor.NereidDispatchAuthoring;

namespace Raid.Nereid.Editor
{
    internal static class NereidHoistSwitchAuthoring
    {
        internal static void Upgrade(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab || prefab.GetComponent<NereidDispatchButton>()) return;
            using var edit = new PrefabUtility.EditPrefabContentsScope(path);
            var root = edit.prefabContentsRoot.transform;
            // Remove only the former factory readout and its fittings. Keep the prefab root,
            // station component and any independently added children and scene bindings.
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var part = root.GetChild(i);
                if (part.name is "Display plane" or "Pressure casing" or "Recessed display" or
                    "Machined vertical edge" or "Edge guard" or "Display seal" or "Cable pedestal" or
                    "Mounting plinth" or "Sloping operator deck" or "Line indicator")
                    Object.DestroyImmediate(part.gameObject);
            }
            var readout = root.GetComponent<NereidInstrument>();
            if (readout) Object.DestroyImmediate(readout);
            Build(root.GetComponent<ShipConsole>());
        }

        internal static void Build(ShipConsole station)
        {
            var root = station.transform;
            var iron = GetMaterial("Graphite forged frames");
            var steel = GetMaterial("Machined titanium");
            var enamel = GetMaterial("Rescue ochre");
            var gasket = GetMaterial("Reinforced hose rubber");
            var amber = GetMaterial("Feed key amber");
            var ink = GetMaterial("Dispatch key inlay");

            var foot = Solid(root, "Deck anchor flange", new(0,.043f,.045f), new(.58f,.043f,.51f), steel, PrimitiveType.Cylinder);
            foot.gameObject.AddComponent<BoxCollider>();
            var column = Solid(root, "Winch control riser", new(0,.53f,.06f), new(.19f,.49f,.19f), iron, PrimitiveType.Cylinder);
            column.gameObject.AddComponent<BoxCollider>();
            for (int i = 0; i < 2; i++)
                Solid(root, "Riser clamp", new(0,.25f+.54f*i,.06f), new(.247f,.037f,.247f), steel, PrimitiveType.Cylinder);
            var head = Pivot(root, "Hoist control head", new(0,1.225f,0));
            head.localRotation = Quaternion.Euler(22,0,0);
            Disk(head, "Cast winch housing", new(0,0,.055f), .705f,.29f, iron);
            Disk(head, "Signal yellow guard rim", new(0,0,-.074f), .685f,.105f, enamel);
            Disk(head, "Piston gland", new(0,0,-.121f), .553f,.067f, gasket);
            Disk(head, "Illuminated collar", new(0,0,-.148f), .481f,.045f, amber);
            var cap = Pivot(head, "Hoist plunger", new(0,0,-.194f));
            Disk(cap, "Button skirt", new(0,0,.028f), .410f,.077f, steel);
            Disk(cap, "Non-slip palm cap", new(0,0,-.004f), .426f,.055f, iron);
            var contact = Pivot(cap, "Hoist palm contact", new(0,0,-.033f));
            Box(contact, "Lift arrow stem", new(0,0,-.003f), new(.030f,.153f,.005f), ink);
            for (int side = -1; side <= 1; side += 2)
            {
                var diagonal = Solid(contact, "Lift arrow point", new(side*.035f,.066f,-.004f), new(.032f,.122f,.006f), ink, PrimitiveType.Cube);
                diagonal.localRotation = Quaternion.Euler(0,0,side*42f);
                Box(contact, "Cargo cradle end", new(side*.102f,-.082f,-.003f), new(.027f,.071f,.005f), ink);
                var guard = Solid(head, "Palm guard cheek", new(side*.283f,0,-.142f), new(.058f,.239f,.139f), steel, PrimitiveType.Cube);
                guard.gameObject.AddComponent<BoxCollider>();
            }
            Box(contact, "Cargo cradle", new(0,-.104f,-.003f), new(.226f,.030f,.005f), ink);
            // Keep the strike surface fixed throughout the animation and reject contact with the nearby guard.
            var strike = Disk(head, "Hoist strike area", new(0,0,-.226f), .429f,.012f, iron);
            strike.GetComponent<MeshRenderer>().enabled = false;
            var collider = strike.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = strike.GetComponent<MeshFilter>().sharedMesh;
            var button = station.gameObject.AddComponent<NereidDispatchButton>();
            button.cap = cap;
            button.palmContact = contact;
            button.hitFace = collider;
            button.travel = .039f;
            station.dispatchButton = button;
            station.screenAnchor = contact;
            station.screenSize = Vector2.zero;
        }
    }
}
