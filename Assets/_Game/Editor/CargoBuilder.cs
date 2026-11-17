using FishNet.Managing.Object;
using FishNet.Object;
using UnderwaterFishing.Cargo;
using UnderwaterFishing.Cargo.Network;
using UnityEditor;
using UnityEngine;

namespace UnderwaterFishing.Editor
{
    public static partial class FoundationBuilder
    {
        private struct CargoBuildAssets { public NetworkObject Station, Scrap, Electronics; }

        private static CargoBuildAssets PrepareCargo()
        {
            var balance = Asset<CargoSettings>("CargoBalance");
            var scrap = Asset<LootDefinition>("ScrapLoot");
            scrap.DisplayName = "Ящик металлолома"; scrap.CargoSize = 2;
            scrap.Yield = new CargoYield { Scrap = 4 }; scrap.Shape = new Vector3(.9f, .7f, .8f);
            var electronics = Asset<LootDefinition>("ElectronicsLoot");
            electronics.DisplayName = "Повреждённый электронный модуль"; electronics.CargoSize = 1;
            electronics.Yield = new CargoYield { Scrap = 1, Electronics = 3 }; electronics.Shape = new Vector3(.5f, .45f, .6f);
            EditorUtility.SetDirty(scrap); EditorUtility.SetDirty(electronics);
            return new CargoBuildAssets {
                Station = CargoStationPrefab(balance),
                Scrap = LootPrefab(scrap, "ScrapLoot", new Color(.65f, .38f, .16f)),
                Electronics = LootPrefab(electronics, "ElectronicsLoot", new Color(.22f, .55f, 1f))
            };
        }
        private static void ConfigureCargoDiver(GameObject diver) { diver.AddComponent<DiverCargo>(); diver.AddComponent<CargoTestBot>(); }
        private static void AddCargoWorld(GameObject sessionRoot, CargoBuildAssets assets, SinglePrefabObjects prefabs)
        {
            prefabs.AddObject(assets.Station, true); prefabs.AddObject(assets.Scrap, true); prefabs.AddObject(assets.Electronics, true);
            var world = sessionRoot.AddComponent<CargoWorld>();
            world.StationPrefab = assets.Station; world.ScrapPrefab = assets.Scrap; world.ElectronicsPrefab = assets.Electronics;
            Shape("Cargo hoist", new Vector3(4, -5.2f, 6), new Vector3(.3f, 3, .3f), Material("Hull", Color.gray));
            Shape("Protected cargo hold", new Vector3(4, -8, 7.8f), new Vector3(3, .3f, 2), Material("CargoHold", new Color(.3f, .4f, .65f)));
            Sign("ГРУЗОВОЙ ОТСЕК\nЗащищённый улов · переработка на базе", new Vector3(4, -6, 7.8f), Quaternion.Euler(0, 180, 0), Color.cyan);
        }
        private static NetworkObject CargoStationPrefab(CargoSettings balance)
        {
            var go = new GameObject("CargoContainer");
            var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(3, 1.7f, 2.5f);
            var material = Material("CargoContainer", new Color(.85f, .63f, .16f));
            CargoShape(go.transform, "Container floor", new Vector3(0, -.75f, 0), new Vector3(3, .2f, 2.5f), material);
            for (int side = -1; side <= 1; side += 2)
            {
                CargoShape(go.transform, "Side rail", new Vector3(side * 1.4f, .4f, 0), new Vector3(.15f, .8f, 2.5f), material);
                CargoShape(go.transform, "End rail", new Vector3(0, .4f, side * 1.15f), new Vector3(3, .8f, .15f), material);
            }
            var label = new GameObject("Cargo status", typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0, 2, 0);
            label.fontSize = 36; label.characterSize = .055f; label.anchor = TextAnchor.MiddleCenter; label.color = Color.cyan;
            var cable = new GameObject("Winch cable", typeof(LineRenderer)).GetComponent<LineRenderer>();
            cable.transform.SetParent(go.transform, false); cable.useWorldSpace = true;
            cable.sharedMaterial = UnlitRope(); cable.startWidth = cable.endWidth = .05f;
            cable.startColor = cable.endColor = new Color(.7f, .8f, .85f);
            go.AddComponent<NetworkObject>();
            Vector3 deployed = new Vector3(4, -14, 6), hold = new Vector3(4, -7, 6);
            go.AddComponent<NetworkCargoStation>().Configure(balance, deployed, hold, label, cable);
            go.transform.position = deployed;
            var prefab = SaveNetworkPrefab(go, Root + "/Prefabs/CargoContainer.prefab");
            Object.DestroyImmediate(go); return prefab.GetComponent<NetworkObject>();
        }
        private static NetworkObject LootPrefab(LootDefinition definition, string name, Color color)
        {
            var go = new GameObject(name);
            var collider = go.AddComponent<BoxCollider>(); collider.size = definition.Shape;
            CargoShape(go.transform, "Loot body", Vector3.zero, definition.Shape, Material(name, color, true));
            CargoShape(go.transform, "Loot stripe", Vector3.up * (definition.Shape.y * .51f), new Vector3(definition.Shape.x * .5f, .04f, definition.Shape.z), Material("Guide", Color.cyan, true));
            go.AddComponent<NetworkObject>(); go.AddComponent<NetworkLoot>().Configure(definition);
            var prefab = SaveNetworkPrefab(go, Root + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(go); return prefab.GetComponent<NetworkObject>();
        }
        private static void CargoShape(Transform parent, string name, Vector3 offset, Vector3 scale, Material material)
        {
            var visual = Shape(name, offset, scale, material, parent: parent);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }
    }
}
