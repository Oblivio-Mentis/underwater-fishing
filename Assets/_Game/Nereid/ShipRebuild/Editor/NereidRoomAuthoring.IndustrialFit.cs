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
                Band(mill,center,x,y,.82f,.52f,.16f,hull,deck,0,360,12);
                var hub=center+z*.17f;
                for(int k=0;k<4;k++)
                {
                    float rad=k*Mathf.PI*.5f;var u=new Vector3(Mathf.Cos(rad),Mathf.Sin(rad),0);var v=Vector3.Cross(z,u);
                    var blade=new[]{new Vector2(.13f,-.02f),new Vector2(.43f,-.20f),new Vector2(.51f,-.04f),new Vector2(.24f,.13f)};
                    Casting(mill,hub,u,v,blade,.065f,deck,steel,dark);
                }
                SocketBolt(mill,hub+z*.074f,x,y,.161f);
                var rail=center+z*.29f;
                mill.Tube(deck,rail-x*.56f-y*.31f,rail+x*.56f+y*.31f,.046f,-1,6);
            }
            for(int j=0;j<2;j++)
            {
                float sx=j==0?-1:1;
                var top=carrier+new Vector3(sx*1.68f,.80f,-.01f);
                mill.Beam(dark,top,top+new Vector3(sx*.23f,.58f,-.37f),.42f,.51f);
                mill.Beam(ochre,top+z*.11f,top+new Vector3(sx*.22f,.46f,-.06f),.22f,.26f);
            }
            HoodedLamp(mill,carrier+new Vector3(.09f,.70f,.42f),x,y,.66f,.22f);
            mill.Lamp("Furnace return",new Color(.90f,.83f,.74f),carrier+new Vector3(.12f,1.06f,.91f),7.5f,6.2f);
            var hood=Part("Cyclone extraction cowl",new(-2.59f,3.29f,-11.05f));
            var cap=new Vector3(-2.57f,3.49f,-11.19f);
            Casting(hood,cap,x,z,Shield(4.67f,1.27f,.65f),.32f,ceramic,steel,enamel,.81f);
            Casting(hood,cap+Vector3.down*.33f,x,z,Shield(3.26f,.66f,.62f),.085f,dark,ochre,rubber);
            foreach(float dx in new[]{-1.02f,1.02f})
                HoodedLamp(hood,cap+new Vector3(dx,-.422f,0),x,z,.66f,.23f);
            Intake(hood,cap+Vector3.down*.423f,x,z,.76f,.52f);

            var winch=Part("Suspended twin drum drive",new(2.28f,2.94f,-14.30f));
            var drum=new Vector3(2.03f,2.88f,-14.75f);
            winch.Tube(dark,drum-x*1.31f,drum+x*1.31f,.39f,-1,12);
            for(int i=0;i<3;i++)
            {
                float a=-1.03f+i*.97f;
                winch.Tube(i==1?enamel:deck,drum+x*a,drum+x*(a+.19f),i==1?.41f:.49f,-1,12);
            }
            foreach(int side in new[]{-1,1})
            {
                var anchor=drum+x*(side*1.34f);
                winch.Tube(steel,anchor,anchor+x*side*.17f,.48f,-1,10);
                winch.Beam(enamel,anchor-Vector3.forward*.13f,anchor+new Vector3(side*.37f,.68f,-.45f),.45f,.38f);
                winch.Beam(ochre,anchor+Vector3.forward*.16f,anchor+new Vector3(side*.33f,.62f,.34f),.24f,.29f);
            }
            Casting(winch,drum+new Vector3(0,.45f,.14f),x,z,Shield(3.94f,1.65f,.54f),.13f,hull,steel,dark,.91f);

            var frame=Part("Cargo gantry apron",new(2.58f,2.67f,-15.88f));
            var panel=new Vector3(3.69f,1.92f,-15.90f);
            Casting(frame,panel,x,y,Shield(3.78f,2.32f,.47f),.22f,ceramic,deck,dark,.93f);
            Casting(frame,panel+new Vector3(-.30f,0,.225f),x,y,Shield(2.50f,1.55f,.55f),.09f,dark,steel,enamel,.94f);
            Intake(frame,panel+new Vector3(-.54f,-.12f,.33f),x,y,1.68f,.85f);
            for(int side=-1;side<=1;side+=2)
            {
                SocketBolt(frame,panel+new Vector3(side*1.39f,.78f,.24f),x,y,.082f);
            }
            BourdonGauge(frame,panel+new Vector3(.96f,.21f,.24f),x,y,.31f,34);
            HoodedLamp(frame,panel+new Vector3(.94f,-.46f,.24f),x,y,.61f,.20f);
            frame.Lamp("Cargo signal bounce",new Color(.84f,.81f,.71f),panel+new Vector3(.89f,-.32f,.93f),9f,6.5f);
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
                var u=Vector3.right*side;
                var bearing=center+u*2.96f+Vector3.up*.20f+n*.18f;
                Casting(brace,bearing,x,y,Shield(1.04f,1.65f,.72f),.30f,hull,ceramic,dark,.88f);
                var boss=bearing+n*.315f;
                SocketBolt(brace,boss,x,y,.23f);
                HoodedLamp(brace,boss-y*.47f,x,y,.48f,.18f);
                var shoulder=bearing+u*1.65f+Vector3.up*.98f;
                brace.Tube(dark,bearing+u*.48f,shoulder,.19f,-1,8);
                brace.Tube(steel,bearing+u*.49f,bearing+u*.94f+Vector3.up*.24f,.135f,-1,8);
                brace.Tube(ochre,bearing+u*.87f+Vector3.up*.29f,shoulder,.245f,.19f,8);
                var baffle=new Vector3(side*4.72f,2.75f,7.80f);
                Casting(brace,baffle,x,y,Shield(1.88f,1.44f,.70f),.18f,enamel,steel,dark);
                Intake(brace,baffle+n*.19f,x,y,1.08f,.62f);
            }
            var crown=Part("Containment bifurcated vault",new(0,4.07f,6.37f));
            for(int side=-1;side<=1;side+=2)
            {
                var p=new Vector3(side*3.68f,4.32f,6.10f);
                Casting(crown,p,Vector3.right,Vector3.forward,Shield(3.55f,2.15f,.8f),.27f,dark,ceramic,enamel,.86f);
                Band(crown,p+Vector3.down*.28f,Vector3.right*1.31f,Vector3.forward,.62f,.43f,.13f,steel,dark,15,339,24);
                HoodedLamp(crown,p+Vector3.down*.30f,Vector3.right,Vector3.forward,.57f,.21f);
                crown.Beam(steel,new(side*.86f,4.19f,7.80f),new(side*2.57f,4.19f,6.64f),.14f,.15f);
            }
            crown.Lamp("Containment reflected task light",new Color(.92f,.71f,.56f),new Vector3(-3.27f,3.55f,6.20f),10,7);
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
                BourdonGauge(engine,c,x,z,.265f,24+i*29);
                for(int side=-1;side<=1;side+=2)
                    if(side<0 && i==0)HoodedLamp(engine,c+x*(side*.94f),x,z,.34f,.40f);
                    else Intake(engine,c+x*(side*.94f),x,z,.40f,.58f);
            }
            var riser=Part("Engine room upper cooling bank",new(1.04f,2.70f,-24.65f));
            p=new Vector3(1.10f,2.61f,-24.77f);
            Casting(riser,p,x,Vector3.up,Shield(4.57f,1.08f,.62f),.22f,ceramic,steel,dark,.90f);
            for(int i=-1;i<=1;i++)
            {
                var face=p+new Vector3(i*1.09f,0,.23f);
                Casting(riser,face,x,Vector3.up,Shield(.84f,.56f,.51f),.045f,hull,steel,dark);
                Intake(riser,face+z*.053f,x,Vector3.up,.68f,.38f);
            }
            HoodedLamp(riser,p+new Vector3(.21f,-.43f,.23f),x,Vector3.up,.73f,.19f);
            riser.Lamp("Service task bounce",new Color(.93f,.83f,.69f),p+new Vector3(.21f,-.38f,.87f),11,8);
        }
    }
}

