using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // Both participants see the same hook, tension and stamina snapshots.
    sealed class ReelReadout
    {
        readonly RectTransform root;
        readonly TMP_Text species,range,load,instruction,strength;
        readonly Image stamina,caution,speciesIcon;
        float tensionTarget,energyTarget,tensionShown,energyShown;
        bool surgeCue;
        readonly Image[] ticks=new Image[24];
        readonly RectTransform needle;
        public bool Visible => root.gameObject.activeSelf;

        public ReelReadout(Transform parent)
        {
            var shell=UiKit.Panel(parent,"Line load instrument",new Color(.043f,.081f,.091f,.90f));UiKit.Tile(shell);
            root=shell.rectTransform;
            UiKit.Place(root,new Vector2(1,.44f),new Vector2(1,.5f),new Vector2(-28,0),new Vector2(438,170));
            caution=UiKit.Panel(root,"Tension warning",UiTheme.Amber);
            UiKit.Place(caution.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-4),new Vector2(414,2));
            var graphic=UiKit.Node(root,"Line gauge");
            UiKit.Place(graphic,Vector2.up,Vector2.up,new Vector2(10,-20),new Vector2(118,118));
            for(int i=0;i<ticks.Length;i++)
            {
                ticks[i]=UiKit.Panel(graphic,"Line graduation "+i,UiTheme.Muted);
                SetTick(ticks[i].rectTransform,i/(ticks.Length-1f),new Vector2(5,9));
            }
            needle=UiKit.Panel(graphic,"Load needle",UiTheme.Text).rectTransform;
            SetTick(needle,0,new Vector2(4,18));
            load=Text(root,"Load",19,-66,100,32,28);
            load.alignment=TextAlignmentOptions.Center;
            var caption=Text(root,"Gauge legend",10,-110,118,24,17);caption.text="НАГРУЗКА";caption.alignment=TextAlignmentOptions.Center;
            range=Text(root,"Distance",320,-12,101,30,21);range.alignment=TextAlignmentOptions.Right;range.color=UiTheme.Cyan;
            speciesIcon=UiKit.Symbol(root,"Caught species",DiveInstruments.Mark.Lantern,Color.white);
            UiKit.Place(speciesIcon.rectTransform,Vector2.up,Vector2.up,new Vector2(141,-14),new Vector2(32,32));
            species=Text(root,"Species",177,-12,143,36,23);species.fontStyle=FontStyles.Bold;
            species.enableAutoSizing=true;species.fontSizeMin=16;species.fontSizeMax=23;
            species.textWrappingMode=TextWrappingModes.NoWrap;
            strength=Text(root,"Stamina",143,-57,272,28,20);strength.color=UiTheme.Muted;
            var track=UiKit.Panel(root,"Fish stamina track",new Color(.21f,.30f,.31f));
            UiKit.Place(track.rectTransform,Vector2.up,Vector2.up,new Vector2(144,-85),new Vector2(266,9));
            stamina=UiKit.Panel(track.transform,"Remaining stamina",UiTheme.Cyan);UiKit.Fill(stamina.rectTransform);
            instruction=Text(root,"Action",142,-100,283,60,21);
            root.gameObject.SetActive(false);
        }
        static TMP_Text Text(Transform parent,string name,float x,float y,float w,float h,float size)
        {
            var t=UiKit.Label(parent,name,"",size);
            UiKit.Place(t.rectTransform,Vector2.up,Vector2.up,new Vector2(x,y),new Vector2(w,h));return t;
        }
        static Color Signal(float load) => load>=.82f?UiTheme.Red:load>=.6f?UiTheme.Amber:UiTheme.Green;
        static void SetTick(RectTransform rect,float value,Vector2 size)
        {
            float angle=219-258*value;
            Vector2 direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
            UiKit.Place(rect,Vector2.one*.5f,Vector2.one*.5f,direction*50,size);
            rect.localRotation=Quaternion.Euler(0,0,angle-90);
        }
        internal static string Species(string id) => id switch {
            "Bristlefish"=>"Щетинник","Sailray"=>"Парусный скат","Lanternfish"=>"Фонарная рыба",
            "Needlefish"=>"Иглорыба",_=>"Улов" };

        public void Refresh(UiContext ctx)
        {
            var tool=ctx.Harpoon;
            bool helping=tool!=null&&tool.AssistTarget!=null;
            if(helping)tool=tool.AssistTarget.GetComponent<HarpoonTool>();
            var fish=tool!=null?tool.HookedFish:null;
            bool active=!ctx.Player.IsDead && fish!=null && fish.Mode>=FishMode.Hooked && fish.Mode<=FishMode.Tired;
            bool entering=active && !Visible;
            root.gameObject.SetActive(active);
            if(!active)return;
            float t=tensionTarget=Mathf.Clamp01(tool.LineTension);
            float energy=energyTarget=Mathf.Clamp01(fish.Stamina/Mathf.Max(.01f,fish.tuning.initialStamina*fish.power));
            if(entering){tensionShown=t;energyShown=energy;}
            surgeCue=fish.SurgeWarning || fish.Surging;
            speciesIcon.sprite=DiveInstruments.Art(DiveInstruments.CatchMark(fish.SpeciesId));
            UiKit.Set(species,Species(fish.SpeciesId));
            UiKit.Set(range,Vector3.Distance(tool.transform.position,fish.transform.position).ToString("0.0")+" м");
            UiKit.Set(strength, $"Гарпуны {fish.HookCount}/{(fish.RequiresPartner ? 2 : 1)}  ·  Силы {Mathf.RoundToInt(energy*100)}%");
            string action = t>=.82f ? "Отпустите ЛКМ · леска натянута"
                : fish.Surging ? "Рывок · следите за леской" : fish.SurgeWarning ? "Рыба готовится к рывку"
                : fish.RequiresPartner && fish.HookCount < 2 ? "Нужно два гарпуна"
                : fish.RequiresPartner && fish.Mode == FishMode.Tired ? "Подплывите к рыбе, удерживая ЛКМ"
                : fish.RequiresPartner && !fish.Coordinated ? "Тяните в одну сторону"
                : fish.Mode == FishMode.Tired ? "Рыба устала · ЛКМ — тянуть"
                : fish.Coordinated ? "Тянете вместе" : "ЛКМ — сматывать";
            UiKit.Set(instruction,action);instruction.color=t>=.82f?UiTheme.Red:surgeCue?UiTheme.Amber:UiTheme.Text;
        }

        internal void Animate()
        {
            if(!root.gameObject.activeInHierarchy)return;
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*20f);
            tensionShown=Mathf.Lerp(tensionShown,tensionTarget,blend);
            energyShown=Mathf.Lerp(energyShown,energyTarget,blend);
            SetTick(needle,tensionShown,new Vector2(4,18));
            for(int i=0;i<ticks.Length;i++)
            {
                float step=i/(ticks.Length-1f);
                var hue=Signal(step);
                float lit=Mathf.Clamp01((tensionShown-step)*ticks.Length+.4f);
                ticks[i].color=Color.Lerp(Color.Lerp(new Color(.10f,.15f,.15f),hue,.36f),hue,lit);
            }
            UiKit.Set(load,Mathf.RoundToInt(tensionShown*100)+"%");
            load.color=Signal(Mathf.Max(tensionShown,tensionTarget));
            UiKit.Fraction(stamina,energyShown);
            bool dangerous=tensionTarget>=.82f;
            var accent=dangerous?UiTheme.Red:surgeCue?UiTheme.Amber:UiTheme.Cyan;
            accent.a=dangerous || surgeCue?.60f+.36f*Mathf.Sin(Time.unscaledTime*9f):.23f;
            caution.color=accent;
        }
    }

}
