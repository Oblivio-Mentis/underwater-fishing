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
        static readonly string[] Currency = {"Биомасса","Хитин","Лом","Электроника"};
        static readonly string[] Units = {"B","C","S","E"};
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
        readonly TMP_Text playerBalance, teamBalance, detailTitle, detailBody, detailCost, detailState, response;
        readonly TMP_Text[] transferAmount = new TMP_Text[4], transferBalance = new TMP_Text[4];
        readonly int[] transfer = new int[4];
        readonly RectTransform graph, graphArea, info, treasury;
        readonly Button focus, deposit, depositAll, treasuryOpen;
        readonly SkillWires wires;
        RaidWorld subscribed;
        SkillId selected;
        Vector2 pan, targetPan;
        float zoom = 1, targetZoom = 1, pendingUntil;
        bool treasuryOnly, treasuryVisible, awaiting;
        int previousRevision, displayedPlayer = int.MinValue, displayedTeam = int.MinValue;
        bool confirmed, pendingTeam;
        SkillId displayedSelection = (SkillId)(-1);
        float lastLod = -1;
        public RectTransform Root { get; }
        internal bool TreasuryOnly => treasuryOnly;

        internal SkillTreeView(Transform parent, UiRoot app)
        {
            this.app = app;
            var surface = UiKit.Frame(parent,"SkillTreeView",1180,810);
            Root = (RectTransform)surface.parent;
            var heading = Image(surface,"Header",Vector2.up,new(14,-12),new(1148,95),Panel);
            Label(heading.transform,"Section title",Vector2.up,new(13,-7),new(708,32),
                "СНАРЯЖЕНИЕ ЭКИПАЖА",30,Cyan);
            playerBalance = Label(heading.transform,"Personal resources",Vector2.up,new(14,-43),new(1100,21),"",18);
            teamBalance = Label(heading.transform,"Ship treasury",Vector2.up,new(14,-66),new(1090,21),"",17,UiTheme.Muted);
            treasuryOpen = Key(heading.transform,"Treasury","Казна",new(797,-10),142,app.OpenTreasury);
            Key(heading.transform,"ResetSkillView","Сбросить вид",new(948,-10),186,Reset);

            graphArea = Image(surface,"Skill graph viewport",Vector2.up,new(14,-114),new(1148,531),Glass,true).rectTransform;
            graphArea.gameObject.AddComponent<RectMask2D>();
            graphArea.gameObject.AddComponent<SkillGraphPan>().View = this;
            graph = UiKit.Node(graphArea,"Skill tree geometry");
            UiKit.Place(graph,Center,Center,Vector2.zero,new(1148,531));
            wires = UiKit.Node(graph,"Nine branches").gameObject.AddComponent<SkillWires>();
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

            info = Image(surface,"Selected skill",Vector2.up,new(14,-652),new(1148,97),Panel,true).rectTransform;
            detailTitle = Label(info,"Skill and scope",Vector2.up,new(14,-5),new(765,29),"",23,Cyan);
            detailBody = Label(info,"Skill benefit",Vector2.up,new(14,-33),new(765,26),"",17);
            detailCost = Label(info,"Cost and prerequisite",Vector2.up,new(14,-62),new(765,28),"",15,UiTheme.Muted);
            detailState = Label(info,"Skill state",Vector2.up,new(793,-48),new(340,37),"",17,UiTheme.Amber);
            focus = Key(info,"FocusSkillBranch","Фокус на ветке",new(793,-6),340,Focus);
            response = Label(surface,"Server result",Vector2.up,new(22,-774),new(1118,22),"",16,Cyan);
            Label(surface,"Controls",Vector2.up,new(22,-752),new(1118,22),
                "ЛКМ — выбрать  ·  ПКМ — купить  ·  Колесо — масштаб  ·  Зажать ЛКМ на поле — переместить  ·  Esc — выйти",16,UiTheme.Muted);

            treasury = Image(surface,"Treasury transfer",Center,new(0,-20),new(660,504),Panel,true).rectTransform;
            var veil = Image(treasury,"Treasury plate",Center,Vector2.zero,new(652,496),Glass,true);
            var form = veil.transform;
            Label(form,"Transfer title",Vector2.up,new(27,-25),new(600,36),"ВЗНОС В КАЗНУ NEREID",26,Gold);
            Label(form,"Transfer scope",Vector2.up,new(27,-62),new(600,31),"Из вашего кошелька в командный запас",19,UiTheme.Muted);
            for (int i=0;i<Currency.Length;i++)
            {
                int material=i;
                float y=-112-i*66;
                var row=Image(form,Currency[i],Vector2.up,new(26,y),new(598,51),Panel);
                Label(row.transform,"Material",Vector2.up,new(12,-7),new(158,37),Currency[i],19);
                transferBalance[i] = Label(row.transform,"Available",Vector2.up,new(170,-8),new(90,34),"",16,UiTheme.Muted);
                Key(row.transform,"Decrease"+Units[i],"−",new(271,-6),51,()=>Adjust(material,-1),39);
                transferAmount[i] = Label(row.transform,"Amount",Vector2.up,new(330,-7),new(92,36),"0",21,Cyan,true);
                Key(row.transform,"Increase"+Units[i],"+1",new(432,-6),56,()=>Adjust(material,1),39);
                Key(row.transform,"IncreaseTen"+Units[i],"+10",new(497,-6),86,()=>Adjust(material,10),39);
            }
            deposit = Key(form,"DepositResources","Внести",new(28,-405),186,()=>Deposit(false),47);
            depositAll = Key(form,"DepositAllResources","Внести всё",new(227,-405),196,()=>Deposit(true),47);
            Key(form,"CancelDeposit","Назад",new(441,-405),184,app.CloseTreasury,47);
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
            var title = Label(background.transform,"Node title",Vector2.up,new(34,-3),new(121,24),"",16);
            title.enableAutoSizing = true;title.fontSizeMin=13;title.fontSizeMax=16;
            var scope = Label(background.transform,"Skill scope",Vector2.up,new(34,-24),new(121,15),"",13,accent);
            var state = Label(background.transform,"Node state",Vector2.up,new(34,-38),new(122,13),"",11,UiTheme.Muted);
            return new Card { Id=id, Branch=branch, Step=step, Root=border.rectTransform,
                Border=border, Background=background, Symbol=symbol, Title=title, Scope=scope, State=state };
        }

        internal void Open(bool teamTransferOnly)
        {
            treasuryOnly=teamTransferOnly;
            treasuryVisible=teamTransferOnly;
            Array.Clear(transfer,0,transfer.Length);
            selected=SkillId.None;
            displayedSelection=(SkillId)(-1);
            pendingUntil=0;
            awaiting=false;
            confirmed=false;
            UiKit.Set(response,"");
            Reset();
            ShowTreasury(teamTransferOnly);
        }

        internal void Close()
        {
            Subscribe(null);
        }

        internal void ShowTreasury(bool show)
        {
            treasuryVisible=show;
            treasury.gameObject.SetActive(show);
            graphArea.gameObject.SetActive(!treasuryOnly && !show);
            info.gameObject.SetActive(!treasuryOnly && !show);
            treasuryOpen.gameObject.SetActive(!treasuryOnly);
            if (!show) Array.Clear(transfer,0,transfer.Length);
            UpdateTransfers();
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
            var wallet=profile==null ? default : profile.wallet;
            UiKit.Set(playerBalance,context.Player==null ? "" : "Водолаз "+UiText.HudPlayerName(context.Player)+
                (profile==null?"    ·    Получение личного баланса":"     "+Compact(wallet)));
            if(team!=null)
                UiKit.Set(teamBalance,"КАЗНА NEREID     "+Compact(new MaterialCost(team.biomass,team.chitin,team.scrap,team.electronics)));

            int playerRevision=profile?.revision ?? -1;
            int teamRevision=team?.teamRevision ?? -1;
            if(awaiting && pendingUntil<Time.unscaledTime)
            {
                awaiting=false;
                SetMessage("Ответ не получен. Проверьте баланс перед повтором.",false);
            }
            if(awaiting && confirmed && (pendingTeam?teamRevision:playerRevision)>previousRevision)
                awaiting=false;

            if (displayedPlayer!=playerRevision || displayedTeam!=teamRevision || displayedSelection!=selected)
            {
                displayedPlayer=playerRevision;displayedTeam=teamRevision;displayedSelection=selected;
                foreach(var card in cards) DisplayCard(card);
                DisplayDetails();
            }
            if(treasuryVisible)UpdateTransfers();
        }

        internal void Animate(float dt)
        {
            if(!Root.gameObject.activeInHierarchy || treasuryVisible)return;
            float blend=1-Mathf.Exp(-dt*15);
            zoom=Mathf.Lerp(zoom,targetZoom,blend);
            pan=Vector2.Lerp(pan,targetPan,blend);
            graph.localScale=Vector3.one*zoom;
            graph.anchoredPosition=pan;
            foreach(var card in cards)card.Root.localScale=Vector3.one/zoom;
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
            card.Scope.gameObject.SetActive(expanded);
            card.State.gameObject.SetActive(expanded);
            card.Symbol.rectTransform.anchoredPosition=expanded?new(-width*.5f+18,0):Vector2.zero;
        }

        void DisplayCard(Card card)
        {
            var node=Node(card.Id);
            var owner=Owned(node);
            bool purchased=owner.Contains(node.Id);
            bool available=SkillTreeDefinition.IsAvailable(node.Id,owner);
            bool funded=Afford(app.Context.World?.LocalProgress?.wallet ?? default,node.Cost);
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
                UiKit.Set(detailCost,"Выберите узел, чтобы узнать условия и цену. Покупки оплачиваются из личного кошелька.");
                UiKit.Set(detailState,"Стартовый узел открыт");
                return;
            }
            var node=Node(selected);
            var owner=Owned(node);
            bool bought=owner.Contains(selected);
            var conditions=SkillTreeDefinition.Dependencies(selected);
            bool unlocked=SkillTreeDefinition.IsAvailable(selected,owner);
            bool funded=Afford(app.Context.World?.LocalProgress?.wallet ?? default,node.Cost);
            UiKit.Set(detailTitle,node.Name+" · "+(node.Team?"Для всей команды":"Личное"));
            UiKit.Set(detailBody,node.Effect+EffectAmount(node));
            UiKit.Set(detailCost,"Цена из личного кошелька: "+Compact(node.Cost)+
                (conditions.Count==0?"":"    ·    Нужен "+string.Join(" или ",conditions)));
            detailState.color=bought?Cyan:unlocked&&funded?UiTheme.Green:Gold;
            UiKit.Set(detailState,bought?(node.Team?"Уже улучшено экипажем":"Куплено лично"):
                unlocked?(funded?"Доступно · ПКМ — купить":"Недостаточно материалов"):"Закрыто · нужна предпосылка");
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
            return (IReadOnlyCollection<SkillId>)(node.Team
                ? app.Context.View?.teamSkills : app.Context.World?.LocalProgress?.skills) ?? Empty;
        }

        void Select(SkillId id)
        {
            if(treasuryVisible)return;
            selected=id;
            Refresh();
        }

        void Buy(SkillId id)
        {
            if(treasuryVisible || awaiting || app.Context.World?.LocalProgress==null || app.Context.View==null ||
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
            var profile=app.Context.World.LocalProgress;
            if(!Afford(profile.wallet,definition.Cost)) {SetMessage("Недостаточно личных материалов",false);return;}
            int revision=definition.Team?app.Context.View.teamRevision:profile.revision;
            BeginRequest(revision,definition.Team);
            app.Context.World.RequestSkill(id,revision);
        }

        void BeginRequest(int revision,bool team)
        {
            awaiting=true;
            confirmed=false;
            pendingTeam=team;
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

        void Adjust(int resource,int delta)
        {
            if(awaiting)return;
            var wallet=app.Context.World?.LocalProgress?.wallet ?? default;
            transfer[resource]=(int)Math.Clamp((long)transfer[resource]+delta,0,Amount(wallet,resource));
            UpdateTransfers();
        }

        void UpdateTransfers()
        {
            var profile=app.Context.World?.LocalProgress;
            var wallet=profile?.wallet ?? default;
            bool haveAny=false,haveSelected=false;
            for(int i=0;i<Currency.Length;i++)
            {
                int balance=Amount(wallet,i);
                transfer[i]=Mathf.Clamp(transfer[i],0,balance);
                haveAny|=balance>0;haveSelected|=transfer[i]>0;
                UiKit.Set(transferBalance[i],"Есть "+balance);
                UiKit.Set(transferAmount[i],transfer[i].ToString());
            }
            deposit.interactable=haveSelected&&!awaiting;
            depositAll.interactable=haveAny&&!awaiting;
        }

        void Deposit(bool all)
        {
            var profile=app.Context.World?.LocalProgress;
            if(awaiting || profile==null || !app.Context.NearTerminal)return;
            MaterialCost amount=all ? profile.wallet : new(transfer[0],transfer[1],transfer[2],transfer[3]);
            if(!Afford(profile.wallet,amount))return;
            BeginRequest(profile.revision,false);
            Array.Clear(transfer,0,transfer.Length);
            UpdateTransfers();
            app.Context.World.RequestTreasury(amount,profile.revision);
        }

        static bool Afford(MaterialCost wallet,MaterialCost price) => wallet.biomass>=price.biomass && wallet.chitin>=price.chitin &&
            wallet.scrap>=price.scrap && wallet.electronics>=price.electronics;
        static int Amount(MaterialCost value,int slot) => slot==0?value.biomass:slot==1?value.chitin:slot==2?value.scrap:value.electronics;
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

