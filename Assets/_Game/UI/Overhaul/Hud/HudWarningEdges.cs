using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // The gradient is confined to the outer 3% of each side. It leaves the entire aiming area transparent.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HudWarningEdges : MaskableGraphic
    {
        float direction;
        public void Set(Color tint,float strength,float side)
        {
            tint.a=strength;
            if(Mathf.Abs(direction-side)>.01f){direction=side;SetVerticesDirty();}
            if(color!=tint)color=tint;
            canvasRenderer.cullTransparentMesh=true;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            float reach=Mathf.Min(r.width*.034f,54f);
            void Strip(float x,float toward,float weight)
            {
                int start=vh.currentVertCount;
                for(int step=0;step<5;step++)
                {
                    float t=step*.25f;
                    float y=Mathf.Lerp(r.yMin,r.yMax,t);
                    float k=Mathf.Sin(Mathf.PI*t);
                    var edge=color;edge.a*=weight*k;var clear=edge;clear.a=0;
                    vh.AddVert(new Vector2(x,y),edge,Vector2.zero);
                    vh.AddVert(new Vector2(x+toward*reach*k,y),clear,Vector2.zero);
                    if(step==0)continue;
                    int i=start+2*step;vh.AddTriangle(i-2,i-1,i);vh.AddTriangle(i-1,i+1,i);
                }
            }
            Strip(r.xMin,1,1-.76f*Mathf.Max(0,direction));
            Strip(r.xMax,-1,1-.76f*Mathf.Max(0,-direction));
        }
    }
}