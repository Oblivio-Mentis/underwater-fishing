using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class OceanEnvelopeAuthoring
    {
        static readonly float[] CZ={-32.67f,-31.78f,-30.48f,-28.7f,-26.1f,-24.72f,-22.50f,-19.15f,-16.25f,-14.70f};
        static readonly float[] CW={.20f,3.32f,5.25f,5.89f,5.93f,5.70f,5.40f,4.88f,3.93f,.38f};
        static readonly float[] CR={7.61f,7.42f,6.98f,6.96f,7.02f,6.95f,5.90f,4.93f,4.70f,4.88f};
        static readonly float[] CT={7.75f,8.06f,8.43f,8.79f,8.63f,8.36f,7.92f,6.69f,5.49f,4.99f};
        static Vector3 Crown(int side,float z,float t,float drop=0)
        {
            float w=Section(z,CZ,CW),rim=Section(z,CZ,CR),top=Section(z,CZ,CT);
            float a=t*Mathf.PI*.50f;
            return new(side*w*Mathf.Sin(a),Mix(rim,top,Mathf.Cos(a))-drop,z);
        }
        static void CommandSaddle()
        {
            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var cap=Part(hand+" command visor",new(side*2,7.1f,-25),true);
                foreach(var band in new[]{(0f,.41f,slate),(.41f,.83f,pearl),(.83f,.89f,orange),(.89f,1f,titanium)})
                    foreach(var span in Bays(CZ[0],CZ[CZ.Length-1],CZ))cap.Surface(band.Item3,1,band.Item1==0?2:1,(u,v)=>Crown(side,Mix(span.start,span.end,u),Mix(band.Item1,band.Item2,v)),(u,v)=>new Vector3(side*.5f,1,0));
                foreach(var span in Bays(CZ[0],CZ[CZ.Length-1],CZ))cap.Surface(black,1,5,(u,v)=>Crown(side,Mix(span.start,span.end,u),v,.18f),(u,v)=>Vector3.down);
                foreach(var span in Bays(CZ[0],CZ[CZ.Length-1],CZ))cap.Surface(teal,1,1,(u,v)=>Crown(side,Mix(span.start,span.end,u),1,.18f*v),(u,v)=>Vector3.right*side);
                cap.Ring(black,u=>Crown(side,Mix(-31.95f,-22.1f,u),1,.12f),.033f,7);
                var saddle=Part(hand+" pilothouse flying buttress",new(side*5.1f,5.30f,-21.8f),true);
                // A glazed bridge floats over the pressure capsule. Aft of its glazing the saddle
                // closes and slopes continuously into the main deck. The port companionway is inside the crown.
                Vector3 S(float z,float t)
                {
                    var high=Crown(side,z,1,.17f);float xHigh=Mathf.Abs(high.x);
                    float lowX=Mathf.Min(W(z)*.95f,xHigh+.24f);
                    var low=Upper(side,z,lowX);
                    float bulge=.15f*Mathf.Sin(t*Mathf.PI);
                    return Vector3.Lerp(low,high,t)+Vector3.right*side*bulge;
                }
                saddle.Surface(slate,6,2,(u,v)=>S(Mix(-24.76f,-14.70f,u),v),(u,v)=>Vector3.right*side);
                saddle.Surface(pearl,5,1,(u,v)=>{
                    float z=Mix(-24.34f,-16.30f,u),shape=Mathf.Sin(u*Mathf.PI);
                    float t=Mix(.28f+.29f*(1-u),.28f+.29f*(1-u)+.28f*shape,v);
                    return S(z,t)+Vector3.right*(.035f*side);
                },(u,v)=>Vector3.right*side);
                // Recessed window surrounds on the sides; glazing itself is part of the navigable bridge.
                var windows=Part(hand+" bridge window recess",new(side*5.02f,6.3f,-27.8f),true);
                windows.Surface(black,5,1,(u,v)=>{
                    float z=Mix(-30.54f,-24.76f,u);var outer=Crown(side,z,1,.17f);
                    return Vector3.Lerp(outer,new Vector3(side*4.38f,6.86f,z),v);
                },(u,v)=>Vector3.down);
                windows.Surface(teal,5,1,(u,v)=>{
                    float z=Mix(-30.54f,-24.76f,u);var a=Upper(side,z,Mathf.Min(W(z),4.38f));
                    return Vector3.Lerp(a,new Vector3(side*4.41f,5.47f,z),v);
                },(u,v)=>Vector3.right*side);
                foreach(float station in new[]{-30.44f,-24.81f})
                {
                    var top=Crown(side,station,1,.13f);
                    var foot=new Vector3(side*4.48f,5.25f,station+.13f);
                    windows.Ring(slate,t=>Vector3.Lerp(foot,top,t),.13f,1);
                }
                var flank=Part(hand+" swept pressure armor",new(side*6,1.50f,-25),true);
                FlankPlates(flank,side);
            }
        }

        static readonly float[] PZ={-38.65f,-37.9f,-36.50f,-34.15f,-30.40f,-25.55f,-21.90f,-19.70f,-18.98f};
        static readonly float[] PX={8.45f,8.47f,8.50f,8.50f,8.40f,7.96f,7.24f,6.66f,6.51f};
        static readonly float[] PY={.05f,.08f,.12f,.16f,.14f,-.27f,-.69f,-.57f,-.49f};
        static readonly float[] RX={1.34f,1.57f,1.64f,1.70f,1.59f,1.18f,.79f,.35f,.035f};
        static readonly float[] RY={1.38f,1.56f,1.71f,1.72f,1.62f,1.28f,.92f,.39f,.040f};
        static Vector3 Pod(int side,float z,float angle,float scale=1)
        {
            const float step=Mathf.PI/6;
            float index=angle/step;
            float a=Mathf.Floor(index)*step,b=a+step,t=index-Mathf.Floor(index);
            float x=Mix(Mathf.Sin(a),Mathf.Sin(b),t);
            float y=Mix(Mathf.Cos(a),Mathf.Cos(b),t);
            return new(side*(Section(z,PZ,PX)+x*Section(z,PZ,RX)*scale), Section(z,PZ,PY)+y*Section(z,PZ,RY)*scale,z);
        }

        static void Propulsion()
        {
            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var pod=Part(hand+" vector pump nacelle",new(side*8.0f,.1f,-30.07f),true);
                Material[] jackets={pearl,pearl,slate,teal,carbon,carbon,carbon,carbon,slate,teal,orange,pearl};
                for(int i=0;i<jackets.Length;i++)
                {
                    float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;
                    foreach(var span in Bays(PZ[0],PZ[PZ.Length-1],PZ))
                        pod.Surface(jackets[i],1,1,(u,v)=>Pod(side,Mix(span.start,span.end,u),Mix(a,b,v)),
                            (u,v)=>new Vector3(side*Mathf.Sin(Mix(a,b,v)),Mathf.Cos(Mix(a,b,v)),0));
                }
                // The aft nozzle is hollow. The inner shroud, five rotor blades, and centre fairing
                // are separate surfaces. A single diffused locator sits inside the nozzle lip.
                var nozzle=Part(hand+" annular impeller",new(side*8.45f,.05f,-37.96f),true);
                float lip=PZ[0];
                nozzle.Surface(titanium,1,12,(u,v)=>Pod(side,lip,Mathf.PI*2*v,Mix(1,.82f,u)),(u,v)=>Vector3.back);
                nozzle.Surface(black,2,12,(u,v)=>Pod(side,Mix(lip,-36.90f,u),Mathf.PI*2*v,.81f),
                    (u,v)=>new Vector3(-side*Mathf.Sin(Mathf.PI*2*v),-Mathf.Cos(Mathf.PI*2*v),0));
                nozzle.Ring(titanium,v=>Pod(side,lip+.09f,Mathf.PI*2*v,.83f),.039f,12);
                const float rotorZ=-38.24f;
                Vector3 Centre(float z)=>new(side*Section(z,PZ,PX),Section(z,PZ,PY),z);
                nozzle.Surface(slate,3,10,(u,v)=>Centre(Mix(rotorZ-.56f,rotorZ+.38f,u))+
                    new Vector3(side*Mathf.Sin(v*2*Mathf.PI),Mathf.Cos(v*2*Mathf.PI),0)*(.052f+.27f*Mathf.Sin(Mathf.PI*u)),
                    (u,v)=>new Vector3(side*Mathf.Sin(v*2*Mathf.PI),Mathf.Cos(v*2*Mathf.PI),-.1f));
                for(int blade=0;blade<5;blade++)
                {
                    float start=blade*Mathf.PI*.4f;
                    Vector3 Blade(float r,float t,float aft)
                    {
                        float angle=start+.72f*r+.45f*t;
                        float radius=.27f+.91f*r;
                        return Centre(rotorZ)+new Vector3(side*Mathf.Sin(angle)*radius,Mathf.Cos(angle)*radius,
                            -.15f+.26f*t+.16f*r+aft);
                    }
                    nozzle.Surface(titanium,2,1,(u,v)=>Blade(u,v,0),(u,v)=>Vector3.back);
                    nozzle.Surface(orange,1,1,(u,v)=>Blade(Mix(.63f,.87f,u),Mix(.36f,.46f,v),-.008f),(u,v)=>Vector3.back);
                    nozzle.Surface(teal,2,1,(u,v)=>Blade(u,v,.07f),(u,v)=>Vector3.forward);
                }

                var wing=Part(hand+" swept hydroplane",new(side*6.7f,-.5f,-27),true);
                Vector3 Foil(float u,float v,bool top)
                {
                    // A swept, cambered hydrofoil; rib chord tapers toward the nacelle.
                    float x=Mix(4.45f,9.25f,u);
                    float front=Mix(-18.89f,-30.78f,u)+1.72f*Mathf.Sin(u*Mathf.PI);
                    float back=Mix(-33.80f,-35.49f,u);
                    float z=Mix(front,back,v);
                    float h=Mix(-.45f,.10f,u)+.27f*Mathf.Sin(u*Mathf.PI);
                    float thick=(.16f+.73f*(1-u))*Mathf.Pow(Mathf.Max(.0001f,Mathf.Sin(Mathf.PI*v)),.58f);
                    return new(side*x,h+(top?thick:-thick*.65f),z);
                }
                foreach(var band in new[]{(0f,.105f,titanium),(.105f,.19f,orange),(.19f,.70f,slate),(.70f,.74f,teal),(.74f,1f,carbon)})
                    wing.Surface(band.Item3,4,1,(u,v)=>Foil(u,Mix(band.Item1,band.Item2,v),true),(u,v)=>Vector3.up);
                wing.Surface(carbon,4,4,(u,v)=>Foil(u,v,false),(u,v)=>Vector3.down);
                // Distinct wingtip fins extend the planform beyond the drive pods.
                Vector3 Tip(float u,float v,bool top)
                {
                    float x=Mix(8.71f,11.20f,u),h=Mix(-.14f,.88f,u);
                    float z=Mix(Mix(-29.30f,-35.12f,u),Mix(-35.15f,-35.93f,u),v);
                    return new(side*x,h+(top?1:-.65f)*(.03f+.23f*(1-u))*Mathf.Sin(Mathf.PI*v),z);
                }
                wing.Surface(pearl,2,2,(u,v)=>Tip(u,v,true),(u,v)=>Vector3.up);
                wing.Surface(slate,2,2,(u,v)=>Tip(u,v,false),(u,v)=>Vector3.down);
                wing.Surface(orange,1,2,(u,v)=>Tip(Mix(.79f,.91f,u),v,true)+Vector3.up*.012f,(u,v)=>Vector3.up);
                pod.Ring(amber,t=>Pod(side,Mix(-34.92f,-34.32f,t),1.34f,1.016f),.042f,1);
            }
        }
    }
}
