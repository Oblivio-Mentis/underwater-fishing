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
            Bind("UpgradeConsole",ShipConsoleKind.Upgrades);Bind("DepthConsole",ShipConsoleKind.Depths);
            var factory=Object.FindAnyObjectByType<Raid.Factory.FactoryVisuals>();
            if(factory!=null && factory.GetComponent<Raid.Factory.FishRoutePresentation>()==null)factory.gameObject.AddComponent<Raid.Factory.FishRoutePresentation>();
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
                bool computer=kind==ShipConsoleKind.Upgrades||kind==ShipConsoleKind.Depths;
                screenHit.size=computer?new Vector3(2.42f,1.77f,.025f):new Vector3(1.02f,.64f,.025f);
            }
            if(go.GetComponentInChildren<Collider>()==null)
            {
                var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.1f,0);collider.size=new Vector3(.75f,1.5f,.65f);
            }
            EditorUtility.SetDirty(station);
        }
    }
}
