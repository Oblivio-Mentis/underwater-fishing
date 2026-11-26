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
        public bool InventionPanelOpen { get; private set; }
        readonly List<TechItem> rackBlueprints=new();
        readonly Dictionary<string,Texture2D> inventoryPlates=new();
        int rackSelection;
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
                LocalProgress!=null && view!=null && TravelRemaining<=0 &&
                (!ui || ui.Page==UiPage.None) && !UiInputGate.Modal && !UiInputGate.MonitorLook &&
                Time.unscaledTime>=UiInputGate.ResumeAt && InventoryFocusAllowed();
        }

        void OpenVendingMachine()
        {
            UiRoot.Instance?.CloseCheats();
            SkillResponded+=InventoryResponse;
            inventoryNotice=null;
            InventionPanelOpen=true;
            inventoryPreviousCursor=Cursor.visible;
            inventoryPreviousLock=Cursor.lockState;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            rackBlueprints.Clear();
            foreach(var item in TechCatalog.Items)
                if(view.TechRank(item.id,"unlock")>0)rackBlueprints.Add(item);
            rackSelection=Mathf.Clamp(rackSelection,0,Mathf.Max(0,rackBlueprints.Count-1));
            repeatRackAt=float.PositiveInfinity;
        }

        public void CloseInventionInventory()
        {
            if(inventoryCanvas)inventoryCanvas.gameObject.SetActive(false);
            inventoryDirty=true;
            if(!InventionPanelOpen)return;
            SkillResponded-=InventoryResponse;
            InventionPanelOpen=false;
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
            if(InventionPanelOpen && !NearInventionRack(localDiver))
            {CloseInventionInventory();return;}
            if(lastInventoryInputFrame==Time.frameCount)return;
            lastInventoryInputFrame=Time.frameCount;
            if(!InventionPanelOpen)
            {
                if(UiInputGate.Blocked && Application.isFocused)return;
                if(keys.eKey.wasPressedThisFrame && !LookAtOwnParcel(localDiver,out _) && InventionRackTargeted(localDiver))OpenVendingMachine();
                return;
            }
            if(keys.escapeKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame)
            {CloseInventionInventory();return;}
            if(rackBlueprints.Count>0)
            {
                int step=keys.upArrowKey.wasPressedThisFrame?-1:keys.downArrowKey.wasPressedThisFrame?1:0;
                if(step!=0)SelectRackBlueprint((rackSelection+step+rackBlueprints.Count)%rackBlueprints.Count);
            }
            if(keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
            {
                InventoryAction();
                repeatRackAt=Time.unscaledTime+.52f;
            }
            else if((keys.enterKey.isPressed || keys.numpadEnterKey.isPressed) && Time.unscaledTime>=repeatRackAt)
            {InventoryAction();repeatRackAt=Time.unscaledTime+.28f;}
        }

        void SelectRackBlueprint(int index) => rackSelection=index;

        int ReceivingPocket()
        {
            if(rackBlueprints.Count==0)return -1;
            var tool=localDiver.GetComponent<HarpoonTool>();
            string kind=rackBlueprints[rackSelection].id;
            return tool ? tool.FindInventionPocket(kind,view.TechStackLimit(kind)) : -1;
        }

        void InventoryAction()
        {
            if(!InventoryContextAllowed() || !InventionPanelOpen || Time.unscaledTime<nextInventoryAction || ReceivingPocket()<0)return;
            nextInventoryAction=Time.unscaledTime+.23f;
            localDiver.CmdRackTake(rackBlueprints[rackSelection].id);
        }

        Texture2D InventoryPlate(string kind)
        {
            if(string.IsNullOrEmpty(kind))return null;
            if(!inventoryPlates.TryGetValue(kind,out var plate))
            {plate=Resources.Load<Texture2D>("Technology/Icons/"+kind);inventoryPlates[kind]=plate;}
            return plate;
        }

        static Color InventoryInk => new Color32(13,29,35,247);
        static Color InventoryRow => new Color32(35,55,61,249);
        static Color InventorySelected => new Color32(39,82,78,255);

        void RefreshInventoryCanvas()
        {
            bool focus=!InventionPanelOpen && !UiRoot.Instance && InventoryContextAllowed() && InventionRackTargeted(localDiver);
            if(!InventionPanelOpen && !focus)
            {
                if(inventoryCanvas)inventoryCanvas.gameObject.SetActive(false);
                inventoryDirty=true;return;
            }
            if(!inventoryCanvas)
            {
                var go=new GameObject("Vending machine UI",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
                go.transform.SetParent(transform,false);
                inventoryCanvas=go.GetComponent<Canvas>();
                inventoryCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
                inventoryCanvas.sortingOrder=1000;
                inventoryCanvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1;
            }
            inventoryCanvas.gameObject.SetActive(true);
            int pocket=InventionPanelOpen ? ReceivingPocket() : -1;
            int stamp=unchecked(((Screen.safeArea.GetHashCode()*31+rackSelection)*31+pocket)*31+view.teamRevision);
            stamp=unchecked(stamp*31+(focus?1:0));
            string notice=Time.unscaledTime<inventoryNoticeUntil?inventoryNotice:null;
            stamp=unchecked(stamp*31+(notice?.GetHashCode()??0));
            if(inventoryPage && !inventoryDirty && stamp==inventoryStamp)return;
            inventoryDirty=false;inventoryStamp=stamp;
            if(inventoryPage){inventoryPage.gameObject.SetActive(false);Destroy(inventoryPage.gameObject);}
            Rect safe=Screen.safeArea;
            float scale=Mathf.Min(safe.width/650f,safe.height/510f,Mathf.Clamp(safe.height/960f,.85f,1.4f));
            if(scale<=0)return;
            float width=safe.width/scale,height=safe.height/scale;
            inventoryPage=InventoryNode((RectTransform)inventoryCanvas.transform,"Vending",new Rect(safe.x,Screen.height-safe.yMax,width,height));
            inventoryPage.localScale=new Vector3(scale,scale,1);
            if(InventionPanelOpen)DrawVendingWindow(width,height,notice,pocket);
            else
            {
                var hint=InventoryBox(inventoryPage,"Machine focus",new Rect((width-234)/2,height*.56f,234,31),InventoryInk);
                InventoryText(hint,"[E]   Автомат снаряжения",new Rect(10,0,218,30),16,UiTheme.Cyan);
            }
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

        void DrawVendingWindow(float screenWidth,float screenHeight,string notice,int pocket)
        {
            float width=Mathf.Min(484,screenWidth*.47f);
            const float height=327;
            var panel=InventoryBox(inventoryPage,"Equipment panel",new Rect(screenWidth-width-12,(screenHeight-height)/2,width,height),InventoryInk);
            InventoryBox(panel,"Signal rail",new Rect(0,0,width,3),UiTheme.Cyan);
            InventoryText(panel,"Автомат снаряжения",new Rect(16,10,width-82,39),22,UiTheme.Text);
            var exit=InventoryBox(panel,"Close",new Rect(width-58,14,45,29),InventoryRow);
            InventoryText(exit,"Esc",new Rect(8,0,34,29),14,UiTheme.Text);InventoryClick(exit,CloseInventionInventory);
            DrawProductCards(panel,width);
            bool allowed=pocket>=0;
            const float buttonWidth=138;
            var action=InventoryBox(panel,"Transfer",new Rect(16,height-52,buttonWidth,36),allowed?InventorySelected:InventoryRow);
            InventoryText(action,"Выдать +1  Enter",new Rect(8,0,buttonWidth-12,36),15,allowed?UiTheme.Text:UiTheme.Muted);
            if(allowed)InventoryClick(action,InventoryAction);
            string status=notice??(rackBlueprints.Count==0?"Сначала исследуйте чертёж на терминале":
                allowed?$"В инвентарь · слот {pocket+1}":"В инвентаре нет места");
            InventoryText(panel,status,new Rect(26+buttonWidth,height-60,width-buttonWidth-42,53),14,allowed?UiTheme.Muted:UiTheme.Amber,true);
        }

        void DrawProductCards(RectTransform panel,float width)
        {
            InventoryText(panel,"Доступное снаряжение   ↑ ↓",new Rect(16,51,width-32,24),14,UiTheme.Muted);
            int page=rackSelection/4;
            if(rackBlueprints.Count>4)
            {
                int pages=(rackBlueprints.Count+3)/4;
                var prev=InventoryBox(panel,"Previous products",new Rect(width-100,49,32,25),InventoryRow);
                var next=InventoryBox(panel,"Next products",new Rect(width-51,49,32,25),InventoryRow);
                InventoryText(prev,"‹",new Rect(9,0,18,25),19,UiTheme.Text);InventoryText(next,"›",new Rect(9,0,18,25),19,UiTheme.Text);
                InventoryClick(prev,()=>SelectRackBlueprint(((page+pages-1)%pages)*4));InventoryClick(next,()=>SelectRackBlueprint(((page+1)%pages)*4));
            }
            float stride=(width-30)/2;
            for(int i=page*4;i<Mathf.Min(rackBlueprints.Count,page*4+4);i++)
            {
                int index=i;
                var item=rackBlueprints[i];
                var card=InventoryBox(panel,item.title,new Rect(16+(i%2)*stride,81+((i%4)/2)*92,stride-6,84),i==rackSelection?InventorySelected:InventoryRow);
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
