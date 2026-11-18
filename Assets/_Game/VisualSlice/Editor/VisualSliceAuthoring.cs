using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
namespace Raid.VisualSlice.Editor
{
    public static class VisualSliceAuthoring
    {
        public const string ScenePath="Assets/_Game/Scenes/VisualVerticalSlice.unity";
        [MenuItem("RAID/010/Create or refresh visual slice")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save current scene before switching");
            UnityEngine.SceneManagement.Scene scene;
            if(System.IO.File.Exists(ScenePath))scene=EditorSceneManager.OpenScene(ScenePath);
            else {scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/FirstPlayable.unity");EditorSceneManager.SaveScene(scene,ScenePath);}
            var existing=GameObject.Find("RAID010 Visuals");if(existing!=null)Object.DestroyImmediate(existing);
            var root=new GameObject("RAID010 Visuals").transform;
            foreach(var go in scene.GetRootGameObjects())
            {
                if(go==root.gameObject)continue;
                string n=go.name;
                bool hidden=n=="Deck"||n=="Roof"||n.Contains("wall")||n=="Aft bulkhead"||n.StartsWith("Front ")||n=="Airlock lintel"||n=="Central catch reservoir"||n=="Cargo container"||n=="Seabed"||n.StartsWith("Rock ")||go.GetComponent<TextMesh>()!=null;
                if(hidden && go.GetComponent<Renderer>()!=null)go.GetComponent<Renderer>().enabled=false;
                if(n=="Interior lamp")go.GetComponent<Light>().enabled=false;
            }
            ShipVisual.Build(root);
            DepthOneVisual.Build(root);
            ParasiteVisual.Build(root);
            DressFish();
            var presentation=root.gameObject.AddComponent<SlicePresentation>();presentation.cargo=GameObject.Find("Cargo container").transform;
            presentation.steel=VisualKit.Mat("Navy");presentation.amber=VisualKit.Mat("Amber");presentation.glow=VisualKit.Mat("Trim");
            var stock=VisualKit.Group("Reservoir stock / real inventory",root,new Vector3(0,0,-3));
            presentation.tankFish=new Transform[5];
            for(int i=0;i<5;i++)
            {
                var visual=new GameObject("Stock fish "+i);visual.transform.SetParent(stock,false);visual.transform.localScale=new Vector3(.45f,.22f,.65f);
                visual.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(VisualKit.AssetsPath+"/Meshes/Lanternfish_body.asset");
                visual.AddComponent<MeshRenderer>().sharedMaterial=VisualKit.Mat("Trim");visual.SetActive(false);presentation.tankFish[i]=visual.transform;
                foreach(int side in new[]{-1,1})
                {
                    VisualKit.Part("Stock tail lobe",visual.transform,PrimitiveType.Cube,new Vector3(0,side*.13f,-.57f),new Vector3(.04f,.32f,.25f),VisualKit.Mat("Amber"),Quaternion.Euler(side*35,0,0));
                    VisualKit.Part("Stock eye",visual.transform,PrimitiveType.Sphere,new Vector3(side*.27f,.1f,.3f),new Vector3(.08f,.08f,.08f),VisualKit.Mat("Navy"));
                }
            }
            var cable=VisualKit.Group("Hoist cable",root).gameObject.AddComponent<LineRenderer>();cable.positionCount=2;cable.startWidth=cable.endWidth=.035f;cable.sharedMaterial=VisualKit.Mat("Navy");presentation.hoistCable=cable;
            VisualKit.Part("Winch boom",root,PrimitiveType.Cube,new Vector3(4,4.2f,10.5f),new Vector3(.25f,.25f,5),VisualKit.Mat("Amber"));
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();sun.color=new Color(.5f,.78f,.87f);sun.intensity=1.1f;sun.shadows=LightShadows.Soft;
            RenderSettings.skybox=null;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=18;RenderSettings.fogEndDistance=85;RenderSettings.fogColor=new Color(.1f,.25f,.3f);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.62f,.7f);
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.4f,.55f,.63f));RenderSettings.ambientProbe=ambient;
            var fill=VisualKit.Group("Ocean reflected fill",root).gameObject.AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.5f,.75f,.82f);fill.intensity=1.5f;fill.transform.rotation=Quaternion.Euler(25,145,0);
            string profilePath=VisualKit.AssetsPath+"/SliceVolume.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);
                var bloom=profile.Add<Bloom>();bloom.intensity.Override(.25f);bloom.threshold.Override(1.3f);AssetDatabase.AddObjectToAsset(bloom,profile);
                var tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,profile);
                var vignette=profile.Add<Vignette>();vignette.intensity.Override(.13f);AssetDatabase.AddObjectToAsset(vignette,profile);
                EditorUtility.SetDirty(profile);
            }
            var volume=VisualKit.Group("Underwater grading",root).gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("[RAID010] Visual scene saved; original colliders, network IDs, prefabs and build settings preserved.");
        }
        static void DressFish()
        {
            foreach(var fish in Object.FindObjectsByType<FishActor>())
            {
                var old=fish.transform.Find("RAID010 fish fins");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var oldTail=fish.transform.Find("Tail");if(oldTail!=null)oldTail.GetComponent<Renderer>().enabled=false;
                var fins=VisualKit.Group("RAID010 fish fins",fish.transform);
                bool large=fish.cargoSize>1;
                var meshHolder=VisualKit.MeshObject(large?"Bristlefish_body":"Lanternfish_body",fins,FishMesh(),VisualKit.Mat(large?"Amber":"Trim"));
                fish.GetComponent<MeshFilter>().sharedMesh=meshHolder.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(meshHolder);
                fish.GetComponent<Renderer>().sharedMaterial=VisualKit.Mat(large?"Amber":"Trim");
                foreach(int side in new[]{-1,1})
                {
                    VisualKit.Part("Pectoral fin",fins,PrimitiveType.Cube,new Vector3(side*.38f,-.03f,-.05f),new Vector3(.6f,.035f,.32f),VisualKit.Mat("Hull"),Quaternion.Euler(0,side*35,side*20));
                    VisualKit.Part("Eye",fins,PrimitiveType.Sphere,new Vector3(side*.34f,.12f,.28f),new Vector3(.08f,.08f,.08f),VisualKit.Mat("Glow"));
                }
                VisualKit.Part("Dorsal crest",fins,PrimitiveType.Cube,new Vector3(0,.34f,-.08f),new Vector3(.04f,large?.7f:.25f,.5f),VisualKit.Mat("Hull"),Quaternion.Euler(20,0,0));
                var tail=VisualKit.Group("Fork tail",fins,new Vector3(0,0,-.48f));
                foreach(int side in new[]{-1,1})VisualKit.Part("Tail lobe",tail,PrimitiveType.Cube,new Vector3(0,side*.16f,-.12f),new Vector3(.05f,.4f,.23f),VisualKit.Mat("Trim"),Quaternion.Euler(side*35,0,0));
                var motion=fins.gameObject.AddComponent<FishVisualMotion>();motion.tail=tail;motion.actor=fish;
            }
        }
        static Mesh FishMesh()
        {
            float[] z={-.5f,-.3f,0,.28f,.53f};float[] width={.045f,.24f,.48f,.3f,.025f};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int ring=0;ring<4;ring++)for(int side=0;side<10;side++)
            {
                float a=side*Mathf.PI*.2f,b=(side+1)*Mathf.PI*.2f;int n=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*width[ring],Mathf.Sin(a)*width[ring],z[ring]));
                vertices.Add(new Vector3(Mathf.Cos(b)*width[ring],Mathf.Sin(b)*width[ring],z[ring]));
                vertices.Add(new Vector3(Mathf.Cos(a)*width[ring+1],Mathf.Sin(a)*width[ring+1],z[ring+1]));
                vertices.Add(new Vector3(Mathf.Cos(b)*width[ring+1],Mathf.Sin(b)*width[ring+1],z[ring+1]));
                triangles.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
            }
            var mesh=new Mesh{name="Faceted fish body"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
        }
    }
}
