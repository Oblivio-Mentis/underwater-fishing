using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class WatchRoomAuthoring
    {
        static void Bridge()
        {
            foreach(int side in new[]{-1,1})
            {
                string hand=side<0?"Port":"Starboard";
                var pos=new Vector3(side*3.15f,4.85f,-27.98f);
                var chair=Group(hand+" helmsman's chair",pos);
                WatchChair(chair,pos,new Vector3(-side*.12f,0,-1),true);

                // The existing binnacle owns the two round instruments. This sidecar only
                // occupies the inboard edge of the desk and keeps the panoramic glass clear.
                var c=new Vector3(side*2.37f,4.85f,-29.36f);
                var console=Group(hand+" engine telegraph",c);
                console.Box(frame,c+new Vector3(side*.02f,.36f,0),new(.40f,.70f,.51f),default,.12f);
                console.Box(enamel,c+Vector3.up*.70f,new(.69f,.18f,.64f),default,.12f);
                var top=c+new Vector3(0,.82f,.02f);
                var slope=Quaternion.Euler(-10,0,0);
                console.Box(ink,top,new(.62f,.085f,.58f),slope,.095f);
                console.Box(ice,top+new Vector3(side*.14f,.073f,-.11f),new(.19f,.06f,.082f),slope);
                Vector3 axis=c+new Vector3(-side*.10f,.94f,.02f);
                console.Tube(frame,axis-Vector3.right*.09f,axis+Vector3.right*.09f,.08f,-1,8);
                console.Beam(metal,axis,axis+new Vector3(0,.17f,.08f),.062f,.071f);
                console.Tube(ochre,axis+new Vector3(-.084f,.17f,.08f),axis+new Vector3(.084f,.17f,.08f),.06f,-1,8);
                console.Collision(c+Vector3.up*.36f,new(.63f,.72f,.61f));

                var sill=Group(hand+" navigator foot locker",new(side*1.25f,5.10f,-30.00f));
                var locker=new Vector3(side*1.35f,5.09f,-30.06f);
                sill.Box(enamel,locker,new(2.15f,.42f,.47f),default,.105f);
                sill.Box(metal,locker+Vector3.up*.22f,new(2.18f,.064f,.49f),default,.09f);
                foreach(float offset in new[]{-.53f,.53f})
                {
                    var drawer=locker+new Vector3(offset,.018f,.24f);
                    Cut(sill,drawer,Vector3.right,Vector3.up,Outline(.96f,.27f,.045f),.051f,shell,frame);
                    sill.Box(frame,drawer+new Vector3(0,.052f,.041f),new(.32f,.061f,.045f));
                }
                sill.Collision(locker,new(2.16f,.44f,.48f));
            }

            var c0=new Vector3(1.74f,4.85f,-25.43f);
            var table=Group("Bathymetric plotting chest",c0);
            foreach(float x in new[]{-1.35f,1.35f})
            {
                var p=c0+new Vector3(x,.41f,.025f);
                Cut(table,p,Vector3.forward,Vector3.up,new Vector2[]{new(-.42f,-.39f),new(.30f,-.39f),
                    new(.36f,-.30f),new(.36f,.39f),new(-.44f,.39f),new(-.44f,.26f),new(-.29f,.11f),new(-.29f,-.27f),new(-.42f,-.31f)},.22f,enamel,frame);
            }
            table.Box(frame,c0+new Vector3(0,.37f,.19f),new(2.72f,.55f,.29f),default,.09f);
            table.Box(shell,c0+Vector3.up*.83f,new(3.25f,.14f,.91f),default,.13f);
            table.Box(ink,c0+Vector3.up*.912f,new(3.08f,.054f,.80f),default,.12f);
            table.Box(ochre,c0+new Vector3(0,.82f,-.468f),new(1.02f,.067f,.033f));
            table.Collision(c0+new Vector3(0,.42f,0),new(3.22f,.84f,.90f));

            var topo=Group("Relief sounding chart",c0+Vector3.up*1.02f);
            // Two opposing canyon walls, raised stepped bathymetry and a physical heading pointer.
            Vector2[] shelf={new(-1.14f,-.24f),new(-.36f,-.27f),new(.10f,-.13f),new(.43f,-.24f),
                new(.65f,-.12f),new(.52f,-.06f),new(.06f,.014f),new(-.36f,-.10f),new(-.78f,.045f),new(-1.14f,0)};
            Vector2[] ridge={new(-.59f,.25f),new(-.52f,.12f),new(-.18f,.15f),new(.20f,.11f),new(.62f,.04f),
                new(1.17f,-.064f),new(1.30f,.085f),new(1.08f,.255f),new(.63f,.24f),new(.23f,.28f)};
            var basePoint=c0+new Vector3(-.035f,.959f,0);
            Cut(topo,basePoint,Vector3.right,Vector3.forward,shelf,.053f,enamel,shell);
            Cut(topo,basePoint,Vector3.right,Vector3.forward,ridge,.053f,enamel,shell);
            Cut(topo,basePoint+new Vector3(-.14f,.045f,-.037f),Vector3.right*.80f,Vector3.forward*.72f,shelf,.047f,ice,metal);
            Cut(topo,basePoint+new Vector3(.15f,.042f,.055f),Vector3.right*.73f,Vector3.forward*.58f,ridge,.044f,ice,metal);
            Vector2[] arrow={new(-.11f,-.042f),new(.04f,-.04f),new(.04f,-.085f),new(.16f,0),new(.04f,.085f),new(.04f,.041f),new(-.11f,.045f)};
            Cut(topo,basePoint+new Vector3(.03f,.045f,.021f),Vector3.right,Vector3.forward,arrow,.035f,ochre,frame);
            foreach(float offset in new[]{-1.40f,1.39f})
            {
                var p=c0+new Vector3(offset,1.04f,.24f);
                topo.Box(frame,p,new(.19f,.23f,.16f),default,.044f);
                topo.Box(lamp,p+new Vector3(0,.057f,-.085f),new(.12f,.085f,.035f));
            }
            topo.Lamp("Chart reading light",new(1,.82f,.61f),c0+new Vector3(.26f,1.37f,-.20f),3f,2.2f);
            var illuminator=topo.node.GetComponentInChildren<Light>();
            illuminator.type=LightType.Spot;illuminator.innerSpotAngle=38;illuminator.spotAngle=78;
            illuminator.transform.localRotation=Quaternion.LookRotation(new Vector3(-.14f,-1,.18f));

            // This library is below the existing overhead navigation binnacle, next to the plot.
            var archive=Group("Bridge sounding tape library",new(3.90f,5.56f,-26.30f));
            var box=new Vector3(3.91f,5.46f,-26.28f);
            archive.Box(enamel,box,new(.56f,1.17f,1.21f),default,.13f);
            archive.Box(metal,box+Vector3.up*.60f,new(.58f,.10f,1.23f),default,.10f);
            var face=box+Vector3.left*.285f;
            archive.Box(frame,face,new(.06f,.84f,1.06f),default,.06f);
            for(int i=0;i<3;i++)
            {
                var p=face+new Vector3(-.044f,.005f,-.34f+i*.34f);
                archive.Box(i==1?ink:shell,p,new(.085f,.69f,.275f),default,.055f);
                archive.Box(i==1?ochre:ice,p+new Vector3(-.053f,.15f,0),new(.035f,.088f,.17f));
                archive.Box(frame,p+new Vector3(-.055f,-.16f,0),new(.051f,.20f,.095f));
            }
            archive.Collision(box,new(.57f,1.18f,1.23f));
        }
    }
}