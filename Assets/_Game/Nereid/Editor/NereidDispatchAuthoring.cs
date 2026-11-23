using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class NereidDispatchAuthoring
    {
        const string Root = "Assets/_Game/Nereid/ShipRebuild/";
        const string Stations = Root + "Prefabs/Stations/";
        const string Materials = Root + "Materials/";

        const string Hall = Root+"Prefabs/Rooms/ObservationHall.prefab";
        const string Post = Stations+"Aquarium dispatch post.prefab";

        [MenuItem("Tools/NEREID/Fit aquarium dispatch post")]
        public static void Install()
        {
            var post=AssetDatabase.LoadAssetAtPath<GameObject>(Post);
            if (!post)
            {
                var model=BuildPost();
                try { post=PrefabUtility.SaveAsPrefabAsset(model,Post); }
                finally { Object.DestroyImmediate(model); }
            }
            using (var prefab=new PrefabUtility.EditPrefabContentsScope(Hall))
            {
                var hall=prefab.prefabContentsRoot.transform;
                if (!hall.GetComponentInChildren<NereidDispatchButton>(true))
                {
                    var installed=((GameObject)PrefabUtility.InstantiatePrefab(post,hall)).transform;
                    // The hall origin is two vessel units aft of the vessel origin. All controls are built in metres.
                    installed.SetLocalPositionAndRotation(new Vector3(3.12f,0,2.52f),Quaternion.Euler(0,107f,0));
                    installed.localScale=Vector3.one*.5f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(installed);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static GameObject BuildPost()
        {
            var root=new GameObject("Aquarium dispatch post");
            var t=root.transform;
            var frame=GetMaterial("Graphite forged frames");
            var metal=GetMaterial("Machined titanium");
            var ochre=GetMaterial("Rescue ochre");
            var glow=Signal("Dispatch key inlay",new Color(.85f,.9f,.83f));
            var foot=Solid(t,"Bolted deck flange",new Vector3(0,.043f,0),new(.89f,.086f,.65f),metal,PrimitiveType.Cube);
            foot.gameObject.AddComponent<BoxCollider>();
            for(int x=-1;x<=1;x+=2)
                for(int z=-1;z<=1;z+=2)
                    Solid(t,"Deck anchor",new Vector3(x*.32f,.092f,z*.224f),new(.074f,.015f,.074f),metal,PrimitiveType.Cylinder);
            var column=Solid(t,"Switchgear column",new Vector3(0,.50f,.039f),new(.39f,.84f,.36f),frame,PrimitiveType.Cube);
            column.gameObject.AddComponent<BoxCollider>();
            Box(t,"Service door",new Vector3(0,.52f,-.154f),new(.31f,.58f,.05f),ochre);
            Box(t,"Switchgear collar",new Vector3(0,.85f,.02f),new(.53f,.12f,.43f),metal);
            var head=Pivot(t,"Paired valve head",new Vector3(0,1.22f,0));
            head.localRotation=Quaternion.Euler(12f,0,0);
            var back=Solid(head,"Common watertight casting",new Vector3(0,.02f,.037f),new(1.62f,.67f,.30f),frame,PrimitiveType.Cube);
            back.gameObject.AddComponent<BoxCollider>();
            Box(head,"Valve head wear rail",new Vector3(0,-.28f,-.098f),new(1.55f,.07f,.12f),metal);
            Box(head,"Worklight hood",new Vector3(0,.35f,-.12f),new(1.3f,.085f,.22f),frame);
            Box(head,"Worklight diffuser",new Vector3(0,.308f,-.16f),new(.94f,.02f,.09f),glow);
            var lamp=Pivot(head,"Valve face lighting",new Vector3(0,.32f,-.57f)).gameObject.AddComponent<Light>();
            lamp.type=LightType.Point;lamp.range=2.4f;lamp.intensity=.75f;
            lamp.color=new Color(.91f,.94f,1f);lamp.shadows=LightShadows.None;
            CreateKey(head,false);
            CreateKey(head,true);
            return root;
        }

        static void CreateKey(Transform head,bool feed)
        {
            var chassis=Pivot(head,feed?"Food pipe control":"Processor pipe control",new Vector3(feed?.395f:-.395f,.015f,-.11f));
            var station=chassis.gameObject.AddComponent<ShipConsole>();
            station.kind=feed?ShipConsoleKind.Feed:ShipConsoleKind.Process;
            station.range=3.75f;
            var metal = GetMaterial("Machined titanium");
            var frame = GetMaterial("Graphite forged frames");
            var signal = Signal(feed ? "Feed key amber" : "Process key cyan",feed ? new Color(1f,.31f,.075f) : new Color(.13f,.84f,.86f));
            var ink = Signal("Dispatch key inlay",new Color(.85f,.9f,.83f));
            var body = GetMaterial(feed ? "Rescue ochre" : "Petrol marine enamel");
            var rubber = GetMaterial("Reinforced hose rubber");

            Box(chassis,"Valve escutcheon",new Vector3(0,0,-.008f),new Vector3(.69f,.55f,.078f),body);
            for(int side=-1;side<=1;side+=2)
            {
                Box(chassis,"Switch guard",new Vector3(side*.301f,0,-.14f),new(.060f,.40f,.14f),metal);
                for(int vertical=-1;vertical<=1;vertical+=2)
                    Disk(chassis,"Mount bolt",new Vector3(side*.298f,vertical*.224f,-.060f),.035f,.020f,metal);
            }
            Disk(chassis,"Pressure seal",new Vector3(0,0,-.135f),.598f,.093f,rubber);
            Disk(chassis,"Retaining ring",new Vector3(0,0,-.188f),.549f,.083f,metal);
            Disk(chassis,"Illuminated collar",new Vector3(0,0,-.229f),.504f,.030f,signal);
            Disk(chassis,"Piston well",new Vector3(0,0,-.249f),.465f,.029f,rubber);
            var cap = Pivot(chassis,"Sprung dispatch plunger",new Vector3(0,0,-.340f));
            Disk(cap,"Visible piston skirt",new Vector3(0,0,.044f),.411f,.147f,body);
            Disk(cap,"Palm pad",new Vector3(0,0,-.025f),.433f,.052f,frame);
            var contact = Pivot(cap,"Palm contact",new Vector3(0,0,-.055f));
            var icon = Pivot(contact,feed?"Feed • toothed jaws":"Process • recovery cycle",Vector3.zero).gameObject;
            icon.AddComponent<MeshFilter>().sharedMesh=Icon(feed);
            icon.AddComponent<MeshRenderer>().sharedMaterial=ink;

            // A stable strike target covers the cap throughout its travel. The screen and plinth cannot start the machine.
            var target = Pivot(chassis,"Dispatch strike area",new Vector3(0,0,-.334f));
            var face=Disk(target,"Operator contact face",new Vector3(0,0,-.050f),.445f,.027f,frame);
            face.GetComponent<MeshRenderer>().enabled=false;
            var collider=face.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=face.GetComponent<MeshFilter>().sharedMesh;
            var button=station.gameObject.AddComponent<NereidDispatchButton>();
            button.cap=cap; button.palmContact=contact; button.hitFace=collider;
            station.dispatchButton=button;
            station.screenAnchor=contact;
        }

        static Material Signal(string name,Color tint)
        {
            string path=Materials+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var template=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Nereid/Generated/Nereid_Lamp.mat");
            material=new Material(template){name=name};
            material.SetColor("_BaseColor",tint);
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",tint.linear*1.2f);
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        internal static Material GetMaterial(string name)
            => AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");

        internal static Transform Pivot(Transform parent,string name,Vector3 position)
        {
            var part=new GameObject(name).transform;
            part.SetParent(parent,false);part.localPosition=position;
            return part;
        }

        internal static Transform Solid(Transform parent,string name,Vector3 position,Vector3 size,Material surface,PrimitiveType primitive)
        {
            var part=GameObject.CreatePrimitive(primitive);
            part.name=name;Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<MeshRenderer>().sharedMaterial=surface;
            return part.transform;
        }

        internal static void Box(Transform parent,string name,Vector3 position,Vector3 size,Material surface)
            => Solid(parent,name,position,size,surface,PrimitiveType.Cube);

        internal static Transform Disk(Transform parent,string name,Vector3 position,float diameter,float depth,Material surface)
        {
            var disk=Solid(parent,name,position,new Vector3(diameter,depth*.5f,diameter),surface,PrimitiveType.Cylinder);
            disk.localRotation=Quaternion.Euler(90,0,0);return disk;
        }

        static Mesh Icon(bool feed)
        {
            string path=Root+"Meshes/"+(feed?"Feed key - jaws":"Process key - recovery cycle")+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) return mesh;
            var vertices=new List<Vector3>();
            var indices=new List<int>();
            void Tri(Vector2 a,Vector2 b,Vector2 c)
            {
                if ((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x)>0) (b,c)=(c,b);
                indices.Add(vertices.Count); vertices.Add(a);
                indices.Add(vertices.Count); vertices.Add(b);
                indices.Add(vertices.Count); vertices.Add(c);
            }
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d) { Tri(a,b,c);Tri(a,c,d); }
            if (feed)
            {
                for (int side=-1;side<=1;side+=2)
                {
                    Vector2 P(float x,float y)=>new(x,y*side);
                    Quad(P(-.153f,.084f),P(-.026f,.119f),P(-.030f,.087f),P(-.131f,.052f));
                    Quad(P(-.026f,.119f),P(.152f,.083f),P(.115f,.058f),P(-.030f,.087f));
                    Tri(P(-.116f,.066f),P(-.076f,.079f),P(-.090f,.014f));
                    Tri(P(-.049f,.089f),P(-.006f,.088f),P(-.020f,.030f));
                    Tri(P(.056f,.083f),P(.119f,.067f),P(.082f,-.002f));
                }
            }
            else
            {
                for(int arrow=0;arrow<3;arrow++)
                {
                    Vector2 Rot(Vector2 p,float angle) => Quaternion.Euler(0,0,angle+arrow*120f)*p;
                    for(int i=0;i<7;i++)
                    {
                        float a=i*11,b=(i+1)*11;
                        Quad(Rot(new(0,.122f),a),Rot(new(0,.122f),b),Rot(new(0,.084f),b),Rot(new(0,.084f),a));
                    }
                    Tri(Rot(new(.006f,.156f),77),Rot(new(.006f,.054f),77),Rot(new(-.054f,.104f),77));
                }
            }
            mesh=new Mesh{name=feed?"Feeding jaws":"Recovery cycle"};
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
