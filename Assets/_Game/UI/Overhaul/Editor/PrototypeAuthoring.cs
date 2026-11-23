using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Raid.UI.Editor
{
    public static class PrototypeAuthoring
    {
        [MenuItem("RAID/011/Install complete physical prototype")]
        public static void Install()
        {
            UiAuthoring.Install();FactoryAuthoring.Install();BindConsoles();
        }
        public static void BindConsoles()
        {
            if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=UiAuthoring.ScenePath)
                throw new System.InvalidOperationException("Bindings require the stopped InterfaceVerticalSlice scene");
            Bind("FishRoute_Process",ShipConsoleKind.Process);Bind("FishRoute_Feed",ShipConsoleKind.Feed);
            Bind("UpgradeConsole",ShipConsoleKind.Workshop);Bind("DepthConsole",ShipConsoleKind.Depths);
            AddReservoirMonitor();Bind("ReservoirConsole",ShipConsoleKind.Reservoir);
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();
            if(factory!=null && factory.GetComponent<Raid.Factory.FishRoutePresentation>()==null)factory.gameObject.AddComponent<Raid.Factory.FishRoutePresentation>();
            if(factory!=null)
            {
                var weapon=factory.GetComponent<HarpoonPresentation>();if(weapon==null)weapon=factory.gameObject.AddComponent<HarpoonPresentation>();
                const string mats="Assets/_Game/Factory/Generated/Materials/";
                weapon.steel=AssetDatabase.LoadAssetAtPath<Material>(mats+"Machined_stainless_trim.mat");
                weapon.amber=AssetDatabase.LoadAssetAtPath<Material>(mats+"Enamel_safety_ochre.mat");
                weapon.dark=AssetDatabase.LoadAssetAtPath<Material>(mats+"Machine_dark_steel.mat");
                weapon.cyan=AssetDatabase.LoadAssetAtPath<Material>(mats+"Inspection_cyan_emission.mat");
                var creature=factory.GetComponent<ThreatPresentation>();if(creature==null)creature=factory.gameObject.AddComponent<ThreatPresentation>();
                creature.steel=weapon.steel;creature.dark=weapon.dark;creature.cyan=weapon.cyan;creature.amber=weapon.amber;
                creature.skin=AssetDatabase.LoadAssetAtPath<Material>(mats+"Living_mouth_skin.mat");
                creature.tooth=AssetDatabase.LoadAssetAtPath<Material>(mats+"Mouth_ivory_teeth.mat");
                var threats=Object.FindAnyObjectByType<ThreatDirector>();
                if(threats!=null)threats.factoryWaveRoute=new[]{new Vector3(9,1.5f,-11.05f),new Vector3(0,1.5f,-11.05f),new Vector3(0,1.5f,-6),new Vector3(0,1.5f,-4.8f)};
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        static void Bind(string name,ShipConsoleKind kind)
        {
            var go=GameObject.Find(name);if(go==null)throw new System.InvalidOperationException("Missing physical console "+name);
            var station=go.GetComponent<ShipConsole>();if(station==null)station=go.AddComponent<ShipConsole>();
            station.kind=kind;station.screenAnchor=go.transform.Find("ScreenAnchor");
            if(station.screenAnchor!=null && station.screenAnchor.GetComponent<Collider>()==null)
            {
                var screenHit=station.screenAnchor.gameObject.AddComponent<BoxCollider>();
                bool computer=kind==ShipConsoleKind.Workshop||kind==ShipConsoleKind.Depths||kind==ShipConsoleKind.Reservoir;
                screenHit.size=computer?new Vector3(station.screenScale*1250,station.screenScale*860,.025f):new Vector3(1.02f,.64f,.025f);
            }
            if(go.GetComponentInChildren<Collider>()==null)
            {
                var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.1f,0);collider.size=new Vector3(.75f,1.5f,.65f);
            }
            EditorUtility.SetDirty(station);
        }
        static void AddReservoirMonitor()
        {
            if(GameObject.Find("ReservoirConsole")!=null)return;
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();if(factory==null)return;
            var root=new GameObject("ReservoirConsole");root.transform.SetParent(factory.transform,false);
            root.transform.localPosition=new Vector3(0,0,-1.25f);root.transform.localRotation=Quaternion.Euler(0,180,0);
            var station=root.AddComponent<ShipConsole>();station.screenScale=.001f;
            var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Reservoir inspection screen housing";backing.transform.SetParent(root.transform,false);
            backing.transform.localPosition=new Vector3(0,1.22f,0);backing.transform.localScale=new Vector3(1.36f,1,.13f);
            backing.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Factory/Generated/Materials/Machine_dark_steel.mat");
            var anchor=new GameObject("ScreenAnchor").transform;anchor.SetParent(root.transform,false);anchor.localPosition=new Vector3(0,1.22f,-.08f);
        }
    }
}
