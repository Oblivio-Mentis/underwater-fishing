using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static void FitObservation()
        {
            foreach(int side in new[]{-1,1})
            {
                var wall=Part((side<0?"Port":"Starboard")+" panoramic pressure castings",new(side*5.59f,2.60f,-2));
                Vector3 u=Vector3.forward*side,v=Vector3.up,n=-Vector3.right*side;
                foreach(float z in new[]{-6.02f,-1.81f,2.31f})
                {
                    // Glazed openings are framed by broad sloping side-jambs and separately keyed sills.
                    var brow=new Vector3(side*5.78f,3.72f,z);
                    var p=new[]{new Vector2(-2.04f,-.24f),new(-1.64f,-.48f),new(-.91f,-.48f),new(-.60f,-.28f),new(1.48f,-.28f),
                        new(2.04f,.06f),new(1.93f,.47f),new(-1.87f,.47f)};
                    Casting(wall,brow,u,v,p,.37f,ceramic,enamel,dark,.91f);
                    HoodedLamp(wall,brow+n*.38f+u*.25f+v*.04f,u,v,.85f,.28f);
                    foreach(float offset in new[]{-1.31f,1.36f})
                        SocketBolt(wall,brow+n*.375f+u*offset+v*.12f,u,v,.073f);
                    var foot=new Vector3(side*5.76f,.45f,z);
                    Casting(wall,foot,u,v,Shield(3.66f,.58f,.62f),.20f,enamel,steel,dark,.95f);
                    for(int j=-1;j<=1;j+=2)
                    {
                        var shoulder=brow+u*(j*1.76f);
                        var rib=ShoulderOutline();
                        Casting(wall,shoulder,n,v,rib,.23f,dark,steel,enamel);
                        wall.Beam(steel,foot+u*(j*1.73f)+n*.06f+v*.30f,foot+u*(j*1.95f)+n*.05f+v*1.22f,.087f,.14f);
                    }
                }
                // A pair of hollow optical saddles arches around the roof lift, clear of the coaming.
                var spine=Part((side<0?"Port":"Starboard")+" lift well armature",new(side*3.64f,4.03f,-2.6f));
                foreach(float z in new[]{-6.76f,1.91f})
                {
                    var a=new Vector3(side*5.60f,3.63f,z+.12f);
                    var b=new Vector3(side*3.74f,4.35f,z-.02f);
                    var c=new Vector3(side*1.35f,4.34f,z+.01f);
                    spine.Beam(dark,a,b,.49f,.42f);spine.Beam(ceramic,a+Vector3.down*.08f,b+Vector3.down*.12f,.31f,.31f);
                    spine.Beam(enamel,b,c,.43f,.33f);spine.Beam(steel,b-Vector3.up*.085f,c-Vector3.up*.12f,.235f,.23f);
                    var yoke=Vector3.Lerp(b,c,.46f)-Vector3.up*.235f;
                    Casting(spine,yoke,Vector3.right,Vector3.forward,Shield(.32f,.41f),.048f,deck,steel,dark);
                    SocketBolt(spine,yoke+Vector3.down*.05f,Vector3.right,Vector3.forward,.071f);
                }
            }
            var o=Part("Bathymetric lens housing",new(0,4.18f,1.92f));
            var pos=new Vector3(0,4.40f,1.92f);var x=Vector3.right;var zAxis=Vector3.forward;
            Band(o,pos,x*1.25f,zAxis,1.14f,.91f,.21f,ceramic,dark,0,360,36);
            Band(o,pos+Vector3.down*.19f,x*1.25f,zAxis,.965f,.665f,.16f,steel,enamel,0,360,32);
            for(int i=0;i<4;i++)
            {
                float a=i*90;
                Band(o,pos+Vector3.down*.355f,x*1.25f,zAxis,.89f,.79f,.029f,deck,hull,a+6,a+68,7);
            }
            Lens(o,pos+Vector3.down*.24f,x*1.25f,zAxis,.60f,glass);
            for(int side=-1;side<=1;side+=2)
                HoodedLamp(o,pos+new Vector3(side*.98f,-.37f,0),zAxis,Vector3.left,.45f,.185f);
            o.Lamp("Reservoir optical bounce",new Color(.76f,.85f,.84f),pos+Vector3.down*.68f,15f,10f);
        }

        static Vector2[] ShoulderOutline()
        {
            // General cast knee seen from the adjacent bay: the return tapers as it reaches inward.
            return new[]{new Vector2(-.03f,-.75f),new Vector2(.22f,-.67f),new Vector2(.49f,-.27f),
                new Vector2(1.16f,.31f),new Vector2(1.20f,.49f),new Vector2(.52f,.49f),new Vector2(-.03f,.10f)};
        }

        static void FitBridge()
        {
            var nav=Part("Navigator suspended sighting ring",new(0,6.99f,-28.08f));
            Vector3 p=new(0,7.27f,-28.10f);var x=Vector3.right;var z=Vector3.forward;
            // Recessed gimbal above eye level, with a continuous latitude scale and split outer bearing.
            Band(nav,p,x*1.31f,z,1.23f,.90f,.16f,dark,steel);
            Band(nav,p-Vector3.up*.14f,x*1.31f,z,1.09f,.84f,.075f,steel,dark);
            Band(nav,p-Vector3.up*.20f,x*1.31f,z,.85f,.74f,.045f,glass,steel);
            for(int k=0;k<3;k++)
            {
                float a=k*120f;
                Band(nav,p-Vector3.up*.22f,x*1.31f,z,1.04f,.972f,.028f,deck,dark,a+11,a+106,11);
            }
            Lens(nav,p-Vector3.up*.17f,x,z,.49f,glass);
            for(int side=-1;side<=1;side+=2)
            {
                HoodedLamp(nav,p+new Vector3(side*1.21f,-.20f,0),z,Vector3.left,.62f,.21f);
                SocketBolt(nav,p+new Vector3(side*.68f,-.22f,.66f),x,z,.08f);
            }
            nav.Lamp("Chart canopy indirect light",new Color(.78f,.84f,.83f),p-Vector3.up*.58f,10,8);
            foreach(int side in new[]{-1,1})
            {
                var console=Part((side<0?"Port":"Starboard")+" navigation binnacle",new(side*3.09f,6.5f,-29.22f));
                var u=Vector3.forward*side;var v=Vector3.up;
                var panel=new Vector3(side*4.11f,6.94f,-27.55f);
                Casting(console,panel,u,v,Shield(4.80f,.58f,.66f),.33f,ceramic,steel,enamel,.94f);
                var top=new Vector3(side*2.45f,7.31f,-27.55f);
                var crown=new[]{new Vector2(-.78f,-1.54f),new Vector2(.34f,-2.07f),new Vector2(.72f,-1.72f),
                    new Vector2(.78f,1.73f),new Vector2(.15f,2.08f),new Vector2(-.76f,1.47f)};
                Casting(console,top,Vector3.right,Vector3.forward,crown,.30f,ceramic,steel,dark,.86f);
                Casting(console,top+Vector3.down*.302f,Vector3.right,Vector3.forward,Shield(.60f,2.27f,.52f),.012f,glass,hull,dark);
                for(int k=0;k<3;k++)
                {
                    var a=top+Vector3.down*.336f+Vector3.forward*(-.69f+k*.68f);
                    Casting(console,a,x,z,Shield(.36f,.26f,.55f),.024f,k==1?cyan:enamel,steel,dark);
                }
                var wing=new Vector3(side*3.59f,5.55f,-29.76f);
                console.Beam(dark,wing+Vector3.up*.29f,wing+Vector3.right*side*.30f+Vector3.down*.45f,.42f,.30f);
                var dial=new Vector3(side*3.27f,5.66f,-29.42f);var slope=new Vector3(0,.72f,-.69f);
                Casting(console,dial,x,slope,Shield(1.38f,.62f,.55f),.095f,dark,ceramic,hull);
                for(int j=0;j<2;j++)
                {
                    var at=dial+Vector3.right*(j==0?-.33f:.33f)+Vector3.Cross(x,slope).normalized*.111f;
                    BourdonGauge(console,at,x,slope,j==0?.21f:.15f,j==0?-37:62);
                }
            }
            var bulk=Part("Bridge vector plot",new(2.06f,6.32f,-24.99f));
            var chart=new Vector3(1.92f,6.30f,-24.88f);var right=Vector3.left;var up=Vector3.up;
            Casting(bulk,chart,right,up,Shield(3.38f,1.67f,.55f),.17f,hull,ceramic,steel,.94f);
            var plot=chart+Vector3.back*.18f;
            for(int i=0;i<3;i++)Band(bulk,plot+Vector3.right*.42f,right,up,.26f+i*.18f,.249f+i*.18f,.013f,i==1?cyan:steel,dark,18,315,27);
            bulk.Beam(ochre,plot+new Vector3(.72f,-.05f,-.035f),plot+new Vector3(-.34f,.38f,-.035f),.025f,.028f);
            for(int i=0;i<3;i++)Casting(bulk,plot+new Vector3(-.96f,.30f-i*.25f,-.04f),right,up,Shield(.53f-i*.09f,.08f,.5f),.02f,i==0?cyan:steel,dark,dark);
        }
    }
}

