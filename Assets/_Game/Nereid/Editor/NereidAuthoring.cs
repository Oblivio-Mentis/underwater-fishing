using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class NereidAuthoring
    {
        const string Scene="Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Folder="Assets/_Game/Nereid/Generated";
        const float VesselScale=2f;
        static Transform root;
        static Dictionary<string,Material> mats;

        [MenuItem("RAID/011/Rebuild NEREID vessel")]
        public static void Install()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Scene)
                throw new InvalidOperationException("NEREID authoring requires the stopped InterfaceVerticalSlice scene");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene before rebuild");
            if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Nereid/Art/Nereid_Interior.fbx")==null)
                throw new InvalidOperationException("Import Nereid_Interior.fbx and Nereid_Exterior.fbx first");
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            SetupMaterials();
            var previous=GameObject.Find("NEREID Rebuilt Vessel");if(previous!=null)Object.DestroyImmediate(previous);
            root=new GameObject("NEREID Rebuilt Vessel").transform;
            root.localScale=Vector3.one*VesselScale;
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();factory.transform.localScale=Vector3.one*VesselScale;
            foreach(var station in factory.GetComponentsInChildren<ShipConsole>(true))station.transform.localScale=Vector3.one/VesselScale;
            var vessel=root.gameObject.AddComponent<NereidVessel>();
            root.gameObject.AddComponent<NereidVfx>();
            root.gameObject.AddComponent<NereidAim>();
            vessel.sealedRooms=new[]{new Bounds(new Vector3(0,2.05f,-1),new Vector3(11.8f,4.08f,14.2f)),new Bounds(new Vector3(0,2.3f,6.5f),new Vector3(11.8f,4.58f,3)),new Bounds(new Vector3(0,1.65f,-12),new Vector3(11.8f,3.28f,8.05f))};
            vessel.cargoRail=new[]{new Vector3(8,.4f,-14),new Vector3(8,6.1f,-14),new Vector3(0,6.1f,-14),new Vector3(0,6.1f,-3)};
            HideOldArchitecture();
            Import("Nereid_Exterior");Import("Nereid_Interior");
            ImportCreature();
            PhysicalRooms();Lighting();ServiceExit(vessel);Labels();RerouteFeed();TankBarrier();OceanSilhouettes();TraversableDecks();
            NereidEquipmentAuthoring.Upgrade();BindIdleScreens();
            RemoveWorkshopGlazing();PlaceFishingGrounds();
            var menu=Object.FindAnyObjectByType<RaidSession>().menuCamera;
            menu.transform.position=new Vector3(-53,15,51);menu.transform.LookAt(new Vector3(0,3,-8));menu.fieldOfView=62;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log("[NEREID REBUILD] Imported pressure hull and structural interior; aft room and real starboard stern exit, isolated scene tuning.");
        }

        static void HideOldArchitecture()
        {
            var old=GameObject.Find("RAID010 Visuals");
            if(old!=null)foreach(Transform child in old.transform)
                if(child.name=="RV NEREID / pressure hull"||child.name=="Main research compartment"||child.name=="Winch boom"||child.name=="Bow parasite / attached oral disc")child.gameObject.SetActive(false);
            foreach(string name in new[]{"Deck","Roof","Port wall","Starboard wall","Aft bulkhead","Front left","Front right","Airlock lintel","Outside platform"})
            {var go=GameObject.Find(name);if(go==null)continue;foreach(var c in go.GetComponents<Collider>())c.enabled=false;foreach(var r in go.GetComponents<Renderer>())r.enabled=false;}
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();
            var rear=factory.transform.Find("Rear wet airlock and service ramp");if(rear!=null)rear.gameObject.SetActive(false);
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if(light.name=="Interior lamp"||light.name=="Warm overhead"||light.name=="Tank water fill"||light.name=="Workshop warm fill"||light.name=="Reservoir bounce"||light.name=="Mouth inspection fill"||light.name=="Ocean reflected fill"||light.name=="Bow attachment inspection fill")light.enabled=false;
        }

        static void Import(string name)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Nereid/Art/"+name+".fbx"),root);
            go.name=name;go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string key=materials[i]==null?"Nereid_Steel":materials[i].name.Replace(" (Instance)","");
                    if(!mats.TryGetValue(key,out var material))material=mats["Nereid_Steel"];
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                if(renderer.name.EndsWith("Nereid_Hull",StringComparison.Ordinal)||renderer.name.EndsWith("Nereid_Steel",StringComparison.Ordinal))
                {var collider=renderer.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;}
            }
        }

        static void PhysicalRooms()
        {
            BoxCollider("Walkable main and aft deck",new Vector3(0,-.15f,-4),new Vector3(11.9f,.3f,24));
            BoxCollider("Main overhead pressure barrier",new Vector3(0,4.7f,0),new Vector3(12,.2f,16));
            BoxCollider("Aft ceiling pressure barrier",new Vector3(0,3.4f,-12),new Vector3(12,.2f,8));
            BoxCollider("Port sealed window wall",new Vector3(-5.95f,2.2f,-4),new Vector3(.15f,4.6f,24));
            BoxCollider("Starboard main sealed windows",new Vector3(5.95f,2.2f,0),new Vector3(.15f,4.6f,16));
            BoxCollider("Starboard aft wall before hatch",new Vector3(5.95f,1.9f,-10.2f),new Vector3(.15f,3.8f,4.4f));
            BoxCollider("Starboard aft wall behind hatch",new Vector3(5.95f,1.9f,-15.8f),new Vector3(.15f,3.8f,.4f));
            BoxCollider("Starboard hatch lintel",new Vector3(5.95f,3.5f,-14),new Vector3(.15f,.8f,3.2f));
            BoxCollider("Stern sealed bulkhead",new Vector3(0,1.9f,-16.1f),new Vector3(12,3.8f,.2f));
            foreach(int side in new[]{-1,1})
            {
                BoxCollider("Bow side pressure wall",new Vector3(side*4.25f,2.2f,8.1f),new Vector3(3.5f,4.4f,.2f));
                BoxCollider("Aft partition side",new Vector3(side*3.7f,1.9f,-8.05f),new Vector3(4.6f,3.8f,.18f));
            }
            BoxCollider("Bow upper pressure wall",new Vector3(0,4.05f,8.1f),new Vector3(5,1.1f,.2f));
            BoxCollider("Aft partition top",new Vector3(0,3.45f,-8.05f),new Vector3(2.8f,.9f,.18f));
        }

        static void ImportCreature()
        {
            const string path="Assets/_Game/Nereid/Art/Creature/Nereid_BowCreature.fbx";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new InvalidOperationException("Import complete external creature first");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,root);go.name="Complete bow creature";go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one/VesselScale;
            var eyes=new List<Renderer>();
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&mats.TryGetValue(materials[i].name,out var mat)){materials[i]=mat;if(mat.name=="Nereid_CreatureEye")eyes.Add(renderer);}
                renderer.sharedMaterials=materials;
                if(renderer.name.Contains("CreatureSkin")||renderer.name.Contains("CreatureDark"))renderer.gameObject.AddComponent<MeshCollider>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            }
            var motion=go.AddComponent<NereidCreatureMotion>();motion.eyes=eyes.Distinct().ToArray();
        }

        static void ServiceExit(NereidVessel vessel)
        {
            var world=Object.FindAnyObjectByType<RaidWorld>();
            string tuningPath=Folder+"/NereidWorld.asset";var tuning=AssetDatabase.LoadAssetAtPath<WorldTuning>(tuningPath);
            if(tuning==null){tuning=Object.Instantiate(world.tuning);tuning.name="NereidWorld";AssetDatabase.CreateAsset(tuning,tuningPath);}
            tuning.cargoPosition=root.TransformPoint(new Vector3(8,.4f,-14));tuning.terminalPosition=root.TransformPoint(new Vector3(0,1,-3));world.tuning=tuning;EditorUtility.SetDirty(tuning);EditorUtility.SetDirty(world);
            world.container.position=tuning.cargoPosition;
            var label=GameObject.Find("CARGO / WINCH");if(label!=null)label.SetActive(false);
            Solid("Stern airlock landing",new Vector3(7.25f,-.08f,-14),new Vector3(2.7f,.2f,3.1f),"Nereid_Grate",true);
            Solid("Stern platform",new Vector3(9.5f,-.12f,-14),new Vector3(2.5f,.22f,4.6f),"Nereid_Dark",true);
            foreach(int side in new[]{-1,1})
            {
                for(int x=6;x<=10;x+=2)Solid("Dock safety stanchion",new Vector3(x,.65f,-14+side*2.1f),new Vector3(.065f,1.3f,.065f),"Nereid_Trim");
                Solid("Dock handrail",new Vector3(8,1.25f,-14+side*2.1f),new Vector3(4,.08f,.08f),"Nereid_Ochre");
            }
            Solid("Overhead cargo rail",new Vector3(0,6.9f,-8.5f),new Vector3(.28f,.35f,12),"Nereid_Dark");
            Solid("Stern cargo transverse rail",new Vector3(4,6.9f,-14),new Vector3(8.3f,.35f,.28f),"Nereid_Dark");
            Solid("Cargo rail support",new Vector3(8,3.4f,-16),new Vector3(.23f,6.9f,.3f),"Nereid_Steel");
            Solid("Sealed reservoir overhead hopper",new Vector3(0,4.95f,-3),new Vector3(2.6f,.7f,2.6f),"Nereid_Steel");
            Solid("Reservoir delivery throat",new Vector3(0,3.65f,-3),new Vector3(.8f,1.2f,.8f),"Nereid_Dark");
            var presentation=Object.FindAnyObjectByType<Raid.VisualSlice.SlicePresentation>();vessel.hoistCable=presentation==null?null:presentation.hoistCable;
            var threats=Object.FindAnyObjectByType<ThreatDirector>();
            if(threats!=null)
            {
                threats.factoryWaveRoute=new[]{new Vector3(20,1.5f,-28),new Vector3(10,1.5f,-28),new Vector3(0,1.5f,-28),new Vector3(0,1.5f,-15.6f),new Vector3(0,1.5f,-9.6f)};
                string threatPath=Folder+"/NereidThreats.asset";var threatTuning=AssetDatabase.LoadAssetAtPath<ThreatTuning>(threatPath);
                if(threatTuning==null){threatTuning=Object.Instantiate(threats.tuning);AssetDatabase.CreateAsset(threatTuning,threatPath);}
                threatTuning.predatorSpawn=new Vector3(28,-5,-29);threatTuning.waveSpawn=new Vector3(26,1.5f,-36);threatTuning.airlockWaypoint=new Vector3(12,1.5f,-28);threatTuning.reservoirPoint=new Vector3(0,2.8f,-6);
                threats.tuning=threatTuning;EditorUtility.SetDirty(threatTuning);EditorUtility.SetDirty(threats);
            }
        }

        static void Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.19f,.23f,.25f);
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.18f,.23f,.26f));RenderSettings.ambientProbe=probe;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.075f,.23f,.29f);RenderSettings.fogDensity=.022f/VesselScale;
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();sun.intensity=.65f;sun.color=new Color(.3f,.61f,.76f);sun.shadows=LightShadows.Soft;
            foreach(float z in new[]{-6.6f,-1,4.6f})foreach(int side in new[]{-1,1})
            {
                var lamp=new GameObject("Caged luminaire downlight");lamp.transform.SetParent(root,false);lamp.transform.localPosition=new Vector3(side*2.7f,4.05f,z);lamp.transform.localRotation=Quaternion.Euler(90,0,0);
                var spot=lamp.AddComponent<Light>();spot.type=LightType.Spot;spot.color=new Color(1,.76f,.49f);spot.intensity=70;spot.range=7.5f;spot.spotAngle=112;spot.innerSpotAngle=78;spot.shadows=LightShadows.Soft;spot.shadowBias=.025f;spot.shadowNormalBias=.15f;
                Light("Cold pressure window bounce",new Vector3(side*5.1f,2.6f,z),new Color(.16f,.48f,.62f),5,4.8f,false);
            }
            foreach(float z in new[]{-5.5f,1.5f})foreach(int side in new[]{-1,1})Light("Warm reflected working-plane fill",new Vector3(side*2.7f,2.2f,z),new Color(1,.76f,.52f),12,5,false);
            Light("Aft loading room",new Vector3(0,3.4f,-12),new Color(1,.65f,.34f),16,7,true);
            Light("Reservoir reflected water",new Vector3(0,2.9f,-3),new Color(.18f,.7f,.8f),7,4.3f,false);
            Light("Workshop task light",new Vector3(-3.5f,2.8f,-5.5f),new Color(1,.74f,.46f),7,4.5f,true);
            Light("Living mouth inspection",new Vector3(-1.7f,2.9f,5.8f),new Color(.4f,.65f,.72f),6,5,false);
            Light("Stern dock light",new Vector3(7.6f,3.3f,-14),new Color(1,.7f,.4f),7,6,true);
            Light("Bow reflected ocean light",new Vector3(-6,5,14),new Color(.28f,.54f,.7f),20,14,false);
            var go=new GameObject("Interior reflection / refresh once");go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(0,2,-3);
            var reflection=go.AddComponent<ReflectionProbe>();reflection.mode=ReflectionProbeMode.Realtime;reflection.refreshMode=ReflectionProbeRefreshMode.OnAwake;
            reflection.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;reflection.resolution=128;reflection.size=new Vector3(12,5,24);reflection.center=new Vector3(0,0,-1);
            reflection.boxProjection=true;reflection.intensity=.8f;reflection.blendDistance=1;reflection.clearFlags=ReflectionProbeClearFlags.SolidColor;reflection.backgroundColor=RenderSettings.fogColor;reflection.shadowDistance=25;reflection.nearClipPlane=.2f;reflection.farClipPlane=60;
            foreach(var light in root.GetComponentsInChildren<Light>()){light.range*=VesselScale;light.intensity*=VesselScale*VesselScale;}
        }

        static void Labels()
        {
            Label("ЦЕХ / КОРМОВОЙ ШЛЮЗ",new Vector3(0,3.2f,-7.85f),Quaternion.Euler(0,180,0),.25f);
            Label("ВЫХОД / ПОДЪЁМНИК",new Vector3(5.72f,3.35f,-14),Quaternion.Euler(0,90,0),.2f);
            Label("NEREID  /  ГЛАВНЫЙ ОТСЕК",new Vector3(0,3.85f,.5f),Quaternion.Euler(0,180,0),.19f);
        }

        static void TraversableDecks()
        {
            // A useful observation route around the real reservoir, reached from either end by walkable stairs.
            const float x=-4.7f,level=1.5f;
            Solid("Reservoir observation gallery",new Vector3(x,level-.05f,-.75f),new Vector3(1.25f,.1f,7.5f),"Nereid_Grate",true);
            foreach(int side in new[]{-1,1})
            {
                float edge=x+side*.61f;
                Solid("Gallery handrail",new Vector3(edge,level+.54f,-.75f),new Vector3(.035f,.035f,7.5f),"Nereid_Ochre",true);
                Solid("Gallery middle rail",new Vector3(edge,level+.29f,-.75f),new Vector3(.022f,.022f,7.5f),"Nereid_Dark",true);
                for(float z=-4.4f;z<3.05f;z+=.9f)Solid("Gallery stanchion",new Vector3(edge,level+.27f,z),new Vector3(.035f,.54f,.035f),"Nereid_Trim",true);
            }
            for(float z=-3.9f;z<3;z+=2.2f)foreach(int side in new[]{-1,1})
                Solid("Gallery structural foot",new Vector3(x+side*.5f,.71f,z),new Vector3(.085f,1.42f,.12f),"Nereid_Dark",true);
            const int steps=20;const float run=.15f;
            for(int i=0;i<steps;i++)
            {
                float h=level*(i+1)/steps;
                Solid("Aft gallery stair "+i,new Vector3(x,h*.5f,-7.5f+(i+.5f)*run),new Vector3(1.25f,h,run+.008f),"Nereid_Steel",true);
                Solid("Forward gallery stair "+i,new Vector3(x,h*.5f,6-(i+.5f)*run),new Vector3(1.25f,h,run+.008f),"Nereid_Steel",true);
                foreach(float z in new[]{-7.5f+(i+1)*run,6-(i+1)*run})
                    Solid("Stair nosing",new Vector3(x,h+.003f,z),new Vector3(1.22f,.012f,.022f),"Nereid_Ochre");
            }
            foreach(int side in new[]{-1,1})for(int i=0;i<=5;i++)
            {
                float t=i/5f,edge=x+side*.61f;
                foreach(float z in new[]{Mathf.Lerp(-7.5f,-4.5f,t),Mathf.Lerp(6,3,t)})
                    Solid("Stair safety post",new Vector3(edge,level*t+.27f,z),new Vector3(.035f,.54f,.035f),"Nereid_Trim",true);
            }
        }

        public static void BindIdleScreens()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before binding physical screens");
            var vessel=Object.FindAnyObjectByType<NereidVessel>();
            var owner=vessel.GetComponent<NereidShipScreens>();if(owner==null)owner=vessel.gameObject.AddComponent<NereidShipScreens>();
            var stations=new List<ShipConsole>();var labels=new List<TMPro.TMP_Text>();
            var upgrade=GameObject.Find("UpgradeConsole");if(upgrade!=null){upgrade.transform.localPosition=new Vector3(3.7f,0,-7.45f);upgrade.transform.rotation=Quaternion.Euler(0,180,0);}
            var reservoir=GameObject.Find("ReservoirConsole");if(reservoir!=null)reservoir.transform.localPosition=new Vector3(0,0,-.75f);
            foreach(var station in Object.FindObjectsByType<ShipConsole>())
            {
                if(station.kind==ShipConsoleKind.Feed||station.kind==ShipConsoleKind.Process||station.screenAnchor==null)continue;
                var previous=station.screenAnchor.Find("Live idle instrument");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
                var go=new GameObject("Live idle instrument");go.transform.SetParent(station.screenAnchor,false);go.transform.localPosition=new Vector3(0,0,-.012f);
                var text=go.AddComponent<TMPro.TextMeshPro>();text.font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Game/UI/Overhaul/Fonts/PTSans-SDF.asset");
                text.fontSize=1.2f;text.color=new Color(.3f,.83f,.83f);text.alignment=TMPro.TextAlignmentOptions.Center;text.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
                float scale=station.screenScale/.0018f;text.rectTransform.sizeDelta=new Vector2(2.25f*scale,1.55f*scale);text.fontSize*=scale;
                text.text="NEREID";text.overflowMode=TMPro.TextOverflowModes.Ellipsis;stations.Add(station);labels.Add(text);
            }
            owner.stations=stations.ToArray();owner.screens=labels.ToArray();EditorUtility.SetDirty(owner);
        }

        static void RemoveWorkshopGlazing()
        {
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();var workshop=factory.transform.Find("Aft processing workshop");
            if(workshop!=null)foreach(var renderer in workshop.GetComponentsInChildren<Renderer>(true))
                if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.name=="RAID011/Factory Flow Glass")renderer.enabled=false;
        }

        static void PlaceFishingGrounds()
        {
            var positions=new[]{new Vector3(22,-2,-22),new Vector3(27,-3,-26),new Vector3(23,-5,-34),new Vector3(33,-5,-31)};
            var counters=new Dictionary<int,int>();
            foreach(var fish in Object.FindObjectsByType<FishActor>().OrderBy(f=>f.transform.GetSiblingIndex()))
            {int i;counters.TryGetValue(fish.depthTier,out i);fish.transform.position=positions[i%positions.Length];counters[fish.depthTier]=i+1;EditorUtility.SetDirty(fish.transform);}
            counters.Clear();
            foreach(var loot in Object.FindObjectsByType<LootActor>().OrderBy(l=>l.transform.GetSiblingIndex()))
            {int i;counters.TryGetValue(loot.tier,out i);loot.transform.position=new Vector3(24+i*4,-6,-37-i*3);counters[loot.tier]=i+1;EditorUtility.SetDirty(loot.transform);}
            var floor=GameObject.Find("Seabed");if(floor!=null){floor.transform.position=new Vector3(0,-18,0);floor.transform.localScale=new Vector3(200,1,200);}
            int spawn=0;foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name.StartsWith("Spawn ",StringComparison.Ordinal)).OrderBy(g=>g.name))
                go.transform.position=new Vector3((spawn++-2)*3,1.2f,3);
        }

        static void RerouteFeed()
        {
            // Leave the centre of the living throat visible: feed through its upper-right shoulder.
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();
            var anchors=new[]{new Vector3(1.6f,1.3f,-3),new Vector3(2.02f,1.3f,-3),new Vector3(2.02f,3.02f,-3),new Vector3(3.15f,3.08f,-2.6f),new Vector3(3.15f,3.08f,4.6f),new Vector3(2.3f,3.05f,6.3f),new Vector3(1.35f,2.7f,7.6f)};
            var path=new List<Vector3>();
            for(int i=0;i<anchors.Length-1;i++)for(int step=0;step<8;step++)
            {
                float t=step/8f;var a=anchors[Mathf.Max(0,i-1)];var b=anchors[i];var c=anchors[i+1];var d=anchors[Mathf.Min(anchors.Length-1,i+2)];
                path.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            path.Add(anchors[anchors.Length-1]);factory.feedTube=path.ToArray();
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int sides=24;float distance=0;
            for(int ring=0;ring<path.Count;ring++)
            {
                if(ring>0)distance+=Vector3.Distance(path[ring-1],path[ring]);
                var tangent=(path[Mathf.Min(ring+1,path.Count-1)]-path[Mathf.Max(0,ring-1)]).normalized;
                var x=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized;var y=Vector3.Cross(tangent,x);
                for(int side=0;side<sides;side++){float a=side*Mathf.PI*2/sides;vertices.Add(path[ring]+(x*Mathf.Cos(a)+y*Mathf.Sin(a))*.34f);uv.Add(new Vector2(side/(float)sides,distance));}
            }
            for(int ring=0;ring<path.Count-1;ring++)for(int s=0;s<sides;s++)
            {int a=ring*sides+s,b=ring*sides+(s+1)%sides,c=a+sides,d=b+sides;triangles.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh{name="Feeding shoulder pipe"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var asset=SaveMesh("FeedingShoulderPipe",mesh);foreach(var r in factory.feedGlass)r.GetComponent<MeshFilter>().sharedMesh=asset;
            foreach(var r in factory.GetComponentsInChildren<Renderer>(true))
                if(r.sharedMaterial!=null&&r.sharedMaterial.name=="Enamel_safety_ochre")r.sharedMaterial=mats["Nereid_Ochre"];
            EditorUtility.SetDirty(factory);
        }

        static Mesh SaveMesh(string name,Mesh mesh)
        {
            string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }

        static void TankBarrier()
        {
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();
            var legacy=factory.transform.Find("Large industrial catch reservoir");if(legacy!=null)foreach(var c in legacy.GetComponentsInChildren<Collider>())c.enabled=false;
            var old=GameObject.Find("Central catch reservoir");if(old!=null)foreach(var c in old.GetComponents<Collider>())c.enabled=false;
            var v=new List<Vector3>();var tri=new List<int>();const int count=32;
            foreach(float y in new[]{.02f,3.13f})for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;v.Add(new Vector3(Mathf.Cos(a)*1.83f,y,Mathf.Sin(a)*1.83f));}
            for(int i=0;i<count;i++){int j=(i+1)%count;tri.AddRange(new[]{i,j,i+count,j,j+count,i+count});}
            for(int i=1;i<count-1;i++){tri.AddRange(new[]{0,i+1,i,count,count+i,count+i+1});}
            var mesh=new Mesh{name="Actual cylindrical reservoir collision"};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Cylindrical tank collision");go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(0,0,-3);go.AddComponent<MeshCollider>().sharedMesh=SaveMesh("PressureTankCollision",mesh);
        }

        static void OceanSilhouettes()
        {
            // Nearby reef geometry is visible from the real pressure windows, not a painted window backdrop.
            var rock=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/VisualSlice/Generated/Meshes/RockMesh201.asset");
            var rockMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/VisualSlice/Generated/Materials/Rock.mat");
            if(rock==null)return;
            Solid("Industrial fishing seabed",new Vector3(0,-8.8f,0),new Vector3(100,.05f,100),"Nereid_Dark");
            if(rockMaterial!=null)root.Find("Industrial fishing seabed").GetComponent<Renderer>().sharedMaterial=rockMaterial;
            for(int i=0;i<4;i++)
            {
                int side=i%2==0?-1:1;var go=new GameObject("Nearby reef spire "+i);go.transform.SetParent(root,false);
                go.transform.localPosition=new Vector3(side*(15+i%2*4),-6,-8+i*5.4f);go.transform.localScale=new Vector3(3.3f+i*.4f,13+i*.6f,4.2f);go.transform.localRotation=Quaternion.Euler(4*i,i*53,side*7);
                go.AddComponent<MeshFilter>().sharedMesh=rock;go.AddComponent<MeshRenderer>().sharedMaterial=rockMaterial!=null?rockMaterial:mats["Nereid_Dark"];go.AddComponent<MeshCollider>().sharedMesh=rock;
            }
        }

        static void SetupMaterials()
        {
            mats=new Dictionary<string,Material>();
            Mat("Nereid_Hull",new Color(.12f,.23f,.27f),.55f,.35f);
            Mat("Nereid_Steel",new Color(.21f,.28f,.29f),.58f,.33f);
            Mat("Nereid_Dark",new Color(.065f,.085f,.09f),.3f,.27f);
            Mat("Nereid_Trim",new Color(.37f,.42f,.4f),.68f,.38f);
            Mat("Nereid_Ochre",new Color(.61f,.37f,.105f),.3f,.32f);
            Mat("Nereid_Grate",new Color(.105f,.13f,.14f),.65f,.3f);
            Mat("Nereid_Rubber",new Color(.025f,.034f,.038f),.05f,.2f);
            Mat("Nereid_Lamp",new Color(1,.77f,.47f),.05f,.4f,3);
            Mat("Nereid_CreatureSkin",new Color(.14f,.17f,.23f),.05f,.43f);
            Mat("Nereid_CreatureDark",new Color(.055f,.08f,.135f),.05f,.36f);
            Mat("Nereid_CreatureGill",new Color(.21f,.09f,.145f),.02f,.48f);
            Mat("Nereid_CreatureIvory",new Color(.55f,.57f,.46f),.03f,.4f);
            Mat("Nereid_CreatureEye",new Color(.9f,.34f,.07f),.05f,.48f,2);
            foreach(string key in new[]{"Nereid_CreatureSkin","Nereid_CreatureDark","Nereid_CreatureGill"})
            {var tissue=mats[key];tissue.shader=Shader.Find("RAID011/Living Tissue");tissue.SetFloat("_DetailStrength",.075f);tissue.SetFloat("_CavityDepth",.08f);tissue.SetColor("_VeinColor",new Color(.08f,.055f,.105f));EditorUtility.SetDirty(tissue);}
            var glass=Mat("Nereid_Window",new Color(.1f,.24f,.28f,.12f),0,.72f);
            glass.shader=Shader.Find("RAID011/Factory Flow Glass");glass.SetColor("_BaseColor",new Color(.075f,.21f,.26f,.105f));glass.SetColor("_FlowColor",Color.black);EditorUtility.SetDirty(glass);
        }
        static Material Mat(string name,Color color,float metal,float smooth,float emission=0)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
            if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
            EditorUtility.SetDirty(m);mats[name]=m;return m;
        }
        static void BoxCollider(string name,Vector3 p,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=p;go.AddComponent<BoxCollider>().size=size;}
        static void Solid(string name,Vector3 p,Vector3 size,string material,bool collision=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mats[material];if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void Light(string name,Vector3 p,Color color,float intensity,float range,bool shadows)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=p;var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=shadows?LightShadows.Soft:LightShadows.None;l.shadowBias=.025f;l.shadowNormalBias=.18f;
        }
        static void Label(string text,Vector3 p,Quaternion rotation,float size)
        {
            var go=new GameObject("Sign "+text);go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localRotation=rotation;
            var label=go.AddComponent<TMPro.TextMeshPro>();label.font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Game/UI/Overhaul/Fonts/PTSans-SDF.asset");label.text=text;label.fontSize=size*10;label.alignment=TMPro.TextAlignmentOptions.Center;label.color=new Color(.7f,.78f,.73f);label.rectTransform.sizeDelta=new Vector2(4,.45f);
        }
    }
}
