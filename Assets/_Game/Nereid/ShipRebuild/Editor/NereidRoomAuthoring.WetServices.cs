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
                    jacket.Beam(cyan,Vector3.Lerp(high,top,.19f)-Vector3.up*.09f,Vector3.Lerp(high,top,.78f)-Vector3.up*.09f,.052f,.041f);
                }
            }
            var control=Part("Ballast pressure compensator",new(8.43f,1.33f,-15.43f));
            Vector3 u=Vector3.right,v=Vector3.up,n=Vector3.forward;
            var at=new Vector3(8.50f,1.34f,-15.46f);
            Casting(control,at,u,v,Shield(2.50f,.97f,.64f),.15f,ceramic,steel,hull,.90f);
            var head=at+new Vector3(-.49f,.015f,.16f);
            Lens(control,head,u,v,.36f,glass);
            for(int j=0;j<3;j++)Band(control,head+n*.115f,u,v,.31f,.259f,.018f,j<2?cyan:ochre,dark,10+j*100,91+j*100,7);
            control.Beam(warm,head+n*.15f,head+n*.15f+new Vector3(.12f,.13f,0),.026f,.022f);
            var valve=at+new Vector3(.49f,.02f,.16f);
            Casting(control,valve,u,v,Shield(.75f,.63f,.72f),.05f,hull,enamel,dark);
            for(int j=0;j<2;j++)
                Casting(control,valve+new Vector3(0,.12f-j*.24f,.059f),u,v,Shield(.49f,.115f,.75f),.035f,j==0?cyan:steel,deck,dark);
            control.Lamp("Pressure gauge wash",new Color(.49f,.80f,.88f),at+new Vector3(-.34f,.32f,.46f),6,4.6f);
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
            Band(shell,center,u,v,.296f,.23f,.030f,steel,dark);
            var track=Part("Bunk reading lamp and pressure controls",new(5.40f,1.12f,3.06f));
            Lens(track,center,u,v,.192f,warm);
            for(int i=-1;i<=1;i+=2)
            {
                var p=back+u*(i*.63f)+v*.19f+n*.27f;
                Casting(track,p,u,v,Shield(.28f,.36f,.62f),.038f,dark,steel,ceramic);
                Casting(track,p+n*.04f,u,v,Shield(.17f,.17f,.82f),.018f,i<0?cyan:ochre,dark,dark);
            }
            track.Lamp("Bunk reading pool",new Color(1,.69f,.43f),a+new Vector3(-.55f,.09f,-.24f),10,4.3f);
        }
    }
}

