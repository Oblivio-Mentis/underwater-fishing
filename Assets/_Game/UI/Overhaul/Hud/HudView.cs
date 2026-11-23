using System.Linq;
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
        readonly TMP_Text harpoonState,tetherDistance,harpoonTip;
        readonly TMP_Text cargoCount,cargoPhase;
        readonly TMP_Text promptLabel,promptDetail,rescueLabel;
        readonly TMP_Text noticeTitle,noticeDetail,scanMarker;
        readonly AimReticle reticle;
        readonly DiveInstruments instruments;
        readonly Image cargoBar,rescueBar;
        readonly ReelReadout reel;
        readonly Image noticeEdge;
        readonly RectTransform harpoonPanel,cargoPanel,promptPanel,noticePanel;
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

            RectTransform content;
            instruments=new DiveInstruments(Root);reel=new ReelReadout(Root);

            harpoonPanel=Instrument(Root,"HUD Harpoon",new Vector2(1,0),new Vector2(1,0),new Vector2(-24,24),new Vector2(352,194),out content);
            harpoonState=Line(content,"Hook State","",18,UiTheme.Cyan,24);
            tetherDistance=Line(content,"Friend Tether Distance","",18,UiTheme.Muted,24);tetherDistance.gameObject.SetActive(false);
            harpoonTip=Line(content,"Harpoon Controls",UiText.HudHarpoonTip,16,UiTheme.Muted,40);

            cargoPanel=Instrument(Root,"HUD Cargo",new Vector2(1,0),new Vector2(1,0),new Vector2(-24,242),new Vector2(352,120),out content);
            Line(content,"Cargo Heading",UiText.HudCargo,16,UiTheme.Muted,20);
            cargoCount=Line(content,"Cargo Count","",20,UiTheme.Text,26);
            cargoBar=UiKit.Bar(content,"Cargo Progress",UiTheme.Amber,5);
            cargoPhase=Line(content,"Cargo Phase","",16,UiTheme.Muted,22);

            var reticleRoot=UiKit.Node(Root,"Harpoon Reticle");UiKit.Place(reticleRoot,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(64,64));
            reticle=reticleRoot.gameObject.AddComponent<AimReticle>();reticle.Initialize(app);
            scanMarker=UiKit.Label(Root,"Detected signal","",20,UiTheme.Cyan);
            scanMarker.alignment=TextAlignmentOptions.Center;
            scanMarker.raycastTarget=false;
            UiKit.Place(scanMarker.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(260,30));
            scanMarker.gameObject.SetActive(false);
            promptPanel=Instrument(Root,"HUD Context",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,125),new Vector2(580,82),out content);
            promptLabel=Line(content,"Context Action","",20,UiTheme.Text,26);promptLabel.alignment=TextAlignmentOptions.Center;
            promptDetail=Line(content,"Context Reason","",16,UiTheme.Muted,22);promptDetail.alignment=TextAlignmentOptions.Center;
            rescueBar=UiKit.Bar(content,"Rescue Progress",UiTheme.Green,6);
            rescueLabel=Line(content,"Rescue Confirmation","",16,UiTheme.Green,22);rescueLabel.alignment=TextAlignmentOptions.Center;

            noticePanel=Instrument(Root,"HUD Alert",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-151),new Vector2(680,94),out content);
            noticeEdge=noticePanel.GetComponent<Image>();
            noticeTitle=Line(content,"Alert Title","",21,UiTheme.Amber,26);noticeTitle.alignment=TextAlignmentOptions.Center;
            noticeDetail=Line(content,"Alert Detail","",17,UiTheme.Text,30);noticeDetail.alignment=TextAlignmentOptions.Center;
            harpoonPanel.gameObject.SetActive(false);cargoPanel.gameObject.SetActive(false);promptPanel.gameObject.SetActive(false);noticePanel.gameObject.SetActive(false);
        }

        // The root calls Refresh at 10 Hz; Set avoids dirtying unchanged text/layout.
        public void Refresh()
        {
            var context=app.Context;
            if(context==null || !context.Connected || context.Player.tuning==null)
            {observedPlayer=null;observedWorld=null;return;}
            var player=context.Player;var view=context.View;var tuning=context.Tuning;
            float capacity=player.AirCapacity;
            float oxygenFraction=Mathf.Clamp01(player.Oxygen/capacity);
            reel.Refresh(context);
            RefreshHarpoon(context);
            RefreshContext(context);
            if(view!=null&&tuning!=null)RefreshCargo(context);else cargoPanel.gameObject.SetActive(false);
            if(player.IsDead)
            {
                harpoonPanel.gameObject.SetActive(false);cargoPanel.gameObject.SetActive(false);
                promptPanel.gameObject.SetActive(false);reticle.gameObject.SetActive(false);
            }
            instruments.Refresh(context,oxygenFraction,!harpoonPanel.gameObject.activeSelf&&!cargoPanel.gameObject.activeSelf&&!reel.Visible);
            RefreshNotice(context,oxygenFraction);
            RefreshEvents(context);
        }

        void RefreshHarpoon(UiContext context)
        {
            var tool=context.Harpoon;var fish=tool==null?null:tool.HookedFish;
            // Incoming R refuses the friend tether and preserves a captured fish.
            // Its controls take priority over the carried-fish hint for that reason.
            bool tether=tool!=null && tool.TetherActive && (fish==null || tool.IsPulled);
            bool captured=tool!=null && tool.HandsFull;
            harpoonPanel.gameObject.SetActive(captured||tether);
            tetherDistance.gameObject.SetActive(tether);
            if(!captured&&!tether)return;
            if(tether)
            {
                bool pulled=tool.IsPulled;
                var owner=tool.TetherOwner;var target=tool.TetherTarget;
                UiKit.Set(harpoonState,UiText.HudTetherActor(pulled?owner:target,pulled));
                UiKit.Set(tetherDistance,owner!=null&&target!=null?UiText.HudTetherDistance(Vector3.Distance(owner.transform.position,target.transform.position),tool.TetherMaxRange):UiText.HudTetherReleasing);
                UiKit.Set(harpoonTip,pulled?UiText.HudTetherRefuse:UiText.HudTetherControls);
                harpoonState.color=pulled?UiTheme.Amber:UiTheme.Cyan;harpoonTip.color=pulled?UiTheme.Amber:UiTheme.Text;
                harpoonTip.fontSizeMax=pulled?20:16;UiKit.Size(harpoonTip,pulled?28:44);harpoonPanel.sizeDelta=new Vector2(352,pulled?118:134);
                return;
            }
            harpoonState.color=UiTheme.Cyan;harpoonTip.color=UiTheme.Muted;harpoonTip.fontSizeMax=16;
            UiKit.Size(harpoonTip,32);harpoonPanel.sizeDelta=new Vector2(352,92);
            string load=tool.CarriedSalvage!=null?tool.CarriedSalvage.DisplayName:ReelReadout.Species(tool.CarriedFish?.SpeciesId);
            UiKit.Set(harpoonState,load+" · обе руки заняты");
            UiKit.Set(harpoonTip,"К клети за кормой   ·   Q — положить");

        }

        void RefreshCargo(UiContext context)
        {
            var view=context.View;var tuning=context.Tuning;
            bool busy=view.hoistPhase!=HoistPhase.Loading;
            cargoPanel.gameObject.SetActive(Nereid.NereidVessel.Active==null && (context.NearCargo || busy));
            cargoPanel.anchoredPosition=new Vector2(-24,harpoonPanel.gameObject.activeSelf?harpoonPanel.sizeDelta.y+38:24);
            if(!cargoPanel.gameObject.activeSelf)return;
            int capacity=view.Capacity(tuning);UiKit.Set(cargoCount,UiText.HudCargoCount(view.CargoUsed,capacity));
            float duration=Mathf.Max(.01f,view.HoistDuration(view.hoistPhase,tuning));
            UiKit.Fraction(cargoBar,busy?1-view.hoistRemaining/duration:(float)view.CargoUsed/Mathf.Max(1,capacity));
            bool full=view.CargoUsed>=capacity;SetColor(cargoBar,full?UiTheme.Red:busy?UiTheme.Cyan:UiTheme.Amber);
            UiKit.Set(cargoPhase,busy?UiText.HudHoistTime(view.hoistPhase,view.hoistRemaining):full?UiText.HudCargoFull:UiText.HudCargoReady);
            cargoPhase.color=full?UiTheme.Amber:UiTheme.Muted;
        }

        internal void AnimateReeling() => reel.Animate();

        public void UpdateScan()
        {
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

        void RefreshContext(UiContext context)
        {
            var player=context.Player;var view=context.View;
            string prompt="",detail="";Color color=UiTheme.Text;
            if(player.IsHelpless)prompt=UiText.HudWaitForRescue;
            else if(context.World.HasGoshaBall(player) && (context.World.Gosha.Mode is GoshaMode.Chasing or GoshaMode.Pouncing))prompt="Гоша догоняет вас!";
            else if(context.World.GoshaBallInHands(player))prompt=UiRoot.Instance.WindingBall?"Отпустите ЛКМ — бросить мяч":"Удерживайте ЛКМ — замах · Q — выложить мяч";
            else if(context.World.IsHoldingGosha(player))prompt="E — опустить Гошу на палубу";
            else if(context.Harpoon!=null && context.Harpoon.HasBallLine)prompt="ЛКМ — подтянуть мяч · R — отпустить";
            else if(UiRoot.Instance!=null && UiRoot.Instance.AimedCargoDispatch)
            {
                prompt=view.CargoUsed>0?"ЛКМ — отправить улов":"Контейнер пуст";
                detail=view.CargoUsed+" / "+view.Capacity(context.Tuning);
            }
            else if(context.Harpoon!=null && CargoDispatch.CanLoad(context.World,player) &&
                    context.Harpoon.SelectedItem(out _) is DroppedKind.Catch or DroppedKind.Salvage)
            {
                bool room=view.hoistPhase==HoistPhase.Loading && view.CargoUsed+context.CarriedSize<=view.Capacity(context.Tuning);
                string load=context.Harpoon.CarriedSalvage!=null?"Q — положить груз в контейнер":"Q — выпустить улов в контейнер";
                prompt=room?load:"Контейнер сейчас недоступен";
                detail=view.CargoUsed+" / "+view.Capacity(context.Tuning);
            }
            else if(UiRoot.Instance!=null && UiRoot.Instance.AimedPad!=null)
            {
                var pad=UiRoot.Instance.AimedPad;
                prompt=(pad.CanRequest?"ЛКМ   ":"")+pad.Caption;
                detail=context.World.Airlock.Caption;
                color=pad.CanRequest?UiTheme.Cyan:UiTheme.Amber;
            }
            else switch(context.Interaction)
            {
                case UiInteraction.PetGosha: prompt="E — взять Гошу на руки и погладить";break;
                case UiInteraction.PickUpBall: prompt="E — Взять мяч";break;
                case UiInteraction.Rescue:
                    if(context.RescueTarget!=null){prompt=UiText.HudRescuePrompt(context.RescueTarget);detail=UiText.HudRescueDuration(player.tuning.rescueSeconds);}break;
                case UiInteraction.Terminal:
                    var console=context.AimedConsole;
                    if(console!=null)
                    {
                        prompt=UiText.HudConsolePrompt(console);detail=UiText.HudConsoleAction(console.kind);
                        color=console.kind==ShipConsoleKind.Feed?UiTheme.Amber:UiTheme.Cyan;
                        if(console.kind==ShipConsoleKind.Feed||console.kind==ShipConsoleKind.Process)
                        {
                            var route=context.World.FishRoute;
                            bool matching=route.feeding==(console.kind==ShipConsoleKind.Feed);
                            var destination=console.kind==ShipConsoleKind.Feed?CatchDestination.Feed:CatchDestination.Process;
                            int selected=view?.tank.Count(item=>FishRouteQuote.IsDraft(item,destination))??0;
                            prompt=route.active && matching && !route.stopping?"ЛКМ — остановить подачу":
                                !route.active && selected>0?"ЛКМ — отправить выбранную рыбу":console.Caption;
                            detail=route.active
                                ? route.stopping?"Клапан закрыт · рыба завершает путь":matching?"В трубе "+(route.total-route.completed):"Другая линия занята"
                                : selected>0?"Назначено рыб  "+selected:"Назначьте рыбу на экране резервуара";
                        }
                    }
                    else {prompt=UiText.HudTerminalPrompt;detail=UiText.HudTerminalHint;}
                    break;
                case UiInteraction.Cargo:
                    prompt=UiText.HudCargoPrompt;
                    if(view==null || context.Tuning==null)break;
                    if(view.hoistPhase!=HoistPhase.Loading){detail=UiText.HudCargoBlocked(view.hoistPhase);color=UiTheme.Amber;}
                    else if(context.HasCargo)
                    {
                        int free=view.Capacity(context.Tuning)-view.CargoUsed;
                        if(context.CarriedSize<=free){prompt=UiText.HudLoadPrompt;detail=UiText.HudCargoCount(view.CargoUsed,view.Capacity(context.Tuning));}
                        else {detail=UiText.HudCargoSpace(context.CarriedSize,free);color=UiTheme.Amber;}
                    }
                    else detail=view.cargo.Count>0?UiText.HudHoistReady:UiText.HudCargoEmpty;
                    break;
                case UiInteraction.DroppedItem:
                    prompt=context.AimedDrop.kind==DroppedKind.Harpoon && context.Harpoon.OwnsHarpoon
                        ?"E — Обменять гарпун":"E — Поднять "+context.AimedDrop.Label;
                    break;
                case UiInteraction.Loot:
                    prompt="E — поднять · "+context.AimedLoot.DisplayName;
                    detail="Обе руки";
                    break;
                case UiInteraction.Assist:
                    if(reel.Visible)break;
                    prompt=context.Harpoon.ActiveSlot==1?"ЛКМ — держать напарника":UiText.HudAssistPrompt;break;
                case UiInteraction.Fish:
                    if(reel.Visible)break;
                    if(context.AimedFish!=null)
                    {
                        if(context.AimedFish.Mode==FishMode.Roam)prompt=context.Harpoon.ActiveSlot!=0?"1 — выбрать гарпун":context.AimedFish.RequiresPartner?"ЛКМ — зацепить · нужны двое":UiText.HudFishPrompt;
                        else if(context.Harpoon!=null && context.Harpoon.HookedFish==context.AimedFish)prompt=UiText.HudReelPrompt;
                        else if(context.AimedFish.RequiresPartner && context.AimedFish.Mode >= FishMode.Hooked && context.AimedFish.Mode <= FishMode.Tired)
                            prompt="ЛКМ — поставить второй гарпун";
                        else {prompt=UiText.HudFishOccupied;color=UiTheme.Muted;}
                    }
                    break;
            }
            if(!reel.Visible && context.Harpoon!=null && context.Harpoon.AssistTarget!=null)
            {
                prompt="Держите напарника";detail=context.Harpoon.ActiveSlot==1?"Отпустите ЛКМ или E, чтобы разжать захват":"Отпустите E, чтобы разжать захват";color=UiTheme.Green;
            }
            if (prompt.Length == 0 && context.Harpoon != null && context.Harpoon.SelectedItem(out _) != DroppedKind.None)
                prompt="Q — Положить выбранный предмет";
            var chamber=Nereid.NereidAirlock.Active;
            if(prompt.Length==0 && chamber!=null && chamber.Contains(player.transform.position))
            {
                var lockState=context.World.Airlock;
                prompt=lockState.Caption;
                detail=lockState.Busy ? "Вода   "+Mathf.RoundToInt(lockState.Water*100)+"%"
                    : lockState.Phase==AirlockPhase.InnerOpen ? "Выход в море · голубая кнопка у наружной двери"
                    : "Возврат на борт · янтарная кнопка у внутренней двери";
                color=UiTheme.Cyan;
            }
            float rescue=context.Rescue==null?0:context.Rescue.DisplayProgress;
            int target=context.Rescue==null?-1:context.Rescue.DisplayTargetOwnerId;
            bool rescuing=rescue>0 && target>=0;
            if(rescuing && prompt.Length==0)prompt=UiText.HudRescueProgress(target,rescue);
            if(!player.IsHelpless && detail.Length==0 && !rescuing)
            {
                var training=context.World.GoshaTrainingHint(player);
                if(prompt.Length==0)prompt=training;
                else detail=training;
            }
            promptPanel.gameObject.SetActive(prompt.Length>0&&!UiInputGate.Modal&&!UiInputGate.MonitorLook);reticle.gameObject.SetActive(!UiInputGate.Modal && !UiInputGate.MonitorLook && (UiInputGate.PointerReserved || context.Harpoon!=null && context.Harpoon.HarpoonInHands));
            UiKit.Set(promptLabel,prompt);promptLabel.color=color;
            UiKit.Set(promptDetail,detail);promptDetail.gameObject.SetActive(detail.Length>0);
            rescueBar.transform.parent.gameObject.SetActive(rescuing);rescueLabel.gameObject.SetActive(rescuing);
            if(rescuing){UiKit.Fraction(rescueBar,rescue);UiKit.Set(rescueLabel,UiText.HudRescueProgress(target,rescue));}
            promptPanel.sizeDelta=new Vector2(580,rescuing?130:detail.Length>0?82:56);
            reticle.ContextColor=context.AimedConsole!=null?color:context.AimedFish!=null && context.AimedFish.Mode==FishMode.Roam?UiTheme.Cyan:UiTheme.Text;
        }

        void RefreshNotice(UiContext context,float oxygenFraction)
        {
            var player=context.Player;string title="",detail="";Color color=UiTheme.Amber;
            if(player.IsDead)
            {
                title=player.Spectating!=null?"Наблюдение  ·  "+UiText.HudPlayerName(player.Spectating):
                    player.IsConsumed?"Вас проглотил левиафан":"Водолаз погиб";
                bool teammate=false;
                foreach(var member in context.Players)if(member!=null&&member!=player&&!member.IsHelpless){teammate=true;break;}
                detail=teammate?"ПКМ — наблюдать":"";
                if(player.CanRespawn)detail+=(detail.Length>0?"    ·    ":"")+"E — вернуться на борт";
                else if(player.CanBeRescued&&teammate)detail+="    ·    Напарник может спасти вас [F]";
            }
            else if(player.AirReserveRemaining>0)
            {
                title="Аварийный воздух";detail=Mathf.CeilToInt(player.AirReserveRemaining)+" с · к сухому отсеку";
            }
            else if(player.IsHypoxic)
            {
                title="Гипоксия";detail="Теряете здоровье · вернитесь в сухой отсек";
                if(player.AirReserveSpent && player.AirReserveCooldown>0)
                    detail+=" · резерв "+player.AirReserveCooldown+" с";
                color=Color.Lerp(UiTheme.Amber,UiTheme.Red,player.HypoxiaSeverity);
            }
            else if(player.IsHelpless){title=UiText.HudOxygenCritical;detail=UiText.HudRescueHint;color=UiTheme.Red;}
            else if(player.IsTrapped){title=UiText.HudTrapped;detail=UiText.HudRescueHint;color=UiTheme.Red;}
            else if(!player.IsInside && oxygenFraction<=.1f){title=UiText.HudOxygenLow;detail=UiText.HudOxygenLowHint;color=UiTheme.Red;}
            else if(!player.IsInside && player.PressureStress>=player.tuning.stressDanger){title=UiText.HudPressureDanger;detail=UiText.HudPressureDangerHint;color=UiTheme.Red;}
            else if(context.DefendingReservoir){title=UiText.HudWave;detail=UiText.HudWaveHint;color=UiTheme.Red;}
            else if(!player.IsInside && oxygenFraction<=.2f){title=UiText.HudOxygenLow;detail=UiText.HudOxygenLowHint;}
            else if(!player.IsInside && player.PressureStress>=player.tuning.stressWarning){title=UiText.HudPressureWarning;detail=UiText.HudPressureWarningHint;}
            noticePanel.gameObject.SetActive(title.Length>0);UiKit.Set(noticeTitle,title);UiKit.Set(noticeDetail,detail);noticeTitle.color=color;SetColor(noticeEdge,color);
        }

        void RefreshEvents(UiContext context)
        {
            bool wave=context.DefendingReservoir;
            string message=context.World.Message??"";int loss=context.World.StockLossCount;
            if(observedPlayer!=context.Player || observedWorld!=context.World || loss<previousLoss)
            {
                observedPlayer=context.Player;observedWorld=context.World;
                previousWave=wave;previousLoss=loss;previousWorldMessage=message;return;
            }
            if(message!=previousWorldMessage && message.StartsWith("Feeding: ") && !message.Contains("None"))app.Toast(UiText.WorldMessage(message));
            if(wave && !previousWave && !Root.gameObject.activeInHierarchy)app.Toast(UiText.HudWave+". "+UiText.HudWaveHint,true);
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
