using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mark = Raid.UI.DiveInstruments.Mark;
using Key = Raid.UI.HudInputGlyph.Control;

namespace Raid.UI
{
    internal readonly struct HudBinding
    {
        public readonly Key key;
        public readonly string label;
        public HudBinding(Key key,string label){this.key=key;this.label=label;}
    }

    internal struct HudHint
    {
        public Mark symbol;
        public HudBinding first,second,third;
        public string note;
        public float progress;
        public bool caution, idle;
        public bool Empty => string.IsNullOrEmpty(first.label) && string.IsNullOrEmpty(note);

        public HudHint(Mark symbol,HudBinding first=default,HudBinding second=default,HudBinding third=default,string note=null) : this()
        {this.symbol=symbol;this.first=first;this.second=second;this.third=third;this.note=note;}
    }

    // Keep the gameplay centre in the camera's coordinate space. Only edge-anchored widgets use the safe rectangle.
    internal sealed class HudSafeArea
    {
        internal readonly RectTransform root;
        Rect lastArea;
        Vector2Int lastScreen;
        internal HudSafeArea(RectTransform parent){root=UiKit.Node(parent,"HUD safe area");UiKit.Fill(root);}
        internal void Refresh()
        {
            var screen=new Vector2Int(Screen.width,Screen.height);
            var r=Screen.safeArea;
            if(screen.x<=0||screen.y<=0||r.width<=0||r.height<=0||(lastArea==r && lastScreen==screen))return;
            lastArea=r;lastScreen=screen;
            root.anchorMin=new Vector2(r.xMin/screen.x,r.yMin/screen.y);
            root.anchorMax=new Vector2(r.xMax/screen.x,r.yMax/screen.y);
            root.offsetMin=root.offsetMax=Vector2.zero;
        }
    }

    internal sealed class HudEdgePanel
    {
        internal static readonly Color Surface = new(.018f,.052f,.068f,.91f);
        readonly RectTransform root,safe;
        readonly Canvas canvas;
        readonly Image symbol,track,progress;
        readonly TMP_Text note;
        readonly TMP_Text[] labels=new TMP_Text[3];
        readonly HudInputGlyph[] keys=new HudInputGlyph[3];
        readonly RectTransform[] rows=new RectTransform[3];

        internal HudEdgePanel(RectTransform safe)
        {
            this.safe=safe;canvas=safe.GetComponentInParent<Canvas>();
            root=UiKit.Node(safe,"HUD Context");
            symbol=UiKit.Symbol(root,"Action symbol",Mark.Glove,UiTheme.Cyan);
            var shadow=symbol.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,.02f,.025f,.94f);shadow.effectDistance=new Vector2(1.6f,-1.6f);
            AtTop(symbol.rectTransform,10,-10,29,29);
            for(int i=0;i<rows.Length;i++)
            {
                rows[i]=UiKit.Node(root,"Action "+(i+1));
                var key=UiKit.Node(rows[i],"Input");
                AtTop(key,0,0,32,31);keys[i]=key.gameObject.AddComponent<HudInputGlyph>();keys[i].Initialize();
                labels[i]=UiKit.Label(rows[i],i==0?"Context Action":"Secondary action","",20);
                RectTransform t=labels[i].rectTransform;UiKit.Fill(t);t.offsetMin=new Vector2(38,0);
                OutlineLabel(labels[i]);
                labels[i].alignment=TextAlignmentOptions.MidlineLeft;labels[i].textWrappingMode=TextWrappingModes.NoWrap;
            }
            note=UiKit.Label(root,"Context Reason","",17,UiTheme.Muted);
            OutlineLabel(note);note.alignment=TextAlignmentOptions.MidlineLeft;
            track=UiKit.Panel(root,"Rescue progress",UiTheme.Edge);
            progress=UiKit.Panel(track.transform,"Completed",UiTheme.Green);UiKit.Fill(progress.rectTransform);
            root.gameObject.SetActive(false);
        }
        internal void Show(HudHint hint,bool visible,bool aboveLegacyCargo)
        {
            visible &= !hint.Empty;
            root.gameObject.SetActive(visible);if(!visible)return;
            float scale=ReadableScale(canvas);root.localScale=Vector3.one*scale;
            float w=Mathf.Min(342,Mathf.Max(202,safe.rect.width*.39f/scale));
            float top=9;
            void Row(int i,HudBinding binding)
            {
                bool use=!string.IsNullOrEmpty(binding.label);rows[i].gameObject.SetActive(use);
                if(!use)return;
                AtTop(rows[i],44,-top,w-54,32);
                keys[i].Set(binding.key);
                keys[i].gameObject.SetActive(binding.key!=Key.None);
                labels[i].rectTransform.offsetMin=new Vector2(binding.key==Key.None?0:38,0);
                UiKit.Set(labels[i],binding.label);top+=34;
            }
            Row(0,hint.first);Row(1,hint.second);Row(2,hint.third);
            bool hasNote=!string.IsNullOrEmpty(hint.note);note.gameObject.SetActive(hasNote);
            if(hasNote)
            {
                float height=hint.note.Length>36?43:25;
                AtTop(note.rectTransform,44,-top,w-54,height);
                UiKit.Set(note,hint.note);top+=height;
            }
            bool rescue=hint.progress>0;track.gameObject.SetActive(rescue);
            if(rescue){AtTop(track.rectTransform,44,-top,w-60,5);UiKit.Fraction(progress,hint.progress);top+=8;}
            UiKit.Place(root,Vector2.right,Vector2.right,new Vector2(-24,aboveLegacyCargo?166:184),new Vector2(w,Mathf.Max(47,top+7)));
            symbol.sprite=DiveInstruments.Art(hint.symbol);
            symbol.color=hint.caution?UiTheme.Amber:UiTheme.Cyan;
        }
        static void OutlineLabel(TMP_Text label)
        {
            // Override only the local text material; the HUD font atlas is shared with the world monitors.
            label.outlineWidth=.24f;
            label.outlineColor=new Color32(8,19,23,255);
            var material=label.fontMaterial;
            if(material.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor(ShaderUtilities.ID_UnderlayColor,new Color(.01f,.025f,.03f,.94f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX,.25f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY,-.38f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness,.12f);
            }
        }
        internal static float ReadableScale(Canvas canvas) => Mathf.Clamp(.76f/Mathf.Max(.1f,canvas.scaleFactor),1,1.75f);
        internal static void AtTop(RectTransform item,float x,float y,float w,float h) =>
            UiKit.Place(item,Vector2.up,Vector2.up,new Vector2(x,y),new Vector2(w,h));
    }
}