using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    public sealed partial class TerminalView
    {
        bool individualFish;
        CatchDestination routingSource;
        const int AllFish=int.MaxValue;

        void AddRoutingControls(List<CatchItem> items)
        {
            var row=UiKit.Row(content,"Routing view",8);
            UiKit.Size(row,48);
            UiKit.Button(row,"ToggleStockView",individualFish?"По видам":"По одной",()=>
            {
                individualFish=!individualFish;
                Rebuild();
            },false,46);
            if(individualFish)return;
            var label=UiKit.Label(row,"Routing source","Из группы",20,UiTheme.Muted);
            UiKit.Size(label,46,118);
            var filters=new Button[3];
            foreach(var source in new[]{CatchDestination.Hold,CatchDestination.Process,CatchDestination.Feed})
                filters[(int)source]=UiKit.Button(row,"Stock_"+source,RouteLabel(source),()=>routingSource=source,false,46);
            refreshRows.Add(()=>
            {
                for(int i=0;i<filters.Length;i++)
                {
                    bool selected=i==(int)routingSource;
                    filters[i].GetComponent<Image>().color=selected?UiTheme.Cyan:UiTheme.Panel;
                    filters[i].GetComponentInChildren<TMP_Text>().color=selected?UiTheme.Steel:UiTheme.Text;
                }
            });

            // Quick actions apply only to unassigned fish, so existing crew selections stay intact.
            if(items.Count<2)return;
            var bulk=UiKit.Row(content,"All unassigned",8);
            UiKit.Size(bulk,47);
            var count=UiKit.Label(bulk,"Unassigned count","",21,UiTheme.Cyan);
            UiKit.Size(count,45,flexibleWidth:1);
            long[] Available(CatchDestination destination) =>
                BatchCandidates(null,CatchDestination.Hold,destination,tab=="feeding",allSpecies:true).Select(fish=>fish.id).ToArray();
            Button AssignAll(CatchDestination destination,string title) =>
                UiKit.Button(bulk,"All_"+destination,title,()=>app.AssignFish(CatchDestination.Hold,destination,Available(destination)),false,44);
            var process=AssignAll(CatchDestination.Process,"Свободные: в переработку");
            var feed=AssignAll(CatchDestination.Feed,"Свободные: на корм");
            refreshRows.Add(()=>
            {
                bool ready=string.IsNullOrEmpty(TerminalParts.BaseReason(app));
                var unassigned=app.Context.View.tank.Count(item=>!item.dispatchCommitted && item.destination==CatchDestination.Hold &&
                    (tab!="feeding" || item.offering>0));
                UiKit.Set(count,$"Без назначения  {unassigned}");
                process.interactable=ready && BatchCandidates(null,CatchDestination.Hold,CatchDestination.Process,tab=="feeding",allSpecies:true).Any();
                feed.interactable=ready && BatchCandidates(null,CatchDestination.Hold,CatchDestination.Feed,tab=="feeding",allSpecies:true).Any();
            });
        }

        void AddSpeciesGroup(CatchItem sample,bool foodOnly)
        {
            string species=sample.species;
            int quantity=AllFish;
            var card=TerminalParts.Card(content,"Species_"+species,133);
            var header=UiKit.Row(card,"Species and routes",10);
            UiKit.Size(header,43);
            UiKit.Size(UiKit.Symbol(header,"Species",DiveInstruments.CatchMark(species),Color.white),38,48);
            UiKit.Size(UiKit.Label(header,"Name",UiText.ItemName(sample),26),43,flexibleWidth:1);
            var status=UiKit.Label(header,"Stock by destination","",21,UiTheme.Muted);
            UiKit.Size(status,43,592);
            var actions=UiKit.Row(card,"Batch actions",7);
            UiKit.Size(actions,48);
            int available=0;
            int Selected() => Mathf.Min(quantity,available);
            void Step(int amount) => quantity=Mathf.Clamp(Selected()+amount,1,Mathf.Max(1,available));
            Button Quantity(string id,string title,float width,UnityEngine.Events.UnityAction action)
            {
                var button=UiKit.Button(actions,id,title,action,false,43);
                UiKit.Size(button,43,width).flexibleWidth=0;
                return button;
            }
            var lessTen=Quantity("LessTen","−10",62,()=>Step(-10));
            var minus=Quantity("Less","−",48,()=>Step(-1));
            var amountText=UiKit.Label(actions,"Quantity","",22,UiTheme.Cyan);
            amountText.alignment=TextAlignmentOptions.Center;
            UiKit.Size(amountText,44,94);
            var plus=Quantity("More","+",48,()=>Step(1));
            var moreTen=Quantity("MoreTen","+10",62,()=>Step(10));
            var all=Quantity("All","Все",86,()=>quantity=AllFish);

            Button Route(CatchDestination destination,string title)
            {
                return UiKit.Button(actions,"Batch_"+destination,title,()=>
                {
                    var ids=BatchCandidates(species,routingSource,destination,foodOnly)
                        .Take(Mathf.Max(1,Selected())).Select(item=>item.id).ToArray();
                    app.AssignFish(routingSource,destination,ids);
                },false,43);
            }
            var process=Route(CatchDestination.Process,"Переработать");
            var feed=Route(CatchDestination.Feed,"На корм");
            var hold=Route(CatchDestination.Hold,"В резервуар");
            refreshRows.Add(()=>
            {
                int free=0,processing=0,feeding=0,dispatched=0;
                foreach(var item in app.Context.View.tank)
                {
                    if(item.species!=species || foodOnly && item.offering<=0)continue;
                    if(item.dispatchCommitted || app.Context.World.IsInFishPipe(item.id)){dispatched++;continue;}
                    switch(item.destination)
                    {
                        case CatchDestination.Process:processing++;break;
                        case CatchDestination.Feed:feeding++;break;
                        default:free++;break;
                    }
                }
                UiKit.Set(status,$"Свободно {free}   ·   Переработка {processing}   ·   Корм {feeding}"+
                    (dispatched>0?$"   ·   В пути {dispatched}":""));
                available=routingSource==CatchDestination.Process?processing:routingSource==CatchDestination.Feed?feeding:free;
                UiKit.Set(amountText,$"{Selected()} / {available}");
                minus.interactable=Selected()>1;
                plus.interactable=Selected()<available;
                all.interactable=moreTen.interactable=plus.interactable;
                lessTen.interactable=minus.interactable;
                bool ready=Selected()>0 && string.IsNullOrEmpty(TerminalParts.BaseReason(app));
                process.interactable=ready && BatchCandidates(species,routingSource,CatchDestination.Process,foodOnly).Any();
                feed.interactable=ready && BatchCandidates(species,routingSource,CatchDestination.Feed,foodOnly).Any();
                hold.interactable=ready && routingSource!=CatchDestination.Hold;
            });
        }

        static string RouteLabel(CatchDestination route) => route switch
        {
            CatchDestination.Process=>"Переработка",
            CatchDestination.Feed=>"Корм",
            _=>"Без назначения"
        };

        IEnumerable<CatchItem> BatchCandidates(string species,CatchDestination source,CatchDestination destination,bool foodOnly,bool allSpecies=false)
        {
            if(source==destination)yield break;
            foreach(var item in app.Context.View.tank)
            {
                if(item.destination!=source || item.dispatchCommitted || app.Context.World.IsInFishPipe(item.id) ||
                   !allSpecies && item.species!=species || foodOnly && item.offering<=0)continue;
                if(RaidWorld.CanRoute(item,destination))yield return item;
            }
        }
    }
}
