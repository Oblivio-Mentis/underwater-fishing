using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Raid
{
    public sealed class RaidStatusHud : MonoBehaviour
    {
        private RaidWorld world;
        private Canvas canvas;
        private Button terminal;
        private void Start()
        {
            world=GetComponent<RaidWorld>();
            var root=new GameObject("RAID HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false); canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            root.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1280,720);
            var button=new GameObject("Reservoir terminal",typeof(RectTransform),typeof(Image),typeof(Button));
            button.transform.SetParent(root.transform,false); terminal=button.GetComponent<Button>();
            terminal.targetGraphic=button.GetComponent<Image>(); button.GetComponent<Image>().color=new Color(.06f,.23f,.26f,.95f);
            var rect=button.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=new Vector2(0,18);rect.sizeDelta=new Vector2(240,34);
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(button.transform,false);
            var labelRect=label.GetComponent<RectTransform>();labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=17;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.text="Терминал резервуара [E]";text.raycastTarget=false;
            terminal.onClick.AddListener(world.ToggleTerminal);
            if(FindAnyObjectByType<EventSystem>()==null)
            {
                var events=new GameObject("UI Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        private void Update()
        {
            if(canvas==null) return;
            canvas.enabled=world!=null && world.IsClientInitialized && world.CanUseTerminal;
            terminal.interactable=Cursor.lockState!=CursorLockMode.Locked;
        }
    }
}
