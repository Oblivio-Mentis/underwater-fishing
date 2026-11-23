using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        static void NavigationDetails()
        {
            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var optics=Part(hand+" observation lighting",new(side*6.98f,.5f,-1));
                // A broad navigation line remains legible in the trench haze, under the real observation windows.
                optics.Ring(black,u=>Side(side,Mix(-7.68f,7.57f,u),.36f)+Vector3.right*side*.086f,.147f,9);
                foreach(float station in new[]{-6.66f,5.35f})
                    optics.Ring(amber,u=>Side(side,station+.56f*u,.36f)+Vector3.right*side*.206f,.056f,1);
                foreach(float z in new[]{-6.53f,5.49f})
                {
                    Vector3 at=Lower(side,z,.89f);
                    Vector3 facing=new Vector3(side*.81f,-.56f,.13f).normalized;
                    Vector3 across=Vector3.Cross(facing,Vector3.up).normalized;
                    Vector3 up=Vector3.Cross(across,facing);
                    Vector3 Rim(float t)=>across*.72f*Mathf.Cos(t*2*Mathf.PI)+up*.29f*Mathf.Sin(t*2*Mathf.PI);
                    optics.Surface(black,1,8,(u,v)=>at+Rim(v)*Mix(1,.78f,u)+facing*(u*.19f),
                        (u,v)=>Rim(v)+facing*.5f);
                    optics.Surface(amber,1,8,(u,v)=>at+Rim(v)*(.77f*u)+facing*(.19f+.065f*(1-u)),
                        (u,v)=>facing);
                    var light=new GameObject("Downward work beam").AddComponent<Light>();
                    light.transform.SetParent(optics.node,false);light.transform.localPosition=at+facing*.29f-optics.node.localPosition;
                    light.transform.localRotation=Quaternion.LookRotation(facing);light.type=LightType.Spot;
                    light.spotAngle=54;light.innerSpotAngle=26;light.range=24;light.color=new(1,.77f,.52f);
                    light.intensity=180;light.shadows=LightShadows.None;
                }
                PaintMark(side);
            }
            AirlockSkin();
            var aerial=Part("Folded command sensor blade",new(0,8.49f,-27.48f),true);
            Vector3 Blade(int side,float u,float v)
            {
                float x=side*(.14f+.34f*(1-u))*Mathf.Sin(Mathf.PI*v);
                float z=Mix(Mix(-25.24f,-28.09f,u),Mix(-29.10f,-29.35f,u),v);
                return new Vector3(x,Mix(8.46f,10.36f,u)-.16f*(v-.5f)*(v-.5f),z);
            }
            foreach(int side in new[]{-1,1})foreach(var band in new[]{(0f,.62f,carbon),(.62f,.83f,pearl),(.83f,.93f,orange),(.93f,1f,carbon)})
                aerial.Surface(band.Item3,1,3,(u,v)=>Blade(side,Mix(band.Item1,band.Item2,u),v),(u,v)=>Vector3.right*side);
            aerial.Ring(amber,t=>Blade(1,.98f,Mix(.49f,.63f,t)),.046f,1);
        }



        static void AirlockSkin()
        {
            var collar=Part("Starboard faired boarding collar",new(8.15f,1.07f,-14),true);
            Vector3 Shell(float u,float angle,float scale)
            {
                float a=angle*2*Mathf.PI;
                float z=-14+Mathf.Cos(a)*Mix(3.06f,1.94f,u)*scale;
                float y=Mix(1.10f,1.00f,u)+Mathf.Sin(a)*Mix(2.42f,1.66f,u)*scale;
                float x=Mix(6.34f,10.30f,u)+.13f*Mathf.Sin(u*Mathf.PI);
                return new Vector3(x,y,z);
            }
            foreach(var band in new[]{(0f,.13f,slate),(.13f,.65f,pearl),(.65f,.72f,orange),(.72f,.93f,slate),(.93f,1f,titanium)})
                collar.Surface(band.Item3,2,12,(u,v)=>Shell(Mix(band.Item1,band.Item2,u),v,1),
                    (u,v)=>new Vector3(.18f,Mathf.Sin(v*2*Mathf.PI),Mathf.Cos(v*2*Mathf.PI)));
            collar.Surface(teal,1,12,(u,v)=>Shell(1,v,Mix(1,.91f,u)),(u,v)=>Vector3.right);
            foreach(float start in new[]{.053f,.39f})collar.Ring(amber,v=>Shell(.91f,start+.051f*v,1.022f),.045f,2);
        }

        static void PaintMark(int side)
        {
            var mark=Part((side<0?"Port":"Starboard")+" enamel designation",new(side*7,1.74f,-10));
            // Project the stencil onto the pressure shell, retaining its curvature and the player's perspective.
            Vector3 At(float x,float h,float offset)
            {
                float z=-10.04f+side*x;
                return Side(side,z,1.75f+h)+Vector3.right*side*offset;
            }
            mark.Surface(slate,3,1,(u,v)=>At(Mix(-1.86f,1.86f,u),Mix(-.48f,.48f,v),.025f),(u,v)=>Vector3.right*side);
            void Stroke(float x,float y,float a,float b,float width,Material mat)
            {
                Vector2 tangent=new Vector2(a-x,b-y).normalized;
                Vector2 d=new Vector2(-tangent.y,tangent.x)*width*.5f;
                mark.Quad(mat,At(x+d.x,y+d.y,.041f),At(a+d.x,b+d.y,.041f),
                    At(a-d.x,b-d.y,.041f),At(x-d.x,y-d.y,.041f),Vector3.right*side);
            }
            float cell=.52f;
            for(int i=0;i<6;i++)
            {
                float k=-1.36f+i*cell;
                void Line(float x,float y,float a,float b)=>Stroke(k+x*.34f,(y-.5f)*.54f,k+a*.34f,(b-.5f)*.54f,.071f,pearl);
                char letter="NEREID"[i];
                if(letter=='I'){Line(.5f,0,.5f,1);continue;}
                Line(0,0,0,1);
                if(letter=='N'){Line(0,1,1,0);Line(1,0,1,1);}
                if(letter=='E'){Line(0,1,1,1);Line(0,.52f,.77f,.52f);Line(0,0,1,0);}
                if(letter=='R'){Line(0,1,.82f,1);Line(.82f,1,1,.84f);Line(1,.84f,1,.63f);Line(1,.63f,.80f,.49f);Line(.80f,.49f,0,.49f);Line(.48f,.49f,1,0);}
                if(letter=='D'){Line(0,1,.65f,1);Line(.65f,1,1,.74f);Line(1,.74f,1,.26f);Line(1,.26f,.65f,0);Line(.65f,0,0,0);}
            }
            Stroke(-1.68f,-.27f,-1.50f,.27f,.092f,orange);
        }
    }
}
