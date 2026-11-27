using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Raid.UI
{
    /// <summary>Reusable HUD widgets read confirmed client snapshots; no gameplay state is changed here.</summary>
    public sealed class HudView
    {
        readonly IUiController app;
        public RectTransform Root {get;private set;}
        readonly TMP_Text cargoCount,cargoPhase,scanMarker;
        readonly AimReticle reticle;
        readonly FishingPullEdges pullEdges;
        readonly Image cageHold;

        public void HitMark(Color tint, float strength) => reticle.Hit(tint, strength);
        public void PullEdge(Vector2 direction, float strength) => pullEdges.Show(direction, strength);
        readonly DiveInstruments instruments;
        readonly Image cargoBar;
        readonly ReelReadout reel;
        readonly RectTransform cargoPanel;
        readonly HudSafeArea safe;
        readonly HudEdgePanel prompts;
        readonly HudWarnings warnings;
        readonly HudDefenseSignals defenseSignals;
        int lastSlot=-1;
        bool lastInside;
        ShipConsole lastMonitor;
        float idleUntil;
        DiverMotor observedPlayer;
        RaidWorld observedWorld;
        bool previousWave;
        int previousLoss;
        string previousWorldMessage;
        public HudView(Transform parent,IUiController app)
        {
            this.app=app;
            Root=UiKit.Node(parent,"Diver HUD");UiKit.Fill(Root);
            var group=Root.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;

            var edgeArea = UiKit.Node(Root, "Line pull");
            UiKit.Fill(edgeArea);
            pullEdges = edgeArea.gameObject.AddComponent<FishingPullEdges>();
            pullEdges.raycastTarget = false;

            RectTransform content;
            instruments=new DiveInstruments(Root);
            defenseSignals=new HudDefenseSignals(Root);

            safe=new HudSafeArea(Root);
            reel=new ReelReadout(Root,safe.root);
            warnings=new HudWarnings(safe.root);
            prompts=new HudEdgePanel(safe.root);
            cargoPanel=Instrument(safe.root,"HUD Cargo",Vector2.right,Vector2.right,new Vector2(-24,24),new Vector2(352,120),out content);
            Line(content,"Cargo Heading",UiText.HudCargo,16,UiTheme.Muted,20);
            cargoCount=Line(content,"Cargo Count","",20,UiTheme.Text,26);
            cargoBar=UiKit.Bar(content,"Cargo Progress",UiTheme.Amber,5);
            cargoPhase=Line(content,"Cargo Phase","",16,UiTheme.Muted,22);

            var reticleRoot=UiKit.Node(Root,"Harpoon Reticle");UiKit.Place(reticleRoot,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(64,64));
            reticle=reticleRoot.gameObject.AddComponent<AimReticle>();reticle.Initialize(app);
            var holdTrack=UiKit.Panel(Root,"Cargo hold track",new Color(.025f,.07f,.09f,.94f));
            UiKit.Place(holdTrack.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-23),new Vector2(108,7));
            cageHold=UiKit.Panel(holdTrack.transform,"Cargo hold progress",UiTheme.Amber);
            UiKit.Fill(cageHold.rectTransform,1);
            holdTrack.gameObject.SetActive(false);
            scanMarker=UiKit.Label(Root,"Detected signal","",20,UiTheme.Cyan);
            scanMarker.alignment=TextAlignmentOptions.Center;
            scanMarker.raycastTarget=false;
            UiKit.Place(scanMarker.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(260,30));
            scanMarker.gameObject.SetActive(false);
            cargoPanel.gameObject.SetActive(false);
        }

        // The root calls Refresh at 10 Hz; Set avoids dirtying unchanged text/layout.
        public void Refresh()
        {
            var context=app.Context;
            if(context==null || !context.Connected || context.Player.tuning==null)
            {observedPlayer=null;observedWorld=null;return;}
            var player=context.Player;var view=context.View;var tuning=context.Tuning;
            float oxygenFraction=Mathf.Clamp01(player.Oxygen/Mathf.Max(1,player.AirCapacity));
            safe.Refresh();
            reel.Refresh(context);
            if(view!=null && tuning!=null && !player.IsDead)RefreshCargo(context);
            else cargoPanel.gameObject.SetActive(false);
            var tool=context.Harpoon;
            var monitor=UiInputGate.MonitorLook?context.SelectedConsole:null;
            if(observedPlayer!=player || lastMonitor!=monitor || lastSlot!=(tool?tool.ActiveSlot:-1) || lastInside!=player.IsInside)
            {
                lastSlot=tool?tool.ActiveSlot:-1;lastInside=player.IsInside;lastMonitor=monitor;
                idleUntil=Time.unscaledTime+6;
            }
            var hint=monitor?UiText.MonitorActions(monitor):UiText.ContextAction(context);
            prompts.Show(hint,!UiInputGate.Modal&&(!hint.idle||Time.unscaledTime<idleUntil),cargoPanel.gameObject.activeSelf);
            reticle.gameObject.SetActive(!player.IsDead && !UiInputGate.Modal);
            reticle.ContextColor=hint.caution?UiTheme.Amber:context.AimedConsole!=null||context.AimedFish!=null?UiTheme.Cyan:UiTheme.Text;
            instruments.Refresh(context,oxygenFraction,!cargoPanel.gameObject.activeSelf&&!reel.Visible);
            warnings.Refresh(context,oxygenFraction);
            RefreshEvents(context);
        }

        void RefreshCargo(UiContext context)
        {
            var view=context.View;var tuning=context.Tuning;
            bool busy=view.hoistPhase!=HoistPhase.Loading;
            cargoPanel.gameObject.SetActive(Nereid.NereidVessel.Active==null && (context.NearCargo || busy));
            if(!cargoPanel.gameObject.activeSelf)return;
            int capacity=view.Capacity(tuning);UiKit.Set(cargoCount,UiText.HudCargoCount(view.CargoUsed,capacity));
            float duration=Mathf.Max(.01f,view.HoistDuration(view.hoistPhase,tuning));
            UiKit.Fraction(cargoBar,busy?1-view.hoistRemaining/duration:(float)view.CargoUsed/Mathf.Max(1,capacity));
            bool full=view.CargoUsed>=capacity;SetColor(cargoBar,full?UiTheme.Red:busy?UiTheme.Cyan:UiTheme.Amber);
            UiKit.Set(cargoPhase,busy?UiText.HudHoistTime(view.hoistPhase,view.hoistRemaining):full?UiText.HudCargoFull:UiText.HudCargoReady);
            cargoPhase.color=full?UiTheme.Amber:UiTheme.Muted;
        }

        internal void ShowCargoHold(float progress)
        {
            cageHold.transform.parent.gameObject.SetActive(progress>0);
            UiKit.Fraction(cageHold,progress);
        }

        internal void AnimateReeling() {reel.Animate();warnings.Animate();}

        public void UpdateScan()
        {
            defenseSignals.Present(app.Context);
            var diver=app.Context.Player;
            bool active=diver!=null && diver.MarkerActive && !diver.IsDead;
            scanMarker.gameObject.SetActive(active);
            if(!active)return;
            var camera=diver.GetComponentInChildren<Camera>();
            if(camera==null)return;
            var at=camera.WorldToViewportPoint(diver.MarkerPosition);
            var anchor=new Vector2(at.z<.2f?.5f:Mathf.Clamp(at.x,.20f,.80f),at.z<.2f?.63f:Mathf.Clamp(at.y,.24f,.76f));
            scanMarker.rectTransform.anchorMin=scanMarker.rectTransform.anchorMax=anchor;
            scanMarker.rectTransform.anchoredPosition=new Vector2(0,22);
            UiKit.Set(scanMarker,diver.ScanLabel);
        }

        DefensePhase seenDefensePhase;

        void RefreshEvents(UiContext context)
        {
            bool wave=context.DefendingReservoir;
            string message=context.World.Message??"";int loss=context.World.StockLossCount;
            if(observedPlayer!=context.Player || observedWorld!=context.World || loss<previousLoss)
            {
                observedPlayer=context.Player;observedWorld=context.World;
                previousWave=wave;previousLoss=loss;previousWorldMessage=message;seenDefensePhase=context.Threats?context.Threats.Defense.phase:DefensePhase.Idle;return;
            }
            if(message!=previousWorldMessage && message.StartsWith("Feeding: ") && !message.Contains("None"))app.Toast(UiText.WorldMessage(message));
            var raid=context.Threats;
            if(raid && raid.Defense.phase!=seenDefensePhase)
            {
                seenDefensePhase=raid.Defense.phase;
                if(seenDefensePhase!=DefensePhase.Idle && !string.IsNullOrEmpty(raid.Message))
                    app.Toast(raid.Message,seenDefensePhase!=DefensePhase.Recovery);
            }
            if(raid && !raid.DefenseActive && wave && !previousWave && !Root.gameObject.activeInHierarchy)app.Toast(UiText.HudWave+". "+UiText.HudWaveHint,true);
            if(!wave && previousWave)app.Toast(UiText.HudWaveEnded);
            if(loss>previousLoss)app.Toast(UiText.HudStockLost(loss-previousLoss),true);
            previousWave=wave;previousLoss=loss;previousWorldMessage=message;
        }

        static RectTransform Instrument(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size,out RectTransform content)
        {
            var edge=UiKit.Panel(parent,name,UiTheme.Edge);UiKit.Place(edge.rectTransform,anchor,pivot,position,size);
            var surface=UiKit.Panel(edge.transform,"Surface",new Color(UiTheme.Steel.r,UiTheme.Steel.g,UiTheme.Steel.b,.94f));UiKit.Fill(surface.rectTransform,1);
            content=UiKit.Column(surface.transform,"Readouts",6,12);UiKit.Fill(content);return edge.rectTransform;
        }
        static TMP_Text Line(Transform parent,string name,string value,float size,Color color,float height)
        {var text=UiKit.Label(parent,name,value,size,color);UiKit.Size(text,Mathf.Max(height,Mathf.Ceil(size*1.3f)+1));return text;}
        static void SetColor(Image image,Color color) {if(image.color!=color)image.color=color;}
    }
}
