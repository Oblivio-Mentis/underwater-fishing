using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // The whole display reads the same confirmed diver, world and tool snapshots as the terminals.
    sealed class DiveInstruments
    {
        internal enum Mark { Helmet, Spear, Glove, Lantern, Bristle, Needle, Ray, Bottle, Jaw, Shield, Submarine, Tile, Compass, SlotOutline, Ball, Biomass, Chitin, Scrap, Electronics, Wrench, Bathymetry, Crate }
        static readonly Color Ink=new Color(.022f,.05f,.061f,.83f);
        static readonly Color Rim=new Color(.60f,.77f,.78f,.49f);
        static readonly Color Ivory=new Color(.95f,.97f,.94f);
        static readonly Color Green=new Color(.44f,.87f,.49f);
        static readonly Color Blue=new Color(.42f,.86f,.94f);
        static readonly Color Gold=new Color(1f,.76f,.32f);
        static readonly Vector2 Center=new Vector2(.5f,.5f);
        static Sprite[] art;
        // NereidInventionIcons uses four columns, read from the top-left.
        static readonly string[] inventionCells={
            "charge","vacuum","net","trap",
            "lure","bubbles","farm","cage",
            "winch","magnet","oxygen","robot",
            "mail","basket"
        };
        static Texture2D inventionAtlas;
        static readonly Dictionary<string,Sprite> inventionIcons=new();
        readonly Image[] slotRim=new Image[HarpoonTool.MaximumSlots], slotArt=new Image[HarpoonTool.MaximumSlots], crewGauge=new Image[5], contacts=new Image[5];
        readonly TMP_Text[] crewName=new TMP_Text[5], keys=new TMP_Text[HarpoonTool.MaximumSlots];
        readonly TMP_Text[] pocketAmounts=new TMP_Text[HarpoonTool.MaximumSlots];
        readonly RectTransform[] crewRows=new RectTransform[5];
        readonly TMP_Text depth,stock,food,oxygen,pressure,route;
        readonly Image phaseMeter, feedingRisk, riskElapsed, phaseIndex;
        readonly TMP_Text phaseCaption, phaseValue;
        readonly Image oxygenBar,pressureBar,healthBar,submarine;
        readonly TMP_Text healthText;
        readonly RectTransform navigation,hotbar,expedition;
        const int PhaseTrackWidth=278;

        public DiveInstruments(RectTransform root)
        {
            if(art==null||art[0]==null)LoadArt();
            var crew=At(root,"HUD Crew",new Vector2(0,1),new Vector2(0,1),28,-29,206,258);
            for(int i=0;i<crewRows.Length;i++)
            {
                crewRows[i]=At(crew,"Crew Slot "+i,new Vector2(0,1),new Vector2(0,1),0,-49*i,206,45);
                var row=crewRows[i];
                Icon(row,"Diver Helmet",Mark.Helmet,0,0,47,47,Ivory);
                crewGauge[i]=Meter(row,"Crew Health",48,-29,111,12,Green,4);
                crewName[i]=Caption(row,"Player Identity",51,-1,143,24,15,TextAlignmentOptions.MidlineLeft);
            }

            expedition=At(root,"HUD Expedition",new Vector2(.5f,1),new Vector2(.5f,1),0,-13,394,126);
            depth=Caption(expedition,"Depth Value",0,0,394,43,36,TextAlignmentOptions.Center);
            depth.fontStyle=FontStyles.Bold;
            var phaseStrip=Back(expedition,"Expedition phase",43,-43,308,43);
            phaseCaption=Caption(phaseStrip,"Phase",15,-2,212,20,17,TextAlignmentOptions.Left);
            phaseValue=Caption(phaseStrip,"Phase value",226,-2,67,20,16,TextAlignmentOptions.Right);
            var phaseTrack=Box(phaseStrip,"Phase track",15,-26,PhaseTrackWidth,10,new Color(.20f,.35f,.39f,.88f)).rectTransform;
            phaseMeter=UiKit.Panel(phaseTrack,"Phase fill",Blue);
            UiKit.Fill(phaseMeter.rectTransform);
            feedingRisk=Box(phaseTrack,"Feeding window",0,0,1,10,new Color(.55f,.13f,.18f,.96f));
            riskElapsed=UiKit.Panel(feedingRisk.transform,"Time in feeding window",UiTheme.Red);
            UiKit.Fill(riskElapsed.rectTransform);
            phaseIndex=Box(phaseTrack,"Phase pointer",0,3,2,16,Ivory);
            var stockStrip=Back(expedition,"Reservoir telemetry",95,-95,204,32);
            Icon(stockStrip,"Fish",Mark.Lantern,7,-1,30,30,Ivory);
            stock=Caption(stockStrip,"Stock Count",40,0,43,32,19,TextAlignmentOptions.Left);
            Box(stockStrip,"Divider",81,-7,1,17,Rim);
            Icon(stockStrip,"Feeding",Mark.Jaw,89,-1,32,30,Ivory);
            food=Caption(stockStrip,"Food Amount",123,0,75,32,18,TextAlignmentOptions.Left);
            route=Caption(expedition,"Stock Detail",0,-129,394,24,16,TextAlignmentOptions.Center);

            var vitals=At(root,"HUD Vitals",Vector2.zero,Vector2.zero,33,29,264,111);
            Icon(vitals,"Air Bottle",Mark.Bottle,0,-2,35,35,Ivory);
            oxygenBar=Meter(vitals,"Oxygen Gauge",39,-15,149,15,Blue,5);
            oxygen=Caption(vitals,"Oxygen Value",194,-2,74,40,17,TextAlignmentOptions.Left);
            if(DiverMotor.PressureEnabled)
            {
                Icon(vitals,"Pressure Suit",Mark.Shield,0,-41,35,35,Ivory);
                pressureBar=Meter(vitals,"Pressure Gauge",39,-54,149,15,Green,5);
                pressure=Caption(vitals,"Pressure Value",194,-41,74,40,17,TextAlignmentOptions.Left);
            }

            var cross=At(vitals,"Health cross",Vector2.up,Vector2.up,9,-78,21,20);
            Box(cross,"Horizontal",0,-7,21,6,Ivory);Box(cross,"Vertical",7,0,6,21,Ivory);
            healthBar=Meter(vitals,"Health gauge",39,-86,149,15,Green,5);
            healthText=Caption(vitals,"Health",194,-73,74,40,17,TextAlignmentOptions.Left);

            hotbar=At(root,"HUD Equipment",new Vector2(.5f,0),new Vector2(.5f,0),0,15,362,104);
            for(int i=0;i<slotRim.Length;i++)
            {
                float x=i*74;
                slotRim[i]=Icon(hotbar,"Slot "+(i+1),Mark.SlotOutline,x,0,66,66,Rim);
                var face=Icon(slotRim[i].transform,"Slot well",Mark.Tile,3,-3,60,60,Ink);
                slotArt[i]=Icon(face.transform,"Item",Mark.Spear,5,-5,50,50,Ivory);
                pocketAmounts[i]=Caption(face.transform,"Stack count",1,-36,56,22,17,TextAlignmentOptions.MidlineRight);
                pocketAmounts[i].fontStyle=FontStyles.Bold;
                pocketAmounts[i].enabled=false;
                keys[i]=Caption(hotbar,"Key "+(i+1),x,-66,66,23,18,TextAlignmentOptions.Center);
                keys[i].fontStyle=FontStyles.Bold;UiKit.Set(keys[i],(i+1).ToString());
            }
            navigation=At(root,"HUD Navigation",Vector2.right,Vector2.right,-32,25,148,148);
            Icon(navigation,"Bearing dial",Mark.Compass,0,0,148,148,Color.white);
            Box(navigation,"Diver bearing",73,-70,2,9,Ivory);
            submarine=Icon(navigation,"Submarine contact",Mark.Submarine,0,0,34,34,new Color(.50f,.86f,.78f));
            for(int i=0;i<contacts.Length;i++)contacts[i]=Box(navigation,"Crew contact",0,0,5,5,Green);
        }

        public void Refresh(UiContext c,float air,bool unobstructed)
        {
            var diver=c.Player.Spectating!=null?c.Player.Spectating:c.Player;
            if(diver!=c.Player)air=Mathf.Clamp01(diver.Oxygen/diver.AirCapacity);
            int count=0;
            foreach(var player in c.Players)
            {
                if(player==null||!player.IsClientInitialized||count==crewRows.Length)continue;
                var row=crewRows[count];row.gameObject.SetActive(true);
                float health=player.IsDead?0:Mathf.Clamp01(player.Health/100f);
                UiKit.Fraction(crewGauge[count],health);
                crewGauge[count].color=player.IsTrapped||player.IsHelpless||health<.25f?UiTheme.Red:player.IsHypoxic?UiTheme.Amber:Green;
                string state=player.IsDead?"  ×":player.IsTrapped||player.IsHelpless?"  SOS":player.IsHypoxic?"  O₂":"";
                UiKit.Set(crewName[count],UiText.HudPlayerName(player)+state);count++;
            }
            for(int i=count;i<crewRows.Length;i++)crewRows[i].gameObject.SetActive(false);
            float metres=diver.Depth;
            if(diver!=c.Player)
            {
                float seaLevel=c.World.CurrentDepth==2?c.World.tuning.deepDepth:c.World.tuning.shallowDepth;
                metres=Mathf.Max(0,seaLevel-diver.transform.position.y);
            }
            int depthMetres=Mathf.RoundToInt(metres);
            UiKit.Set(depth,depthMetres>0?"↓  −"+depthMetres+" м":"↓  0 м");
            var v=c.View;
            expedition.gameObject.SetActive(v!=null);
            if(v!=null)
            {
                int n=0;foreach(var item in v.tank)if(item!=null&&(item.offering>0||item.biomass>0||item.chitin>0))n++;
                UiKit.Set(stock,n.ToString());
                UiKit.Set(food,TeamProgress.FoodText(v.offeringPoints)+" / "+TeamProgress.FoodText(c.FoodRequired));
                food.color=v.offeringPoints>0?Green:Ivory;
                var flow=c.World.FishRoute;
                UiKit.Set(route,flow.active?(flow.stopping?"Клапан закрыт":flow.feeding?"Кормление":"Переработка")+"   "+flow.completed+" / "+flow.total:"");
            }
            UiKit.Fraction(oxygenBar,air);oxygenBar.color=air<.14f?UiTheme.Red:air<.25f?Gold:Blue;
            UiKit.Set(oxygen,Mathf.RoundToInt(air*100)+"%");
            if(pressureBar)
            {
                float strain=Mathf.Clamp01(diver.PressureStress/100);
                UiKit.Fraction(pressureBar,1-strain);pressureBar.color=strain>.8f?UiTheme.Red:strain>.55f?Gold:Green;
                UiKit.Set(pressure,Mathf.RoundToInt((1-strain)*100)+"%");
            }
            UiKit.Fraction(healthBar,diver.Health/100f);
            healthBar.color=diver.IsHypoxic?UiTheme.Red:diver.Health<45?Gold:Green;
            UiKit.Set(healthText,Mathf.CeilToInt(diver.Health)+"%");
            var tool=diver==c.Player?c.Harpoon:diver.GetComponent<HarpoonTool>();
            hotbar.gameObject.SetActive(tool!=null&&!diver.IsDead);
            if(tool!=null)
            {
                int slots=tool.SlotCount;
                bool carryingSalvage=tool.CarriedSalvage!=null;
                var carried=tool.CarriedInvention;
                hotbar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,slots*74-8);
                for(int i=0;i<slotRim.Length;i++)
                {
                    bool available=i<slots;
                    slotRim[i].gameObject.SetActive(available);
                    keys[i].gameObject.SetActive(available);
                    if(!available)continue;
                    var fish=i>=2?tool.FishInSlot(i):null;
                    bool ball=tool.HasBallInSlot(i);
                    bool holdingSalvage=carryingSalvage && i==1;
                    bool holdingRig=carried!=null && i==1;
                    var stack=tool.InventionInSlot(i);
                    bool selected=holdingSalvage || holdingRig || i==tool.ActiveSlot&&!tool.HandsFull;
                    slotRim[i].color=selected?Gold:Rim;keys[i].color=selected?Gold:Ivory;
                    slotArt[i].enabled=i==0?tool.OwnsHarpoon:i==1||fish!=null||ball||stack.count>0;
                    if(holdingRig || stack.count>0)
                        slotArt[i].sprite=InventionIcon(holdingRig?carried.kind:stack.kind);
                    else slotArt[i].sprite=art[(int)(i==0?Mark.Spear:i==1?(holdingSalvage?Mark.Crate:Mark.Glove):ball?Mark.Ball:CatchMark(fish?.SpeciesId))];
                    slotArt[i].color=tool.HandsFull&&!holdingSalvage&&!holdingRig?new Color(.68f,.73f,.73f):Ivory;
                    pocketAmounts[i].enabled=stack.count>0;
                    if(stack.count>0)UiKit.Set(pocketAmounts[i],"×"+stack.count);
                }
            }
            var threats=c.Threats;
            bool defense=threats && threats.DefenseActive;
            feedingRisk.gameObject.SetActive(!defense && v!=null);
            if(defense)
            {
                var field=threats.Defense;
                bool approaching=field.phase is DefensePhase.Warning or DefensePhase.Encirclement;
                var color=field.breached && field.phase!=DefensePhase.Recovery || field.Ratio<=.25f?UiTheme.Red:field.Ratio<=.60f?Gold:Blue;
                string phase=field.phase switch {
                    DefensePhase.Warning=>"ДО НАПАДЕНИЯ",
                    DefensePhase.Encirclement=>"СТАЯ ПРИБЛИЖАЕТСЯ",
                    DefensePhase.Assault=>"ПОЛЕ · ВОЛНА "+Mathf.Max(1,field.wave),
                    DefensePhase.Resolution=>field.breached?"ПОЛЕ РАЗРУШЕНО · КРАЖА":"СТАЯ ОТСТУПАЕТ",
                    _=>"ПОЛЕ ВОССТАНАВЛИВАЕТСЯ"
                };
                UiKit.Set(phaseCaption,phase);
                int countdown=Mathf.CeilToInt(threats.DefenseCountdown);
                UiKit.Set(phaseValue,approaching?$"{countdown/60:00}:{countdown%60:00}":Mathf.CeilToInt(field.Ratio*100)+"%");
                float duration=(float)(field.assaultAt-field.beginsAt);
                PresentPhase(approaching?threats.DefenseCountdown/Mathf.Max(1,duration):field.Ratio,
                    threats.DefenseAlarm&&approaching?UiTheme.Red:color);
                if(!approaching && field.impact>0)
                {
                    float flash=Mathf.Clamp01(1-(float)(threats.DefenseClock-field.impactAt)/.44f);
                    phaseMeter.color=Color.Lerp(phaseMeter.color,new Color(1,.33f,.23f),flash);
                }
            }
            else if(v!=null && c.Tuning)
            {
                float window=v.HungerWindowSeconds(c.Tuning);
                float safe=v.HungerUntilRisk(c.Tuning);
                float latest=v.HungerLatest(c.Tuning);
                float start=1-window/latest;
                float elapsed=Mathf.Clamp01(start-safe/latest);
                feedingRisk.rectTransform.anchoredPosition=new Vector2(start*PhaseTrackWidth,0);
                feedingRisk.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,(1-start)*PhaseTrackWidth);
                UiKit.Fraction(riskElapsed,window>0?Mathf.Clamp01(-safe/window):0);
                UiKit.Set(phaseCaption,safe>0?"ДО КОРМЛЕНИЯ":"ОКНО КОРМЛЕНИЯ");
                UiKit.Set(phaseValue,"");
                PresentPhase(elapsed,safe>0?Blue:UiTheme.Red,Mathf.Min(elapsed,start));
            }
            bool showNav=unobstructed&&!diver.IsInside&&!diver.IsDead&&(!defense || threats.DefenseConcealment<.32f);
            navigation.gameObject.SetActive(showNav);
            if(!showNav)return;
            Plot(submarine,diver,c.World.tuning.terminalPosition,true);
            int j=0;
            foreach(var player in c.Players)
            {
                if(player==null||player==diver||player.IsDead||j==contacts.Length)continue;
                Plot(contacts[j++],diver,player.transform.position,false);
            }
            for(;j<contacts.Length;j++)contacts[j].enabled=false;
        }

        void PresentPhase(float amount,Color color,float fill=-1)
        {
            phaseCaption.color=phaseValue.color=phaseIndex.color=color;
            phaseMeter.color=fill<0?color:Blue;
            UiKit.Fraction(phaseMeter,fill<0?amount:fill);
            phaseIndex.rectTransform.anchoredPosition=new Vector2(Mathf.Clamp01(amount)*PhaseTrackWidth,3);
        }

        static void Plot(Image image,DiverMotor diver,Vector3 point,bool clampToRim)
        {
            var offset=Quaternion.Euler(0,-diver.transform.eulerAngles.y,0)*(point-diver.transform.position);
            var blip=new Vector2(offset.x,offset.z)*1.5f;
            image.enabled=clampToRim||blip.magnitude<=57;
            blip=Vector2.ClampMagnitude(blip,54);
            var r=image.rectTransform;r.anchorMin=r.anchorMax=r.pivot=Center;r.anchoredPosition=blip;
            if(clampToRim)r.localRotation=Quaternion.Euler(0,0,diver.transform.eulerAngles.y);
        }

        internal static Mark CatchMark(string species)
        {
            switch(species)
            {
                case "Bristlefish":return Mark.Bristle;
                case "Needlefish":return Mark.Needle;
                case "Sailray":return Mark.Ray;
                default:return Mark.Lantern;
            }
        }

        internal static Sprite Art(Mark mark)
        {
            if(art==null || art[0]==null)LoadArt();
            return art[(int)mark];
        }

        static Sprite InventionIcon(string kind)
        {
            if(inventionIcons.TryGetValue(kind,out var icon) && icon!=null)return icon;
            int cell=System.Array.IndexOf(inventionCells,kind);
            if(!inventionAtlas)inventionAtlas=Resources.Load<Texture2D>("NereidInventionIcons");
            if(inventionAtlas && cell>=0)
            {
                float w=inventionAtlas.width*.25f,h=inventionAtlas.height*.25f;
                var rect=new Rect(cell%4*w,(3-cell/4)*h,w,h);
                icon=Sprite.Create(inventionAtlas,rect,Center,100,0,SpriteMeshType.FullRect);
                icon.name=kind;
                icon.hideFlags=HideFlags.HideAndDontSave;
            }
            else icon=Art(kind=="basket"?Mark.Crate:Mark.Wrench);
            inventionIcons[kind]=icon;
            return icon;
        }

        static void LoadArt()
        {
            var instruments=Resources.Load<Texture2D>("DiveInstruments");
            var equipment=Resources.Load<Texture2D>("NereidEquipmentIcons");
            art=new Sprite[(int)Mark.Crate+1];
            for(int i=0;i<art.Length;i++)
            {
                bool added=i>=(int)Mark.Ball;
                var rect=added?new Rect((i-(int)Mark.Ball)*96,0,96,96):new Rect((i%8)*96,(1-i/8)*96,96,96);
                Vector4 border=i==(int)Mark.Tile||i==(int)Mark.SlotOutline?new Vector4(16,16,16,16):Vector4.zero;
                art[i]=Sprite.Create(added?equipment:instruments,rect,Center,100,0,SpriteMeshType.FullRect,border);
                art[i].name=((Mark)i).ToString();
                art[i].hideFlags=HideFlags.HideAndDontSave;
            }
        }

        static RectTransform At(Transform parent,string name,Vector2 anchor,Vector2 pivot,float x,float y,float w,float h)
        {
            var r=UiKit.Node(parent,name);UiKit.Place(r,anchor,pivot,new Vector2(x,y),new Vector2(w,h));return r;
        }
        static Image Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var img=UiKit.Panel(parent,name,color);
            UiKit.Place(img.rectTransform,Vector2.up,Vector2.up,new Vector2(x,y),new Vector2(w,h));return img;
        }
        static Image Icon(Transform p,string name,Mark mark,float x,float y,float w,float h,Color color)
        {
            var img=Box(p,name,x,y,w,h,color);img.sprite=art[(int)mark];img.type=(mark==Mark.Tile||mark==Mark.SlotOutline)?Image.Type.Sliced:Image.Type.Simple;
            return img;
        }
        static RectTransform Back(Transform p,string name,float x,float y,float w,float h)
        {return Icon(p,name,Mark.Tile,x,y,w,h,Ink).rectTransform;}

        static Image Meter(Transform p,string name,float x,float y,float w,float h,Color color,int segments)
        {
            var background=Back(p,name,x-4,y+3,w+8,h+6);
            var inner=At(background,"Track",Vector2.up,Vector2.up,4,-3,w,h);
            var fill=UiKit.Panel(inner,"Level",color);UiKit.Fraction(fill,1);
            for(int i=1;i<segments;i++)Box(inner,"Segment",w*i/segments-.8f,0,1.6f,h,Ink);
            return fill;
        }
        static TMP_Text Caption(Transform parent,string name,float x,float y,float w,float h,float size,TextAlignmentOptions alignment)
        {
            var t=UiKit.Label(parent,name,"",size,Ivory);UiKit.Place(t.rectTransform,Vector2.up,Vector2.up,new Vector2(x,y),new Vector2(w,h));t.alignment=alignment;
            t.textWrappingMode=TextWrappingModes.NoWrap;
            var shadow=t.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.8f);shadow.effectDistance=new Vector2(1,-2);return t;
        }
    }
}
