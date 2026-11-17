using FishNet.Managing.Object;
using FishNet.Object;
using UnderwaterFishing.Progression.Network;
using UnityEngine;

namespace UnderwaterFishing.Editor
{
    public static partial class FoundationBuilder
    {
        public struct ProgressionBuildAssets { public NetworkObject Controller; }
        public static ProgressionBuildAssets PrepareProgression()
        {
            var config = Asset<ProgressionConfig>("ProgressionBalance");
            var root = new GameObject("CrewProgression"); root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkProgression>().Configure(config);
            var prefab = SaveNetworkPrefab(root, Root + "/Prefabs/CrewProgression.prefab");
            Object.DestroyImmediate(root);
            return new ProgressionBuildAssets { Controller = prefab.GetComponent<NetworkObject>() };
        }
        public static DiverProgression ConfigureProgressionDiver(GameObject diver)
        { return diver.GetComponent<DiverProgression>() ?? diver.AddComponent<DiverProgression>(); }
        public static ProgressionWorld AddProgressionWorld(GameObject sessionRoot, ProgressionBuildAssets assets, SinglePrefabObjects prefabs)
        {
            prefabs.AddObject(assets.Controller, true);
            var world = sessionRoot.AddComponent<ProgressionWorld>(); world.ProgressionPrefab = assets.Controller;
            AddProgressionTerminal(ProgressionTerminalKind.Processing, "ПЕРЕРАБОТКА", new Vector3(-2, -7.2f, 8.8f), new Color(.15f, .7f, .35f));
            AddProgressionTerminal(ProgressionTerminalKind.Workshop, "МАСТЕРСКАЯ", new Vector3(2, -7.2f, 8.8f), new Color(.2f, .55f, 1));
            AddProgressionTerminal(ProgressionTerminalKind.Offering, "ПОДНОШЕНИЕ", new Vector3(6, -7.2f, 8.8f), new Color(.85f, .4f, .2f));
            return world;
        }
        private static void AddProgressionTerminal(ProgressionTerminalKind kind, string title, Vector3 position, Color color)
        {
            var terminal = new GameObject("Progression terminal " + kind); terminal.transform.position = position;
            terminal.AddComponent<ProgressionTerminal>().Kind = kind;
            var box = terminal.AddComponent<BoxCollider>(); box.size = new Vector3(1.1f, .6f, .08f);
            var panel = Shape(title, Vector3.zero, box.size, Material("Progression" + kind, color, true), parent: terminal.transform);
            panel.transform.localPosition = Vector3.zero;
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            var label = new GameObject("Terminal label", typeof(TextMesh)).GetComponent<TextMesh>(); label.transform.SetParent(terminal.transform, false);
            label.transform.localPosition = new Vector3(0, 0, -.06f); label.fontSize = 32; label.characterSize = .025f;
            label.anchor = TextAnchor.MiddleCenter; label.color = Color.white; label.text = title + "\nE — открыть";
        }
    }
}
