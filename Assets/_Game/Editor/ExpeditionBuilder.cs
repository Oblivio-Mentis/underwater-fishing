using FishNet.Managing.Object;
using FishNet.Object;
using UnderwaterFishing.Data;
using UnderwaterFishing.Expedition;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Expedition.Presentation;
using UnityEngine;

namespace UnderwaterFishing.Editor
{
    public static partial class FoundationBuilder
    {
        private struct ExpeditionBuildAssets { public NetworkObject Controller; }
        private static ExpeditionBuildAssets PrepareExpedition(GameBalance balance)
        {
            var settings = Asset<ExpeditionSettings>("ExpeditionBalance");
            var go = new GameObject("ExpeditionTerminal");
            var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.1f, .6f, .08f);
            var panel = Shape("Expedition display", Vector3.zero, box.size, Material("ExpeditionDisplay", new Color(.08f, .6f, .75f), true), parent: go.transform);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            var label = new GameObject("Expedition status", typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0, 0, -.06f);
            label.fontSize = 32; label.characterSize = .025f; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            go.AddComponent<NetworkObject>(); go.AddComponent<NetworkExpedition>().Configure(balance, settings, label);
            go.transform.position = new Vector3(-6, -7.2f, 8.78f);
            var prefab = SaveNetworkPrefab(go, Root + "/Prefabs/ExpeditionTerminal.prefab");
            Object.DestroyImmediate(go); return new ExpeditionBuildAssets { Controller = prefab.GetComponent<NetworkObject>() };
        }
        private static void ConfigureExpeditionDiver(GameObject diver) { diver.AddComponent<DiverExpedition>(); }
        private static void AddExpeditionWorld(GameObject sessionRoot, ExpeditionBuildAssets assets, SinglePrefabObjects prefabs)
        {
            prefabs.AddObject(assets.Controller, true);
            sessionRoot.AddComponent<ExpeditionWorld>().ExpeditionPrefab = assets.Controller;
            var baseDressing = new GameObject("Base docking lights");
            for (int i = 0; i < 3; i++)
            {
                var lamp = new GameObject("Base lamp", typeof(Light)).GetComponent<Light>();
                lamp.transform.SetParent(baseDressing.transform); lamp.transform.position = new Vector3(12, -6, -10 + i * 10);
                lamp.type = LightType.Point; lamp.range = 12; lamp.intensity = 3; lamp.color = new Color(.15f, .85f, 1);
            }
            var diveDressing = new GameObject("Test expedition survey beacons");
            Vector3[] positions = { new Vector3(12, -12, 4), new Vector3(20, -25, 19), new Vector3(7, -20, -13) };
            foreach (var p in positions)
            {
                var beacon = Shape("Survey beacon", p, new Vector3(.2f, 2, .2f), Material("SurveyBeacon", new Color(.85f, .7f, .18f), true), parent: diveDressing.transform);
                Object.DestroyImmediate(beacon.GetComponent<Collider>());
                var lamp = beacon.AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 9; lamp.intensity = 3; lamp.color = new Color(1, .7f, .2f);
            }
            var environment = sessionRoot.AddComponent<ExpeditionEnvironment>();
            environment.BaseObjects = new[] { baseDressing }; environment.ExpeditionObjects = new[] { diveDressing };
            diveDressing.SetActive(false);
        }
    }
}
