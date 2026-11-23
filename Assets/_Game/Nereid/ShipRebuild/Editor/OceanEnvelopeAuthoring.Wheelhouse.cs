using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        static void Wheelhouse()
        {
            var brow=Part("Continuous command glazing plinth",new(0,4.86f,-28.60f),true);
            var shield=Part("Bowed command windscreen",new(0,6.15f,-31.3f),true);
            var seams=Part("Wheelhouse mullions and lights",new(0,6.3f,-31.54f));
            float[] pos={-33.65f,-32.48f,-30.52f,-28.35f,-25.18f,-23.25f,-21.55f};
            float[] radius={.08f,2.89f,5.02f,5.31f,5.42f,5.21f,4.75f};
            float[] height={3.63f,4.59f,5.42f,5.49f,5.50f,5.47f,4.93f};
            foreach(int side in new[]{-1,1})
            {
                Vector3 Belly(float z,float v)
                {
                    float r=Spline(z,pos,radius),h=Spline(z,pos,height);
                    float w=Mathf.Min(W(z)*.993f,r*.99f);
                    float bottom=Upper(side,z,w).y-.16f;
                    float x=Mix(w,r,v)+.28f*Mathf.Sin(v*Mathf.PI);
                    return new(side*x,Mix(bottom,h,v),z);
                }
                foreach(var band in new[]{(0f,.36f,slate),(.36f,.44f,titanium),(.44f,.84f,teal),(.84f,1f,pearl)})
                    brow.Surface(band.Item3,48,6,(u,v)=>Belly(Mix(pos[0],pos[pos.Length-1],u),Mix(band.Item1,band.Item2,v)),(u,v)=>Vector3.right*side);
                brow.Surface(carbon,39,4,(u,v)=>{
                    float z=Mix(-30.61f,-21.55f,u);var outer=Belly(z,1);
                    float inner=Mathf.Min(Mathf.Abs(outer.x)-.001f,4.27f);
                    return Vector3.Lerp(outer,new Vector3(side*inner,outer.y-.11f,z),v);
                },(u,v)=>Vector3.up);
                brow.Surface(pearl,16,9,(u,v)=>{
                    float z=Mix(pos[0],-30.61f,u);var outer=Belly(z,1);
                    return new Vector3(outer.x*v,outer.y+.12f*(1-v*v),z);
                },(u,v)=>Vector3.up);
                // Sloping windscreen transitions into a chamfered rearmost observation bay.
                Vector3 Pane(float t,float h)
                {
                    float x=side*(4.95f+.21f*h)*t;
                    float z=-30.51f-(1-t*t)*(1.20f+.39f*h);
                    float y=Mix(5.43f+.11f*(1-t*t),6.98f+.30f*(1-t*t),h);
                    return new(x,y,z);
                }
                shield.Surface(visor,17,5,Pane,(u,v)=>new Vector3(side*.24f,0,-1));
                foreach(float h in new[]{0f,1f})
                {
                    seams.Ring(titanium,t=>Pane(t,h),h==0?.13f:.116f,26);
                    if(h==0)seams.Ring(amber,u=>Pane(Mix(.57f,.655f,u),h)+Vector3.back*.095f,.032f,6);
                }
                foreach(float col in new[]{.43f,1f})
                    seams.Ring(carbon,h=>Pane(Mathf.Lerp(col,col*.945f+.048f,h),h),col==1f?.13f:.072f,9);
                // A thin cap closes the space between the rear canopy overhang and windscreen sill.
                brow.Surface(slate,24,5,(u,v)=>{
                    var p=Pane(u,1);
                    float fraction=Mathf.Clamp01(Mathf.Abs(p.x)/Spline(-30.51f,CZ,CW));
                    var behind=Crown(side,-30.51f,Mathf.Asin(fraction)/(Mathf.PI*.5f),.24f);
                    return Vector3.Lerp(p,behind,v);
                },(u,v)=>new Vector3(0,.28f,-1));
                for(int i=0;i<2;i++)
                {
                    float zA=i==0?-30.52f:-27.68f,zB=i==0?-27.68f:-24.77f;
                    shield.Surface(visor,12,4,(u,v)=>{
                        float z=Mix(zA,zB,u);var hi=Crown(side,z,1,.15f);var lo=Belly(z,1);
                        var p=Vector3.Lerp(lo,hi,v);p.x+=side*.07f*Mathf.Sin(Mathf.PI*v);return p;
                    },(u,v)=>Vector3.right*side);
                }
            }

            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var collars=Part(hand+" pressure joints and pod intakes",new(side*6,-1,-16));
                foreach(float station in new[]{-32.26f,-16.74f,-7.8f,4.49f})
                {
                    float z=station;
                    // Four widely spaced pressure seams wrap the underside. Keep the window bands open.
                    Vector3 Band(float u,float v,float offset)
                    {
                        float t=Mix(.085f,.96f,v);
                        float curve=z+.49f*Mathf.Sin(Mathf.PI*v);
                        var p=Lower(side,curve+u,t);
                        return p+new Vector3(side*Mathf.Sin(t*Mathf.PI*.5f),-Mathf.Cos(t*Mathf.PI*.5f),0)*offset;
                    }
                    collars.Surface(carbon,4,24,(u,v)=>Band(Mix(-.15f,.15f,u),v,.047f),(u,v)=>new Vector3(side,-.65f,0));
                    collars.Surface(titanium,2,24,(u,v)=>Band(Mix(-.12f,-.057f,u),v,.073f),(u,v)=>new Vector3(side,-.65f,0));
                }
                for(int k=0;k<3;k++)
                {
                    float mid=-31.23f+k*1.03f;
                    Vector3 Pocket(float u,float v,float depth)
                    {
                        float a=Mix(.31f,1.27f,u);
                        float z=mid-.94f*u+Mix(-.12f,.12f,v)*Mathf.Sin(u*Mathf.PI)+.048f*v;
                        return Pod(side,z,a,depth);
                    }
                    collars.Surface(black,12,4,(u,v)=>Pocket(u,v,1.012f),(u,v)=>new Vector3(side,.55f,0));
                    collars.Ring(titanium,u=>Pocket(u,0,1.024f),.042f,12);
                }
            }
        }
    }
}
