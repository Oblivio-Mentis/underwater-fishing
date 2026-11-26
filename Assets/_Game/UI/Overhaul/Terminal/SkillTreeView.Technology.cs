using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    internal sealed partial class SkillTreeView
    {
        static readonly Color TechGreen=new(.58f,.84f,.65f);
        static readonly string[] Categories={"Активная рыбалка","Пассивная добыча","Охота на монстров","Оборудование"};

        RectTransform technologyPage, upgradeList;
        ScrollRect upgradeScroll;
        readonly List<Button> techItems=new(), techNodes=new();
        readonly Button[] categoryKeys=new Button[4];
        TMP_Text itemTitle, itemPurpose, scopeCaption, effectCaption, tuningCaption, gateCaption, equipmentInfo;
        TMP_Text[] techCosts;
        TMP_Text rackHint;
        Button techPurchase, previousRig, nextRig, robotCamera, trapDispatch;
        TechItem selectedItem;
        TechUpgrade selectedUpgrade;
        TeamProgress shownTechView;
        int category, rigIndex, selectedRobot;
        float techRequestUntil;
        internal bool TechnologyOpen => technologyPage && technologyPage.gameObject.activeInHierarchy;

        void BuildTechnologyPage(Transform surface)
        {
            technologyPage=UiKit.Node(surface,"TechnologyWorkshop");
            Place(technologyPage,0,94,1176,662);
            Key(technologyPage,"TechnologyOverview","‹  Все ветки",19,5,158,45,()=>Back());
            for(int i=0;i<Categories.Length;i++)
            {
                int tab=i;
                categoryKeys[i]=Key(technologyPage,"TechCategory"+i,Categories[i],187+244*i,5,238,45,
                    ()=>SelectCategory(tab));
            }
            var items=Box(technologyPage,"Blueprint rack",19,67,284,503,Steel).rectTransform;
            Label(items,"Rack title",15,10,250,35,"ЧЕРТЕЖИ",23,Muted);
            // The biggest category is industrial equipment. Grow the list from data, within a scrollable rack.
            var itemScroll=UiKit.Scroll(items,"Inventions",out var itemList);
            Place((RectTransform)itemScroll.transform,4,53,276,440);
            foreach(var item in TechCatalog.Items)
            {
                var key=UiKit.Button(itemList,"TechItem-"+item.id,item.title,()=>SelectItem(item),false,57);
                techItems.Add(key);
                var plate=Resources.Load<Texture2D>("Technology/Icons/"+item.id);
                if(!plate)continue;
                var icon=UiKit.Node(key.transform,"Equipment");
                Place(icon,5,5,47,47);
                var image=icon.gameObject.AddComponent<RawImage>();image.texture=plate;image.raycastTarget=false;
                var caption=key.GetComponentInChildren<TMP_Text>();
                caption.rectTransform.offsetMin=new Vector2(57,4);caption.rectTransform.offsetMax=new Vector2(-6,-4);
                caption.enableAutoSizing=true;caption.fontSizeMin=15;caption.fontSizeMax=22;
            }

            var modules=Box(technologyPage,"Module rack",311,67,387,503,Steel).rectTransform;
            itemTitle=Label(modules,"Item",16,10,347,38,"",29,Ink);
            itemPurpose=Paragraph(modules,"Purpose",16,51,349,79,20,Muted,15);
            upgradeScroll=UiKit.Scroll(modules,"Modules",out upgradeList);
            Place((RectTransform)upgradeScroll.transform,5,136,377,358);

            var details=Box(technologyPage,"Technology readout",706,67,455,503,Night).rectTransform;
            Box(details,"Edge",0,0,455,3,TechGreen);
            scopeCaption=Label(details,"Module kind",12,12,430,31,"",21,TechGreen);
            effectCaption=Paragraph(details,"Selected module",12,55,424,116,24,Ink,17);
            tuningCaption=Label(details,"Rank change",12,176,428,45,"",25,Cyan);
            gateCaption=Paragraph(details,"Research status",12,232,425,72,19,Muted,15);
            techCosts=MaterialStrip(details,"Next rank price",9,312,436,57);
            techPurchase=Key(details,"TechPurchase","Купить",9,382,436,47,BuyTechnology);
            rackHint=Paragraph(details,"Equipment rack",12,439,426,51,20,Muted,16);

            Box(technologyPage,"Equipment footer",19,577,1142,76,Steel);
            equipmentInfo=Paragraph(technologyPage,"Equipment status",31,586,775,65,21,Ink,16);
            previousRig=Key(technologyPage,"PreviousRig","‹",816,590,42,45,()=>BrowseRig(-1));
            nextRig=Key(technologyPage,"NextRig","›",864,590,42,45,()=>BrowseRig(1));
            robotCamera=Key(technologyPage,"RobotCamera","Камера робота",912,590,235,45,()=>
            {
                if(selectedRobot<=0 || app.Context.World==null)return;
                app.Context.World.RequestRobotConsole(selectedRobot);
                app.ClosePanel();
            });
            trapDispatch=Key(technologyPage,"DispatchTrapNetwork","Отправить улов сети",912,590,235,45,()=>
                app.Context.Player?.CmdDispatchTrapNetwork());
            technologyPage.gameObject.SetActive(false);
        }

        void OpenTechnology()
        {
            if(transition<1)return;
            UiKit.Set(section,"ТЕХНОЛОГИИ И ИЗОБРЕТЕНИЯ");
            overview.gameObject.SetActive(false);
            detail.gameObject.SetActive(false);
            technologyPage.gameObject.SetActive(true);
            techRequestUntil=0;
            SelectCategory(category);
        }

        void SelectCategory(int value)
        {
            category=value;
            TechItem first=null;
            for(int i=0;i<techItems.Count;i++)
            {
                var item=TechCatalog.Items[i];
                bool visible=item.category==value;
                techItems[i].gameObject.SetActive(visible);
                if(visible && first==null)first=item;
            }
            for(int i=0;i<categoryKeys.Length;i++)
                categoryKeys[i].targetGraphic.color=i==value?Color.Lerp(Steel,TechGreen,.3f):Steel;
            if(first!=null)SelectItem(first);
        }

        void SelectItem(TechItem item)
        {
            selectedItem=item;
            selectedUpgrade=item.Upgrade("unlock");
            rigIndex=0;
            UiKit.Set(itemTitle,item.title);
            UiKit.Set(itemPurpose,item.description);
            UiKit.Clear(upgradeList);
            techNodes.Clear();
            foreach(var node in item.upgrades)
                techNodes.Add(UiKit.Button(upgradeList,"TechNode-"+node.id,node.title,
                    ()=>{selectedUpgrade=node;shownTechView=null;Refresh();},false,48));
            upgradeScroll.verticalNormalizedPosition=1;
            shownTechView=null;
            Refresh();
        }

        void BrowseRig(int delta)
        {
            rigIndex+=delta;
            shownTechView=null;
            Refresh();
        }

        void RefreshTechnology(TeamProgress team)
        {
            if(shownTechView==team && (techRequestUntil==0 || Time.unscaledTime<techRequestUntil))return;
            if(Time.unscaledTime>=techRequestUntil)techRequestUntil=0;
            shownTechView=team;
            var item=selectedItem;
            var node=selectedUpgrade;
            if(item==null || node==null)return;
            for(int i=0;i<techItems.Count;i++)
            {
                var row=TechCatalog.Items[i];
                if(row.category!=category)continue;
                techItems[i].targetGraphic.color=row==item?Color.Lerp(Steel,TechGreen,.26f):Night;
                UiKit.Caption(techItems[i],(team.TechRank(row.id,"unlock")>0?"[+]  ":"")+row.title);
            }
            for(int i=0;i<item.upgrades.Length;i++)
            {
                var row=item.upgrades[i];
                int owned=team.TechRank(item.id,row.id);
                string state=owned==row.values.Length?"[+]":team.TechRequirement(item,row)!=null?"—":
                    team.Treasury.Covers(row.NextCost(owned))?"+":"·";
                UiKit.Caption(techNodes[i],$"{state}   {row.title}   {owned}/{row.values.Length}");
                techNodes[i].targetGraphic.color=row==node?Color.Lerp(Steel,KindColor(row.kind),.32f):Night;
            }
            int rank=team.TechRank(item.id,node.id);
            bool complete=rank>=node.values.Length;
            MaterialCost cost=complete?default:node.NextCost(rank);
            int stage=TechCatalog.Stage(item,node);
            string blocked=team.TechRequirement(item,node);
            bool enough=team.Treasury.Covers(cost);
            UiKit.Set(scopeCaption,"ЭКИПАЖ   /   "+KindLabel(node.kind));
            scopeCaption.color=KindColor(node.kind);
            UiKit.Set(effectCaption,node.title+"\n"+node.description);
            string current=rank==0?(node.id=="stack"?item.stackBase+" шт. в слоте":node.kind=="stat"?"Базовый":"Закрыто"):ValueLabel(node,rank);
            UiKit.Set(tuningCaption,complete?current:current+"   →   "+ValueLabel(node,rank+1));
            UiKit.Set(gateCaption,"Стадия "+SkillTreeDefinition.Roman(stage)+" · "+TechCatalog.StageTitle(stage)+"\n"+
                (blocked??(enough?"Материалы доступны":"Не хватает материалов")));
            PresentMaterials(techCosts,cost,team.Treasury);
            bool control=app.Context.NearTerminal && app.Context.Player!=null && !app.Context.Player.IsHelpless;
            techPurchase.interactable=control && blocked==null && enough && techRequestUntil==0;
            UiKit.Caption(techPurchase,complete?"Приобретено":techRequestUntil>0?"Ожидание хоста…":"Купить для экипажа");
            var rigs=team.rigs?.FindAll(r=>r.kind==item.id);
            int count=rigs?.Count??0;
            int limit=TechRig.TotalLimit(team,item);
            bool unlocked=team.TechRank(item.id,"unlock")>0;
            UiKit.Set(rackHint,unlocked
                ? $"Бесплатно в раздатчике у монитора  ·  {team.TechStackLimit(item.id)} шт. в слоте  ·  в работе {count}/{limit}"
                : "После открытия можно взять в раздатчике на корабле");
            rigIndex=Mathf.Clamp(rigIndex,0,Math.Max(0,count-1));
            var rig=count==0?null:rigs[rigIndex];
            string status=rig==null?"Экземпляров в экспедиции нет":
                $"№{rig.serial}   ·   {RigState(rig.mode)}   ·   {rig.health:P0}   ·   {RaidWorld.EquipmentReadout(rig,team)}\n"+
                $"Глубина {rig.depth}   ·   координаты {rig.position.x:0} / {rig.position.y:0} / {rig.position.z:0}";
            bool network=item.id=="trap" && team.TechRank("trap","group")>0;
            if(network && count>0)
            {
                int alarms=0,stock=0;
                foreach(var trap in rigs){stock+=trap.contents.Count;if(RaidWorld.TrapAlarm(team,trap))alarms++;}
                status+="   ·   сеть: "+stock+" рыб, сигналов "+alarms;
            }
            trapDispatch.gameObject.SetActive(network);
            trapDispatch.interactable=network && control && count>0;
            UiKit.Set(equipmentInfo,status);
            previousRig.interactable=rigIndex>0;
            nextRig.interactable=rigIndex+1<count;
            selectedRobot=rig!=null && item.id=="robot" && rig.depth==team.expeditionDepth?rig.serial:0;
            robotCamera.gameObject.SetActive(item.id=="robot");
            robotCamera.interactable=control && selectedRobot>0;
        }

        static string KindLabel(string kind)=>kind switch
        {
            "unlock"=>"ЧЕРТЁЖ", "stat"=>"ПАРАМЕТР", "automation"=>"АВТОМАТИКА", _=>"ФУНКЦИЯ"
        };
        static Color KindColor(string kind)=>kind switch
        {
            "unlock"=>TechGreen, "stat"=>Cyan, "automation"=>Amber, _=>new Color(.80f,.72f,.93f)
        };
        static string ValueLabel(TechUpgrade node,int rank)=>node.id=="stack"?node.values[rank-1].ToString("0")+" шт. в слоте":node.kind=="stat"?
            $"×{node.values[rank-1]:0.##}":node.values.Length==1?"Открыто":"Уровень "+rank;

        static string RigState(string mode)=>mode switch
        {
            "carried"=>"переноска", "placed"=>"установлено", "ready"=>"готово", "active"=>"в работе",
            "armed"=>"взведено", "captured"=>"с добычей", "damaged"=>"повреждено", "deployed"=>"установлено",
            "stored"=>"на судне", "idle"=>"ожидание", "towed"=>"буксировка", _=>mode
        };

        void BuyTechnology()
        {
            if(!techPurchase.interactable || app.Context.View==null)return;
            var team=app.Context.View;
            app.Context.Player.CmdTechBuy(selectedItem.id,selectedUpgrade.id,
                team.TechRank(selectedItem.id,selectedUpgrade.id),team.teamRevision);
            WaitTechnology();
        }

        void WaitTechnology()
        {
            techRequestUntil=Time.unscaledTime+1;
            shownTechView=null;
            Refresh();
        }
    }
}
