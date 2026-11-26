using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Raid.UI
{
    internal sealed partial class SkillTreeView
    {
        const float Width=1180, Height=810, GraphWidth=754, GraphHeight=564;
        const float TransitionSeconds=.48f, PurchaseSeconds=.38f;
        static readonly Vector2 Center=new(.5f,.5f), Entry=new(-330,0);
        static readonly Color Ink=new(.89f,.95f,.97f), Muted=new(.55f,.68f,.72f);
        static readonly Color Night=new(.025f,.059f,.079f), Steel=new(.047f,.098f,.125f), Border=new(.16f,.28f,.33f);
        static readonly Color Cyan=new(.40f,.82f,.86f), Amber=new(.98f,.71f,.39f);
        static readonly SkillId[] Empty=Array.Empty<SkillId>();
        static readonly string[] StageNames={"Подготовка","Модернизация","Профессиональное оборудование","Глубинные технологии","Экспериментальные разработки"};
        static readonly string[] MaterialNames={"Биомасса","Хитин","Металлолом","Электроника"};
        static readonly Color[] MaterialColors={new(.56f,.85f,.63f),new(.81f,.74f,.92f),new(.94f,.75f,.48f),new(.45f,.83f,.89f)};
        static readonly DiveInstruments.Mark[] BranchMarks={
            DiveInstruments.Mark.Spear,DiveInstruments.Mark.Helmet,DiveInstruments.Mark.Needle,DiveInstruments.Mark.Submarine,
            DiveInstruments.Mark.Tile,DiveInstruments.Mark.Shield,DiveInstruments.Mark.Jaw,DiveInstruments.Mark.Bristle
        };

        sealed class Sector
        {
            internal SkillSectorGraphic Face;
            internal TMP_Text Count;
        }

        sealed class Card
        {
            internal SkillNode Node;
            internal TechUpgrade Upgrade;
            internal int TechRank;
            internal SkillId WireKey;
            internal SkillNodeBadge Face;
            internal Image Symbol;
            internal TMP_Text Rank, Name;
        }

        readonly UiRoot app;
        readonly RectTransform overview, dial, detail, graphArea, graph, dataPanel, hintPanel;
        readonly RectTransform skillControls, skillGraph, statComparison, blueprintGuide;
        readonly TMP_Text blueprintDirections;
        readonly CanvasGroup overviewGroup, detailGroup;
        readonly TMP_Text section, stageTitle, title, scope, description, before, after, note, response;
        readonly TMP_Text[] treasury, costs, requirements=new TMP_Text[3];
        readonly Sector[] sectors=new Sector[SkillSectorGraphic.Sectors];
        readonly Sprite[] sprites=new Sprite[SkillTreeDefinition.BranchCount];
        readonly Card[] cards=new Card[SkillTreeDefinition.NodesPerBranch];
        readonly Button buy;
        readonly Button[] stageKeys=new Button[5];
        readonly Dictionary<SkillId,Vector2> positions=new(31);
        readonly SkillWires wires;
        readonly Image rootSymbol, detailSymbol;
        Texture2D sectorAtlas;
        RaidWorld subscribed;
        SkillId selected, pending, filled;
        MaterialCost shownTreasury;
        int branch=-1, targetBranch=-1, stage;
        int shownPlayer=int.MinValue, shownTeam=int.MinValue;
        float transition=1, fill=1, zoom=1, targetZoom=1, deadline, responseUntil, fillTime;
        Vector2 pan, targetPan, overviewFocus;
        bool revised, approved, detailLabels;
        bool ReducedMotion => app.Settings.reduceInterfaceMotion;

        public RectTransform Root { get; }

        internal SkillTreeView(Transform parent,UiRoot app)
        {
            this.app=app;
            var surface=UiKit.Frame(parent,"SkillTreeView",Width,Height);
            Root=(RectTransform)surface.parent;
            var backing=UiKit.Panel(surface,"Screen glass",Night);
            UiKit.Fill(backing.rectTransform);
            Box(surface,"Display rail",19,18,1142,71,Steel);
            Box(surface,"Rail edge",19,88,1142,2,Border);
            section=Label(surface,"Screen title",38,32,394,43,"МОДУЛИ NEREID",32,Ink);
            treasury=MaterialStrip(surface,"Treasury",718,30,420,46);

            overview=UiKit.Node(surface,"Progression sectors");
            Place(overview,0,94,1176,662);
            overview.gameObject.AddComponent<RectMask2D>();
            overviewGroup=overview.gameObject.AddComponent<CanvasGroup>();
            dial=UiKit.Node(overview,"Progression dial");
            UiKit.Place(dial,Center,Center,Vector2.zero,new(622,622));
            sectorAtlas=Resources.Load<Texture2D>("NereidProgression/Sectors");
            DrawOverview();

            detail=UiKit.Node(surface,"Progression branch");
            Place(detail,0,94,1176,658);
            detailGroup=detail.gameObject.AddComponent<CanvasGroup>();
            Key(detail,"ProgressionOverview","‹  Все ветки",19,7,158,43,()=>Back());
            skillControls=UiKit.Node(detail,"Skill navigation");
            UiKit.Fill(skillControls);
            for(int i=0;i<stageKeys.Length;i++)
            {
                int index=i+1;
                stageKeys[i]=Key(skillControls,"ProgressionStage"+index,SkillTreeDefinition.Roman(index),
                    196+i*58,7,51,43,()=>FocusStage(index));
            }
            stageTitle=Label(skillControls,"Technology stage",502,8,368,42,"",20,Muted);
            Key(skillControls,"ProgressionCenter","Центр",949,7,212,43,ResetGraph);

            graphArea=Box(detail,"ProgressionGraphViewport",19,61,GraphWidth,GraphHeight,Steel,true).rectTransform;
            graphArea.gameObject.AddComponent<RectMask2D>();
            graphArea.gameObject.AddComponent<SkillGraphPan>().View=this;
            graph=UiKit.Node(graphArea,"Skill branch graph");
            UiKit.Place(graph,Center,Center,Vector2.zero,new(GraphWidth,GraphHeight));
            skillGraph=UiKit.Node(graph,"Skill ranks");UiKit.Fill(skillGraph);
            for(int i=0;i<5;i++)
            {
                float left=103+i*109;
                Box(skillGraph,"Stage division",left,20,1,524,new Color(.106f,.192f,.226f));
                Label(skillGraph,"Stage marker",left+4,22,108,29,SkillTreeDefinition.Roman(i+1),18,Muted,true);
            }
            var ink=UiKit.Node(skillGraph,"Rank links");
            UiKit.Fill(ink);
            wires=ink.gameObject.AddComponent<SkillWires>();
            wires.raycastTarget=false;
            rootSymbol=CenterIcon(Emblem(skillGraph,"Branch entry",Entry,45),"Entry icon",29);
            for(int i=0;i<cards.Length;i++)cards[i]=MakeCard();

            var card=Box(detail,"Selected perk",793,61,368,GraphHeight,Night).rectTransform;
            Box(card,"Selected perimeter",0,0,368,2,Border);
            Box(card,"Selected divider",0,0,2,GraphHeight,Border);
            dataPanel=UiKit.Node(card,"Perk readout");
            UiKit.Fill(dataPanel);
            var module=Box(dataPanel,"Selected icon mount",19,18,52,52,Steel).rectTransform;
            detailSymbol=CenterIcon(module,"Selected icon",31);
            scope=Label(dataPanel,"Scope",88,25,250,38,"",22,Muted);
            title=Paragraph(dataPanel,"Full perk name",22,83,325,78,31,Ink,20);
            description=Paragraph(dataPanel,"Real effect",22,164,325,69,22,Muted,17);
            statComparison=UiKit.Node(dataPanel,"Effect comparison");
            UiKit.Fill(statComparison);
            Box(statComparison,"Effect strip",22,242,324,82,Steel);
            Label(statComparison,"Current value caption",29,245,134,20,"СЕЙЧАС",16,Muted,true);
            Label(statComparison,"Target value caption",204,245,134,20,"ПОСЛЕ",16,Muted,true);
            before=Label(statComparison,"Current value",30,270,132,44,"",29,Ink,true);
            Label(statComparison,"Effect direction",162,270,42,44,"→",26,Muted,true);
            after=Label(statComparison,"New value",201,270,137,44,"",29,Cyan,true);
            blueprintGuide=UiKit.Node(dataPanel,"Invention guide");
            UiKit.Fill(blueprintGuide);
            Box(blueprintGuide,"Guide background",22,164,324,184,Steel);
            Box(blueprintGuide,"Guide accent",22,164,3,184,Cyan);
            Label(blueprintGuide,"Guide label",36,172,285,24,"НАЗНАЧЕНИЕ",16,Cyan);
            blueprintDirections=Paragraph(blueprintGuide,"Invention instructions",34,201,303,136,19,Ink,16);
            blueprintGuide.gameObject.SetActive(false);
            for(int i=0;i<requirements.Length;i++)
                requirements[i]=Label(dataPanel,"Requirement "+(i+1),23,335+i*25,323,24,"",18,Muted);
            costs=MaterialStrip(dataPanel,"Purchase cost",19,421,330,47);
            note=Label(dataPanel,"Availability",22,471,324,31,"",18,Muted,true);
            buy=Key(dataPanel,"BuySkill","Купить  ·  ПКМ по узлу",22,509,324,42,()=>{if(TechnologyOpen)BuyTechnology();else Buy(selected);});

            hintPanel=UiKit.Node(card,"Branch guidance");
            UiKit.Fill(hintPanel);
            var hintIcon=CenterIcon(hintPanel,"Selected perk hint icon",64);
            hintIcon.sprite=DiveInstruments.Art(DiveInstruments.Mark.Compass);
            hintIcon.rectTransform.anchoredPosition=new Vector2(0,38);
            Label(hintPanel,"Selected perk hint",18,322,332,35,"Выберите узел",24,Muted,true);

            Label(skillControls,"Graph controls",20,628,750,29,"ЛКМ — выбор   ·   ПКМ — покупка   ·   Колесо — масштаб   ·   ЛКМ на поле — сдвиг",18,Muted);
            response=Label(surface,"Purchase reply",24,762,1128,35,"",20,Ink,true);
            BuildTechnologyPage();
            ResetMode();
            Root.gameObject.SetActive(false);
        }

        void DrawOverview()
        {
            for(int i=0;i<sectors.Length;i++)
            {
                int index=i;
                bool workshop=index==SkillTreeDefinition.BranchCount;
                Vector2 axis=SkillBoardMesh.At(index*SkillSectorGraphic.Spacing);
                Color accent=workshop?TechGreen:index>=3 && index<=6?Amber:Cyan;
                var container=UiKit.Node(dial,"Sector"+index);
                UiKit.Place(container,Center,Center,Vector2.zero,new(622,622));
                var face=container.gameObject.AddComponent<SkillSectorGraphic>();
                face.Bind(index,accent);
                var click=container.gameObject.AddComponent<SkillSectorButton>();
                click.Face=face;
                click.targetGraphic=face;
                click.transition=Selectable.Transition.None;
                click.onClick.AddListener(()=>{if(workshop)OpenTechnology();else ChangeBranch(index);});
                var icon=CenterIcon(container,"Direction icon",58);
                icon.sprite=workshop?DiveInstruments.Art(DiveInstruments.Mark.Wrench):SectorArt(index);
                icon.color=Color.white;
                UiKit.Place(icon.rectTransform,Center,Center,axis*222+Vector2.up*26,new(58,58));
                TMP_Text Centered(string name,Vector2 point,Vector2 size,string caption,int font,Color tint)
                {
                    var label=Label(container,name,0,0,size.x,size.y,caption,font,tint,true);
                    UiKit.Place(label.rectTransform,Center,Center,point,size);
                    return label;
                }
                Centered("Direction",axis*222-Vector2.up*14,new(144,30),workshop?"Технологии":SkillTreeDefinition.Branches[i].ShortName,22,Ink);
                var count=Centered("Owned direction ranks",axis*216-Vector2.up*46,new(80,24),"",19,accent);
                sectors[index]=new Sector { Face=face, Count=count };
            }
            var hub=Emblem(dial,"NEREID",Vector2.zero,195);
            var ship=CenterIcon(hub,"NEREID silhouette",65);
            ship.sprite=SectorArt(3);
            ship.color=Color.white;
            ship.rectTransform.anchoredPosition=new(0,19);
            Label(hub,"NEREID hull type",16,111,163,38,"NEREID",30,Ink,true);
        }

        Sprite SectorArt(int index)
        {
            if(sprites[index]!=null)return sprites[index];
            if(sectorAtlas==null)return DiveInstruments.Art(BranchMarks[index]);
            float w=sectorAtlas.width/4f,h=sectorAtlas.height/2f;
            var cell=new Rect(index%4*w,(index<4?1:0)*h,w,h);
            return sprites[index]=Sprite.Create(sectorAtlas,cell,Center,100,0,SpriteMeshType.FullRect);
        }

        Card MakeCard()
        {
            var rect=UiKit.Node(skillGraph,"Rank");
            UiKit.Place(rect,Center,Center,Vector2.zero,new(48,48));
            var face=rect.gameObject.AddComponent<SkillNodeBadge>();
            var button=rect.gameObject.AddComponent<SkillPurchaseButton>();
            button.targetGraphic=face;
            button.transition=Selectable.Transition.None;
            var icon=CenterIcon(rect,"Rank symbol",19);
            var rank=Label(rect,"Rank",0,20,48,28,"",23,Ink,true);
            var name=Label(rect,"Nearby node name",-49,-18,147,19,"",14,Ink,true);
            name.gameObject.SetActive(false);
            var result=new Card{Face=face,Symbol=icon,Rank=rank,Name=name};
            button.onClick.AddListener(()=>{if(result.Upgrade!=null)SelectTechRank(result);else Select(result.Node.Id);});
            button.Purchase=()=>
            {
                if(result.Upgrade==null)Buy(result.Node.Id);
                else{SelectTechRank(result);BuyTechnology();}
            };
            return result;
        }

        static RectTransform Emblem(Transform parent,string name,Vector2 position,float size)
        {
            var rect=UiKit.Node(parent,name);
            UiKit.Place(rect,Center,Center,position,new(size,size));
            var face=rect.gameObject.AddComponent<SkillNodeBadge>();
            face.raycastTarget=false;
            face.Accent=Cyan;
            face.State=SkillNodeState.Purchased;
            return rect;
        }

        internal void Open()
        {
            pending=filled=SkillId.None;
            ClearTechRequest();
            approved=false;
            deadline=responseUntil=0;
            UiKit.Set(response,"");
            ResetMode();
        }

        internal void Close()=>Subscribe(null);

        internal bool Back()
        {
            if(transition<1)return true;
            if(branch<0 && technologyIndex.gameObject.activeSelf)
            {
                if(category>=0)ShowTechnologyIndex(-1);else ResetMode();
                return true;
            }
            if(branch<0)return false;
            ChangeBranch(-1);
            return true;
        }

        void ResetMode()
        {
            branch=targetBranch=-1;
            transition=1;
            selected=SkillId.None;
            technologyIndex.gameObject.SetActive(false);
            dial.gameObject.SetActive(true);
            overview.gameObject.SetActive(true);
            detail.gameObject.SetActive(false);
            overviewGroup.alpha=1;
            overviewGroup.blocksRaycasts=true;
            dial.localScale=Vector3.one;
            dial.anchoredPosition=Vector2.zero;
            UiKit.Set(section,"МОДУЛИ NEREID");
            Invalidate();
        }

        void ChangeBranch(int index)
        {
            if(transition<1 || branch==index)return;
            targetBranch=index;
            transition=0;
            overviewFocus=SkillBoardMesh.At(SkillSectorGraphic.Spacing*(index>=0?index:branch))*216;
            overviewGroup.blocksRaycasts=false;
            detailGroup.blocksRaycasts=false;
            if(index>=0)
            {
                if(index==SkillTreeDefinition.BranchCount)BindTechBranch();else BindBranch(index);
                detail.gameObject.SetActive(true);
                detailGroup.alpha=0;
            }
            else
            {
                overview.gameObject.SetActive(true);
                overviewGroup.alpha=0;
            }
            if(ReducedMotion)CompleteTransition();
        }

        void CompleteTransition()
        {
            branch=targetBranch;
            transition=1;
            overview.gameObject.SetActive(branch<0);
            detail.gameObject.SetActive(branch>=0);
            overviewGroup.alpha=detailGroup.alpha=1;
            overviewGroup.blocksRaycasts=branch<0;
            detailGroup.blocksRaycasts=branch>=0;
            dial.localScale=Vector3.one;
            dial.anchoredPosition=Vector2.zero;
            detail.localScale=Vector3.one;
            UiKit.Set(section,branch<0?(technologyIndex.gameObject.activeSelf?Categories[category]:"МОДУЛИ NEREID"):
                branch==SkillTreeDefinition.BranchCount?selectedItem.title:SkillTreeDefinition.Branches[branch].Name);
            shownTechView=null;
            Invalidate();
            Refresh();
        }

        void BindBranch(int index)
        {
            ClearCards();
            positions.Clear();
            int used=0;
            rootSymbol.sprite=SectorArt(index);
            foreach(var node in SkillTreeDefinition.All)
            {
                if(node.Branch!=index)continue;
                var card=cards[used++];
                card.Node=node;
                var point=NodePosition(node);
                BindRankBadge(card,node.Key,point,node.Capstone,Accent(node),NodeArt(node),node.Rank,node.LineName);
                positions.Add(node.Id,point);
            }
            for(int i=used;i<cards.Length;i++)cards[i].Face.gameObject.SetActive(false);
            wires.SetLinks(BranchLinks(used));
            selected=SkillId.None;
            ResetGraph();
            ApplyLod();
            Invalidate();
        }

        void ClearCards()
        {
            foreach(var card in cards)
            {
                card.Node=null;card.Upgrade=null;card.Face.gameObject.SetActive(false);
            }
            selectedTech=null;
            wires.AnimateLink(SkillId.None,1);
            buy.name="BuySkill";
            UiKit.Caption(buy,"Купить  ·  ПКМ по узлу");
        }

        static void BindRankBadge(Card card,string key,Vector2 point,bool capstone,Color accent,Sprite icon,int level,string name)
        {
            card.Face.Capstone=capstone;card.Face.Accent=accent;card.Face.PurchaseFill=1;
            var rect=card.Face.rectTransform;
            rect.name=key;rect.sizeDelta=Vector2.one*(capstone?82:48);rect.anchoredPosition=point;
            card.Symbol.sprite=icon;
            UiKit.Place(card.Symbol.rectTransform,Center,Center,capstone?Vector2.zero:new Vector2(0,9),Vector2.one*(capstone?34:19));
            UiKit.Place(card.Rank.rectTransform,Center,Center,new(0,-10),new(43,20));
            UiKit.Set(card.Rank,capstone?"":SkillTreeDefinition.Roman(level));
            UiKit.Set(card.Name,name);card.Name.gameObject.SetActive(false);rect.gameObject.SetActive(true);
        }

        List<SkillWires.Link> BranchLinks(int count)
        {
            var links=new List<SkillWires.Link>(40);
            for(int i=0;i<count;i++)
            {
                var node=cards[i].Node;
                Vector2 p=positions[node.Id];
                Color accent=Accent(node);
                var deps=SkillTreeDefinition.Dependencies(node.Id);
                if(deps.Count==0)
                {
                    links.Add(new(SkillId.None,node.Id,accent,Entry,new(-289,0),new(-270,p.y),p));
                    continue;
                }
                foreach(var dep in deps)
                {
                    if(!positions.TryGetValue(dep,out var from))continue;
                    if(node.Capstone)
                    {
                        int lane=(int)SkillTreeDefinition.Get(dep).Line%SkillTreeDefinition.LinesPerBranch;
                        float shelf=from.y+(lane<=2?29:-29);
                        float turn=273+lane*5;
                        links.Add(new(dep,node.Id,accent,from,new(from.x,shelf),new(turn,shelf),new(turn,p.y),p));
                    }
                    else if(Mathf.Approximately(from.y,p.y))
                        links.Add(new(dep,node.Id,accent,from,p));
                    else
                        links.Add(new(dep,node.Id,accent,from,new((p.x+from.x)*.5f,p.y),p));
                }
            }
            return links;
        }

        Sprite NodeArt(SkillNode node)
        {
            if(node.Capstone)return SectorArt(node.Branch);
            var mark=node.Line switch
            {
                SkillLine.Reel or SkillLine.Tip or SkillLine.GoshaAutonomy=>DiveInstruments.Mark.Spear,
                SkillLine.Cable or SkillLine.Winch or SkillLine.Separator or SkillLine.Chitin=>DiveInstruments.Mark.Bristle,
                SkillLine.Damper or SkillLine.Armor or SkillLine.TugStability or SkillLine.CargoDefense=>
                    DiveInstruments.Mark.Shield,
                SkillLine.SyncPull or SkillLine.CargoCarry or SkillLine.Pockets or SkillLine.GoshaClaws or SkillLine.GoshaDelivery=>DiveInstruments.Mark.Glove,
                SkillLine.Oxygen or SkillLine.EmergencyAir or SkillLine.Medical or SkillLine.Satiety=>DiveInstruments.Mark.Bottle,
                SkillLine.Seal=>DiveInstruments.Mark.Helmet,
                SkillLine.Pressure or SkillLine.SwimmingTurn or SkillLine.Scanner=>DiveInstruments.Mark.Compass,
                SkillLine.Fins or SkillLine.SwimStart or SkillLine.GoshaSpeed=>DiveInstruments.Mark.Needle,
                SkillLine.Hull=>DiveInstruments.Mark.Submarine,
                SkillLine.Searchlights or SkillLine.Electronics or SkillLine.GoshaScent=>DiveInstruments.Mark.Lantern,
                SkillLine.Airlock or SkillLine.Cage or SkillLine.Reservoir or SkillLine.Scrap or SkillLine.Carryover=>
                    DiveInstruments.Mark.Tile,
                SkillLine.Nutrition=>DiveInstruments.Mark.Jaw,
                SkillLine.SmallRation or SkillLine.LargeRation or SkillLine.Biomass=>DiveInstruments.Mark.Ray,
                _=>BranchMarks[node.Branch]
            };
            return DiveInstruments.Art(mark);
        }

        static Vector2 NodePosition(SkillNode node)
        {
            int lane=(int)node.Line%SkillTreeDefinition.LinesPerBranch;
            if(node.Capstone)return new(330,0);
            if(lane==0)return new(-248+(node.Rank-1)*54,0);
            return SideRankPosition(lane-1,node.Rank-1);
        }

        static Vector2 SideRankPosition(int lane,int column) =>
            new(-221+column*108,lane switch {0=>169,1=>84,2=>-85,_=>-170});

        void ResetGraph()
        {
            stage=0;
            targetPan=Vector2.zero;
            targetZoom=1;
            ShowStage();
        }

        void FocusStage(int index)
        {
            if(transition<1 || branch<0)return;
            stage=index;
            targetZoom=1.75f;
            float column=-221+(index-1)*108;
            targetPan=ClampPan(new(-column*targetZoom,0),targetZoom);
            ShowStage();
        }

        void ShowStage()
        {
            UiKit.Set(stageTitle,stage==0?"":StageNames[stage-1]);
            for(int i=0;i<stageKeys.Length;i++)
                stageKeys[i].targetGraphic.color=stage==i+1?new Color(.14f,.27f,.32f):Steel;
        }

        internal void Pan(Vector2 delta)
        {
            if(transition<1 || branch<0)return;
            targetPan=ClampPan(targetPan+delta,targetZoom);
        }

        internal void Zoom(PointerEventData pointer)
        {
            if(transition<1 || branch<0 ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(graphArea,pointer.position,pointer.enterEventCamera,out var point))
                return;
            float next=Mathf.Clamp(targetZoom*Mathf.Pow(1.22f,pointer.scrollDelta.y),1,2.65f);
            targetPan=ClampPan(point-(point-targetPan)*(next/targetZoom),next);
            targetZoom=next;
        }

        Vector2 ClampPan(Vector2 requested,float scale)
        {
            Vector2 limit=graphArea.rect.size*(scale-1)*.5f;
            return new(Mathf.Clamp(requested.x,-limit.x,limit.x),Mathf.Clamp(requested.y,-limit.y,limit.y));
        }

        internal void Animate(float dt)
        {
            if(!Root.gameObject.activeInHierarchy)return;
            if(transition<1)
            {
                transition=Mathf.Min(1,transition+dt/TransitionSeconds);
                float blend=Mathf.SmoothStep(0,1,transition);
                float phase=targetBranch>=0?blend:1-blend;
                dial.localScale=Vector3.one*Mathf.Lerp(1,1.36f,phase);
                dial.anchoredPosition=-overviewFocus*(.38f*phase);
                overviewGroup.alpha=1-phase;
                detailGroup.alpha=phase;
                detail.localScale=Vector3.one*Mathf.Lerp(.97f,1,phase);
                if(transition>=1)CompleteTransition();
            }

            float ease=ReducedMotion?1:1-Mathf.Exp(-16*dt);
            if(Mathf.Abs(zoom-targetZoom)>.0001f || (pan-targetPan).sqrMagnitude>.001f)
            {
                zoom=Mathf.Abs(zoom-targetZoom)<.002f?targetZoom:Mathf.Lerp(zoom,targetZoom,ease);
                pan=(pan-targetPan).sqrMagnitude<.02f?targetPan:Vector2.Lerp(pan,targetPan,ease);
                graph.localScale=Vector3.one*zoom;
                graph.anchoredPosition=pan;
            }
            bool showNames=zoom>=1.55f;
            if(detailLabels!=showNames)
            {
                detailLabels=showNames;
                ApplyLod();
            }

            if(fill<1 && filled!=SkillId.None)
            {
                fillTime+=dt;
                fill=ReducedMotion?1:Mathf.Clamp01(fillTime/PurchaseSeconds);
                PresentPurchaseFill();
            }
            if(responseUntil>0 && Time.unscaledTime>responseUntil)
            {
                responseUntil=0;
                UiKit.Set(response,"");
            }
        }

        void PresentPurchaseFill()
        {
            foreach(var card in cards)
            {
                if(card.Node?.Id!=filled)continue;
                card.Face.PurchaseFill=fill;
                card.Face.SetVerticesDirty();
                break;
            }
            wires.AnimateLink(filled,fill);
        }

        void ApplyLod()
        {
            foreach(var card in cards)
            {
                if(card.Upgrade!=null)
                {
                    card.Name.gameObject.SetActive(detailLabels && (card==selectedTech || card.TechRank==1 || card.Face.Capstone));
                    continue;
                }
                if(card.Node==null)continue;
                bool show=detailLabels&&(card.Node.Id==selected || card.Node.Rank==1 || card.Node.Capstone);
                if(card.Node.Id==selected && !card.Node.Capstone)UiKit.Set(card.Name,card.Node.Name);
                else UiKit.Set(card.Name,card.Node.LineName);
                card.Name.gameObject.SetActive(show);
            }
        }

        void Subscribe(RaidWorld world)
        {
            if(subscribed==world)return;
            if(subscribed!=null)subscribed.SkillResponded-=OnReply;
            subscribed=world;
            if(subscribed!=null)subscribed.SkillResponded+=OnReply;
            Invalidate();
        }

        void Invalidate()=>revised=true;

        internal void Refresh()
        {
            if(!Root.gameObject.activeInHierarchy)return;
            Subscribe(app.Context.World);
            var profile=subscribed?.LocalProgress;
            var team=app.Context.View;
            CheckTechRequest(team);
            if(TechnologyOpen && team!=null)RefreshTechnology(team);
            int p=profile?.revision??-1, t=team?.teamRevision??-1;
            var money=team?.Treasury??default;
            if(pending!=SkillId.None)
            {
                if(approved && Owned(SkillTreeDefinition.Get(pending)).Contains(pending))
                {
                    filled=pending;
                    fill=ReducedMotion?1:0;
                    fillTime=0;
                    pending=SkillId.None;
                    approved=false;
                    PresentPurchaseFill();
                    Invalidate();
                }
                else if(Time.unscaledTime>deadline)
                {
                    pending=SkillId.None;
                    approved=false;
                    ShowResponse("Хост не ответил",false);
                    Invalidate();
                }
            }

            if(!revised && shownPlayer==p && shownTeam==t && SameMaterials(shownTreasury,money))return;
            revised=false;
            shownPlayer=p;
            shownTeam=t;
            shownTreasury=money;
            PresentMaterials(treasury,money);
            RefreshSectors();
            if(TechnologyOpen || branch<0 && targetBranch<0)return;
            wires.SetOwned(PersonalSkills(),TeamSkills());
            foreach(var card in cards) if(card.Node!=null)DisplayCard(card,money);
            DisplayDetails(money);
        }

        void RefreshSectors()
        {
            Span<int> count=stackalloc int[SkillTreeDefinition.BranchCount];
            count.Clear();
            foreach(var id in PersonalSkills())
            {
                var item=SkillTreeDefinition.Get(id);
                if(item!=null)count[item.Branch]++;
            }
            foreach(var id in TeamSkills())
            {
                var item=SkillTreeDefinition.Get(id);
                if(item!=null)count[item.Branch]++;
            }
            for(int i=0;i<count.Length;i++)
            {
                sectors[i].Face.SetProgress(count[i]/(float)SkillTreeDefinition.NodesPerBranch);
                UiKit.Set(sectors[i].Count,count[i]+" / "+SkillTreeDefinition.NodesPerBranch);
            }
            var team=app.Context.View;
            int ownedRanks=0,totalRanks=0;
            foreach(var item in TechCatalog.Items)
                foreach(var upgrade in item.upgrades)
                {
                    ownedRanks+=team?.TechRank(item.id,upgrade.id)??0;
                    totalRanks+=upgrade.values.Length;
                }
            var inventions=sectors[SkillTreeDefinition.BranchCount];
            inventions.Face.SetProgress(totalRanks>0?(float)ownedRanks/totalRanks:0);
            UiKit.Set(inventions.Count,ownedRanks+" / "+totalRanks);
        }

        void DisplayCard(Card card,MaterialCost money)
        {
            var node=card.Node;
            var owned=Owned(node);
            SkillNodeState state=Status(node,owned,money);
            card.Face.State=state;
            card.Face.Selected=selected==node.Id;
            card.Face.PurchaseFill=pending==node.Id?0:filled==node.Id?fill:1;
            card.Symbol.color=state==SkillNodeState.MissingRank?Muted:Accent(node);
            card.Rank.color=state==SkillNodeState.MissingRank?Muted:Ink;
            card.Face.SetVerticesDirty();
        }

        static SkillNodeState Status(SkillNode node,IReadOnlyCollection<SkillId> owned,MaterialCost money)
        {
            if(owned.Contains(node.Id))return SkillNodeState.Purchased;
            if(!SkillTreeDefinition.IsAvailable(node.Id,owned))return SkillNodeState.MissingRank;
            return money.Covers(node.Cost)?SkillNodeState.Available:SkillNodeState.MissingMaterials;
        }

        void DisplayDetails(MaterialCost money)
        {
            var node=SkillTreeDefinition.Get(selected);
            dataPanel.gameObject.SetActive(node!=null);
            hintPanel.gameObject.SetActive(node==null);
            if(node==null)return;
            var owned=Owned(node);
            var state=Status(node,owned,money);
            Color accent=Accent(node);
            detailSymbol.sprite=NodeArt(node);
            detailSymbol.color=accent;
            scope.color=accent;
            UiKit.Set(scope,node.Team?"КОМАНДА":"ЛИЧНОЕ");
            UiKit.Set(title,node.Name);
            ShowBlueprintGuide(false);
            UiKit.Set(description,node.Effect);
            int magnitude=SkillTreeDefinition.Value(owned,node.Line);
            UiKit.Set(before,SkillTreeDefinition.FormatValue(node.Line,magnitude));
            UiKit.Set(after,state==SkillNodeState.Purchased?"—":node.ValueText);
            after.color=accent;

            var deps=SkillTreeDefinition.Dependencies(node.Id);
            for(int i=0;i<requirements.Length;i++)
            {
                string requirement="";
                if(i<deps.Count)
                {
                    bool complete=owned.Contains(deps[i]);
                    requirement=(complete?"+  ":"—  ")+SkillTreeDefinition.Get(deps[i]).Name;
                    requirements[i].color=complete?Muted:Amber;
                }
                UiKit.Set(requirements[i],requirement);
            }
            PresentMaterials(costs,node.Cost,money);
            bool waiting=pending!=SkillId.None;
            bool awaitingProfile=subscribed?.LocalProgress==null;
            note.color=state==SkillNodeState.Available?Cyan:state==SkillNodeState.Purchased?Muted:Amber;
            UiKit.Set(note,waiting?"Подтверждение хоста…":awaitingProfile?"Подключение профиля…":state switch
            {
                SkillNodeState.Purchased=>"Куплено",
                SkillNodeState.MissingRank=>"Требуются предыдущие ранги",
                SkillNodeState.MissingMaterials=>"Не хватает материалов",
                _=>"Готово к покупке"
            });
            buy.interactable=!waiting && !awaitingProfile && state==SkillNodeState.Available;
        }

        void Select(SkillId id)
        {
            if(transition<1 || branch<0 || SkillTreeDefinition.Get(id)?.Branch!=branch)return;
            selected=id;
            Invalidate();
            ApplyLod();
            Refresh();
        }

        void Buy(SkillId id)
        {
            var context=app.Context;
            var profile=context.World?.LocalProgress;
            var team=context.View;
            var node=SkillTreeDefinition.Get(id);
            if(transition<1 || pending!=SkillId.None || pendingTech!=null || profile==null || team==null ||
                context.Player==null || context.Player.IsHelpless || !context.NearTerminal || node?.Branch!=branch)return;
            Select(id);
            if(Status(node,Owned(node),team.Treasury)!=SkillNodeState.Available)return;
            pending=id;
            approved=false;
            deadline=Time.unscaledTime+7;
            Invalidate();
            Refresh();
            context.World.RequestSkill(id,team.teamRevision,profile.revision);
        }

        void OnReply(bool success,string explanation)
        {
            if(pendingTech!=null)
            {
                techAccepted=success;
                if(!success)ClearTechRequest();
                shownTechView=null;
                ShowResponse(explanation,success);
                Refresh();
                return;
            }
            if(pending==SkillId.None)return;
            approved=success;
            if(!success)pending=SkillId.None;
            ShowResponse(success?"Модуль открыт":explanation,success);
            Invalidate();
            Refresh();
        }

        void ShowResponse(string reason,bool success)
        {
            response.color=success?Cyan:Amber;
            responseUntil=Time.unscaledTime+3.5f;
            UiKit.Set(response,reason);
        }

        IReadOnlyList<SkillId> PersonalSkills()=>subscribed?.LocalProgress?.skills??(IReadOnlyList<SkillId>)Empty;
        IReadOnlyList<SkillId> TeamSkills()=>app.Context.View?.teamSkills??(IReadOnlyList<SkillId>)Empty;
        IReadOnlyList<SkillId> Owned(SkillNode node)=>node.Team?TeamSkills():PersonalSkills();
        static Color Accent(SkillNode node)=>node.Team?Amber:Cyan;

        static TMP_Text[] MaterialStrip(Transform parent,string name,float x,float y,float width,float h)
        {
            var strip=Box(parent,name,x,y,width,h,Steel).rectTransform;
            var values=new TMP_Text[4];
            for(int i=0;i<4;i++)
            {
                float column=(width/4)*i;
                var tile=Box(strip,MaterialNames[i]+" counter",column+2,1,width/4-4,h-2,Night).rectTransform;
                var shape=UiKit.Node(tile,MaterialNames[i]);
                Place(shape,4,(h-24)*.5f,24,24);
                var mark=shape.gameObject.AddComponent<SkillMaterialGlyph>();
                mark.raycastTarget=false;
                mark.Kind=(SkillMaterial)i;
                mark.color=MaterialColors[i];
                values[i]=Label(tile,"Amount",30,0,width/4-36,h-3,"—",23,Ink,true);
            }
            return values;
        }

        static int Amount(MaterialCost amount,int i)=>i switch
        {
            0=>amount.biomass,1=>amount.chitin,2=>amount.scrap,_=>amount.electronics
        };

        static bool SameMaterials(MaterialCost a,MaterialCost b)=>
            a.biomass==b.biomass&&a.chitin==b.chitin&&a.scrap==b.scrap&&a.electronics==b.electronics;

        static void PresentMaterials(TMP_Text[] text,MaterialCost amount,MaterialCost? available=null)
        {
            for(int i=0;i<text.Length;i++)
            {
                int number=Amount(amount,i);
                text[i].color=available.HasValue && number>Amount(available.Value,i)?Amber:Ink;
                UiKit.Set(text[i],number.ToString());
            }
        }

        static void Place(RectTransform rect,float x,float y,float width,float height) =>
            UiKit.Place(rect,Vector2.up,Vector2.up,new(x,-y),new(width,height));

        static Image Box(Transform parent,string name,float x,float y,float width,float height,Color color,bool raycast=false)
        {
            var image=UiKit.Panel(parent,name,color,raycast);
            Place(image.rectTransform,x,y,width,height);
            return image;
        }

        static Image CenterIcon(Transform parent,string name,float side)
        {
            var icon=UiKit.Panel(parent,name,Ink);
            icon.preserveAspect=true;
            UiKit.Place(icon.rectTransform,Center,Center,Vector2.zero,new(side,side));
            return icon;
        }

        static TMP_Text Label(Transform parent,string name,float x,float y,float w,float h,string value,
            float font,Color color,bool center=false)
        {
            var label=UiKit.Label(parent,name,value,font,color);
            Place(label.rectTransform,x,y,w,h);
            label.alignment=center?TextAlignmentOptions.Center:TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode=TextWrappingModes.NoWrap;
            label.overflowMode=TextOverflowModes.Ellipsis;
            label.fontSizeMin=Mathf.Min(15,font);
            return label;
        }

        static TMP_Text Paragraph(Transform parent,string name,float x,float y,float w,float h,
            float font,Color color,float min)
        {
            var label=Label(parent,name,x,y,w,h,"",font,color);
            label.textWrappingMode=TextWrappingModes.Normal;
            label.alignment=TextAlignmentOptions.TopLeft;
            label.fontSizeMin=min;
            return label;
        }

        static Button Key(Transform parent,string name,string caption,float x,float y,float w,float h,Action command)
        {
            var button=UiKit.Button(parent,name,caption,()=>command(),false,h);
            Place((RectTransform)button.transform,x,y,w,h);
            button.targetGraphic.color=Steel;
            return button;
        }
    }

    internal sealed class SkillPurchaseButton : Button
    {
        internal Action Purchase;
    }

    internal sealed class SkillGraphPan : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
    {
        internal SkillTreeView View;
        public void OnPointerDown(PointerEventData data) { }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data)=>View.Pan(data.delta);
        public void OnScroll(PointerEventData data)=>View.Zoom(data);
    }
}
