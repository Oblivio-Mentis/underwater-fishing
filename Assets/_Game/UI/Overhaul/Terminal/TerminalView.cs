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
    public sealed partial class TerminalView
    {
        public RectTransform Root {get;private set;}
        readonly IUiController app;
        readonly MaterialStrip resources;
        readonly Dictionary<string,Button> tabs=new Dictionary<string,Button>();
        readonly List<Action> refreshRows=new List<Action>();
        readonly TMP_Text summary,footer,shellTitle;
        readonly RectTransform tabRow,dispatchRow;
        readonly Button sendFish;
        readonly TMP_Text dispatchSummary;
        readonly RectTransform content;
        readonly ScrollRect scroll;
        string tab="catch",builtKey;
        TMP_Text feedingSummary,feedingTime,feedingState,feedingThreat,feedingLoss;
        Image feedingBar;

        public TerminalView(Transform parent,IUiController app)
        {
            this.app=app;RectTransform root;
            var layout=TerminalParts.Shell(parent,"TerminalView",UiText.TerminalTitle,UiText.TerminalKicker,app,out root);Root=root;
            shellTitle=layout.Find("Heading/Titles/Title").GetComponent<TMP_Text>();
            resources=new MaterialStrip(layout,"Team treasury",true);
            summary=TerminalParts.Text(layout,"Summary",UiText.NoConfirmedState,30,21,UiTheme.Muted);
            tabRow=UiKit.Row(layout,"Tabs",8);UiKit.Size(tabRow,50);
            AddTab(tabRow,"catch",UiText.CatchTab);AddTab(tabRow,"depths",UiText.DepthsTab);AddTab(tabRow,"feeding",UiText.FeedingTab);
            dispatchRow=UiKit.Row(layout,"Dispatch controls",14);
            UiKit.Size(dispatchRow,54);
            dispatchSummary=UiKit.Label(dispatchRow,"Selection summary","",21);
            UiKit.Size(dispatchSummary,50,flexibleWidth:1);
            sendFish=UiKit.Button(dispatchRow,"SendFish","Отправить выбранную рыбу",SendSelection,true,52);
            UiKit.Size(sendFish,52,340).flexibleWidth=0;
            scroll=TerminalParts.Body(layout,out content);
            footer=TerminalParts.Text(layout,"Footer","",40,19,UiTheme.Muted);
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
            resources.Balances(view);
            dispatchRow.gameObject.SetActive(station?.kind==ShipConsoleKind.Reservoir && (tab=="catch" || tab=="feeding"));
            if(dispatchRow.gameObject.activeSelf)RefreshDispatch();
            if(view==null)
            {
                UiKit.Set(summary,UiText.NoConfirmedState);UiKit.Set(footer,UiText.NoConfirmedState);
            }
            else
            {
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
        void RefreshDispatch()
        {
            var ctx=app.Context;
            var view=ctx.View;
            string reason=TerminalParts.BaseReason(app);
            if(!string.IsNullOrEmpty(reason)){TerminalParts.ButtonReason(sendFish,dispatchSummary,reason,"");return;}
            var route=ctx.World.FishRoute;
            if(route.active || ctx.World.HasFishDispatch)
            {
                dispatchSummary.color=UiTheme.Cyan;
                UiKit.Set(dispatchSummary,route.active
                    ? $"{(route.feeding?"Кормление":"Переработка")} · {route.completed} / {route.total}"
                    : "Подача готовится");
                sendFish.interactable=false;
                return;
            }
            int process=view.tank.Count(item=>FishRouteQuote.IsDraft(item,CatchDestination.Process));
            int feed=view.tank.Count(item=>FishRouteQuote.IsDraft(item,CatchDestination.Feed));
            dispatchSummary.color=UiTheme.Muted;
            UiKit.Set(dispatchSummary,process+feed==0?"Выберите назначение для рыб":$"Переработка {process}   ·   Кормление {feed}");
            sendFish.interactable=process+feed>0;
            sendFish.GetComponentInChildren<TMP_Text>().color=sendFish.interactable?UiTheme.Steel:UiTheme.Text;
        }

        void SendSelection()
        {
            var ctx=app.Context;
            if(ctx.View==null || ctx.World.HasFishDispatch)return;
            long quote=FishRouteQuote.ComputeDraft(ctx.View.tank);
            if(quote!=0)app.Send(WorldAction.SendFish,quote);
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
            if(app.Context.SelectedConsole?.kind==ShipConsoleKind.Reservoir)
            {
                AddRoutingControls(items);
                if(!individualFish)
                {
                    foreach(var group in items.GroupBy(item=>item.species).OrderBy(group=>UiText.ItemName(group.First())))
                        AddSpeciesGroup(group.First(),foodOnly);
                    return;
                }
            }
            foreach(var item in items)AddItem(item);
        }

        void AddItem(CatchItem item)
        {
            long id=item.id;
            var body=TerminalParts.Card(content,"Catch_"+id,151);
            var title=UiKit.Row(body,"Catch details",10);
            UiKit.Size(title,35);
            UiKit.Size(UiKit.Symbol(title,"Species",DiveInstruments.CatchMark(item.species),Color.white),35,40);
            UiKit.Size(UiKit.Label(title,"Title",UiText.ItemName(item),25),35,flexibleWidth:1);
            var food=UiKit.Label(title,"Saturation","",21,UiTheme.Amber);
            UiKit.Size(food,35,220);
            var forecastStrip=new MaterialStrip(body,"Processing yield");
            var row=UiKit.Row(body,"Actions",10);
            UiKit.Size(row,44);
            var process=UiKit.Button(row,"Process_"+id,"На переработку",()=>app.Send(WorldAction.Process,id),false,44);
            var feed=UiKit.Button(row,"Feed_"+id,"В пасть",()=>app.Send(WorldAction.Feed,id),false,44);
            var hold=UiKit.Button(row,"Hold_"+id,"Оставить",()=>app.Send(WorldAction.HoldFish,id),false,44);
            var status=UiKit.Label(row,"Destination","",18,UiTheme.Cyan);
            UiKit.Size(status,44,240);
            refreshRows.Add(()=>
            {
                var ctx=app.Context;
                var progress=ctx.View;
                var fish=progress.tank.Find(entry=>entry.id==id);
                if(fish==null)return;
                var forecast=progress.ProcessPreview(fish,ctx.Tuning);
                forecastStrip.Quote(forecast,true);
                UiKit.Set(food,"Насыщение  "+TeamProgress.FoodText(progress.FoodValue(fish,ctx.Tuning)));
                bool travelling=ctx.World.IsInFishPipe(id);
                bool enabled=!fish.dispatchCommitted && !travelling && string.IsNullOrEmpty(TerminalParts.BaseReason(app));
                process.interactable=enabled && !forecast.IsEmpty && fish.destination!=CatchDestination.Process;
                feed.interactable=enabled && fish.offering>0 && fish.destination!=CatchDestination.Feed;
                hold.interactable=enabled && fish.destination!=CatchDestination.Hold;
                var destination=fish.destination;
                UiKit.Set(status,travelling?"В трубе":fish.dispatchCommitted?"Подача готовится":destination switch
                {
                    CatchDestination.Process=>"Выбрана переработка",
                    CatchDestination.Feed=>"Выбрано кормление",
                    _=>"В резервуаре"
                });
                process.GetComponent<Image>().color=destination==CatchDestination.Process?UiTheme.Hex("305F5D"):UiTheme.Panel;
                feed.GetComponent<Image>().color=destination==CatchDestination.Feed?UiTheme.Hex("614A2E"):UiTheme.Panel;
            });
        }

        static string YieldText(MaterialCost points) =>
            TeamProgress.FoodText(points.biomass)+" Б  "+TeamProgress.FoodText(points.chitin)+" Х  "+
            TeamProgress.FoodText(points.scrap)+" Л  "+TeamProgress.FoodText(points.electronics)+" Э";

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
            if(ctx.World.HasFishDispatch)return UiText.RouteBusy;
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
            UiKit.Set(feedingSummary,TeamProgress.FoodText(view.offeringPoints)+" / "+TeamProgress.FoodText(ctx.FoodRequired)+" ед. питания");UiKit.Fraction(feedingBar,(float)view.offeringPoints/Mathf.Max(1,ctx.FoodRequired));
            bool wave=ctx.DefendingReservoir;
            float untilRisk=view.HungerUntilRisk(ctx.Tuning);
            UiKit.Set(feedingTime,wave?"Срок кормления истёк":UiText.HungerForecast(untilRisk));
            feedingTime.color=!wave&&untilRisk<=0?UiTheme.Red:UiTheme.Muted;
            UiKit.Set(feedingState,wave?"Следующий срок начнётся после обороны":UiText.FoodProjection(view.offeringPoints,ctx.FoodRequired));
            feedingState.color=wave?UiTheme.Muted:view.offeringPoints>0?UiTheme.Green:UiTheme.Amber;
            UiKit.Set(feedingThreat,wave?UiText.WaveActiveTitle:"");
            feedingThreat.gameObject.SetActive(wave);
            feedingThreat.color=UiTheme.Red;
            UiKit.Set(feedingLoss,UiText.SessionStockLoss(ctx.World.StockLossCount));feedingLoss.color=ctx.World.StockLossCount>0?UiTheme.Amber:UiTheme.Muted;
        }
    }
}
