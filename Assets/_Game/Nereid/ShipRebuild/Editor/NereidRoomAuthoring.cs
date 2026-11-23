using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        const string Root="Assets/_Game/Nereid/ShipRebuild";
        const string RoomPath=Root+"/Prefabs/Rooms";
        const string HullPath=Root+"/Prefabs/Hull";
        public const string ShipPath=Root+"/Prefabs/NEREID_Rebuilt.prefab";

        static Material hull, dark, deck, enamel, ceramic, steel, ochre, warm, cyan, alarm, glass, water, rubber;
        static Transform room;
        static Vector3 origin;
        static string roomId;
        static readonly List<ShipSectionMesh> parts=new();

        static readonly (string id,Vector3 at,Action build)[] modules={
            ("PressureHull",new(0,0,-9),Exterior),
            ("ObservationHall",new(0,0,-2),ObservationHall),
            ("Aquarium",new(0,-1.2f,-2),Aquarium),
            ("AftTransfer",new(0,0,-12),CargoDeck),
            ("EquipmentRoom",new(0,0,-20),EquipmentRoom),
            ("NavigationBridge",new(0,4.85f,-27.5f),CommandBridge),
            ("BioContainment",new(0,0,6),FeedingRoom),
            ("CompanionBerth",new(4.6f,0,2.6f),CompanionBerth),
            ("AirlockCollar",new(5.95f,0,-14),AirlockCollar),
        };

        static readonly (NereidRoomRole role,string module,Vector3 at,float yaw)[] sockets={
            (NereidRoomRole.Command,"NavigationBridge",new(-.38f,4.85f,-28.53f),180),
            (NereidRoomRole.Reservoir,"ObservationHall",new(3.65f,0,-.7f),90),
            (NereidRoomRole.Workshop,"AftTransfer",new(-4.85f,0,-10),-90),
            (NereidRoomRole.Cargo,"AftTransfer",new(-4.85f,0,-14.25f),-90),
            (NereidRoomRole.Equipment,"EquipmentRoom",new(3.30f,0,-22.40f),90),
            (NereidRoomRole.Feeding,"BioContainment",new(3.95f,0,6.20f),90),
            (NereidRoomRole.Airlock,"AftTransfer",new(5.50f,0,-12.15f),90),
            (NereidRoomRole.Companion,"CompanionBerth",new(4.65f,0,2.65f),90),
        };

        public static NereidShipLayout Install(NereidVessel vessel)
        {
            if(vessel==null || !vessel.gameObject.scene.IsValid() || Application.isPlaying)
                throw new InvalidOperationException("Select the stopped gameplay scene before installing the NEREID rooms.");
            var existing=vessel.GetComponentInChildren<NereidShipLayout>(true);
            if(existing!=null)return existing;
            Folder(Root+"/Prefabs");
            Folder(RoomPath);Folder(HullPath);Folder(Root+"/Meshes");Folder(Root+"/Materials");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ShipPath);
            if(!prefab)
            {
                Palette();
                var assembly=new GameObject("NEREID_Rebuilt");
                try
                {
                    var layout=assembly.AddComponent<NereidShipLayout>();
                    var placed=new Dictionary<string,Transform>();
                    foreach(var module in modules)
                    {
                        string path=(module.id=="PressureHull"?HullPath:RoomPath)+"/"+module.id+".prefab";
                        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if(!asset)
                        {
                            roomId=module.id;origin=module.at;room=new GameObject(roomId).transform;parts.Clear();
                            try
                            {
                                module.build();
                                foreach(var socket in sockets.Where(v=>v.module==roomId))
                                {
                                    var anchor=new GameObject(socket.role+" socket").transform;
                                    anchor.SetParent(room,false);anchor.localPosition=socket.at-origin;
                                    anchor.localRotation=Quaternion.Euler(0,socket.yaw,0);
                                }
                                foreach(var p in parts)p.Store(Root+"/Meshes/"+roomId+"_"+p.node.name+".asset");
                                asset=PrefabUtility.SaveAsPrefabAsset(room.gameObject,path);
                            }
                            finally { Object.DestroyImmediate(room.gameObject); parts.Clear(); }
                        }
                        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,assembly.transform);
                        instance.transform.localPosition=module.at;
                        placed.Add(module.id,instance.transform);
                    }
                    layout.stations=sockets.Select(s=>new NereidShipLayout.StationSocket{
                        role=s.role,anchor=placed[s.module].Find(s.role+" socket")}).ToArray();
                    var volumes=new List<NereidShipLayout.PressureVolume>();
                    void Volume(string id,Vector3 a,Vector3 b)
                    {
                        var t=placed[id];
                        volumes.Add(new NereidShipLayout.PressureVolume{room=t,localBounds=new Bounds((a+b)*.5f-t.localPosition,b-a)});
                    }
                    Volume("ObservationHall",new(-5.94f,-.025f,-8.06f),new(5.94f,4.6f,4.50f));
                    Volume("BioContainment",new(-5.94f,-.025f,4.49f),new(5.94f,4.59f,8.15f));
                    Volume("AftTransfer",new(-5.94f,-.025f,-16.15f),new(5.94f,3.65f,-8.04f));
                    Volume("EquipmentRoom",new(-4.25f,-.025f,-24.86f),new(4.25f,3.45f,-16.1f));
                    Volume("NavigationBridge",new(-4.34f,4.82f,-30.52f),new(4.34f,7.12f,-24.74f));
                    // The companionway is inclined; its thin volume follows the open stairwell above the machinery deck.
                    var passage=new GameObject("Companionway dry volume").transform;
                    passage.SetParent(placed["EquipmentRoom"],false);
                    var a=new Vector3(-2.58f,0,-17.65f);var b=new Vector3(-2.58f,4.85f,-24.85f);
                    passage.localPosition=(a+b)*.5f+Vector3.up*1.05f-placed["EquipmentRoom"].localPosition;
                    passage.localRotation=Quaternion.LookRotation((a-b).normalized);
                    volumes.Add(new NereidShipLayout.PressureVolume {room=passage,localBounds=new Bounds(Vector3.zero,
                        new Vector3(2.03f,2.32f,Vector3.Distance(a,b)+1.44f))});
                    layout.dryCompartments=volumes.ToArray();
                    prefab=PrefabUtility.SaveAsPrefabAsset(assembly,ShipPath);
                }
                finally { Object.DestroyImmediate(assembly); }
            }
            if(prefab==null)throw new InvalidOperationException("NEREID prefab save failed");
            CompletePressureFairings();
            OceanEnvelopeAuthoring.Install();
            RetireOldShip(vessel);
            var result=(GameObject)PrefabUtility.InstantiatePrefab(prefab,vessel.transform);
            EditorSceneManager.MarkSceneDirty(vessel.gameObject.scene);
            return result.GetComponent<NereidShipLayout>();
        }

        static void RetireOldShip(NereidVessel vessel)
        {
            // Limit retirement to known generated geometry. Gameplay objects, ocean, lights on the
            // graft and user-authored children survive. The original assets remain on disk.
            string[] exact={
                "Nereid_Interior","Nereid_Exterior","Walkable main and aft deck","Main overhead pressure barrier",
                "Aft ceiling pressure barrier","Port sealed window wall","Starboard main sealed windows",
                "Starboard aft wall before hatch","Starboard aft wall behind hatch","Starboard hatch lintel",
                "Stern sealed bulkhead","Bow side pressure wall","Bow upper pressure wall","Aft partition side","Aft partition top",
                "Cylindrical tank collision","Reservoir observation gallery","Continuous stair handrail","Stair nosing",
                "Stair safety post","Stern airlock landing","Stern platform","Dock safety stanchion","Dock handrail",
                "Caged luminaire downlight","Cold pressure window bounce","Warm reflected working-plane fill","Reservoir reflected water",
                "Aft loading room","Aft loading work light","Workshop task light","Overhead cargo rail","Stern cargo transverse rail",
                "Cargo rail support","Sealed reservoir overhead hopper","Reservoir delivery throat"};
            var names=new HashSet<string>(exact,StringComparer.Ordinal);
            foreach(Transform old in vessel.transform)
                if(names.Contains(old.name)||old.name.StartsWith("Gallery ",StringComparison.Ordinal)||
                   old.name.StartsWith("Aft gallery stair ",StringComparison.Ordinal)||old.name.StartsWith("Forward gallery stair ",StringComparison.Ordinal))
                    old.gameObject.SetActive(false);
            var f=Object.FindObjectsByType<Factory.FactoryVisuals>().FirstOrDefault(v=>v.gameObject.scene==vessel.gameObject.scene);
            if(f!=null)
            {
                foreach(var path in new[]{"Large industrial catch reservoir","Tank water fill","NEREID equipment refinement/Pressure reservoir"})
                {var t=f.transform.Find(path); if(t!=null)t.gameObject.SetActive(false);}
                // The legacy mesh baker merged the cylinder, processor and pipe clamps into global material slots.
                // The processor and pipes have individual meshes in the rebuilt prefabs and original conveyor objects.
                var refinement=f.transform.Find("NEREID equipment refinement");
                if(refinement!=null)
                    foreach(Transform t in refinement)
                        if(t.name.StartsWith("Equipment /",StringComparison.Ordinal) && !t.name.Contains("Rooted graft"))
                            t.gameObject.SetActive(false);
            }
        }

        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int cut=path.LastIndexOf('/');
            Folder(path.Substring(0,cut));AssetDatabase.CreateFolder(path.Substring(0,cut),path.Substring(cut+1));
        }

        static ShipSectionMesh Part(string name, Vector3 p)
        {
            var s=new ShipSectionMesh(room,origin,name,p);parts.Add(s);return s;
        }

        static void Palette()
        {
            var lit=Shader.Find("Universal Render Pipeline/Lit");
            var coating=Resources.Load<Shader>("NereidHullCoating");
            var atlas=Resources.Load<Texture2D>("NereidServiceFittings/Textures/T_Trim_01_BaseColor");
            if(!lit||!coating||!atlas)throw new InvalidOperationException("NEREID requires URP and the imported service-fitting trim atlas.");
            Material Make(string label,Color color,float metallic,float smooth,float glow=0,bool coat=false,bool floor=false,bool transparent=false)
            {
                string path=Root+"/Materials/"+label+".mat";
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
                var m=new Material(coat?coating:lit){name=label};
                m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
                if(coat)
                {
                    m.SetTexture("_SurfaceAtlas",atlas);
                    m.SetVector("_SurfaceRegion",floor?new Vector4(.019f,.074f,.962f,.412f):new Vector4(.242f,.686f,.516f,.293f));
                    m.SetVector("_SurfaceTiling",floor?new Vector4(.38f,.67f,0,0):new Vector4(.32f,.50f,0,0));
                    m.SetVector("_SurfaceDetail",floor?new Vector4(.053f,.65f,.012f,1):new Vector4(.10f,.55f,.012f,1));
                    m.SetFloat("_Marine",label=="Atlantic blue pressure steel"?1:0);m.SetFloat("_DeckTread",floor?1:0);
                }
                if(transparent)
                {
                    m.SetFloat("_Surface",1);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);
                    m.SetFloat("_SrcBlendAlpha",1);m.SetFloat("_DstBlendAlpha",10);m.SetFloat("_ZWrite",0);
                    m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);
                }
                if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(color.r,color.g,color.b)*glow);}
                AssetDatabase.CreateAsset(m,path);return m;
            }
            hull=Make("Atlantic blue pressure steel",new(.17f,.30f,.34f),.45f,.43f,coat:true);
            dark=Make("Graphite forged frames",new(.105f,.14f,.16f),.40f,.36f,coat:true);
            enamel=Make("Petrol marine enamel",new(.24f,.37f,.37f),.29f,.45f,coat:true);
            ceramic=Make("Ivory ceramic panels",new(.50f,.56f,.52f),.24f,.50f,coat:true);
            deck=Make("Matte graphite deck",new(.25f,.29f,.32f),.19f,.28f,coat:true,floor:true);
            steel=Make("Machined titanium",new(.44f,.51f,.53f),.68f,.55f);
            ochre=Make("Rescue ochre",new(.84f,.445f,.10f),.28f,.39f,coat:true);
            warm=Make("Amber work light",new(1,.66f,.30f),.02f,.44f,2.2f);
            cyan=Make("Reservoir teal light",new(.22f,.87f,.93f),.01f,.45f,1.9f);
            alarm=Make("Containment signal",new(1,.23f,.075f),.05f,.37f,2.1f);
            rubber=Make("Reinforced hose rubber",new(.040f,.063f,.068f),.04f,.27f);
            glass=Make("Laminated observation glass",new(.22f,.58f,.64f,.13f),.06f,.82f,transparent:true);
            water=Make("Aquarium immersion tint",new(.07f,.62f,.70f,.10f),.03f,.62f,transparent:true);
        }
    }
}
