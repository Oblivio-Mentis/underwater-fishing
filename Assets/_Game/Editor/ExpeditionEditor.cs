using System.Linq;
using Raid.Factory;
using Raid.Nereid;
using Raid.Nereid.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Raid.Editor
{
    [InitializeOnLoad]
    public sealed class ExpeditionEditor : EditorWindow
    {
        static readonly (string label,string filter,string folder)[] Profiles={
            ("Модели изобретений", "t:ExpeditionEquipmentArt", "Assets/_Game"),
            ("Мир", "t:WorldTuning", "Assets/_Game"),
            ("Водолаз", "t:DiverTuning", "Assets/_Game"),
            ("Улов и гарпун", "t:FishingTuning", "Assets/_Game"),
            ("Страховочный трос", "t:FriendTetherTuning", "Assets/_Game"),
            ("Рывки рыбы на леске", "t:FishFightProfile", "Assets/_Game"),
            ("Состояния рыбы", "t:FishFsmProfile", "Assets/_Game"),
            ("Популяции рыб", "t:FishHabitat", "Assets/_Game"),
            ("Виды рыб", "t:FishSpeciesProfile", "Assets/_Game"),
            ("Угрозы", "t:ThreatTuning", "Assets/_Game"),
            ("Удильщик", "t:PredatorProfile", "Assets/_Game"),
            ("Прогрессия", "t:ProgressionTuning", "Assets/_Game"),
            ("Дерево прокачки", "RadialProgressionCatalog", "Assets/_Game"),
            ("Вода и видимость", "t:NereidWaterTuning", "Assets/_Game"),
            ("Цвет воды", "t:UnderwaterGrading", "Assets/_Game")
        };

        static ExpeditionEditor()=>UnityEditor.Editor.finishedDefaultHeaderGUI+=DrawHeader;
        GameObject selected;
        Object profile;
        UnityEditor.Editor profileEditor;
        readonly System.Collections.Generic.Dictionary<string,Object[]> assetGroups=new();
        int page,depth;
        Vector2 scroll;
        static readonly string[] Pages={"Объект","Спавн","Параметры"};
        static readonly (string field,string label)[] SpawnFields={
            ("anglerEnabled","Удильщик"),("raiderEnabled","Налётчик"),("clamEnabled","Моллюск"),
            ("eelEnabled","Угорь"),("krakenEnabled","Кракен"),("raiderArrival","Прибытие налётчика"),
            ("clamSite","Место моллюска"),("krakenSite","Место кракена"),
            ("anglerTerritories","Зоны удильщика"),("eelPatrol","Патруль угря")
        };

        [MenuItem("RAID/Редактор экспедиции",false,0)]
        public static void Open()=>Open(Selection.activeGameObject);

        internal static void Open(GameObject obj)
        {
            var window=GetWindow<ExpeditionEditor>("Экспедиция");
            window.selected=obj;window.page=0;window.Focus();
        }

        static void DrawHeader(UnityEditor.Editor inspector)
        {
            if(inspector.targets.Length!=1 || inspector.target is not GameObject go || !go.scene.IsValid())return;
            if(GUILayout.Button("Редактировать",EditorStyles.miniButton))
            {
                if(!OpenSpecialized(go))Open(go);
            }
        }

        internal static bool OpenSpecialized(GameObject go)
        {
            var pipe=go.GetComponentInParent<NereidPipeRoute>(true);
            if(!pipe && go.TryGetComponent<PipeSocket>(out var socket))
                pipe=InScene<NereidPipeRoute>(go.scene).FirstOrDefault(x=>x.source==socket || x.destination==socket);
            if(pipe){PipeRouteEditor.Open(pipe);return true;}
            var cargo=go.GetComponentInParent<NereidCargoRail>(true);
            if(!cargo)
                cargo=InScene<NereidCargoRail>(go.scene).FirstOrDefault(x=>
                    (x.hatch && go.transform.IsChildOf(x.hatch.transform)) ||
                    (x.stops!=null && x.stops.Any(stop=>stop.marker && go.transform.IsChildOf(stop.marker))));
            if(cargo){CargoRailEditor.Open(cargo);return true;}
            return false;
        }

        internal static T[] InScene<T>(Scene scene) where T:Component =>
            scene.IsValid() && scene.isLoaded
                ?scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<T>(true)).ToArray()
                :System.Array.Empty<T>();

        void OnSelectionChange()
        {
            if(Selection.activeGameObject)selected=Selection.activeGameObject;
            Repaint();
        }

        void OnDisable(){if(profileEditor)DestroyImmediate(profileEditor);}
        void OnProjectChange(){assetGroups.Clear();Repaint();}

        void OnGUI()
        {
            var scene=selected?selected.scene:SceneManager.GetActiveScene();
            var world=InScene<RaidWorld>(scene).FirstOrDefault();
            var layout=world?world.expeditionLayout:null;
            using(new EditorGUILayout.HorizontalScope())
            {
                page=GUILayout.Toolbar(page,Pages);
                using(new EditorGUI.DisabledScope(Application.isPlaying || !scene.IsValid() || string.IsNullOrEmpty(scene.path)))
                    if(GUILayout.Button("Сохранить сцену",GUILayout.Width(130)))
                        EditorSceneManager.SaveScene(scene);
            }
            if(!world && GUILayout.Button("Открыть игровую сцену") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ExpeditionSceneAuthoring.ScenePath);
                selected=null;
                GUIUtility.ExitGUI();
            }
            if(world && !layout && !Application.isPlaying && GUILayout.Button("Разместить редактируемые объекты экспедиции"))
                layout=ExpeditionSceneAuthoring.Install(scene);

            using(var view=new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll=view.scrollPosition;
                if(page==0)DrawObjects(scene,layout);
                if(page==1)DrawSpawns(layout);
                if(page==2)DrawProfiles(world);
            }
        }

        void DrawObjects(Scene scene,ExpeditionLayout layout)
        {
            selected=(GameObject)EditorGUILayout.ObjectField("Объект",selected,typeof(GameObject),true);
            if(selected)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Выбрать в Hierarchy"))Selection.activeGameObject=selected;
                    if(GUILayout.Button("В кадр в Scene"))
                    {
                        Selection.activeGameObject=selected;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
                if(GUILayout.Button("Открыть редактор объекта"))
                {
                    if(!OpenSpecialized(selected))EditorUtility.OpenPropertyEditor(selected);
                }
            }

            EditorGUILayout.Space();
            foreach(var route in InScene<NereidPipeRoute>(scene))
                if(GUILayout.Button("Редактировать трубу · "+route.name))PipeRouteEditor.Open(route);
            foreach(var rail in InScene<NereidCargoRail>(scene))
                if(GUILayout.Button("Редактировать кран, клеть и люк"))CargoRailEditor.Open(rail);
            if(layout)
            {
                Pick("Раздатчик технологий",layout.dispenser);
                Pick("Домик Гоши и места укладки",layout.goshaDen);
                if(layout.goshaModel && GUILayout.Button("Редактировать модель Гоши"))
                    AssetDatabase.OpenAsset(layout.goshaModel.gameObject);
                Pick("Подход Гоши к домику",layout.goshaApproach);
                Pick("Пасть кракена · плоскость попадания",layout.chargeAperture);
                Pick("Пасть кракена · направление пламени",layout.flameOutlet);
                Pick("Все ссылки и параметры размещения",layout);
            }
            Pick("Оборудование переработки и кормления",InScene<FactoryVisuals>(scene).FirstOrDefault());
            Pick("Навигация корабля и точки экипажа",InScene<NereidDeckNavigation>(scene).FirstOrDefault());
            Pick("Шлюз и уровни воды",InScene<NereidPressureDoors>(scene).FirstOrDefault());
            Pick("Палитра и шрифт интерфейса",InScene<Raid.UI.UiRoot>(scene).FirstOrDefault());
        }

        void DrawSpawns(ExpeditionLayout layout)
        {
            if(!layout)return;
            depth=GUILayout.Toolbar(depth,new[]{"Мелководье","Разлом"});
            var zones=layout.ForDepth(depth+1);
            var data=new SerializedObject(layout);
            var map=data.FindProperty(depth==0?"shallows":"rift");
            EditorGUILayout.HelpBox("Выберите маркер и двигайте его инструментом Move. Моллюск и кракен привязываются к дну. Точка удильщика и патруль угря задают центр области прибытия.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                foreach(var (field,label) in SpawnFields)
                    EditorGUILayout.PropertyField(map.FindPropertyRelative(field),new GUIContent(label),true);
                EditorGUILayout.PropertyField(data.FindProperty("goshaJunkSites"),new GUIContent("Места находок Гоши"),true);
                EditorGUILayout.PropertyField(data.FindProperty("crabDens"),new GUIContent("Укрытия крабов"),true);
                data.ApplyModifiedProperties();
            }
            using(new EditorGUILayout.HorizontalScope())
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if(GUILayout.Button("+ Зона удильщика"))
                    zones.anglerTerritories=AddSpawn(layout,zones.anglerTerritories,"Удильщик",zones.raiderArrival);
                if(zones.eelEnabled && GUILayout.Button("+ Участок патруля угря"))
                    zones.eelPatrol=AddSpawn(layout,zones.eelPatrol,"Угорь",zones.krakenSite?zones.krakenSite:zones.clamSite);
            }
            using(new EditorGUILayout.HorizontalScope())
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if(zones.krakenEnabled && !zones.krakenSite && GUILayout.Button("+ Место кракена"))
                    zones.krakenSite=AddSpawn(layout,null,"Кракен",zones.clamSite)[0];
                if(GUILayout.Button("+ Укрытие крабов"))
                    layout.crabDens=AddSpawn(layout,layout.crabDens,"Укрытие крабов",zones.clamSite);
                if(GUILayout.Button("+ Место находки"))
                    layout.goshaJunkSites=AddSpawn(layout,layout.goshaJunkSites,"Находка Гоши",zones.clamSite);
            }
            Pick("Налётчик",zones.raiderArrival);
            Pick("Моллюск",zones.clamSite);
            Pick("Кракен",zones.krakenSite);
            foreach(var marker in zones.anglerTerritories??System.Array.Empty<Transform>())if(marker)Pick(marker.name,marker);
            foreach(var marker in zones.eelPatrol??System.Array.Empty<Transform>())if(marker)Pick(marker.name,marker);
            if(GUILayout.Button("Настроить зоны и численность рыб"))
            {page=2;profile=FindAssets("t:FishHabitat","Assets/_Game").FirstOrDefault();}
        }

        static Transform[] AddSpawn(ExpeditionLayout layout,Transform[] existing,string label,Transform fallback)
        {
            var list=existing??System.Array.Empty<Transform>();
            var last=list.LastOrDefault(x=>x)??fallback;
            Transform parent=last?last.parent:layout.transform;
            Vector3 position=last?last.position+Vector3.right*2:layout.transform.position;
            Undo.RecordObject(layout,"Добавить точку спавна");
            var marker=ExpeditionSceneAuthoring.Marker(parent,label+" "+(list.Length+1),position);
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Selection.activeGameObject=marker.gameObject;
            return list.Append(marker).ToArray();
        }

        void DrawProfiles(RaidWorld world)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                if(world && world.goshaHunt && GUILayout.Button("Гоша"))profile=world.goshaHunt;
                if(GUILayout.Button("Технологии")) ExpeditionDataEditor.Open("catalog");
                if(GUILayout.Button("Этапы")) ExpeditionDataEditor.Open("stages");
                if(GUILayout.Button("Промысел")) ExpeditionDataEditor.Open("active");
            }
            foreach(var entry in Profiles)
            {
                var assets=FindAssets(entry.filter,entry.folder);
                if(assets.Length==0)continue;
                using(new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(entry.label,GUILayout.Width(142));
                    if(assets.Length==1)
                    {
                        if(GUILayout.Button(assets[0].name))profile=assets[0];
                    }
                    else if(GUILayout.Button(profile && assets.Contains(profile)?profile.name+" ▾":"Выбрать профиль ▾"))
                    {
                        var menu=new GenericMenu();
                        foreach(var asset in assets)
                        {
                            var captured=asset;
                            menu.AddItem(new GUIContent(asset.name),asset==profile,()=>{profile=captured;Repaint();});
                        }
                        menu.ShowAsContext();
                    }
                }
            }
            if(!profile)return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(profile.name,EditorStyles.boldLabel);
            UnityEditor.Editor.CreateCachedEditor(profile,null,ref profileEditor);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
                if(profileEditor)profileEditor.OnInspectorGUI();
            if(GUILayout.Button("Сохранить параметры"))AssetDatabase.SaveAssetIfDirty(profile);
        }

        Object[] FindAssets(string filter,string folder)
        {
            if(!assetGroups.TryGetValue(filter,out var assets))
                assetGroups[filter]=assets=AssetDatabase.FindAssets(filter,new[]{folder})
                    .Select(guid=>AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(asset=>asset is ScriptableObject).ToArray();
            return assets;
        }

        static void Pick(string label, Object target)
        {
            if(!target)return;
            if(GUILayout.Button(label))
            {
                Selection.activeObject=target is Component component?component.gameObject:target;
                if(target is Component && SceneView.lastActiveSceneView is SceneView view)view.FrameSelected();
            }
        }
    }

    static class ExpeditionSpawnGizmos
    {
        [DrawGizmo(GizmoType.Selected|GizmoType.InSelectionHierarchy)]
        static void Draw(ExpeditionLayout layout,GizmoType type)
        {
            if(Application.isPlaying)return;
            foreach(var area in new[]{layout.shallows,layout.rift})
            {
                DrawMarker(area.raiderArrival,new Color(1,.4f,.28f));
                DrawMarker(area.clamSite,new Color(.9f,.77f,.4f));
                DrawMarker(area.krakenSite,new Color(.95f,.45f,.7f));
                foreach(var point in area.anglerTerritories??System.Array.Empty<Transform>())DrawMarker(point,Color.cyan);
                var patrol=(area.eelPatrol??System.Array.Empty<Transform>()).Where(x=>x).ToArray();
                if(patrol.Length>1)
                    for(int i=0;i<patrol.Length;i++)
                    {
                        DrawMarker(patrol[i],Color.green);
                        Gizmos.DrawLine(patrol[i].position,patrol[(i+1)%patrol.Length].position);
                    }
            }
        }

        static void DrawMarker(Transform mark,Color tint)
        {
            if(!mark)return;
            Gizmos.color=tint;
            Gizmos.DrawWireSphere(mark.position,1);
            Handles.Label(mark.position+Vector3.up,mark.name);
        }
    }

    [CustomEditor(typeof(FishHabitat))]
    sealed class HabitatSceneEditor : UnityEditor.Editor
    {
        int zone;
        void OnEnable()=>SceneView.duringSceneGui+=DrawHabitat;
        void OnDisable()=>SceneView.duringSceneGui-=DrawHabitat;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var habitat=(FishHabitat)target;
            if(habitat.zones==null || habitat.zones.Length==0)return;
            zone=EditorGUILayout.Popup("Зона в Scene",Mathf.Min(zone,habitat.zones.Length-1),
                habitat.zones.Select(x=>x?.name??"Пустая зона").ToArray());
            if(GUILayout.Button("Показать зону в Scene") && habitat.zones[zone]?.anchors is {Length:>0} points)
                SceneView.lastActiveSceneView?.LookAt(points[0],Quaternion.Euler(55,0,0),30,true);
        }
        void DrawHabitat(SceneView view)
        {
            var habitat=target as FishHabitat;
            if(!habitat || habitat.zones==null || zone>=habitat.zones.Length || Application.isPlaying)return;
            var area=habitat.zones[zone];
            if(area?.anchors==null)return;
            Handles.color=new Color(.25f,.9f,1);
            if(area.anchors.Length>1)Handles.DrawAAPolyLine(2,area.anchors);
            for(int i=0;i<area.anchors.Length;i++)
            {
                var point=area.anchors[i];
                Handles.Label(point,area.name+" "+(i+1));
                EditorGUI.BeginChangeCheck();
                var moved=Handles.PositionHandle(point,Quaternion.identity);
                if(!EditorGUI.EndChangeCheck())continue;
                Undo.RecordObject(habitat,"Переместить точку популяции");
                area.anchors[i]=moved;EditorUtility.SetDirty(habitat);Repaint();
            }
        }
    }
}
