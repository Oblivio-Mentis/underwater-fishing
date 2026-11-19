using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace Raid.UI
{
    [DefaultExecutionOrder(-10000)]
    public sealed class UiRoot : MonoBehaviour,IUiController
    {
        public static UiRoot Instance {get;private set;}
        public TMP_FontAsset font;
        public UiContext Context {get;}=new UiContext();
        public UiSettings Settings {get;private set;}
        public UiPage Page {get;private set;}=UiPage.Main;
        public bool CommandPending {get;private set;}
        public string LastCommandReason {get;private set;}="";
        public bool LastCommandSucceeded {get;private set;}
        CanvasScaler scaler;
        Canvas canvas;
        bool computerSurface;
        Camera viewCamera;
        float desiredFov=80;
        InputSystemUIInputModule inputModule;
        RectTransform canvasRect,shade,confirmation;
        TMP_Text toastText,pendingText;
        Image toastPanel;
        MenuView menu;
        HudView hud;
        TerminalView terminal;
        CargoView cargo;
        CanvasGroup[] modalGroups;
        GameObject cancelConfirmationButton;
        UiPage settingsBack=UiPage.Main;
        UiPage crewBack=UiPage.Pause;
        Action confirmAction;
        RaidWorld subscribedWorld;
        bool connectedBefore,overlayBefore,replyReceived,wasPulled,wasDead;
        float nextRefresh,toastUntil,pendingSince,replyAt,saveSettingsAt=-1;
        [SerializeField,HideInInspector] float initialVolume=1;
        WorldAction pendingAction;
        long pendingId;
        int requestSequence,pendingRequestId;
        string beforeFingerprint;

        void Awake()
        {
            if(Instance!=null){Destroy(gameObject);return;}Instance=this;UiInputGate.Reset();UiInputGate.Active=true;
            Settings=UiSettings.Load();UiKit.Font=font;initialVolume=AudioListener.volume;
            gameObject.AddComponent<UnderwaterLook>();
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;gameObject.AddComponent<GraphicRaycaster>();canvasRect=(RectTransform)transform;
            var systems=UnityEngine.Object.FindObjectsByType<EventSystem>();
            EventSystem events=systems.Length>0?systems[0]:new GameObject("RAID011 EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            foreach(var system in systems)if(system!=events)system.enabled=false;
            foreach(var module in events.GetComponents<BaseInputModule>())if(!(module is InputSystemUIInputModule))module.enabled=false;
            inputModule=events.GetComponent<InputSystemUIInputModule>();if(inputModule==null)inputModule=events.gameObject.AddComponent<InputSystemUIInputModule>();
            events.sendNavigationEvents=true;
            hud=new HudView(transform,this);
            var dim=UiKit.Panel(transform,"Menu shade",new Color(.015f,.035f,.045f,.82f),true);shade=dim.rectTransform;UiKit.Fill(shade);
            menu=new MenuView(transform,this);terminal=new TerminalView(transform,this);cargo=new CargoView(transform,this);
            modalGroups=new[]{menu.Root.gameObject.AddComponent<CanvasGroup>(),terminal.Root.gameObject.AddComponent<CanvasGroup>(),cargo.Root.gameObject.AddComponent<CanvasGroup>()};
            MakeConfirmation();
            toastPanel=UiKit.Panel(transform,"System notification",UiTheme.Panel);UiKit.Place(toastPanel.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-4),new Vector2(1080,38));
            toastText=UiKit.Label(toastPanel.transform,"Message","",20);UiKit.Fill(toastText.rectTransform,4);toastText.alignment=TextAlignmentOptions.Center;
            toastText.enableAutoSizing=true;toastText.fontSizeMin=16;toastText.fontSizeMax=20;toastText.textWrappingMode=TextWrappingModes.NoWrap;toastText.overflowMode=TextOverflowModes.Ellipsis;
            pendingText=UiKit.Label(transform,"Server acknowledgement","",20,UiTheme.Cyan);UiKit.Place(pendingText.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(750,34));pendingText.alignment=TextAlignmentOptions.Center;
            ApplySettings();ShowPage(UiPage.Main);
        }
        void MakeConfirmation()
        {
            var dark=UiKit.Panel(transform,"Confirm veil",new Color(0,0,0,.68f),true);UiKit.Fill(dark.rectTransform);confirmation=dark.rectTransform;
            var surface=UiKit.Frame(confirmation,"Confirmation",780,620);var column=UiKit.Column(surface,"Content",18,28);UiKit.Fill(column);
            UiKit.Size(UiKit.Label(column,"Tag",UiText.ModalTag,18,UiTheme.Cyan),28);
            UiKit.Size(UiKit.Label(column,"Title","",30),48);UiKit.Size(UiKit.Label(column,"Description","",23),260);
            var row=UiKit.Row(column,"Actions");UiKit.Size(row,56);
            cancelConfirmationButton=UiKit.Button(row,"Confirm cancel",UiText.Cancel,CancelConfirmation).gameObject;
            UiKit.Button(row,"Confirm accept",UiText.Confirm,()=>{var action=confirmAction;CancelConfirmation();action?.Invoke();},true);
            confirmation.gameObject.SetActive(false);
        }
        public void Confirm(string title,string description,Action confirmed)
        {
            if(CommandPending)return;
            confirmAction=confirmed;confirmation.gameObject.SetActive(true);
            foreach(var group in modalGroups)group.interactable=false;
            var labels=confirmation.GetComponentsInChildren<TMP_Text>();foreach(var label in labels){if(label.name=="Title")label.text=title;if(label.name=="Description")label.text=description;}
            SetCursor();EventSystem.current?.SetSelectedGameObject(cancelConfirmationButton);
        }
        void CancelConfirmation() { confirmAction=null;confirmation.gameObject.SetActive(false);foreach(var group in modalGroups)group.interactable=true;if(Page==UiPage.None)Context.SelectedConsole=null;UiInputGate.ResumeAt=Time.unscaledTime+.15f;SetCursor(); }
        public void ShowPage(UiPage value)
        {
            if(value==UiPage.Settings && Page!=UiPage.Settings)settingsBack=Page;
            if(value==UiPage.Crew && Page!=UiPage.Crew)crewBack=Page==UiPage.None?UiPage.Pause:Page;
            if(!Context.Connected && value==UiPage.None)value=UiPage.Main;
            Page=value;
            if(value!=UiPage.Terminal)Context.SelectedConsole=null;
            ConfigureSurface(value);
            if(toastPanel!=null)UiKit.Place(toastPanel.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,value==UiPage.None?-270:-4),new Vector2(value==UiPage.None?760:1080,38));
            bool menuPage=value==UiPage.Main||value==UiPage.Connection||value==UiPage.Crew||value==UiPage.Pause||value==UiPage.Settings;
            menu.Root.gameObject.SetActive(menuPage);if(menuPage)menu.Open(value);
            terminal.Root.gameObject.SetActive(value==UiPage.Terminal);cargo.Root.gameObject.SetActive(value==UiPage.Cargo);
            shade.gameObject.SetActive(value!=UiPage.None&&!computerSurface);hud.Root.gameObject.SetActive(Context.Connected && value==UiPage.None);
            UiInputGate.ResumeAt=Time.unscaledTime+.15f;SetCursor();EventSystem.current?.SetSelectedGameObject(null);
            if(Context.Connected && value==UiPage.Terminal)terminal.Refresh();if(Context.Connected&&value==UiPage.Cargo)cargo.Refresh();
            Canvas.ForceUpdateCanvases();
        }
        void ConfigureSurface(UiPage page)
        {
            var station=Context.SelectedConsole;
            computerSurface=page==UiPage.Terminal && station!=null && station.screenAnchor!=null;
            if(computerSurface)
            {
                canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Context.Player.GetComponentInChildren<Camera>();
                canvasRect.sizeDelta=new Vector2(1920,1080);
                canvasRect.position=station.screenAnchor.position;canvasRect.rotation=station.screenAnchor.rotation;
                canvasRect.localScale=Vector3.one*station.screenScale;
                Context.Player.LookAtConsole(station.screenAnchor.position);
            }
            else
            {
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
                canvasRect.localScale=Vector3.one;canvasRect.localRotation=Quaternion.identity;
            }
        }
        public void ClosePanel()
        {
            if(confirmation.gameObject.activeSelf){CancelConfirmation();return;}
            ShowPage(Page==UiPage.Settings?settingsBack:Page==UiPage.Crew?crewBack:Context.Connected?UiPage.None:UiPage.Main);
        }
        void SetCursor()
        {
            bool overlay=Context.Session!=null&&Context.Session.lobby!=null&&Context.Session.lobby.OverlayActive;
            if(inputModule!=null)inputModule.enabled=Application.isFocused&&!overlay;
            UiInputGate.Modal=Page!=UiPage.None||confirmation.gameObject.activeSelf||overlay;
            bool locked=Context.Connected && !UiInputGate.Modal && Application.isFocused;
            Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;
        }
        void Update()
        {
            if(menu==null)return;
            bool overlay=Context.Session!=null&&Context.Session.lobby!=null&&Context.Session.lobby.OverlayActive;
            if(overlay!=overlayBefore){overlayBefore=overlay;if(Context.Connected && Page==UiPage.None)ShowPage(UiPage.Pause);SetCursor();}
            if(Application.isFocused && !overlayBefore && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if(confirmation.gameObject.activeSelf)CancelConfirmation();
                else if(Page==UiPage.None)ShowPage(UiPage.Pause);
                else ClosePanel();
            }
            if(!UiInputGate.Blocked && Context.Connected && Page==UiPage.None && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)Interact();
            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.1f;Context.Refresh();
                if(subscribedWorld!=Context.World)
                {if(subscribedWorld!=null)subscribedWorld.CommandResponded-=OnCommandReply;subscribedWorld=Context.World;if(subscribedWorld!=null)subscribedWorld.CommandResponded+=OnCommandReply;}
                bool connected=Context.Connected;
                if(connected!=connectedBefore)
                {
                    connectedBefore=connected;CommandPending=false;
                    if(confirmation.gameObject.activeSelf)CancelConfirmation();ShowPage(connected?UiPage.None:UiPage.Main);
                    if(!connected)Toast(UiText.LostHost,true);
                }
                bool dead=connected && Context.Player.IsDead;
                if(dead && !wasDead)
                {
                    if(confirmation.gameObject.activeSelf)CancelConfirmation();
                    if(Page!=UiPage.None)ShowPage(UiPage.None);
                }
                wasDead=dead;
                UiInputGate.AllowAssist=Context.Interaction==UiInteraction.Assist;
                if(connected)
                {
                    if((Page==UiPage.Terminal && !Context.NearTerminal)||(Page==UiPage.Cargo && !Context.NearCargo))ShowPage(UiPage.None);
                    bool pulled=Context.Harpoon!=null&&Context.Harpoon.IsPulled;
                    if(pulled&&!wasPulled&&Page!=UiPage.None)Toast("Напарник тянет вас к себе. R — отцепиться");
                    wasPulled=pulled;
                    hud.Refresh();if(Page==UiPage.Terminal)terminal.Refresh();else if(Page==UiPage.Cargo)cargo.Refresh();
                    ApplyCameraSettings();
                    string worldMessage=Context.World.Message;
                    if((worldMessage.StartsWith("SAVE ERROR")||worldMessage.StartsWith("Подача остановлена:")) && worldMessage!=lastSaveMessage){lastSaveMessage=worldMessage;Toast(UiText.WorldMessage(worldMessage),true);}
                    else if(!worldMessage.StartsWith("SAVE ERROR")&&!worldMessage.StartsWith("Подача остановлена:"))lastSaveMessage="";
                }
                if(menu.Root.gameObject.activeSelf)menu.Refresh();UpdatePending();
                toastPanel.gameObject.SetActive(Time.unscaledTime<toastUntil);UiKit.Set(pendingText,CommandPending?UiText.WaitCommand:"");
            }
            if(saveSettingsAt>=0 && Time.unscaledTime>=saveSettingsAt){saveSettingsAt=-1;Settings.Save();}
        }
        string lastSaveMessage="";
        void Interact()
        {
            if(Context.Player.IsHelpless)return;
            switch(Context.Interaction)
            {
                case UiInteraction.Terminal:
                    UseConsole(Context.AimedConsole);
                    break;
                case UiInteraction.Cargo:
                    if(Context.View.hoistPhase!=HoistPhase.Loading)Toast(UiText.CargoLocked);
                    else if(Context.HasCargo && Context.View.CargoUsed+Context.CarriedSize<=Context.View.Capacity(Context.Tuning))Send(WorldAction.LoadCargo);
                    ShowPage(UiPage.Cargo);break;
                case UiInteraction.Loot: Send(WorldAction.Collect); break;
            }
        }
        public void UseConsole(ShipConsole station)
        {
            if(!Context.Connected || Context.Player.IsHelpless || CommandPending || confirmAction!=null || Context.World.TravelRemaining>0)return;
            if(station!=null&&!station.InRange(Context.Player)){Toast(UiText.NeedTerminal,true);return;}
            if(station==null&&ShipConsole.Stations.Count>0)return;
            Context.SelectedConsole=station;
            if(station!=null && (station.kind==ShipConsoleKind.Process || station.kind==ShipConsoleKind.Feed))ToggleFishRoute(station.kind==ShipConsoleKind.Feed);
            else
            {
                if(station!=null)Context.Player.TouchTerminal(station.kind);
                ShowPage(UiPage.Terminal);
                terminal.SelectTab(station==null||station.kind==ShipConsoleKind.Reservoir?"catch":station.kind==ShipConsoleKind.Depths?"depths":"upgrades");
            }
        }
        void ToggleFishRoute(bool feeding)
        {
            var view=Context.View;if(view==null)return;
            var route=Context.World.FishRoute;
            if(route.active)
            {
                if(route.feeding!=feeding){Toast("Другая линия занята");return;}
                if(route.stopping){Toast("Подача закрыта. Рыба в трубе завершает путь");return;}
                Send(feeding?WorldAction.StopFeeding:WorldAction.StopProcessing,route.sequence);return;
            }
            if(!feeding&&!Context.AtBase){Toast(UiText.ProcessAtBase);return;}
            var fish=view.tank.FindAll(x=>x.offering>0);if(fish.Count==0){Toast("В резервуаре нет рыбы");return;}
            long quoted=FishRouteQuote.Compute(fish,Context.FoodRequired);
            Send(feeding?WorldAction.FeedAll:WorldAction.ProcessAll,quoted);
        }
        public void Send(WorldAction action,long id=0)
        {
            if(!Context.Connected||Context.World==null){Toast(UiText.NotConnected,true);return;}if(CommandPending)return;
            pendingAction=action;pendingId=id;beforeFingerprint=Fingerprint();pendingSince=Time.unscaledTime;replyReceived=false;CommandPending=true;
            int expectedLevel=action>=WorldAction.Reel&&action<=WorldAction.Winch?Context.View.Level((Upgrade)((int)action-(int)WorldAction.Reel)):-1;
            pendingRequestId=++requestSequence;Context.World.RequestCommand(action,id,pendingRequestId,expectedLevel);
        }
        string Fingerprint()
        {
            var v=Context.View;if(v==null)return "";
            if(pendingAction>=WorldAction.Reel && pendingAction<=WorldAction.Winch)return v.Level((Upgrade)((int)pendingAction-(int)WorldAction.Reel)).ToString();
            if(pendingAction==WorldAction.Process||pendingAction==WorldAction.Feed)return v.tank.Exists(x=>x.id==pendingId).ToString();
            if(pendingAction==WorldAction.LoadCargo)return v.CargoUsed.ToString();if(pendingAction==WorldAction.Hoist)return v.hoistPhase.ToString();
            if(pendingAction==WorldAction.Collect)return Context.Loot.FindAll(x=>Context.Player!=null&&x.CarrierOwnerId==Context.Player.OwnerId).Count.ToString();
            if(pendingAction==WorldAction.ProcessAll||pendingAction==WorldAction.FeedAll)return Context.World.FishRoute.sequence.ToString();
            if(pendingAction==WorldAction.StopProcessing||pendingAction==WorldAction.StopFeeding)return Context.World.FishRoute.sequence+"/"+Context.World.FishRoute.stopping+"/"+Context.World.FishRoute.active;
            return Context.World.CurrentDepth+"/"+Context.AtBase+"/"+(Context.World.TravelRemaining>0);
        }
        void OnCommandReply(int requestId,bool success,string reason)
        {
            if(!CommandPending||requestId!=pendingRequestId)return;replyReceived=true;replyAt=Time.unscaledTime;LastCommandReason=reason;LastCommandSucceeded=success;
            if(!success)Toast(UiText.CommandReason(reason),true);
        }
        void UpdatePending()
        {
            if(!CommandPending)return;
            if(replyReceived && Time.unscaledTime-replyAt>=.3f && (!LastCommandSucceeded || Fingerprint()!=beforeFingerprint)){CommandPending=false;return;}
            if(Time.unscaledTime-pendingSince>5){CommandPending=false;Toast(replyReceived?UiText.SnapshotTimeout:UiText.CommandTimeout,true);}
        }
        public void Toast(string message,bool dangerous=false)
        {
            UiKit.Set(toastText,message);toastText.color=dangerous?UiTheme.Red:UiTheme.Text;toastPanel.gameObject.SetActive(true);toastUntil=Time.unscaledTime+4.5f;
        }
        public void ApplySettings()
        {
            AudioListener.volume=Settings.volume;scaler.referenceResolution=new Vector2(1920,1080)/Settings.scale;
            ApplyCameraSettings();saveSettingsAt=Time.unscaledTime+.4f;
        }
        void ApplyCameraSettings()
        {
            if(Context.Player==null)return;Context.Player.LocalLookMultiplier=Settings.sensitivity*Nereid.NereidAim.SensitivityScale;
            viewCamera=Context.Player.GetComponentInChildren<Camera>();desiredFov=Settings.fov;
            if(computerSurface && viewCamera!=null && Context.SelectedConsole!=null)
            {
                float distance=Vector3.Distance(viewCamera.transform.position,Context.SelectedConsole.screenAnchor.position);
                desiredFov=Mathf.Clamp(2*Mathf.Atan(Context.SelectedConsole.screenScale*810*.65f/Mathf.Max(.5f,distance))*Mathf.Rad2Deg,34,80);
            }
        }
        void LateUpdate()
        {
            var aim=Nereid.NereidAim.Active;
            if(viewCamera!=null)
            {
                // ADS already eases per frame; physical computer zoom keeps its existing target.
                if(aim!=null&&!computerSurface&&!UiInputGate.Modal&&Nereid.NereidAim.Blend>0)
                    viewCamera.fieldOfView=aim.ResolveFieldOfView(Settings.fov);
                else viewCamera.fieldOfView=Mathf.Lerp(viewCamera.fieldOfView,desiredFov,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
            }
        }
        void OnApplicationFocus(bool focused)
        {
            if(menu==null)return;
            if(!focused && Context.Connected && Page==UiPage.None){ShowPage(UiPage.Pause);Toast(UiText.FocusPause);}
            else SetCursor();
        }
        void OnDestroy()
        {
            if(Instance!=this)return;if(subscribedWorld!=null)subscribedWorld.CommandResponded-=OnCommandReply;
            Settings?.Save();AudioListener.volume=initialVolume;UiInputGate.Reset();Instance=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
