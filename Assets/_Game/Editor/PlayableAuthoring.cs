using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Raid.Editor
{
    public static class PlayableAuthoring
    {
        public const string Path="Assets/_Game/Scenes/FirstPlayable.unity";
        [MenuItem("RAID/Build playable scene")]
        public static void Create()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Stop and save active scene first");
            var scene=EditorSceneManager.OpenScene(RaidProjectBuilder.ScenePath);
            EditorSceneManager.SaveScene(scene,Path);
            var fishing=FishingAuthoring.CreateAssets("Assets/_Game/Data/Fishing");
            var prefab=PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/Diver.prefab");
            FishingAuthoring.ConfigureHarpoon(prefab,fishing);
            if(prefab.GetComponent<RescueTool>()==null) prefab.AddComponent<RescueTool>();
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/_Game/Prefabs/Diver.prefab");
            PrefabUtility.UnloadPrefabContents(prefab);
            var go=new GameObject("Game rules"); go.AddComponent<NetworkObject>();
            var world=go.AddComponent<RaidWorld>(); var threats=go.AddComponent<ThreatDirector>();
            go.AddComponent<RaidStatusHud>();
            world.tuning=Asset<WorldTuning>("WorldTuning"); world.progression=Asset<ProgressionTuning>("ProgressionTuning");
            threats.tuning=Asset<ThreatTuning>("ThreatTuning"); threats.anglerProfile=Asset<PredatorProfile>("PredatorProfile"); world.threats=threats;
            world.container=GameObject.Find("Cargo container").transform;
            var session=Object.FindFirstObjectByType<RaidSession>();
            var authenticator=session.gameObject.GetComponent<LobbyAuthenticator>();
            if(authenticator==null) authenticator=session.gameObject.AddComponent<LobbyAuthenticator>();
            authenticator.session=session; authenticator.lobby=session.lobby;
            var server=session.gameObject.GetComponent<FishNet.Managing.Server.ServerManager>();
            if(server==null) server=session.gameObject.AddComponent<FishNet.Managing.Server.ServerManager>();
            var serverSo=new SerializedObject(server); serverSo.FindProperty("_authenticator").objectReferenceValue=authenticator; serverSo.ApplyModifiedPropertiesWithoutUndo();
            for(int tier=1;tier<=2;tier++) for(int i=0;i<4;i++)
            {
                bool large=i%2==1;
                var fish=GameObject.CreatePrimitive(PrimitiveType.Sphere); fish.name="Depth "+tier+(large?" Bristlefish":" Lanternfish");
                fish.transform.position=new Vector3((i-1.5f)*5,-2-(i%2)*3,19+(i/2)*9);
                fish.transform.localScale=large?new Vector3(1.4f,.9f,2.2f):new Vector3(.8f,.5f,1.2f);
                fish.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Tank.mat");
                var actor=FishingAuthoring.ConfigureFish(fish,fishing,large?1.5f*tier:1f*tier);
                actor.depthTier=tier; actor.fishId=large?"Bristlefish":"Lanternfish";
                actor.biomass=large?6*tier:3*tier; actor.chitin=tier==2?(large?3:1):0;
                actor.offering=large?6:3; actor.cargoSize=large?3:1;
                var tail=GameObject.CreatePrimitive(PrimitiveType.Cube);tail.name="Tail";tail.transform.SetParent(fish.transform,false);
                tail.transform.localPosition=new Vector3(0,0,-.65f);tail.transform.localScale=new Vector3(.1f,.65f,.4f);
                Object.DestroyImmediate(tail.GetComponent<Collider>());tail.GetComponent<Renderer>().sharedMaterial=fish.GetComponent<Renderer>().sharedMaterial;
            }
            for(int tier=1;tier<=2;tier++) for(int i=0;i<3;i++)
            {
                var loot=GameObject.CreatePrimitive(PrimitiveType.Cube); loot.name=tier==1?"Scrap crate":"Electronics crate";
                loot.transform.position=new Vector3((i-1)*8,-5,25+i*5); loot.transform.localScale=Vector3.one*.6f;
                loot.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Safety.mat");
                loot.AddComponent<NetworkObject>(); var actor=loot.AddComponent<LootActor>(); actor.tier=tier;
                actor.scrap=3; actor.electronics=tier==2?2:0;
            }
            var rubble=new GameObject("Rubble rescue gym"); rubble.transform.position=new Vector3(-8,-4,20);
            var trigger=rubble.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.size=new Vector3(4,3,4);
            rubble.AddComponent<Rigidbody>().isKinematic=true; rubble.AddComponent<RubbleTrap>();
            for(int i=0;i<3;i++)
            {
                var stone=GameObject.CreatePrimitive(PrimitiveType.Cube); stone.name="Loose wreckage";stone.transform.position=new Vector3(-9+i,-4.5f,20+(i%2));
                stone.transform.localScale=new Vector3(1.4f,.6f,1);stone.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Safety.mat");
            }
            foreach(var label in Object.FindObjectsByType<TextMesh>()) label.characterSize*=.15f;
            SerializeNetworkObjects();
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Path,true),new EditorBuildSettingsScene(RaidProjectBuilder.ScenePath,false),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",false)};
            AssetDatabase.SaveAssets(); Debug.Log("[RAID] FirstPlayable generated");
        }
        [MenuItem("RAID/Serialize authored network objects")]
        public static void SerializeNetworkObjects()
        {
            // This is the same native operation used by FishNet's reserialization window.
            var method=typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(method==null) throw new System.MissingMethodException("FishNet editor reserialization API changed");
            foreach(var nob in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include))
            { method.Invoke(nob,new object[]{true,false}); EditorUtility.SetDirty(nob); }
            var sceneIds=typeof(NetworkObject).GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            foreach(var sceneMethod in sceneIds) if(sceneMethod.Name=="CreateSceneId" && sceneMethod.GetParameters().Length==3)
                sceneMethod.Invoke(null,new object[]{UnityEngine.SceneManagement.SceneManager.GetActiveScene(),true,0});
            var prefab=AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/_Game/Prefabs/Diver.prefab");
            method.Invoke(prefab,new object[]{true,false}); EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
        }
        private static T Asset<T>(string name) where T:ScriptableObject
        {
            string path="Assets/_Game/Data/"+name+".asset"; var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null) { asset=ScriptableObject.CreateInstance<T>(); if(asset is ProgressionTuning progression) ConfigureProgressionDefaults(progression); AssetDatabase.CreateAsset(asset,path); }
            return asset;
        }
        public static void ConfigureProgressionDefaults(ProgressionTuning tuning)
        {
            tuning.Recipe(Upgrade.Reel).additionalPerLevel=new MaterialCost(0,2);
            tuning.Recipe(Upgrade.Fins).additionalPerLevel=new MaterialCost(0,0,2);
            tuning.Recipe(Upgrade.Tank).additionalPerLevel=new MaterialCost(0,1);
            tuning.Recipe(Upgrade.Winch).initialCost=new MaterialCost(12,0,2);
            tuning.Recipe(Upgrade.Winch).additionalPerLevel=new MaterialCost(0,0,0,1);
            EditorUtility.SetDirty(tuning);
        }
    }
}
