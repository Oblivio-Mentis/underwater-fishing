using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Raid.Editor
{
    public static class RaidProjectBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Foundation.unity";
        [MenuItem("RAID/Build foundation scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save user scene before generating Foundation");
            foreach (string folder in new[]{"Scenes","Prefabs","Materials"})
                System.IO.Directory.CreateDirectory("Assets/_Game/" + folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var steel = Material("Steel",new Color(.12f,.2f,.23f));
            var trim = Material("Trim",new Color(.35f,.46f,.44f));
            var orange = Material("Safety",new Color(.95f,.49f,.12f));
            var sea = Material("Seabed",new Color(.12f,.24f,.25f));
            var glass = Material("Tank",new Color(.12f,.55f,.59f));
            Box("Deck",new Vector3(0,-.3f,0),new Vector3(12,.5f,16),steel);
            Box("Roof",new Vector3(0,4.4f,0),new Vector3(12,.5f,16),steel);
            Box("Port wall",new Vector3(-6,2,0),new Vector3(.3f,4.5f,16),steel);
            Box("Starboard wall",new Vector3(6,2,0),new Vector3(.3f,4.5f,16),steel);
            Box("Aft bulkhead",new Vector3(0,2,-8),new Vector3(12,4.5f,.3f),steel);
            Box("Front left",new Vector3(-4,2,8),new Vector3(4,4.5f,.3f),steel);
            Box("Front right",new Vector3(4,2,8),new Vector3(4,4.5f,.3f),steel);
            Box("Airlock lintel",new Vector3(0,3.7f,8),new Vector3(4,1,.3f),orange);
            Box("Central catch reservoir",new Vector3(0,.6f,-3),new Vector3(3,1.3f,3),glass);
            Label("RESERVOIR",new Vector3(-1.4f,1.6f,-4.6f),.25f);
            Label("AIRLOCK / DIVE",new Vector3(-1.6f,3.1f,7.7f),.25f);
            Box("Outside platform",new Vector3(0,-.5f,13),new Vector3(10,.5f,10),trim);
            Box("Cargo container",new Vector3(4,.4f,13),new Vector3(2,1.4f,2),orange);
            Label("CARGO / WINCH",new Vector3(2.8f,1.4f,11.8f),.2f);
            Box("Seabed",new Vector3(0,-18,15),new Vector3(100,1,100),sea);
            for(int i=0;i<12;i++)
                Box("Rock "+i,new Vector3((i%4-1.5f)*14,-15,18+(i/4)*12),new Vector3(5,5+(i%3)*2,6),sea);
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.1f; light.color=new Color(.45f,.75f,.85f);
            light.transform.rotation=Quaternion.Euler(40,-30,0);
            for(int i=0;i<3;i++)
            {
                var lamp=new GameObject("Interior lamp").AddComponent<Light>();
                lamp.type=LightType.Point; lamp.range=12; lamp.intensity=5; lamp.color=new Color(.6f,1,.92f);
                lamp.transform.position=new Vector3(0,3,(i-1)*5);
            }
            RenderSettings.ambientLight=new Color(.2f,.34f,.4f);
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.025f,.13f,.17f);
            RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.025f;
            RenderSettings.skybox=null;
            var camera=new GameObject("Menu Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.position=new Vector3(4,3,-6); camera.transform.LookAt(new Vector3(0,1,4));
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=RenderSettings.fogColor;

            var tuning=AssetDatabase.LoadAssetAtPath<DiverTuning>("Assets/_Game/Data/DiverTuning.asset");
            if(tuning==null) { tuning=ScriptableObject.CreateInstance<DiverTuning>(); AssetDatabase.CreateAsset(tuning,"Assets/_Game/Data/DiverTuning.asset"); }
            var diver=new GameObject("Diver");
            diver.transform.position=new Vector3(0,1.2f,0);
            var controller=diver.AddComponent<CharacterController>(); controller.height=1.8f; controller.radius=.35f;
            diver.AddComponent<NetworkObject>();
            var motor=diver.AddComponent<DiverMotor>(); motor.tuning=tuning;
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Diver body"; body.transform.SetParent(diver.transform,false);
            Object.DestroyImmediate(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial=orange;
            var visor=GameObject.CreatePrimitive(PrimitiveType.Cube); visor.name="Visor"; visor.transform.SetParent(diver.transform,false);
            visor.transform.localPosition=new Vector3(0,.55f,.3f); visor.transform.localScale=new Vector3(.5f,.3f,.2f);
            Object.DestroyImmediate(visor.GetComponent<Collider>()); visor.GetComponent<Renderer>().sharedMaterial=glass;
            var prefab=PrefabUtility.SaveAsPrefabAsset(diver,"Assets/_Game/Prefabs/Diver.prefab");
            Object.DestroyImmediate(diver);
            var objects=ScriptableObject.CreateInstance<SinglePrefabObjects>(); objects.AddObject(prefab.GetComponent<NetworkObject>());
            AssetDatabase.CreateAsset(objects,"Assets/_Game/Data/SpawnablePrefabs.asset");

            var net=new GameObject("RAID Network"); net.SetActive(false);
            var local=net.AddComponent<Tugboat>(); local.SetPort(7770); local.SetMaximumClients(5);
            var steam=net.AddComponent<FishySteamworks.FishySteamworks>();
            var steamSo=new SerializedObject(steam); steamSo.FindProperty("_peerToPeer").boolValue=true;
            steamSo.FindProperty("_maximumClients").intValue=5; steamSo.ApplyModifiedPropertiesWithoutUndo();
            var mp=net.AddComponent<Multipass>();
            var mpSo=new SerializedObject(mp); var list=mpSo.FindProperty("_transports"); list.arraySize=2;
            list.GetArrayElementAtIndex(0).objectReferenceValue=local; list.GetArrayElementAtIndex(1).objectReferenceValue=steam;
            mpSo.ApplyModifiedPropertiesWithoutUndo();
            net.AddComponent<TransportManager>().Transport=mp;
            var manager=net.AddComponent<NetworkManager>(); manager.SpawnablePrefabs=objects;
            var spawner=net.AddComponent<PlayerSpawner>(); spawner.SetPlayerPrefab(prefab.GetComponent<NetworkObject>());
            spawner.Spawns=new Transform[5];
            for(int i=0;i<5;i++) { var spawn=new GameObject("Spawn "+i).transform; spawn.position=new Vector3((i-2)*1.5f,1.2f,1); spawner.Spawns[i]=spawn; }
            var session=net.AddComponent<RaidSession>(); var lobby=net.AddComponent<SteamLobby>();
            session.manager=manager; session.localTransport=local; session.steamTransport=steam; session.multipass=mp;
            session.menuCamera=camera; session.lobby=lobby; lobby.session=session;
            net.SetActive(true);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",false)};
            PlayerSettings.companyName="Oblivio Mentis"; PlayerSettings.productName="Underwater Fishing";
            PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();
            Debug.Log("[RAID] Foundation scene and network prefabs generated.");
        }

        private static Material Material(string name,Color color)
        {
            string path="Assets/_Game/Materials/"+name+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null) { result=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result,path); }
            result.color=color; return result;
        }
        private static GameObject Box(string name,Vector3 position,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=position;
            go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=mat; return go;
        }
        private static void Label(string text,Vector3 position,float size)
        {
            var label=new GameObject(text).AddComponent<TextMesh>(); label.text=text;
            label.transform.position=position; label.characterSize=size*.15f; label.fontSize=48; label.color=Color.white;
        }
    }
}
