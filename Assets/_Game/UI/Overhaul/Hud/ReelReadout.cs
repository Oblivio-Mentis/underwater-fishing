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
        readonly Image stamina;
        readonly Image[] ticks=new Image[24];
        readonly RectTransform needle;
        public bool Visible => root.gameObject.activeSelf;

        public ReelReadout(Transform parent)
        {
            var shell=UiKit.Panel(parent,"Line load instrument",new Color(.043f,.081f,.091f,.90f));UiKit.Tile(shell);
            root=shell.rectTransform;
            UiKit.Place(root,new Vector2(1,.44f),new Vector2(1,.5f),new Vector2(-28,0),new Vector2(438,170));
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
            species=Text(root,"Species",142,-12,177,36,25);species.fontStyle=FontStyles.Bold;
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
            root.gameObject.SetActive(active);
            if(!active)return;
            float t=Mathf.Clamp01(tool.LineTension);
            float energy=Mathf.Clamp01(fish.Stamina/Mathf.Max(.01f,fish.tuning.initialStamina*fish.power));
            SetTick(needle,t,new Vector2(4,18));
            for(int i=0;i<ticks.Length;i++)
            {
                float amount=i/(ticks.Length-1f);Color hue=Signal(amount);
                ticks[i].color=amount<=t?hue:Color.Lerp(new Color(.10f,.15f,.15f),hue,.36f);
            }
            UiKit.Set(load,Mathf.RoundToInt(t*100)+"%");load.color=Signal(t);
            UiKit.Set(species,Species(fish.SpeciesId));
            UiKit.Set(range,Vector3.Distance(tool.transform.position,fish.transform.position).ToString("0.0")+" м");
            UiKit.Set(strength, $"Гарпуны {fish.HookCount}/{(fish.RequiresPartner ? 2 : 1)}  ·  Силы {Mathf.RoundToInt(energy*100)}%");
            UiKit.Fraction(stamina,energy);
            string action = fish.Surging ? "Рывок!" : fish.SurgeWarning ? "Готовьтесь к рывку"
                : t >= .82f ? "Ослабьте леску!"
                : fish.RequiresPartner && fish.HookCount < 2 ? "Нужно два гарпуна"
                : fish.RequiresPartner && !fish.Coordinated ? "Тяните в одну сторону"
                : fish.Mode == FishMode.Tired ? "Рыба устала · ЛКМ — тянуть"
                : fish.Coordinated ? "Тянете вместе" : "ЛКМ — сматывать";
            UiKit.Set(instruction,action);instruction.color=t>=.6f?Signal(t):UiTheme.Text;
        }
    }

}
