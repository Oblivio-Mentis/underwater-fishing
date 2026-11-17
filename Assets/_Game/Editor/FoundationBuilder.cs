using System.IO;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnderwaterFishing.Data;
using UnderwaterFishing.Network;
using UnderwaterFishing.Network.Steam;
using UnderwaterFishing.Player;
using UnderwaterFishing.UI;
using UnderwaterFishing.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using GameKit.Dependencies.Utilities;

namespace UnderwaterFishing.Editor
{
    public static partial class FoundationBuilder
    {
        private const string Root = "Assets/_Game";
        [MenuItem("Underwater Fishing/Create Foundation Scenes")]
        public static void Create()
        {
            var existing = new System.Collections.Generic.List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<NetworkObject>() == null) continue;
                SetPrefabHash(prefab, path); existing.Add(prefab);
            }
            foreach (var prefab in existing) PrefabUtility.SavePrefabAsset(prefab);
            CreateContent();
            AssetDatabase.SaveAssets();
        }
        private static void SetPrefabHash(GameObject go, string path)
        {
            var normalized = new System.Text.StringBuilder();
            foreach (char c in (path + go.name).ToLowerInvariant())
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) normalized.Append(c);
            var networkObject = go.GetComponent<NetworkObject>();
            networkObject.SetAssetPathHash(normalized.ToString().GetStableHashU64());
            EditorUtility.SetDirty(networkObject);
        }
        private static GameObject SaveNetworkPrefab(GameObject go, string path)
        { SetPrefabHash(go, path); return PrefabUtility.SaveAsPrefabAsset(go, path); }
        private static void CreateContent()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play before rebuilding scenes.");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Data");
            AssetDatabase.Refresh();
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(Root + "/Data/PrototypeBalance.asset");
            if (balance == null)
            {
                balance = ScriptableObject.CreateInstance<GameBalance>();
                AssetDatabase.CreateAsset(balance, Root + "/Data/PrototypeBalance.asset");
            }
            var fishing = PrepareFishing(balance);
            var controls = Controls();
            var diver = DiverPrefab(balance, fishing);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.025f, .13f, .17f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .018f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.12f, .23f, .26f);
            var sun = new GameObject("Shaft of daylight", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.7f;
            sun.color = new Color(.5f, .95f, 1f); sun.transform.rotation = Quaternion.Euler(65, -30, 0);
            var rock = Material("Rock", new Color(.08f, .17f, .19f));
            var hull = Material("Hull", new Color(.16f, .24f, .27f));
            var teal = Material("Guide", new Color(.12f, .85f, .75f), true);
            var yellow = Material("Terminal", new Color(.95f, .65f, .16f), true);
            Shape("Seabed", Vector3.down * 39, new Vector3(100, 2, 110), rock);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2 / 18;
                Shape("Cave wall " + i, new Vector3(Mathf.Cos(angle) * 43, -18, Mathf.Sin(angle) * 48), new Vector3(18, 42, 17), rock, PrimitiveType.Sphere);
            }
            Shape("Cave roof left", new Vector3(-24, 1, 0), new Vector3(35, 6, 90), rock);
            Shape("Cave roof right", new Vector3(24, 1, 0), new Vector3(35, 6, 90), rock);
            var sub = new GameObject("Submarine");
            sub.transform.position = new Vector3(0, -7, 0);
            sub.AddComponent<SubmarineInterior>();
            Shape("Deck", new Vector3(0, -9, 0), new Vector3(16, .6f, 26), hull, parent: sub.transform);
            Shape("Ceiling", new Vector3(0, -4, 0), new Vector3(16, .5f, 26), hull, parent: sub.transform);
            Shape("Port hull", new Vector3(-8, -6.5f, 0), new Vector3(.5f, 5, 26), hull, parent: sub.transform);
            Shape("Starboard forward", new Vector3(8, -6.5f, 7.5f), new Vector3(.5f, 5, 11), hull, parent: sub.transform);
            Shape("Starboard aft", new Vector3(8, -6.5f, -7.5f), new Vector3(.5f, 5, 11), hull, parent: sub.transform);
            Shape("Bow", new Vector3(0, -6.5f, 13), new Vector3(16, 5, .5f), hull, parent: sub.transform);
            Shape("Stern", new Vector3(0, -6.5f, -13), new Vector3(16, 5, .5f), hull, parent: sub.transform);
            Shape("Airlock guide", new Vector3(8, -8.5f, 0), new Vector3(5, .12f, 3.5f), teal, parent: sub.transform);
            Sign("ШЛЮЗ →", new Vector3(7.5f, -5, 0), Quaternion.Euler(0, -90, 0), teal.color);
            Sign("UNDERWATER FISHING\nЭКИПАЖ · 2–5", new Vector3(0, -5.5f, 12.6f), Quaternion.identity, teal.color);
            string[] names = { "ЭКСПЕДИЦИЯ", "ПЕРЕРАБОТКА", "МАСТЕРСКАЯ", "ПОДНОШЕНИЕ" };
            for (int i = 0; i < names.Length; i++)
            {
                var p = new Vector3(-6 + i * 4, -7.7f, 9.5f);
                Shape(names[i], p, new Vector3(1.5f, 1.8f, 1), hull, parent: sub.transform);
                Shape(names[i] + " screen", p + new Vector3(0, .5f, -.55f), new Vector3(1.2f, .65f, .1f), yellow, parent: sub.transform);
                Sign(names[i], p + new Vector3(0, 1.3f, -.65f), Quaternion.identity, teal.color);
            }
            for (int i = 0; i < 4; i++)
            {
                var lamp = new GameObject("Cabin lamp", typeof(Light)).GetComponent<Light>();
                lamp.type = LightType.Point; lamp.range = 13; lamp.intensity = 4;
                lamp.color = new Color(.4f, 1f, .9f); lamp.transform.position = new Vector3(0, -5, -9 + i * 6);
            }
            Shape("Wreck hull", new Vector3(20, -29, 19), new Vector3(11, 4, 18), hull);
            Shape("Wreck mast", new Vector3(20, -21, 19), new Vector3(.5f, 15, .5f), hull);
            for (int i = 0; i < 8; i++) Shape("Rock", new Vector3(-25 + i * 7, -34, 25 + i % 3 * 4), new Vector3(7, 9, 6), rock, PrimitiveType.Sphere);

            var root = new GameObject("Session");
            var manager = root.AddComponent<NetworkManager>();
            var lan = root.GetComponent<Tugboat>() ?? root.AddComponent<Tugboat>();
            var lanSettings = new SerializedObject(lan);
            lanSettings.FindProperty("_maximumClients").intValue = 5; lanSettings.ApplyModifiedPropertiesWithoutUndo();
            var transportManager = root.GetComponent<TransportManager>() ?? root.AddComponent<TransportManager>();
            var router = root.AddComponent<RoutingTransport>();
            router.InitialTransport = lan;
            transportManager.Transport = router;
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(Root + "/Data/SpawnablePrefabs.asset");
            if (prefabs == null) { prefabs = ScriptableObject.CreateInstance<SinglePrefabObjects>(); AssetDatabase.CreateAsset(prefabs, Root + "/Data/SpawnablePrefabs.asset"); }
            prefabs.Clear(); prefabs.AddObject(diver, true);
            prefabs.AddObject(fishing.Small, true); prefabs.AddObject(fishing.Large, true);
            manager.SpawnablePrefabs = prefabs;
            EditorUtility.SetDirty(prefabs);
            var input = root.AddComponent<DiverInput>(); input.Configure(controls);
            var session = root.AddComponent<NetworkSession>(); session.Manager = manager; session.DiverPrefab = diver; session.LanTransport = lan;
            var fishWorld = root.AddComponent<UnderwaterFishing.Fishing.Network.FishingWorld>();
            fishWorld.SmallFish = fishing.Small; fishWorld.LargeFish = fishing.Large;
            var steamTransport = root.AddComponent<FishySteamworks.FishySteamworks>();
            var steamSettings = new SerializedObject(steamTransport);
            steamSettings.FindProperty("_peerToPeer").boolValue = true;
            steamSettings.FindProperty("_maximumClients").intValue = 5; steamSettings.ApplyModifiedPropertiesWithoutUndo();
            var steam = root.AddComponent<SteamLobbyService>(); steam.Manager = manager; steam.Transport = steamTransport;
            root.AddComponent<SessionUI>();
            var camera = new GameObject("Lobby Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, -6.5f, -9); camera.transform.rotation = Quaternion.Euler(0, 0, 0);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
            session.LobbyCamera = camera;
            PlayerSettings.companyName = "Oblivio Mentis"; PlayerSettings.productName = "Underwater Fishing";
            PlayerSettings.bundleVersion = "0.0.1-raid003";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            var path = Root + "/Scenes/UnderwaterFishing.unity";
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "/Scenes/DivingGym.unity", true);
            var gym = root.AddComponent<UnderwaterFishing.Fishing.Gym.FishingGym>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "/Scenes/FishingGym.unity", true);
            Object.DestroyImmediate(gym);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("RAID_FOUNDATION_CREATED");
        }

        private static InputActionAsset Controls()
        {
            string path = Root + "/Data/DiverControls.inputactions";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Diver");
            map.AddAction("Move", InputActionType.Value).AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            map.AddAction("Vertical", InputActionType.Value).AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/space").With("Negative", "<Keyboard>/leftCtrl");
            map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            string[] actionNames = { "Sprint", "Interact", "Help", "Fire", "Reel", "Release", "Sonar", "Menu", "Info" };
            string[] bindings = { "<Keyboard>/leftShift", "<Keyboard>/e", "<Keyboard>/f", "<Mouse>/leftButton", "<Mouse>/rightButton", "<Keyboard>/r", "<Keyboard>/q", "<Keyboard>/escape", "<Keyboard>/tab" };
            for (int i = 0; i < actionNames.Length; i++) map.AddAction(actionNames[i], InputActionType.Button, bindings[i]);
            File.WriteAllText(path, asset.ToJson()); Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        private static NetworkObject DiverPrefab(GameBalance balance, FishBuildAssets fishing)
        {
            var go = new GameObject("Diver");
            var controller = go.AddComponent<CharacterController>(); controller.height = 1.7f; controller.radius = .35f; controller.center = Vector3.zero; controller.stepOffset = .1f;
            var mesh = Shape("Diver body", Vector3.zero, new Vector3(.7f, .85f, .7f), Material("Diver", new Color(.9f, .55f, .18f)), PrimitiveType.Capsule, go.transform);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            var visor = Shape("Visor direction", new Vector3(0, .4f, .34f), new Vector3(.5f, .25f, .2f), Material("Visor", new Color(.1f, .9f, .9f), true), parent: mesh.transform);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            var camera = new GameObject("Eyes", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.SetParent(go.transform); camera.transform.localPosition = new Vector3(0, .6f, 0); camera.nearClipPlane = .05f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .1f, .14f); camera.enabled = false;
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkDiver>().Configure(balance, camera, mesh);
            ConfigureHarpoon(go, camera, fishing);
            var prefab = SaveNetworkPrefab(go, Root + "/Prefabs/Diver.prefab");
            Object.DestroyImmediate(go); return prefab.GetComponent<NetworkObject>();
        }
        private static Material Material(string name, Color color, bool emission = false)
        {
            string path = Root + "/Data/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.5f); }
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Shape(string name, Vector3 p, Vector3 size, Material material, PrimitiveType type = PrimitiveType.Cube, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            if (parent != null) go.transform.SetParent(parent);
            go.transform.position = p; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            if (type == PrimitiveType.Sphere && parent == null)
            {
                // SphereCollider uses the largest scale axis; static cave rocks need the visible mesh shape.
                Object.DestroyImmediate(go.GetComponent<SphereCollider>());
                go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }
        private static void Sign(string text, Vector3 p, Quaternion rotation, Color color)
        {
            var go = new GameObject(text, typeof(TextMesh)); go.transform.position = p; go.transform.rotation = rotation;
            var mesh = go.GetComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 40; mesh.characterSize = .055f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.color = color;
        }
        [MenuItem("Underwater Fishing/Build Windows Development")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Root + "/Scenes/UnderwaterFishing.unity" },
                locationPathName = "Builds/Windows/UnderwaterFishing.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Windows build failed: " + report.summary.result);
            Debug.Log("RAID_BUILD_SUCCEEDED " + report.summary.totalSize);
        }
        public static void BuildFromSource()
        {
            Create();
            BuildWindows();
        }
    }
}
