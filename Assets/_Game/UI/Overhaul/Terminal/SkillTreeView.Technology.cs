using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    internal sealed partial class SkillTreeView
    {
        static readonly Color TechGreen=new(.58f,.84f,.65f);
        static readonly string[] Categories={"Активная рыбалка","Пассивная добыча","Охота на монстров","Оборудование"};
        const float TechGraphHeight=468;

        sealed class TechRankCard
        {
            internal TechUpgrade Upgrade;
            internal int Rank;
            // SkillWires uses these per-graph keys only to color bought connections.
            internal SkillId WireKey;
            internal SkillNodeBadge Face;
            internal TMP_Text Numeral;
        }

        RectTransform technologyPage,techGraph,itemStrip;
        SkillWires techWires;
        Image equipmentArt;
        TMP_Text equipmentInfo;
        Button previousRig,nextRig,robotCamera,trapDispatch;
        readonly List<TechRankCard> techRanks=new();
        readonly List<SkillId> ownedTechLinks=new();
        readonly Button[] categoryKeys=new Button[4];
        readonly Dictionary<TechItem,Button> itemKeys=new();
        readonly Dictionary<string,Sprite> blueprintArt=new();
        TechItem selectedItem,pendingTech;
        TechUpgrade pendingUpgrade;
        TechRankCard selectedTech;
        TeamProgress shownTechView;
        int category,rigIndex,selectedRobot,pendingTechRank;
        float techRequestUntil;
        bool techAccepted,techControl;
        internal bool TechnologyOpen=>technologyPage && technologyPage.gameObject.activeInHierarchy;

        void BuildTechnologyPage()
        {
            // The ninth sector shares the viewport, zoom, reticle, detail card and purchase controls.
            technologyPage=UiKit.Node(detail,"Technology navigation");
            UiKit.Fill(technologyPage);
            for(int i=0;i<Categories.Length;i++)
            {
                int index=i;
                categoryKeys[i]=Key(technologyPage,"TechCategory"+i,Categories[i],187+i*244,7,238,43,()=>SelectCategory(index));
            }
            itemStrip=UiKit.Node(technologyPage,"Чертежи категории");
            Place(itemStrip,19,62,GraphWidth,44);
            Key(technologyPage,"TechCenter","Центр",692,62,81,44,ResetGraph);
            techGraph=UiKit.Node(graph,"Technology ranks");UiKit.Fill(techGraph);

            var fleet=Box(technologyPage,"Equipment in the expedition",19,590,GraphWidth,63,Steel).rectTransform;
            equipmentArt=CenterIcon(fleet,"Оборудование",40);
            Place(equipmentArt.rectTransform,8,10,40,40);
            equipmentInfo=Paragraph(fleet,"Equipment status",57,4,475,55,19,Ink,15);
            previousRig=Key(fleet,"PreviousRig","‹",533,9,38,44,()=>BrowseRig(-1));
            nextRig=Key(fleet,"NextRig","›",575,9,38,44,()=>BrowseRig(1));
            robotCamera=Key(fleet,"RobotCamera","Камера",620,9,126,44,()=>
            {
                if(selectedRobot<=0 || app.Context.World==null)return;
                app.Context.World.RequestRobotConsole(selectedRobot);
                app.ClosePanel();
            });
            trapDispatch=Key(fleet,"DispatchTrapNetwork","Сдать улов",620,9,126,44,()=>app.Context.Player?.CmdDispatchTrapNetwork());
            Label(technologyPage,"Graph gestures",793,628,368,29,"Колесо — масштаб   ·   Потяните поле для сдвига",17,Muted,true);
        }

        void SetTechnologyMode(bool active)
        {
            technologyPage.gameObject.SetActive(active);techGraph.gameObject.SetActive(active);
            skillGraph.gameObject.SetActive(!active);skillControls.gameObject.SetActive(!active);
            Place(graphArea,19,active?115:61,GraphWidth,active?TechGraphHeight:GraphHeight);
            graph.sizeDelta=new Vector2(GraphWidth,active?TechGraphHeight:GraphHeight);
            Place(title.rectTransform,22,active?73:83,325,active?63:78);
            Place(description.rectTransform,22,active?138:164,325,active?99:69);
            description.fontSizeMin=active?15:17;
            Place(requirements[1].rectTransform,23,360,323,24);
            requirements[1].textWrappingMode=active?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
            requirements[2].gameObject.SetActive(true);
            if(active){dataPanel.gameObject.SetActive(false);hintPanel.gameObject.SetActive(true);buy.interactable=false;}
            buy.name=active?"TechPurchase":"BuySkill";
            UiKit.Caption(buy,"Купить  ·  ПКМ по узлу");
            shownTechView=null;
        }

        void OpenTechnology()=>ChangeBranch(SkillTreeDefinition.BranchCount);

        void SelectCategory(int value)
        {
            category=value;
            UiKit.Clear(itemStrip);itemKeys.Clear();
            var items=TechCatalog.Items.Where(item=>item.category==value).ToArray();
            if(items.Length==0)return;
            float width=666f/items.Length;
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];
                itemKeys[item]=Key(itemStrip,"TechItem-"+item.id,item.title,i*width,0,width-4,44,()=>SelectItem(item));
            }
            for(int i=0;i<categoryKeys.Length;i++)
                categoryKeys[i].targetGraphic.color=i==value?Color.Lerp(Steel,TechGreen,.3f):Steel;
            SelectItem(selectedItem!=null && selectedItem.category==value?selectedItem:items[0]);
        }

        Sprite BlueprintSprite(TechItem item)
        {
            if(blueprintArt.TryGetValue(item.id,out var sprite))return sprite;
            var texture=Resources.Load<Texture2D>("Technology/Icons/"+item.id);
            sprite=texture?Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Center):
                DiveInstruments.Art(DiveInstruments.Mark.Wrench);
            blueprintArt.Add(item.id,sprite);
            return sprite;
        }

        void SelectItem(TechItem item)
        {
            selectedItem=item;rigIndex=0;
            UiKit.Clear(techGraph);techRanks.Clear();
            var ink=UiKit.Node(techGraph,"Исследованные связи");UiKit.Fill(ink);
            techWires=ink.gameObject.AddComponent<SkillWires>();techWires.raycastTarget=false;
            var links=new List<SkillWires.Link>();
            var blueprint=item.Upgrade("unlock");
            Vector2 origin=new(-316,0);
            var entry=AddTechRank(blueprint,1,origin,68,true);
            var upgrades=item.upgrades.Where(u=>u!=blueprint).ToArray();
            int columns=upgrades.Length==0?1:upgrades.Max(u=>u.values.Length);
            float spacing=Mathf.Min(67f,406f/Mathf.Max(1,upgrades.Length-1));
            float diameter=Mathf.Min(44f,spacing-6f);
            for(int i=0;i<upgrades.Length;i++)
            {
                var upgrade=upgrades[i];
                float y=((upgrades.Length-1)*.5f-i)*spacing;
                var previous=entry;
                for(int level=1;level<=upgrade.values.Length;level++)
                {
                    float x=10+(level-1)*(295f/Mathf.Max(1,columns-1));
                    Vector2 point=new(x,y);
                    var node=AddTechRank(upgrade,level,point,diameter,false);
                    Vector2 from=previous.Face.rectTransform.anchoredPosition;
                    links.Add(level==1?new(previous.WireKey,node.WireKey,KindColor(upgrade.kind),origin,new(-263,0),new(-263,y),point):
                        new(previous.WireKey,node.WireKey,KindColor(upgrade.kind),from,point));
                    previous=node;
                }
                var row=UiKit.Node(techGraph,"Линия "+upgrade.id);
                UiKit.Place(row,Center,Center,new(-135,y),new(225,Mathf.Min(43,spacing-3)));
                var plate=row.gameObject.AddComponent<Image>();plate.color=Steel;
                var label=UiKit.Label(row,"Название линии",upgrade.title,19,Ink);
                UiKit.Fill(label.rectTransform);label.margin=new Vector4(5,0,5,0);
                label.alignment=TextAlignmentOptions.MidlineLeft;
                var choose=row.gameObject.AddComponent<Button>();choose.targetGraphic=plate;
                choose.onClick.AddListener(()=>SelectNextTechRank(upgrade));
            }
            techWires.SetLinks(links);
            foreach(var pair in itemKeys)
                pair.Value.targetGraphic.color=pair.Key==item?Color.Lerp(Steel,TechGreen,.3f):Steel;
            equipmentArt.sprite=BlueprintSprite(item);
            selectedTech=entry;
            ResetGraph();
            shownTechView=null;
            Invalidate();Refresh();
        }

        TechRankCard AddTechRank(TechUpgrade upgrade,int level,Vector2 at,float side,bool blueprint)
        {
            var rect=UiKit.Node(techGraph,"TechNode-"+upgrade.id+(level==1?"":"-"+level));
            UiKit.Place(rect,Center,Center,at,Vector2.one*side);
            var face=rect.gameObject.AddComponent<SkillNodeBadge>();
            face.Accent=KindColor(upgrade.kind);face.Capstone=blueprint;
            var pick=rect.gameObject.AddComponent<SkillPurchaseButton>();
            pick.targetGraphic=face;pick.transition=Selectable.Transition.None;
            var card=new TechRankCard{Face=face,Upgrade=upgrade,Rank=level,WireKey=(SkillId)(techRanks.Count+1)};
            if(blueprint)CenterIcon(rect,"Чертёж",36).sprite=BlueprintSprite(selectedItem);
            else
            {
                card.Numeral=UiKit.Label(rect,"Ранг",SkillTreeDefinition.Roman(level),19,Ink);
                UiKit.Fill(card.Numeral.rectTransform);card.Numeral.alignment=TextAlignmentOptions.Center;
            }
            pick.onClick.AddListener(()=>SelectTechRank(card));
            pick.Purchase=()=>{SelectTechRank(card);BuyTechnology();};
            techRanks.Add(card);
            return card;
        }

        void SelectNextTechRank(TechUpgrade upgrade)
        {
            int current=app.Context.View?.TechRank(selectedItem.id,upgrade.id)??0;
            SelectTechRank(techRanks.Find(r=>r.Upgrade==upgrade && r.Rank==Mathf.Min(current+1,upgrade.values.Length)));
        }

        void SelectTechRank(TechRankCard card)
        {
            if(card==null)return;
            selectedTech=card;shownTechView=null;Refresh();
        }

        void BrowseRig(int delta)
        {
            rigIndex+=delta;shownTechView=null;Refresh();
        }

        static SkillNodeState TechStatus(TeamProgress team,TechItem item,TechRankCard card)
        {
            int owned=team.TechRank(item.id,card.Upgrade.id);
            if(card.Rank<=owned)return SkillNodeState.Purchased;
            if(card.Rank!=owned+1 || team.TechRequirement(item,card.Upgrade)!=null)return SkillNodeState.MissingRank;
            return team.Treasury.Covers(card.Upgrade.NextCost(owned))?SkillNodeState.Available:SkillNodeState.MissingMaterials;
        }

        void RefreshTechnology(TeamProgress team)
        {
            bool control=app.Context.NearTerminal && app.Context.Player!=null && !app.Context.Player.IsHelpless;
            if(selectedTech==null || shownTechView==team && control==techControl)return;
            shownTechView=team;techControl=control;
            ownedTechLinks.Clear();
            foreach(var card in techRanks)
            {
                var state=TechStatus(team,selectedItem,card);
                if(state==SkillNodeState.Purchased)ownedTechLinks.Add(card.WireKey);
                card.Face.State=state;card.Face.Selected=card==selectedTech;card.Face.SetVerticesDirty();
                if(card.Numeral)card.Numeral.color=state==SkillNodeState.MissingRank?Muted:Ink;
            }
            techWires.SetOwned(ownedTechLinks,Empty);
            DisplayTechDetails(team);
            DisplayEquipment(team);
        }

        void DisplayTechDetails(TeamProgress team)
        {
            var item=selectedItem;
            var card=selectedTech;
            var node=card.Upgrade;
            int owned=team.TechRank(item.id,node.id);
            int level=card.Rank;
            var state=TechStatus(team,item,card);
            bool bought=state==SkillNodeState.Purchased;
            bool blueprint=node.id=="unlock";
            dataPanel.gameObject.SetActive(true);hintPanel.gameObject.SetActive(false);
            detailSymbol.sprite=BlueprintSprite(item);detailSymbol.color=Color.white;
            scope.color=KindColor(node.kind);
            UiKit.Set(scope,blueprint?"ЭКИПАЖ  /  ЧЕРТЁЖ":"ЭКИПАЖ  /  "+SkillTreeDefinition.Roman(level));
            UiKit.Set(title,blueprint?item.title:node.title+(node.values.Length>1?" "+SkillTreeDefinition.Roman(level):""));
            UiKit.Set(description,blueprint?item.description:node.id=="stack"?
                "Увеличивает число экземпляров этого изобретения в одном кармане водолаза. Вместимость действует для всего экипажа.":node.description);
            UiKit.Set(before,ValueLabel(item,node,owned));
            UiKit.Set(after,bought?"—":ValueLabel(item,node,level));after.color=KindColor(node.kind);
            int stage=TechCatalog.Stage(item,node);
            UiKit.Set(requirements[0],"Стадия "+SkillTreeDefinition.Roman(stage)+" · "+TechCatalog.StageTitle(stage));
            string blocked=bought?null:level>owned+1?"Сначала изучите ранг "+SkillTreeDefinition.Roman(level-1):team.TechRequirement(item,node);
            Place(requirements[1].rectTransform,23,360,323,blocked==null?24:49);
            requirements[2].gameObject.SetActive(blocked==null);
            UiKit.Set(requirements[1],blocked??(blueprint?"Бесплатная выдача в раздатчике":item.title));
            UiKit.Set(requirements[2],blueprint?"В кармане до "+team.TechStackLimit(item.id)+" шт.":"Изучено "+owned+" из "+node.values.Length);
            requirements[0].color=requirements[2].color=Muted;requirements[1].color=blocked==null?Muted:Amber;
            PresentMaterials(costs,node.NextCost(level-1),team.Treasury);
            bool waiting=pendingTech!=null || pending!=SkillId.None;
            note.color=state==SkillNodeState.Available?Cyan:bought?Muted:Amber;
            UiKit.Set(note,waiting?"Подтверждение хоста…":bought?"Куплено":blocked!=null?"Пока недоступно":
                !techControl?"Подойдите к монитору":state==SkillNodeState.Available?"Готово к покупке":"Не хватает материалов");
            buy.interactable=!waiting && techControl && state==SkillNodeState.Available;
            UiKit.Caption(buy,bought?"Приобретено":"Купить  ·  ПКМ по узлу");
        }

        static string ValueLabel(TechItem item,TechUpgrade upgrade,int rank)
        {
            if(upgrade.id=="stack")return (rank==0?item.stackBase:Mathf.RoundToInt(upgrade.values[rank-1]))+" шт.";
            if(upgrade.id=="vacuum_capacity")return (1+(rank==0?0:Mathf.RoundToInt(upgrade.values[rank-1])))+" шт.";
            if(upgrade.kind=="stat")return "×"+(rank==0?1:upgrade.values[rank-1]).ToString("0.##");
            return rank==0?"Закрыто":upgrade.values.Length==1?"Открыто":"Ур. "+rank;
        }

        static Color KindColor(string kind)=>kind switch
        {
            "unlock"=>TechGreen, "stat"=>Cyan, "automation"=>Amber, _=>new Color(.80f,.72f,.93f)
        };

        void DisplayEquipment(TeamProgress team)
        {
            var item=selectedItem;
            var rigs=team.rigs?.FindAll(r=>r.kind==item.id);
            int count=rigs?.Count??0;
            rigIndex=Mathf.Clamp(rigIndex,0,Math.Max(0,count-1));
            var rig=count==0?null:rigs[rigIndex];
            bool network=item.id=="trap" && team.TechRank("trap","group")>0;
            string status=item.title+" · в работе "+count+"/"+TechRig.TotalLimit(team,item);
            if(rig!=null)status+="\n№"+rig.serial+" · "+RigState(rig.mode)+" · "+rig.health.ToString("P0")+
                " · глубина "+rig.depth+" · "+$"{rig.position.x:0} / {rig.position.y:0} / {rig.position.z:0}"+
                "\n"+RaidWorld.EquipmentReadout(rig,team);
            if(network && count>0)status+=" · улов "+rigs.Sum(r=>r.contents.Count)+", сигналы "+rigs.Count(r=>RaidWorld.TrapAlarm(team,r));
            UiKit.Set(equipmentInfo,status);
            previousRig.gameObject.SetActive(count>1);nextRig.gameObject.SetActive(count>1);
            previousRig.interactable=rigIndex>0;nextRig.interactable=rigIndex+1<count;
            selectedRobot=rig!=null && item.id=="robot" && rig.depth==team.expeditionDepth?rig.serial:0;
            robotCamera.gameObject.SetActive(item.id=="robot");robotCamera.interactable=techControl && selectedRobot>0;
            trapDispatch.gameObject.SetActive(network);trapDispatch.interactable=techControl && count>0;
        }

        static string RigState(string mode)=>mode switch
        {
            "carried"=>"переноска", "placed"=>"установлено", "ready"=>"готово", "active"=>"в работе",
            "armed"=>"взведено", "captured"=>"с добычей", "damaged"=>"повреждено", "deployed"=>"установлено",
            "stored"=>"на судне", "idle"=>"ожидание", "towed"=>"буксировка", _=>mode
        };

        void BuyTechnology()
        {
            var team=app.Context.View;
            if(!TechnologyOpen || transition<1 || pendingTech!=null || pending!=SkillId.None ||
                selectedTech==null || team==null || !app.Context.NearTerminal || app.Context.Player==null ||
                app.Context.Player.IsHelpless || TechStatus(team,selectedItem,selectedTech)!=SkillNodeState.Available)return;
            pendingTech=selectedItem;pendingUpgrade=selectedTech.Upgrade;pendingTechRank=selectedTech.Rank;techAccepted=false;
            techRequestUntil=Time.unscaledTime+7;
            shownTechView=null;Refresh();
            app.Context.Player.CmdTechBuy(pendingTech.id,pendingUpgrade.id,pendingTechRank-1,team.teamRevision);
        }

        void CheckTechRequest(TeamProgress team)
        {
            if(pendingTech==null)return;
            if(techAccepted && team!=null && team.TechRank(pendingTech.id,pendingUpgrade.id)>=pendingTechRank)ClearTechRequest();
            else if(Time.unscaledTime>techRequestUntil)
            {
                ClearTechRequest();ShowResponse("Не получено подтверждение покупки",false);
            }
        }

        void ClearTechRequest()
        {
            pendingTech=null;pendingUpgrade=null;techAccepted=false;shownTechView=null;
        }
    }
}
