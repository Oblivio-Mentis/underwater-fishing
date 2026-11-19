using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace Raid.UI
{
    public static class UiKit
    {
        public static TMP_FontAsset Font;
        public static RectTransform Node(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;
        }
        public static void Fill(RectTransform rect,float margin=0)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*margin;rect.offsetMax=-Vector2.one*margin;
        }
        public static void Place(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;
        }
        public static Image Panel(Transform parent,string name,Color color,bool raycast=false)
        {
            var r=Node(parent,name);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;
        }
        internal static void Tile(Image image)
        {
            image.sprite=DiveInstruments.Art(DiveInstruments.Mark.Tile);image.type=Image.Type.Sliced;
        }
        internal static Image Symbol(Transform parent,string name,DiveInstruments.Mark mark,Color color)
        {
            var image=Panel(parent,name,color);image.sprite=DiveInstruments.Art(mark);image.preserveAspect=true;return image;
        }
        public static RectTransform Frame(Transform parent,string name,float width=1180,float height=810)
        {
            var edge=Panel(parent,name,UiTheme.Edge,true);Place(edge.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(width,height));
            Tile(edge);
            var inside=Panel(edge.transform,"Surface",UiTheme.Steel,true);Tile(inside);Fill(inside.rectTransform,2);return inside.rectTransform;
        }
        public static TMP_Text Label(Transform parent,string name,string value,float size=22,Color? color=null)
        {
            var r=Node(parent,name);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=Font;label.text=value;label.fontSize=size;
            label.color=color??UiTheme.Text;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;
            label.overflowMode=TextOverflowModes.Overflow;label.margin=Vector4.zero;
            label.enableAutoSizing=true;label.fontSizeMin=Mathf.Min(16,size);label.fontSizeMax=size;return label;
        }
        public static LayoutElement Size(Component component,float height=-1,float width=-1,float flexibleWidth=0)
        {
            var e=component.GetComponent<LayoutElement>();if(e==null)e=component.gameObject.AddComponent<LayoutElement>();
            if(height>=0){e.minHeight=height;e.preferredHeight=height;e.flexibleHeight=0;}if(width>=0){e.minWidth=width;e.preferredWidth=width;}e.flexibleWidth=flexibleWidth;return e;
        }
        public static RectTransform Column(Transform parent,string name,float spacing=12,int padding=0)
        {
            var r=Node(parent,name);var layout=r.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(padding,padding,padding,padding);
            layout.spacing=spacing;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;return r;
        }
        public static RectTransform Row(Transform parent,string name,float spacing=12,int padding=0)
        {
            var r=Node(parent,name);var layout=r.gameObject.AddComponent<HorizontalLayoutGroup>();layout.padding=new RectOffset(padding,padding,padding,padding);
            layout.spacing=spacing;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=true;return r;
        }
        public static Button Button(Transform parent,string name,string caption,UnityAction clicked,bool primary=false,float height=52)
        {
            var image=Panel(parent,name,primary?UiTheme.Cyan:UiTheme.Panel,true);Tile(image);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.22f,1.25f,1.25f);colors.pressedColor=new Color(.72f,.78f,.8f);
            colors.selectedColor=new Color(1.18f,1.2f,1.1f);colors.disabledColor=new Color(.46f,.52f,.55f,.75f);colors.fadeDuration=.08f;button.colors=colors;
            var edge=image.gameObject.AddComponent<Outline>();edge.effectColor=UiTheme.Edge;edge.effectDistance=new Vector2(1,-1);
            var text=Label(image.transform,"Caption",caption,22,primary?UiTheme.Steel:UiTheme.Text);Fill(text.rectTransform,10);text.alignment=TextAlignmentOptions.Center;
            image.gameObject.AddComponent<UiButtonFeedback>().primary=primary;
            Size(image,height,flexibleWidth:1);if(clicked!=null)button.onClick.AddListener(()=>{UnderwaterLook.Instance?.Click();clicked();});return button;
        }
        public static void Caption(Button button,string value) { var label=button.GetComponentInChildren<TMP_Text>();if(label.text!=value)label.text=value; }
        public static ScrollRect Scroll(Transform parent,string name,out RectTransform content)
        {
            var r=Node(parent,name);var scroll=r.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=36;
            var viewport=Panel(r,"Viewport",new Color(0,0,0,.08f),true);Fill(viewport.rectTransform);viewport.rectTransform.offsetMax=new Vector2(-16,0);viewport.gameObject.AddComponent<RectMask2D>();scroll.viewport=viewport.rectTransform;
            content=Column(viewport.transform,"Content",12,8);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;
            var track=Panel(r,"Vertical scrollbar",UiTheme.Panel,true);track.rectTransform.anchorMin=new Vector2(1,0);track.rectTransform.anchorMax=Vector2.one;track.rectTransform.pivot=new Vector2(1,.5f);track.rectTransform.sizeDelta=new Vector2(8,0);track.rectTransform.anchoredPosition=Vector2.zero;
            var handle=Panel(track.transform,"Handle",UiTheme.Edge,true);Fill(handle.rectTransform);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;return scroll;
        }
        public static Image Bar(Transform parent,string name,Color color,float height=10)
        {
            var track=Panel(parent,name,UiTheme.Steel);Size(track,height);var fill=Panel(track.transform,"Fill",color);Fill(fill.rectTransform);return fill;
        }
        public static void Fraction(Image fill,float value)
        {
            fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(value),1);fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
        }
        public static TMP_InputField Input(Transform parent,string name,string value,string placeholder)
        {
            var image=Panel(parent,name,UiTheme.Panel,true);Size(image,58,flexibleWidth:1);var input=image.gameObject.AddComponent<TMP_InputField>();
            var viewport=Node(image.transform,"Viewport");Fill(viewport,12);viewport.gameObject.AddComponent<RectMask2D>();input.textViewport=viewport;
            var text=(TextMeshProUGUI)Label(viewport,"Text","",24);Fill(text.rectTransform);input.textComponent=text;
            var hint=Label(viewport,"Placeholder",placeholder,24,UiTheme.Muted);Fill(hint.rectTransform);input.placeholder=hint;input.text=value;input.characterLimit=128;
            input.targetGraphic=image;input.customCaretColor=true;input.caretColor=UiTheme.Cyan;return input;
        }
        public static void Set(TMP_Text text,string value) { if(text!=null && text.text!=value)text.text=value; }
        public static void Clear(Transform root) { foreach(Transform child in root){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);} }
    }
}
