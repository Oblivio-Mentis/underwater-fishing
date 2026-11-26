using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mark = Raid.UI.DiveInstruments.Mark;

namespace Raid.UI
{
    public sealed class CheatPanel
    {
        const float Width = 530, Margin = 22, Inner = Width - 2 * Margin;
        const float ListHeight = 220, RowStep = 44;
        static readonly Color Ink = new Color32(13, 28, 32, 255);
        static readonly Color RowColor = new Color32(31, 51, 56, 255);
        static readonly Color SelectedColor = new Color32(45, 76, 76, 255);
        static readonly (string id, string name, Mark icon)[] Resources = {
            ("biomass", "Биомасса", Mark.Biomass), ("chitin", "Хитин", Mark.Chitin),
            ("scrap", "Металлолом", Mark.Scrap), ("electronics", "Электроника", Mark.Electronics),
            ("offering", "Пища", Mark.Crate)
        };

        public static bool PointerActive => UiRoot.Instance != null && UiRoot.Instance.CheatsOpen;
        internal bool IsOpen => surface.gameObject.activeSelf;
        internal bool QuantityFocused => IsOpen && amount.isFocused;

        sealed class ItemRow
        {
            internal string id;
            internal Button button;
            internal Image selected;
            internal TMP_Text count;
        }

        sealed class ModeSwitch
        {
            internal Image track, knob;
        }

        readonly UiRoot root;
        readonly RectTransform surface, resourcesPage, fishPage, fishList;
        readonly TMP_InputField amount;
        readonly TMP_Text section, column, feedback, actionText;
        readonly Button resourcesTab, fishTab, grant, decrease, increase;
        readonly ModeSwitch noclip, fast;
        readonly List<ItemRow> resources = new(), fish = new();
        IReadOnlyList<string> currentSpecies;
        DiverMotor player;
        int resourceIndex, fishIndex;
        bool showingFish, requestedNoclip, requestedFast;
        float movementDeadline;

        internal CheatPanel(UiRoot root)
        {
            this.root = root;
            surface = UiKit.Panel(root.transform, "CheatPanel", Ink, true).rectTransform;
            UiKit.Place(surface, new Vector2(1,.5f), new Vector2(1,.5f), new Vector2(-28,0), new Vector2(Width,610));
            var edge = surface.gameObject.AddComponent<Outline>();
            edge.effectColor = UiTheme.Edge;
            edge.effectDistance = new Vector2(1,-1);
            Block(surface, "Top edge", 0,0,Width,3,UiTheme.Cyan);
            Symbol(surface, Mark.Wrench, Margin,20,34);
            Text(surface,"Title","Панель читов",Margin+48,14,340,46,32,UiTheme.Text).fontStyle = FontStyles.Bold;
            var close = Hit(surface,"CheatClose",Width-Margin-40,18,40,40,RowColor,root.CloseCheats);
            Text(close.transform,"Close","×",0,0,40,39,30,UiTheme.Text,TextAlignmentOptions.Center);

            float tabWidth = (Inner-4)/2;
            resourcesTab = Tab("CheatResourcesTab","Ресурсы",Margin,tabWidth,() => SelectTab(false));
            fishTab = Tab("CheatFishTab","Рыба",Margin+tabWidth+4,tabWidth,() => SelectTab(true));
            section = Text(surface,"Section","",Margin,130,Inner/2+24,28,21,UiTheme.Text);
            section.fontStyle = FontStyles.Bold;
            column = Text(surface,"Balance heading","",Width/2+24,130,Inner/2-24,28,17,UiTheme.Muted,TextAlignmentOptions.MidlineRight);

            resourcesPage = Page("CheatResourcesPage",out var resourceList);
            fishPage = Page("CheatFishPage",out fishList);
            for(int i=0;i<Resources.Length;i++)
            {
                var item = Resources[i];
                int index = i;
                resources.Add(Row(resourceList,"CheatResource_",item.id,item.name,item.icon,i,
                    () => { resourceIndex=index; ClearReceipt(); Refresh(); }));
            }
            ResizeList(resourceList,resources.Count);
            Text(surface,"Quantity label","Количество",Margin,394,222,26,19,UiTheme.Muted);
            decrease = Hit(surface,"CheatLess",Margin,426,37,46,RowColor,() => Step(-1));
            Text(decrease.transform,"Minus","−",0,0,37,46,26,UiTheme.Text,TextAlignmentOptions.Center);
            amount = UiKit.Input(surface,"CheatAmountField","1","0");
            At(amount.transform,Margin+40,426,130,46);
            amount.GetComponent<Image>().color = UiTheme.Panel;
            amount.contentType = TMP_InputField.ContentType.IntegerNumber;
            amount.onValidateInput = (_,_,c) => c >= '0' && c <= '9' ? c : '\0';
            amount.onFocusSelectAll = true;
            amount.richText = false;
            amount.navigation = MouseOnly();
            amount.textComponent.alignment = TextAlignmentOptions.Center;
            amount.placeholder.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            UiKit.Fill(amount.textViewport,4);
            UiKit.Fill(amount.textComponent.rectTransform,1);
            UiKit.Fill(amount.placeholder.rectTransform,1);
            increase = Hit(surface,"CheatMore",Margin+173,426,37,46,RowColor,() => Step(1));
            Text(increase.transform,"Plus","+",0,0,37,46,26,UiTheme.Text,TextAlignmentOptions.Center);
            const float actionX = Margin+226;
            grant = Hit(surface,"CheatGrant",actionX,426,Width-Margin-actionX,46,UiTheme.Cyan,Grant);
            actionText = Text(grant.transform,"Caption","",0,0,Width-Margin-actionX,46,22,Ink,TextAlignmentOptions.Center);
            actionText.fontStyle = FontStyles.Bold;
            feedback = Text(surface,"CheatFeedback","",Margin,477,Inner,32,18,UiTheme.Green);
            Block(surface,"Divider",Margin,517,Inner,1,UiTheme.Edge);
            Text(surface,"Movement","Перемещение",Margin,526,Inner,27,21,UiTheme.Text).fontStyle = FontStyles.Bold;
            float switchWidth = (Inner-8)/2;
            noclip = Switch("CheatNoclip","Сквозь стены",Margin,switchWidth,() => ToggleMovement(true));
            fast = Switch("CheatFast","Скорость ×4",Margin+switchWidth+8,switchWidth,() => ToggleMovement(false));
            amount.onValueChanged.AddListener(QuantityChanged);
            surface.gameObject.SetActive(false);
        }

        static Navigation MouseOnly() => new Navigation { mode=Navigation.Mode.None };

        static void At(Transform node,float x,float y,float w,float h) =>
            UiKit.Place((RectTransform)node,Vector2.up,Vector2.up,new Vector2(x,-y),new Vector2(w,h));

        static Image Block(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var image = UiKit.Panel(parent,name,color);
            At(image.transform,x,y,w,h);
            return image;
        }

        static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,
            float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {
            var text = UiKit.Label(parent,name,value,size,color);
            At(text.transform,x,y,w,h);
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        static void Symbol(Transform parent,Mark icon,float x,float y,float size) =>
            At(UiKit.Symbol(parent,"Icon",icon,UiTheme.Text).transform,x,y,size,size);

        static Button Hit(Transform parent,string name,float x,float y,float w,float h,Color background,Action click)
        {
            var image = Block(parent,name,x,y,w,h,background);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.22f,1.22f,1.22f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.62f,.65f,.66f,.7f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.navigation = MouseOnly();
            button.onClick.AddListener(() => click());
            return button;
        }

        Button Tab(string id,string title,float x,float width,Action click)
        {
            var button = Hit(surface,id,x,77,width,42,RowColor,click);
            Text(button.transform,"Name",title,0,0,width,41,22,UiTheme.Text,TextAlignmentOptions.Center);
            Block(button.transform,"Selected",0,39,width,3,UiTheme.Cyan);
            return button;
        }

        RectTransform Page(string id,out RectTransform content)
        {
            var page = UiKit.Panel(surface,id,Ink,true).rectTransform;
            At(page,Margin,165,Inner,ListHeight);
            page.gameObject.AddComponent<RectMask2D>();
            content = UiKit.Node(page,"Items");
            At(content,0,0,Inner,ListHeight);
            var scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = page;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = RowStep;
            return page;
        }

        static void ResizeList(RectTransform list,int count)
        {
            list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(ListHeight,count*RowStep));
            list.anchoredPosition = Vector2.zero;
        }

        static ItemRow Row(RectTransform parent,string prefix,string id,string title,Mark icon,int index,Action click)
        {
            var button = Hit(parent,prefix+id,0,index*RowStep,Inner,RowStep-3,RowColor,click);
            var stripe = Block(button.transform,"Selected",0,0,4,RowStep-3,UiTheme.Cyan);
            Symbol(button.transform,icon,14,5,31);
            Text(button.transform,"Name",title,60,0,Inner-205,RowStep-3,22,UiTheme.Text);
            var count = Text(button.transform,"Count","",Inner-158,0,142,RowStep-3,21,UiTheme.Cyan,TextAlignmentOptions.MidlineRight);
            return new ItemRow {id=id,button=button,selected=stripe,count=count};
        }

        ModeSwitch Switch(string id,string title,float x,float w,Action click)
        {
            var button = Hit(surface,id,x,559,w,37,RowColor,click);
            Text(button.transform,"Name",title,12,0,w-70,37,20,UiTheme.Text);
            var track = Block(button.transform,"Switch",w-49,10,36,17,UiTheme.Edge);
            var knob = Block(track.transform,"Position",3,3,11,11,UiTheme.Text);
            return new ModeSwitch {track=track,knob=knob};
        }

        int Quantity => int.TryParse(amount.text,NumberStyles.None,CultureInfo.InvariantCulture,out int n) ? n : 0;
        void Step(int direction)
        {
            long next=(long)Quantity+direction;
            amount.text=Math.Max(1L,Math.Min(int.MaxValue,next)).ToString(CultureInfo.InvariantCulture);
        }

        void QuantityChanged(string text)
        {
            ClearReceipt();
            Refresh();
        }

        void ClearReceipt()
        {
            if(amount.text.Length>0 && Quantity==0)
            {
                UiKit.Set(feedback,"Введите число от 1 до 2 147 483 647.");
                feedback.color=UiTheme.Amber;
            }
            else UiKit.Set(feedback,"");
        }

        void SelectTab(bool fishTabSelected)
        {
            showingFish=fishTabSelected;
            ClearReceipt();
            Refresh();
        }

        void UpdateFishList(IReadOnlyList<string> ids)
        {
            if(ReferenceEquals(currentSpecies,ids))return;
            currentSpecies=ids;
            UiKit.Clear(fishList);
            fish.Clear();
            for(int i=0;i<ids.Count;i++)
            {
                string id=ids[i];
                int index=i;
                string title=ReelReadout.Species(id);
                fish.Add(Row(fishList,"CheatFish_",id,title=="Улов"?id:title,DiveInstruments.CatchMark(id),i,
                    () => { fishIndex=index; ClearReceipt(); Refresh(); }));
            }
            fishIndex=Mathf.Clamp(fishIndex,0,Mathf.Max(0,fish.Count-1));
            ResizeList(fishList,fish.Count);
        }

        internal void Open(DiverMotor owner)
        {
            player=owner;
            player.CheatGrantResponded+=GrantResponded;
            movementDeadline=0;
            ClearReceipt();
            surface.gameObject.SetActive(true);
            surface.SetAsLastSibling();
            Refresh();
        }

        internal void Close()
        {
            if(player!=null)player.CheatGrantResponded-=GrantResponded;
            player=null;
            surface.gameObject.SetActive(false);
        }

        internal void Refresh()
        {
            var view=root.Context.View;
            var world=root.Context.World;
            if(view==null || !world || !player)return;
            UpdateFishList(world.CheatFishSpecies);
            resourcesPage.gameObject.SetActive(!showingFish);
            fishPage.gameObject.SetActive(showingFish);
            StyleTab(resourcesTab,!showingFish);
            StyleTab(fishTab,showingFish);
            UiKit.Set(section,showingFish?"Резервуар":"Казна экипажа");
            UiKit.Set(column,showingFish?"Свободно мест: "+world.CheatFishRoom:"В наличии");
            if(showingFish)
            {
                for(int i=0;i<fish.Count;i++)
                {
                    int count=0;
                    foreach(var catchItem in view.tank)
                        if(catchItem.species==fish[i].id)count++;
                    SetRow(fish[i],i==fishIndex,count.ToString());
                }
            }
            else
            {
                var wallet=view.Treasury;
                for(int i=0;i<resources.Count;i++)
                {
                    string balance=resources[i].id switch {
                        "biomass"=>wallet.biomass.ToString(), "chitin"=>wallet.chitin.ToString(),
                        "scrap"=>wallet.scrap.ToString(), "electronics"=>wallet.electronics.ToString(),
                        _=>TeamProgress.FoodText(view.offeringPoints)
                    };
                    SetRow(resources[i],i==resourceIndex,balance);
                }
            }
            int quantity=Quantity;
            decrease.interactable=quantity>1;
            increase.interactable=quantity<int.MaxValue;
            grant.interactable=quantity>0 && (!showingFish || fish.Count>0 && world.CheatFishRoom>0);
            UiKit.Set(actionText,showingFish?"Добавить рыбу":"Выдать ресурс");
            if(Time.unscaledTime>=movementDeadline || (requestedNoclip==player.CheatNoclip && requestedFast==player.CheatFast))
            {
                requestedNoclip=player.CheatNoclip;
                requestedFast=player.CheatFast;
                movementDeadline=0;
            }
            SetSwitch(noclip,requestedNoclip);
            SetSwitch(fast,requestedFast);
        }

        static void StyleTab(Button tab,bool active)
        {
            tab.targetGraphic.color=active?SelectedColor:RowColor;
            tab.transform.Find("Selected").GetComponent<Image>().enabled=active;
        }

        static void SetRow(ItemRow row,bool selected,string count)
        {
            row.button.targetGraphic.color=selected?SelectedColor:RowColor;
            row.selected.enabled=selected;
            UiKit.Set(row.count,count);
        }

        static void SetSwitch(ModeSwitch mode,bool on)
        {
            mode.track.color=on?UiTheme.Green:UiTheme.Edge;
            mode.knob.color=on?Ink:UiTheme.Text;
            mode.knob.rectTransform.anchoredPosition=new Vector2(on?22:3,-3);
        }

        void ToggleMovement(bool throughWalls)
        {
            if(player==null)return;
            if(throughWalls)requestedNoclip=!requestedNoclip;
            else requestedFast=!requestedFast;
            movementDeadline=Time.unscaledTime+1.5f;
            player.RequestCheatMovement(requestedNoclip,requestedFast);
            Refresh();
        }

        void Grant()
        {
            if(!player || Quantity<=0)return;
            if(showingFish)
            {
                if(fishIndex<fish.Count)player.CmdCheatGrantFish(fish[fishIndex].id,Quantity);
            }
            else player.CmdCheatGrantResource(Resources[resourceIndex].id,Quantity);
        }

        void GrantResponded(bool accepted,int granted,string message)
        {
            UiKit.Set(feedback,message);
            feedback.color=accepted?UiTheme.Green:UiTheme.Amber;
        }
    }
}
