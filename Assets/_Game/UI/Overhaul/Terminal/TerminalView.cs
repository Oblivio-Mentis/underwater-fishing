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
        readonly MaterialStrip wallet;
        readonly Image stationIcon;
        readonly Dictionary<string,Button> tabs=new Dictionary<string,Button>();
        readonly List<Action> refreshRows=new List<Action>();
        readonly TMP_Text summary,footer,shellTitle;
        readonly RectTransform tabRow;
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
            stationIcon=layout.Find("Heading/Station symbol").GetComponent<Image>();
            TerminalParts.Text(layout,"Ledger heading","КАЗНА ЭКИПАЖА",21,17,UiTheme.Muted);
            wallet=new MaterialStrip(layout,"Treasury",true);
            summary=TerminalParts.Text(layout,"Summary",UiText.NoConfirmedState,30,21,UiTheme.Muted);
            tabRow=UiKit.Row(layout,"Tabs",8);UiKit.Size(tabRow,50);
            AddTab(tabRow,"catch",UiText.CatchTab);AddTab(tabRow,"upgrades",UiText.UpgradesTab);AddTab(tabRow,"depths",UiText.DepthsTab);AddTab(tabRow,"feeding",UiText.FeedingTab);
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
            bool equipment=station!=null&&station.kind==ShipConsoleKind.Upgrades;
            bool navigation=station!=null&&station.kind==ShipConsoleKind.Depths;
            if(equipment)tab="upgrades";else if(navigation)tab="depths";
            tabRow.gameObject.SetActive(!equipment&&!navigation);
            UiKit.Set(shellTitle,station==null?UiText.TerminalTitle:station.Caption.ToUpperInvariant());
            stationIcon.sprite=DiveInstruments.Art(equipment?DiveInstruments.Mark.Wrench:
                navigation?DiveInstruments.Mark.Bathymetry:DiveInstruments.Mark.Submarine);
            wallet.Balances(view);
            foreach(var pair in tabs)
            {
                pair.Value.gameObject.SetActive(station==null||station.kind!=ShipConsoleKind.Reservoir||pair.Key=="catch"||pair.Key=="feeding");
                bool selected=pair.Key==tab;pair.Value.GetComponent<Image>().color=selected?UiTheme.Cyan:UiTheme.Panel;
                pair.Value.GetComponentInChildren<TMP_Text>().color=selected?UiTheme.Steel:UiTheme.Text;
            }
            if(view==null)
            {
                UiKit.Set(summary,UiText.NoConfirmedState);UiKit.Set(footer,UiText.NoConfirmedState);
            }
            else
            {
                UiKit.Set(summary,UiText.TerminalSummary(view,ctx.FoodRequired,ctx.DefendingReservoir));
                if(equipment)UiKit.Set(summary,"Бортовая мастерская · оборудование экипажа");
                if(navigation)UiKit.Set(summary,"На борту: "+ctx.Players.Count(p=>p.IsInside)+" / "+ctx.Players.Count+"  ·  "+(ctx.AllInside?"Все водолазы в лодке":"Верните экипаж в лодку"));
                string reason=TerminalParts.BaseReason(app);
                UiKit.Set(footer,ctx.World.TravelRemaining>0?UiText.TravelRemaining(ctx.World.TravelRemaining):!string.IsNullOrEmpty(reason)?reason:UiText.CargoSummary(view,ctx.Tuning));
                if(equipment&&string.IsNullOrEmpty(reason))UiKit.Set(footer,"Улучшения действуют на весь экипаж");
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
            if(tab=="upgrades")foreach(Upgrade upgrade in Enum.GetValues(typeof(Upgrade)))key.Append('|').Append(view.Level(upgrade));
            if(tab=="depths")key.Append('|').Append(view.highestDepth).Append('|').Append(view.hullLevel);
            return key.ToString();
        }
        void Rebuild()
        {
            UiKit.Clear(content);refreshRows.Clear();feedingSummary=feedingTime=feedingState=feedingThreat=feedingLoss=null;feedingBar=null;scroll.StopMovement();scroll.verticalNormalizedPosition=1;
            if(app.Context.View==null){TerminalParts.Text(content,"Waiting",UiText.NoConfirmedState,70);return;}
            if(tab=="catch")BuildCatch(false);else if(tab=="upgrades")BuildUpgrades();else if(tab=="depths")BuildDepths();else BuildFeeding();
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
            if(!foodOnly)TerminalParts.Text(content,"ListHint","В РЕЗЕРВУАРЕ  /  "+items.Count,27,18,UiTheme.Muted);
            if(ShipConsole.Stations.Count>0 && items.Any(item=>item.offering>0))
            {
                var hint=TerminalParts.Text(content,"RouteHint","",32,20,UiTheme.Cyan);
                refreshRows.Add(()=>UiKit.Set(hint,app.Context.World.FishRoute.active?UiText.RouteBusy:"Подача всей рыбы: пульты по сторонам резервуара"));
            }
            for(int i=0;i<items.Count;i++)AddItem(items[i],i,foodOnly);
        }
        void AddItem(CatchItem item,int index,bool feedOnly)
        {
            bool bulk=ShipConsole.Stations.Count>0 && item.offering>0;
            long id=item.id;
            var body=TerminalParts.Card(content,"Catch_"+id,bulk?105:173);
            var heading=UiKit.Row(body,"TitleRow",12);UiKit.Size(heading,32);
            var silhouette=UiKit.Symbol(heading,"Specimen",item.offering>0||item.biomass>0||item.chitin>0?
                DiveInstruments.CatchMark(item.species):DiveInstruments.Mark.Crate,Color.white);
            UiKit.Size(silhouette,32,46);
            var title=UiKit.Label(heading,"Title",UiText.CargoName(item),26);UiKit.Size(title,32,flexibleWidth:1);
            title.fontStyle=FontStyles.Bold;
            var depth=UiKit.Label(heading,"Horizon","Горизонт "+item.depthTier,18,UiTheme.Muted);
            UiKit.Size(depth,32,130);
            var food=UiKit.Label(heading,"Food",UiText.FoodValue(item.offering),21,item.offering>0?UiTheme.Green:UiTheme.Muted);
            UiKit.Size(food,32,132);
            var yieldRow=new MaterialStrip(body,"Yield");
            yieldRow.Quote(MaterialStrip.Yield(item));
            if(bulk)return;
            var actions=UiKit.Row(body,"Actions",12);UiKit.Size(actions,44);
            var reason=TerminalParts.Text(body,"Reason","",25,19,UiTheme.Muted);
            if(!feedOnly && item.biomass+item.chitin+item.scrap+item.electronics>0)
            {
                Func<string> check=()=>ItemReason(id,false);
                var process=UiKit.Button(actions,"Process_"+id,UiText.ProcessAction,()=>ConfirmItem(id,false),false,44);
                refreshRows.Add(()=>process.interactable=string.IsNullOrEmpty(check()));
            }
            if(item.offering>0)
            {
                Func<string> check=()=>ItemReason(id,true);
                var feed=UiKit.Button(actions,"Feed_"+id,UiText.FeedAction,()=>ConfirmItem(id,true),true,44);
                refreshRows.Add(()=>feed.interactable=string.IsNullOrEmpty(check()));
            }
            refreshRows.Add(()=>
            {
                string unavailable=TerminalParts.BaseReason(app);
                UiKit.Set(reason,string.IsNullOrEmpty(unavailable)?UiText.ItemUnits(item.cargoSize):unavailable);
                reason.color=string.IsNullOrEmpty(unavailable)?UiTheme.Muted:UiTheme.Amber;
            });
        }
        string ItemReason(long id,bool feed)
        {
            string reason=TerminalParts.BaseReason(app);if(!string.IsNullOrEmpty(reason))return reason;
            var item=app.Context.View.tank.Find(x=>x.id==id);if(item==null)return UiText.ItemGone;
            if(feed)return item.offering>0?"":UiText.CannotFeed;
            return item.biomass+item.chitin+item.scrap+item.electronics>0?"":UiText.NoYield;
        }
        void ConfirmItem(long id,bool feed)
        {
            string reason=ItemReason(id,feed);if(!string.IsNullOrEmpty(reason)){app.Toast(reason,true);return;}
            var item=app.Context.View.tank.Find(x=>x.id==id);
            string description=UiText.ItemName(item)+(feed?"\n"+UiText.FoodValue(item.offering):"")+"\n\n"+UiText.ConsumeWarning;
            app.Confirm(feed?UiText.FeedTitle:UiText.ProcessTitle,description,
                ()=>TerminalParts.Execute(app,()=>ItemReason(id,feed),feed?WorldAction.Feed:WorldAction.Process,id),
                feed?null:MaterialStrip.Yield(item));
        }
        void BuildUpgrades()
        {
            foreach(Upgrade value in Enum.GetValues(typeof(Upgrade)))
            {
                Upgrade upgrade=value;var ctx=app.Context;var view=ctx.View;var recipe=ctx.Tuning.Recipe(upgrade);
                int level=view.Level(upgrade);var body=TerminalParts.Card(content,"Upgrade_"+upgrade,222);
                var heading=UiKit.Row(body,"Heading",12);UiKit.Size(heading,34);
                var title=UiKit.Label(heading,"Name",UiText.UpgradeName(upgrade),28);UiKit.Size(title,34,flexibleWidth:1);
                var levelText=UiKit.Label(heading,"Level",UiText.UpgradeLevel(level,recipe==null?0:recipe.maximumLevel),22,UiTheme.Cyan);UiKit.Size(levelText,34,220);
                bool maximum=recipe!=null && level>=recipe.maximumLevel;
                TerminalParts.Text(body,"Bonus",maximum?UiText.CurrentUpgradeBonus(upgrade,level,ctx):UiText.UpgradeBonus(upgrade,level,ctx),38,22,maximum?UiTheme.Green:UiTheme.Text);
                if(recipe==null){TerminalParts.Text(body,"MissingRecipe",UiText.MissingRecipe,70,22,UiTheme.Amber);continue;}
                if(maximum){TerminalParts.Text(body,"Complete",UiText.MaxUpgrade,68,22,UiTheme.Muted);continue;}
                var cost=view.Cost(upgrade,ctx.Tuning);
                var costRow=new MaterialStrip(body,"Cost");
                costRow.Quote(cost);
                var row=UiKit.Row(body,"Purchase",20);UiKit.Size(row,54);
                var status=UiKit.Label(row,"Reason","",20,UiTheme.Amber);UiKit.Size(status,54,flexibleWidth:1);
                var buy=UiKit.Button(row,"Buy_"+upgrade,UiText.BuyUpgradeLevel(level+1),()=>ConfirmUpgrade(upgrade),true,52);UiKit.Size(buy,52,210);buy.GetComponent<LayoutElement>().flexibleWidth=0;
                refreshRows.Add(()=>TerminalParts.ButtonReason(buy,status,UpgradeReason(upgrade),UiText.UpgradeReady));
            }
        }
        string UpgradeReason(Upgrade upgrade)
        {
            string reason=TerminalParts.BaseReason(app);if(!string.IsNullOrEmpty(reason))return reason;
            var ctx=app.Context;var view=ctx.View;var recipe=ctx.Tuning.Recipe(upgrade);
            if(recipe==null)return UiText.MissingRecipe;if(view.Level(upgrade)>=recipe.maximumLevel)return UiText.MaxUpgrade;
            string missing=UiText.NeedMaterials(view.Cost(upgrade,ctx.Tuning),view);
            if(upgrade==Upgrade.Hull && view.lifetimeCatch<ctx.Tuning.depthTwoLifetimeCatch)return UiText.LifetimeNeed(view.lifetimeCatch,ctx.Tuning.depthTwoLifetimeCatch)+(string.IsNullOrEmpty(missing)?"":"\n"+missing);
            return missing;
        }
        void ConfirmUpgrade(Upgrade upgrade)
        {
            string reason=UpgradeReason(upgrade);if(!string.IsNullOrEmpty(reason)){app.Toast(reason,true);return;}
            var ctx=app.Context;var view=ctx.View;
            int quotedLevel=view.Level(upgrade);var quotedCost=view.Cost(upgrade,ctx.Tuning);
            app.Confirm(UiText.BuyConfirmTitle,UiText.UpgradeName(upgrade)+"\n"+UiText.UpgradeTarget(quotedLevel+1)+"\n\n"+UiText.UpgradeConfirmHint,
                ()=>
                {
                    if(ctx.View==null||ctx.View.Level(upgrade)!=quotedLevel||!ctx.View.Cost(upgrade,ctx.Tuning).Equals(quotedCost)){app.Toast(UiText.QuoteChanged,true);return;}
                    TerminalParts.Execute(app,()=>UpgradeReason(upgrade),(WorldAction)((int)WorldAction.Reel+(int)upgrade));
                },quotedCost);
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
            TerminalParts.Text(body,"Loot",depth==1?"Риф и грузовые ящики":"Разлом и глубинная живность",36,22);
            var outlook=UiKit.Row(body,"Expected resources",8);UiKit.Size(outlook,37);
            foreach(var kind in (depth==1?new[]{DiveInstruments.Mark.Biomass,DiveInstruments.Mark.Scrap}:
                new[]{DiveInstruments.Mark.Chitin,DiveInstruments.Mark.Electronics}))
                UiKit.Size(UiKit.Symbol(outlook,kind.ToString(),kind,Color.white),35,39);
            TerminalParts.Text(body,"Risk",depth==1?UiText.DepthOneRisk:UiText.DepthTwoRisk,54,20,UiTheme.Muted);
            var access=TerminalParts.Text(body,"Access","",50,20,UiTheme.Muted);
            var reason=TerminalParts.Text(body,"Reason","",26,19,UiTheme.Amber);
            var button=UiKit.Button(body,"Dive_"+depth,UiText.StartExpedition,()=>ConfirmDepth(depth),true,52);
            refreshRows.Add(()=>
            {
                var ctx=app.Context;if(ctx.View==null){button.interactable=false;return;}
                bool unlocked=ctx.View.CanVisitDepth(depth);
                UiKit.Set(access,unlocked?UiText.DepthAvailable:UiText.HullUnlock(ctx.View.lifetimeCatch,ctx.Tuning.depthTwoLifetimeCatch));access.color=unlocked?UiTheme.Green:UiTheme.Amber;
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
            TerminalParts.Text(explanation,"Over",UiText.OverfeedingRule(app.Context.Tuning.maximumFoodCarry),54,20,UiTheme.Muted);
            TerminalParts.Text(content,"FeedingHint",UiText.FeedingHint,50,21,UiTheme.Muted);
            BuildCatch(true);
        }
        void RefreshFeeding()
        {
            if(feedingSummary==null || app.Context.View==null)return;var ctx=app.Context;var view=ctx.View;
            UiKit.Set(feedingSummary,UiText.FeedingAmount(view.offering,ctx.FoodRequired));UiKit.Fraction(feedingBar,(float)view.offering/Mathf.Max(1,ctx.FoodRequired));
            bool wave=ctx.DefendingReservoir;
            UiKit.Set(feedingTime,wave?"Срок кормления истёк":UiText.HungerCountdown(view.hungerRemaining));
            feedingTime.color=!wave&&view.offering==0&&view.hungerRemaining<=30?UiTheme.Amber:UiTheme.Muted;
            UiKit.Set(feedingState,wave?"Следующий срок начнётся после обороны":UiText.FoodProjection(view.offering,ctx.FoodRequired));
            feedingState.color=wave?UiTheme.Muted:view.offering>0?UiTheme.Green:UiTheme.Amber;
            UiKit.Set(feedingThreat,wave?UiText.WaveActiveTitle:"");
            feedingThreat.gameObject.SetActive(wave);
            feedingThreat.color=UiTheme.Red;
            UiKit.Set(feedingLoss,UiText.SessionStockLoss(ctx.World.StockLossCount));feedingLoss.color=ctx.World.StockLossCount>0?UiTheme.Amber:UiTheme.Muted;
        }
    }
}
