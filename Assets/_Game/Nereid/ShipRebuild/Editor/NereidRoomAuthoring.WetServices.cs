using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static void FitLock()
        {
            // The working chamber is 8.65 world metres long, with the inner door at local x 5.95
            // and the outer seal at 10.275. The mechanism and its control pads occupy both ends.
            var jacket=Part("Flood lock pressure spines",new(8.12f,1.16f,-14));
            foreach(float x in new[]{6.72f,8.69f,9.60f})
            {
                Vector3 centre=new(x,1.11f,-14);
                foreach(int side in new[]{-1,1})
                {
                    var p=centre+new Vector3(0,.42f,side*1.43f);
                    var high=centre+new Vector3(0,.83f,side*1.16f);
                    var top=centre+new Vector3(0,.88f,side*.57f);
                    jacket.Beam(dark,p,high,.20f,.22f);
                    jacket.Beam(steel,high,top,.18f,.17f);
                    jacket.Beam(enamel,p-Vector3.up*.41f,p,.20f,.12f);
                    if(x==8.69f)
                        HoodedLamp(jacket,Vector3.Lerp(high,top,.50f)-Vector3.up*.10f,Vector3.right,Vector3.forward,.45f,.18f);
                }
            }
            var control=Part("Ballast pressure compensator",new(8.43f,1.33f,-15.43f));
            Vector3 u=Vector3.right,v=Vector3.up,n=Vector3.forward;
            var at=new Vector3(8.50f,1.34f,-15.46f);
            Casting(control,at,u,v,Shield(2.50f,.97f,.64f),.15f,ceramic,steel,hull,.90f);
            var head=at+new Vector3(-.49f,.015f,.16f);
            BourdonGauge(control,head,u,v,.37f,-31);
            var valve=at+new Vector3(.48f,.03f,.16f);
            Casting(control,valve,u,v,Shield(.77f,.64f,.37f),.055f,hull,enamel,dark);
            var arm=valve+n*.070f;
            SocketBolt(control,arm,u,v,.108f);
            var lever=arm+n*.060f;
            control.Beam(deck,lever,lever-u*.165f+v*.17f,.066f,.07f);
            control.Tube(ochre,lever-u*.165f+v*.17f,lever-u*.165f+v*.17f+n*.09f,.059f,-1,9);
            control.Lamp("Pressure gauge wash",new Color(.82f,.86f,.81f),at+new Vector3(-.38f,.51f,.65f),2.8f,4.2f);

        }

        static void FitBerth()
        {
            Vector3 a=new(5.53f,1.12f,3.04f),u=Vector3.forward,v=Vector3.up,n=Vector3.left;
            var shell=Part("Companion shelter lamellae",a);
            // Shell panels overlap from the rear and terminate above the companion's resting area.
            // In front of the bunk the entire width remains open to the companion and the player.
            var back=new Vector3(5.69f,1.00f,3.06f);
            var outline=new[]{new Vector2(-.96f,-.52f),new Vector2(.88f,-.52f),new Vector2(1.06f,-.24f),new Vector2(.97f,.46f),
                new Vector2(.63f,.70f),new Vector2(-.55f,.70f),new Vector2(-1.02f,.36f)};
            Casting(shell,back,u,v,outline,.19f,enamel,ceramic,steel,.90f);
            Casting(shell,back+n*.20f,u,v,Shield(1.66f,.82f,.95f),.06f,water,dark,enamel,.91f);
            for(int k=-1;k<=1;k++)
            {
                var p=a+new Vector3(-.26f,.62f-Mathf.Abs(k)*.075f,k*.53f);
                var wing=new[]{new Vector2(-.56f,-.20f),new Vector2(-.30f,-.35f),new Vector2(.46f,-.32f),
                    new Vector2(.67f,-.02f),new Vector2(.40f,.31f),new Vector2(-.34f,.31f),new Vector2(-.63f,.11f)};
                // The crown slopes toward the closed rear, creating a warm shell-shaped grotto.
                Casting(shell,p,new Vector3(1,.25f,0).normalized,Vector3.forward,wing,.17f,k==0?ceramic:enamel,steel,dark,.82f);
            }
            var center=back+n*.265f+v*.13f;
            var track=Part("Bunk reading lamp and pressure controls",new(5.40f,1.12f,3.06f));
            // The bunk is a wall-mounted field berth: a caged sleeper light above an upholstery pad
            // and an offset service vent, cushioned face exposed to the animal.
            Intake(track,center-u*.47f+v*.10f,u,v,.44f,.39f);
            HoodedLamp(track,center+u*.30f+v*.27f,u,v,.57f,.21f);
            var pad=center+u*.17f-v*.11f;
            Casting(track,pad,u,v,Shield(.97f,.32f,.36f),.057f,rubber,deck,water,.94f);
            track.Lamp("Bunk reading pool",new Color(.95f,.82f,.63f),a+new Vector3(-.65f,.34f,.35f),3.4f,3.7f);

        }
    }
}

