using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // The silhouette is independent of the font. Left and right are always shown from the player's perspective.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HudInputGlyph : MaskableGraphic
    {
        internal enum Control { None, Left, LiftLeft, Right, Wheel, E, F, Q, R, One, G, H, J }
        Control control;
        TMP_Text legend;
        static readonly Color Face = new(.045f,.105f,.13f,.98f);
        static readonly Color Ink = new(.80f,.91f,.92f,1);
        static readonly Vector2[] Mouse = {
            new(-.44f,.10f),new(-.40f,.32f),new(-.24f,.46f),new(.24f,.46f),
            new(.40f,.32f),new(.44f,.10f),new(.42f,-.26f),new(.27f,-.42f),
            new(.09f,-.48f),new(-.09f,-.48f),new(-.27f,-.42f),new(-.42f,-.26f)
        };
        static readonly Vector2[] Key = {
            new(-.48f,.25f),new(-.37f,.39f),new(.37f,.39f),new(.48f,.25f),
            new(.48f,-.25f),new(.37f,-.39f),new(-.37f,-.39f),new(-.48f,-.25f)
        };
        internal void Initialize()
        {
            raycastTarget=false;
            legend=UiKit.Label(transform,"Key",string.Empty,18,Ink);
            legend.fontStyle=FontStyles.Bold;legend.alignment=TextAlignmentOptions.Center;
            UiKit.Fill(legend.rectTransform);
        }
        internal void Set(Control value)
        {
            if(control==value)return;
            control=value;SetVerticesDirty();
            bool key=value!=Control.Left && value!=Control.LiftLeft && value!=Control.Wheel && value!=Control.Right && value!=Control.None;
            legend.enabled=key;
            if(key)UiKit.Set(legend,value==Control.One?"1":value.ToString());
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(control==Control.None)return;
            bool mouse=control is Control.Left or Control.LiftLeft or Control.Right or Control.Wheel;
            var points=mouse?Mouse:Key;
            var rect=GetPixelAdjustedRect();
            float size=Mathf.Min(rect.width,rect.height);
            Vector2 centre=rect.center;
            Fill(vh,points,centre,size,Ink);
            Fill(vh,points,centre,size*.84f,Face);
            if(!mouse)return;
            if(control==Control.Wheel)
            {
                int wheel=vh.currentVertCount;
                void Tip(float x,float y)=>vh.AddVert(centre+new Vector2(x,y)*size,UiTheme.Cyan,Vector2.zero);
                Tip(-.07f,.29f);Tip(.07f,.29f);Tip(.07f,.04f);Tip(-.07f,.04f);
                vh.AddTriangle(wheel,wheel+1,wheel+2);vh.AddTriangle(wheel,wheel+2,wheel+3);
                return;
            }
            float side=control==Control.Right?1:-1;
            if(control==Control.LiftLeft)
            {
                int arrow=vh.currentVertCount;
                void Lift(float x,float y)=>vh.AddVert(centre+new Vector2(x,y)*size,UiTheme.Amber,Vector2.zero);
                Lift(-.44f,.22f);Lift(-.23f,.53f);Lift(-.02f,.22f);
                vh.AddTriangle(arrow,arrow+1,arrow+2);
                Lift(-.29f,.24f);Lift(-.17f,.24f);Lift(-.17f,.05f);Lift(-.29f,.05f);
                vh.AddTriangle(arrow+3,arrow+4,arrow+5);vh.AddTriangle(arrow+3,arrow+5,arrow+6);
                return;
            }
            int start=vh.currentVertCount;
            void Point(float x,float y)=>vh.AddVert(centre+new Vector2(x*side,y)*size,UiTheme.Cyan,Vector2.zero);
            Point(.055f,.045f);Point(.055f,.37f);Point(.215f,.37f);Point(.325f,.265f);Point(.36f,.045f);
            for(int i=1;i<4;i++)vh.AddTriangle(start,start+i,start+i+1);
        }
        static void Fill(VertexHelper vh,Vector2[] points,Vector2 centre,float size,Color tint)
        {
            int first=vh.currentVertCount;
            vh.AddVert(centre,tint,Vector2.zero);
            foreach(var p in points)vh.AddVert(centre+p*size,tint,Vector2.zero);
            for(int i=0;i<points.Length;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%points.Length);
        }
    }
}