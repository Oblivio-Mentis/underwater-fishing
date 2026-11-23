using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    /// <summary>Read-only presentation of the confirmed stock and existing world commands.</summary>
    public sealed class TerminalView
    {
        public RectTransform Root {get;private set;}
        readonly IUiController app;
        readonly TMP_Text[] resources=new TMP_Text[4];
        readonly Dictionary<string,Button> tabs=new Dictionary<string,Button>();
        readonly List<Action> refreshRows=new List<Action>();
        readonly TMP_Text summary,footer,shellTitle;
        readonly RectTransform tabRow;
        readonly RectTransform content;
        readonly ScrollRect scroll;
        string tab="catch",builtKey;
        TMP_Text feedingSummary,feedingTime,feedingState,feedingThreat,feedingLoss;
        Image feedingBar;
        readonly RectTransform receipt;
        readonly TMP_Text receiptHeader;
        readonly TMP_Text[] receiptRows=new TMP_Text[4];
        readonly Image receiptBar;
        readonly CanvasGroup receiptFade;
        RaidWorld observedWorld;
        MaterialCost receiptTotal,receiptBefore,receiptAfter;
        float receiptStarted=-100;
        static readonly string[] MaterialNames={"Биомасса","Хитин","Лом","Электроника"};
        static int MaterialAt(MaterialCost amount,int index) => index switch
        {0=>amount.biomass,1=>amount.chitin,2=>amount.scrap,_=>amount.electronics};

        public TerminalView(Transform parent,IUiController app)
        {
            this.app=app;RectTransform root;
            var layout=TerminalParts.Shell(parent,"TerminalView",UiText.TerminalTitle,UiText.TerminalKicker,app,out root);Root=root;
            shellTitle=layout.Find("Heading/Titles/Title").GetComponent<TMP_Text>();
            var resourceRow=UiKit.Row(layout,"Resources",12);UiKit.Size(resourceRow,66);
            string[] names={UiText.BiomassLabel,UiText.ChitinLabel,UiText.ScrapLabel,UiText.ElectronicsLabel};
            for(int i=0;i<4;i++)
            {
                var panel=UiKit.Panel(resourceRow,"Resource"+i,UiTheme.Panel);UiKit.Tile(panel);UiKit.Size(panel,66,flexibleWidth:1);
                var body=UiKit.Column(panel.transform,"Layout",0);UiKit.Fill(body,10);
                TerminalParts.Text(body,"Name",names[i],22,17,UiTheme.Muted);resources[i]=TerminalParts.Text(body,"Amount","—",28,26);
            }
            summary=TerminalParts.Text(layout,"Summary",UiText.NoConfirmedState,30,21,UiTheme.Muted);
            tabRow=UiKit.Row(layout,"Tabs",8);UiKit.Size(tabRow,50);
            AddTab(tabRow,"catch",UiText.CatchTab);AddTab(tabRow,"depths",UiText.DepthsTab);AddTab(tabRow,"feeding",UiText.FeedingTab);
            scroll=TerminalParts.Body(layout,out content);
            footer=TerminalParts.Text(layout,"Footer","",40,19,UiTheme.Muted);
            var receiptPanel=UiKit.Panel(Root,"Processing receipt",new Color(.035f,.10f,.12f,.98f));
            UiKit.Place(receiptPanel.rectTransform,Vector2.one,Vector2.one,new Vector2(-35,-245),new Vector2(485,176));
            receipt=receiptPanel.rectTransform;
            receiptFade=receipt.gameObject.AddComponent<CanvasGroup>();
            receiptFade.blocksRaycasts=false;
            var ledger=UiKit.Column(receipt,"Resource intake",5);
            UiKit.Fill(ledger,14);
            receiptHeader=TerminalParts.Text(ledger,"Header","",32,23,UiTheme.Cyan);
            for(int i=0;i<receiptRows.Length;i++)
                receiptRows[i]=TerminalParts.Text(ledger,"Receipt "+i,"",21,19,UiTheme.Text);
            receiptBar=UiKit.Panel(ledger,"Intake progress",UiTheme.Cyan);
            receiptBar.type=Image.Type.Filled;
            receiptBar.fillMethod=Image.FillMethod.Horizontal;
            UiKit.Size(receiptBar,3);
            receipt.gameObject.SetActive(false);
            Root.gameObject.SetActive(false);
        }
        void AddTab(Transform row,string key,string label)
        {
            tabs.Add(key,UiKit.Button(row,"Tab_"+key,label,()=>SelectTab(key),false,50));
        }
        public void SelectTab(string key)
        {
            if(!tabs.ContainsKey(key))return;tab=key;builtKey=null;Refresh();
        }
        public void Refresh()
        {
            if(!Root.gameObject.activeInHierarchy)return;
            var ctx=app.Context;var view=ctx.View;
            var station=ctx.SelectedConsole;
            bool navigation=station!=null&&station.kind==ShipConsoleKind.Depths;
            if(navigation)tab="depths";
            tabRow.gameObject.SetActive(!navigation);
            UiKit.Set(shellTitle,station==null?UiText.TerminalTitle:station.Caption.ToUpperInvariant());
            foreach(var pair in tabs)
            {
                pair.Value.gameObject.SetActive(station==null||station.kind!=ShipConsoleKind.Reservoir||pair.Key=="catch"||pair.Key=="feeding");
                bool selected=pair.Key==tab;pair.Value.GetComponent<Image>().color=selected?UiTheme.Cyan:UiTheme.Panel;
                pair.Value.GetComponentInChildren<TMP_Text>().color=selected?UiTheme.Steel:UiTheme.Text;
            }
            if(view==null)
            {
                foreach(var text in resources)UiKit.Set(text,"—");UiKit.Set(summary,UiText.NoConfirmedState);UiKit.Set(footer,UiText.NoConfirmedState);
            }
            else
            {
                UiKit.Set(resources[0],view.biomass.ToString());UiKit.Set(resources[1],view.chitin.ToString());UiKit.Set(resources[2],view.scrap.ToString());UiKit.Set(resources[3],view.electronics.ToString());
                UiKit.Set(summary,UiText.TerminalSummary(view,ctx.Tuning,ctx.FoodRequired,ctx.DefendingReservoir));
                if(navigation)UiKit.Set(summary,"На борту: "+ctx.Players.Count(p=>p.IsInside)+" / "+ctx.Players.Count+"  ·  "+(ctx.AllInside?"Все водолазы в лодке":"Верните экипаж в лодку"));
                string reason=TerminalParts.BaseReason(app);
                UiKit.Set(footer,ctx.World.TravelRemaining>0?UiText.TravelRemaining(ctx.World.TravelRemaining):!string.IsNullOrEmpty(reason)?reason:UiText.CargoSummary(view,ctx.Tuning));
                if(station?.kind==ShipConsoleKind.Reservoir && string.IsNullOrEmpty(reason))
                    UiKit.Set(footer,view.processingRemainder.IsEmpty
                        ?"Улов без задания остаётся в резервуаре"
                        :"Дробный остаток переработки   "+YieldText(view.processingRemainder));
                footer.color=string.IsNullOrEmpty(reason)?UiTheme.Muted:UiTheme.Amber;
            }
            string key=BuildKey();if(key!=builtKey){builtKey=key;Rebuild();}
            foreach(var update in refreshRows)update();
            RefreshFeeding();
        }
        string BuildKey()
        {
            var view=app.Context.View;if(view==null)return "waiting";
            var key=new StringBuilder(tab);
            if(tab=="catch" || tab=="feeding")foreach(var item in view.tank)key.Append('|').Append(item.id);
            if(tab=="depths")key.Append('|').Append(view.highestDepth);
            return key.ToString();
        }
        void Rebuild()
        {
            UiKit.Clear(content);refreshRows.Clear();feedingSummary=feedingTime=feedingState=feedingThreat=feedingLoss=null;feedingBar=null;scroll.StopMovement();scroll.verticalNormalizedPosition=1;
            if(app.Context.View==null){TerminalParts.Text(content,"Waiting",UiText.NoConfirmedState,70);return;}
            if(tab=="catch")BuildCatch(false);else if(tab=="depths")BuildDepths();else BuildFeeding();
        }
        void BuildCatch(bool foodOnly)
        {
            var view=app.Context.View;
            var items=foodOnly?view.tank.Where(item=>item.offering>0).ToList():view.tank;
            if(items.Count==0)
            {
                var empty=TerminalParts.Card(content,"EmptyStock",152);
                TerminalParts.Text(empty,"Title",foodOnly?UiText.NoFoodStock:UiText.NoStock,40,28);
                TerminalParts.Text(empty,"Description",foodOnly?UiText.NoFoodHint:UiText.NoStockHint,66,22,UiTheme.Muted);return;
            }
            for(int i=0;i<items.Count;i++)AddItem(items[i],i,foodOnly);
        }

        void AddItem(CatchItem item,int index,bool feedOnly)
        {
            long id=item.id;
            var body=TerminalParts.Card(content,"Catch_"+id,146);
            TerminalParts.Text(body,"Title",UiText.ItemTitle(index,item),32,26);
            var yield=TerminalParts.Text(body,"Yield","",26,19,UiTheme.Muted);
            var row=UiKit.Row(body,"Actions",10);
            UiKit.Size(row,45);
            var process=UiKit.Button(row,"Process_"+id,"Переработать",()=>app.Send(WorldAction.Process,id),false,44);
            var feed=UiKit.Button(row,"Feed_"+id,"В пасть",()=>app.Send(WorldAction.Feed,id),false,44);
            var hold=UiKit.Button(row,"Hold_"+id,"Оставить",()=>app.Send(WorldAction.HoldFish,id),false,44);
            var status=TerminalParts.Text(body,"Destination","",21,17,UiTheme.Cyan);
            refreshRows.Add(()=>
            {
                var ctx=app.Context;
                var progress=ctx.View;
                var fish=progress.tank.Find(entry=>entry.id==id);
                if(fish==null)return;
                var forecast=progress.ProcessPreview(fish,ctx.Tuning);
                string resources=UiText.YieldLabel(fish);
                bool boosted=forecast.biomass!=fish.biomass*100 || forecast.chitin!=fish.chitin*100 ||
                    forecast.scrap!=fish.scrap*100 || forecast.electronics!=fish.electronics*100;
                if(boosted)resources+=" → "+YieldText(forecast);
                UiKit.Set(yield,resources+"   ·   Насыщение "+TeamProgress.FoodText(progress.FoodValue(fish,ctx.Tuning)));
                var route=ctx.World.FishRoute;
                bool travelling=false;
                for(int i=route.completed;route.active && i<route.total;i++)
                    if(ctx.World.ActiveRouteId(i)==id){travelling=true;break;}
                bool enabled=!travelling && string.IsNullOrEmpty(TerminalParts.BaseReason(app));
                process.interactable=enabled && !forecast.IsEmpty && fish.destination!=CatchDestination.Process;
                feed.interactable=enabled && fish.offering>0 && fish.destination!=CatchDestination.Feed;
                hold.interactable=enabled && fish.destination!=CatchDestination.Hold;
                UiKit.Set(status,travelling?"В трубе":fish.destination switch
                {
                    CatchDestination.Process=>"В очереди на переработку",
                    CatchDestination.Feed=>"В очереди на кормление",
                    _=>"В резервуаре"
                });
            });
        }

        static string YieldText(MaterialCost points) =>
            TeamProgress.FoodText(points.biomass)+" Б  "+TeamProgress.FoodText(points.chitin)+" Х  "+
            TeamProgress.FoodText(points.scrap)+" Л  "+TeamProgress.FoodText(points.electronics)+" Э";

        void OnProcessingFinished(MaterialCost total,MaterialCost before,MaterialCost after)
        {
            receiptBefore=before;receiptTotal=total;
            receiptAfter=after;
            receiptStarted=Time.unscaledTime;
        }

        public void UpdateReceipt()
        {
            var world=app.Context.World;
            if(observedWorld!=world)
            {
                if(observedWorld!=null)observedWorld.ProcessingFinished-=OnProcessingFinished;
                observedWorld=world;
                if(world!=null)world.ProcessingFinished+=OnProcessingFinished;
                receiptStarted=-100;
            }
            float elapsed=Time.unscaledTime-receiptStarted;
            bool show=Root.gameObject.activeInHierarchy && (tab=="catch" || tab=="feeding") && elapsed<6f;
            receipt.gameObject.SetActive(show);
            if(!show)return;
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/1.2f));
            receiptFade.alpha=Mathf.Clamp01(elapsed*7)*Mathf.Clamp01(6-elapsed);
            receipt.anchoredPosition=new Vector2(-35,-245+20*(1-reveal));
            receiptBar.fillAmount=reveal;
            UiKit.Set(receiptHeader,"ПЕРЕРАБОТКА · КАЗНА NEREID");
            for(int i=0;i<receiptRows.Length;i++)
            {
                int old=MaterialAt(receiptBefore,i);
                int added=MaterialAt(receiptAfter,i)-old;
                int output=MaterialAt(receiptTotal,i);
                int delta=Mathf.RoundToInt(added*reveal);
                UiKit.Set(receiptRows[i],MaterialNames[i]+"  "+old+"  <color=#82E5C7>+"+delta+"</color>  →  "+(old+delta)+"   <color=#A5BBC4>из "+output+"</color>");
            }
        }

        void BuildDepths()
        {
            var zones=UiKit.Row(content,"DepthZones",16);UiKit.Size(zones,356);
            AddDepth(zones,1);AddDepth(zones,2);
            var cargo=TerminalParts.Card(content,"CargoInformation",96);
            TerminalParts.Text(cargo,"Title",UiText.CargoFromTerminal,30,24);
            TerminalParts.Text(cargo,"Description",UiText.CargoFromTerminalHint,36,20,UiTheme.Muted);
        }
        void AddDepth(Transform parent,int depth)
        {
            var body=TerminalParts.Card(parent,"Depth_"+depth,356);
            TerminalParts.Text(body,"Title",depth==1?UiText.DepthOneTitle:UiText.DepthTwoTitle,36,28);
            TerminalParts.Text(body,"Loot",depth==1?UiText.DepthOneLoot:UiText.DepthTwoLoot,70,22);
            TerminalParts.Text(body,"Risk",depth==1?UiText.DepthOneRisk:UiText.DepthTwoRisk,54,20,UiTheme.Muted);
            var access=TerminalParts.Text(body,"Access","",50,20,UiTheme.Muted);
            var reason=TerminalParts.Text(body,"Reason","",26,19,UiTheme.Amber);
            var button=UiKit.Button(body,"Dive_"+depth,UiText.StartExpedition,()=>ConfirmDepth(depth),true,52);
            refreshRows.Add(()=>
            {
                var ctx=app.Context;if(ctx.View==null){button.interactable=false;return;}
                bool unlocked=ctx.View.CanVisitDepth(depth);
                UiKit.Set(access,unlocked?UiText.DepthAvailable:UiText.HullUnlock);access.color=unlocked?UiTheme.Green:UiTheme.Amber;
                TerminalParts.ButtonReason(button,reason,TravelReason(depth),"");
            });
        }
        string TravelReason(int destination)
        {
            string reason=TerminalParts.BaseReason(app);if(!string.IsNullOrEmpty(reason))return reason;
            var ctx=app.Context;var view=ctx.View;
            if(destination==ctx.World.CurrentDepth)return UiText.AlreadyHere;
            if(!view.CanVisitDepth(destination))return UiText.DepthLocked;
            if(ctx.World.FishRoute.active)return UiText.RouteBusy;
            if(!ctx.AllInside)return UiText.NotAllInside;
            if(view.hoistPhase!=HoistPhase.Loading)return UiText.FinishCargoCycle;
            if(view.cargo.Count>0)return UiText.EmptyCargoBeforeTravel;
            return "";
        }
        void ConfirmDepth(int depth)
        {
            string reason=TravelReason(depth);if(!string.IsNullOrEmpty(reason)){app.Toast(reason,true);return;}
            app.Confirm(UiText.DiveConfirmTitle,(depth==1?UiText.DepthOneTitle:UiText.DepthTwoTitle)+"\n\n"+UiText.DiveConfirmHint,
                ()=>TerminalParts.Execute(app,()=>TravelReason(depth),depth==1?WorldAction.DiveOne:WorldAction.DiveTwo));
        }
        void BuildFeeding()
        {
            var body=TerminalParts.Card(content,"FeedingStatus",296);
            TerminalParts.Text(body,"Title",UiText.HungerTitle,34,26);
            feedingSummary=TerminalParts.Text(body,"Food","",32,24,UiTheme.Cyan);
            feedingBar=UiKit.Bar(body,"FoodProgress",UiTheme.Cyan,12);
            feedingTime=TerminalParts.Text(body,"Countdown","",28,22,UiTheme.Muted);
            feedingState=TerminalParts.Text(body,"Projection","",30,22,UiTheme.Amber);
            feedingThreat=TerminalParts.Text(body,"Threat","",56,20,UiTheme.Muted);
            feedingLoss=TerminalParts.Text(body,"StockLoss","",28,20,UiTheme.Muted);
            var explanation=TerminalParts.Card(content,"FeedingOutcomes",250);
            TerminalParts.Text(explanation,"Title",UiText.FeedingOutcomes,30,24);
            TerminalParts.Text(explanation,"None",UiText.OutcomeNone,30,20,UiTheme.Muted);
            TerminalParts.Text(explanation,"Under",UiText.OutcomeUnder,30,20,UiTheme.Muted);
            TerminalParts.Text(explanation,"Enough",UiText.OutcomeEnough,32,20,UiTheme.Muted);
            TerminalParts.Text(explanation,"Over",UiText.OverfeedingRule(app.Context.View.TeamValue(SkillLine.Carryover),app.Context.Tuning.maximumFoodCarry),54,20,UiTheme.Muted);
            TerminalParts.Text(content,"FeedingHint",UiText.FeedingHint,50,21,UiTheme.Muted);
            BuildCatch(true);
        }
        void RefreshFeeding()
        {
            if(feedingSummary==null || app.Context.View==null)return;var ctx=app.Context;var view=ctx.View;
            UiKit.Set(feedingSummary,TeamProgress.FoodText(view.offeringPoints)+" / "+ctx.FoodRequired+" ед. питания");UiKit.Fraction(feedingBar,(float)view.offeringPoints/Mathf.Max(1,ctx.FoodRequired*TeamProgress.FoodScale));
            bool wave=ctx.DefendingReservoir;
            UiKit.Set(feedingTime,wave?"Срок кормления истёк":UiText.HungerCountdown(view.hungerRemaining));
            feedingTime.color=!wave&&view.offeringPoints==0&&view.hungerRemaining<=30?UiTheme.Amber:UiTheme.Muted;
            UiKit.Set(feedingState,wave?"Следующий срок начнётся после обороны":UiText.FoodProjection(view.offeringPoints,ctx.FoodRequired*TeamProgress.FoodScale));
            feedingState.color=wave?UiTheme.Muted:view.offeringPoints>0?UiTheme.Green:UiTheme.Amber;
            UiKit.Set(feedingThreat,wave?UiText.WaveActiveTitle:"");
            feedingThreat.gameObject.SetActive(wave);
            feedingThreat.color=UiTheme.Red;
            UiKit.Set(feedingLoss,UiText.SessionStockLoss(ctx.World.StockLossCount));feedingLoss.color=ctx.World.StockLossCount>0?UiTheme.Amber:UiTheme.Muted;
        }
    }
}
