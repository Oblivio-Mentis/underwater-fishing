using System.Collections.Generic;
using System.Linq;
using Raid.Factory;
using Raid.Nereid;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Raid.Editor
{
    public static class ExpeditionSceneAuthoring
    {
        internal const string ScenePath="Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string Models="Assets/_Game/Data/ExpeditionModels";

        public static ExpeditionLayout Install(Scene scene)
        {
            var hierarchy=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).ToArray();
            T Find<T>() where T:Component=>hierarchy.Select(x=>x.GetComponent<T>()).FirstOrDefault(x=>x);
            var world=Find<RaidWorld>();
            var vessel=Find<NereidVessel>();
            var threat=Find<ThreatDirector>();
            if(!world || !vessel || !threat || !threat.tuning)return null;
            if(world.expeditionLayout)
            {
                var current=world.expeditionLayout;
                if(current.goshaJunkSites==null || current.goshaJunkSites.Length==0)
                {
                    Undo.RecordObject(current,"Migrate encounter defaults");
                    if(!current.shallows.krakenSite)current.shallows.krakenEnabled=false;
                    if(current.shallows.eelPatrol==null || current.shallows.eelPatrol.Length==0)current.shallows.eelEnabled=false;
                    InstallJunkSites(current,vessel.transform,Find<GoshaReceiver>(),Find<NereidPressureDoors>());
                }
                if(current.dispenser)foreach(var text in current.dispenser.GetComponentsInChildren<TMP_Text>(true))
                    if(text.TryGetComponent<MeshFilter>(out var filter) && AssetDatabase.Contains(filter.sharedMesh))
                        filter.sharedMesh=null;
                return current;
            }

            var group=new GameObject("Редактирование экспедиции");
            SceneManager.MoveGameObjectToScene(group,scene);
            Undo.RegisterCreatedObjectUndo(group,"Добавить настройки экспедиции");
            var layout=group.AddComponent<ExpeditionLayout>();
            var occupied=hierarchy.Select(x=>x.GetComponent<ShipConsole>())
                .FirstOrDefault(x=>x && x.kind==ShipConsoleKind.SkillTree && x.screenAnchor);
            var shoal=Marker(group.transform,"Спавн · Мелководье",Vector3.zero);
            var rift=Marker(group.transform,"Спавн · Разлом",Vector3.zero);
            layout.shallows=CreateSpawns(threat,shoal,false);
            layout.rift=CreateSpawns(threat,rift,true);

            var hull=vessel.transform;
            layout.goshaDen=Marker(hull,"Гоша · Домик",hull.TransformPoint(GoshaDenLayout.Bed),hull.rotation);
            layout.goshaApproach=Marker(hull,"Гоша · Подход к домику",hull.TransformPoint(new Vector3(3.96f,.14f,3.02f)));
            layout.goshaDenSlots=GoshaDenLayout.Pieces.Select((piece,i)=>
                Marker(layout.goshaDen,$"Находка {i+1:00}",layout.goshaDen.position+hull.rotation*piece.at,
                    hull.rotation*Quaternion.Euler(piece.angle))).ToArray();
            var receiver=Find<GoshaReceiver>();
            InstallJunkSites(layout,hull,receiver,Find<NereidPressureDoors>());

            if(receiver && !receiver.ServicePipe)
            {
                var navigation=Find<NereidDeckNavigation>();
                var mark=navigation && navigation.companionWaypoints.Length>5?navigation.companionWaypoints[5]:null;
                Vector3 entrance=mark?mark.position:hull.TransformPoint(GoshaDeck.DefaultMarks[5]);
                Undo.RecordObject(receiver,"Назначить маршрут Гоши");
                var service=receiver.ServiceRoute(entrance);
                if(service)Undo.RegisterCreatedObjectUndo(service.gameObject,"Добавить маршрут Гоши");
                Record(receiver);
            }

            if(!AssetDatabase.IsValidFolder(Models))AssetDatabase.CreateFolder("Assets/_Game/Data","ExpeditionModels");
            layout.goshaModel=CompanionModel();
            layout.goshaJunkMeshes=System.Enum.GetValues(typeof(GoshaJunkKind)).Cast<GoshaJunkKind>()
                .Select(kind=>Asset("Gosha "+kind,GoshaDenArt.Found(kind).Mesh(kind.ToString()))).ToArray();
            layout.goshaShelterMeshes=Enumerable.Range(0,3)
                .Select(i=>Asset("Gosha shelter "+(i+1),GoshaDenArt.Shelter(i).Mesh("Gosha shelter"))).ToArray();

            var factory=Find<FactoryVisuals>();
            if(factory)
            {
                if(factory.processRoute && factory.processorDrop)
                {
                    var floorHit=Marker(factory.processorDrop,"Вода · Точка падения",factory.processorDrop.position+Vector3.up*.07f);
                    Undo.RecordObject(factory.processRoute,"Назначить падение воды");
                    factory.processRoute.waterReceiver=floorHit;Record(factory.processRoute);
                }
                var tongue=factory.upperJaw?factory.upperJaw.parent.Find("Curved muscular tongue"):null;
                if(factory.feedRoute && tongue)
                {
                    Undo.RecordObject(factory.feedRoute,"Назначить падение воды");
                    factory.feedRoute.waterReceiver=Marker(tongue,"Вода · Точка падения",tongue.TransformPoint(new Vector3(.10f,-.53f,.33f)));
                    Record(factory.feedRoute);
                }
                foreach(var route in new[]{factory.processRoute,factory.feedRoute})
                {
                    var shader=Resources.Load<Shader>("RaidPipeWater");
                    if(!route || !shader)continue;
                    Undo.RecordObject(route,"Назначить материал воды");
                    route.flowWaterMaterial=Asset("Pipe water "+route.line,new Material(shader));Record(route);
                }
            }
            if(factory && factory.upperJaw)
            {
                var mouth=factory.upperJaw.parent;
                layout.chargeAperture=Marker(mouth,"Кракен · Приём динамита",mouth.TransformPoint(new Vector3(0,-.12f,-.84f)),mouth.rotation);
                layout.flameOutlet=Marker(mouth,"Кракен · Выход пламени",mouth.TransformPoint(new Vector3(0,-.12f,-1.03f)),
                    Quaternion.LookRotation((-mouth.forward-mouth.up*.25f).normalized,Vector3.up));
                layout.gulletMesh=Asset("Kraken throat",KrakenGullet.Build());
                var throat=Resources.Load<Shader>("RaidKrakenGullet");
                if(throat)layout.gulletMaterial=Asset("Kraken throat",new Material(throat));
            }

            if(occupied)
            {
                var screen=occupied.screenAnchor;
                var forward=Vector3.ProjectOnPlane(-screen.forward,Vector3.up).normalized;
                var center=screen.position-screen.right*2+forward*.30f;
                var physics=scene.GetPhysicsScene();
                Physics.SyncTransforms();
                if(physics.Raycast(center,Vector3.down,out var floor,3,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    center.y=floor.point.y+1.2f;
                layout.dispenser=Marker(occupied.transform.parent,"Раздатчик технологий",center,Quaternion.LookRotation(forward));
                var collider=layout.dispenser.gameObject.AddComponent<BoxCollider>();
                const float back=-.485f;
                collider.center=new Vector3(0,-.13f,(back+TechRigShell.VendorGlassZ)*.5f);
                collider.size=new Vector3(1.32f,2.10f,TechRigShell.VendorGlassZ-back);
                var cabinet=TechRigShell.BuildDispenser(layout.dispenser.gameObject);
                if(cabinet.sound && !AssetDatabase.Contains(cabinet.sound))cabinet.sound=null;
                var font=Find<UI.UiRoot>()?.font;
                foreach(var label in cabinet.GetComponentsInChildren<TMP_Text>(true))if(font)label.font=font;
                BakeVendorMeshes(cabinet.transform);
                BakeEquipment();
            }

            Undo.RecordObjects(new Object[]{world,threat},"Связать настройки экспедиции");
            world.expeditionLayout=threat.expeditionLayout=layout;
            Record(world);Record(threat);Record(layout);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return layout;
        }

        public static GoshaModel CompanionModel()
        {
            const string path=Models+"/Gosha.prefab";
            var saved=AssetDatabase.LoadAssetAtPath<GoshaModel>(path);
            if(saved)return saved;

            var preview=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Gosha");
            SceneManager.MoveGameObjectToScene(root,preview);
            var view=root.AddComponent<GoshaView>();
            try
            {
                var model=view.BuildAuthoringTemplate();
                var material=SaveMaterial("Gosha lacquer and woven ball",model.ball.sharedMaterial);
                int index=0;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    filter.sharedMesh=Asset($"Gosha rig {++index:00} {filter.name}",Object.Instantiate(filter.sharedMesh));
                    filter.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                view.ReleaseAuthoringTemplate();
                Object.DestroyImmediate(view);
                return PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<GoshaModel>();
            }
            finally
            {
                if(view)view.ReleaseAuthoringTemplate();
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void InstallJunkSites(ExpeditionLayout layout,Transform hull,GoshaReceiver receiver,NereidPressureDoors lockRoom)
        {
            if(layout.goshaJunkSites is {Length:>0})return;
            var exit=lockRoom?lockRoom.transform.TransformPoint(new Vector3(12.6f,1,0)):
                hull.TransformPoint(new Vector3(5.95f,0,-14))+hull.rotation*new Vector3(12.6f,1,0);
            var inlet=receiver?receiver.TransferPosition:exit;
            var side=Mathf.Sign(hull.InverseTransformPoint(inlet).x);
            var outward=hull.right*(side==0?1:side);
            var junk=Marker(layout.transform,"Гоша · Области находок",Vector3.zero);
            var slots=new List<Transform>();
            for(int i=0;i<6;i++)
            {
                float fore=(i-2.5f)*2.7f;
                slots.Add(Marker(junk,$"Находка {2*i+1:00}",inlet+outward*(2.5f+(i%3)*2.6f)+hull.forward*fore));
                slots.Add(Marker(junk,$"Находка {2*i+2:00}",exit+hull.right*6.1f+hull.forward*fore));
            }
            slots.AddRange(RaidWorld.JunkSpots.Select((spot,i)=>Marker(junk,$"Находка {i+13:00}",
                hull.TransformPoint(new Vector3(spot.x,0,spot.y)))));
            Undo.RecordObject(layout,"Назначить места находок");
            layout.goshaJunkSites=slots.ToArray();
            Record(layout);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        }

        static ExpeditionLayout.Depth CreateSpawns(ThreatDirector threat,Transform group,bool deep)
        {
            var offsets=threat.anglerProfile?.territoryOffsets;
            if(offsets==null || offsets.Length==0)offsets=new[]{Vector3.zero};
            var tuning=threat.tuning;
            return new ExpeditionLayout.Depth
            {
                eelEnabled=deep,krakenEnabled=deep,
                anglerTerritories=offsets.Select((offset,i)=>Marker(group,$"Удильщик · зона {i+1}",tuning.predatorSpawn+offset)).ToArray(),
                raiderArrival=Marker(group,"Налётчик · прибытие",tuning.waveSpawn),
                clamSite=Marker(group,"Моллюск · место на дне",tuning.clamAnchor),
                krakenSite=deep?Marker(group,"Кракен · место на дне",tuning.krakenAnchor):null,
                eelPatrol=deep?ThreatDirector.DefaultEelPatrol.Select((p,i)=>Marker(group,$"Угорь · патруль {i+1}",p)).ToArray():null
            };
        }

        internal static Transform Marker(Transform parent,string name,Vector3 point,Quaternion? rotation=null)
        {
            var marker=new GameObject(name);
            SceneManager.MoveGameObjectToScene(marker,parent.gameObject.scene);
            marker.transform.SetPositionAndRotation(point,rotation??Quaternion.identity);
            marker.transform.SetParent(parent,true);
            Undo.RegisterCreatedObjectUndo(marker,"Добавить маркер");
            return marker.transform;
        }

        static void Record(Object target)
        {
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static void BakeVendorMeshes(Transform cabinet)
        {
            int meshIndex=0,materialIndex=0;
            var saved=new Dictionary<Material,Material>();
            Material Keep(Material material)
            {
                if(!material || AssetDatabase.Contains(material))return material;
                if(!saved.TryGetValue(material,out var asset))saved[material]=asset=SaveMaterial("Vendor material "+(++materialIndex),material);
                return asset;
            }
            foreach(var filter in cabinet.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh && !filter.GetComponent<TMP_Text>())filter.sharedMesh=Asset("Vendor "+(++meshIndex)+" "+filter.name,Object.Instantiate(filter.sharedMesh));
            foreach(var renderer in cabinet.GetComponentsInChildren<Renderer>(true))
                if(!renderer.GetComponent<TMP_SubMesh>() && !renderer.GetComponent<TMP_Text>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(Keep).ToArray();
        }

        static void BakeEquipment()
        {
            const string profilePath="Assets/_Game/Systems/Resources/ExpeditionEquipment.asset";
            if(AssetDatabase.LoadAssetAtPath<ExpeditionEquipmentArt>(profilePath))return;
            var profile=ScriptableObject.CreateInstance<ExpeditionEquipmentArt>();
            var catalogue=TechCatalog.Items.Select(item=>item.id).Append("basket").Distinct();
            profile.models=catalogue.Select(id=>
            {
                var parts=TechRigShell.Design(id);
                Mesh Part(int i,string label)=>i<parts.Length && parts[i]?Asset(id+" "+label,parts[i]):null;
                return new ExpeditionEquipmentArt.Model{id=id,body=Part(0,"body"),movablePart=Part(1,"moving"),
                    signal=Part(2,"signal"),captive=Part(3,"catch"),eelCaptive=Part(4,"eel catch")};
            }).ToArray();
            profile.materials=TechRigShell.ArtPalette.Select((material,i)=>AssetDatabase.Contains(material)?material:
                SaveMaterial("Equipment material "+(i+1),material)).ToArray();
            AssetDatabase.CreateAsset(profile,profilePath);
        }

        static Material SaveMaterial(string name,Material material)
        {
            var clone=new Material(material);
            if(clone.HasProperty("_BaseMap") && clone.mainTexture is Texture2D texture && !AssetDatabase.Contains(texture))
                clone.mainTexture=Asset("Equipment paint detail",Object.Instantiate(texture));
            return Asset(name,clone);
        }

        static T Asset<T>(string name,T asset) where T:Object
        {
            string safe=string.Concat(name.Select(ch=>char.IsLetterOrDigit(ch) || ch==' ' || ch=='-'?ch:'_'));
            string path=Models+"/"+safe+(asset is Material?".mat":".asset");
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing){Object.DestroyImmediate(asset);return existing;}
            AssetDatabase.CreateAsset(asset,path);
            return asset;
        }
    }
}
