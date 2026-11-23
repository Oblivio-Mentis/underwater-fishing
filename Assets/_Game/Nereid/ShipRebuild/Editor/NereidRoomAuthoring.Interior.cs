using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static readonly Color incandescent=new(1,.71f,.46f);
        static readonly Color submerged=new(.30f,.87f,.98f);

        // A chamfered panoramic light between fitted metal panels, with continuous collision
        // through the window. Baked under its room prefab, selectable as a single bay in Unity.
        static void WallBay(string name,float x,float z,float span,float baseY,float height,bool window)
        {
            var p=new Vector3(x,baseY+height*.5f,z);int side=x>0?1:-1;
            var s=Part(name,p);
            s.Collision(p,new(.24f,height,span));
            void Panel(Material m,float dx,float y,float dz,float width,float h,float len) =>
                s.Box(m,new(x+dx*side,baseY+y,z+dz),new(width,h,len));
            Panel(dark,0,.24f,0,.34f,.48f,span);
            Panel(enamel,-.04f,.54f,0,.26f,.44f,span-.04f);
            float top=height-.63f,bot=.82f;
            Panel(dark,.01f,height-.29f,0,.40f,.58f,span);
            Panel(ceramic,-.10f,top+.075f,0,.22f,.15f,span-.13f);
            if(window)
            {
                Panel(glass,0,(bot+top)*.5f,0,.065f,top-bot,span-.12f);
                Panel(steel,-.12f,bot-.05f,0,.21f,.14f,span);
                foreach(int end in new[]{-1,1})
                {
                    float j=z+end*(span*.5f-.10f);
                    s.Box(dark,new(x,baseY+(bot+top)*.5f,j),new(.45f,top-bot+.26f,.21f));
                    s.Beam(steel,new(x-side*.136f,baseY+bot,j-end*.37f),new(x-side*.136f,baseY+bot+.44f,j),.10f,.12f);
                    s.Beam(steel,new(x-side*.136f,baseY+top-.44f,j),new(x-side*.136f,baseY+top,j-end*.37f),.10f,.12f);
                }
            }
            else
            {
                Panel(enamel,0,(bot+top)*.5f,0,.26f,top-bot+.37f,span);
                Panel(ceramic,-.11f,(bot+top)*.53f,0,.08f,top-bot-.36f,span-.30f);
                Panel(steel,-.16f,bot+.16f,0,.065f,.08f,span*.78f);
                for(int i=0;i<5;i++)Panel(dark,-.163f,top-.40f,-span*.28f+i*.085f,.036f,.28f,.031f);
                Panel(ochre,-.154f,bot+.27f,span*.32f,.05f,.13f,.23f);
            }
            Panel(dark,-.17f,.16f,0,.12f,.16f,span-.19f);
        }

        static void CrossFrame(string name,float z,float width,float height,Material indicator)
        {
            var s=Part(name,new(0,height*.5f,z));
            foreach(int d in new[]{-1,1})
            {
                var a=new Vector3(d*(width-.12f),.10f,z);var b=new Vector3(a.x,height-.71f,z);
                var c=new Vector3(d*(width-.80f),height-.14f,z);
                s.Beam(dark,a,b,.26f,.23f);s.Beam(enamel,b,c,.29f,.27f);
                s.Beam(steel,a+Vector3.left*d*.14f+Vector3.up*.40f,b+Vector3.left*d*.14f,.056f,.083f);
                s.Beam(indicator,Vector3.Lerp(b,c,.19f)-Vector3.right*d*.086f,Vector3.Lerp(b,c,.82f)-Vector3.right*d*.086f,.074f,.091f);
                s.Box(dark,new(d*(width-1.2f),height-.11f,z),new(1.42f,.24f,.27f));
            }
            // Only outboard frame haunches by the loading hatch.
            if(z<=-5.46f || z>=-.54f)s.Beam(dark,new(-width+.85f,height-.13f,z),new(width-.85f,height-.13f,z),.17f,.19f);
        }

        static void Plate(string name,float a,float b,float z0,float z1,float y)
        {
            var p=new Vector3((a+b)*.5f,y-.11f,(z0+z1)*.5f);
            var s=Part(name,p);s.Slab(deck,p,new(b-a,.22f,z1-z0));
            // Removable longitudinal cable trough, never a trip collider.
            if(b-a<1.2f)return;
            float x=Mathf.Abs(a)>Mathf.Abs(b)?a+.30f:b-.30f;
            s.Box(dark,new(x,y+.004f,p.z),new(.28f,.018f,z1-z0-.12f));
            for(float z=z0+.18f;z<z1-.12f;z+=.225f)
                s.Box(steel,new(x,y+.018f,z),new(.25f,.019f,.053f));
        }

        static void DoorFrame(string name,float z,float halfSpan,float halfPass,float top,float height)
        {
            var s=Part(name,new(0,top*.5f,z));
            foreach(int side in new[]{-1,1})
            {
                float x=side*(halfPass+halfSpan)*.5f;
                s.Slab(enamel,new(x,height*.5f,z),new(halfSpan-halfPass,height,.22f));
                Vector3 a=new Vector3(side*(halfPass+.03f),.02f,z),b=new Vector3(a.x,top-.42f,z);
                var c=new Vector3(side*(halfPass-.36f),top,z);
                s.Beam(dark,a,b,.23f,.40f);s.Beam(dark,b,c,.24f,.40f);
                s.Box(warm,new(a.x,1.14f,z-.205f),new(.06f,.72f,.053f));
                s.Box(steel,new(x,.65f,z-.145f),new(halfSpan-halfPass-.28f,.085f,.088f));
            }
            s.Slab(dark,new(0,(height+top)*.5f,z),new(halfPass*2,height-top,.25f));
            s.Box(ochre,new(0,top-.02f,z),new(halfPass*2-.56f,.12f,.32f));
        }

        static void Stairs(string name,float x,float w,float foot,float landing,float rise,bool gallery)
        {
            Vector3 a=new(x,0,foot),b=new(x,rise,landing);
            var s=Part(name,(a+b)*.5f);
            int count=Mathf.CeilToInt(rise/.125f);float dz=Mathf.Abs(foot-landing)/count;
            for(int i=0;i<count;i++)
            {
                float y=rise*(i+1)/count, z=Mathf.Lerp(foot,landing,(i+.5f)/count);
                s.Box(deck,new(x,y-.061f,z),new(w,.122f,dz+.015f));
                s.Box(i%4==0?ochre:steel,new(x,y-.008f,z+Mathf.Sign(foot-landing)*(dz*.5f-.013f)),new(w-.04f,.025f,.023f));
            }
            Vector3 dir=(b-a).normalized;
            s.Collision((a+b)*.5f-Vector3.up*.068f,new(w,.13f,Vector3.Distance(a,b)),Quaternion.LookRotation(dir));
            foreach(int side in new[]{-1,1})
            {
                var shift=Vector3.right*(w*.5f-.025f)*side;
                s.Guard(dark,ochre,a+shift,b+shift);
                s.Beam(dark,a+shift-Vector3.up*.09f,b+shift-Vector3.up*.09f,.10f,gallery?.14f:.22f);
            }
        }

        static void ObservationHall()
        {
            const float l=-5.96f,r=5.96f;
            Plate("Aft crossover",l,r,-8.12f,-6.70f,0);
            Plate("Port inspection aisle",l,-2.42f,-6.70f,2.70f,0);
            Plate("Starboard inspection aisle",2.42f,r,-6.70f,2.70f,0);
            Plate("Bow crossover",l,r,2.70f,4.50f,0);
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<3;i++)WallBay((side<0?"Port":"Starboard")+" observation window "+(i+1),
                    side*5.94f,-8.10f+(i+.5f)*4.20f,4.20f,0,4.6f,true);
                var soffit=Part((side<0?"Port":"Starboard")+" overhead systems",new(side*4.10f,4.32f,-2));
                soffit.Slab(enamel,new(side*4.21f,4.65f,-1.80f),new(3.48f,.22f,12.62f));
                for(int k=0;k<3;k++)
                {
                    var a=new Vector3(side*(4.60f+k*.19f),4.18f,-7.94f);var b=new Vector3(a.x,a.y,4.34f);
                    soffit.Tube(k==0?ochre:rubber,a,b,k==0?.081f:.054f);
                    for(float z=-7.78f;z<4.25f;z+=2.10f)soffit.Tube(steel,new(a.x,a.y,z),new(a.x,a.y,z+.066f),k==0?.105f:.072f);
                }
                for(int i=0;i<3;i++)
                {
                    float z=-6.35f+i*4.19f;var c=new Vector3(side*3.84f,4.26f,z);
                    soffit.Box(dark,c,new(.63f,.31f,1.33f));
                    soffit.Box(warm,c-Vector3.up*.157f,new(.46f,.042f,1.10f));
                    foreach(float dz in new[]{-.44f,0,.44f})soffit.Box(steel,c+new Vector3(0,-.191f,dz),new(.51f,.044f,.033f));
                    soffit.Lamp("Work lamp",incandescent,c-Vector3.up*.30f,48,9);
                }
            }
            var ceiling=Part("Coffer and lift well",new(0,4.7f,-3));
            ceiling.Slab(dark,new(0,4.69f,-6.78f),new(4.94f,.20f,2.64f));
            ceiling.Slab(dark,new(0,4.69f,1.98f),new(4.94f,.20f,5.05f));
            for(int d=-1;d<=1;d+=2)
            {
                ceiling.Box(ochre,new(d*2.49f,4.67f,-3),new(.10f,.20f,5.08f));
                ceiling.Box(steel,new(0,4.72f,-3+d*2.50f),new(5.04f,.20f,.13f));
            }
            foreach(float z in new[]{-7.94f,-5.46f,-.54f,4.38f})CrossFrame("Main pressure rib "+z.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),z,5.87f,4.57f,warm);
            Stairs("Port aft observation stair",-4.70f,1.25f,-6.75f,-4.50f,1.50f,true);
            Stairs("Port forward observation stair",-4.70f,1.25f,5.45f,3,1.50f,true);
            var gallery=Part("Raised sampling gallery",new(-4.7f,1.45f,-.75f));
            gallery.Slab(deck,new(-4.7f,1.44f,-.75f),new(1.25f,.12f,7.5f));
            foreach(int d in new[]{-1,1})
            {
                var a=new Vector3(-4.70f+d*.60f,1.50f,-4.5f);var b=new Vector3(a.x,a.y,3);
                gallery.Guard(steel,ochre,a,b);
                for(int i=0;i<4;i++)gallery.Beam(dark,new(a.x,0,-4.36f+i*2.37f),new(a.x,1.42f,-4.36f+i*2.37f),.093f,.13f);
            }
        }

        static void Aquarium()
        {
            const float x=2.42f,z0=-6.70f,z1=2.70f;
            var pool=Part("Pressure tank and intake grids",new(0,-1.22f,-2));
            pool.Box(enamel,new(0,-2.34f,-2),new(4.97f,.22f,9.56f));
            foreach(int d in new[]{-1,1})
            {
                pool.Box(hull,new(d*(x+.05f),-1.19f,-2),new(.15f,2.36f,9.51f));
                pool.Box(hull,new(0,-1.19f,-2+d*4.75f),new(4.96f,2.36f,.15f));
                for(int i=0;i<6;i++)
                {
                    float z=z0+.52f+i*1.62f;
                    pool.Box(dark,new(d*(x-.06f),-1.29f,z),new(.18f,2.08f,.12f));
                    pool.Box(cyan,new(d*(x-.11f),-1.42f,z),new(.054f,.58f,.13f));
                    pool.Box(steel,new(d*1.88f,-2.20f,z),new(.41f,.075f,.64f));
                    for(int t=0;t<4;t++)pool.Box(dark,new(d*1.88f,-2.152f,z-.20f+t*.13f),new(.34f,.018f,.055f));
                }
                pool.Tube(steel,new(d*2.27f,-1.95f,z0+.14f),new(d*2.27f,-1.95f,z1-.14f),.081f);
                pool.Lamp("Submerged inspection wash",submerged,new(d*1.77f,-1.57f,-2+d*2.29f),18,6.1f);
            }
            var glazing=Part("Laminated walkable glass",new(0,-.065f,-2));
            // Glazing slabs share a single continuous collision surface; seams cannot catch player feet.
            glazing.Collision(new(0,-.08f,-2),new(x*2,.16f,z1-z0));
            for(int i=0;i<5;i++)
            {
                float z=z0+(i+.5f)*(z1-z0)/5;
                foreach(int d in new[]{-1,1})glazing.Box(glass,new(d*x*.50f,-.056f,z),new(x-.018f,.11f,(z1-z0)/5-.019f));
            }
            glazing.Box(water,new(0,-.166f,-2),new(4.80f,.011f,9.37f));
            var ribs=Part("Tank window clamp lattice",new(0,-.12f,-2));
            foreach(float sx in new[]{-x,0,x})ribs.Box(steel,new(sx,-.05f,-2),new(.066f,.11f,9.42f));
            for(int i=0;i<=5;i++)
            {
                float z=z0+i*(z1-z0)/5;
                ribs.Box(dark,new(0,-.102f,z),new(4.94f,.20f,.074f));
                foreach(float xx in new[]{-x,0,x})
                {
                    ribs.Box(ochre,new(xx,-.005f,z),new(.165f,.031f,.165f));
                    ribs.Tube(steel,new(xx,.010f,z),new(xx,.021f,z),.040f,-1,6);
                }
            }
        }
    }
}
