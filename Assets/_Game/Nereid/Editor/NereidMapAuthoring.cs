using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class NereidMapAuthoring
    {
        const string Main = "Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Maps = "Assets/_Game/Maps";
        const string First = Maps + "/Pale Shoals";
        const string Second = Maps + "/Needle Rift";

        [MenuItem("RAID/NEREID/Validate editable expedition maps")]
        public static void Validate()
        {
            var reef = Object.FindAnyObjectByType<NereidReef>();
            if (!reef || !reef.Shoal || !reef.Rift) throw new InvalidOperationException("Missing authored expedition maps.");
            foreach (var region in new[]{reef.Shoal,reef.Rift})
            {
                string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(region);
                if (string.IsNullOrEmpty(prefab)) throw new InvalidOperationException(region.name + " has no prefab.");
                int filters=0, triangles=0;
                foreach(var f in region.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!f.sharedMesh || !AssetDatabase.Contains(f.sharedMesh)) throw new InvalidOperationException("Unsaved mesh on " + f.name);
                    filters++;triangles+=f.sharedMesh.triangles.Length/3;
                }
                var colliders=region.GetComponentsInChildren<MeshCollider>(true);
                if (filters < 2 || colliders.Length == 0) throw new InvalidOperationException("Incomplete area " + region.name);
                foreach(var collider in colliders)
                    if (!collider.sharedMesh || !AssetDatabase.Contains(collider.sharedMesh))
                        throw new InvalidOperationException("Unsaved collision on "+collider.name);
                Debug.Log($"[NEREID MAP] {prefab}: {filters} saved mesh instances; {triangles} triangles; {colliders.Length} mesh colliders.");
            }
            var sea=new SerializedObject(reef);
            foreach(string key in new[]{"shallowBed","deepBed","horizon"})
                if(!sea.FindProperty(key).objectReferenceValue) throw new InvalidOperationException("Missing map reference "+key);
            var life=reef.GetComponent<NereidWaterLife>();
            if(!life || life.FirstHoldfast==Vector3.zero)throw new InvalidOperationException("No saved kelp.");
        }

        // One-time migration for the original scene. The playable map subsequently consists of standard prefab instances.
        public static void BakeOriginalMaps()
        {
            var scene=EditorSceneManager.OpenScene(Main);
            if (Object.FindAnyObjectByType<NereidReef>()) { Validate(); return; }
            string shallowPath=First+"/Pale Shoals.prefab",deepPath=Second+"/Needle Rift.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(shallowPath) || AssetDatabase.LoadAssetAtPath<GameObject>(deepPath))
                throw new InvalidOperationException("Original map import would overwrite existing map assets.");
            foreach(string dir in new[]{Maps,First,Second,First+"/Meshes",Second+"/Meshes",Maps+"/Shared",Maps+"/Materials"})
            {
                if(!AssetDatabase.IsValidFolder(dir))AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\','/'),Path.GetFileName(dir));
            }
            var root=new GameObject("NEREID fishing grounds");
            var reef=root.AddComponent<NereidReef>();
            reef.BuildSourceMeshes();
            reef.Shoal.name="Pale Shoals";
            reef.Rift.name="Needle Rift";
            SaveGeometry(reef.Shoal,First+"/Meshes");
            SaveGeometry(reef.Rift,Second+"/Meshes");
            SaveGeometry(root,Maps+"/Shared");
            foreach(var loot in Object.FindObjectsByType<LootActor>())
            {
                SaveGeometry(loot.gameObject,Maps+"/Shared");
                foreach(var part in loot.GetComponents<Component>())
                {
                    if(PrefabUtility.IsPartOfPrefabInstance(part))PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                    EditorUtility.SetDirty(part);
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(reef.Shoal,shallowPath,InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(reef.Rift,deepPath,InteractionMode.AutomatedAction);
            reef.Rift.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(reef.Rift);
            var vessel=Object.FindAnyObjectByType<NereidVessel>();
            static void Deactivate(GameObject go)
            {
                if(!go)return;
                go.SetActive(false);
                if(PrefabUtility.IsPartOfPrefabInstance(go))PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
            Deactivate(vessel.transform.Find("Industrial fishing seabed")?.gameObject);
            foreach(var item in scene.GetRootGameObjects())
            {
                if(item.name=="Seabed")Deactivate(item);
                if(item.name=="RAID010 Visuals")Deactivate(item.transform.Find("DepthOneEnvironment")?.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save "+Main);
            EditorSceneManager.OpenScene(Main);
            Validate();
        }

        static void SaveGeometry(GameObject root,string meshFolder)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                SaveAsset(filter.sharedMesh,meshFolder,".asset");
            foreach(var surface in root.GetComponentsInChildren<Renderer>(true))
                foreach(var material in surface.sharedMaterials)SaveAsset(material,Maps+"/Materials",".mat");
            foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))
                SaveAsset(collider.sharedMesh,meshFolder,".asset");
        }

        static void SaveAsset(Object asset,string folder,string extension)
        {
            if(!asset || EditorUtility.IsPersistent(asset))return;
            string name=asset.name;
            foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'-');
            name=name.Replace('/','-').Trim();
            AssetDatabase.CreateAsset(asset,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+extension));
        }
    }
}
