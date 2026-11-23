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

        [MenuItem("Tools/NEREID/Fit fish dispatch buttons")]
        public static void Install()
        {
            foreach (var kind in new[] {ShipConsoleKind.Process, ShipConsoleKind.Feed})
            {
                using var prefab = new PrefabUtility.EditPrefabContentsScope(Stations+kind+".prefab");
                Fit(prefab.prefabContentsRoot.GetComponent<ShipConsole>());
            }
            AssetDatabase.SaveAssets();
        }

        internal static void Fit(ShipConsole station)
        {
            if (station.dispatchButton) return;
            bool feed = station.kind == ShipConsoleKind.Feed;
            var root = station.transform;
            var monitor = station.GetComponent<NereidInstrument>().readout;
            monitor.fontSize = 1.03f;
            monitor.rectTransform.localPosition = new Vector3(-.50f,.02f,-.025f);
            monitor.rectTransform.sizeDelta = new Vector2(1.49f,1.46f);
            var desk = root.Find("Sloping operator deck");
            if (desk) Object.DestroyImmediate(desk.gameObject);
            var chassis = Pivot(root,"Dispatch key housing",new Vector3(.72f,1.39f,-.14f));
            var metal = GetMaterial("Machined titanium");
            var frame = GetMaterial("Graphite forged frames");
            var signal = Signal(feed ? "Feed key amber" : "Process key cyan",feed ? new Color(1f,.31f,.075f) : new Color(.13f,.84f,.86f));
            var ink = Signal("Dispatch key inlay",new Color(.85f,.9f,.83f));
            var body = GetMaterial(feed ? "Rescue ochre" : "Petrol marine enamel");
            var rubber = GetMaterial("Reinforced hose rubber");

            Box(chassis,"Instrument island",new Vector3(0,0,-.025f),new Vector3(.97f,1.32f,.12f),frame);
            Box(chassis,"Service enamel",new Vector3(0,.045f,-.095f),new Vector3(.84f,1.07f,.05f),body);
            for (int side=-1;side<=1;side+=2)
            {
                Box(chassis,"Guard lug",new Vector3(side*.33f,0,-.195f),new Vector3(.067f,.55f,.19f),metal);
                Box(chassis,"Guard bumper",new Vector3(side*.33f,0,-.30f),new Vector3(.085f,.57f,.045f),rubber);
                for (int vertical=-1;vertical<=1;vertical+=2)
                    Disk(chassis,"Mount bolt",new Vector3(side*.386f,vertical*.57f,-.092f),.051f,.026f,metal);
            }
            var hood=Pivot(chassis,"Recessed work lamp",new Vector3(0,.60f,-.20f));
            Box(hood,"Lamp cowl",Vector3.zero,new Vector3(.62f,.089f,.29f),metal);
            Box(hood,"Diffuser",new Vector3(0,-.046f,-.035f),new Vector3(.46f,.019f,.14f),ink);
            var light=Pivot(hood,"Task light",new Vector3(-.03f,-.06f,-.39f)).gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.range=2.4f;light.intensity=.82f;
            light.color=new Color(1f,.86f,.68f);light.shadows=LightShadows.None;
            Box(chassis,"Line pilot",new Vector3(0,.424f,-.128f),new Vector3(.25f,.028f,.022f),signal);
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

        static Material GetMaterial(string name)
            => AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");

        static Transform Pivot(Transform parent,string name,Vector3 position)
        {
            var part=new GameObject(name).transform;
            part.SetParent(parent,false);part.localPosition=position;
            return part;
        }

        static Transform Solid(Transform parent,string name,Vector3 position,Vector3 size,Material surface,PrimitiveType primitive)
        {
            var part=GameObject.CreatePrimitive(primitive);
            part.name=name;Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<MeshRenderer>().sharedMaterial=surface;
            return part.transform;
        }

        static void Box(Transform parent,string name,Vector3 position,Vector3 size,Material surface)
            => Solid(parent,name,position,size,surface,PrimitiveType.Cube);

        static Transform Disk(Transform parent,string name,Vector3 position,float diameter,float depth,Material surface)
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
