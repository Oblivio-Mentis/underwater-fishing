using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static void FitTransfer()
        {
            var mill=Part("Twin induction mill jacket",new(-2.47f,2.0f,-13.02f));
            Vector3 x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var carrier=new Vector3(-2.47f,2.08f,-13.43f);
            var shield=new[]{new Vector2(-2.01f,-.72f),new Vector2(-1.52f,-.98f),new Vector2(-.54f,-.90f),
                new Vector2(.1f,-.60f),new Vector2(1.64f,-.67f),new Vector2(2.14f,-.20f),
                new Vector2(2.14f,.56f),new Vector2(1.78f,.84f),new Vector2(-1.67f,.84f),new Vector2(-2.10f,.52f)};
            Casting(mill,carrier,x,y,shield,.40f,enamel,steel,dark,.92f);
            for(int j=0;j<2;j++)
            {
                var center=carrier+new Vector3(-.94f+j*1.83f,.11f,.385f);
                Band(mill,center,x,y,.82f,.64f,.16f,dark,steel);
                Band(mill,center+z*.15f,x,y,.73f,.515f,.11f,steel,dark);
                for(int k=0;k<6;k++)
                {
                    float a=k*60;
                    Band(mill,center+z*.255f,x,y,.67f,.57f,.028f,k%2==0?warm:ochre,dark,a+6,a+49,5);
                }
                var hub=center+z*.17f;
                for(int k=0;k<6;k++)
                {
                    float rad=k*Mathf.PI/3;var u=new Vector3(Mathf.Cos(rad),Mathf.Sin(rad),0);var v=Vector3.Cross(z,u);
                    var blade=new[]{new Vector2(.13f,-.02f),new Vector2(.43f,-.20f),new Vector2(.51f,-.04f),new Vector2(.24f,.13f)};
                    Casting(mill,hub,u,v,blade,.065f,deck,steel,dark);
                }
                Lens(mill,hub+z*.064f,x,y,.177f,ochre);
            }
            for(int j=0;j<2;j++)
            {
                float sx=j==0?-1:1;
                var top=carrier+new Vector3(sx*1.68f,.80f,-.01f);
                mill.Beam(dark,top,top+new Vector3(sx*.23f,.58f,-.37f),.42f,.51f);
                mill.Beam(ochre,top+z*.11f,top+new Vector3(sx*.22f,.46f,-.06f),.22f,.26f);
            }
            mill.Lamp("Furnace return",new Color(1,.59f,.34f),carrier+new Vector3(.21f,.30f,1.08f),25,6.6f);
            var hood=Part("Cyclone extraction cowl",new(-2.59f,3.29f,-11.05f));
            var cap=new Vector3(-2.57f,3.49f,-11.19f);
            Casting(hood,cap,x,z,Shield(4.67f,1.27f,.65f),.32f,ceramic,steel,enamel,.81f);
            Casting(hood,cap+Vector3.down*.33f,x,z,Shield(3.26f,.66f,.62f),.085f,dark,ochre,rubber);
            foreach(float dx in new[]{-1.04f,0,1.04f})
                Casting(hood,cap+new Vector3(dx,-.422f,0),x,z,Shield(.68f,.25f,.6f),.018f,warm,dark,dark);

            var winch=Part("Suspended twin drum drive",new(2.28f,2.94f,-14.30f));
            var drum=new Vector3(2.03f,2.88f,-14.75f);
            winch.Tube(dark,drum-x*1.31f,drum+x*1.31f,.39f,-1,24);
            for(int i=0;i<7;i++)
            {
                float a=-.96f+i*.31f;
                winch.Tube(i==0||i==6?ochre:deck,drum+x*a,drum+x*(a+.19f),i==0||i==6?.49f:.408f,-1,24);
            }
            foreach(int side in new[]{-1,1})
            {
                var anchor=drum+x*(side*1.34f);
                winch.Tube(steel,anchor,anchor+x*side*.17f,.48f,-1,16);
                winch.Beam(enamel,anchor-Vector3.forward*.13f,anchor+new Vector3(side*.37f,.68f,-.45f),.45f,.38f);
                winch.Beam(ochre,anchor+Vector3.forward*.16f,anchor+new Vector3(side*.33f,.62f,.34f),.24f,.29f);
            }
            Casting(winch,drum+new Vector3(0,.45f,.14f),x,z,Shield(3.94f,1.65f,.54f),.13f,hull,steel,dark,.91f);

            var frame=Part("Cargo gantry apron",new(2.58f,2.67f,-15.88f));
            var panel=new Vector3(3.69f,1.92f,-15.90f);
            Casting(frame,panel,x,y,Shield(3.78f,2.32f,.47f),.22f,ceramic,deck,dark,.93f);
            Casting(frame,panel+new Vector3(-.30f,0,.225f),x,y,Shield(2.50f,1.55f,.55f),.09f,dark,steel,enamel,.94f);
            for(int j=0;j<3;j++)
            {
                var chev=new[]{new Vector2(-.38f,-.47f),new Vector2(-.15f,-.47f),new Vector2(.31f,0),
                    new Vector2(-.15f,.47f),new Vector2(-.38f,.47f),new Vector2(.06f,0)};
                // Split the concave mark into two solid prismatic fins.
                var at=panel+new Vector3(-1.00f+j*.64f,0,.33f);
                Casting(frame,at,x,y,new[]{chev[0],chev[1],chev[2],chev[5]},.035f,ochre,dark,dark);
                Casting(frame,at,x,y,new[]{chev[5],chev[2],chev[3],chev[4]},.035f,j==2?warm:ochre,dark,dark);
            }
            Lens(frame,panel+new Vector3(1.30f,.33f,.23f),x,y,.25f,cyan);
            CastSmallSignals(frame,panel+new Vector3(1.31f,-.28f,.24f),x,y);
            frame.Lamp("Cargo signal bounce",new Color(.88f,.74f,.54f),panel+new Vector3(-.44f,.50f,.93f),19,7);
        }

        static void CastSmallSignals(ShipSectionMesh s,Vector3 p,Vector3 u,Vector3 v)
        {
            for(int k=0;k<2;k++)Casting(s,p+v*(k*-.22f),u,v,Shield(.47f,.105f,.6f),.045f,k==0?cyan:deck,steel,dark);
        }

        static void FitContainment()
        {
            var brace=Part("Parasite radial restraint",new(0,2.05f,7.73f));
            Vector3 center=new(0,1.55f,7.87f),x=Vector3.left,y=Vector3.up,n=Vector3.back;
            // Separate clamps flank the living jaws. The central mouth and supply pipe remain visible from the doorway.
            for(int side=-1;side<=1;side+=2)
            {
                float a=side<0?24:103,b=side<0?77:156;
                Band(brace,center,x*1.20f,y,2.53f,2.26f,.28f,dark,steel,a,b,12);
                Band(brace,center+n*.28f,x*1.20f,y,2.48f,2.32f,.10f,ceramic,hull,a+2,b-2,12);
                Band(brace,center+n*.381f,x*1.20f,y,2.405f,2.35f,.030f,alarm,dark,a+7,b-7,10);
                var u=Vector3.right*side;
                var bearing=center+u*2.96f+Vector3.up*.20f+n*.18f;
                Casting(brace,bearing,x,y,Shield(1.04f,1.65f,.72f),.30f,hull,ceramic,dark,.88f);
                Lens(brace,bearing+n*.315f,x,y,.39f,alarm);
                var shoulder=bearing+u*1.65f+Vector3.up*.98f;
                brace.Tube(dark,bearing+u*.48f,shoulder,.19f,-1,16);
                brace.Tube(steel,bearing+u*.49f,bearing+u*.94f+Vector3.up*.24f,.135f,-1,16);
                brace.Tube(ochre,bearing+u*.87f+Vector3.up*.29f,shoulder,.245f,.19f,16);
                var baffle=new Vector3(side*4.72f,2.75f,7.80f);
                Casting(brace,baffle,x,y,Shield(1.88f,1.44f,.70f),.18f,enamel,steel,dark);
                for(int k=0;k<3;k++)
                    Casting(brace,baffle+new Vector3(0,.31f-k*.32f,-.185f),x,y,Shield(1.07f-k*.10f,.12f,.8f),.055f,k==0?alarm:rubber,deck,dark);
            }
            var crown=Part("Containment bifurcated vault",new(0,4.07f,6.37f));
            for(int side=-1;side<=1;side+=2)
            {
                var p=new Vector3(side*3.68f,4.32f,6.10f);
                Casting(crown,p,Vector3.right,Vector3.forward,Shield(3.55f,2.15f,.8f),.27f,dark,ceramic,enamel,.86f);
                Band(crown,p+Vector3.down*.28f,Vector3.right*1.31f,Vector3.forward,.62f,.43f,.13f,steel,dark,15,339,24);
                Lens(crown,p+Vector3.down*.28f,Vector3.right,Vector3.forward,.37f,alarm);
                crown.Beam(steel,new(side*.86f,4.19f,7.80f),new(side*2.57f,4.19f,6.64f),.14f,.15f);
            }
            crown.Lamp("Containment reflected task light",new Color(1,.52f,.39f),new Vector3(-2.70f,3.06f,6.20f),20,7);
        }

        static void FitEngineering()
        {
            var engine=Part("Service trunk thermal manifold",new(.79f,2.89f,-20.74f));
            Vector3 x=Vector3.right,z=Vector3.forward;
            var p=new Vector3(1.16f,3.40f,-20.81f);
            Casting(engine,p,x,z,Shield(3.55f,6.72f,.82f),.20f,enamel,steel,dark,.94f);
            var core=p+Vector3.down*.21f;
            for(int i=-1;i<=1;i++)
            {
                var cap=core+z*(i*1.87f);
                Casting(engine,cap,x,z,Shield(2.95f,1.52f,.64f),.13f,ceramic,steel,dark,.90f);
                var c=cap+Vector3.down*.135f;
                Band(engine,c,x,z,.51f,.32f,.065f,dark,steel,-42,296,22);
                Lens(engine,c,x,z,.27f,i==0?warm:glass);
                for(int side=-1;side<=1;side+=2)
                    Casting(engine,c+x*(side*.94f),x,z,Shield(.37f,.59f,.6f),.030f,side<0?warm:steel,enamel,dark);
            }
            var riser=Part("Engine room upper cooling bank",new(1.04f,2.70f,-24.65f));
            p=new Vector3(1.10f,2.61f,-24.77f);
            Casting(riser,p,x,Vector3.up,Shield(4.57f,1.08f,.62f),.22f,ceramic,steel,dark,.90f);
            for(int i=-1;i<=1;i++)
            {
                var face=p+new Vector3(i*1.09f,0,.23f);
                Casting(riser,face,x,Vector3.up,Shield(.84f,.56f,.51f),.045f,hull,steel,dark);
                for(int k=0;k<2;k++)
                    Casting(riser,face+new Vector3(0,.09f-.19f*k,.05f),x,Vector3.up,Shield(.56f,.072f),.031f,k==0?rubber:ochre,deck,dark);
            }
            riser.Lamp("Service task bounce",new Color(.97f,.77f,.53f),p+new Vector3(-.47f,.18f,.83f),21,8);
        }
    }
}

