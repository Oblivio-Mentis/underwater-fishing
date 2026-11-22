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
        readonly Image[] slotRim=new Image[7], slotArt=new Image[7], crewGauge=new Image[5], contacts=new Image[5];
        readonly TMP_Text[] crewName=new TMP_Text[5], keys=new TMP_Text[7];
        readonly RectTransform[] crewRows=new RectTransform[5];
        readonly TMP_Text depth,stock,food,timer,oxygen,pressure,route;
        readonly Image oxygenBar,pressureBar,healthBar,submarine;
        readonly TMP_Text healthText;
        readonly RectTransform navigation,depthTicks,hotbar,expedition;
        readonly Image[] depthMarks=new Image[13];

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
            depthTicks=At(expedition,"Depth Scale",new Vector2(.5f,1),new Vector2(.5f,1),0,-48,276,16);
            for(int i=0;i<depthMarks.Length;i++)
            {
                depthMarks[i]=Box(depthTicks,"Depth Tick",i*23,0,2,i%3==0?11:5,Ivory);
            }
            Box(depthTicks,"Baseline",0,-5,276,1,new Color(.82f,.89f,.88f,.8f));
            Box(depthTicks,"Index",137,4,3,18,Gold);
            var stockStrip=Back(expedition,"Reservoir telemetry",40,-70,306,32);
            Icon(stockStrip,"Fish",Mark.Lantern,7,-1,30,30,Ivory);
            stock=Caption(stockStrip,"Stock Count",40,0,43,32,19,TextAlignmentOptions.Left);
            Box(stockStrip,"Divider",81,-7,1,17,Rim);
            Icon(stockStrip,"Feeding",Mark.Jaw,89,-1,32,30,Ivory);
            food=Caption(stockStrip,"Food Amount",123,0,75,32,18,TextAlignmentOptions.Left);
            Box(stockStrip,"Clock divider",202,-7,1,17,Rim);
            timer=Caption(stockStrip,"Hunger Timer",208,0,88,32,19,TextAlignmentOptions.Center);
            route=Caption(expedition,"Stock Detail",0,-105,394,24,16,TextAlignmentOptions.Center);

            var vitals=At(root,"HUD Vitals",Vector2.zero,Vector2.zero,33,29,264,111);
            Icon(vitals,"Air Bottle",Mark.Bottle,0,-2,35,35,Ivory);
            oxygenBar=Meter(vitals,"Oxygen Gauge",39,-15,149,15,Blue,5);
            oxygen=Caption(vitals,"Oxygen Value",194,-2,74,40,17,TextAlignmentOptions.Left);
            Icon(vitals,"Pressure Suit",Mark.Shield,0,-41,35,35,Ivory);
            pressureBar=Meter(vitals,"Pressure Gauge",39,-54,149,15,Green,5);
            pressure=Caption(vitals,"Pressure Value",194,-41,74,40,17,TextAlignmentOptions.Left);

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
            // A pure client applies team equipment/depth tuning to its owned motor only.
            float capacityBonus=c.View!=null&&c.Tuning!=null?1+c.View.tankLevel*c.Tuning.oxygenPerTankLevel:c.Player.OxygenMultiplier;
            if(diver!=c.Player)air=Mathf.Clamp01(diver.Oxygen/Mathf.Max(1,diver.tuning.oxygenCapacity*capacityBonus));
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
            // A graduation passes the center index for each metre of real depth.
            float offset=Mathf.Repeat(metres,3)*23;
            for(int i=0;i<depthMarks.Length;i++)
            {
                var tick=depthMarks[i].rectTransform;
                float x=Mathf.Repeat(i*23-offset,299);
                tick.anchoredPosition=new Vector2(x,0);
                depthMarks[i].enabled=x<=276;
            }
            var v=c.View;
            expedition.gameObject.SetActive(v!=null);
            if(v!=null)
            {
                int n=0;foreach(var item in v.tank)if(item!=null&&(item.offering>0||item.biomass>0||item.chitin>0))n++;
                UiKit.Set(stock,n.ToString());
                UiKit.Set(food,TeamProgress.FoodText(v.offeringPoints)+" / "+c.FoodRequired);
                UiKit.Set(timer,c.DefendingReservoir?"Оборона":UiText.HudHungerTime(v.hungerRemaining));
                timer.color=c.DefendingReservoir?UiTheme.Red:v.offeringPoints==0&&v.hungerRemaining<=30?Gold:Ivory;
                food.color=v.offeringPoints>0?Green:Ivory;
                var flow=c.World.FishRoute;
                UiKit.Set(route,flow.active?(flow.stopping?"Клапан закрыт":flow.feeding?"Кормление":"Переработка")+"   "+flow.completed+" / "+flow.total:"");
            }
            UiKit.Fraction(oxygenBar,air);oxygenBar.color=air<.14f?UiTheme.Red:air<.25f?Gold:Blue;
            UiKit.Set(oxygen,Mathf.RoundToInt(air*100)+"%");
            float strain=Mathf.Clamp01(diver.PressureStress/100);
            // The shield expresses remaining suit pressure tolerance.
            UiKit.Fraction(pressureBar,1-strain);pressureBar.color=strain>.8f?UiTheme.Red:strain>.55f?Gold:Green;
            UiKit.Set(pressure,Mathf.RoundToInt((1-strain)*100)+"%");
            UiKit.Fraction(healthBar,diver.Health/100f);
            healthBar.color=diver.IsHypoxic?UiTheme.Red:diver.Health<45?Gold:Green;
            UiKit.Set(healthText,Mathf.CeilToInt(diver.Health)+"%");
            var tool=diver==c.Player?c.Harpoon:diver.GetComponent<HarpoonTool>();
            hotbar.gameObject.SetActive(tool!=null&&!diver.IsDead);
            if(tool!=null)
            {
                int slots=tool.SlotCount;
                bool carryingSalvage=tool.CarriedSalvage!=null;
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
                    bool selected=holdingSalvage || i==tool.ActiveSlot&&!tool.HandsFull;
                    slotRim[i].color=selected?Gold:Rim;keys[i].color=selected?Gold:Ivory;
                    slotArt[i].enabled=i==0?tool.OwnsHarpoon:i==1||fish!=null||ball;
                    slotArt[i].sprite=art[(int)(i==0?Mark.Spear:i==1?(holdingSalvage?Mark.Crate:Mark.Glove):ball?Mark.Ball:CatchMark(fish?.SpeciesId))];
                    slotArt[i].color=tool.HandsFull&&!holdingSalvage?new Color(.68f,.73f,.73f):Ivory;
                }
            }
            bool showNav=unobstructed&&!diver.IsInside&&!diver.IsDead;
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
