using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        // Both fairings participate in several compartments. Rebuild their shared meshes
        // from the complete hull profile after changing any port, without replacing prefab instances.
        [MenuItem("Tools/NEREID/Rebake port and starboard side fairings")]
        public static void RebakeSideFairings()
        {
            var hull = PrefabUtility.LoadPrefabContents(Envelope);
            var generated = new GameObject("Side fairing authoring");
            try
            {
                body = generated.transform;
                assemblies.Clear();
                Palette();
                PressureForm();
                var panels = new[] {"Port","Starboard"}.Select(hand =>
                {
                    string label = hand + " side fairings and window reveals";
                    var part = assemblies.Single(p => p.mesh.node.name == label);
                    var panel = hull.transform.Find(label);
                    string path = Folder + "/Meshes/" + label + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (!panel || !mesh || !panel.TryGetComponent(out MeshFilter filter) || filter.sharedMesh != mesh ||
                        !panel.TryGetComponent(out MeshCollider collider) || collider.sharedMesh != mesh ||
                        !panel.TryGetComponent(out MeshRenderer renderer))
                        throw new InvalidOperationException("Check the mesh, collider and renderer of " + label);
                    return (part,mesh,path,renderer);
                }).ToArray();

                bool changedSlots = false;
                foreach (var (assembly,original,path,renderer) in panels)
                {
                    var existing = renderer.sharedMaterials;
                    assembly.mesh.Save(path);
                    var next = assembly.mesh.node.GetComponent<MeshRenderer>().sharedMaterials;
                    for (int i = 0; i < Mathf.Min(existing.Length,next.Length); i++)
                        if (existing[i] && !AssetDatabase.GetAssetPath(existing[i]).StartsWith(Folder + "/Materials/",StringComparison.Ordinal))
                            next[i] = existing[i];
                    if (!existing.SequenceEqual(next))
                    {
                        renderer.sharedMaterials = next;
                        changedSlots = true;
                    }
                    AssetDatabase.SaveAssetIfDirty(original);
                }
                if (changedSlots) PrefabUtility.SaveAsPrefabAsset(hull,Envelope);
            }
            finally
            {
                assemblies.Clear();
                body = null;
                Object.DestroyImmediate(generated);
                PrefabUtility.UnloadPrefabContents(hull);
            }
        }
    }
}
