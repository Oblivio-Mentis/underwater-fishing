using System.IO;
using Raid.Nereid;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    // One menu command installs the pet's own wet hatch on the starboard skin beside the aquarium:
    // a prefab with the mouth, a dry tray, a flap and a short glass pressure line into the real reservoir.
    // Everything stays editable afterwards: move the knots with NEREID / Pipe Route Editor, move the prefab itself.
    public static class GoshaReceiverAuthoring
    {
        const string Folder = "Assets/_Game/Nereid/ShipRebuild/Prefabs/Cargo";
        const string PrefabPath = Folder + "/GoshaReceiver.prefab";
        const string Plumbing = "Assets/_Game/Nereid/ShipRebuild/Prefabs/Plumbing/PressureLine.prefab";

        [MenuItem("NEREID/Install Gosha receiver")]
        public static void Install()
        {
            var vessel = Object.FindAnyObjectByType<NereidVessel>();
            if (!vessel || Application.isPlaying) throw new System.InvalidOperationException("Open the rebuilt vessel in Edit Mode.");
            var rail = vessel.authoredRail;
            if (!rail || !rail.aquarium) throw new System.InvalidOperationException("Install cargo and pipes first.");
            var hull = vessel.transform;
            var existing = Object.FindAnyObjectByType<GoshaReceiver>();
            if (existing) { Debug.Log("Gosha receiver already installed: " + existing.name); Selection.activeObject = existing.gameObject; return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hull);
            instance.name = "Gosha receiver";
            // Starboard skin at aquarium height, just forward of the reservoir: the probe found the plating at x≈6.9 (hull units).
            instance.transform.localPosition = new Vector3(6.75f, -1.05f, -1.2f);
            instance.transform.localRotation = Quaternion.Euler(0, 90, 0);

            var receiver = instance.GetComponent<GoshaReceiver>();
            var so = new SerializedObject(receiver);
            var pipeStart = so.FindProperty("pipeStart").objectReferenceValue as Transform;
            var pipeEnd = so.FindProperty("pipeEnd").objectReferenceValue as Transform;

            // The glass line reuses the ship's own pressure-line prefab and the aquarium's inlet coupling volume.
            var inletDestination = rail.inlet ? rail.inlet.destination : null;
            var tankModule = inletDestination ? inletDestination.transform.parent : rail.aquarium;
            var endSocket = new GameObject("GoshaInlet coupling").transform;
            endSocket.SetParent(tankModule, false);
            endSocket.position = rail.aquarium.TransformPoint(new Vector3(rail.aquariumVolume.extents.x * .92f, rail.aquariumVolume.extents.y * .55f, rail.aquariumVolume.center.z + 1.1f));
            var endPort = endSocket.gameObject.AddComponent<PipeSocket>();
            endPort.port = PipePort.ReservoirInlet; endPort.fitting = tankModule;
            endPort.connectionVolume = new Bounds(endSocket.localPosition, Vector3.one * .6f);
            var startPort = pipeStart.gameObject.GetComponent<PipeSocket>() ?? pipeStart.gameObject.AddComponent<PipeSocket>();
            startPort.port = PipePort.Receiver; startPort.fitting = instance.transform;
            startPort.connectionVolume = new Bounds(pipeStart.localPosition, Vector3.one * .4f);

            var linePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Plumbing);
            if (!linePrefab) throw new System.InvalidOperationException("PressureLine.prefab missing; run NEREID/Install Cargo and Pipes first.");
            var lineObject = (GameObject)PrefabUtility.InstantiatePrefab(linePrefab, instance.transform);
            var line = lineObject.GetComponent<NereidPipeRoute>();
            line.name = "Gosha inlet pressure line";
            line.line = PipeLine.Inlet; line.radius = .22f;
            if (rail.inlet) { line.sightGlass = rail.inlet.sightGlass; line.metal = rail.inlet.metal; line.enamel = rail.inlet.enamel; }
            line.source = startPort; line.destination = endPort;
            line.knots.Clear();
            Vector3 a = line.transform.InverseTransformPoint(startPort.transform.position);
            Vector3 b = line.transform.InverseTransformPoint(endPort.transform.position);
            line.knots.Add(new PipeKnot { position = a });
            line.knots.Add(new PipeKnot { position = Vector3.Lerp(a, b, .3f) + Vector3.up * .25f, section = PipeSection.Glass });
            line.knots.Add(new PipeKnot { position = Vector3.Lerp(a, b, .7f) + Vector3.up * .1f, section = PipeSection.Hanger });
            line.knots.Add(new PipeKnot { position = b });
            line.Rebuild();
            PrefabUtility.RecordPrefabInstancePropertyModifications(line);
            so.FindProperty("pipeEnd").objectReferenceValue = endSocket;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(receiver);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(vessel.gameObject.scene);
            Selection.activeObject = instance;
            Debug.Log("Gosha receiver installed at hull-local " + instance.transform.localPosition);
        }

        static GameObject BuildPrefab()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) Directory.CreateDirectory(Folder);
            var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Nereid/ShipRebuild/Materials/Machined titanium.mat");
            var enamel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Nereid/ShipRebuild/Materials/Petrol marine enamel.mat");
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Nereid/ShipRebuild/Materials/Laminated observation glass.mat");
            var lamp = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Nereid/ShipRebuild/Materials/Reservoir teal light.mat");

            var root = new GameObject("GoshaReceiver");
            try
            {
                var receiver = root.AddComponent<GoshaReceiver>();
                // Collar: a short ring set into the plating, 70 cm across. Local +Z points out to sea.
                Part(PrimitiveType.Cylinder, root.transform, "Collar ring", new Vector3(0, 0, .12f), Quaternion.Euler(90, 0, 0), new Vector3(.78f, .09f, .78f), steel);
                Part(PrimitiveType.Cylinder, root.transform, "Throat", new Vector3(0, 0, -.25f), Quaternion.Euler(90, 0, 0), new Vector3(.6f, .34f, .6f), enamel);
                var flap = Part(PrimitiveType.Cube, root.transform, "Pressure flap", new Vector3(0, .31f, .2f), Quaternion.identity, new Vector3(.62f, .04f, .62f), steel);
                flap.name = "Pressure flap";
                var hinge = new GameObject("Flap hinge").transform; hinge.SetParent(root.transform, false); hinge.localPosition = new Vector3(0, .33f, .2f);
                flap.SetParent(hinge, true); flap.localPosition = new Vector3(0, -.02f, 0);
                var lampPart = Part(PrimitiveType.Sphere, root.transform, "Ready lamp", new Vector3(.42f, .36f, .14f), Quaternion.identity, Vector3.one * .1f, lamp);
                lampPart.name = "Ready lamp";
                // Dry tray below the collar for materials: a shallow shelf with a lip.
                var tray = Part(PrimitiveType.Cube, root.transform, "Dry tray", new Vector3(0, -.62f, .3f), Quaternion.identity, new Vector3(.9f, .06f, .55f), steel);
                Part(PrimitiveType.Cube, tray, "Tray lip", new Vector3(0, .6f, .5f), Quaternion.identity, new Vector3(1f, 1.2f, .12f), enamel);
                Part(PrimitiveType.Cylinder, root.transform, "Sight glass", new Vector3(0, 0, -.52f), Quaternion.Euler(90, 0, 0), new Vector3(.46f, .12f, .46f), glass);

                var mouth = new GameObject("Mouth").transform; mouth.SetParent(root.transform, false); mouth.localPosition = new Vector3(0, 0, .95f);
                var pipeStart = new GameObject("Pipe start").transform; pipeStart.SetParent(root.transform, false); pipeStart.localPosition = new Vector3(0, 0, -.62f);
                var dry = new GameObject("Dry slot").transform; dry.SetParent(root.transform, false); dry.localPosition = new Vector3(0, -.5f, .75f);

                var so = new SerializedObject(receiver);
                so.FindProperty("mouth").objectReferenceValue = mouth;
                so.FindProperty("pipeStart").objectReferenceValue = pipeStart;
                so.FindProperty("dryTray").objectReferenceValue = dry;
                so.FindProperty("flapHinge").objectReferenceValue = hinge;
                so.FindProperty("readyLamp").objectReferenceValue = lampPart.GetComponent<Renderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 at, Quaternion rotation, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localRotation = rotation; go.transform.localScale = size;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
