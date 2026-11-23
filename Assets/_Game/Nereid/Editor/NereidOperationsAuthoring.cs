using System;
using System.IO;
using System.Linq;
using Raid.Nereid;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class NereidOperationsAuthoring
    {
        const string ScenePath = "Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Stations = "Assets/_Game/Nereid/ShipRebuild/Prefabs/Stations";
        const string Finishes = "Assets/_Game/Nereid/Generated/Nereid_";

        [MenuItem("Tools/NEREID/Install modular expedition")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing the vessel.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("Save the current scene before installing the vessel.");
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var vessel = Object.FindAnyObjectByType<NereidVessel>();
            if (vessel == null) throw new InvalidOperationException("Missing expedition root.");
            var layout = vessel.GetComponentInChildren<NereidShipLayout>();
            if (!layout) layout = NereidRoomAuthoring.Install(vessel);
            NereidTransitAuthoring.Install(vessel);
            InstallNavigation(vessel,layout);
            Directory.CreateDirectory(Stations);
            AssetDatabase.Refresh();
            Fit(layout, NereidRoomRole.Reservoir, ShipConsoleKind.Reservoir);
            Fit(layout, NereidRoomRole.Command, ShipConsoleKind.Depths);
            Fit(layout, NereidRoomRole.Equipment, ShipConsoleKind.SkillTree);
            Fit(layout, NereidRoomRole.Workshop, ShipConsoleKind.Process);
            Fit(layout, NereidRoomRole.Feeding, ShipConsoleKind.Feed);
            Fit(layout, NereidRoomRole.Cargo, ShipConsoleKind.CargoControl);
            if (AlignOperatorStations(layout)) EditorSceneManager.MarkSceneDirty(scene);
            foreach (var station in Object.FindObjectsByType<ShipConsole>())
                if (!station.GetComponent<NereidInstrument>()) station.gameObject.SetActive(false);
            var factory = Object.FindAnyObjectByType<Factory.FactoryVisuals>();
            if (factory)
            {
                foreach (var label in factory.GetComponentsInChildren<TMP_Text>(true))
                    if (label.text == "ЦЕХ ПЕРЕРАБОТКИ" || label.text == "ПЕРЕРАБОТЧИК")
                        label.gameObject.SetActive(false);
            }
            var weapon = Resources.Load<GameObject>("ExpeditionHarpoon");
            if (weapon && weapon.TryGetComponent<HarpoonVisualRig>(out var rig))
            {
                var display = Object.FindAnyObjectByType<HarpoonPresentation>();
                if (display && !display.weaponAppearance)
                {
                    display.weaponAppearance=rig;
                    EditorUtility.SetDirty(display);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(display);
                }
            }
            var oldScreens = vessel.GetComponent<NereidShipScreens>();
            if (oldScreens) oldScreens.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save modular vessel scene.");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/NEREID/Fit standing monitor positions")]
        public static void FitOperationalMonitors()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before mounting monitors.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty)throw new InvalidOperationException("Save current changes before mounting monitors.");
            if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
            var layout=Object.FindAnyObjectByType<NereidShipLayout>();
            if(!layout)throw new InvalidOperationException("Install the modular ship before its monitors.");
            if(AlignOperatorStations(layout))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Unable to save the monitor positions.");
            }
            AssetDatabase.SaveAssets();
        }

        // Socket-local offsets only. Original sockets also own crew spawns, pipe anchors and navigation markers.
        static (Vector3 position,float yaw) Mount(ShipConsoleKind kind)=>kind switch
        {
            ShipConsoleKind.CargoControl=>(new(2.20f,0,-9.75f),142),     // Across from the outer airlock apron
            ShipConsoleKind.Process=>(new(.72f,0,-2.45f),-18),            // Inside the arch, overlooking the conveyor
            ShipConsoleKind.Feed=>(new(-.41f,0,-.45f),-42),              // Facing the approach from the tank
            ShipConsoleKind.Reservoir=>(new(.33f,0,.22f),-19),            // Stand on the solid starboard aisle
            ShipConsoleKind.SkillTree=>(new(.22f,0,-.29f),22),           // Readable from the equipment-room entrance
            ShipConsoleKind.Depths=>(new(-.15f,0,.59f),-16),             // Turned toward the bridge landing
            _=>(Vector3.zero,0)
        };

        static void PlaceStation(Transform station,ShipConsoleKind kind)
        {
            var pose=Mount(kind);
            station.SetLocalPositionAndRotation(pose.position,Quaternion.Euler(0,pose.yaw,0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
        }

        static bool AlignOperatorStations(NereidShipLayout layout)
        {
            bool changed=false;
            foreach(var instrument in layout.GetComponentsInChildren<NereidInstrument>(true))
            {
                var station=instrument.console;
                if(!station)continue;
                var t=station.transform;
                bool atSocket=layout.stations.Any(s=>s.anchor==t.parent);
                // A previous factory install attached each console flush to its socket. Hand-aligned
                // transforms and added stations belong to the level artist and retain their poses.
                if(atSocket && t.localPosition.sqrMagnitude<.000001f && Quaternion.Angle(t.localRotation,Quaternion.identity)<.05f)
                {
                    PlaceStation(t,station.kind);
                    changed=true;
                }
                if(!station.IsMonitor)continue;
                string path=Stations+"/"+station.kind+".prefab";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!source || source.transform.Find(DisplayCarrier))continue;
                var host=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    MountDisplay(host.GetComponent<ShipConsole>());
                    PrefabUtility.SaveAsPrefabAsset(host,path);
                }
                finally {PrefabUtility.UnloadPrefabContents(host);}
            }
            return changed;
        }

        const string DisplayCarrier="Articulated display carrier";
        static void MountDisplay(ShipConsole station)
        {
            if(!station.IsMonitor)return;
            var pivot=station.screenAnchor.localPosition;
            var carrier=Child(station.transform,DisplayCarrier,pivot);
            var components=station.transform.Cast<Transform>().Where(t=>t==station.screenAnchor||
                t.name is "Pressure casing" or "Recessed display" or "Machined vertical edge" or "Edge guard"
                    or "Display seal" or "Angled instrument wing" or "Line indicator").ToArray();
            foreach(var piece in components)
            {
                Vector3 relative=piece.localPosition-pivot;
                piece.SetParent(carrier,false);piece.localPosition=relative;
            }
            (float elevation,float pitch)=station.kind switch
            {
                ShipConsoleKind.Reservoir=>(1.58f,2f), // Bottom row clears the keyboard on the tall panel
                ShipConsoleKind.SkillTree=>(1.52f,4f),
                _=>(1.50f,4f)
            };
            carrier.localPosition=new Vector3(pivot.x,elevation,pivot.z);
            carrier.localRotation=Quaternion.Euler(pitch,0,0);
        }

        static void InstallNavigation(NereidVessel vessel,NereidShipLayout layout)
        {
            if (layout.GetComponentInChildren<NereidDeckNavigation>()) return;
            var rig = Child(layout.transform,"Expedition navigation",Vector3.zero).gameObject.AddComponent<NereidDeckNavigation>();
            rig.rooms = layout;
            var spawner = Object.FindAnyObjectByType<FishNet.Component.Spawning.PlayerSpawner>();
            if (!spawner) throw new InvalidOperationException("Missing FishNet crew spawner.");
            var crew = Child(layout.Socket(NereidRoomRole.Reservoir),"Crew muster points",Vector3.zero);
            rig.crewSpawns = spawner.Spawns;
            foreach(var spawn in rig.crewSpawns) spawn.SetParent(crew,true);

            Transform Mark(NereidRoomRole role,string name,Vector3 local)
            {
                var node = Child(layout.Socket(role),name,Vector3.zero);
                node.position = vessel.transform.TransformPoint(local);
                return node;
            }
            var deck = NereidDeckNavigation.DefaultCompanionWaypoints;
            rig.companionWaypoints = new Transform[deck.Length];
            for (int i=0;i<deck.Length;i++)
            {
                Vector3 p = deck[i];
                var room = p.z < -24.6f ? NereidRoomRole.Command : p.z < -16 ? NereidRoomRole.Equipment :
                    p.z < -8 ? NereidRoomRole.Cargo : p.z > 4 ? NereidRoomRole.Feeding : NereidRoomRole.Reservoir;
                rig.companionWaypoints[i] = Mark(room,"Companion waypoint "+(i+1),p);
            }
            var approach = new[]{new Vector3(12,.75f,-14),new Vector3(10,.75f,-14),new Vector3(5,.75f,-14),
                new Vector3(0,.75f,-14),new Vector3(0,.75f,-7.25f),new Vector3(0,.75f,-5.5f),new Vector3(0,-.75f,-5.5f)};
            rig.raiderApproach = new Transform[approach.Length];
            for (int i=0;i<approach.Length;i++)
                rig.raiderApproach[i] = Mark(i<3?NereidRoomRole.Airlock:i<5?NereidRoomRole.Cargo:NereidRoomRole.Reservoir,
                    "Raider approach "+(i+1),approach[i]);
            Physics.SyncTransforms();
            var origin = rig.raiderApproach[approach.Length-2].position;
            if (!Physics.Raycast(origin,Vector3.down,out var hit,3.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Missing solid reservoir deck at raider approach.");
            rig.reservoirGlass = hit.collider;
            EditorUtility.SetDirty(spawner);
        }

        static void Fit(NereidShipLayout layout, NereidRoomRole room, ShipConsoleKind kind)
        {
            // An existing instrument can be moved or replaced in Unity; installation preserves its placement.
            if (layout.GetComponentsInChildren<NereidInstrument>(true).Any(i => i.console && i.console.kind == kind)) return;
            var socket = layout.Socket(room);
            if (!socket) throw new InvalidOperationException("Missing room socket for " + room);
            string path = Stations + "/" + kind + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                var master = Build(kind);
                try { prefab = PrefabUtility.SaveAsPrefabAsset(master,path); }
                finally { Object.DestroyImmediate(master); }
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,socket);
            PlaceStation(instance.transform,kind);
            var scale = socket.lossyScale;
            instance.transform.localScale = new Vector3(1f/scale.x,1f/scale.y,1f/scale.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        static Transform Child(Transform parent,string name,Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent,false);
            child.localPosition = position;
            return child;
        }

        static Transform Block(Transform parent,string name,Vector3 position,Vector3 size,string finish)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            body.name = name;
            body.transform.SetParent(parent,false);
            body.transform.localPosition = position;
            body.transform.localScale = size;
            body.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Finishes+finish+".mat");
            return body.transform;
        }

        static GameObject Build(ShipConsoleKind kind)
        {
            var go = new GameObject(kind + " · NEREID");
            var station = go.AddComponent<ShipConsole>();
            station.kind = kind;
            station.range = 3.75f;
            float w = kind == ShipConsoleKind.Reservoir ? 3.32f : 2.64f;
            float h = kind == ShipConsoleKind.Reservoir ? 1.98f : 1.75f;
            float y = 1.57f;
            station.screenSize = new Vector2(w,h);
            station.screenAnchor = Child(go.transform,"Display plane",new Vector3(0,y,-.14f));
            var face = station.screenAnchor.gameObject.AddComponent<BoxCollider>();
            face.size = new Vector3(w,h,.026f);
            var instrument = go.AddComponent<NereidInstrument>();
            instrument.console = station;
            var text = Child(station.screenAnchor,"Live readout",Vector3.back*.022f).gameObject.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/UI/Overhaul/Fonts/PTSans-SDF.asset");
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = station.screenSize*.91f;
            text.fontSize = 1.30f;
            text.color = new Color(.78f,.95f,.94f);
            text.text = "NEREID";
            instrument.readout = text;
            Block(go.transform,"Pressure casing",new Vector3(0,y,.024f),new Vector3(w+.18f,h+.18f,.28f),"Hull");
            Block(go.transform,"Recessed display",new Vector3(0,y,-.115f),new Vector3(w,h,.026f),"Dark");
            for(int side=-1;side<=1;side+=2)
            {
                Block(go.transform,"Machined vertical edge",new Vector3(side*(w*.5f+.045f),y,-.134f),new Vector3(.085f,h+.14f,.088f),"Steel");
                Block(go.transform,"Edge guard",new Vector3(side*(w*.5f+.047f),y+.2f,-.17f),new Vector3(.10f,h*.42f,.06f),"Ochre");
                Block(go.transform,"Display seal",new Vector3(0,y+side*(h*.5f+.028f),-.13f),new Vector3(w,.061f,.10f),"Rubber");
                if(kind==ShipConsoleKind.Reservoir)
                {
                    var wing = Child(go.transform,"Angled instrument wing",new Vector3(side*(w*.5f+.12f),y,-.02f));
                    wing.localRotation = Quaternion.Euler(0,side*19,0);
                    Block(wing,"Wing casting",new Vector3(side*.18f,0,0),new Vector3(.38f,h+.10f,.23f),"Hull");
                    for(int j=0;j<5;j++) Block(wing,"Signal aperture",new Vector3(side*.17f,(j-2)*.17f,-.126f),new Vector3(.18f,.05f,.035f),j<3?"Trim":"Dark");
                }
            }
            var pedestal = Block(go.transform,"Cable pedestal",new Vector3(0,.32f,.10f),new Vector3(1.30f,.64f,.52f),"Hull");
            pedestal.gameObject.AddComponent<BoxCollider>();
            Block(go.transform,"Mounting plinth",new Vector3(0,.05f,.04f),new Vector3(1.69f,.10f,.86f),"Steel");
            var desk=Child(go.transform,"Sloping operator deck",new Vector3(0,.59f,-.37f));
            desk.localRotation=Quaternion.Euler(-12,0,0);
            Block(desk,"Keyboard surround",Vector3.zero,new Vector3(1.58f,.08f,.46f),"Steel");
            for(int row=0;row<3;row++)
                for(int key=0;key<8;key++) Block(desk,"Keycap",new Vector3(-.54f+key*.103f,.05f,-.13f+row*.105f),new Vector3(.083f,.047f,.075f),"Dark");
            for(int i=0;i<3;i++) Block(desk,"Guarded switch",new Vector3(.38f+i*.13f,.064f,.024f),new Vector3(.071f,.09f,.14f),i==2?"Ochre":"Trim");
            var strip=Child(go.transform,"Line indicator",new Vector3(-.62f,y-h*.5f-.045f,-.196f));
            Block(strip,"Active signal",new Vector3(.62f,0,0),new Vector3(1.24f,.027f,.015f),"Lamp");
            instrument.gauge=strip;
            MountDisplay(station);
            if (kind is ShipConsoleKind.Feed or ShipConsoleKind.Process) NereidDispatchAuthoring.Fit(station);
            return go;
        }
    }
}
