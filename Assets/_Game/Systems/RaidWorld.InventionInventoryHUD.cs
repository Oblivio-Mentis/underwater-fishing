using System.Collections.Generic;
using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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
        Vector2 rackScroll;
        CursorLockMode inventoryPreviousLock;
        bool inventoryPreviousCursor;
        GUIStyle inventoryHeading,inventoryLabel,inventorySmall;
        public bool InventionPanelOpen => inventoryPanel!=InventoryPanel.Closed;

        public bool InventionRackTargeted(DiverMotor player) =>
            IsClientInitialized && player && player.IsOwner && !player.IsHelpless && !player.IsTrapped &&
            NearInventionRack(player) && RackFocused(player);

        bool InventoryContextAllowed()
        {
            var ui=UiRoot.Instance;
            return IsClientInitialized && localDiver && !localDiver.IsHelpless && !localDiver.IsTrapped &&
                LocalProgress?.techSlots?.Length==4 && view!=null && TravelRemaining<=0 &&
                (!ui || ui.Page==UiPage.None) && !UiInputGate.Modal && !UiInputGate.MonitorLook &&
                Time.unscaledTime>=UiInputGate.ResumeAt && Application.isFocused;
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
                rackScroll=Vector2.zero;
                if(rackBlueprints.Count>0)SelectRackBlueprint(rackSelection);
            }
            repeatRackAt=float.PositiveInfinity;
        }

        public void CloseInventionInventory()
        {
            if(!InventionPanelOpen)return;
            SkillResponded-=InventoryResponse;
            inventoryPanel=InventoryPanel.Closed;
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
            var keys=Keyboard.current;
            if(!InventoryContextAllowed() || keys==null)
            {CloseInventionInventory();return;}
            if(inventoryPanel==InventoryPanel.Rack && !NearInventionRack(localDiver))
            {CloseInventionInventory();return;}
            if(lastInventoryInputFrame==Time.frameCount)return;
            lastInventoryInputFrame=Time.frameCount;
            if(!InventionPanelOpen)
            {
                if(UiInputGate.Blocked)return;
                if(keys.eKey.wasPressedThisFrame && InventionRackTargeted(localDiver))OpenInventionInventory(true);
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
            rackScroll.y=Mathf.Max(0,(index/2)*76f-76f);
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

        static void InventoryFill(Rect rect,Color color)
        {
            var previous=GUI.color;GUI.color=color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=previous;
        }

        static Color InventoryInk => new Color32(13,29,35,247);
        static Color InventoryRow => new Color32(35,55,61,249);

        void InventoryStyles()
        {
            if(inventoryHeading!=null)return;
            inventoryHeading=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};
            inventoryLabel=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};
            inventorySmall=new GUIStyle(inventoryLabel){fontSize=12};
        }

        void InventoryText(Rect rect,string text,GUIStyle style,Color color)
        {
            style.normal.textColor=color;GUI.Label(rect,text,style);
        }

        bool InventoryButton(Rect area,string label,bool selected,bool enabled=true)
        {
            bool hover=enabled && area.Contains(Event.current.mousePosition);
            InventoryFill(area,selected?new Color32(40,86,82,255):hover?new Color32(58,82,84,255):InventoryRow);
            if(selected)InventoryFill(new Rect(area.x,area.y,3,area.height),UiTheme.Cyan);
            InventoryText(new Rect(area.x+9,area.y+1,area.width-16,area.height-2),label,inventoryLabel,enabled?UiTheme.Text:UiTheme.Muted);
            return enabled && GUI.Button(area,GUIContent.none,GUIStyle.none);
        }

        void InventionInventoryHUD()
        {
            if(!InventoryContextAllowed())return;
            InventoryStyles();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BeginRackCapture();
#endif
            var old=GUI.matrix;
            int previousDepth=GUI.depth;
            GUI.depth=-40;
            Rect safe=Screen.safeArea;
            float scale=Mathf.Min(safe.width/650f,safe.height/510f,Mathf.Clamp(safe.height/960f,.85f,1.4f));
            if(scale<=0){GUI.depth=previousDepth;return;}
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
            float width=safe.width/scale,height=safe.height/scale;
            if(InventionPanelOpen)DrawInventoryPanel(width,height);
            else DrawInventoryStrip(width,height);
            GUI.matrix=old;GUI.depth=previousDepth;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndRackCapture();
#endif
        }

        void DrawInventoryStrip(float width,float height)
        {
            bool rack=InventionRackTargeted(localDiver);
            if(rack)
            {
                var focus=new Rect((width-230)/2,height*.56f,230,31);
                InventoryFill(focus,InventoryInk);
                InventoryText(new Rect(focus.x+12,focus.y,focus.width-24,31),"[E]  Автомат снаряжения",inventoryLabel,UiTheme.Cyan);
            }
            const float slotsWidth=4*55+96;
            float x=(width-slotsWidth)/2;
            // The existing fishing toolbar occupies the bottom 104 units of the HUD.
            float y=height-153;
            InventoryFill(new Rect(x,y,slotsWidth,30),new Color32(13,29,35,218));
            InventoryText(new Rect(x+7,y,89,30),"TAB  ·  Слоты",inventorySmall,UiTheme.Cyan);
            for(int i=0;i<4;i++)
            {
                var slot=LocalProgress.techSlots[i];
                int count=slot.count;
                var r=new Rect(x+96+i*55,y+3,52,24);
                InventoryFill(r,i==inventorySlot?new Color32(45,78,77,255):InventoryRow);
                var icon=count>0?InventoryPlate(slot.kind):null;
                if(icon)GUI.DrawTexture(new Rect(r.x+2,r.y,22,24),icon,ScaleMode.ScaleToFit);
                InventoryText(new Rect(r.x+(icon?25:6),r.y,r.width-9,r.height),count>0?"×"+count:"—",inventorySmall,count>0?UiTheme.Text:UiTheme.Muted);
            }
        }

        void DrawInventoryPanel(float screenWidth,float screenHeight)
        {
            bool rack=inventoryPanel==InventoryPanel.Rack;
            float height=rack?420:224;
            float width=rack?Mathf.Min(484,screenWidth*.47f):610;
            var box=new Rect(rack?screenWidth-width-12:(screenWidth-width)/2,(screenHeight-height)/2,width,height);
            if(!rack)InventoryFill(new Rect(0,0,screenWidth,screenHeight),new Color(0,.03f,.045f,.50f));
            GUI.BeginGroup(box);
            InventoryFill(new Rect(0,0,box.width,height),InventoryInk);
            InventoryFill(new Rect(0,0,box.width,3),UiTheme.Cyan);
            InventoryText(new Rect(16,11,width-90,35),rack?"Автомат снаряжения":"Инвентарь технологий",inventoryHeading,UiTheme.Text);
            if(InventoryButton(new Rect(width-63,15,47,28),"Esc",false))CloseInventionInventory();
            DrawInventorySlots(width);
            if(rack)DrawBlueprints(width);
            else
            {
                var held=HeldInventoryRig();
                var selected=LocalProgress.techSlots[inventorySlot];
                string state=held!=null?"В руках: "+InventoryTitle(held.kind):
                    selected.count>0?"В слоте "+(inventorySlot+1)+": "+InventoryTitle(selected.kind):"Слот "+(inventorySlot+1)+" свободен";
                InventoryText(new Rect(20,126,570,30),state,inventoryLabel,UiTheme.Text);
            }
            bool allowed=InventoryAction(out var label,out var reason);
            var button=new Rect(16,height-54,rack?132:206,38);
            if(InventoryButton(button,label+"   ↵",allowed,allowed))InventoryAction();
            string note=!allowed?reason:rack?"Бесплатно  ·  удерживайте Enter":"←  →   выбрать слот";
            if(Time.unscaledTime<inventoryNoticeUntil && !string.IsNullOrEmpty(inventoryNotice))note=inventoryNotice;
            InventoryText(new Rect(button.xMax+10,height-57,width-button.xMax-24,45),note,inventorySmall,allowed?UiTheme.Muted:UiTheme.Amber);
            GUI.EndGroup();
        }

        void DrawInventorySlots(float width)
        {
            for(int i=0;i<4;i++)
            {
                var slot=LocalProgress.techSlots[i];
                int count=slot.count;
                float stride=(width-30)/4f;
                var r=new Rect(16+i*stride,55,stride-5,60);
                bool selected=i==inventorySlot;
                InventoryFill(r,selected?new Color32(40,78,77,255):InventoryRow);
                if(selected)InventoryFill(new Rect(r.x,r.yMax-3,r.width,3),UiTheme.Cyan);
                InventoryText(new Rect(r.x+5,r.y+1,r.width-8,20),"СЛОТ "+(i+1),inventorySmall,UiTheme.Muted);
                var icon=count>0?InventoryPlate(slot.kind):null;
                if(icon)GUI.DrawTexture(new Rect(r.x+3,r.y+23,29,28),icon,ScaleMode.ScaleToFit);
                InventoryText(new Rect(r.x+(icon?34:5),r.y+20,r.width-(icon?36:8),29),count>0?count+" / "+view.TechStackLimit(slot.kind):"Свободен",inventoryLabel,UiTheme.Text);
                if(GUI.Button(r,new GUIContent("",count>0?InventoryTitle(slot.kind):"Свободный слот"),GUIStyle.none))inventorySlot=i;
            }
        }

        void DrawBlueprints(float width)
        {
            InventoryText(new Rect(16,121,width-32,24),"Доступное снаряжение   ↑ ↓",inventorySmall,UiTheme.Muted);
            var area=new Rect(16,150,width-32,200);
            if(rackBlueprints.Count==0)return;
            float listWidth=area.width-17;
            int rows=(rackBlueprints.Count+1)/2;
            rackScroll=GUI.BeginScrollView(area,rackScroll,new Rect(0,0,listWidth,rows*76),false,false);
            float stride=listWidth/2;
            for(int i=0;i<rackBlueprints.Count;i++)
            {
                var item=rackBlueprints[i];
                var row=new Rect((i%2)*stride,(i/2)*76,stride-5,71);
                bool selected=i==rackSelection;
                InventoryFill(row,selected?new Color32(40,78,77,255):InventoryRow);
                if(selected)InventoryFill(new Rect(row.x,row.y,3,row.height),UiTheme.Cyan);
                float iconWidth=Mathf.Clamp(row.width*.26f,26,48);
                var icon=InventoryPlate(item.id);
                if(icon)GUI.DrawTexture(new Rect(row.x+5,row.y+6,iconWidth,row.height-12),icon,ScaleMode.ScaleToFit);
                float tx=row.x+iconWidth+9;
                inventorySmall.wordWrap=true;
                InventoryText(new Rect(tx,row.y+2,row.xMax-tx-4,41),item.title,inventorySmall,UiTheme.Text);
                inventorySmall.wordWrap=false;
                InventoryText(new Rect(tx,row.y+39,row.xMax-tx-3,26),"До "+view.TechStackLimit(item.id)+" / слот",inventorySmall,UiTheme.Cyan);
                if(GUI.Button(row,GUIContent.none,GUIStyle.none))rackSelection=i;
            }
            GUI.EndScrollView();
        }
    }
}
