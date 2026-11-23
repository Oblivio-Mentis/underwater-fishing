using System;
using System.Collections.Generic;
using System.IO;
using Raid.Factory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class NereidTransitAuthoring
    {
        const string Root="Assets/_Game/Nereid/ShipRebuild";
        const string Cargo=Root+"/Prefabs/Cargo";
        const string Plumbing=Root+"/Prefabs/Plumbing";
        const string Generated="Assets/_Game/Nereid/Generated";
        static Material steel,dark,trim,enamel,lamp,glass,amber;

        [MenuItem("NEREID/Install Cargo and Pipes")]
        static void InstallSelected() => Install(Object.FindAnyObjectByType<NereidVessel>());

        public static void Install(NereidVessel vessel)
        {
            if(!vessel || Application.isPlaying) throw new InvalidOperationException("Open the rebuilt vessel in Edit Mode.");
            if(vessel.authoredRail) return;
            var scene=vessel.gameObject.scene;
            FactoryVisuals factory=null; RaidWorld world=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                if(!factory) factory=root.GetComponentInChildren<FactoryVisuals>(true);
                if(!world) world=root.GetComponentInChildren<RaidWorld>(true);
            }
            if(!world || !factory || !world.container || !factory.upperJaw)
                throw new InvalidOperationException("A bound factory, parasite and network cargo container are required.");
            var layout=FindModule(vessel.transform,"ObservationHall");
            var tankModule=FindModule(vessel.transform,"Aquarium");
            if(!layout || !tankModule) throw new InvalidOperationException("Install ObservationHall and Aquarium modules before transit.");
            var workshop=factory.transform.Find("Aft processing workshop");
            var oldBasket=world.container.Find("RAID010 cargo basket");
            if(!workshop || !oldBasket || !oldBasket.Find("Basket base") || !oldBasket.Find("Basket rim"))
                throw new InvalidOperationException("Missing workshop or original cargo frame.");
            EnsureFolder(Cargo); EnsureFolder(Plumbing); EnsureFolder(Root+"/Materials");
            steel=Finish("Nereid_Steel");dark=Finish("Nereid_Dark");trim=Finish("Nereid_Trim");
            enamel=Finish("Nereid_Ochre");lamp=Finish("Nereid_Lamp");
            glass=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Equipment/Materials/Process pipe pressure glass.mat");
            amber=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Equipment/Materials/Feeding pressure glass.mat");

            var rig=New("Deck cargo transit",vessel.transform,Vector3.zero);
            var rail=rig.gameObject.AddComponent<NereidCargoRail>();
            var passage=New("Reserved central crew passage",layout,Vector3.zero);
            passage.position=vessel.transform.TransformPoint(new Vector3(0,0,-2));
            passage.gameObject.AddComponent<NereidTransitClearance>().walkway=new Bounds(new Vector3(0,.55f,0),new Vector3(1.55f,1.10f,7.4f));
            Transform hatch=MakeHatch(layout);
            hatch.position=vessel.transform.TransformPoint(new Vector3(0,4.8f,-3));
            rail.hatch=hatch;rail.cageAnchor=world.container;rail.timings=world.progression;
            rail.leftDoor=hatch.Find("Port pressure door");
            rail.rightDoor=hatch.Find("Starboard pressure door");
            rail.leftClosed=rail.leftDoor.localPosition; rail.rightClosed=rail.rightDoor.localPosition;
            rail.aquarium=New("Stock swimming volume",tankModule,Vector3.zero);
            rail.aquarium.position=vessel.transform.TransformPoint(new Vector3(0,-1.195f,-2f));
            rail.aquariumSwimRadii=new Vector3(1.99f,.68f,4.05f);
            rail.stops=new[]{
                Stop(rig,"01 · Seabed berth",new Vector3(11.5f,-8.2f,-14)),
                Stop(rig,"02 · Lift clear",new Vector3(11.5f,6.1f,-14)),
                Stop(rig,"03 · Travel across stern",new Vector3(0,6.1f,-14)),
                Stop(hatch,"04 · Above receiver",new Vector3(0,1.30f,0)),
                Stop(hatch,"05 · Roof entry",new Vector3(0,0,0)),
                Stop(hatch,"06 · Discharge and reverse",new Vector3(0,-1.38f,0))};
            rail.trackBeams=new Transform[rail.stops.Length-1]; rail.trackSupports=new Transform[rail.trackBeams.Length];
            for(int i=0;i<rail.trackBeams.Length;i++)
            {
                rail.trackBeams[i]=Box(rig,"Rail web "+(i+1),Vector3.zero,Vector3.one,steel);
                rail.trackSupports[i]=Box(rig,"Rail cap "+(i+1),Vector3.zero,Vector3.one,enamel);
            }
            var traveller=New("Recovery traveller",rig,Vector3.zero); rail.traveller=traveller;
            Box(traveller,"Axle housing",Vector3.zero,new Vector3(.67f,.24f,.68f),steel);
            Box(traveller,"Cable guide",Vector3.down*.14f,new Vector3(.3f,.36f,.35f),enamel);
            rail.wheels=new Transform[4];
            for(int i=0;i<4;i++)
            {
                var wheel=Part(PrimitiveType.Cylinder,traveller,"Guide roller",
                    new Vector3(i%2==0?-.22f:.22f,.13f,i<2?-.26f:.26f),new Vector3(.27f,.085f,.27f),dark);
                wheel.localRotation=Quaternion.Euler(0,0,90);rail.wheels[i]=wheel;
            }
            rail.signalLights=new[]{hatch.Find("Port signal").GetComponent<Renderer>(),hatch.Find("Starboard signal").GetComponent<Renderer>()};
            for(int i=0;i<2;i++)
            {
                var pier=New("Gantry support frame",rig,new Vector3(i==0?10.7f:-1.3f,0,-15.45f));
                float foot=i==0?-.52f:4.5f;
                var basePoint=new Vector3(0,foot,0);
                var head=new Vector3(0,rail.craneAboveHatch+4.8f,0);
                Strut(pier,"Compression column",basePoint,head,.19f,steel);
                Strut(pier,"Cantilever",head,head+new Vector3(1.32f,0,1.46f),.16f,dark);
                Strut(pier,"Knee brace",head-Vector3.up*1.1f,head+new Vector3(1.32f,0,1.46f),.11f,enamel);
            }
            var receiver=hatch.Find("Receiver throat");
            var inSource=Socket(PipePort.Receiver,receiver,receiver.position,Vector3.one*1.2f);
            var inDest=AtHull(PipePort.ReservoirInlet,tankModule,vessel,new Vector3(2.26f,-.82f,-3),Vector3.one*.95f);
            var processStart=AtHull(PipePort.ReservoirProcessOutlet,tankModule,vessel,new Vector3(-2.28f,-1.03f,-5.86f),Vector3.one*.95f);
            var feedStart=AtHull(PipePort.ReservoirFeedOutlet,tankModule,vessel,new Vector3(2.29f,-.95f,1.91f),Vector3.one*.95f);
            var processEnd=Socket(PipePort.Processor,workshop,workshop.TransformPoint(new Vector3(-3.45f,4.70f,-4.95f)),Vector3.one*2.3f);
            var jaw=factory.upperJaw.parent;
            var feedEnd=Socket(PipePort.Parasite,jaw,jaw.TransformPoint(new Vector3(.48f,-.05f,-.43f)),Vector3.one*1.35f);
            rail.inlet=Line(rig,PipeLine.Inlet,inSource,inDest,new[]{
                new Vector3(.10f,2.55f,-3),new Vector3(1.05f,2.23f,-3),new Vector3(2.96f,2.23f,-3),
                new Vector3(3.18f,1.66f,-3),new Vector3(3.18f,-.45f,-3)});
            factory.processRoute=Line(rig,PipeLine.Process,processStart,processEnd,new[]{
                new Vector3(-2.93f,-1.03f,-5.86f),new Vector3(-3.31f,-.60f,-6.09f),
                new Vector3(-3.31f,2.78f,-6.43f),new Vector3(-3.31f,2.88f,-9.64f),new Vector3(-2.55f,2.6f,-10.55f)});
            factory.feedRoute=Line(rig,PipeLine.Feed,feedStart,feedEnd,new[]{
                new Vector3(3.12f,-.95f,1.91f),new Vector3(3.39f,-.50f,2.17f),
                new Vector3(3.39f,2.79f,2.69f),new Vector3(3.39f,2.89f,5.69f),new Vector3(2.38f,2.56f,6.34f),new Vector3(1.14f,1.97f,7.16f)});
            factory.processorDrop=New("Conveyor loading line",workshop,new Vector3(-3.45f,factory.beltY,-4.95f));
            factory.processorIntake=New("Conveyor intake line",workshop,new Vector3(-3.45f,factory.beltY,-6.42f));
            factory.processorGrinder=New("Grinder intake",workshop,new Vector3(-3.45f,.65f,-6.94f));
            factory.feedThroat=New("End of swallow",jaw,new Vector3(0,0,.87f));
            factory.tankCenter=rail.aquarium;
            InstallBasket(world,oldBasket);
            vessel.authoredRail=rail;
            foreach(string name in new[]{"Overhead cargo rail","Stern cargo transverse rail","Cargo rail support","Sealed reservoir overhead hopper","Reservoir delivery throat"})
                Disable(vessel.transform.Find(name));
            Disable(factory.transform.Find("01 Process pipeline"));Disable(factory.transform.Find("02 Feeding pipeline"));
            foreach(var component in new Component[]{vessel,factory,rail})
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void InstallBasket(RaidWorld world,Transform legacy)
        {
            string path=Cargo+"/RecoveryCage.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab)
            {
                var template=new GameObject("Recovery Cage");
                try
                {
                    var signal=Instrument("Cargo signal",Color.white);
                    var fascia=Instrument("Cargo instrument fascia",new Color(.016f,.032f,.048f));
                    CargoBasket.Bake(template.transform,legacy.Find("Basket base"),legacy.Find("Basket rim").GetComponent<Renderer>().sharedMaterial,enamel,signal,fascia);
                    prefab=PrefabUtility.SaveAsPrefabAsset(template,path);
                }
                finally {Object.DestroyImmediate(template);}
            }
            var cage=(GameObject)PrefabUtility.InstantiatePrefab(prefab,world.container);
            cage.transform.SetLocalPositionAndRotation(legacy.localPosition,legacy.localRotation);
            cage.transform.localScale=legacy.localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(cage.transform);
            Disable(legacy);
            if(world.container.TryGetComponent<Collider>(out var collider)) collider.enabled=false;
        }

        static Transform MakeHatch(Transform parent)
        {
            string path=Cargo+"/RoofReceiver.prefab";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!asset)
            {
                var temp=new GameObject("Roof recovery lock");
                var root=temp.transform;
                try
                {
                    foreach(int side in new[]{-1,1})
                    {
                        Box(root,"Pressure guide",new Vector3(side*2.52f,.05f,0),new Vector3(.24f,.32f,5.28f),steel);
                        Box(root,"Longitudinal service coaming",new Vector3(0,.08f,side*2.51f),new Vector3(5.31f,.26f,.21f),dark);
                        var door=New(side<0?"Port pressure door":"Starboard pressure door",root,new Vector3(side*1.2f,0,0));
                        Box(door,"Laminated shutter",Vector3.zero,new Vector3(2.39f,.19f,4.8f),steel);
                        Box(door,"Pressure seal",new Vector3(-side*1.13f,.12f,0),new Vector3(.12f,.10f,4.60f),enamel);
                        for(int k=-2;k<=2;k++)
                            Box(door,"Armoured stiffener",new Vector3(0,.12f,k*.82f),new Vector3(2.1f,.13f,.11f),dark);
                        Box(root,side<0?"Port signal":"Starboard signal",new Vector3(side*2.55f,.23f,-2.04f),new Vector3(.15f,.08f,.47f),lamp);
                    }
                    var throat=New("Receiver throat",root,new Vector3(0,-2.12f,0));
                    Mesh funnel=ReceiverMesh();
                    throat.gameObject.AddComponent<MeshFilter>().sharedMesh=funnel;
                    throat.gameObject.AddComponent<MeshRenderer>().sharedMaterial=steel;
                    for(int i=0;i<4;i++)
                    {
                        float x=i%2==0?-1:1,z=i<2?-1:1;
                        Strut(root,"Hopper hanger",new Vector3(2.51f*x,.04f,1.6f*z),new Vector3(x*.98f,-1.67f,z*.98f),.085f,dark);
                    }
                    asset=PrefabUtility.SaveAsPrefabAsset(temp,path);
                }
                finally {Object.DestroyImmediate(temp);}
            }
            return ((GameObject)PrefabUtility.InstantiatePrefab(asset,parent)).transform;
        }

        static Mesh ReceiverMesh()
        {
            string path=Cargo+"/Steel funnel.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh) return mesh;
            var vertices=new List<Vector3>(); var faces=new List<int>();
            int n=32;
            foreach(var p in new[]{new Vector2(.43f,0),new Vector2(.48f,.09f),new Vector2(1.5f,.50f),
                new Vector2(1.55f,.53f),new Vector2(1.55f,.59f),new Vector2(1.46f,.59f),new Vector2(.425f,.09f),new Vector2(.395f,0)})
                for(int i=0;i<n;i++){float a=2*Mathf.PI*i/n;vertices.Add(new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x));}
            for(int ring=0;ring<vertices.Count/n;ring++)
                for(int i=0;i<n;i++)
                {
                    int a=ring*n+i, b=ring*n+(i+1)%n,c=((ring+1)*n+i)%vertices.Count,d=((ring+1)*n+(i+1)%n)%vertices.Count;
                    faces.AddRange(new[]{a,c,b,b,c,d});
                }
            mesh=new Mesh{name="Dished wet receiver"};mesh.SetVertices(vertices);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        static NereidPipeRoute Line(Transform root,PipeLine kind,PipeSocket start,PipeSocket end,Vector3[] waypoints)
        {
            string path=Plumbing+"/PressureLine.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab)
            {
                var template=new GameObject("Pressure line",typeof(NereidPipeRoute));
                try
                {
                    var route=template.GetComponent<NereidPipeRoute>();
                    route.sightGlass=glass;route.metal=trim;route.enamel=enamel;
                    route.knots.Add(new PipeKnot());route.knots.Add(new PipeKnot {position=Vector3.forward});
                    prefab=PrefabUtility.SaveAsPrefabAsset(template,path);
                }
                finally {Object.DestroyImmediate(template);}
            }
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);
            var pipe=instance.GetComponent<NereidPipeRoute>(); pipe.name=kind+" pressure line";
            pipe.line=kind;pipe.radius=kind==PipeLine.Inlet ? .43f : .37f;
            pipe.source=start; pipe.destination=end;pipe.sightGlass=kind==PipeLine.Feed ? amber : glass;
            pipe.knots.Clear();
            pipe.knots.Add(new PipeKnot {position=root.InverseTransformPoint(start.transform.position)});
            for(int i=0;i<waypoints.Length;i++)
                pipe.knots.Add(new PipeKnot {position=waypoints[i],section=i==1 ? PipeSection.Valve : i==waypoints.Length-2 ? PipeSection.Hanger : i==0 ? PipeSection.ElbowFlange : PipeSection.Glass});
            pipe.knots.Add(new PipeKnot {position=root.InverseTransformPoint(end.transform.position)});
            pipe.Rebuild();
            PrefabUtility.RecordPrefabInstancePropertyModifications(pipe);
            return pipe;
        }

        static PipeSocket AtHull(PipePort kind,Transform fitting,NereidVessel hull,Vector3 point,Vector3 size) =>
            Socket(kind,fitting,hull.transform.TransformPoint(point),size);

        static PipeSocket Socket(PipePort kind,Transform fitting,Vector3 world,Vector3 volume)
        {
            var t=New(kind+" coupling",fitting,fitting.InverseTransformPoint(world));
            var s=t.gameObject.AddComponent<PipeSocket>();s.port=kind;s.fitting=fitting;s.connectionVolume=new Bounds(t.localPosition,volume); return s;
        }

        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        static Material Finish(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Generated+"/"+name+".mat");

        static Material Instrument(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";
            var asset=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(asset)return asset;
            asset=new Material(Resources.Load<Shader>("CargoInstrument")){name=name};
            asset.SetColor("_BaseColor",color); AssetDatabase.CreateAsset(asset,path);return asset;
        }

        static RailStop Stop(Transform parent,string name,Vector3 local) => new RailStop{label=name,marker=New(name,parent,local)};
        static Transform FindModule(Transform root,string name)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) if(child.name==name)return child;
            return null;
        }
        static void Disable(Transform node){if(node)node.gameObject.SetActive(false);}
        static Transform New(string name,Transform parent,Vector3 at)
        {
            var child=new GameObject(name).transform;child.SetParent(parent,false);child.localPosition=at;return child;
        }
        static Transform Part(PrimitiveType type,Transform parent,string name,Vector3 at,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        static Transform Box(Transform parent,string name,Vector3 at,Vector3 size,Material mat)=>Part(PrimitiveType.Cube,parent,name,at,size,mat);
        static void Strut(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat)
        {
            var strut=Box(parent,name,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),mat);
            strut.localRotation=Quaternion.FromToRotation(Vector3.forward,b-a);
        }
    }
}

