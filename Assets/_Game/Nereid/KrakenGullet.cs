using UnityEngine;

namespace Raid.Nereid
{
    internal static class KrakenGullet
    {
        const float End=4.82f;

        internal static Vector3 Center(float depth)
        {
            float t=Mathf.Clamp01(depth/End);
            return new Vector3(1.73f*Mathf.Pow(t,2.48f),-2.58f*Mathf.Pow(t,1.67f),depth);
        }

        internal static Mesh Build()
        {
            const int rings=29,sides=36;
            var positions=new Vector3[(rings+1)*(sides+1)];
            var uv=new Vector2[positions.Length];
            var faces=new int[rings*sides*6];
            int face=0;
            for(int ring=0;ring<=rings;ring++)
            {
                float t=ring/(float)rings;
                float depth=Mathf.Lerp(-.042f,End,t);
                var center=Center(depth);
                float taper=Mathf.Exp(-depth*1.10f);
                float width=.78f+1.03f*taper,height=.71f+.52f*taper;
                for(int side=0;side<=sides;side++)
                {
                    float angle=side/(float)sides*(Mathf.PI*2);
                    float fold=1+.059f*Mathf.Sin(angle*9+depth*3.6f)+.034f*Mathf.Cos(angle*15-depth*2.5f);
                    fold+=.041f*Mathf.Sin(depth*23+Mathf.Sin(angle*6)*1.45f);
                    int i=ring*(sides+1)+side;
                    positions[i]=center+new Vector3(Mathf.Cos(angle)*width*fold,
                        Mathf.Sin(angle)*height*fold,.033f*Mathf.Sin(angle*7+depth*5));
                    uv[i]=new Vector2(side/(float)sides,t);
                    if(ring>=rings || side>=sides)continue;
                    int next=i+sides+1;
                    faces[face++]=i;faces[face++]=next;faces[face++]=i+1;
                    faces[face++]=i+1;faces[face++]=next;faces[face++]=next+1;
                }
            }
            var mesh=new Mesh{name="Kraken gullet · curved interior",vertices=positions,uv=uv,triangles=faces};
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
