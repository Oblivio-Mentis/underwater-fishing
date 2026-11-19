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
        readonly TMP_Text harpoonState,tetherDistance,tensionValue,staminaValue,harpoonTip;
        readonly TMP_Text cargoCount,cargoPhase;
        readonly TMP_Text promptLabel,promptDetail,rescueLabel;
        readonly TMP_Text noticeTitle,noticeDetail;
        readonly AimReticle reticle;
        readonly DiveInstruments instruments;
        readonly Image tensionBar,staminaBar,cargoBar,rescueBar;
        readonly Image noticeEdge;
        readonly RectTransform harpoonPanel,cargoPanel,promptPanel,noticePanel;
        readonly RectTransform tensionRow,staminaRow;
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
            instruments=new DiveInstruments(Root);

            harpoonPanel=Instrument(Root,"HUD Harpoon",new Vector2(1,0),new Vector2(1,0),new Vector2(-24,24),new Vector2(352,194),out content);
            harpoonState=Line(content,"Hook State","",18,UiTheme.Cyan,24);
            tetherDistance=Line(content,"Friend Tether Distance","",18,UiTheme.Muted,24);tetherDistance.gameObject.SetActive(false);
            tensionRow=UiKit.Row(content,"Tension Values",8);UiKit.Size(tensionRow,22);
            var tensionCaption=UiKit.Label(tensionRow,"Tension Caption",UiText.HudTension,16,UiTheme.Muted);UiKit.Size(tensionCaption,20,flexibleWidth:1);
            tensionValue=UiKit.Label(tensionRow,"Tension Value","",17);UiKit.Size(tensionValue,20,180);tensionValue.alignment=TextAlignmentOptions.MidlineRight;
            tensionBar=UiKit.Bar(content,"Tension Gauge",UiTheme.Cyan,8);
            staminaRow=UiKit.Row(content,"Stamina Values",8);UiKit.Size(staminaRow,22);
            var staminaCaption=UiKit.Label(staminaRow,"Stamina Caption",UiText.HudStamina,16,UiTheme.Muted);UiKit.Size(staminaCaption,20,flexibleWidth:1);
            staminaValue=UiKit.Label(staminaRow,"Stamina Value","",17);UiKit.Size(staminaValue,20,82);staminaValue.alignment=TextAlignmentOptions.MidlineRight;
            staminaBar=UiKit.Bar(content,"Stamina Gauge",UiTheme.Green,6);
            harpoonTip=Line(content,"Harpoon Controls",UiText.HudHarpoonTip,16,UiTheme.Muted,40);

            cargoPanel=Instrument(Root,"HUD Cargo",new Vector2(1,0),new Vector2(1,0),new Vector2(-24,242),new Vector2(352,120),out content);
            Line(content,"Cargo Heading",UiText.HudCargo,16,UiTheme.Muted,20);
            cargoCount=Line(content,"Cargo Count","",20,UiTheme.Text,26);
            cargoBar=UiKit.Bar(content,"Cargo Progress",UiTheme.Amber,5);
            cargoPhase=Line(content,"Cargo Phase","",16,UiTheme.Muted,22);

            var reticleRoot=UiKit.Node(Root,"Harpoon Reticle");UiKit.Place(reticleRoot,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(64,64));
            reticle=reticleRoot.gameObject.AddComponent<AimReticle>();reticle.Initialize(app);
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
            float capacity=Mathf.Max(1,player.tuning.oxygenCapacity*player.OxygenMultiplier);
            float oxygenFraction=Mathf.Clamp01(player.Oxygen/capacity);
            RefreshHarpoon(context);
            RefreshContext(context);
            if(view!=null&&tuning!=null)RefreshCargo(context);else cargoPanel.gameObject.SetActive(false);
            if(player.IsDead)
            {
                harpoonPanel.gameObject.SetActive(false);cargoPanel.gameObject.SetActive(false);
                promptPanel.gameObject.SetActive(false);reticle.gameObject.SetActive(false);
            }
            instruments.Refresh(context,oxygenFraction,!harpoonPanel.gameObject.activeSelf&&!cargoPanel.gameObject.activeSelf);
            RefreshNotice(context,oxygenFraction);
            RefreshEvents(context);
        }

        void RefreshHarpoon(UiContext context)
        {
            var tool=context.Harpoon;var fish=tool==null?null:tool.HookedFish;
            // Incoming R refuses the friend tether and preserves a captured fish.
            // Its controls take priority over the carried-fish hint for that reason.
            bool tether=tool!=null && tool.TetherActive && (fish==null || tool.IsPulled);
            bool hooked=fish!=null && fish.Mode>=FishMode.Hooked && fish.Mode<=FishMode.Tired;
            bool captured=tool!=null && tool.HandsFull;
            harpoonPanel.gameObject.SetActive(hooked||captured||tether);
            tetherDistance.gameObject.SetActive(tether);
            tensionRow.gameObject.SetActive(hooked&&!tether);tensionBar.transform.parent.gameObject.SetActive(hooked&&!tether);
            staminaRow.gameObject.SetActive(hooked&&!tether);staminaBar.transform.parent.gameObject.SetActive(hooked&&!tether);
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
            UiKit.Size(harpoonTip,captured?24:44);harpoonPanel.sizeDelta=new Vector2(352,captured?84:194);
            if(!hooked && !captured)return;
            UiKit.Set(harpoonState,captured?UiText.HudCaptured:UiText.HudHarpoonState(fish.Mode));
            UiKit.Set(harpoonTip,captured?"Обе руки заняты · R — отпустить":fish.RequiresPartner&&tool.AssistingDiver==null?"Требуется напарник · удерживать E рядом":UiText.HudHarpoonTip);
            if(!hooked)return;
            float maximum=Mathf.Max(1,fish.tuning.initialStamina*fish.power);
            UiKit.Set(tensionValue,UiText.HudTensionValue(fish.Tension));
            Color color=fish.Tension>=.85f?UiTheme.Red:fish.Tension>=.6f?UiTheme.Amber:UiTheme.Cyan;
            tensionValue.color=color;SetColor(tensionBar,color);UiKit.Fraction(tensionBar,fish.Tension);
            UiKit.Set(staminaValue,UiText.HudStaminaValue(fish.Stamina,maximum));UiKit.Fraction(staminaBar,fish.Stamina/maximum);
        }

        void RefreshCargo(UiContext context)
        {
            var view=context.View;var tuning=context.Tuning;
            bool busy=view.hoistPhase!=HoistPhase.Loading;
            cargoPanel.gameObject.SetActive(context.NearCargo || busy);
            cargoPanel.anchoredPosition=new Vector2(-24,harpoonPanel.gameObject.activeSelf?harpoonPanel.sizeDelta.y+38:24);
            if(!cargoPanel.gameObject.activeSelf)return;
            int capacity=view.Capacity(tuning);UiKit.Set(cargoCount,UiText.HudCargoCount(view.CargoUsed,capacity));
            float duration=view.hoistPhase==HoistPhase.Raising?tuning.raisingSeconds:view.hoistPhase==HoistPhase.Unloading?tuning.unloadingSeconds:tuning.returningSeconds;
            duration=Mathf.Max(.01f,duration/Mathf.Max(1,1+view.winchLevel*tuning.winchSpeedPerLevel));
            UiKit.Fraction(cargoBar,busy?1-view.hoistRemaining/duration:(float)view.CargoUsed/Mathf.Max(1,capacity));
            bool full=view.CargoUsed>=capacity;SetColor(cargoBar,full?UiTheme.Red:busy?UiTheme.Cyan:UiTheme.Amber);
            UiKit.Set(cargoPhase,busy?UiText.HudHoistTime(view.hoistPhase,view.hoistRemaining):full?UiText.HudCargoFull:UiText.HudCargoReady);
            cargoPhase.color=full?UiTheme.Amber:UiTheme.Muted;
        }

        void RefreshContext(UiContext context)
        {
            string prompt="",detail="";Color color=UiTheme.Text;
            var player=context.Player;var view=context.View;
            if(player.IsHelpless)prompt=UiText.HudWaitForRescue;
            else switch(context.Interaction)
            {
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
                            prompt=route.active&&matching&&!route.stopping?"E — остановить подачу":"E — запустить подачу";
                            detail=route.active?(route.stopping?"Клапан закрыт · рыба в трубе завершает путь":matching?"Повторное нажатие сохранит оставшуюся рыбу":"Другая линия занята"):"Поток из резервуара · повторное нажатие — стоп";
                        }
                        if(console.kind==ShipConsoleKind.Process&&!context.AtBase){detail=UiText.HudProcessAtBase;color=UiTheme.Amber;}
                    }
                    else {prompt=UiText.HudTerminalPrompt;detail=context.AtBase?UiText.HudTerminalBaseHint:UiText.HudTerminalDiveHint;}
                    break;
                case UiInteraction.Cargo:
                    prompt=UiText.HudCargoPrompt;
                    if(view==null || context.Tuning==null)break;
                    if(context.AtBase){detail=UiText.HudCargoBaseHint;color=UiTheme.Muted;}
                    else if(view.hoistPhase!=HoistPhase.Loading){detail=UiText.HudCargoBlocked(view.hoistPhase);color=UiTheme.Amber;}
                    else if(context.HasCargo)
                    {
                        int free=view.Capacity(context.Tuning)-view.CargoUsed;
                        if(context.CarriedSize<=free){prompt=UiText.HudLoadPrompt;detail=UiText.HudCargoCount(view.CargoUsed,view.Capacity(context.Tuning));}
                        else {detail=UiText.HudCargoSpace(context.CarriedSize,free);color=UiTheme.Amber;}
                    }
                    else detail=view.cargo.Count>0?UiText.HudHoistReady:UiText.HudCargoEmpty;
                    break;
                case UiInteraction.Loot:
                    bool carryingLoot=false;foreach(var loot in context.Loot)if(loot!=null && loot.CarrierOwnerId==player.OwnerId){carryingLoot=true;break;}
                    prompt=carryingLoot?UiText.HudLootOccupied:UiText.HudLootPrompt;color=carryingLoot?UiTheme.Amber:UiTheme.Text;break;
                case UiInteraction.Assist:
                    prompt=context.Harpoon.ActiveSlot==1?"ЛКМ — держать напарника":UiText.HudAssistPrompt;break;
                case UiInteraction.Fish:
                    if(context.AimedFish!=null)
                    {
                        if(context.AimedFish.Mode==FishMode.Roam)prompt=context.Harpoon.ActiveSlot!=0?"1 — выбрать гарпун":context.AimedFish.RequiresPartner?"ЛКМ — зацепить · нужны двое":UiText.HudFishPrompt;
                        else if(context.Harpoon!=null && context.Harpoon.HookedFish==context.AimedFish)prompt=UiText.HudReelPrompt;
                        else {prompt=UiText.HudFishOccupied;color=UiTheme.Muted;}
                    }
                    break;
            }
            if(context.Harpoon!=null && context.Harpoon.AssistTarget!=null)
            {
                prompt="Держите напарника";detail=context.Harpoon.ActiveSlot==1?"Отпустите ЛКМ или E, чтобы разжать захват":"Отпустите E, чтобы разжать захват";color=UiTheme.Green;
            }
            float rescue=context.Rescue==null?0:context.Rescue.DisplayProgress;
            int target=context.Rescue==null?-1:context.Rescue.DisplayTargetOwnerId;
            bool rescuing=rescue>0 && target>=0;
            if(rescuing && prompt.Length==0)prompt=UiText.HudRescueProgress(target,rescue);
            promptPanel.gameObject.SetActive(prompt.Length>0&&!UiInputGate.Modal);reticle.gameObject.SetActive(!UiInputGate.Modal && context.Harpoon!=null && context.Harpoon.ActiveSlot==0 && !context.Harpoon.HandsFull);
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
            else if(player.IsHelpless){title=UiText.HudOxygenCritical;detail=UiText.HudRescueHint;color=UiTheme.Red;}
            else if(player.IsTrapped){title=UiText.HudTrapped;detail=UiText.HudRescueHint;color=UiTheme.Red;}
            else if(!player.IsInside && oxygenFraction<=.1f){title=UiText.HudOxygenLow;detail=UiText.HudOxygenLowHint;color=UiTheme.Red;}
            else if(!player.IsInside && player.PressureStress>=player.tuning.stressDanger){title=UiText.HudPressureDanger;detail=UiText.HudPressureDangerHint;color=UiTheme.Red;}
            else if(context.Threats!=null && context.Threats.WaveActive){title=UiText.HudWave;detail=UiText.HudWaveHint;color=UiTheme.Red;}
            else if(!player.IsInside && oxygenFraction<=.2f){title=UiText.HudOxygenLow;detail=UiText.HudOxygenLowHint;}
            else if(!player.IsInside && player.PressureStress>=player.tuning.stressWarning){title=UiText.HudPressureWarning;detail=UiText.HudPressureWarningHint;}
            noticePanel.gameObject.SetActive(title.Length>0);UiKit.Set(noticeTitle,title);UiKit.Set(noticeDetail,detail);noticeTitle.color=color;SetColor(noticeEdge,color);
        }

        void RefreshEvents(UiContext context)
        {
            bool wave=context.Threats!=null && context.Threats.WaveActive;
            string message=context.World.Message??"";int loss=context.World.StockLossCount;
            if(observedPlayer!=context.Player || observedWorld!=context.World || loss<previousLoss)
            {
                observedPlayer=context.Player;observedWorld=context.World;
                previousWave=wave;previousLoss=loss;previousWorldMessage=message;return;
            }
            if(message!=previousWorldMessage && message.StartsWith("Feeding: "))app.Toast(UiText.WorldMessage(message),message.Contains("Underfed")||message.Contains("None"));
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
