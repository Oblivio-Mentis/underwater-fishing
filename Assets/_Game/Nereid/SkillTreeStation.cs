using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Raid.Nereid
{
    internal sealed class SkillTreeStation : MonoBehaviour
    {
        ShipConsole console;
        TMP_Text idle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
            Install(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }

        static void Install(Scene scene,LoadSceneMode mode)
        {
            var vessel=NereidVessel.Active;
            if(vessel==null || vessel.gameObject.scene!=scene || vessel.DeckExtension==null ||
                vessel.GetComponentInChildren<NereidShipLayout>()!=null || vessel.GetComponentInChildren<SkillTreeStation>()!=null)return;

            var station=new GameObject("SkillTreeConsole");
            station.SetActive(false);
            station.transform.SetParent(vessel.transform,false);
            station.transform.localPosition=NereidDeckExtension.SkillStation;
            station.transform.localRotation=Quaternion.Euler(0,90,0);
            Vector3 scale=vessel.transform.lossyScale;
            station.transform.localScale=new Vector3(1/scale.z,1/scale.y,1/scale.x);

            var view=station.AddComponent<SkillTreeStation>();
            view.console=station.AddComponent<ShipConsole>();
            view.console.kind=ShipConsoleKind.SkillTree;
            view.console.screenAnchor=NereidDeckExtension.Node(station.transform,"ScreenAnchor",Vector3.zero);
            vessel.DeckExtension.FitMonitor(view.console);
            view.Label();
            station.SetActive(true);
        }

        void Label()
        {
            if(UI.UiRoot.Instance==null)return;
            var sign=NereidDeckExtension.Node(console.screenAnchor,"Skill computer idle display",Vector3.back*.017f);
            idle=sign.gameObject.AddComponent<TextMeshPro>();
            idle.font=UI.UiRoot.Instance.font;
            idle.alignment=TextAlignmentOptions.Center;
            idle.rectTransform.sizeDelta=NereidDeckExtension.ScreenSize*.89f;
            idle.textWrappingMode=TextWrappingModes.NoWrap;
            idle.fontSize=1.23f;
            idle.color=UI.UiTheme.Cyan;
            idle.text="NEREID\n\nМОДУЛИ ЭКИПАЖА";
        }

        void LateUpdate()
        {
            if(idle==null)return;
            var ui=UI.UiRoot.Instance;
            bool interactive=ui!=null && ui.Page==UI.UiPage.SkillTree && ui.Context.SelectedConsole==console;
            if(idle.gameObject.activeSelf==interactive)idle.gameObject.SetActive(!interactive);
        }
    }
}
