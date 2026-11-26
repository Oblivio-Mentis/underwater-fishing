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
        static readonly DiveInstruments.Mark[] CategoryIcons={
            DiveInstruments.Mark.Spear,DiveInstruments.Mark.Ray,DiveInstruments.Mark.Jaw,DiveInstruments.Mark.Wrench
        };

        [Serializable] sealed class RecipePicture { public RecipeEdge[] recipes; }
        [Serializable] sealed class RecipeEdge { public string item,upgrade; public string[] requireAny; }
        RecipeEdge[] techRecipes;
        RectTransform technologyIndex,technologyDial,fleetPanel;
        TMP_Text equipmentInfo,fleetTitle;
        Button previousRig,nextRig,robotCamera,trapDispatch;
        readonly Dictionary<string,Sprite> blueprintArt=new();
        readonly List<Card> techRanks=new();
        readonly List<SkillId> ownedTechLinks=new();
        readonly List<(SkillSectorGraphic face,TMP_Text count,TechItem[] items)> indexSectors=new();
        TechItem selectedItem,pendingTech;
        TechItem[] categoryItems=Array.Empty<TechItem>();
        TechUpgrade pendingUpgrade;
        Card selectedTech;
        TeamProgress shownTechView;
        int category=-1,rigIndex,selectedRobot,pendingTechRank;
        float techRequestUntil;
        bool techAccepted,techControl;
        internal bool TechnologyOpen=>technologyIndex && technologyIndex.gameObject.activeSelf && Root.gameObject.activeInHierarchy;

        void BuildTechnologyPage()
        {
            technologyIndex=UiKit.Node(overview,"Technology index");UiKit.Fill(technologyIndex);
            Key(technologyIndex,"TechIndexBack","‹  Назад",19,7,158,43,()=>Back());
            technologyDial=UiKit.Node(technologyIndex,"Technology dial");
            UiKit.Place(technologyDial,Center,Center,Vector2.zero,new(622,622));
            fleetPanel=Box(technologyIndex,"Оборудование экипажа",796,211,359,258,Steel).rectTransform;
            fleetTitle=Paragraph(fleetPanel,"Модель",16,10,217,55,24,Ink,17);
            equipmentInfo=Paragraph(fleetPanel,"Equipment status",16,73,326,113,19,Muted,16);
            previousRig=Key(fleetPanel,"PreviousRig","‹",15,197,40,43,()=>BrowseRig(-1));
            nextRig=Key(fleetPanel,"NextRig","›",61,197,40,43,()=>BrowseRig(1));
            robotCamera=Key(fleetPanel,"RobotCamera","Камера",175,197,165,43,()=>
            {
                if(selectedRobot<=0 || app.Context.World==null)return;
                app.Context.World.RequestRobotConsole(selectedRobot);app.ClosePanel();
            });
            trapDispatch=Key(fleetPanel,"DispatchTrapNetwork","Сдать улов",175,197,165,43,()=>app.Context.Player?.CmdDispatchTrapNetwork());
            Key(fleetPanel,"TechPreviousModel","‹",244,15,43,40,()=>BrowseFleetModel(-1));
            Key(fleetPanel,"TechNextModel","›",295,15,43,40,()=>BrowseFleetModel(1));
        }

        void OpenTechnology()
        {
            if(transition>=1)ShowTechnologyIndex(-1);
        }

        void ShowTechnologyIndex(int value)
        {
            category=value;
            selectedTech=null;shownTechView=null;
            dial.gameObject.SetActive(false);technologyIndex.gameObject.SetActive(true);
            fleetPanel.gameObject.SetActive(value>=0);
            UiKit.Clear(technologyDial);indexSectors.Clear();
            technologyDial.anchoredPosition=value<0?Vector2.zero:new Vector2(-144,0);
            categoryItems=value<0?Array.Empty<TechItem>():TechCatalog.Items.Where(item=>item.category==value).ToArray();
            if(value>=0 && (selectedItem==null || selectedItem.category!=value))selectedItem=categoryItems[0];
            rigIndex=0;
            UiKit.Set(section,value<0?"ТЕХНОЛОГИИ":Categories[value]);
            int count=value<0?Categories.Length:categoryItems.Length;
            for(int i=0;i<count;i++)
            {
                int index=i;
                var item=value<0?null:categoryItems[i];
                var slice=UiKit.Node(technologyDial,item==null?"TechCategory"+i:"TechItem-"+item.id);
                UiKit.Place(slice,Center,Center,Vector2.zero,new(622,622));
                var face=slice.gameObject.AddComponent<SkillSectorGraphic>();face.Bind(i,Cyan,count);
                var key=slice.gameObject.AddComponent<SkillSectorButton>();key.Face=face;key.targetGraphic=face;key.transition=Selectable.Transition.None;
                key.onClick.AddListener(()=>
                {
                    if(item==null)ShowTechnologyIndex(index);
                    else {selectedItem=item;rigIndex=0;ChangeBranch(SkillTreeDefinition.BranchCount);}
                });
                Vector2 axis=SkillBoardMesh.At(i*360f/count);
                var picture=CenterIcon(slice,"Direction icon",58);
                picture.sprite=item==null?DiveInstruments.Art(CategoryIcons[i]):BlueprintSprite(item);
                UiKit.Place(picture.rectTransform,Center,Center,axis*219+Vector2.up*26,new(58,58));
                var name=UiKit.Label(slice,"Direction",item==null?Categories[i]:item.title,22,Ink);
                name.alignment=TextAlignmentOptions.Center;
                UiKit.Place(name.rectTransform,Center,Center,axis*216-Vector2.up*18,new(172,47));
                var counter=UiKit.Label(slice,"Исследовано",string.Empty,18,Cyan);
                UiKit.Place(counter.rectTransform,Center,Center,axis*216-Vector2.up*56,new(105,22));
                counter.alignment=TextAlignmentOptions.Center;
                indexSectors.Add((face,counter,item!=null?new[]{item}:TechCatalog.Items.Where(t=>t.category==index).ToArray()));
            }
            var hub=Emblem(technologyDial,"NEREID",Vector2.zero,195);
            var hubArt=CenterIcon(hub,"Эмблема",58);hubArt.sprite=DiveInstruments.Art(value<0?DiveInstruments.Mark.Wrench:CategoryIcons[value]);
            hubArt.rectTransform.anchoredPosition=new(0,19);
            Label(hub,"Судно",16,111,163,38,"NEREID",30,Ink,true);
            Invalidate();Refresh();
        }

        void BrowseFleetModel(int step)
        {
            if(categoryItems.Length==0)return;
            int index=Array.IndexOf(categoryItems,selectedItem);
            selectedItem=categoryItems[(index+step+categoryItems.Length)%categoryItems.Length];
            rigIndex=0;shownTechView=null;Refresh();
        }

        void DisplayTechIndex(TeamProgress team)
        {
            foreach(var sector in indexSectors)
            {
                int owned=0,total=0;
                foreach(var item in sector.items)
                    foreach(var upgrade in item.upgrades){owned+=team.TechRank(item.id,upgrade.id);total+=upgrade.values.Length;}
                sector.face.SetProgress(total>0?(float)owned/total:0);
                UiKit.Set(sector.count,owned+" / "+total);
            }
            if(category>=0 && selectedItem!=null){UiKit.Set(fleetTitle,selectedItem.title);DisplayEquipment(team);}
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


        void BindTechBranch()
        {
            ClearCards();techRanks.Clear();selected=SkillId.None;rigIndex=0;
            rootSymbol.sprite=BlueprintSprite(selectedItem);buy.name="TechPurchase";
            if(techRecipes==null)
            {
                var stages=Resources.Load<TextAsset>("Technology/stages");
                techRecipes=stages?JsonUtility.FromJson<RecipePicture>(stages.text).recipes:Array.Empty<RecipeEdge>();
            }
            var item=selectedItem;
            var blueprint=item.Upgrade("unlock");
            var capstone=item.upgrades.Where(n=>n.values.Length==1 && TechParents(n).Any())
                .OrderBy(n=>TechCatalog.Stage(item,n)).LastOrDefault();
            var branches=item.upgrades.Where(n=>n.id!="unlock" && n.id!="stack" && n!=capstone).ToArray();
            var lengths=new int[4];
            AddTechBadge(blueprint,1,new(-248,0));
            var stack=item.Upgrade("stack");
            for(int rank=1;rank<=stack.values.Length;rank++)AddTechBadge(stack,rank,new(-248+54*rank,0));
            int lane=0;
            foreach(var upgrade in branches.Where(n=>n.values.Length>1))
            {
                for(int rank=1;rank<=upgrade.values.Length;rank++)
                    AddTechBadge(upgrade,rank,SideRankPosition(lane,rank-1));
                lengths[lane++]=upgrade.values.Length;
            }
            // Each side rank occupies the same stations as a normal branch. Single-rank modules
            // have their own inbound wire and do not require other modules printed on that lane.
            foreach(var upgrade in branches.Where(n=>n.values.Length==1).OrderBy(n=>TechCatalog.Stage(item,n)))
            {
                int target=Array.IndexOf(lengths,lengths.Min());
                int preferred=TechCatalog.Stage(item,upgrade)-1;
                int column=Mathf.Clamp(Mathf.Max(preferred,lengths[target]),0,4);
                if(lengths[target]>4)
                    throw new InvalidOperationException("Equipment exceeds the ship graph's side stations: "+item.id);
                AddTechBadge(upgrade,1,SideRankPosition(target,column));
                lengths[target]=column+1;
            }
            if(capstone!=null)AddTechBadge(capstone,1,new(330,0),true);
            wires.SetLinks(TechLinks(blueprint));
            dataPanel.gameObject.SetActive(false);hintPanel.gameObject.SetActive(true);
            shownTechView=null;ResetGraph();ApplyLod();Invalidate();
        }

        void AddTechBadge(TechUpgrade upgrade,int level,Vector2 at,bool capstone=false)
        {
            var card=cards[techRanks.Count];
            card.Upgrade=upgrade;card.TechRank=level;card.WireKey=(SkillId)(techRanks.Count+1);
            string key="TechNode-"+upgrade.id+(level==1?"":"-"+level);
            BindRankBadge(card,key,at,capstone,Cyan,TechArt(upgrade,capstone),level,upgrade.title);
            techRanks.Add(card);
        }

        Sprite TechArt(TechUpgrade node,bool capstone)
        {
            if(node.id=="unlock" || capstone)return BlueprintSprite(selectedItem);
            var mark=node.id switch
            {
                "stack" or "compact"=>DiveInstruments.Mark.Glove,
                "capacity" or "vacuum_capacity" or "limit"=>DiveInstruments.Mark.Tile,
                "speed"=>DiveInstruments.Mark.Needle,
                "range" or "select" or "beacon"=>DiveInstruments.Mark.Compass,
                "reinforced" or "quality"=>DiveInstruments.Mark.Shield,
                "bait"=>DiveInstruments.Mark.Ray,
                "delivery"=>DiveInstruments.Mark.Submarine,
                "remote" or "auto" or "group"=>DiveInstruments.Mark.Lantern,
                _=>DiveInstruments.Mark.Wrench
            };
            return DiveInstruments.Art(mark);
        }

        IEnumerable<TechUpgrade> TechParents(TechUpgrade node)
        {
            // Only draw same-invention links. The server checks full recipe groups, including
            // alternate blueprints in other inventions, and returns their names on the detail card.
            foreach(var recipe in techRecipes)
            {
                if(recipe.item!=selectedItem.id || recipe.upgrade!=node.id || recipe.requireAny==null)continue;
                foreach(var dependency in recipe.requireAny)
                {
                    string[] path=dependency.Split('/');
                    if(path[0]!=selectedItem.id)continue;
                    var parent=selectedItem.Upgrade(path.Length==1?"unlock":path[1]);
                    if(parent!=null)yield return parent;
                }
            }
        }

        List<SkillWires.Link> TechLinks(TechUpgrade blueprint)
        {
            var links=new List<SkillWires.Link>();
            var root=techRanks[0];
            links.Add(new(SkillId.None,root.WireKey,Cyan,Entry,root.Face.rectTransform.anchoredPosition));
            foreach(var card in techRanks)
            {
                var node=card.Upgrade;
                if(node==blueprint)continue;
                if(card.TechRank>1)
                {
                    var previous=techRanks.Find(r=>r.Upgrade==node && r.TechRank==card.TechRank-1);
                    links.Add(new(previous.WireKey,card.WireKey,Cyan,previous.Face.rectTransform.anchoredPosition,card.Face.rectTransform.anchoredPosition));
                    continue;
                }
                var dependencies=TechParents(node).Select(parent=>techRanks.Find(r=>r.Upgrade==parent && r.TechRank==1)).Where(r=>r!=null);
                foreach(var parent in dependencies.DefaultIfEmpty(root))
                {
                    Vector2 p=card.Face.rectTransform.anchoredPosition,from=parent.Face.rectTransform.anchoredPosition;
                    if(card.Face.Capstone)
                    {
                        float shelf=from.y+(from.y>=0?29:-29),turn=288+Mathf.Sign(from.y)*7;
                        links.Add(new(parent.WireKey,card.WireKey,Cyan,from,new(from.x,shelf),new(turn,shelf),new(turn,p.y),p));
                    }
                    else if(p.y==from.y && p.x-from.x<110)
                        links.Add(new(parent.WireKey,card.WireKey,Cyan,from,p));
                    else if(p.x<=-210)
                        links.Add(new(parent.WireKey,card.WireKey,Cyan,from,new(-260,p.y),p));
                    else
                    {
                        float shelf=p.y+(p.y>=0?30:-30);
                        float turn=parent==root?-276:from.x+29;
                        links.Add(new(parent.WireKey,card.WireKey,Cyan,from,new(turn,from.y),new(turn,shelf),new(p.x,shelf),p));
                    }
                }
            }
            return links;
        }

        void SelectTechRank(Card card)
        {
            if(transition<1 || branch!=SkillTreeDefinition.BranchCount)return;
            selectedTech=card;shownTechView=null;ApplyLod();Refresh();
        }
        void BrowseRig(int delta)
        {
            rigIndex+=delta;shownTechView=null;Refresh();
        }

        static SkillNodeState TechStatus(TeamProgress team,TechItem item,Card card)
        {
            int owned=team.TechRank(item.id,card.Upgrade.id);
            if(card.TechRank<=owned)return SkillNodeState.Purchased;
            if(card.TechRank!=owned+1 || team.TechRequirement(item,card.Upgrade)!=null)return SkillNodeState.MissingRank;
            return team.Treasury.Covers(card.Upgrade.NextCost(owned))?SkillNodeState.Available:SkillNodeState.MissingMaterials;
        }

        void RefreshTechnology(TeamProgress team)
        {
            bool control=app.Context.NearTerminal && app.Context.Player!=null && !app.Context.Player.IsHelpless;
            if(shownTechView==team && control==techControl)return;
            shownTechView=team;techControl=control;
            if(technologyIndex.gameObject.activeInHierarchy)DisplayTechIndex(team);
            if(branch!=SkillTreeDefinition.BranchCount && targetBranch!=SkillTreeDefinition.BranchCount)return;
            ownedTechLinks.Clear();
            foreach(var card in techRanks)
            {
                var state=TechStatus(team,selectedItem,card);
                if(state==SkillNodeState.Purchased)ownedTechLinks.Add(card.WireKey);
                card.Face.State=state;card.Face.Selected=card==selectedTech;card.Face.SetVerticesDirty();
                card.Symbol.color=state==SkillNodeState.MissingRank?Muted:Cyan;
                card.Rank.color=state==SkillNodeState.MissingRank?Muted:Ink;
            }
            wires.SetOwned(ownedTechLinks,Empty);
            dataPanel.gameObject.SetActive(selectedTech!=null);hintPanel.gameObject.SetActive(selectedTech==null);
            if(selectedTech!=null)DisplayTechDetails(team);
        }

        void DisplayTechDetails(TeamProgress team)
        {
            var item=selectedItem;
            var card=selectedTech;
            var node=card.Upgrade;
            int owned=team.TechRank(item.id,node.id);
            int level=card.TechRank;
            var state=TechStatus(team,item,card);
            bool bought=state==SkillNodeState.Purchased;
            bool blueprint=node.id=="unlock";
            dataPanel.gameObject.SetActive(true);hintPanel.gameObject.SetActive(false);
            detailSymbol.sprite=TechArt(node,card.Face.Capstone);detailSymbol.color=Cyan;
            scope.color=Cyan;
            UiKit.Set(scope,blueprint?"ЭКИПАЖ  /  ЧЕРТЁЖ":"ЭКИПАЖ  /  "+SkillTreeDefinition.Roman(level));
            UiKit.Set(title,blueprint?item.title:node.title+(node.values.Length>1?" "+SkillTreeDefinition.Roman(level):""));
            UiKit.Set(description,blueprint?item.description:node.id=="stack"?
                "Увеличивает число экземпляров этого изобретения в одном кармане водолаза. Вместимость действует для всего экипажа.":node.description);
            UiKit.Set(before,ValueLabel(item,node,owned));
            UiKit.Set(after,bought?"—":ValueLabel(item,node,level));after.color=Cyan;
            int stage=TechCatalog.Stage(item,node);
            UiKit.Set(requirements[0],"Стадия "+SkillTreeDefinition.Roman(stage)+" · "+TechCatalog.StageTitle(stage));
            string blocked=bought?null:level>owned+1?"Сначала изучите ранг "+SkillTreeDefinition.Roman(level-1):team.TechRequirement(item,node);

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

        void DisplayEquipment(TeamProgress team)
        {
            var item=selectedItem;
            var rigs=team.rigs?.FindAll(r=>r.kind==item.id && r.mode is not ("dropped" or "spent"));
            int count=rigs?.Count??0;
            rigIndex=Mathf.Clamp(rigIndex,0,Math.Max(0,count-1));
            var rig=count==0?null:rigs[rigIndex];
            bool network=item.id=="trap" && team.TechRank("trap","group")>0;
            string status=item.title+" · в работе "+count+(TechRig.HasTeamLimit(item.id)?"/"+TechRig.TotalLimit(team,item):"");
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
            pendingTech=selectedItem;pendingUpgrade=selectedTech.Upgrade;pendingTechRank=selectedTech.TechRank;techAccepted=false;
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
