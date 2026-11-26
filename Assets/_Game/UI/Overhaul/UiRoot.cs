using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
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
        static readonly Vector2 BoardSize=new(1180,810);
        CanvasScaler scaler,hudScaler;
        GameObject hudOverlay;
        ShipConsole dismissedMonitor, activeSkillStation;
        DiverMotor skillStationPlayer;
        Canvas canvas;
        bool computerSurface;
        Camera viewCamera;
        Nereid.AirlockPad aimedPad;
        RectTransform boardPointer;
        ReticleBoardInput boardInput;
        float ballPressAt=-1, ballReleasedAt=-10f, releaseWind;
        public bool WindingBall => ballPressAt>=0;
        internal float ReleasedBallWind => Time.unscaledTime-ballReleasedAt<.32f ? releaseWind : 0f;
        public float LocalBallWind => WindingBall && Context.World!=null ? Mathf.Clamp01((Time.unscaledTime-ballPressAt)/Mathf.Max(.1f,Context.World.tuning.ballWindSeconds)) : 0;
        float desiredFov=80, fightZoom, fishingPunch;
        /// <summary>Confirmed hook / tug / bite impulse on the owned camera. Decays back to the fight baseline.</summary>
        public void PushFishingPunch(float degrees)
        {
            fishingPunch=Mathf.Max(fishingPunch,Mathf.Clamp(degrees,0,14));
        }
        public void HitMark(Color color,float strength){if(hud!=null)hud.HitMark(color,strength);}
        public void PullEdge(Vector2 direction,float strength){if(hud!=null)hud.PullEdge(direction,strength);}
        InputSystemUIInputModule inputModule;
        RectTransform canvasRect,shade,confirmation;
        TMP_Text toastText,pendingText;
        Image toastPanel;
        MenuView menu;
        HudView hud;
        ProcessingReceiptHud processingReceipt;
        TerminalView terminal;
        CargoView cargo;
        SkillTreeView skills;
        CheatPanel cheats;
        int ignoreWorldPointerThroughFrame = -1;
        internal bool CheatsOpen => cheats?.IsOpen == true;
        internal bool TechnologyOpen => skills?.TechnologyOpen==true;
        CanvasGroup[] modalGroups;
        GameObject cancelConfirmationButton;
        UiPage settingsBack=UiPage.Main;
        UiPage crewBack=UiPage.Pause;
        Action confirmAction;
        MaterialStrip confirmMaterials;
        RaidWorld subscribedWorld;
        bool connectedBefore,overlayBefore,replyReceived,wasPulled,wasDead;
        float nextRefresh,toastUntil,pendingSince,replyAt,saveSettingsAt=-1;
        WorldAction pendingAction;
        long pendingId;
        int requestSequence,pendingRequestId;
        string beforeFingerprint;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartFullscreen()
        {
            if (Application.isBatchMode) return;
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-screen-fullscreen");
            if (option >= 0 && option + 1 < args.Length && args[option + 1] == "0") return;

            // Older builds may have saved a smaller window for this display.
            var monitor = Screen.mainWindowDisplayInfo;
            if (monitor.width > 0 && monitor.height > 0 &&
                (Screen.fullScreenMode != FullScreenMode.FullScreenWindow ||
                 Screen.width != monitor.width || Screen.height != monitor.height))
                Screen.SetResolution(monitor.width, monitor.height, FullScreenMode.FullScreenWindow);
        }
#endif

        void Awake()
        {
            if(Instance!=null){Destroy(gameObject);return;}Instance=this;UiInputGate.Reset();UiInputGate.Active=true;
            Settings=UiSettings.Load();UiKit.Font=font;
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
            boardInput=new ReticleBoardInput(GetComponent<GraphicRaycaster>(),events);
            hudOverlay=new GameObject("Expedition HUD",typeof(Canvas),typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(hudOverlay,gameObject.scene);
            var hudCanvas=hudOverlay.GetComponent<Canvas>();
            hudCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder=95;
            hudScaler=hudOverlay.GetComponent<CanvasScaler>();
            hudScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hudScaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            hud=new HudView(hudOverlay.transform,this);
            processingReceipt=new ProcessingReceiptHud(gameObject.scene);
            var dim=UiKit.Panel(transform,"Menu shade",new Color(.012f,.027f,.031f,.62f),true);shade=dim.rectTransform;UiKit.Fill(shade);
            menu=new MenuView(transform,this);terminal=new TerminalView(transform,this);cargo=new CargoView(transform,this);skills=new SkillTreeView(transform,this);
            modalGroups=new[]{menu.Root.gameObject.AddComponent<CanvasGroup>(),terminal.Root.gameObject.AddComponent<CanvasGroup>(),cargo.Root.gameObject.AddComponent<CanvasGroup>(),skills.Root.gameObject.AddComponent<CanvasGroup>()};
            MakeConfirmation();
            var dot=UiKit.Panel(transform,"Physical monitor sight",UiTheme.Amber);
            boardPointer=dot.rectTransform;UiKit.Place(boardPointer,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(9,9));
            boardPointer.gameObject.AddComponent<Outline>().effectColor=Color.black;
            boardPointer.gameObject.SetActive(false);
            toastPanel=UiKit.Panel(transform,"System notification",UiTheme.Panel);UiKit.Place(toastPanel.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-4),new Vector2(1080,38));
            toastText=UiKit.Label(toastPanel.transform,"Message","",20);UiKit.Fill(toastText.rectTransform,4);toastText.alignment=TextAlignmentOptions.Center;
            toastText.enableAutoSizing=true;toastText.fontSizeMin=16;toastText.fontSizeMax=20;toastText.textWrappingMode=TextWrappingModes.NoWrap;toastText.overflowMode=TextOverflowModes.Ellipsis;
            pendingText=UiKit.Label(transform,"Server acknowledgement","",20,UiTheme.Cyan);UiKit.Place(pendingText.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(750,34));pendingText.alignment=TextAlignmentOptions.Center;
            ApplySettings();ShowPage(UiPage.Main);
        }
        void MakeConfirmation()
        {
            var dark=UiKit.Panel(transform,"Confirm veil",new Color(0,0,0,.68f),true);UiKit.Fill(dark.rectTransform);confirmation=dark.rectTransform;
            var surface=UiKit.Frame(confirmation,"Confirmation",810,490);var column=UiKit.Column(surface,"Content",18,28);UiKit.Fill(column);
            var glyph=UiKit.Symbol(column,"Confirmation instrument",DiveInstruments.Mark.Shield,UiTheme.Amber);UiKit.Size(glyph,44);
            UiKit.Size(UiKit.Label(column,"Title","",30),48);UiKit.Size(UiKit.Label(column,"Description","",23),138);
            confirmMaterials=new MaterialStrip(column,"Confirmed resources");
            var row=UiKit.Row(column,"Actions");UiKit.Size(row,56);
            cancelConfirmationButton=UiKit.Button(row,"Confirm cancel",UiText.Cancel,CancelConfirmation).gameObject;
            UiKit.Button(row,"Confirm accept",UiText.Confirm,()=>{var action=confirmAction;CancelConfirmation();action?.Invoke();},true);
            confirmation.gameObject.SetActive(false);
        }
        public void Confirm(string title,string description,Action confirmed,MaterialCost? materials=null)
        {
            if(CommandPending)return;
            confirmAction=confirmed;
            confirmMaterials.Root.gameObject.SetActive(materials.HasValue);
            if(materials.HasValue)confirmMaterials.Quote(materials.Value);
            confirmation.gameObject.SetActive(true);
            foreach(var group in modalGroups)group.interactable=false;
            var labels=confirmation.GetComponentsInChildren<TMP_Text>();foreach(var label in labels){if(label.name=="Title")label.text=title;if(label.name=="Description")label.text=description;}
            SetCursor();EventSystem.current?.SetSelectedGameObject(cancelConfirmationButton);
        }
        void CancelConfirmation() { confirmAction=null;confirmation.gameObject.SetActive(false);foreach(var group in modalGroups)group.interactable=true;if(Page==UiPage.None)Context.SelectedConsole=null;UiInputGate.ResumeAt=Time.unscaledTime+.15f;SetCursor(); }
        public void ShowPage(UiPage value)
        {
            if (value != UiPage.None && CheatsOpen) CloseCheats();
            if(value==UiPage.Settings && Page!=UiPage.Settings)settingsBack=Page;
            if(value==UiPage.Crew && Page!=UiPage.Crew)crewBack=Page==UiPage.None?UiPage.Pause:Page;
            if(!Context.Connected && value==UiPage.None)value=UiPage.Main;
            if(value!=UiPage.None)CancelBallGesture();
            bool wasMonitor=computerSurface;
            Page=value;
            if(boardPointer!=null)boardPointer.gameObject.SetActive(false);
            if(value!=UiPage.Terminal && value!=UiPage.SkillTree)Context.SelectedConsole=null;
            if(value!=UiPage.SkillTree && value!=UiPage.None)ClearSkillStation();
            boardInput.Clear();
            ConfigureSurface(value);
            if(toastPanel!=null)UiKit.Place(toastPanel.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),
                new Vector2(0,value==UiPage.None?-270:0),new Vector2(value==UiPage.None?760:1080,38));
            bool menuPage=value==UiPage.Main||value==UiPage.Connection||value==UiPage.Crew||value==UiPage.Pause||value==UiPage.Settings;
            menu.Root.gameObject.SetActive(menuPage);if(menuPage)menu.Open(value);
            terminal.Root.gameObject.SetActive(value==UiPage.Terminal);cargo.Root.gameObject.SetActive(value==UiPage.Cargo);skills.Root.gameObject.SetActive(value==UiPage.SkillTree);
            shade.gameObject.SetActive(value!=UiPage.None&&!computerSurface);
            hud.Root.gameObject.SetActive(Context.Connected && (value==UiPage.None || computerSurface));
            if(!computerSurface && !(wasMonitor && value==UiPage.None))UiInputGate.ResumeAt=Time.unscaledTime+.15f;
            SetCursor();EventSystem.current?.SetSelectedGameObject(null);
            if(Context.Connected && value==UiPage.Terminal)terminal.Refresh();if(Context.Connected&&value==UiPage.Cargo)cargo.Refresh();
            if(value==UiPage.SkillTree)skills.Refresh();
            Canvas.ForceUpdateCanvases();
        }
        void ConfigureSurface(UiPage page)
        {
            var station=Context.SelectedConsole;
            computerSurface=(page==UiPage.Terminal || page==UiPage.SkillTree) && station!=null && station.screenAnchor!=null;
            if(computerSurface)
            {
                canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Context.Player.GetComponentInChildren<Camera>();
                var face=station.screenAnchor;
                var size=station.DisplaySize;
                canvasRect.sizeDelta=BoardSize;
                canvasRect.SetPositionAndRotation(face.position-face.forward*.006f,face.rotation);
                canvasRect.localScale=new Vector3(size.x/BoardSize.x,size.y/BoardSize.y,station.screenScale);
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
            if(computerSurface)dismissedMonitor=Context.SelectedConsole;
            if(Page==UiPage.SkillTree)ClearSkillStation();
            ShowPage(Page==UiPage.Settings?settingsBack:Page==UiPage.Crew?crewBack:Context.Connected?UiPage.None:UiPage.Main);
        }
        void ClearSkillStation()
        {
            activeSkillStation=null;
            skillStationPlayer=null;
            skills.Close();
        }

        void SetCursor()
        {
            bool overlay=Context.Session!=null&&Context.Session.lobby!=null&&Context.Session.lobby.OverlayActive;
            if(inputModule!=null)
            {
                inputModule.cursorLockBehavior=computerSurface?InputSystemUIInputModule.CursorLockBehavior.ScreenCenter:InputSystemUIInputModule.CursorLockBehavior.OutsideScreen;
                inputModule.enabled=Application.isFocused&&!overlay&&!computerSurface;
            }
            if(EventSystem.current!=null)EventSystem.current.sendNavigationEvents=!computerSurface && !CheatsOpen;
            UiInputGate.MonitorLook=computerSurface && !overlay;
            UiInputGate.Modal=(!computerSurface && Page!=UiPage.None) || confirmation.gameObject.activeSelf || overlay;
            bool locked=Context.Connected && !CheatsOpen && (!UiInputGate.Modal || UiInputGate.MonitorLook) && Application.isFocused;
            Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;
        }
        void Update()
        {
            if(menu==null)return;
            bool overlay=Context.Session!=null&&Context.Session.lobby!=null&&Context.Session.lobby.OverlayActive;
            if(overlay!=overlayBefore){overlayBefore=overlay;if(Context.Connected && Page==UiPage.None)ShowPage(UiPage.Pause);SetCursor();}
            if (CheatsOpen && (!Context.Connected || Context.Player.IsHelpless)) CloseCheats();
            if (Application.isFocused && !overlay && Keyboard.current?.xKey.wasPressedThisFrame == true &&
                Context.Connected && Context.World != null && !Context.Player.IsHelpless && (Page == UiPage.None || computerSurface) &&
                !confirmation.gameObject.activeSelf)
            {
                if (CheatsOpen) CloseCheats();
                else
                {
                    if (Context.World.InventionPanelOpen) Context.World.CloseInventionInventory();
                    if (computerSurface) ClosePanel();
                    CancelBallGesture();
                    cheats ??= new CheatPanel(this);
                    cheats.Open(Context.Player);
                    EventSystem.current?.SetSelectedGameObject(null);
                    SetCursor();
                }
            }
            if(Application.isFocused && !overlayBefore && Context.World?.InventionPanelOpen!=true && Time.unscaledTime>=UiInputGate.ResumeAt && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if(confirmation.gameObject.activeSelf)CancelConfirmation();
                else if(Page==UiPage.SkillTree && skills.Back())boardInput.Clear();
                else if(Page==UiPage.None)ShowPage(UiPage.Pause);
                else ClosePanel();
            }
            var mouse=Application.isFocused?Mouse.current:null;
            bool skillPurchase=Page==UiPage.SkillTree && mouse!=null && mouse.rightButton.wasPressedThisFrame;
            bool pointerAllowed=!UiInputGate.Blocked || UiInputGate.MonitorLook && mouse!=null && !overlayBefore;
            bool interact=Application.isFocused && Keyboard.current?.eKey.wasPressedThisFrame==true &&
                Context.World?.InventionPanelOpen!=true && Context.World?.InventionRackTargeted(Context.Player)!=true &&
                Context.World?.LookAtOwnParcel(Context.Player,out _)!=true;
            bool consumed=false;
            if (CheatsOpen || Time.frameCount <= ignoreWorldPointerThroughFrame)
            {
                UiInputGate.PointerReserved = true;
                aimedPad = null;
                AimedCargoDispatch = false;
            }
            else
            {
                consumed=UpdatePhysicalPointer(mouse!=null&&mouse.leftButton.wasPressedThisFrame,mouse!=null&&mouse.leftButton.wasReleasedThisFrame,
                    mouse==null?0:Mathf.Clamp(mouse.scroll.ReadValue().y,-1,1),pointerAllowed,skillPurchase);
            }
            skills.Animate(Time.unscaledDeltaTime);
            if(interact && !consumed && !UiInputGate.Blocked && Context.Connected && Page==UiPage.None)Interact();
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
                    if (CheatsOpen) cheats.Refresh();
                    if(((Page==UiPage.Terminal || Page==UiPage.SkillTree) && !Context.NearTerminal)||(Page==UiPage.Cargo && !Context.NearCargo))ShowPage(UiPage.None);
                    bool pulled=Context.Harpoon!=null&&Context.Harpoon.IsPulled;
                    if(pulled&&!wasPulled&&Page!=UiPage.None)Toast("Напарник тянет вас к себе. R — отцепиться");
                    wasPulled=pulled;
                    hud.Refresh();if(Page==UiPage.Terminal)terminal.Refresh();else if(Page==UiPage.Cargo)cargo.Refresh();else if(Page==UiPage.SkillTree)skills.Refresh();
                    ApplyCameraSettings();
                    string worldMessage=Context.World.Message;
                    if((worldMessage.StartsWith("SAVE ERROR")||worldMessage.StartsWith("Сохранение несовместимо")||worldMessage.StartsWith("Подача остановлена:")) && worldMessage!=lastSaveMessage){lastSaveMessage=worldMessage;Toast(UiText.WorldMessage(worldMessage),true);}
                    else if(!worldMessage.StartsWith("SAVE ERROR")&&!worldMessage.StartsWith("Сохранение несовместимо")&&!worldMessage.StartsWith("Подача остановлена:"))lastSaveMessage="";
                }
                if(menu.Root.gameObject.activeSelf)menu.Refresh();UpdatePending();
                toastPanel.gameObject.SetActive(Time.unscaledTime<toastUntil);UiKit.Set(pendingText,CommandPending?UiText.WaitCommand:"");
            }
            processingReceipt.Update(Context.World,Context.Connected);
            hud.AnimateReeling();
            hud.UpdateScan();
            if(saveSettingsAt>=0 && Time.unscaledTime>=saveSettingsAt){saveSettingsAt=-1;Settings.Save();}
        }
        public Nereid.AirlockPad AimedPad => aimedPad;
        internal void CloseCheats()
        {
            if (!CheatsOpen) return;
            cheats.Close();
            ignoreWorldPointerThroughFrame = Time.frameCount + 1;
            EventSystem.current?.SetSelectedGameObject(null);
            SetCursor();
        }
        public bool AimedCargoDispatch { get; private set; }

        void CancelBallGesture()
        {
            if(!WindingBall)return;
            ballPressAt=-1;
            if(Context?.World!=null && Context.World.HasGoshaBall(Context.Player))
                Context.World.RequestGosha(GoshaAction.CancelWindUp);
        }

        internal void HandleBallPointer(bool pressed,bool released)
        {
            var world=Context.World;
            if(Page!=UiPage.None || UiInputGate.Modal || Time.unscaledTime<UiInputGate.ResumeAt || world==null || !world.GoshaBallInHands(Context.Player) ||
               world.Gosha.Mode is GoshaMode.Chasing or GoshaMode.Pouncing)
            {
                CancelBallGesture();
                return;
            }
            if(pressed && !WindingBall)
            {
                ballReleasedAt=-10f;
                ballPressAt=Time.unscaledTime;
                world.RequestGosha(GoshaAction.WindUp);
            }
            if(released && WindingBall)
            {
                releaseWind=LocalBallWind;
                ballReleasedAt=Time.unscaledTime;
                ballPressAt=-1;
                world.RequestGosha(GoshaAction.Throw);
            }
        }

        bool UpdatePhysicalPointer(bool clicked,bool released,float scroll,bool allowed,bool skillPurchase=false)
        {
            UiInputGate.PointerReserved=false;
            aimedPad=null;
            AimedCargoDispatch=false;
            if(boardPointer.gameObject.activeSelf && !computerSurface)boardPointer.gameObject.SetActive(false);
            var player=Context.Player;
            if(activeSkillStation!=null && (player==null || player!=skillStationPlayer || player.IsHelpless ||
                !player.IsInside || !activeSkillStation.InRange(player)))ClearSkillStation();
            if(WindingBall && (UiInputGate.Modal || !Context.Connected || player==null || player.IsHelpless || overlayBefore || Context.World==null || !Context.World.GoshaBallInHands(player)))CancelBallGesture();
            if(!Context.Connected || player==null || player.IsDead || overlayBefore || (Page!=UiPage.None&&!computerSurface))return false;
            var cam=player.GetComponentInChildren<Camera>();
            var station=ShipConsole.ResolveSight(player,cam);
            if(station!=dismissedMonitor)dismissedMonitor=null;
            if(computerSurface && (station!=Context.SelectedConsole || player.IsHelpless))
            {
                if(confirmAction!=null)CancelConfirmation();
                ShowPage(UiPage.None);
            }
            if(Context.World!=null && Context.World.GoshaBallInHands(player))
            {
                UiInputGate.PointerReserved=true;
                if(allowed)HandleBallPointer(clicked,released);
                return clicked || released;
            }
            if(Context.World!=null && Context.World.IsHoldingGosha(player))
            {
                UiInputGate.PointerReserved=true;
                return false;
            }
            if(clicked && allowed && Page==UiPage.None && Context.Harpoon!=null && Context.Harpoon.ConsumeCargoPrimaryInput())
            {
                UiInputGate.PointerReserved=true;
                return true;
            }
            if(Page==UiPage.None && CargoDispatch.Aimed(player,cam)!=null)
            {
                AimedCargoDispatch=true;
                UiInputGate.PointerReserved=true;
                if(clicked && allowed)Send(WorldAction.Hoist);
                return clicked;
            }
            aimedPad=Nereid.AirlockPad.Aimed(player,cam);
            UiInputGate.PointerReserved=station!=null || aimedPad!=null;
            if(aimedPad!=null)
            {
                if(clicked && allowed && !player.IsHelpless && aimedPad.CanRequest){aimedPad.Press(player);return true;}
                return false;
            }
            if(station==null || !allowed || player.IsHelpless || station==dismissedMonitor)return false;
            if(station.IsMonitor)
            {
                if(!computerSurface)UseConsole(station);
                return computerSurface && UpdateMonitorReticle(player,cam,station,clicked,released,scroll,allowed,skillPurchase);
            }
            if(!clicked)return false;
            UseConsole(station);
            return true;
        }

        bool UpdateMonitorReticle(DiverMotor player,Camera cam,ShipConsole station,bool clicked,bool released,float scroll,bool allowed,bool skillPurchase)
        {
            var screenCenter=cam.pixelRect.center;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screenCenter,cam,out var point))
            {
                boardPointer.anchoredPosition=point;
                boardPointer.SetAsLastSibling();
                boardPointer.gameObject.SetActive(true);
            }
            if(clicked && allowed)player.TouchTerminal(station.kind);
            return boardInput.Update(screenCenter,clicked&&allowed,released,allowed?scroll:0,skillPurchase&&allowed);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool QaClickReticle() => UpdatePhysicalPointer(true,true,0,!UiInputGate.Modal || UiInputGate.MonitorLook);
        public bool QaPointAtMonitor() { UpdatePhysicalPointer(false,false,0,true);return computerSurface; }
        public void QaDragReticle(bool down,bool up) => UpdatePhysicalPointer(down,up,0,UiInputGate.MonitorLook);
        public bool QaBuyReticle() => UpdatePhysicalPointer(false,false,0,UiInputGate.MonitorLook,true);
        public bool QaScrollReticle(float steps) => UpdatePhysicalPointer(false,false,steps,!UiInputGate.Modal || UiInputGate.MonitorLook);
        public string ReticleControl => computerSurface?boardInput.ControlName:"";
        public bool ProcessingReceiptVisible => processingReceipt?.Visible==true;
        internal RectTransform QaHudRoot => hud.Root;
#endif
        string lastSaveMessage="";
        void Interact()
        {
            if(Context.Player.IsHelpless)return;
            switch(Context.Interaction)
            {
                case UiInteraction.Terminal:
                    if(ShipConsole.Stations.Count==0)UseConsole(null);
                    break;
                case UiInteraction.Cargo:
                    if(Nereid.NereidVessel.Active!=null)break;
                    if(Context.View.hoistPhase!=HoistPhase.Loading)Toast(UiText.CargoLocked);
                    else if(Context.HasCargo && Context.View.CargoUsed+Context.CarriedSize<=Context.View.Capacity(Context.Tuning))Send(WorldAction.LoadCargo);
                    ShowPage(UiPage.Cargo);break;
                case UiInteraction.PetGosha: Context.World.RequestGosha(GoshaAction.Pet);break;
                case UiInteraction.PickUpBall: Context.World.RequestGosha(GoshaAction.PickUp);break;
                case UiInteraction.DroppedItem: Context.Harpoon.RequestPickup(Context.AimedDrop.id);break;
                case UiInteraction.Loot: Send(WorldAction.Collect); break;
            }
        }
        public void UseConsole(ShipConsole station)
        {
            if(!Context.Connected || Context.Player.IsHelpless || confirmAction!=null)return;
            if(station?.IsMonitor!=true && (CommandPending || Context.World.TravelRemaining>0))return;
            if(station?.kind==ShipConsoleKind.SkillTree)
            {
                if(!station.InRange(Context.Player)){Toast(UiText.NeedTerminal,true);return;}
                Context.SelectedConsole=station;
                if(activeSkillStation!=station || skillStationPlayer!=Context.Player)
                {
                    skills.Open();
                    activeSkillStation=station;
                    skillStationPlayer=Context.Player;
                }
                ShowPage(UiPage.SkillTree);
                return;
            }
            if(station!=null&&!station.InRange(Context.Player)){Toast(UiText.NeedTerminal,true);return;}
            if(station==null&&ShipConsole.Stations.Count>0)return;
            if(station!=null && !station.IsMonitor)
            {
                station.dispatchButton?.Press(Context.Player);
                if(station.kind==ShipConsoleKind.CargoControl)
                    Send(WorldAction.Hoist);
                else ToggleFishRoute(station.kind==ShipConsoleKind.Feed);
                return;
            }
            Context.SelectedConsole=station;
            terminal.SelectTab(station?.kind==ShipConsoleKind.Depths?"depths":"catch");
            ShowPage(UiPage.Terminal);
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
            var destination=feeding?CatchDestination.Feed:CatchDestination.Process;
            long quote=FishRouteQuote.ComputeDraft(view.tank,destination);
            if(quote==0)
            {
                Toast(feeding?"На кормление ничего не назначено":"На переработку ничего не назначено");
                return;
            }
            Send(feeding?WorldAction.SendFeedingDraft:WorldAction.SendProcessingDraft,quote);
        }
        public void Send(WorldAction action,long id=0)
        {
            if(!Context.Connected||Context.World==null){Toast(UiText.NotConnected,true);return;}if(CommandPending)return;
            pendingAction=action;pendingId=id;beforeFingerprint=Fingerprint();pendingSince=Time.unscaledTime;replyReceived=false;CommandPending=true;
            pendingRequestId=++requestSequence;Context.World.RequestCommand(action,id,pendingRequestId);
        }
        string Fingerprint()
        {
            var v=Context.View;if(v==null)return "";
            if(pendingAction==WorldAction.Process||pendingAction==WorldAction.Feed||pendingAction==WorldAction.HoldFish)
                return v.tank.Find(x=>x.id==pendingId)?.destination.ToString() ?? "gone";
            if(pendingAction==WorldAction.LoadCargo)return v.CargoUsed.ToString();if(pendingAction==WorldAction.Hoist)return v.hoistPhase.ToString();
            if(pendingAction==WorldAction.Collect)return Context.Loot.FindAll(x=>Context.Player!=null&&x.CarrierOwnerId==Context.Player.OwnerId).Count.ToString();
            if(pendingAction is WorldAction.ProcessAll or WorldAction.FeedAll or WorldAction.SendFish or WorldAction.SendProcessingDraft or WorldAction.SendFeedingDraft)return Context.World.FishRoute.sequence.ToString();
            if(pendingAction==WorldAction.StopProcessing||pendingAction==WorldAction.StopFeeding)return Context.World.FishRoute.sequence+"/"+Context.World.FishRoute.stopping+"/"+Context.World.FishRoute.active;
            return Context.World.CurrentDepth+"/"+(Context.World.TravelRemaining>0);
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
            scaler.referenceResolution=new Vector2(1920,1080)/Settings.scale;
            hudScaler.referenceResolution=scaler.referenceResolution;
            ApplyCameraSettings();saveSettingsAt=Time.unscaledTime+.4f;
        }
        void ApplyCameraSettings()
        {
            if(Context.Player==null)return;Context.Player.LocalLookMultiplier=Settings.sensitivity*Nereid.NereidAim.SensitivityScale;
            viewCamera=Context.Player.GetComponentInChildren<Camera>();desiredFov=Settings.fov;

        }
        void LateUpdate()
        {
            var aim=Nereid.NereidAim.Active;
            if(viewCamera!=null)
            {
                var fish = Page == UiPage.None && Context.Player != null && !Context.Player.IsHelpless && !computerSurface
                    ? Context.Harpoon?.HookedFish : null;
                bool fighting = fish != null && fish.HookCount > 0 && fish.Mode <= FishMode.Tired;
                float target = fighting ? fish.tuning.fightFov + (fish.Surging ? fish.tuning.burstFov : 0) : 0;
                float attack = fishingPunch > fightZoom + .05f ? 26f : 6f;
                fightZoom = Mathf.Lerp(fightZoom, target * Settings.fishingCamera, 1-Mathf.Exp(-Time.unscaledDeltaTime*attack));
                // A fresh confirmed event snaps the view open, then settles into the steady fight offset.
                // While the fight continues the extra snap bleeds back into the steady fight offset.
                fishingPunch = Mathf.MoveTowards(fishingPunch,0,Time.unscaledDeltaTime*(fighting?9f:26f));
                if (!fighting && Mathf.Abs(fightZoom) < .01f) { fightZoom = 0; fishingPunch = 0; }
                if(aim!=null&&!computerSurface&&!UiInputGate.Modal&&Nereid.NereidAim.Blend>0)
                    viewCamera.fieldOfView=aim.ResolveFieldOfView(Settings.fov) + fightZoom + fishingPunch*Settings.fishingCamera;
                else
                {
                    viewCamera.fieldOfView=Mathf.Lerp(viewCamera.fieldOfView,desiredFov+fightZoom+fishingPunch*Settings.fishingCamera,
                        1-Mathf.Exp(-Time.unscaledDeltaTime*18));
                }
            }
        }
        void OnApplicationFocus(bool focused)
        {
            if(menu==null)return;
            if(!focused && Context.Connected && (Page==UiPage.None||computerSurface)){if(confirmAction!=null)CancelConfirmation();ShowPage(UiPage.Pause);Toast(UiText.FocusPause);}
            else SetCursor();
        }
        void OnDestroy()
        {
            if(Instance!=this)return;if(subscribedWorld!=null)subscribedWorld.CommandResponded-=OnCommandReply;
            if(CheatsOpen)cheats.Close();
            if(hudOverlay!=null)Destroy(hudOverlay);
            processingReceipt?.Dispose();skills?.Close();Settings?.Save();UiInputGate.Reset();Instance=null;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
    }
}
