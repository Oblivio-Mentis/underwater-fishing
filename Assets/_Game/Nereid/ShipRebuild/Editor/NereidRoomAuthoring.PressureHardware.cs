using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static float FinishRelief(int chip,float u,float v)
        {
            if(chip==13)return 0; // Glazed face of the pressure gauge.
            if(chip==14)return .30f*Mathf.Cos(u*29f*Mathf.PI);
            if(chip==15)
            {
                float rib=Mathf.Max(Mathf.Abs(Mathf.Repeat((u+v)*12,1)-.5f),Mathf.Abs(Mathf.Repeat((u-v)*12,1)-.5f));
                return Mathf.SmoothStep(0,.17f,Mathf.InverseLerp(.36f,.46f,rib));
            }
            float grain=Mathf.PerlinNoise(u*4.1f+chip*5.93f,v*44f);
            float pit=Mathf.PerlinNoise(u*32.2f+chip*3.21f,v*30.6f);
            float pits=chip==4 || chip==5 ? .28f : .15f;
            if(chip==6 || chip==11 || chip==12)pits=.035f;
            return grain*.115f-Mathf.SmoothStep(0,pits,Mathf.InverseLerp(.68f,.85f,pit));
        }

        // Exposed edges on service covers get short, broken paint chips, confined to the actual lip.
        static void AbradedEdge(ShipSectionMesh s,Vector3 a,Vector3 b,Vector3 inward,Vector3 normal,float width)
        {
            Vector3 lift=normal*.0032f;
            for(int i=0;i<3;i++)
            {
                float t=.12f+i*.27f;var first=Vector3.Lerp(a,b,t)+lift;var end=Vector3.Lerp(a,b,t+.09f+i*.008f)+lift;
                s.Face(steel,first,end,Vector3.Lerp(first,end,.73f)+inward*width,Vector3.Lerp(first,end,.48f)+inward*width*.38f);
            }
        }

        // Last three cells in the finish atlas are scale artwork, prismatic safety glass and intake mesh.
        static void InstrumentFinish(int chip,float u,float v,int index,Color32[][] maps)
        {
            Color ink,glow=Color.black;
            float metal,smooth;
            float grain=Mathf.PerlinNoise(u*46f,v*76f)-.5f;
            if(chip==13)
            {
                float dx=u-.5f,dy=v-.5f;
                float r=Mathf.Sqrt(dx*dx+dy*dy);
                float degrees=Mathf.Atan2(dx,dy)*Mathf.Rad2Deg;
                float angle=degrees+142f;
                bool arc=angle>=0 && angle<=284;
                bool warning=angle>233;
                float div=Mathf.Abs(Mathf.Repeat(angle+3.55f,7.10f)-3.55f);
                float major=Mathf.Abs(Mathf.Repeat(angle+17.75f,35.50f)-17.75f);
                bool engraving=arc && ((r>.365f && r<.378f) || (div<1.15f && r>.317f && r<.374f) ||
                    (major<1.65f && r>.263f && r<.374f));
                ink=new Color(.72f,.71f,.60f)*(1+grain*.10f);
                if(engraving)ink=warning?new Color(.50f,.19f,.11f):new Color(.14f,.18f,.17f);
                if(r>.421f)ink=Color.Lerp(ink,new Color(.31f,.32f,.29f),Mathf.InverseLerp(.421f,.465f,r));
                metal=.12f;smooth=.44f;
            }
            else if(chip==14)
            {
                float flutes=.5f+.5f*Mathf.Cos(u*29f*Mathf.PI);
                float rim=Mathf.SmoothStep(.0f,1f,Mathf.Clamp01(8f*Mathf.Min(v,1-v)));
                ink=Color.Lerp(new Color(.51f,.51f,.42f),new Color(.85f,.80f,.63f),flutes*.62f+rim*.28f);
                glow=new Color(.78f,.70f,.53f)*(.26f+.31f*flutes)*rim;
                metal=.07f;smooth=.63f;
            }
            else
            {
                // Expanded steel on the back of the intake; the louvers above it have real depth.
                float a=Mathf.Abs(Mathf.Repeat((u+v)*12,.999f)-.5f);
                float b=Mathf.Abs(Mathf.Repeat((u-v)*12,.999f)-.5f);
                float mesh=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.375f,.465f,Mathf.Max(a,b)));
                ink=Color.Lerp(new Color(.115f,.13f,.13f),new Color(.26f,.285f,.275f),mesh)*(1+grain*.20f);
                metal=.39f;smooth=.26f;
            }
            maps[0][index]=ink;maps[1][index]=new Color(metal,0,0,smooth);maps[2][index]=glow;
        }

        static void SocketBolt(ShipSectionMesh s,Vector3 at,Vector3 u,Vector3 v,float radius)
        {
            var n=Vector3.Cross(u,v).normalized;
            s.Tube(rubber,at-n*.007f,at+n*(radius*.17f),radius*1.14f,-1,10);
            Band(s,at+n*(radius*.16f),u,v,radius,radius*.34f,radius*.22f,steel,deck,15,375,6);
            // The washer backs the open hex socket to read as a recessed head rather than another light.
        }

        static void InstrumentPlate(ShipSectionMesh s,Material baseMat,int chip,Vector3 c,Vector3 u,Vector3 v,
            float width,float height)
        {
            var a=u*(width*.5f);var b=v*(height*.5f);
            s.PrintedFace(baseMat,chip,c,u,v,width*.5f,height*.5f,c-a-b,c+a-b,c+a+b,c-a+b);
        }

        static void HoodedLamp(ShipSectionMesh s,Vector3 at,Vector3 u,Vector3 v,float width,float height)
        {
            Vector3 n=Vector3.Cross(u,v).normalized;
            float depth=height*.45f;
            Casting(s,at,u,v,Shield(width,height,.55f),depth,dark,deck,hull,.93f);
            var c=at+n*(depth+.006f);
            InstrumentPlate(s,warm,14,c,u,v,width*.73f,height*.59f);
            for(int i=-1;i<=1;i++)
            {
                var x=c+u*(width*i*.24f);
                s.Tube(deck,x-v*(height*.37f)+n*.016f,x+v*(height*.37f)+n*.016f,height*.039f,-1,6);
            }
            // A casting shades the upper lip. Three replaceable steel bars protect the ribbed glass.
            s.Beam(hull,c+v*(height*.35f)-u*(width*.39f)+n*.023f,c+v*(height*.35f)+u*(width*.39f)+n*.023f,height*.24f,height*.13f);
            SocketBolt(s,c-u*(width*.426f),u,v,height*.085f);
            SocketBolt(s,c+u*(width*.426f),u,v,height*.085f);
        }

        static void BourdonGauge(ShipSectionMesh s,Vector3 c,Vector3 u,Vector3 v,float r,float degrees)
        {
            Vector3 n=Vector3.Cross(u,v).normalized;
            Band(s,c,u,v,r,r*.73f,r*.17f,deck,dark,0,360,28);
            Band(s,c+n*(r*.17f),u,v,r*.96f,r*.76f,r*.065f,steel,deck,0,360,28);
            var center=c+n*(r*.175f);var ring=new Vector3[28];
            for(int i=0;i<ring.Length;i++)
            {
                float a=i*2f*Mathf.PI/ring.Length;
                ring[i]=center+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*r*.78f;
            }
            s.PrintedFace(ceramic,13,center,u,v,r*.78f,r*.78f,ring);
            float angle=degrees*Mathf.Deg2Rad;
            var pointer=u*Mathf.Sin(angle)+v*Mathf.Cos(angle);
            var side=Vector3.Cross(n,pointer);
            var axis=center+n*(r*.016f);
            s.Face(rubber,axis+pointer*r*.63f,axis+side*r*.042f-pointer*r*.15f,axis-side*r*.042f-pointer*r*.15f);
            s.Tube(steel,axis,axis+n*(r*.018f),r*.058f,-1,10);
        }

        static void Intake(ShipSectionMesh s,Vector3 at,Vector3 u,Vector3 v,float w,float h,int vanes)
        {
            Vector3 n=Vector3.Cross(u,v).normalized;
            Casting(s,at,u,v,Shield(w,h,.25f),.07f,deck,hull,dark,.97f);
            var face=at+n*.075f;
            InstrumentPlate(s,rubber,15,face,u,v,w*.91f,h*.86f);
            for(int i=0;i<vanes;i++)
            {
                float y=h*(.35f-(i+.5f)*.70f/vanes);float height=h*.33f/vanes;
                var c=face+v*y+n*.015f;var left=c-u*(w*.43f);var right=c+u*(w*.43f);
                s.Face(hull,left-v*height,right-v*height,right+v*height+n*.046f,left+v*height+n*.046f);
                s.Face(steel,left+v*height+n*.046f,right+v*height+n*.046f,right+v*height+n*.054f,left+v*height+n*.054f);
            }
        }
    }
}
