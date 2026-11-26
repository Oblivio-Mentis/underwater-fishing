using System.Collections.Generic;
using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        enum InventoryPanel { Closed, Backpack, Rack }
        InventoryPanel inventoryPanel;
        readonly List<TechItem> rackBlueprints=new();
        readonly Dictionary<string,Texture2D> inventoryPlates=new();
        int inventorySlot,rackSelection;
        float nextInventoryAction,repeatRackAt;
        int lastInventoryInputFrame=-1;
        string inventoryNotice;
        float inventoryNoticeUntil;

        CursorLockMode inventoryPreviousLock;
        bool inventoryPreviousCursor;
        Canvas inventoryCanvas;
        RectTransform inventoryPage;
        int inventoryStamp;
        bool inventoryDirty=true;
        public bool InventionPanelOpen => inventoryPanel!=InventoryPanel.Closed;

        public bool InventionRackTargeted(DiverMotor player) =>
            IsClientInitialized && player && player.IsOwner && !player.IsHelpless && !player.IsTrapped &&
            RackFocused(player,true);

        bool InventoryFocusAllowed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(rackUiFixture)return true;
#endif
            return Application.isFocused;
        }

        bool InventoryContextAllowed()
        {
            var ui=UiRoot.Instance;
            return IsClientInitialized && localDiver && !localDiver.IsHelpless && !localDiver.IsTrapped &&
                LocalProgress?.techSlots?.Length==4 && view!=null && TravelRemaining<=0 &&
                (!ui || ui.Page==UiPage.None) && !UiInputGate.Modal && !UiInputGate.MonitorLook &&
                Time.unscaledTime>=UiInputGate.ResumeAt && InventoryFocusAllowed();
        }

        void OpenInventionInventory(bool rack)
        {
            SkillResponded+=InventoryResponse;
            inventoryNotice=null;
            inventoryPanel=rack?InventoryPanel.Rack:InventoryPanel.Backpack;
            inventoryPreviousCursor=Cursor.visible;
            inventoryPreviousLock=Cursor.lockState;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            rackBlueprints.Clear();
            if(rack)
            {
                foreach(var item in TechCatalog.Items)
                    if(view.TechRank(item.id,"unlock")>0)rackBlueprints.Add(item);
                rackSelection=Mathf.Clamp(rackSelection,0,Mathf.Max(0,rackBlueprints.Count-1));

                if(rackBlueprints.Count>0)SelectRackBlueprint(rackSelection);
            }
            repeatRackAt=float.PositiveInfinity;
        }

        public void CloseInventionInventory()
        {
            if(inventoryCanvas)inventoryCanvas.gameObject.SetActive(false);
            inventoryDirty=true;
            if(!InventionPanelOpen)return;
            SkillResponded-=InventoryResponse;
            inventoryPanel=InventoryPanel.Closed;
            UiInputGate.ResumeAt=Mathf.Max(UiInputGate.ResumeAt,Time.unscaledTime+.15f);
            if(!UiInputGate.Modal && (!UiRoot.Instance || UiRoot.Instance.Page==UiPage.None) && Application.isFocused)
            {Cursor.lockState=inventoryPreviousLock;Cursor.visible=inventoryPreviousCursor;}
        }

        void InventoryResponse(bool accepted,string message)
        {
            inventoryNotice=message;
            inventoryNoticeUntil=Time.unscaledTime+1.6f;
        }

        void PresentInventionInventory()
        {
            UpdateInventionInventoryInput();
            RefreshInventoryCanvas();
        }

        void UpdateInventionInventoryInput()
        {
            var keys=Keyboard.current;
            if(!InventoryContextAllowed() || keys==null)
            {CloseInventionInventory();return;}
            if(inventoryPanel==InventoryPanel.Rack && !NearInventionRack(localDiver))
            {CloseInventionInventory();return;}
            if(lastInventoryInputFrame==Time.frameCount)return;
            lastInventoryInputFrame=Time.frameCount;
            if(!InventionPanelOpen)
            {
                if(UiInputGate.Blocked && Application.isFocused)return;
                if(keys.eKey.wasPressedThisFrame && !LookAtOwnParcel(localDiver,out _) && InventionRackTargeted(localDiver))OpenInventionInventory(true);
                else if(keys.tabKey.wasPressedThisFrame)OpenInventionInventory(false);
                return;
            }
            if(keys.escapeKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame ||
                (inventoryPanel==InventoryPanel.Rack && keys.eKey.wasPressedThisFrame))
            {CloseInventionInventory();return;}
            if(keys.leftArrowKey.wasPressedThisFrame)inventorySlot=(inventorySlot+3)%4;
            if(keys.rightArrowKey.wasPressedThisFrame)inventorySlot=(inventorySlot+1)%4;
            if(inventoryPanel==InventoryPanel.Rack && rackBlueprints.Count>0)
            {
                int step=keys.upArrowKey.wasPressedThisFrame?-1:keys.downArrowKey.wasPressedThisFrame?1:0;
                if(step!=0)SelectRackBlueprint((rackSelection+step+rackBlueprints.Count)%rackBlueprints.Count);
            }
            if(keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
            {
                InventoryAction();
                repeatRackAt=Time.unscaledTime+.52f;
            }
            else if(inventoryPanel==InventoryPanel.Rack && (keys.enterKey.isPressed || keys.numpadEnterKey.isPressed) && Time.unscaledTime>=repeatRackAt)
            {InventoryAction();repeatRackAt=Time.unscaledTime+.28f;}
        }

        void SelectRackBlueprint(int index)
        {
            rackSelection=index;

        }

        TechRig HeldInventoryRig()
        {
            foreach(var r in view.rigs)
                if(r.Carrying(localDiver.OwnerId) && r.operatorId==localDiver.OwnerId)return r;
            return null;
        }

        static string InventoryTitle(string kind) => TechCatalog.Find(kind)?.title??"Пусто";

        bool InventoryAction(out string label,out string reason)
        {
            var slot=LocalProgress.techSlots[inventorySlot];
            string kind=slot.kind;
            int count=slot.count;
            if(inventoryPanel==InventoryPanel.Rack)
            {
                label="Выдать  +1";reason="";
                if(rackBlueprints.Count==0){reason="Сначала исследуйте чертёж на инженерном терминале.";return false;}
                var item=rackBlueprints[rackSelection];
                if(view.TechRank(item.id,"unlock")<=0){reason="Чертёж ещё не исследован.";return false;}
                if(count>0 && kind!=item.id){reason="В этом слоте другой предмет. Выберите свободный слот.";return false;}
                if(count>=view.TechStackLimit(item.id)){reason="Стопка заполнена. Выберите другой слот.";return false;}
                return NearInventionRack(localDiver);
            }
            var held=HeldInventoryRig();
            if(held!=null)
            {
                label="Убрать в слот";reason="";
                if(held.kind=="basket" || held.helperId>=0 || held.contents.Count>0 || held.brood.Count>0 ||
                    held.baitId>=0 || held.targetSerial!=0 || held.health<.999f || !string.IsNullOrEmpty(held.flight))
                {reason="Освободите и отремонтируйте снаряжение перед укладкой.";return false;}
                if(count>0 && kind!=held.kind){reason="Выберите свободный слот или стопку того же предмета.";return false;}
                if(count>=view.TechStackLimit(held.kind)){reason="В этой стопке нет места.";return false;}
                return true;
            }
            label="Достать из слота";reason=count>0?"":"Выберите слот со снаряжением.";
            return count>0;
        }

        void InventoryAction()
        {
            if(!InventoryContextAllowed() || !InventionPanelOpen || Time.unscaledTime<nextInventoryAction || !InventoryAction(out _,out _))return;
            nextInventoryAction=Time.unscaledTime+.23f;
            if(inventoryPanel==InventoryPanel.Rack)localDiver.CmdRackTake(rackBlueprints[rackSelection].id,inventorySlot);
            else
            {
                var held=HeldInventoryRig();
                if(held!=null)localDiver.CmdTechStore(held.serial,inventorySlot);
                else localDiver.CmdTechEquipSlot(inventorySlot);
            }
        }

        Texture2D InventoryPlate(string kind)
        {
            if(string.IsNullOrEmpty(kind))return null;
            if(!inventoryPlates.TryGetValue(kind,out var plate))
            {plate=Resources.Load<Texture2D>("Technology/Icons/"+kind);inventoryPlates[kind]=plate;}
            return plate;
        }

        bool InventoryPermitted => InventoryContextAllowed();
        static Color InventoryInk => new Color32(13,29,35,247);
        static Color InventoryRow => new Color32(35,55,61,249);
        static Color InventorySelected => new Color32(39,82,78,255);

        void RefreshInventoryCanvas()
        {
            if(!InventoryPermitted)
            {
                if(inventoryCanvas)inventoryCanvas.gameObject.SetActive(false);
                inventoryDirty=true;return;
            }
            if(!inventoryCanvas)
            {
                var go=new GameObject("Technology inventory",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
                go.transform.SetParent(transform,false);
                inventoryCanvas=go.GetComponent<Canvas>();
                inventoryCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
                inventoryCanvas.sortingOrder=1000;
                inventoryCanvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1;
            }
            inventoryCanvas.gameObject.SetActive(true);
            var held=InventionPanelOpen?HeldInventoryRig():null;
            bool focus=!InventionPanelOpen && InventionRackTargeted(localDiver);
            int stamp=unchecked((((Screen.safeArea.GetHashCode()*31+(int)inventoryPanel)*31+inventorySlot)*31+rackSelection)*31+LocalProgress.revision);
            stamp=unchecked(stamp*31+view.teamRevision);
            stamp=unchecked(stamp*31+(focus?1:0));
            stamp=unchecked(stamp*31+(held?.serial??0));
            if(held!=null)stamp=unchecked(stamp*31+held.contents.Count+held.brood.Count+held.health.GetHashCode());
            string notice=Time.unscaledTime<inventoryNoticeUntil?inventoryNotice:null;
            stamp=unchecked(stamp*31+(notice?.GetHashCode()??0));
            if(inventoryPage && !inventoryDirty && stamp==inventoryStamp)return;
            inventoryDirty=false;inventoryStamp=stamp;
            if(inventoryPage){inventoryPage.gameObject.SetActive(false);Destroy(inventoryPage.gameObject);}
            Rect safe=Screen.safeArea;
            float scale=Mathf.Min(safe.width/650f,safe.height/510f,Mathf.Clamp(safe.height/960f,.85f,1.4f));
            if(scale<=0)return;
            float width=safe.width/scale,height=safe.height/scale;
            inventoryPage=InventoryNode((RectTransform)inventoryCanvas.transform,"Loadout",new Rect(safe.x,Screen.height-safe.yMax,width,height));
            inventoryPage.localScale=new Vector3(scale,scale,1);
            if(InventionPanelOpen)DrawInventoryWindow(width,height,notice);
            else DrawInventoryStrip(width,height,focus);
        }

        static RectTransform InventoryNode(RectTransform parent,string name,Rect rect)
        {
            var node=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            node.SetParent(parent,false);
            node.anchorMin=node.anchorMax=node.pivot=Vector2.up;
            node.anchoredPosition=new Vector2(rect.x,-rect.y);node.sizeDelta=rect.size;
            return node;
        }

        static RectTransform InventoryBox(RectTransform parent,string name,Rect rect,Color tint)
        {
            var node=InventoryNode(parent,name,rect);
            var img=node.gameObject.AddComponent<Image>();img.color=tint;img.raycastTarget=false;
            return node;
        }

        static void InventoryText(RectTransform parent,string text,Rect area,float size,Color tint,bool wrap=false)
        {
            var label=InventoryNode(parent,"Label",area).gameObject.AddComponent<TextMeshProUGUI>();
            if(UiRoot.Instance && UiRoot.Instance.font)label.font=UiRoot.Instance.font;
            label.fontSize=size;label.color=tint;label.text=text;label.alignment=TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
            label.overflowMode=TextOverflowModes.Ellipsis;label.raycastTarget=false;
        }

        void InventoryIcon(RectTransform parent,string kind,Rect area)
        {
            var texture=InventoryPlate(kind);
            if(!texture)return;
            float size=Mathf.Min(area.width,area.height);
            var icon=InventoryNode(parent,"Product artwork",new Rect(area.center.x-size*.5f,area.center.y-size*.5f,size,size)).gameObject.AddComponent<RawImage>();
            icon.texture=texture;icon.raycastTarget=false;
        }

        static void InventoryClick(RectTransform surface,UnityEngine.Events.UnityAction action)
        {
            var image=surface.GetComponent<Image>();
            image.raycastTarget=true;
            var button=surface.gameObject.AddComponent<Button>();
            button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            button.onClick.AddListener(action);
        }

        void DrawInventoryStrip(float width,float height,bool focus)
        {
            if(focus && !UiRoot.Instance)
            {
                var hint=InventoryBox(inventoryPage,"Machine focus",new Rect((width-234)/2,height*.56f,234,31),InventoryInk);
                InventoryText(hint,"[E]   Автомат снаряжения",new Rect(10,0,218,30),16,UiTheme.Cyan);
            }
            const float stripWidth=316;
            var bar=InventoryBox(inventoryPage,"Technology quick slots",new Rect((width-stripWidth)/2,height-153,stripWidth,30),InventoryInk);
            InventoryText(bar,"TAB · Слоты",new Rect(7,0,86,30),12,UiTheme.Cyan);
            for(int i=0;i<TechSlot.Slots;i++)
            {
                var slot=LocalProgress.techSlots[i];
                var cell=InventoryBox(bar,"Slot "+(i+1),new Rect(96+i*55,3,52,24),i==inventorySlot?InventorySelected:InventoryRow);
                if(slot.count>0)InventoryIcon(cell,slot.kind,new Rect(1,1,24,22));
                InventoryText(cell,slot.count>0?"×"+slot.count:"—",new Rect(slot.count>0?26:8,0,24,24),13,UiTheme.Text);
            }
        }

        void DrawInventoryWindow(float screenWidth,float screenHeight,string notice)
        {
            bool rack=inventoryPanel==InventoryPanel.Rack;
            float height=rack?420:224;
            float width=rack?Mathf.Min(484,screenWidth*.47f):610;
            if(!rack)InventoryBox(inventoryPage,"Shade",new Rect(0,0,screenWidth,screenHeight),new Color(0,.03f,.045f,.50f));
            var panel=InventoryBox(inventoryPage,"Equipment panel",new Rect(rack?screenWidth-width-12:(screenWidth-width)/2,(screenHeight-height)/2,width,height),InventoryInk);
            InventoryBox(panel,"Signal rail",new Rect(0,0,width,3),UiTheme.Cyan);
            InventoryText(panel,rack?"Автомат снаряжения":"Инвентарь технологий",new Rect(16,10,width-82,39),22,UiTheme.Text);
            var exit=InventoryBox(panel,"Close",new Rect(width-58,14,45,29),InventoryRow);
            InventoryText(exit,"Esc",new Rect(8,0,34,29),14,UiTheme.Text);InventoryClick(exit,CloseInventionInventory);
            float step=(width-30)/4;
            for(int i=0;i<TechSlot.Slots;i++)
            {
                int index=i;
                var slot=LocalProgress.techSlots[i];
                var cell=InventoryBox(panel,"Slot "+(i+1),new Rect(16+i*step,55,step-5,60),i==inventorySlot?InventorySelected:InventoryRow);
                InventoryText(cell,"СЛОТ "+(i+1),new Rect(5,0,step-12,20),12,UiTheme.Muted);
                if(i==inventorySlot)InventoryBox(cell,"Selected",new Rect(0,57,step-5,3),UiTheme.Cyan);
                if(slot.count>0)InventoryIcon(cell,slot.kind,new Rect(4,22,27,27));
                InventoryText(cell,slot.count>0?slot.count+" / "+view.TechStackLimit(slot.kind):"Свободен",new Rect(slot.count>0?33:5,20,step-(slot.count>0?39:11),31),14,UiTheme.Text);
                InventoryClick(cell,()=>inventorySlot=index);
            }
            if(rack)DrawProductCards(panel,width);
            else
            {
                var held=HeldInventoryRig();
                var selected=LocalProgress.techSlots[inventorySlot];
                string line=held!=null?"В руках: "+InventoryTitle(held.kind):selected.count>0?"В слоте "+(inventorySlot+1)+": "+InventoryTitle(selected.kind):"Слот "+(inventorySlot+1)+" свободен";
                InventoryText(panel,line,new Rect(16,123,width-32,32),16,UiTheme.Text);
            }
            bool allowed=InventoryAction(out string label,out string reason);
            float buttonWidth=rack?138:206;
            var action=InventoryBox(panel,"Transfer",new Rect(16,height-53,buttonWidth,37),allowed?InventorySelected:InventoryRow);
            InventoryText(action,label+"  Enter",new Rect(9,0,buttonWidth-14,37),15,allowed?UiTheme.Text:UiTheme.Muted);
            if(allowed)InventoryClick(action,InventoryAction);
            string note=notice??(!allowed?reason:rack?"Бесплатно  ·  удерживайте Enter":"←  →   выбрать слот");
            InventoryText(panel,note,new Rect(26+buttonWidth,height-60,width-buttonWidth-42,52),13,allowed?UiTheme.Muted:UiTheme.Amber,true);
        }

        void DrawProductCards(RectTransform panel,float width)
        {
            InventoryText(panel,"Доступное снаряжение   ↑ ↓",new Rect(16,124,width-32,24),14,UiTheme.Muted);
            int page=rackSelection/4;
            if(rackBlueprints.Count>4)
            {
                int pages=(rackBlueprints.Count+3)/4;
                var prev=InventoryBox(panel,"Previous products",new Rect(width-100,122,32,25),InventoryRow);
                var next=InventoryBox(panel,"Next products",new Rect(width-51,122,32,25),InventoryRow);
                InventoryText(prev,"‹",new Rect(9,0,18,25),19,UiTheme.Text);InventoryText(next,"›",new Rect(9,0,18,25),19,UiTheme.Text);
                InventoryClick(prev,()=>SelectRackBlueprint(((page+pages-1)%pages)*4));InventoryClick(next,()=>SelectRackBlueprint(((page+1)%pages)*4));
            }
            float stride=(width-30)/2;
            for(int i=page*4;i<Mathf.Min(rackBlueprints.Count,page*4+4);i++)
            {
                int index=i;
                var item=rackBlueprints[i];
                var card=InventoryBox(panel,item.title,new Rect(16+(i%2)*stride,152+((i%4)/2)*92,stride-6,84),i==rackSelection?InventorySelected:InventoryRow);
                if(i==rackSelection)InventoryBox(card,"Selected",new Rect(0,0,3,84),UiTheme.Cyan);
                float icon=Mathf.Clamp(stride*.27f,32,58);
                InventoryIcon(card,item.id,new Rect(5,10,icon,64));
                InventoryText(card,item.title,new Rect(icon+11,3,stride-icon-22,47),14,UiTheme.Text,true);
                InventoryText(card,"До "+view.TechStackLimit(item.id)+" / слот",new Rect(icon+11,50,stride-icon-21,28),12,UiTheme.Cyan);
                InventoryClick(card,()=>SelectRackBlueprint(index));
            }
        }
    }
}
