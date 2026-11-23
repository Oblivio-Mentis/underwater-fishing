using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Raid.Editor
{
    public static class ExpeditionHarpoonMeshBaker
    {
        const string Folder = "Assets/_Game/Factory/HarpoonArt";
        const string Model = Folder + "/ExpeditionHarpoon.fbx";
        const string Menu = "RAID/Art/Rebake expedition harpoon from selected model";

        // The model can be dragged into a scene and unpacked to reposition or replace any of
        // its original parts. Select that model root to bake the meshes referenced by the game.
        [MenuItem(Menu)]
        public static void Bake()
        {
            var source = Selection.activeGameObject;
            if (!source) source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (!source || !source.GetComponentsInChildren<Transform>(true).Any(t => t.name == "RigDatum"))
                throw new InvalidOperationException("Select the ExpeditionHarpoon FBX or its editable scene instance.");
            var nodes = source.GetComponentsInChildren<Transform>(true);
            Transform Socket(string name) => nodes.Single(node => node.name == name);
            var datum = Socket("RigDatum").position;
            var forward = Socket("RigZPlus").position - datum;
            var up = Socket("RigYPlus").position - datum;
            if (forward.sqrMagnitude < .000001f || Vector3.Cross(forward, up).sqrMagnitude < .000001f)
                throw new InvalidOperationException("The model requires independent RigZPlus and RigYPlus axes.");
            var frame = Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(Quaternion.LookRotation(forward, up)),
                Vector3.one / forward.magnitude) * Matrix4x4.Translate(-datum);
            var spool = frame.MultiplyPoint3x4(Socket("SpoolPivot").position);
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            var inputs = new Dictionary<string, List<CombineInstance>>();
            foreach (var input in filters)
            {
                if (!input.sharedMesh) throw new InvalidOperationException("Missing model mesh on " + input.name);
                string group = Group(input.name);
                if (!inputs.TryGetValue(group, out var pieces))
                    inputs[group] = pieces = new List<CombineInstance>();
                var offset = group == "Cable drum" ? Matrix4x4.Translate(-spool) : Matrix4x4.identity;
                for (int slot = 0; slot < input.sharedMesh.subMeshCount; slot++)
                    pieces.Add(new CombineInstance { mesh = input.sharedMesh, subMeshIndex = slot,
                        transform = offset * frame * input.transform.localToWorldMatrix });
            }
            var results = new List<(Mesh output, Mesh built)>();
            try
            {
                foreach (string group in new[] { "Pressure housing", "Detachable spear", "Cable drum", "Breech shuttle", "Line pressure lamp" })
                {
                    if (!inputs.TryGetValue(group, out var pieces) || pieces.Count == 0)
                        throw new InvalidOperationException("Source model has no parts for " + group);
                    var target = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Meshes/" + group + ".asset");
                    if (!target) throw new InvalidOperationException("Missing authored destination mesh: " + group);
                    var built = new Mesh { name = group };
                    results.Add((target, built));
                    built.CombineMeshes(pieces.ToArray(), true, true, false);
                    built.RecalculateBounds();
                    if (built.vertexCount == 0) throw new InvalidOperationException("Could not read the geometry for " + group);
                }
                foreach (var pair in results)
                {
                    Undo.RegisterCompleteObjectUndo(pair.output, "Update expedition harpoon mesh");
                    EditorUtility.CopySerialized(pair.built, pair.output);
                    EditorUtility.SetDirty(pair.output);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"Updated {results.Count} expedition harpoon meshes from {source.name}.", source);
            }
            finally
            {
                foreach (var pair in results) UnityEngine.Object.DestroyImmediate(pair.built);
            }
        }

        static string Group(string part)
        {
            if (part == "LineStatusLens") return "Line pressure lamp";
            const string prefix = "tripo_part_";
            if (!part.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(part.Substring(prefix.Length), out int number)) return "Pressure housing";
            return number switch
            {
                7 or 20 => "Detachable spear",
                2 or 24 => "Cable drum",
                3 or 18 or 49 => "Breech shuttle",
                _ => "Pressure housing"
            };
        }
    }
}
