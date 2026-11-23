using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        // Transverse loft stations. The raised stern crown contains the command deck, then
        // sweeps down over the companionway to the transfer hall. No closed shell behind glazing.
        readonly struct Frame
        {
            internal readonly float z,width,low,high,sill,brow;
            internal Frame(float z,float width,float low,float high,float sill,float brow)
            {this.z=z;this.width=width;this.low=low;this.high=high;this.sill=sill;this.brow=brow;}
            internal Vector3[] Profile(float side) => new Vector3[]{
                new(0,low,z),new(side*width*.59f,low+.21f,z),new(side*width*.91f,low+.89f,z),
                new(side*width,sill,z),new(side*width,brow,z),new(side*width*.91f,high-.32f,z),
                new(side*width*.75f,high,z),new(0,high+.06f,z)};
        }
        static readonly Frame[] frames={
            new(-34.4f,1.90f,.85f,3.78f,1.72f,3.05f),
            new(-32.65f,3.97f,-.48f,6.18f,3.72f,5.18f),
            new(-30.52f,5.17f,-1.42f,7.72f,5.11f,6.96f),
            new(-27.60f,5.48f,-1.95f,7.90f,5.12f,7.02f),
            new(-24.76f,5.55f,-2.20f,7.72f,4.91f,6.93f),
            new(-21.2f,5.65f,-2.40f,6.48f,.51f,4.88f),
            new(-17.04f,6.15f,-2.65f,4.82f,.52f,3.63f),
            new(-15.75f,6.23f,-2.80f,4.82f,-.13f,3.60f),
            new(-12.28f,6.32f,-2.88f,4.82f,-.13f,3.60f),
            new(-8.06f,6.36f,-3.12f,5.00f,.50f,3.82f),
            new(-5.46f,6.38f,-3.25f,5.02f,.50f,3.83f),
            new(-.54f,6.38f,-3.25f,5.02f,.50f,3.83f),
            new(4.52f,6.26f,-2.86f,4.99f,.52f,3.85f),
            new(8.12f,6.15f,-2.24f,4.90f,.53f,3.85f),
            new(9.20f,4.65f,-1.98f,4.43f,.35f,3.45f),
            new(10.18f,3.24f,-1.63f,3.84f,.34f,2.99f),
        };

        static void Exterior()
        {
            for(int k=0;k<frames.Length-1;k++)
            {
                Frame a=frames[k],b=frames[k+1];Vector3 centre=new(0,(a.high+a.low)*.5f,(a.z+b.z)*.5f);
                var s=Part("Welded shell bay "+k.ToString("D2"),centre);
                bool roofOpening=a.z==-5.46f;
                foreach(int side in new[]{-1,1})
                {
                    var p=a.Profile(side);var q=b.Profile(side);
                    void Patch(Material m,Vector3 u,Vector3 v,Vector3 w,Vector3 x)
                    {
                        var mid=(u+v+w+x)*.25f;
                        var outward=new Vector3(mid.x,mid.y-centre.y,0);
                        if(Vector3.Dot(Vector3.Cross(v-u,w-u),outward)<0)s.Face(m,u,x,w,v);
                        else s.Face(m,u,v,w,x);
                    }
                    bool blindEnd=k<2||a.z>=8.12f;
                    for(int j=0;j<7;j++)
                    {
                        // Mid-side slot is reserved for actual room walls and windows.
                        if(j==3 && !blindEnd)continue;
                        if(j==6 && roofOpening)
                        {
                            Patch(hull,p[j],q[j],new(side*2.45f,b.high+.05f,b.z),new(side*2.45f,a.high+.05f,a.z));
                            continue;
                        }
                        Patch(j<2?dark:(j==2||j==5?enamel:hull),p[j],q[j],q[j+1],p[j+1]);
                    }
                    // Segmented external rubbing strakes and weld flanges, carried below window sills.
                    s.Beam(dark,p[2],q[2],.24f,.25f);
                    s.Beam(k%4==1?ochre:steel,p[2]+Vector3.up*.024f,q[2]+Vector3.up*.024f,.27f,.056f);
                    for(int j=0;j<6;j++)
                    {
                        if(j==3&&!blindEnd)continue;
                        s.Beam(dark,p[j],p[j+1],.15f,.12f);
                    }
                    s.Beam(steel,p[5],q[5],.13f,.085f);
                    if(k>=2&&k<14)
                    {
                        var pad=Vector3.Lerp(p[5],q[5],.5f);
                        s.Box(dark,pad,new(.33f,.36f,.75f));
                        s.Box(k%4==0?cyan:warm,pad+Vector3.right*side*.16f,new(.055f,.12f,.48f));
                    }
                }
            }
            var drive=Part("Twin shrouded azimuth drives",new(0,2,-32));
            foreach(int side in new[]{-1,1})
            {
                Vector3 centre=new(side*3.65f,1.00f,-33.28f);
                drive.Beam(hull,new(side*2.30f,1.48f,-31.2f),centre,.48f,.55f);
                drive.Tube(dark,centre+Vector3.forward*1.68f,centre+Vector3.forward*.63f,1.03f,1.41f,24);
                for(int i=0;i<20;i++)
                {
                    float f=i*Mathf.PI*.1f,g=(i+1)*Mathf.PI*.1f;
                    Vector3 a=new(Mathf.Cos(f),Mathf.Sin(f),0),b=new(Mathf.Cos(g),Mathf.Sin(g),0);
                    drive.Face(hull,centre+a*1.40f+Vector3.forward*.70f,centre+b*1.40f+Vector3.forward*.70f,
                        centre+b*1.42f-Vector3.forward*.68f,centre+a*1.42f-Vector3.forward*.68f);
                    drive.Beam(steel,centre+a*1.40f-Vector3.forward*.68f,centre+b*1.40f-Vector3.forward*.68f,.13f,.14f);
                }
                drive.Tube(steel,centre+Vector3.forward*.45f,centre-Vector3.forward*.58f,.33f,.17f);
                for(int i=0;i<6;i++)
                {
                    var spin=Quaternion.AngleAxis(i*60+19,Vector3.forward);
                    drive.Box(ochre,centre+spin*new Vector3(.75f,0,-.10f),new(.95f,.20f,.33f),
                        spin*Quaternion.Euler(26,0,-18),.12f);
                }
                // Keel rails and acoustic fairings follow the entire length below the working deck.
                var keel=Part("Bilge riser "+(side<0?"port":"starboard"),new(side*4,-1.8f,-6));
                for(int k=4;k<frames.Length-2;k++)
                {
                    var p=frames[k].Profile(side)[1];var q=frames[k+1].Profile(side)[1];
                    keel.Tube(rubber,p,q,.16f);
                    keel.Tube(ochre,p,Vector3.Lerp(p,q,.055f),.20f,-1,12);
                }
            }
            var mast=Part("Navigation mast and locator",new(0,7.74f,-27.2f));
            mast.Box(hull,new(0,7.91f,-27.2f),new(1.35f,.49f,2.32f),default,.19f);
            mast.Tube(steel,new(0,8.10f,-27.2f),new(0,9.67f,-27.2f),.24f,.15f);
            mast.Box(ochre,new(0,9.68f,-27.20f),new(1.78f,.23f,.50f));
            mast.Tube(dark,new(-.56f,9.68f,-27.2f),new(-.56f,10.45f,-27.2f),.065f,.025f);
            mast.Tube(alarm,new(.6f,9.71f,-27.2f),new(.60f,9.89f,-27.2f),.086f);
        }

        static void AirlockCollar()
        {
            // Existing pressure doors and flooded chamber run along +X from x=5.95 to 10.28.
            // Swept steel cheeks fair the side tube into the hull. Centre and exterior opening are clear.
            var s=Part("Sponson pressure fillets",new(7.31f,1.3f,-14));
            foreach(int sign in new[]{-1,1})
            {
                Vector3 a=new(5.85f,-.20f,-14+sign*3.12f),b=new(9.86f,-.17f,-14+sign*1.68f);
                Vector3 c=b+Vector3.up*2.15f,d=a+Vector3.up*3.01f;
                s.Face(enamel,a,b,c,d);s.Face(enamel,d,c,b,a);
                var e=a+new Vector3(-.38f,.0f,sign*1.18f);
                s.Face(hull,e,d,b);s.Face(hull,b,d,e);
                s.Beam(steel,d,c,.13f,.10f);
                s.Tube(ochre,a+Vector3.up*.44f,b+Vector3.up*.44f,.053f);
                s.Box(dark,b+Vector3.up*1.16f,new(.35f,1.48f,.30f));
                s.Box(warm,b+new Vector3(.15f,1.18f,0),new(.065f,.58f,.15f));
            }
            s.Box(dark,new(10.52f,-.13f,-14),new(1.46f,.24f,3.35f));
            s.Collision(new(10.52f,-.13f,-14),new(1.46f,.24f,3.35f));
            for(int i=0;i<7;i++)s.Box(ochre,new(11.19f,-.008f,-15.22f+i*.40f),new(.11f,.016f,.20f),Quaternion.Euler(0,23,0));
        }
    }
}
