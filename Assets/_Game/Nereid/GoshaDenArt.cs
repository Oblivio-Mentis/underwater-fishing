using UnityEngine;

namespace Raid.Nereid
{
    internal static class GoshaDenLayout
    {
        // The existing mattress and reading light in the companion berth anchor the shelter.
        internal static readonly Vector3 Bed=new(5.03f,.22f,3.03f);
        internal static readonly (Vector3 at,Vector3 angle)[] Pieces={
            (new(.33f,.20f,.71f),new(-7,21,4)),(new(-.13f,.20f,-.74f),new(8,-25,13)),
            (new(-.50f,.20f,.57f),new(0,-15,-7)),(new(.43f,.20f,-.59f),new(27,16,-18)),
            (new(-.57f,.20f,-.59f),new(-12,23,-30)),(new(.22f,.20f,.37f),new(10,61,21)),
            (new(.05f,1.09f,-.37f),new(3,30,10)),(new(.15f,1.12f,.36f),new(-3,-24,10)),
            (new(-.39f,.955f,.34f),new(-3,36,18)),(new(-.39f,.944f,-.29f),new(4,-42,18)),
            (new(-.43f,.20f,.85f),new(-20,35,-42)),(new(.31f,.20f,-.85f),new(18,-25,47))
        };

        internal static Vector3 Approach(NereidVessel vessel) => RaidWorld.Instance?.expeditionLayout?.goshaApproach is Transform mark && mark?mark.position:vessel.transform.TransformPoint(new Vector3(3.96f,.14f,3.02f));
        internal static Vector3 Origin(NereidVessel vessel) => RaidWorld.Instance?.expeditionLayout?.goshaDen is Transform mark && mark?mark.position:vessel.transform.TransformPoint(Bed);
        internal static Quaternion Rotation(NereidVessel vessel) => RaidWorld.Instance?.expeditionLayout?.goshaDen is Transform mark && mark?mark.rotation:vessel.transform.rotation;
        internal static Pose Piece(NereidVessel vessel,int index)
        {
            var marker=ExpeditionLayout.At(RaidWorld.Instance?.expeditionLayout?.goshaDenSlots,index);
            if(marker)return new Pose(marker.position,marker.rotation);
            var piece=Pieces[Mathf.Clamp(index,0,Pieces.Length-1)];
            return new Pose(Origin(vessel)+Rotation(vessel)*piece.at,Rotation(vessel)*Quaternion.Euler(piece.angle));
        }
    }

    internal static class GoshaDenArt
    {
        static readonly Color Rust=new(.41f,.15f,.06f),Ochre=new(.85f,.44f,.12f),
            Teal=new(.12f,.43f,.41f),Iron=new(.18f,.24f,.26f),Canvas=new(.47f,.49f,.35f),Rope=new(.71f,.59f,.35f);
        static readonly Color Rubber=new(.10f,.12f,.12f),Faded=new(.83f,.75f,.52f);

        internal static ReefGeometry Found(GoshaJunkKind kind)
        {
            var g=new ReefGeometry();
            switch(kind)
            {
                case GoshaJunkKind.Buoy:
                    g.Tube(new[]{new Vector3(0,-.145f,0),new(0,-.11f,0),new(0,.04f,0),new(0,.13f,0),new(0,.16f,0)},
                        new[]{.065f,.16f,.185f,.131f,.06f},Ochre,10,.94f);
                    g.Tube(new[]{new Vector3(0,-.066f,0),new(0,-.034f,0)},new[]{.176f,.181f},Faded,10);
                    Ring(g,new Vector3(.01f,.19f,0),.045f,.014f,Rust,9,Quaternion.Euler(90,0,0));
                    g.Tube(new[]{new Vector3(.02f,.18f,.018f),new(.18f,.10f,-.037f),new(.22f,-.05f,-.13f),new(.30f,-.12f,-.07f)},
                        new[]{.012f,.012f,.010f,.007f},Rope,6);
                    break;
                case GoshaJunkKind.Sign:
                    Panel(g,new(-.32f,-.044f,-.22f),new(.27f,-.04f,-.22f),new(.34f,.024f,.18f),new(-.30f,.041f,.24f),Rust);
                    Panel(g,new(-.27f,-.039f,-.19f),new(.24f,-.034f,-.18f),new(.29f,.030f,.14f),new(-.25f,.044f,.20f),Ochre);
                    for(int i=0;i<3;i++)
                    {
                        float x=-.23f+i*.155f;
                        Panel(g,new(x,-.028f,-.158f),new(x+.057f,-.027f,-.148f),
                            new(x+.175f,.029f,.13f),new(x+.118f,.030f,.138f),Iron);
                    }
                    Bolt(g,new(-.26f,.041f,.174f));Bolt(g,new(.24f,-.028f,-.147f));
                    break;
                case GoshaJunkKind.Crate:
                    Box(g,new(0,-.09f,0),new(.60f,.054f,.42f),Iron);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(g,new(side*.279f,.006f,0),new(.045f,.22f,.43f),Teal);
                        Box(g,new(0,.10f,side*.205f),new(.59f,.045f,.048f),Teal);
                        Box(g,new(0,-.025f,side*.206f),new(.59f,.04f,.036f),Iron);
                        Bolt(g,new(side*.26f,.123f,.19f));
                    }
                    Box(g,new(-.077f,.001f,.21f),new(.045f,.205f,.026f),Ochre,Quaternion.Euler(0,0,-20));
                    break;
                case GoshaJunkKind.Hose:
                    Ring(g,Vector3.zero,.212f,.062f,Rubber,14);
                    Ring(g,new Vector3(.036f,.07f,-.018f),.195f,.027f,Iron,14);
                    g.Tube(new[]{new Vector3(.15f,-.01f,.16f),new(.26f,.02f,.08f),new(.29f,.016f,-.06f),new(.35f,.008f,-.085f)},
                        new[]{.035f,.034f,.034f,.046f},Rust,8);
                    break;
                case GoshaJunkKind.Filter:
                    g.Tube(new[]{new Vector3(-.24f,-.018f,0),new(-.115f,-.018f,0),new(.055f,.016f,0),new(.16f,.105f,0)},
                        new[]{.112f,.115f,.111f,.111f},Rust,9);
                    Ring(g,new Vector3(-.245f,-.018f,0),.088f,.024f,Ochre,10,Quaternion.Euler(0,0,90));
                    Ring(g,new Vector3(.16f,.105f,0),.092f,.022f,Iron,9,Quaternion.Euler(0,0,-32));
                    g.Tube(new[]{new Vector3(-.249f,-.018f,0),new(-.242f,-.018f,0)},new[]{.075f,.076f},Rubber,9);
                    break;
                case GoshaJunkKind.Grate:
                    for(int i=0;i<6;i++)
                    {
                        float x=-.27f+i*.106f;
                        Box(g,new Vector3(x,.015f*Mathf.Sin(i),0),new(.043f,.054f,.48f),i%3==0?Ochre:Iron,Quaternion.Euler(0,0,i*2));
                    }
                    Box(g,new(0,-.016f,.19f),new(.60f,.049f,.056f),Rust);
                    Box(g,new(-.014f,-.013f,-.19f),new(.55f,.048f,.049f),Teal);
                    break;
            }
            return g;
        }

        internal static ReefGeometry Shelter(int stage)
        {
            var g=new ReefGeometry();
            if(stage==0)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    float z=side*.67f;
                    var foot=new Vector3(-.49f,-.105f,z);
                    var elbow=new Vector3(-.56f,.49f,z+side*.04f);
                    var top=new Vector3(-.51f,.91f,z-side*.03f);
                    g.Tube(new[]{foot,elbow,top, new Vector3(.09f,1.02f,z)},new[]{.064f,.047f,.046f,.033f},side<0?Iron:Rust,7);
                    g.Tube(new[]{top+Vector3.down*.073f,top+Vector3.up*.063f},new[]{.065f,.064f},Rope,9);
                    Panel(g,new(.16f,-.10f,z),foot+Vector3.up*.13f,elbow+Vector3.right*.21f,new(.27f,.54f,z+side*.04f),side<0?Rust:Teal);
                    Box(g,new Vector3(-.42f,.24f,z+side*.065f),new(.14f,.61f,.055f),Ochre,Quaternion.Euler(3,0,13*side));
                }
                g.Tube(new[]{new Vector3(-.51f,.91f,-.67f),new(-.58f,.86f,-.08f),new(-.51f,.91f,.67f)},
                    new[]{.048f,.039f,.044f},Iron,7);
            }
            else if(stage==1)
            {
                // Three salvaged fabric panels overlap on a lean-to with a low edge and a full-width entrance.
                var left=new[]{new Vector3(-.76f,.86f,-.77f),new(-.69f,.79f,-.28f),new(-.72f,.82f,.29f),new(-.64f,.91f,.73f)};
                var ridge=new[]{new Vector3(-.24f,1.10f,-.76f),new(-.17f,1.06f,-.26f),new(-.16f,1.12f,.32f),new(-.12f,1.15f,.74f)};
                var rear=new[]{new Vector3(.43f,1.20f,-.72f),new(.43f,1.19f,-.28f),new(.40f,1.21f,.31f),new(.40f,1.19f,.72f)};
                for(int i=0;i<3;i++)
                {
                    Color fabric=i==1?Canvas:Teal*(i==0?.85f:1.13f);
                    Panel(g,left[i],left[i+1],ridge[i+1],ridge[i],fabric);
                    Panel(g,ridge[i],ridge[i+1],rear[i+1],rear[i],fabric*.85f);
                    g.Tube(new[]{left[i],ridge[i],rear[i]},new[]{.017f,.019f,.016f},Rope,6);
                }
                g.Tube(left,new[]{.024f,.020f,.021f,.022f},Rope,7);
                g.Tube(new[]{new Vector3(.33f,.245f,-.37f),new(.34f,.297f,-.24f),new(.36f,.30f,.23f),new(.32f,.25f,.35f)},
                    new[]{.086f,.147f,.15f,.088f},Canvas,8,.65f);
                g.Tube(new[]{new Vector3(.31f,.24f,-.365f),new(.33f,.355f,-.325f),new(.38f,.37f,-.274f)},
                    new[]{.024f,.024f,.021f},Rope,7);
                Panel(g,new(-.62f,.894f,-.49f),new(-.62f,.88f,-.20f),new(-.42f,1.016f,-.17f),new(-.39f,1.055f,-.48f),Rust);
                for(int i=0;i<5;i++)
                {
                    var thread=new Vector3(-.60f+i*.038f,.914f+i*.024f,-.46f);
                    g.Tube(new[]{thread,thread+new Vector3(.018f,.013f,.027f)},new[]{.006f,.006f},Faded,5);
                }
            }
            else
            {
                for(int i=0;i<7;i++)
                {
                    float x=-.57f+i*.127f;
                    g.Tube(new[]{new Vector3(x,.91f,.69f),new(x+.06f,.44f,.78f),new(x-.05f,.16f,.77f)},
                        new[]{.012f,.011f,.008f},Rope,6);
                }
                for(int i=0;i<5;i++)
                {
                    float y=.27f+i*.123f;
                    g.Tube(new[]{new Vector3(-.59f,y,.756f),new(-.15f,y-.042f,.79f),new(.27f,y,.735f)},
                        new[]{.014f,.012f,.012f},Rope,6);
                }
                g.Tube(new[]{new Vector3(-.73f,.84f,-.44f),new(-.72f,.66f,-.44f)},new[]{.012f,.011f},Rope,7);
                g.Tube(new[]{new Vector3(-.72f,.61f,-.44f),new(-.72f,.73f,-.44f)},new[]{.041f,.045f},Ochre,7);
                for(int i=0;i<3;i++)Bolt(g,new(-.30f+i*.14f,.071f,.62f));
            }
            return g;
        }

        static void Bolt(ReefGeometry g,Vector3 at)
        {
            g.Tube(new[]{at,at+Vector3.up*.022f},new[]{.027f,.027f},Faded,6);
        }

        static void Ring(ReefGeometry g,Vector3 at,float radius,float thickness,Color tone,int steps,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            int start=g.VertexCount;
            const int sides=6;
            for(int i=0;i<steps;i++)
            {
                float angle=i*Mathf.PI*2/steps;
                Vector3 radial=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
                for(int j=0;j<sides;j++)
                {
                    float phi=j*Mathf.PI*2/sides;
                    var point=radial*(radius+thickness*Mathf.Cos(phi))+Vector3.up*(Mathf.Sin(phi)*thickness);
                    g.Add(at+rotation*point,tone*(.75f+.25f*Mathf.Max(0,Mathf.Sin(phi))));
                    if(i>0){int a=start+(i-1)*sides+j,b=start+(i-1)*sides+(j+1)%sides;g.Quad(a,b,b+sides,a+sides);}
                }
            }
            for(int j=0;j<sides;j++)
            {
                int a=start+j,b=start+(j+1)%sides,offset=(steps-1)*sides;
                g.Quad(a+offset,b+offset,b,a);
            }
        }

        static void Panel(ReefGeometry g,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tone)
        {
            int start=g.Add(a,tone*.85f);
            g.Add(b,tone*.96f);g.Add(c,tone);g.Add(d,tone*.87f);
            g.Quad(start,start+1,start+2,start+3);
            int back=g.Add(d,tone*.68f);
            g.Add(c,tone*.78f);g.Add(b,tone*.74f);g.Add(a,tone*.64f);
            g.Quad(back,back+1,back+2,back+3);
        }

        static void Box(ReefGeometry g,Vector3 center,Vector3 size,Color tone,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            Vector3 extents=size*.5f;
            var v=new Vector3[8];
            for(int i=0;i<8;i++)v[i]=center+rotation*Vector3.Scale(extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            int[] faces={0,2,3,1,4,5,7,6,0,4,6,2,1,3,7,5,2,6,7,3,0,1,5,4};
            for(int i=0;i<faces.Length;i+=4)
            {
                Color color=tone*(i==16?1.13f:i==20?.68f:.90f);
                int a=g.Add(v[faces[i]],color),b=g.Add(v[faces[i+1]],color),c=g.Add(v[faces[i+2]],color),d=g.Add(v[faces[i+3]],color);
                g.Quad(a,b,c,d);
            }
        }
    }
}
