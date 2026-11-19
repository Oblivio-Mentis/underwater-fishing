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
        readonly ReelNeedle dial;
        public bool Visible => root.gameObject.activeSelf;

        public ReelReadout(Transform parent)
        {
            var shell=UiKit.Panel(parent,"Line load instrument",new Color(.043f,.081f,.091f,.90f));UiKit.Tile(shell);
            root=shell.rectTransform;
            UiKit.Place(root,new Vector2(1,.44f),new Vector2(1,.5f),new Vector2(-28,0),new Vector2(398,158));
            var graphic=UiKit.Node(root,"Line gauge");
            UiKit.Place(graphic,Vector2.up,Vector2.up,new Vector2(10,-20),new Vector2(118,118));
            dial=graphic.gameObject.AddComponent<ReelNeedle>();dial.raycastTarget=false;
            load=Text(root,"Load",19,-64,100,31,26);
            load.alignment=TextAlignmentOptions.Center;
            var caption=Text(root,"Gauge legend",18,-109,103,22,14);caption.text="ТРОС";caption.alignment=TextAlignmentOptions.Center;
            range=Text(root,"Distance",277,-11,106,26,18);range.alignment=TextAlignmentOptions.Right;range.color=UiTheme.Cyan;
            species=Text(root,"Species",142,-14,150,34,24);species.fontStyle=FontStyles.Bold;
            strength=Text(root,"Stamina",143,-55,232,24,16);strength.color=UiTheme.Muted;
            var track=UiKit.Panel(root,"Fish stamina track",new Color(.21f,.30f,.31f));
            UiKit.Place(track.rectTransform,Vector2.up,Vector2.up,new Vector2(144,-85),new Vector2(226,8));
            stamina=UiKit.Panel(track.transform,"Remaining stamina",UiTheme.Cyan);UiKit.Fill(stamina.rectTransform);
            instruction=Text(root,"Action",142,-99,248,52,18);
            root.gameObject.SetActive(false);
        }
        static TMP_Text Text(Transform parent,string name,float x,float y,float w,float h,float size)
        {
            var t=UiKit.Label(parent,name,"",size);
            UiKit.Place(t.rectTransform,Vector2.up,Vector2.up,new Vector2(x,y),new Vector2(w,h));return t;
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
            float t=Mathf.Clamp01(fish.Tension);
            float energy=Mathf.Clamp01(fish.Stamina/Mathf.Max(.01f,fish.tuning.initialStamina*fish.power));
            dial.SetLoad(t);
            UiKit.Set(load,Mathf.RoundToInt(t*100)+"%");load.color=dial.Signal;
            UiKit.Set(species,Species(fish.SpeciesId));
            UiKit.Set(range,Vector3.Distance(tool.transform.position,fish.transform.position).ToString("0.0")+" м");
            UiKit.Set(strength,"Силы рыбы   "+Mathf.RoundToInt(energy*100)+"%");
            UiKit.Fraction(stamina,energy);
            string action=helping?"Держите напарника":fish.RequiresPartner&&!tool.IsAssisted?"Нужен захват напарника":t>=.82f?"Отпустите ЛКМ — обрыв близко":t>=.6f?"Ослабьте натяжение":"ЛКМ — тянуть"+(tool.IsAssisted?" вместе":"");
            UiKit.Set(instruction,action);instruction.color=t>=.6f?dial.Signal:UiTheme.Text;
        }
    }

    sealed class ReelNeedle : MaskableGraphic
    {
        float load;
        public Color Signal => load>=.82f?UiTheme.Red:load>=.60f?UiTheme.Amber:UiTheme.Green;
        public void SetLoad(float next){if(Mathf.Abs(next-load)<.001f)return;load=next;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;var center=r.center;float radius=Mathf.Min(r.width,r.height)*.48f;
            const int steps=28;
            for(int i=0;i<steps;i++)
            {
                float t=i/(float)steps;
                Color hue=t>=.82f?UiTheme.Red:t>=.6f?UiTheme.Amber:UiTheme.Green;
                if(t>load)hue=Color.Lerp(new Color(.105f,.157f,.157f),hue,.24f);
                var a=Direction(t);var b=Direction((i+.79f)/steps);
                Quad(mesh,center+a*radius,center+b*radius,center+b*(radius-7),center+a*(radius-7),hue);
            }
            var d=Direction(load);var n=new Vector2(-d.y,d.x);
            Quad(mesh,center+d*(radius-1)-n,center+d*(radius-1)+n,center+d*(radius-15)+n*2,center+d*(radius-15)-n*2,UiTheme.Text);
        }
        static Vector2 Direction(float fraction)
        {
            float angle=(219-258*fraction)*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        }
        static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start+2,start+3,start);
        }
    }
}
