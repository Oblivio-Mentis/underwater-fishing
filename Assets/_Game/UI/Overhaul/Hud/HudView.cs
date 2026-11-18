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
        readonly TMP_Text crewHeading,depthLabel,depthState,depthZone,oxygenLabel,vitalsState,stressLabel;
        readonly TMP_Text stockValue,stockDetail,hungerTime,foodLabel;
        readonly TMP_Text harpoonState,tetherDistance,tensionValue,staminaValue,harpoonTip;
        readonly TMP_Text cargoCount,cargoPhase;
        readonly TMP_Text promptLabel,promptDetail,rescueLabel;
        readonly TMP_Text noticeTitle,noticeDetail,reticle;
        readonly Image oxygenBar,stressBar,foodBar,tensionBar,staminaBar,cargoBar,rescueBar;
        readonly Image vitalsEdge,noticeEdge,missionAccent;
        readonly RectTransform crewPanel,missionPanel,vitalsPanel,harpoonPanel,cargoPanel,promptPanel,noticePanel;
        readonly RectTransform tensionRow,staminaRow;
        readonly CrewSlot[] crew=new CrewSlot[5];
        DiverMotor observedPlayer;
        RaidWorld observedWorld;
        bool previousWave;
        int previousLoss;
        string previousWorldMessage;
        sealed class CrewSlot
        {
            public RectTransform Root;
            public TMP_Text Name,State;
            public Image Mark;
        }

        public HudView(Transform parent,IUiController app)
        {
            this.app=app;
            Root=UiKit.Node(parent,"Diver HUD");UiKit.Fill(Root);
            var group=Root.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;

            RectTransform content;
            crewPanel=Instrument(Root,"HUD Crew",new Vector2(0,1),new Vector2(0,1),new Vector2(24,-24),new Vector2(310,202),out content);
            crewHeading=Line(content,"Crew Count",UiText.HudCrewCount(0),16,UiTheme.Muted,22);
            for(int i=0;i<crew.Length;i++)
            {
                var row=UiKit.Row(content,"Crew Slot "+i,8);UiKit.Size(row,24);
                var mark=UiKit.Panel(row,"State Mark",UiTheme.Green);UiKit.Size(mark,18,3);
                var name=UiKit.Label(row,"Player Identity","",18);UiKit.Size(name,24,flexibleWidth:1);
                var state=UiKit.Label(row,"Player State","",17,UiTheme.Muted);UiKit.Size(state,24,150);state.alignment=TextAlignmentOptions.MidlineRight;
                crew[i]=new CrewSlot{Root=row,Name=name,State=state,Mark=mark};row.gameObject.SetActive(false);
            }
            // Only real occupied slots are shown. The fixed objects avoid rebuilding the crew tree on snapshots.
            crewPanel.sizeDelta=new Vector2(310,52);

            // One shared expedition instrument replaces separate depth and hunger windows.
            missionPanel=Instrument(Root,"HUD Expedition",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-24),new Vector2(700,108),out content);
            missionAccent=UiKit.Panel(missionPanel,"Status Rail",UiTheme.Cyan);missionAccent.rectTransform.anchorMin=new Vector2(0,1);missionAccent.rectTransform.anchorMax=Vector2.one;missionAccent.rectTransform.pivot=new Vector2(.5f,1);missionAccent.rectTransform.sizeDelta=new Vector2(0,3);missionAccent.rectTransform.anchoredPosition=Vector2.zero;
            var strip=UiKit.Row(content,"Expedition Readouts",16);UiKit.Size(strip,82);
            var stock=UiKit.Column(strip,"Reservoir",2);UiKit.Size(stock,82,154);
            var stockHeading=UiKit.Row(stock,"Stock Heading",8);UiKit.Size(stockHeading,20);
            var fishGlyph=UiKit.Node(stockHeading,"Fish Symbol");UiKit.Size(fishGlyph,20,26);fishGlyph.gameObject.AddComponent<HudFishGlyph>().color=UiTheme.Cyan;
            var stockTitle=UiKit.Label(stockHeading,"Stock Caption",UiText.HudStock,16,UiTheme.Muted);UiKit.Size(stockTitle,20,flexibleWidth:1);
            stockValue=Line(stock,"Stock Count","—",32,UiTheme.Text,34);
            stockDetail=Line(stock,"Stock Detail",UiText.HudReservoir,16,UiTheme.Muted,20);
            Divider(strip);
            var feeding=UiKit.Column(strip,"Feeding",4);UiKit.Size(feeding,82,300);
            Line(feeding,"Feeding Heading",UiText.HudHunger,16,UiTheme.Muted,20);
            var feedingValues=UiKit.Row(feeding,"Feeding Values",8);UiKit.Size(feedingValues,34);
            hungerTime=UiKit.Label(feedingValues,"Hunger Timer","—",30);UiKit.Size(hungerTime,34,112);
            foodLabel=UiKit.Label(feedingValues,"Food Amount","",18,UiTheme.Muted);UiKit.Size(foodLabel,34,flexibleWidth:1);foodLabel.alignment=TextAlignmentOptions.MidlineRight;
            foodBar=UiKit.Bar(feeding,"Food Progress",UiTheme.Green,5);
            Divider(strip);
            var depth=UiKit.Column(strip,"Depth",2);UiKit.Size(depth,82,154);
            depthZone=Line(depth,"Depth Zone",UiText.HudBase,16,UiTheme.Muted,20);
            depthLabel=Line(depth,"Depth Value","NEREID",30,UiTheme.Text,34);
            depthState=Line(depth,"Depth State","",16,UiTheme.Muted,20);

            vitalsPanel=Instrument(Root,"HUD Vitals",Vector2.zero,Vector2.zero,new Vector2(24,24),new Vector2(310,140),out content);
            vitalsEdge=vitalsPanel.GetComponent<Image>();
            oxygenLabel=Line(content,"Oxygen Value","",22,UiTheme.Text,26);
            oxygenBar=UiKit.Bar(content,"Oxygen Gauge",UiTheme.Cyan,8);
            vitalsState=Line(content,"Diver Environment","",16,UiTheme.Muted,20);
            stressLabel=Line(content,"Pressure Value","",16,UiTheme.Muted,20);
            stressBar=UiKit.Bar(content,"Pressure Gauge",UiTheme.Muted,4);

            harpoonPanel=Instrument(Root,"HUD Harpoon",new Vector2(1,0),new Vector2(1,0),new Vector2(-24,24),new Vector2(352,194),out content);
            harpoonState=Line(content,"Hook State","",18,UiTheme.Cyan,24);
            tetherDistance=Line(content,"Friend Tether Distance","",18,UiTheme.Muted,24);tetherDistance.gameObject.SetActive(false);
            tensionRow=UiKit.Row(content,"Tension Values",8);UiKit.Size(tensionRow,20);
            var tensionCaption=UiKit.Label(tensionRow,"Tension Caption",UiText.HudTension,16,UiTheme.Muted);UiKit.Size(tensionCaption,20,flexibleWidth:1);
            tensionValue=UiKit.Label(tensionRow,"Tension Value","",17);UiKit.Size(tensionValue,20,180);tensionValue.alignment=TextAlignmentOptions.MidlineRight;
            tensionBar=UiKit.Bar(content,"Tension Gauge",UiTheme.Cyan,8);
            staminaRow=UiKit.Row(content,"Stamina Values",8);UiKit.Size(staminaRow,20);
            var staminaCaption=UiKit.Label(staminaRow,"Stamina Caption",UiText.HudStamina,16,UiTheme.Muted);UiKit.Size(staminaCaption,20,flexibleWidth:1);
            staminaValue=UiKit.Label(staminaRow,"Stamina Value","",17);UiKit.Size(staminaValue,20,82);staminaValue.alignment=TextAlignmentOptions.MidlineRight;
            staminaBar=UiKit.Bar(content,"Stamina Gauge",UiTheme.Green,6);
            harpoonTip=Line(content,"Harpoon Controls",UiText.HudHarpoonTip,16,UiTheme.Muted,40);

            cargoPanel=Instrument(Root,"HUD Cargo",new Vector2(1,0),new Vector2(1,0),new Vector2(-24,242),new Vector2(352,120),out content);
            Line(content,"Cargo Heading",UiText.HudCargo,16,UiTheme.Muted,20);
            cargoCount=Line(content,"Cargo Count","",20,UiTheme.Text,26);
            cargoBar=UiKit.Bar(content,"Cargo Progress",UiTheme.Amber,5);
            cargoPhase=Line(content,"Cargo Phase","",16,UiTheme.Muted,22);

            reticle=UiKit.Label(Root,"HUD Reticle","·",25,UiTheme.Text);UiKit.Place(reticle.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(20,20));reticle.alignment=TextAlignmentOptions.Center;
            promptPanel=Instrument(Root,"HUD Context",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,32),new Vector2(580,82),out content);
            promptLabel=Line(content,"Context Action","",20,UiTheme.Text,26);promptLabel.alignment=TextAlignmentOptions.Center;
            promptDetail=Line(content,"Context Reason","",16,UiTheme.Muted,22);promptDetail.alignment=TextAlignmentOptions.Center;
            rescueBar=UiKit.Bar(content,"Rescue Progress",UiTheme.Green,6);
            rescueLabel=Line(content,"Rescue Confirmation","",16,UiTheme.Green,22);rescueLabel.alignment=TextAlignmentOptions.Center;

            noticePanel=Instrument(Root,"HUD Alert",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-146),new Vector2(680,88),out content);
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
            RefreshCrew(context);
            UiKit.Set(depthZone,UiText.HudDepthZone(context.World.CurrentDepth,context.AtBase));
            UiKit.Set(depthLabel,UiText.HudDepth(player.Depth,context.World.CurrentDepth,context.AtBase));
            UiKit.Set(depthState,UiText.HudDepthState(player.IsInside));

            float capacity=Mathf.Max(1,player.tuning.oxygenCapacity*player.OxygenMultiplier);
            float oxygenFraction=Mathf.Clamp01(player.Oxygen/capacity);
            Color oxygenColor=player.IsHelpless?UiTheme.Red:player.IsInside?UiTheme.Green:oxygenFraction<=.1f?UiTheme.Red:oxygenFraction<=.2f?UiTheme.Amber:UiTheme.Cyan;
            UiKit.Set(oxygenLabel,UiText.HudOxygen(player.Oxygen,capacity));SetColor(oxygenBar,oxygenColor);UiKit.Fraction(oxygenBar,oxygenFraction);
            UiKit.Set(vitalsState,player.IsInside?UiText.HudInside:UiText.HudOutside);vitalsState.color=player.IsInside?UiTheme.Green:UiTheme.Muted;
            UiKit.Set(stressLabel,UiText.HudStress(player.PressureStress,player.IsInside));
            bool showPressure=!player.IsInside || player.PressureStress>1;
            stressLabel.gameObject.SetActive(showPressure);stressBar.transform.parent.gameObject.SetActive(showPressure);
            vitalsPanel.sizeDelta=new Vector2(310,showPressure?140:96);
            Color stressColor=player.IsInside?UiTheme.Muted:player.PressureStress>=player.tuning.stressDanger?UiTheme.Red:player.PressureStress>=player.tuning.stressWarning?UiTheme.Amber:UiTheme.Cyan;
            SetColor(stressBar,stressColor);UiKit.Fraction(stressBar,player.PressureStress/100);SetColor(vitalsEdge,player.IsHelpless?UiTheme.Red:UiTheme.Edge);

            RefreshHarpoon(context);
            RefreshContext(context);
            missionPanel.gameObject.SetActive(view!=null && tuning!=null);
            if(view!=null && tuning!=null)
            {
                int fishCount=0,technicalCount=0;
                foreach(var item in view.tank)
                {
                    if(item==null)continue;
                    if(item.offering>0 || item.biomass>0 || item.chitin>0)fishCount++;else technicalCount++;
                }
                UiKit.Set(stockValue,fishCount.ToString());UiKit.Set(stockDetail,UiText.HudStockDetail(technicalCount));
                int foodRequired=context.FoodRequired;
                bool wave=context.Threats!=null && context.Threats.WaveActive;
                UiKit.Set(hungerTime,UiText.HudHungerTime(view.hungerRemaining));
                hungerTime.color=wave?UiTheme.Red:view.hungerRemaining<=30?UiTheme.Amber:UiTheme.Text;
                UiKit.Set(foodLabel,UiText.HudFood(view.offering,foodRequired));
                bool enough=view.offering>=foodRequired;
                foodLabel.color=enough?UiTheme.Green:UiTheme.Muted;
                SetColor(foodBar,enough?UiTheme.Green:UiTheme.Cyan);UiKit.Fraction(foodBar,(float)view.offering/Mathf.Max(1,foodRequired));
                SetColor(missionAccent,wave?UiTheme.Red:view.hungerRemaining<=30&&!enough?UiTheme.Amber:enough?UiTheme.Green:UiTheme.Cyan);
                RefreshCargo(context);
            }
            else cargoPanel.gameObject.SetActive(false);
            RefreshNotice(context,oxygenFraction);
            RefreshEvents(context);
        }

        void RefreshCrew(UiContext context)
        {
            int count=0;
            foreach(var player in context.Players)
            {
                if(player==null || !player.IsClientInitialized || count>=crew.Length)continue;
                var slot=crew[count++];slot.Root.gameObject.SetActive(true);
                UiKit.Set(slot.Name,UiText.HudPlayerName(player));UiKit.Set(slot.State,UiText.PlayerState(player));
                var color=player.IsHelpless||player.IsTrapped?UiTheme.Red:player.IsInside?UiTheme.Green:UiTheme.Cyan;
                SetColor(slot.Mark,color);slot.State.color=color;
            }
            for(int i=count;i<crew.Length;i++)crew[i].Root.gameObject.SetActive(false);
            UiKit.Set(crewHeading,UiText.HudCrewCount(count));
            crewPanel.sizeDelta=new Vector2(310,52+count*30);
        }

        void RefreshHarpoon(UiContext context)
        {
            var tool=context.Harpoon;var fish=tool==null?null:tool.HookedFish;
            // Incoming R refuses the friend tether and preserves a captured fish.
            // Its controls take priority over the carried-fish hint for that reason.
            bool tether=tool!=null && tool.TetherActive && (fish==null || tool.IsPulled);
            bool hooked=fish!=null && fish.Mode>=FishMode.Hooked && fish.Mode<=FishMode.Tired;
            bool captured=fish!=null && fish.Mode==FishMode.Captured;
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
                harpoonTip.fontSizeMax=pulled?20:16;UiKit.Size(harpoonTip,pulled?28:40);harpoonPanel.sizeDelta=new Vector2(352,pulled?116:128);
                return;
            }
            harpoonState.color=UiTheme.Cyan;harpoonTip.color=UiTheme.Muted;harpoonTip.fontSizeMax=16;
            UiKit.Size(harpoonTip,captured?24:40);harpoonPanel.sizeDelta=new Vector2(352,captured?84:194);
            if(!hooked && !captured)return;
            UiKit.Set(harpoonState,captured?UiText.HudCaptured:UiText.HudHarpoonState(fish.Mode));
            UiKit.Set(harpoonTip,captured?UiText.HudCarryTip:UiText.HudHarpoonTip);
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
                    prompt=UiText.HudAssistPrompt;break;
                case UiInteraction.Fish:
                    if(context.AimedFish!=null)
                    {
                        if(context.AimedFish.Mode==FishMode.Roam)prompt=UiText.HudFishPrompt;
                        else if(context.Harpoon!=null && context.Harpoon.HookedFish==context.AimedFish)prompt=UiText.HudReelPrompt;
                        else {prompt=UiText.HudFishOccupied;color=UiTheme.Muted;}
                    }
                    break;
            }
            float rescue=context.Rescue==null?0:context.Rescue.DisplayProgress;
            int target=context.Rescue==null?-1:context.Rescue.DisplayTargetOwnerId;
            bool rescuing=rescue>0 && target>=0;
            if(rescuing && prompt.Length==0)prompt=UiText.HudRescueProgress(target,rescue);
            promptPanel.gameObject.SetActive(prompt.Length>0);
            UiKit.Set(promptLabel,prompt);promptLabel.color=color;
            UiKit.Set(promptDetail,detail);promptDetail.gameObject.SetActive(detail.Length>0);
            rescueBar.transform.parent.gameObject.SetActive(rescuing);rescueLabel.gameObject.SetActive(rescuing);
            if(rescuing){UiKit.Fraction(rescueBar,rescue);UiKit.Set(rescueLabel,UiText.HudRescueProgress(target,rescue));}
            promptPanel.sizeDelta=new Vector2(580,rescuing?130:detail.Length>0?82:56);
            reticle.color=context.AimedConsole!=null?color:context.AimedFish!=null && context.AimedFish.Mode==FishMode.Roam?UiTheme.Cyan:UiTheme.Text;
        }

        void RefreshNotice(UiContext context,float oxygenFraction)
        {
            var player=context.Player;string title="",detail="";Color color=UiTheme.Amber;
            if(player.IsHelpless){title=UiText.HudOxygenCritical;detail=UiText.HudRescueHint;color=UiTheme.Red;}
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
            if(wave && !previousWave)app.Toast(UiText.HudWave+". "+UiText.HudWaveHint,true);
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
        {var text=UiKit.Label(parent,name,value,size,color);UiKit.Size(text,height);return text;}
        static void Divider(Transform parent)
        {var line=UiKit.Panel(parent,"Readout Divider",UiTheme.Edge);UiKit.Size(line,74,1);}
        static void SetColor(Image image,Color color) {if(image.color!=color)image.color=color;}
    }
}
