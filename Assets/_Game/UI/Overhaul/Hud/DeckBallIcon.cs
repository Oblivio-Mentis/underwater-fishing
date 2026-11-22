using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // Cross-sections of the orange ball and its woven turquoise bands.
    internal sealed class DeckBallIcon : MaskableGraphic
    {
        static readonly float[] Rows={-1,-.65f,-.53f,-.43f,.43f,.53f,.65f,1};
        static readonly Color Orange=new(1f,.42f,.13f), Cream=new(1,.90f,.65f), Teal=new(.11f,.54f,.58f);
        static readonly Color[] Bands={Orange,Cream,Teal,Orange,Teal,Cream,Orange};
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect=GetPixelAdjustedRect();
            float radius=Mathf.Min(rect.width,rect.height)*.44f;
            for(int band=0;band<Bands.Length;band++)
            {
                for(int j=0;j<4;j++)
                {
                    float lo=Mathf.Lerp(Rows[band],Rows[band+1],j/4f);
                    float hi=Mathf.Lerp(Rows[band],Rows[band+1],(j+1)/4f);
                    int start=mesh.currentVertCount;
                    for(int k=0;k<=14;k++)
                    {
                        float u=k/7f-1;
                        AddVertex(mesh,rect.center,radius,u,lo,Bands[band]);
                        AddVertex(mesh,rect.center,radius,u,hi,Bands[band]);
                        if(k==0)continue;
                        int a=start+2*k-2;
                        mesh.AddTriangle(a,a+1,a+3);
                        mesh.AddTriangle(a,a+3,a+2);
                    }
                }
            }
        }

        static void AddVertex(VertexHelper mesh,Vector2 center,float radius,float u,float y,Color pigment)
        {
            float x=u*Mathf.Sqrt(Mathf.Max(0,1-y*y));
            float z=Mathf.Sqrt(Mathf.Max(0,1-x*x-y*y));
            var position=new Vector2(x-.25f*y,.25f*x+y)*(.97f*radius);
            position.y-=z*(1-y*y)*.15f*radius;
            float light=.66f+.34f*Mathf.Clamp01(-.45f*x+.55f*y+z);
            mesh.AddVert(center+position,new Color(pigment.r*light,pigment.g*light,pigment.b*light,1),Vector2.zero);
        }
    }
}
