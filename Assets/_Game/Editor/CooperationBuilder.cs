using FishNet.Object;
using UnderwaterFishing.Cooperation;
using UnityEditor;
using UnityEngine;

namespace UnderwaterFishing.Editor
{
    public static partial class FoundationBuilder
    {
        private static NetworkObject PrepareCooperation()
        {
            var root = new GameObject("Unstable wreckage");
            root.AddComponent<NetworkObject>();
            var trap = root.AddComponent<NetworkCollapseTrap>();
            var material = Material("Hazard", new Color(.95f, .48f, .12f), true);
            var slab = Shape("Fallen beam", Vector3.up * 3, new Vector3(3.5f, .5f, 2.5f), material, parent: root.transform);
            Object.DestroyImmediate(slab.GetComponent<Collider>()); trap.Slab = slab.transform;
            for (int side = -1; side <= 1; side += 2)
            {
                var support = Shape("Broken support", new Vector3(side * 2, 0, 0), new Vector3(.25f, 4, .4f), material, parent: root.transform);
                Object.DestroyImmediate(support.GetComponent<Collider>());
            }
            var label = new GameObject("Debris warning", typeof(TextMesh)); label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0, 2.5f, -1.3f);
            var text = label.GetComponent<TextMesh>(); text.text = "НЕУСТОЙЧИВЫЕ ОБЛОМКИ\nE удерживать — освободить напарника";
            text.anchor = TextAnchor.MiddleCenter; text.fontSize = 36; text.characterSize = .045f; text.color = Color.yellow;
            var prefab = SaveNetworkPrefab(root, Root + "/Prefabs/CollapseTrap.prefab");
            Object.DestroyImmediate(root); return prefab.GetComponent<NetworkObject>();
        }
    }
}
