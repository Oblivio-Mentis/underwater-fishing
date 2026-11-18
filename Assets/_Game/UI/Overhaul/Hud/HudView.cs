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
        readonly TMP_Text crewHeading,depthLabel,depthState,oxygenLabel,vitalsState,stressLabel;
        readonly TMP_Text hungerTime,foodLabel;
        readonly TMP_Text harpoonState,tensionValue,staminaValue,harpoonTip;
        readonly TMP_Text cargoCount,cargoPhase;
        readonly TMP_Text promptLabel,promptDetail,rescueLabel;
        readonly TMP_Text noticeTitle,noticeDetail,reticle;
        readonly Image oxygenBar,stressBar,foodBar,tensionBar,staminaBar,cargoBar,rescueBar;
        readonly Image vitalsEdge,noticeEdge;
        readonly RectTransform crewPanel,hungerPanel,harpoonPanel,cargoPanel,promptPanel,noticePanel;
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
            crewPanel=Instrument(Root,"HUD Crew",new Vector2(0,1),new Vector2(0,1),new Vector2(28,-28),new Vector2(390,232),out content);
            crewHeading=Line(content,"Crew Count",UiText.HudCrewCount(0),18,UiTheme.Muted,26);
            for(int i=0;i<crew.Length;i++)
            {
                var row=UiKit.Row(content,"Crew Slot "+i,10);UiKit.Size(row,30);
                var mark=UiKit.Panel(row,"State Mark",UiTheme.Green);UiKit.Size(mark,22,4);
                var name=UiKit.Label(row,"Player Identity","",18);UiKit.Size(name,30,flexibleWidth:1);
                var state=UiKit.Label(row,"Player State","",18,UiTheme.Muted);UiKit.Size(state,30,142);state.alignment=TextAlignmentOptions.MidlineRight;
                crew[i]=new CrewSlot{Root=row,Name=name,State=state,Mark=mark};row.gameObject.SetActive(false);
            }
            // Only real occupied slots are shown. The fixed objects avoid rebuilding the crew tree on snapshots.
            crewPanel.sizeDelta=new Vector2(390,78);

            Instrument(Root,"HUD Depth",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-28),new Vector2(340,84),out content);
            depthLabel=Line(content,"Depth Value",UiText.HudBase,23,UiTheme.Text,30);depthLabel.alignment=TextAlignmentOptions.Center;
            depthState=Line(content,"Depth State","",18,UiTheme.Muted,20);depthState.alignment=TextAlignmentOptions.Center;

            hungerPanel=Instrument(Root,"HUD Feeding",Vector2.one,Vector2.one,new Vector2(-28,-28),new Vector2(324,128),out content);
            Line(content,"Feeding Heading",UiText.HudHunger,18,UiTheme.Muted,24);
            var feeding=UiKit.Row(content,"Feeding Values",12);UiKit.Size(feeding,28);
            hungerTime=UiKit.Label(feeding,"Hunger Timer","",20);UiKit.Size(hungerTime,28,flexibleWidth:1);
            foodLabel=UiKit.Label(content,"Food Amount","",18,UiTheme.Muted);UiKit.Size(foodLabel,24);
            foodBar=UiKit.Bar(content,"Food Progress",UiTheme.Green,5);

            var vitals=Instrument(Root,"HUD Vitals",Vector2.zero,Vector2.zero,new Vector2(28,28),new Vector2(390,170),out content);
            vitalsEdge=vitals.GetComponent<Image>();
            oxygenLabel=Line(content,"Oxygen Value","",22,UiTheme.Text,30);
            oxygenBar=UiKit.Bar(content,"Oxygen Gauge",UiTheme.Cyan,10);
            vitalsState=Line(content,"Diver Environment","",17,UiTheme.Muted,25);
            stressLabel=Line(content,"Pressure Value","",18,UiTheme.Muted,25);
            stressBar=UiKit.Bar(content,"Pressure Gauge",UiTheme.Muted,6);

            harpoonPanel=Instrument(Root,"HUD Harpoon",new Vector2(1,0),new Vector2(1,0),new Vector2(-28,28),new Vector2(390,238),out content);
            harpoonState=Line(content,"Hook State","",18,UiTheme.Cyan,28);
            tensionRow=UiKit.Row(content,"Tension Values",8);UiKit.Size(tensionRow,22);
            var tensionCaption=UiKit.Label(tensionRow,"Tension Caption",UiText.HudTension,18,UiTheme.Muted);UiKit.Size(tensionCaption,22,flexibleWidth:1);
            tensionValue=UiKit.Label(tensionRow,"Tension Value","",18);UiKit.Size(tensionValue,22,185);tensionValue.alignment=TextAlignmentOptions.MidlineRight;
            tensionBar=UiKit.Bar(content,"Tension Gauge",UiTheme.Cyan,9);
            staminaRow=UiKit.Row(content,"Stamina Values",8);UiKit.Size(staminaRow,22);
            var staminaCaption=UiKit.Label(staminaRow,"Stamina Caption",UiText.HudStamina,18,UiTheme.Muted);UiKit.Size(staminaCaption,22,flexibleWidth:1);
            staminaValue=UiKit.Label(staminaRow,"Stamina Value","",18);UiKit.Size(staminaValue,22,84);staminaValue.alignment=TextAlignmentOptions.MidlineRight;
            staminaBar=UiKit.Bar(content,"Stamina Gauge",UiTheme.Green,7);
            harpoonTip=Line(content,"Harpoon Controls",UiText.HudHarpoonTip,17,UiTheme.Muted,48);

            cargoPanel=Instrument(Root,"HUD Cargo",new Vector2(1,0),new Vector2(1,0),new Vector2(-28,284),new Vector2(390,138),out content);
            Line(content,"Cargo Heading",UiText.HudCargo,18,UiTheme.Muted,24);
            cargoCount=Line(content,"Cargo Count","",20,UiTheme.Text,28);
            cargoBar=UiKit.Bar(content,"Cargo Progress",UiTheme.Amber,6);
            cargoPhase=Line(content,"Cargo Phase","",18,UiTheme.Muted,25);

            reticle=UiKit.Label(Root,"HUD Reticle","+",23,UiTheme.Text);UiKit.Place(reticle.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(28,28));reticle.alignment=TextAlignmentOptions.Center;
            promptPanel=Instrument(Root,"HUD Context",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,44),new Vector2(640,88),out content);
            promptLabel=Line(content,"Context Action","",21,UiTheme.Text,28);promptLabel.alignment=TextAlignmentOptions.Center;
            promptDetail=Line(content,"Context Reason","",17,UiTheme.Muted,24);promptDetail.alignment=TextAlignmentOptions.Center;
            rescueBar=UiKit.Bar(content,"Rescue Progress",UiTheme.Green,8);
            rescueLabel=Line(content,"Rescue Confirmation","",17,UiTheme.Green,24);rescueLabel.alignment=TextAlignmentOptions.Center;

            noticePanel=Instrument(Root,"HUD Alert",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-128),new Vector2(710,110),out content);
            noticeEdge=noticePanel.GetComponent<Image>();
            noticeTitle=Line(content,"Alert Title","",23,UiTheme.Amber,31);noticeTitle.alignment=TextAlignmentOptions.Center;
            noticeDetail=Line(content,"Alert Detail","",18,UiTheme.Text,44);noticeDetail.alignment=TextAlignmentOptions.Center;
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
            UiKit.Set(depthLabel,UiText.HudDepth(player.Depth,context.World.CurrentDepth,context.AtBase));
            UiKit.Set(depthState,UiText.HudDepthState(player.IsInside));

            float capacity=Mathf.Max(1,player.tuning.oxygenCapacity*player.OxygenMultiplier);
            float oxygenFraction=Mathf.Clamp01(player.Oxygen/capacity);
            Color oxygenColor=player.IsHelpless?UiTheme.Red:player.IsInside?UiTheme.Green:oxygenFraction<=.1f?UiTheme.Red:oxygenFraction<=.2f?UiTheme.Amber:UiTheme.Cyan;
            UiKit.Set(oxygenLabel,UiText.HudOxygen(player.Oxygen,capacity));SetColor(oxygenBar,oxygenColor);UiKit.Fraction(oxygenBar,oxygenFraction);
            UiKit.Set(vitalsState,player.IsInside?UiText.HudInside:UiText.HudOutside);vitalsState.color=player.IsInside?UiTheme.Green:UiTheme.Muted;
            UiKit.Set(stressLabel,UiText.HudStress(player.PressureStress,player.IsInside));
            Color stressColor=player.IsInside?UiTheme.Muted:player.PressureStress>=player.tuning.stressDanger?UiTheme.Red:player.PressureStress>=player.tuning.stressWarning?UiTheme.Amber:UiTheme.Cyan;
            SetColor(stressBar,stressColor);UiKit.Fraction(stressBar,player.PressureStress/100);SetColor(vitalsEdge,player.IsHelpless?UiTheme.Red:UiTheme.Edge);

            RefreshHarpoon(context);
            RefreshContext(context);
            hungerPanel.gameObject.SetActive(view!=null && tuning!=null);
            if(view!=null && tuning!=null)
            {
                int foodRequired=context.FoodRequired;
                UiKit.Set(hungerTime,UiText.HudHungerTime(view.hungerRemaining));
                hungerTime.color=context.Threats!=null && context.Threats.WaveActive?UiTheme.Red:view.hungerRemaining<=30?UiTheme.Amber:UiTheme.Text;
                UiKit.Set(foodLabel,UiText.HudFood(view.offering,foodRequired));
                SetColor(foodBar,view.offering>=foodRequired?UiTheme.Green:UiTheme.Cyan);UiKit.Fraction(foodBar,(float)view.offering/Mathf.Max(1,foodRequired));
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
                UiKit.Set(slot.Name,UiText.PlayerName(player));UiKit.Set(slot.State,UiText.PlayerState(player));
                var color=player.IsHelpless||player.IsTrapped?UiTheme.Red:player.IsInside?UiTheme.Green:UiTheme.Cyan;
                SetColor(slot.Mark,color);slot.State.color=color;
            }
            for(int i=count;i<crew.Length;i++)crew[i].Root.gameObject.SetActive(false);
            UiKit.Set(crewHeading,UiText.HudCrewCount(count));
            crewPanel.sizeDelta=new Vector2(390,56+count*36);
        }

        void RefreshHarpoon(UiContext context)
        {
            var fish=context.Harpoon==null?null:context.Harpoon.HookedFish;
            bool hooked=fish!=null && fish.Mode>=FishMode.Hooked && fish.Mode<=FishMode.Tired;
            bool captured=fish!=null && fish.Mode==FishMode.Captured;
            harpoonPanel.gameObject.SetActive(hooked||captured);
            tensionRow.gameObject.SetActive(hooked);tensionBar.transform.parent.gameObject.SetActive(hooked);
            staminaRow.gameObject.SetActive(hooked);staminaBar.transform.parent.gameObject.SetActive(hooked);
            UiKit.Size(harpoonTip,captured?26:48);harpoonPanel.sizeDelta=new Vector2(390,captured?94:238);
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
            cargoPanel.anchoredPosition=new Vector2(-28,harpoonPanel.gameObject.activeSelf?harpoonPanel.sizeDelta.y+46:28);
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
                    prompt=UiText.HudTerminalPrompt;detail=context.AtBase?UiText.HudTerminalBaseHint:UiText.HudTerminalDiveHint;break;
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
            promptPanel.sizeDelta=new Vector2(640,rescuing?136:detail.Length>0?88:58);
            reticle.color=context.AimedFish!=null && context.AimedFish.Mode==FishMode.Roam?UiTheme.Cyan:UiTheme.Text;
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
            if(loss>previousLoss && (wave||previousWave))app.Toast(UiText.HudStockLost(loss-previousLoss),true);
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
        static void SetColor(Image image,Color color) {if(image.color!=color)image.color=color;}
    }
}
