using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Raid.UI
{
    internal sealed class SkillTreeView
    {
        static readonly Vector2 Center = new(.5f, .5f);
        static readonly Color Glass = new(.025f,.065f,.071f,1);
        static readonly Color Panel = new(.053f,.115f,.123f,1);
        static readonly Color Cyan = new(.43f,.93f,.84f);
        static readonly Color Gold = new(1f,.72f,.32f);
        static readonly Color Rest = new(.43f,.57f,.59f);
        static readonly string[] Branch = {"Гарпун","Костюм","Плавание","Сканирование","Добыча","Гоша","Контейнер","Переработка","Кормление"};
        static readonly DiveInstruments.Mark[] Icon = {
            DiveInstruments.Mark.Spear,DiveInstruments.Mark.Bottle,DiveInstruments.Mark.Glove,
            DiveInstruments.Mark.Compass,DiveInstruments.Mark.Bristle,DiveInstruments.Mark.Lantern,
            DiveInstruments.Mark.Tile,DiveInstruments.Mark.Shield,DiveInstruments.Mark.Jaw
        };
        static readonly SkillId[] Empty = Array.Empty<SkillId>();

        sealed class Card
        {
            internal SkillId Id;
            internal int Branch, Step;
            internal RectTransform Root;
            internal Image Border, Background, Symbol;
            internal TMP_Text Title, Scope, State;
        }

        readonly UiRoot app;
        readonly List<Card> cards = new(36);
        readonly Vector2[] positions = new Vector2[37];
        readonly TMP_Text playerLabel, teamBalance, detailTitle, detailBody, detailCost, detailState, response;
        readonly RectTransform graph, graphArea;
        readonly Button focus;
        readonly SkillWires wires;
        RaidWorld subscribed;
        SkillId selected;
        Vector2 pan, targetPan;
        float zoom = 1, targetZoom = 1, pendingUntil;
        bool awaiting;
        int previousRevision, displayedPlayer = int.MinValue, displayedTeam = int.MinValue;
        bool confirmed;
        SkillId displayedSelection = (SkillId)(-1);
        float lastLod = -1;
        public RectTransform Root { get; }

        internal SkillTreeView(Transform parent, UiRoot app)
        {
            this.app = app;
            var surface = UiKit.Frame(parent,"SkillTreeView",1180,810);
            Root = (RectTransform)surface.parent;
            var heading = Image(surface,"Header",Vector2.up,new(14,-12),new(1148,95),Panel);
            Label(heading.transform,"Section title",Vector2.up,new(13,-7),new(708,32),
                "СНАРЯЖЕНИЕ ЭКИПАЖА",30,Cyan);
            playerLabel = Label(heading.transform,"Viewing diver",Vector2.up,new(14,-44),new(620,20),"",17,UiTheme.Muted);
            teamBalance = Label(heading.transform,"Ship treasury",Vector2.up,new(14,-66),new(1090,23),"",19,Gold);
            Key(heading.transform,"ResetSkillView","Сбросить вид",new(948,-10),186,Reset);

            graphArea = Image(surface,"Skill graph viewport",Vector2.up,new(14,-114),new(1148,531),Glass,true).rectTransform;
            graphArea.gameObject.AddComponent<RectMask2D>();
            graphArea.gameObject.AddComponent<SkillGraphPan>().View = this;
            graph = UiKit.Node(graphArea,"Skill tree geometry");
            UiKit.Place(graph,Center,Center,Vector2.zero,new(1148,531));
            var wireObject = UiKit.Node(graph,"Nine branches").gameObject;
            wireObject.AddComponent<CanvasRenderer>();
            wires = wireObject.AddComponent<SkillWires>();
            UiKit.Fill(wires.rectTransform);
            wires.raycastTarget = false;

            for (int branch=0;branch<Branch.Length;branch++)
            {
                float angle = (90-SkillTreeDefinition.Angles[branch])*Mathf.Deg2Rad;
                for (int step=0;step<4;step++)
                {
                    int index = branch*4+step+1;
                    float radial = step==0?.60f:step==3?.96f:.85f;
                    float angular = step==1?-.182f:step==2?.182f:0;
                    float a = angle+angular;
                    positions[index] = new Vector2(535*Mathf.Cos(a),246*Mathf.Sin(a))*radial;
                    cards.Add(MakeCard((SkillId)index,branch,step,positions[index]));
                }
            }
            wires.Build(positions);
            var start = Image(graph,"ROOT",Center,Vector2.zero,new(144,65),new(.15f,.32f,.32f));
            var core = Image(start.transform,"Start node",Center,Vector2.zero,new(139,60),Glass);
            Label(core.transform,"NEREID",Center,new(0,12),new(135,32),"NEREID",24,Cyan,true);
            Label(core.transform,"Starting equipment",Center,new(0,-13),new(132,20),"СНАРЯЖЕНИЕ",13,UiTheme.Text,true);
            start.raycastTarget=true;
            var resetSelection=start.gameObject.AddComponent<Button>();
            resetSelection.onClick.AddListener(()=>Select(SkillId.None));

            var info = Image(surface,"Selected skill",Vector2.up,new(14,-652),new(1148,97),Panel,true).rectTransform;
            detailTitle = Label(info,"Skill and scope",Vector2.up,new(14,-5),new(765,29),"",23,Cyan);
            detailBody = Label(info,"Skill benefit",Vector2.up,new(14,-33),new(765,26),"",17);
            detailCost = Label(info,"Cost and prerequisite",Vector2.up,new(14,-62),new(765,28),"",15,UiTheme.Muted);
            detailState = Label(info,"Skill state",Vector2.up,new(793,-48),new(340,37),"",17,UiTheme.Amber);
            focus = Key(info,"FocusSkillBranch","Фокус на ветке",new(793,-6),340,Focus);
            response = Label(surface,"Server result",Vector2.up,new(22,-774),new(1118,22),"",16,Cyan);
            Label(surface,"Controls",Vector2.up,new(22,-752),new(1118,22),
                "ЛКМ — выбрать  ·  ПКМ — купить  ·  Колесо — масштаб  ·  Зажать ЛКМ на поле — переместить  ·  Esc — выйти",16,UiTheme.Muted);

            Root.gameObject.SetActive(false);
        }

        Card MakeCard(SkillId id,int branch,int step,Vector2 position)
        {
            Color accent = branch < 6 ? Cyan : Gold;
            var border = Image(graph,id.ToString(),Center,position,new(35,35),accent,true);
            var button = border.gameObject.AddComponent<SkillPurchaseButton>();
            button.targetGraphic = border;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(()=>Select(id));
            button.Purchase = ()=>Buy(id);
            var background = Image(border.transform,"Instrument face",Center,Vector2.zero,new(31,31),Panel);
            var symbol = UiKit.Symbol(background.transform,"Branch symbol",Icon[branch],accent);
            UiKit.Place(symbol.rectTransform,Center,Center,Vector2.zero,new(22,22));
            var title = Label(background.transform,"Node title",Vector2.up,new(33,-.5f),new(122,32),"",16);
            title.enableAutoSizing=true;title.fontSizeMin=13;title.fontSizeMax=16;
            title.textWrappingMode=TextWrappingModes.Normal;
            title.overflowMode=TextOverflowModes.Overflow;
            var scope = Label(background.transform,"Skill scope",Vector2.up,new(33,-31),new(123,18),"",13,accent);
            scope.overflowMode=TextOverflowModes.Overflow;
            var state=Label(background.transform,"Node state",Vector2.up,new(34,-35),new(119,18),"",13,UiTheme.Muted);
            state.overflowMode=TextOverflowModes.Overflow;
            return new Card { Id=id, Branch=branch, Step=step, Root=border.rectTransform,
                Border=border, Background=background, Symbol=symbol, Title=title, Scope=scope, State=state };
        }

        internal void Open()
        {
            selected=SkillId.None;
            displayedSelection=(SkillId)(-1);
            pendingUntil=0;
            awaiting=false;
            confirmed=false;
            UiKit.Set(response,"");
            Reset();
        }

        internal void Close()
        {
            Subscribe(null);
        }

        void Subscribe(RaidWorld world)
        {
            if (subscribed==world) return;
            if(subscribed!=null)subscribed.SkillResponded-=OnReply;
            subscribed=world;
            if(subscribed!=null)subscribed.SkillResponded+=OnReply;
            displayedPlayer=int.MinValue;
            displayedTeam=int.MinValue;
        }

        internal void Refresh()
        {
            if (!Root.gameObject.activeInHierarchy) return;
            var context=app.Context;
            Subscribe(context.World);
            var profile=context.World?.LocalProgress;
            var team=context.View;
            UiKit.Set(playerLabel,context.Player==null ? "" : "Водолаз "+UiText.HudPlayerName(context.Player));
            UiKit.Set(teamBalance,team==null?"КАЗНА NEREID     Загрузка…":"КАЗНА NEREID     "+Compact(team.Treasury));

            int playerRevision=profile?.revision ?? -1;
            int teamRevision=team?.teamRevision ?? -1;
            if(awaiting && pendingUntil<Time.unscaledTime)
            {
                awaiting=false;
                SetMessage("Ответ не получен. Проверьте казну перед повтором.",false);
            }
            if(awaiting && confirmed && teamRevision>previousRevision)
                awaiting=false;

            if (displayedPlayer!=playerRevision || displayedTeam!=teamRevision || displayedSelection!=selected)
            {
                displayedPlayer=playerRevision;displayedTeam=teamRevision;displayedSelection=selected;
                foreach(var card in cards) DisplayCard(card);
                DisplayDetails();
            }
        }

        internal void Animate(float dt)
        {
            if(!Root.gameObject.activeInHierarchy)return;
            float blend=1-Mathf.Exp(-dt*15);
            if(Mathf.Abs(zoom-targetZoom)>.0001f || lastLod<0)
            {
                zoom=Mathf.Abs(zoom-targetZoom)<.002f?targetZoom:Mathf.Lerp(zoom,targetZoom,blend);
                graph.localScale=Vector3.one*zoom;
                foreach(var card in cards)card.Root.localScale=Vector3.one/zoom;
            }
            if((pan-targetPan).sqrMagnitude>.001f)
            {
                pan=(pan-targetPan).sqrMagnitude<.025f?targetPan:Vector2.Lerp(pan,targetPan,blend);
                graph.anchoredPosition=pan;
            }
            float lod=zoom<2.04f?0:1;
            if(lod==lastLod)return;
            lastLod=lod;
            foreach(var card in cards) DisplayLod(card,lod>0);
        }

        void DisplayLod(Card card,bool detail)
        {
            bool expanded=card.Step==0 || detail;
            float width=expanded?159:35;
            float height=expanded?54:35;
            card.Root.sizeDelta=new(width,height);
            card.Background.rectTransform.sizeDelta=new(width-4,height-4);
            card.Title.gameObject.SetActive(expanded);
            card.Scope.gameObject.SetActive(expanded && !detail);
            card.State.gameObject.SetActive(expanded && detail);
            card.Symbol.rectTransform.anchoredPosition=expanded?new(-width*.5f+18,0):Vector2.zero;
        }

        void DisplayCard(Card card)
        {
            var node=Node(card.Id);
            var owner=Owned(node);
            bool purchased=owner.Contains(node.Id);
            bool available=SkillTreeDefinition.IsAvailable(node.Id,owner);
            bool funded=(app.Context.View?.Treasury ?? default).Covers(node.Cost);
            Color theme=node.Team?Gold:Cyan;
            Color edge=purchased?theme:available&&funded?Color.Lerp(theme,Rest,.36f):Rest;
            float brightness=purchased?.26f:available&&funded?.15f:.07f;
            card.Border.color=card.Id==selected?UiTheme.Text:edge;
            card.Background.color=Color.Lerp(Glass,theme,brightness);
            card.Symbol.color=edge;
            UiKit.Set(card.Title,node.Name);
            UiKit.Set(card.Scope,card.Step==0?Branch[card.Branch]+" · "+(node.Team?"Команда":"Личное"):node.Id+" · "+(node.Team?"Команда":"Личное"));
            UiKit.Set(card.State,purchased?"Куплено":available?ShortCost(node.Cost):"Нужна предпосылка");
        }

        void DisplayDetails()
        {
            focus.interactable=selected!=SkillId.None;
            if(selected==SkillId.None)
            {
                UiKit.Set(detailTitle,"NEREID / Снаряжение экипажа");
                UiKit.Set(detailBody,"Бирюзовые навыки действуют лично. Янтарные действуют на весь экипаж.");
                UiKit.Set(detailCost,"Выберите узел, чтобы узнать условия и цену. Покупки оплачиваются из казны NEREID.");
                UiKit.Set(detailState,"Стартовый узел открыт");
                return;
            }
            var node=Node(selected);
            var owner=Owned(node);
            bool bought=owner.Contains(selected);
            var conditions=SkillTreeDefinition.Dependencies(selected);
            bool unlocked=SkillTreeDefinition.IsAvailable(selected,owner);
            bool funded=(app.Context.View?.Treasury ?? default).Covers(node.Cost);
            UiKit.Set(detailTitle,node.Name+" · "+(node.Team?"Для всей команды":"Личное"));
            UiKit.Set(detailBody,(bought?"Действует · ":"После покупки · ")+node.Effect+EffectAmount(node));
            UiKit.Set(detailCost,"Цена из казны NEREID  "+Compact(node.Cost)+
                (conditions.Count==0?"":"    ·    Нужен "+string.Join(" или ",conditions)));
            detailState.color=bought?Cyan:unlocked&&funded?UiTheme.Green:Gold;
            UiKit.Set(detailState,bought?(node.Team?"Уже улучшено экипажем":"Куплено лично"):
                unlocked?(funded?"Доступно · ПКМ — купить":"Недостаточно материалов в казне"):"Закрыто · нужна предпосылка");
        }

        static string EffectAmount(SkillNode node)
        {
            if(node.Id is SkillId.G1 or SkillId.G2 or SkillId.G3 or SkillId.R2 or SkillId.C3)return "";
            return node.Id switch
            {
                SkillId.S2 or SkillId.S4 or SkillId.R1 or SkillId.R3 or SkillId.R4 => " · "+node.Magnitude,
                SkillId.C1 or SkillId.C4 or SkillId.K1 or SkillId.K2 or SkillId.K3 or SkillId.K4 => " · +"+node.Magnitude,
                SkillId.S3 or SkillId.H3 or SkillId.F3 or SkillId.C2 or SkillId.G4 => " · −"+node.Magnitude+"%",
                _ => " · +"+node.Magnitude+"%"
            };
        }

        SkillNode Node(SkillId id) => app.Context.Tuning?.Skill(id) ?? SkillTreeDefinition.Get(id);
        IReadOnlyCollection<SkillId> Owned(SkillNode node)
        {
            IReadOnlyCollection<SkillId> owned=node.Team?app.Context.View?.teamSkills:app.Context.World?.LocalProgress?.skills;
            return owned ?? Empty;
        }

        void Select(SkillId id)
        {
            selected=id;
            Refresh();
        }

        void Buy(SkillId id)
        {
            if(awaiting || app.Context.World?.LocalProgress==null || app.Context.View==null ||
                app.Context.Player==null || app.Context.Player.IsHelpless || !app.Context.NearTerminal) return;
            Select(id);
            var definition=Node(id);
            var owner=Owned(definition);
            if(owner.Contains(id))
            {
                SetMessage(definition.Team?"Уже улучшено экипажем":"Навык уже куплен",false);
                return;
            }
            if(!SkillTreeDefinition.IsAvailable(id,owner)) {SetMessage("Сначала откройте предыдущий узел",false);return;}
            var team=app.Context.View;
            if(!team.Treasury.Covers(definition.Cost)) {SetMessage("Недостаточно материалов в казне",false);return;}
            int revision=team.teamRevision;
            BeginRequest(revision);
            app.Context.World.RequestSkill(id,revision);
        }

        void BeginRequest(int revision)
        {
            awaiting=true;
            confirmed=false;
            previousRevision=revision;
            pendingUntil=Time.unscaledTime+5f;
            SetMessage("Ожидаем подтверждение хоста…",true);
        }

        void OnReply(bool success,string reason)
        {
            if(!awaiting)return;
            confirmed=success;
            if(!success)awaiting=false;
            SetMessage(reason,success);
            Refresh();
        }

        void SetMessage(string reason,bool success)
        {
            response.color=success?Cyan:Gold;
            UiKit.Set(response,reason);
        }

        static string Compact(MaterialCost cost) => $"B {cost.biomass}    C {cost.chitin}    S {cost.scrap}    E {cost.electronics}";
        static string ShortCost(MaterialCost cost) => $"B{cost.biomass} C{cost.chitin} S{cost.scrap} E{cost.electronics}";

        void Reset()
        {
            targetZoom=1;
            targetPan=Vector2.zero;
            if(!Root.gameObject.activeInHierarchy)
            {
                zoom=1;pan=Vector2.zero;
                graph.anchoredPosition=pan;
                graph.localScale=Vector3.one;
                lastLod=-1;
            }
        }

        void Focus()
        {
            if(selected==SkillId.None)return;
            int branch=((int)selected-1)/4;
            targetZoom=2.18f;
            Vector2 center=Vector2.Lerp(positions[branch*4+1],positions[branch*4+4],.55f);
            targetPan=ClampPan(-center*targetZoom,targetZoom);
        }

        internal void Pan(Vector2 delta) => targetPan=ClampPan(targetPan+delta,targetZoom);

        internal void Zoom(PointerEventData pointer)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(graphArea,pointer.position,pointer.enterEventCamera,out var point))return;
            ApplyZoom(point,pointer.scrollDelta.y);
        }

        void ApplyZoom(Vector2 point,float ticks)
        {
            float next=Mathf.Clamp(targetZoom*Mathf.Pow(1.24f,ticks),1,3.25f);
            targetPan=ClampPan(point-(point-targetPan)*(next/targetZoom),next);
            targetZoom=next;
        }

        Vector2 ClampPan(Vector2 offset,float scale)
        {
            if(scale<=1)return Vector2.zero;
            var limit=graphArea.rect.size*(scale-1)*.5f;
            return new Vector2(Mathf.Clamp(offset.x,-limit.x,limit.x),Mathf.Clamp(offset.y,-limit.y,limit.y));
        }

        static Image Image(Transform parent,string name,Vector2 anchor,Vector2 offset,Vector2 size,Color color,bool raycast=false)
        {
            var image=UiKit.Panel(parent,name,color,raycast);
            UiKit.Place(image.rectTransform,anchor,anchor,offset,size);
            return image;
        }

        static TMP_Text Label(Transform parent,string name,Vector2 anchor,Vector2 point,Vector2 size,string content,
            float font,Color? color=null,bool align=false)
        {
            var label=UiKit.Label(parent,name,content,font,color);
            UiKit.Place(label.rectTransform,anchor,anchor,point,size);
            label.textWrappingMode=TextWrappingModes.NoWrap;
            label.overflowMode=TextOverflowModes.Ellipsis;
            label.alignment=align?TextAlignmentOptions.Center:TextAlignmentOptions.MidlineLeft;
            return label;
        }

        static Button Key(Transform parent,string name,string label,Vector2 point,float width,Action action,float height=35)
        {
            var button=UiKit.Button(parent,name,label,()=>action(),false,height);
            UiKit.Place((RectTransform)button.transform,Vector2.up,Vector2.up,point,new(width,height));
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
        public void OnDrag(PointerEventData data) => View.Pan(data.delta);
        public void OnScroll(PointerEventData data) => View.Zoom(data);
    }

    internal sealed class SkillWires : MaskableGraphic
    {
        readonly List<(Vector2 Start,Vector2 End,Color Color,float Width)> strokes = new(260);

        internal void Build(Vector2[] position)
        {
            for(int ring=1;ring<=4;ring++)
            {
                const int sides=40;
                float radius=ring*.25f;
                Vector2 last=new(535*radius,0);
                for(int i=1;i<=sides;i++)
                {
                    float angle=i*Mathf.PI*2/sides;
                    Vector2 next=new Vector2(535*Mathf.Cos(angle),246*Mathf.Sin(angle))*radius;
                    strokes.Add((last,next,new Color(.31f,.61f,.60f,.12f),1.5f));
                    last=next;
                }
            }
            for(int branch=0;branch<9;branch++)
            {
                Color line=branch<6?new Color(.33f,.82f,.74f,.62f):new Color(.94f,.62f,.28f,.65f);
                int first=branch*4+1;
                strokes.Add((Vector2.zero,position[first],line,3));
                for(int variant=1;variant<=2;variant++)
                {
                    strokes.Add((position[first],position[first+variant],line,2.5f));
                    strokes.Add((position[first+variant],position[first+3],line,2.5f));
                }
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            foreach(var stroke in strokes)
            {
                Vector2 offset=Vector2.Perpendicular((stroke.End-stroke.Start).normalized)*stroke.Width*.5f;
                int i=vertices.currentVertCount;
                vertices.AddVert(stroke.Start-offset,stroke.Color,Vector2.zero);
                vertices.AddVert(stroke.Start+offset,stroke.Color,Vector2.zero);
                vertices.AddVert(stroke.End+offset,stroke.Color,Vector2.zero);
                vertices.AddVert(stroke.End-offset,stroke.Color,Vector2.zero);
                vertices.AddTriangle(i,i+1,i+2);
                vertices.AddTriangle(i,i+2,i+3);
            }
        }
    }
}
