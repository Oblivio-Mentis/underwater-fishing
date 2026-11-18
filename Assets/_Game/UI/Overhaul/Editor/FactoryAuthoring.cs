using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Raid.UI.Editor
{
    /// <summary>Scene-specific industrial extension. Original RAID-010 source assets are never overwritten.</summary>
    public static class FactoryAuthoring
    {
        const string ScenePath="Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string AssetsPath="Assets/_Game/Factory/Generated";
        const string RootName="RAID011 Physical factory";
        static TMP_FontAsset font;
        static Material steel,dark,trim,amber,rubber,skin,flesh,tooth,glass,feedGlass,cyan,orange;
        static readonly float[] LaneX={-4.8f,-3.9f,-3f,-2.1f};

        [MenuItem("RAID/011/Install physical fish factory")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing the factory");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new InvalidOperationException("Factory installation is limited to InterfaceVerticalSlice.unity");
            if(scene.isDirty)throw new InvalidOperationException("Save the interface scene before installing the factory");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/UI/Overhaul/Fonts/PTSans-SDF.asset");
            if(font==null)throw new InvalidOperationException("Install the RAID011 UI/font before the factory");
            EnsureFolder(AssetsPath);EnsureFolder(AssetsPath+"/Meshes");EnsureFolder(AssetsPath+"/Materials");
            InitMaterials();
            var previous=FindInScene(RootName);if(previous!=null)Object.DestroyImmediate(previous);
            var root=new GameObject(RootName).transform;
            var runtime=root.gameObject.AddComponent<Raid.Factory.FactoryVisuals>();
            HideLegacyTank();BuildTank(root,runtime);BuildProcessLine(root,runtime);BuildFeedingLine(root,runtime);
            BuildPults(root);BuildRearExit(root);BuildFishTemplate(root,runtime);
            var bubble=Part("Bubble mesh reference",root,PrimitiveType.Sphere,Vector3.zero,Vector3.one,glass);
            runtime.bubbleMesh=bubble.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(bubble);
            runtime.bubbleMaterial=glass;
            AddLight("Tank water fill",root,new Vector3(0,2.8f,-3),new Color(.2f,.8f,.9f),28,6);
            AddLight("Workshop warm fill",root,new Vector3(-3.5f,3.2f,-5.5f),new Color(1,.66f,.31f),38,7);
            AddLight("Mouth inspection fill",root,new Vector3(0,3.1f,5.3f),new Color(.83f,.44f,.32f),30,7);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("[RAID011 Factory] Installed in InterfaceVerticalSlice only. New aft hatch and service ramp; original RAID010 assets and NetworkObject IDs retained.");
        }

        static void HideLegacyTank()
        {
            foreach(var name in new[]{"Central aquarium","Crew terminal","Reservoir stock / real inventory","Aft service door","ENGINEERING\nSEALED DECK","DIVE LOCK  /  01"})
            {var old=FindInScene(name);if(old!=null)old.SetActive(false);}
        }

        static void InitMaterials()
        {
            steel=Mat("Brushed pressure steel",new Color(.18f,.29f,.32f),.7f,.32f);
            dark=Mat("Machine dark steel",new Color(.035f,.075f,.09f),.58f,.23f);
            trim=Mat("Machined stainless trim",new Color(.47f,.57f,.57f),.85f,.43f);
            amber=Mat("Enamel safety ochre",new Color(.83f,.36f,.055f),.35f,.34f);
            rubber=Mat("Conveyor rubber",new Color(.07f,.1f,.1f),.05f,.18f);
            skin=Mat("Living mouth skin",new Color(.24f,.085f,.16f),.04f,.32f);
            flesh=Mat("Mouth inner folds",new Color(.43f,.16f,.21f),.03f,.43f);
            tooth=Mat("Mouth ivory teeth",new Color(.76f,.69f,.49f),.04f,.3f);
            cyan=Mat("Inspection cyan emission",new Color(.14f,.66f,.72f),.2f,.35f,1.8f);
            orange=Mat("Batch amber emission",new Color(.95f,.37f,.06f),.2f,.3f,2);
            glass=FlowMat("Process flow glass",new Color(.12f,.48f,.57f,.12f),new Color(.22f,.71f,.78f));
            feedGlass=FlowMat("Feeding flow glass",new Color(.44f,.27f,.16f,.13f),new Color(.83f,.49f,.15f));
            steel.SetTexture("_BaseMap",Texture("Steel grain",false));rubber.SetTexture("_BaseMap",Texture("Rubber tread",true));
            steel.SetTextureScale("_BaseMap",new Vector2(3,3));EditorUtility.SetDirty(steel);EditorUtility.SetDirty(rubber);
        }

        static void BuildTank(Transform root,Raid.Factory.FactoryVisuals runtime)
        {
            var tank=Group("Large industrial catch reservoir",root,new Vector3(0,0,-3));
            Box("Sealed tank plinth",tank,new Vector3(0,.12f,0),new Vector3(3.6f,.3f,3.6f),dark,true);
            Box("Main tank barrier",tank,new Vector3(0,1.4f,0),new Vector3(3.22f,2.5f,3.22f),glass,true);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("Tank structural upright",tank,new Vector3(x*1.66f,1.45f,z*1.66f),new Vector3(.12f,2.8f,.12f),trim);
            foreach(int side in new[]{-1,1})
            {
                Box("Reservoir upper frame",tank,new Vector3(0,2.8f,side*1.65f),new Vector3(3.44f,.15f,.15f),amber);
                Box("Reservoir upper frame",tank,new Vector3(side*1.65f,2.8f,0),new Vector3(.15f,.15f,3.44f),amber);
                Box("Reservoir floor inspection light",tank,new Vector3(0,.3f,side*1.63f),new Vector3(2.8f,.05f,.035f),cyan);
                Box("Tank capacity plate",tank,new Vector3(0,.55f,side*1.67f),new Vector3(2.7f,.44f,.06f),dark);
                Label("ОБЩИЙ УЛОВ",tank,new Vector3(0,.56f,side*1.713f),.19f,side>0?Quaternion.Euler(0,180,0):Quaternion.identity,new Vector2(2.5f,.35f));
            }
            runtime.tankCenter=Group("Tank fish center",root,new Vector3(0,1.4f,-3));
            // New barriers add real collision to the taller tank; the old original collider is retained underneath.
        }

        static void BuildProcessLine(Transform root,Raid.Factory.FactoryVisuals runtime)
        {
            runtime.processTube=SmoothPath(new[]{new Vector3(-1.6f,1.3f,-3),new Vector3(-2.02f,1.3f,-3),new Vector3(-2.02f,3.03f,-3),new Vector3(-3.45f,3.08f,-3.15f),new Vector3(-3.45f,3.0f,-4.35f),new Vector3(-3.45f,2.95f,-4.95f)});
            var process=Group("01 Process pipeline",root);
            runtime.processGlass=new[]{Tube("Process main transparent pipe",process,runtime.processTube,.34f,glass).GetComponent<Renderer>()};
            Cuffs(process,runtime.processTube,.355f);
            Label("01 / ПЕРЕРАБОТКА",process,new Vector3(-3.42f,3.48f,-3.9f),.16f,Quaternion.identity,new Vector2(3.2f,.3f));
            var workshop=Group("Aft processing workshop",root);
            Box("Workshop rear service panel",workshop,new Vector3(-3.45f,1.5f,-7.7f),new Vector3(4.2f,2.9f,.14f),steel);
            Label("ЦЕХ ПЕРЕРАБОТКИ",workshop,new Vector3(-3.45f,3.3f,-7.35f),.24f,Quaternion.Euler(0,180,0),new Vector2(4.3f,.45f));
            var rollList=new List<Transform>();var beltList=new List<Renderer>();
            for(int lane=0;lane<4;lane++)
            {
                var belt=Group("Conveyor belt "+(lane+1),workshop,new Vector3(LaneX[lane],0,0));
                Box("Belt underframe",belt,new Vector3(0,.55f,-5.65f),new Vector3(.75f,.36f,1.72f),steel,true);
                var tread=Quad("Belt moving rubber tread "+lane,belt,new Vector3(0,.84f,-5.65f),new Vector2(.65f,1.72f),rubber);beltList.Add(tread.GetComponent<Renderer>());
                foreach(int side in new[]{-1,1})
                {
                    Box("Conveyor guard rail",belt,new Vector3(side*.36f,.99f,-5.65f),new Vector3(.045f,.28f,1.72f),amber);
                    Box("Conveyor leg",belt,new Vector3(side*.25f,.22f,-6.12f),new Vector3(.1f,.44f,.1f),dark);
                }
                for(int i=0;i<7;i++)
                {
                    var roll=Part("Powered conveyor roller",belt,PrimitiveType.Cylinder,new Vector3(0,.75f,-4.95f-i*.23f),new Vector3(.16f,.35f,.16f),trim,Quaternion.Euler(0,0,90));rollList.Add(roll.transform);
                }
            }
            runtime.belts=beltList.ToArray();runtime.rollers=rollList.ToArray();
            var hopper=Group("Transparent distribution hopper",workshop,new Vector3(-3.45f,0,-4.93f));
            Box("Hopper back wall",hopper,new Vector3(0,1.9f,.22f),new Vector3(3.7f,1.5f,.045f),glass);
            foreach(int side in new[]{-1,1})
            {
                Box("Hopper upright",hopper,new Vector3(side*1.9f,1.7f,0),new Vector3(.09f,2,.09f),trim);
                Box("Hopper safety bracket",hopper,new Vector3(side*1.9f,2.55f,0),new Vector3(.3f,.12f,.5f),amber);
            }
            Box("Hopper top rail",hopper,new Vector3(0,2.6f,.2f),new Vector3(3.85f,.1f,.12f),trim);
            var machine=Group("Biomaterial processor",workshop,new Vector3(-3.45f,0,-6.9f));
            Box("Machine pressure casing",machine,new Vector3(0,1.1f,0),new Vector3(4.1f,2.2f,.78f),dark,true);
            Box("Processor front armor",machine,new Vector3(0,1.5f,.44f),new Vector3(3.9f,1.2f,.18f),steel);
            Box("Processor intakes shadow",machine,new Vector3(0,.91f,.54f),new Vector3(3.8f,.48f,.04f),dark);
            runtime.processorLamp=Box("Processor active batch lamp",machine,new Vector3(0,2.11f,.49f),new Vector3(3.2f,.07f,.04f),orange).GetComponent<Renderer>();
            for(int lane=0;lane<4;lane++)
            {
                float x=LaneX[lane]+3.45f;
                Box("Intake upper guide",machine,new Vector3(x,1.18f,.57f),new Vector3(.68f,.08f,.1f),trim);
                Box("Intake lower guide",machine,new Vector3(x,.63f,.57f),new Vector3(.68f,.08f,.1f),trim);
            }
            Label("ПЕРЕРАБОТЧИК",machine,new Vector3(0,1.73f,.55f),.2f,Quaternion.Euler(0,180,0),new Vector2(3.2f,.38f));
            string[] resources={"БИОМАССА","ХИТИН","МЕТАЛЛОЛОМ","ЭЛЕКТРОНИКА"};
            Material[] materials={Mat("Output biomass",new Color(.24f,.43f,.25f),.1f,.3f),tooth,trim,cyan};
            for(int lane=0;lane<4;lane++)
            {
                var bin=Group("Material output bin "+lane,workshop,new Vector3(LaneX[lane],0,-7.52f));
                Box("Material output cradle",bin,new Vector3(0,.25f,0),new Vector3(.7f,.5f,.48f),steel,true);
                Box("Labeled material outlet",bin,new Vector3(0,.53f,0),new Vector3(.56f,.09f,.4f),materials[lane]);
                Label(resources[lane],bin,new Vector3(0,.39f,.26f),.085f,Quaternion.Euler(0,180,0),new Vector2(.69f,.24f));
            }
        }

        static void BuildFeedingLine(Transform root,Raid.Factory.FactoryVisuals runtime)
        {
            runtime.feedTube=SmoothPath(new[]{new Vector3(1.6f,1.3f,-3),new Vector3(2.02f,1.3f,-3),new Vector3(2.02f,3.02f,-3),new Vector3(3.15f,3.08f,-2.6f),new Vector3(3.15f,3.08f,4.6f),new Vector3(1.9f,2.6f,6.5f),new Vector3(0,1.75f,7.6f)});
            var route=Group("02 Feeding pipeline",root);
            runtime.feedGlass=new[]{Tube("Feeding main transparent pipe",route,runtime.feedTube,.34f,feedGlass).GetComponent<Renderer>()};Cuffs(route,runtime.feedTube,.355f);
            Label("02 / КОРМЛЕНИЕ",route,new Vector3(3.13f,3.48f,-1.2f),.16f,Quaternion.identity,new Vector2(3.2f,.3f));
            var mouth=Group("Living bow feeding mouth",root,new Vector3(0,1.75f,7.8f));
            var rim=MeshPart("Living mouth rim",mouth,RingMesh(1.55f,.3f),skin);rim.transform.localScale=new Vector3(1.2f,1,1);
            Box("Mouth dark inner throat",mouth,new Vector3(0,0,.55f),new Vector3(3.4f,2.9f,.25f),Mat("Throat darkness",new Color(.018f,.012f,.021f),0,.3f),true);
            runtime.upperJaw=Part("Breathing upper jaw",mouth,PrimitiveType.Sphere,new Vector3(0,1.45f,.05f),new Vector3(4.4f,.9f,1.6f),skin).transform;
            runtime.lowerJaw=Part("Breathing lower jaw",mouth,PrimitiveType.Sphere,new Vector3(0,-1.35f,-.02f),new Vector3(4.1f,.8f,1.5f),flesh).transform;
            for(int i=0;i<18;i++)
            {
                float a=(i+.5f)*Mathf.PI*2/18;Vector3 p=new Vector3(Mathf.Cos(a)*1.65f,Mathf.Sin(a)*1.36f,-.2f);
                var fang=MeshPart("Mouth tooth "+i,mouth,ConeMesh(),tooth);fang.transform.localPosition=p;fang.transform.localScale=new Vector3(.2f,.2f,.57f);
                fang.transform.localRotation=Quaternion.LookRotation(new Vector3(-p.x,-p.y,.25f).normalized);
            }
            for(int side=-1;side<=1;side+=2)
            {
                var points=SmoothPath(new[]{new Vector3(side*1.8f,2.7f,7.85f),new Vector3(side*2.5f,3.15f,7.15f),new Vector3(side*2.7f,3.25f,5.5f),new Vector3(side*2.1f,3.4f,4.3f)});
                Tube("Organic attachment fold "+side,route,points,.23f,skin);
            }
            runtime.mouthLamp=Part("Living throat pulse",mouth,PrimitiveType.Sphere,new Vector3(0,.05f,.38f),new Vector3(.35f,.8f,.12f),orange).GetComponent<Renderer>();
            Label("ПАСТЬ / ПИЩЕВОЙ ТРАКТ",root,new Vector3(0,3.55f,7.03f),.18f,Quaternion.identity,new Vector2(4.2f,.4f));
        }

        static void BuildPults(Transform root)
        {
            Pult(root,"FishRoute_Process",new Vector3(-2.4f,0,-3),90,"ПЕРЕРАБОТАТЬ\nВЕСЬ УЛОВ",false,cyan);
            Pult(root,"FishRoute_Feed",new Vector3(2.4f,0,-3),-90,"ПОКОРМИТЬ\nВСЕЙ РЫБОЙ",false,orange);
            Pult(root,"UpgradeConsole",new Vector3(4.6f,0,-5.8f),90,"УЛУЧШЕНИЯ",true,cyan);
            Pult(root,"DepthConsole",new Vector3(4.6f,0,2.4f),90,"ГЛУБИНЫ",true,cyan);
        }

        static void Pult(Transform parent,string name,Vector3 position,float yaw,string title,bool computer,Material signal)
        {
            var pult=Group(name,parent,position);pult.localRotation=Quaternion.Euler(0,yaw,0);
            Box("Interaction plinth",pult,new Vector3(0,.45f,0),new Vector3(computer?1.1f:.65f,.9f,.55f),steel,true);
            if(computer)
            {
                Box("Instrument computer casing",pult,new Vector3(0,1.75f,0),new Vector3(2.65f,2.05f,.4f),steel);
                Box("World interface black backing",pult,new Vector3(0,1.75f,-.215f),new Vector3(2.42f,1.77f,.025f),dark);
                Group("ScreenAnchor",pult,new Vector3(0,1.75f,-.24f));
                Label(title,pult,new Vector3(0,2.91f,-.23f),.18f,Quaternion.identity,new Vector2(2.65f,.34f));
            }
            else
            {
                Box("Route selector casing",pult,new Vector3(0,1.19f,0),new Vector3(1.03f,.64f,.48f),dark);
                Box("Route selector lamp",pult,new Vector3(0,1.33f,-.25f),new Vector3(.76f,.08f,.025f),signal);
                Label(title,pult,new Vector3(0,1.05f,-.26f),.13f,Quaternion.identity,new Vector2(.96f,.43f));
                Group("ScreenAnchor",pult,new Vector3(0,1.19f,-.28f));
                Part("Physical route lever",pult,PrimitiveType.Cylinder,new Vector3(.38f,1.13f,-.38f),new Vector3(.055f,.14f,.055f),amber,Quaternion.Euler(35,0,0));
            }
        }

        static void BuildRearExit(Transform root)
        {
            // Explicit new-scene override: open the aft passage while retaining the original object and all network IDs.
            var original=FindInScene("Aft bulkhead");if(original!=null)foreach(var collider in original.GetComponents<Collider>())collider.enabled=false;
            var exit=Group("Rear wet airlock and service ramp",root);
            foreach(int side in new[]{-1,1})
            {
                Box("Split aft bulkhead",exit,new Vector3(side*3.7f,1.5f,-8),new Vector3(4.6f,3,.25f),steel,true);
                Box("Aft hatch frame",exit,new Vector3(side*1.46f,1.5f,-7.78f),new Vector3(.13f,3,.16f),amber);
            }
            Box("Aft opening lintel",exit,new Vector3(0,3.5f,-8),new Vector3(12,1,.25f),steel,true);
            Label("КОРМОВОЙ ШЛЮЗ / ВЫХОД",exit,new Vector3(0,3.31f,-7.69f),.18f,Quaternion.Euler(0,180,0),new Vector2(4.1f,.4f));
            Box("Aft exit deck",exit,new Vector3(0,.1f,-9.45f),new Vector3(2.8f,.24f,3.5f),steel,true,Quaternion.Euler(7.5f,0,0));
            Box("Service exit ramp",exit,new Vector3(4.2f,.33f,-11.05f),new Vector3(8.4f,.24f,2.8f),steel,true);
            foreach(int side in new[]{-1,1})
            {
                Box("Ramp handrail",exit,new Vector3(4.5f,1.48f,-11.05f+side*1.3f),new Vector3(7.8f,.08f,.08f),amber);
                for(int x=1;x<=8;x+=2)Box("Ramp upright",exit,new Vector3(x,.95f,-11.05f+side*1.3f),new Vector3(.07f,1,.07f),trim);
            }
            for(int z=-7;z<=-5;z++)Box("Clear central walk marking",exit,new Vector3(.85f,.018f,z),new Vector3(.07f,.012f,.65f),amber);
            for(int z=-7;z<=5;z+=2)Box("Starboard ring corridor marking",exit,new Vector3(3.35f,.018f,z),new Vector3(.06f,.012f,1.2f),amber);
            CutOwnHullPortal();
            foreach(var item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if(item.gameObject.scene.path==ScenePath && item.position.x>6 && item.position.z<-10 && item.position.z>-18 && (item.name=="Hull nameplate backing"||item.name=="NEREID  /  07"))item.position+=Vector3.back*7;
            foreach(int side in new[]{-1,1})Box("Starboard wet hatch jamb",exit,new Vector3(7.04f,1.9f,-11.02f+side*1.49f),new Vector3(.14f,3.2f,.12f),amber);
            Box("Starboard wet hatch lintel",exit,new Vector3(7.04f,3.47f,-11.02f),new Vector3(.14f,.12f,3.08f),amber);
            Label("ВЫХОД В ВОДУ",exit,new Vector3(6.93f,3.62f,-11.02f),.19f,Quaternion.Euler(0,90,0),new Vector2(2.9f,.35f));
        }

        static void CutOwnHullPortal()
        {
            var shell=FindInScene("Nereid_shell");if(shell==null)return;
            var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/VisualSlice/Generated/Meshes/Nereid_shell.asset");if(original==null)return;
            var vertices=new List<Vector3>();var triangles=new List<int>();var source=original.vertices;var indices=original.triangles;
            Vector3 min=new Vector3(5.6f,-.2f,-12.52f),max=new Vector3(7.5f,3.35f,-9.52f);
            for(int i=0;i<indices.Length;i+=3)
            {
                var remaining=new List<Vector3>{source[indices[i]],source[indices[i+1]],source[indices[i+2]]};
                for(int plane=0;plane<6 && remaining.Count>0;plane++)
                {
                    int axis=plane/2;bool low=plane%2==0;float edge=low?min[axis]:max[axis];
                    var outside=Clip(remaining,axis,edge,low?false:true);Append(outside,vertices,triangles);
                    remaining=Clip(remaining,axis,edge,low?true:false);
                }
            }
            var mesh=new Mesh{name="Nereid shell with starboard aft service portal"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            shell.GetComponent<MeshFilter>().sharedMesh=MeshAsset("Nereid service portal shell",mesh);shell.GetComponent<Renderer>().sharedMaterial=steel;
        }
        static List<Vector3> Clip(List<Vector3> input,int axis,float edge,bool greater)
        {
            var output=new List<Vector3>();if(input.Count==0)return output;
            Vector3 previous=input[input.Count-1];bool previousInside=greater?previous[axis]>=edge:previous[axis]<=edge;
            foreach(var current in input)
            {
                bool inside=greater?current[axis]>=edge:current[axis]<=edge;
                if(inside!=previousInside){float denominator=current[axis]-previous[axis];if(Mathf.Abs(denominator)>.00001f)output.Add(Vector3.LerpUnclamped(previous,current,(edge-previous[axis])/denominator));}
                if(inside)output.Add(current);previous=current;previousInside=inside;
            }
            return output;
        }
        static void Append(List<Vector3> polygon,List<Vector3> vertices,List<int> triangles)
        {
            if(polygon.Count<3)return;int first=vertices.Count;vertices.AddRange(polygon);
            for(int i=1;i<polygon.Count-1;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}
        }

        static void BuildFishTemplate(Transform root,Raid.Factory.FactoryVisuals runtime)
        {
            var mesh=new Mesh{name="Factory fish"};var v=new List<Vector3>();var t=new List<int>();
            float[] z={-.48f,-.3f,0,.26f,.5f},r={.035f,.17f,.28f,.21f,.025f};
            for(int ring=0;ring<z.Length;ring++)for(int side=0;side<10;side++){float a=side*Mathf.PI*.2f;v.Add(new Vector3(Mathf.Cos(a)*r[ring],Mathf.Sin(a)*r[ring]*.72f,z[ring]));}
            for(int ring=0;ring<z.Length-1;ring++)for(int side=0;side<10;side++){int a=ring*10+side,b=ring*10+(side+1)%10,c=a+10,d=b+10;t.AddRange(new[]{a,b,c,b,d,c});}
            mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();
            var fish=MeshPart("Batch fish template",root,mesh,Mat("Reservoir silver fish",new Color(.51f,.7f,.68f),.35f,.45f));
            foreach(int side in new[]{-1,1})
            {
                Box("Forked tail",fish.transform,new Vector3(0,side*.12f,-.52f),new Vector3(.035f,.29f,.24f),amber,false,Quaternion.Euler(side*32,0,0));
                Part("Fish eye",fish.transform,PrimitiveType.Sphere,new Vector3(side*.18f,.06f,.29f),new Vector3(.06f,.06f,.06f),dark);
            }
            runtime.fishTemplate=fish.transform;fish.SetActive(false);
        }

        static GameObject Tube(string name,Transform parent,Vector3[] points,float radius,Material material)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();const int sides=16;float distance=0;
            for(int ring=0;ring<points.Length;ring++)
            {
                if(ring>0)distance+=Vector3.Distance(points[ring],points[ring-1]);
                Vector3 tangent=(points[Mathf.Min(ring+1,points.Length-1)]-points[Mathf.Max(0,ring-1)]).normalized;
                Vector3 a=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized,b=Vector3.Cross(tangent,a);
                for(int i=0;i<sides;i++){float angle=i*Mathf.PI*2/sides;v.Add(points[ring]+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radius);uv.Add(new Vector2(i/(float)sides,distance));}
            }
            for(int ring=0;ring<points.Length-1;ring++)for(int side=0;side<sides;side++){int a=ring*sides+side,b=ring*sides+(side+1)%sides,c=a+sides,d=b+sides;tri.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();return MeshPart(name,parent,mesh,material);
        }
        static void Cuffs(Transform parent,Vector3[] points,float radius)
        {
            for(int i=0;i<points.Length;i+=4)
            {
                var cuff=MeshPart("Pipe support cuff",parent,RingMesh(radius,.055f),trim);cuff.transform.localPosition=points[i];
                cuff.transform.localRotation=Quaternion.LookRotation(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]);
            }
        }
        static Vector3[] SmoothPath(Vector3[] anchors)
        {
            var points=new List<Vector3>();
            for(int i=0;i<anchors.Length-1;i++)for(int step=0;step<5;step++)
            {
                float t=step/5f;Vector3 a=anchors[Mathf.Max(0,i-1)],b=anchors[i],c=anchors[i+1],d=anchors[Mathf.Min(anchors.Length-1,i+2)];
                points.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            points.Add(anchors[anchors.Length-1]);return points.ToArray();
        }
        static Mesh RingMesh(float radius,float tube)
        {
            const int segments=24,sides=6;var v=new List<Vector3>();var t=new List<int>();
            for(int a=0;a<segments;a++)for(int b=0;b<sides;b++){float u=a*Mathf.PI*2/segments,w=b*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(u)*(radius+tube*Mathf.Cos(w)),Mathf.Sin(u)*(radius+tube*Mathf.Cos(w)),Mathf.Sin(w)*tube));}
            for(int a=0;a<segments;a++)for(int b=0;b<sides;b++){int p=a*sides+b,q=((a+1)%segments)*sides+b,r=a*sides+(b+1)%sides,s=((a+1)%segments)*sides+(b+1)%sides;t.AddRange(new[]{p,q,r,r,q,s});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
        }
        static Mesh ConeMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<8;i++){float a=i*Mathf.PI*.25f;v.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0));}v.Add(Vector3.forward);v.Add(Vector3.zero);
            for(int i=0;i<8;i++)t.AddRange(new[]{i,(i+1)%8,8,(i+1)%8,i,9});var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
        }
        static GameObject Quad(string name,Transform parent,Vector3 p,Vector2 size,Material material)
        {
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-size.x/2,0,-size.y/2),new Vector3(-size.x/2,0,size.y/2),new Vector3(size.x/2,0,-size.y/2),new Vector3(size.x/2,0,size.y/2)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.right,Vector2.one};mesh.triangles=new[]{0,1,2,2,1,3};mesh.RecalculateNormals();var go=MeshPart(name,parent,mesh,material);go.transform.localPosition=p;return go;
        }
        static GameObject MeshPart(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=MeshAsset(name,mesh);go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static Mesh MeshAsset(string name,Mesh mesh)
        {
            string path=AssetsPath+"/Meshes/"+name.Replace(' ','_')+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }
        static Transform Group(string name,Transform parent,Vector3 position=default)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool solid=false,Quaternion rotation=default)
        {return Part(name,parent,PrimitiveType.Cube,position,scale,material,rotation,solid);}
        static GameObject Part(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion rotation=default,bool solid=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation==default?Quaternion.identity:rotation;
            if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static void Label(string text,Transform parent,Vector3 position,float size,Quaternion rotation,Vector2 bounds)
        {
            var go=Group("Label "+text,parent,position);go.localRotation=rotation;var label=go.gameObject.AddComponent<TextMeshPro>();label.font=font;label.text=text;label.alignment=TextAlignmentOptions.Center;
            label.fontSize=size*10;label.isOrthographic=false;
            label.color=new Color(.75f,.85f,.84f);label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Overflow;label.rectTransform.sizeDelta=bounds;
        }
        static void AddLight(string name,Transform parent,Vector3 position,Color color,float intensity,float range)
        {var light=Group(name,parent,position).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;}
        static Material Mat(string name,Color color,float metallic,float smoothness,float emission=0)
        {
            string path=AssetsPath+"/Materials/"+name.Replace(' ','_')+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smoothness);mat.enableInstancing=true;
            if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*emission);}EditorUtility.SetDirty(mat);return mat;
        }
        static Material FlowMat(string name,Color tint,Color flow)
        {
            string path=AssetsPath+"/Materials/"+name.Replace(' ','_')+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("RAID011/Factory Flow Glass")){name=name};AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",tint);mat.SetColor("_FlowColor",flow);EditorUtility.SetDirty(mat);return mat;
        }
        static Texture2D Texture(string name,bool tread)
        {
            string path=AssetsPath+"/"+name.Replace(' ','_')+".asset";var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing!=null)return existing;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};var pixels=new Color[64*64];var random=new System.Random(117);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float v=tread?(y%16<3?.35f:.8f):.76f+(float)random.NextDouble()*.2f;pixels[y*64+x]=new Color(v,v,v,1);}texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,path);return texture;
        }
        static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));}
        static GameObject FindInScene(string name)
        {foreach(var item in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(item.gameObject.scene.path==ScenePath && item.name==name)return item.gameObject;return null;}
    }
}
