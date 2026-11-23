using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Level composition: one readable route per depth, named masses beside it, branches off it,
    // and a lit way home. Placement is constant data; jitter comes from the seeded reef random.
    public sealed partial class NereidReef
    {
        static readonly Color PathSand=new(.243f,.286f,.243f);
        static readonly Vector2 CausewayStart=new(44.6f,-55.4f);
        static readonly Vector2 CausewayEnd=new(50.2f,-49.0f);

        // Hoist pad -> sand runway -> limestone gate -> far pocket -> wreck basin.
        static readonly Vector2[] ShoalRoute={new(23,-28),new(27,-30),new(30,-33),new(33,-37),
            new(35.5f,-40.5f),new(38,-44),new(40.5f,-46.5f),new(46,-53),new(49,-58),new(52,-62)};
        // Hoist pad -> lip pad -> west bank descent -> berth terrace -> drop into the throat -> fissure.
        static readonly Vector2[] DeepRoute={new(23,-28),new(27.5f,-32),new(31,-36),new(34.5f,-39.5f),
            new(37,-43),new(39.5f,-47),new(41.5f,-51),new(43,-55),new(44.5f,-59),new(46,-63),
            new(47,-68),new(46.5f,-74),new(45.5f,-80),new(44,-86),new(45,-92)};

        // Fishing pockets beside the hoist pad: flat for two harpoon lines, kept clear of props.
        static readonly Vector2[] LandingPads={new(34,-33),new(46,-53)};

        readonly struct Needle
        {
            internal readonly float x,z,r,h;
            internal Needle(float x,float z,float r,float h){this.x=x;this.z=z;this.r=r;this.h=h;}
        }

        // Far crown flanks the Lantern Fault fish loop (x 43-48, z -82..-94) instead of crossing it.
        static readonly Needle[] FarCrown={new(37.5f,-84,2.0f,9),new(36,-90,2.2f,11),new(55,-85,2.0f,9),new(56.5f,-91,1.8f,8)};

        static readonly Needle[] DeepNeedles={
            // West comb: four clusters along the descent with 3-4 m slots; tops stay below the fish lanes.
            new(27.5f,-41,1.5f,11),new(30,-47,1.3f,8.6f),new(26.5f,-52,1.5f,12.5f),new(34,-53,1.8f,9),
            // East palisade along the rim, 3-4.5 m slots between them for cold light shafts.
            new(55.5f,-60.5f,2.0f,14),new(58,-52,2.0f,12),new(57.5f,-43,2.4f,13),
            // North doorpost of the first look east from the hoist.
            new(39.5f,-28.5f,1.2f,4),
            // Throat gate: 5 m slot between the fallen column's root and the throat needle.
            new(39.5f,-61.5f,2.3f,14),new(48.5f,-60.5f,1.4f,9),
            // North-east shoulder.
            new(55,-31,1.6f,8),new(57,-34.5f,1.5f,7)
        };

        static readonly Needle[] DeepBuddies={
            new(25,-43.5f,1.1f,6.4f),new(24,-49,1.0f,7.6f)
        };

        static Needle[] Concat(Needle[] a,Needle[] b)
        {
            var all=new Needle[a.Length+b.Length];
            a.CopyTo(all,0);b.CopyTo(all,a.Length);
            return all;
        }

        static float SegmentDistance(float x,float z,Vector2 a,Vector2 b)
        {
            var p=new Vector2(x,z);
            var ab=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);
            return Vector2.Distance(p,a+ab*t);
        }

        static float RouteDistance(Vector2[] route,float x,float z)
        {
            float best=float.MaxValue;
            for(int i=1;i<route.Length;i++)
                best=Mathf.Min(best,SegmentDistance(x,z,route[i-1],route[i]));
            return best;
        }

        static float RunwayClearance(float x,float z)=>RouteDistance(ShoalRoute,x,z);
        static float DeepTrailClearance(float x,float z)=>RouteDistance(DeepRoute,x,z);

        static float ShoalRouteTint(float x,float z)=>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.2f,1.2f,RunwayClearance(x,z)));
        static float DeepRouteTint(float x,float z)=>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.4f,1.4f,DeepTrailClearance(x,z)));

        // A shallow trough with low shoulders turns the runway into a path rather than a plane;
        // the expedition benches ease off beside the lane so nothing climbs into it.
        static float ShoalRelief(float x,float z)
        {
            float distance=RunwayClearance(x,z);
            float open=Mathf.Lerp(.55f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.4f,12f,distance)));
            float trough=-.30f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,3f,distance)));
            float shoulder=.45f*Mathf.Exp(-Mathf.Pow((distance-4.4f)/2.4f,2));
            return trough+shoulder+open*ExpeditionShelf(x,z);
        }

        static float PadMask(float x,float z,float atX,float atZ,float flat,float shore)=>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(flat,shore,Vector2.Distance(new(x,z),new(atX,atZ))));

        static float LandingPadMask(float x,float z)
        {
            float mask=1;
            foreach(var pad in LandingPads)mask=Mathf.Min(mask,PadMask(x,z,pad.x,pad.y,6.2f,9.8f));
            return mask;
        }

        static bool PadFree(float x,float z,float radius)
        {
            foreach(var pad in LandingPads)
                if(Vector2.Distance(new Vector2(x,z),pad)<6.2f+radius)return false;
            return Vector2.Distance(new Vector2(x,z),new Vector2(23,-28))>5f+radius;
        }

        static bool ShallowClear(float x,float z,float radius)=>
            RunwayClearance(x,z)>2.7f+radius && PadFree(x,z,radius) && !NereidSalvage.Reserved(x,z);

        static bool DeepClear(float x,float z,float radius)=>
            DeepTrailClearance(x,z)>2.8f+radius && PadFree(x,z,radius) && !NereidSalvage.Reserved(x,z);

        // The causeway is the berth terrace: it holds the salvage at the shallow bed height and
        // carries the descent out over the ravine instead of leaving a pale pillar standing in it.
        static float CausewayMask(float x,float z)=>
            1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.6f,5.6f,SegmentDistance(x,z,CausewayStart,CausewayEnd)));

        // The east step fills the rim under the (52,-38) berth; the Rift Lip school circles above it.
        static float EastStep(float x,float z)
        {
            float dx=(x-52.6f)/5.2f,dz=(z+38.4f)/4.2f;
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.25f,Mathf.Sqrt(dx*dx+dz*dz)));
        }

        // The lantern shelf is the lower terrace, 5.2 m under the bed on the throat's west side.
        static float LanternShelf(float x,float z)
        {
            float dx=(x-37.5f)/7f,dz=(z+67)/6f;
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1.3f,Mathf.Sqrt(dx*dx+dz*dz)));
        }

        // A bedded limestone slab: wide, low and flat-topped, unlike the round mounds around it.
        void Slab(ReefGeometry mesh,float x,float z,float yaw,float length,float width,float height,float tilt)
        {
            const int sides=7;
            float heading=yaw*Mathf.Deg2Rad,c=Mathf.Cos(heading),s=Mathf.Sin(heading);
            float baseY=Seafloor(false,x,z)-.18f;
            var radius=new float[sides];
            for(int i=0;i<sides;i++)radius[i]=Range(.84f,1.12f);
            Vector3 Point(float u,float w,float y)=>new(x+c*u-s*w,y,z+s*u+c*w);
            var bottom=new Vector3[sides];var upper=new Vector3[sides];var top=new Vector3[sides];
            for(int i=0;i<sides;i++)
            {
                float a=2*Mathf.PI*i/sides,r=radius[i];
                float u=Mathf.Cos(a)*length*.5f*r,w=Mathf.Sin(a)*width*.5f*r;
                bottom[i]=Point(u*1.08f,w*1.08f,baseY);
                upper[i]=Point(u*1.03f,w*1.03f,baseY+height*.58f);
                top[i]=Point(u*.98f,w*.98f,baseY+height+tilt*u);
            }
            for(int i=0;i<sides;i++)
            {
                int n=(i+1)%sides;
                Flat(mesh,bottom[i],upper[i],upper[n],bottom[n],Silt*Range(.9f,1.15f));
                Flat(mesh,upper[i],top[i],top[n],upper[n],Color.Lerp(Silt,Chalk,.58f)*Range(.86f,1.08f));
            }
            int centre=mesh.Add(Point(0,0,baseY+height),Chalk);
            for(int i=0;i<sides;i++)mesh.Face(centre,mesh.Add(top[(i+1)%sides],Chalk*1.03f),mesh.Add(top[i],Chalk));
        }

        // A shoal pinnacle: tall and narrow with exposed bedding, the opposite silhouette to the slab.
        void Spire(ReefGeometry mesh,float x,float z,float radius,float height)
        {
            const int sides=7;
            float baseY=Seafloor(false,x,z)-.16f,heading=Range(0,6.28f),lean=Range(-.09f,.09f);
            float[] level={0,.16f,.55f,.9f,1},taper={1.16f,1,.79f,.48f,.13f};
            var edge=new float[sides];
            for(int i=0;i<sides;i++)edge[i]=Range(.84f,1.12f);
            var rings=new Vector3[level.Length,sides];
            for(int row=0;row<level.Length;row++)
            for(int i=0;i<sides;i++)
            {
                float a=heading+2*Mathf.PI*i/sides,r=radius*taper[row]*edge[i];
                rings[row,i]=new Vector3(x+Mathf.Cos(a)*r+lean*height*level[row],
                    baseY+height*level[row],z+Mathf.Sin(a)*r*.84f);
            }
            for(int row=1;row<level.Length;row++)
            for(int i=0;i<sides;i++)
            {
                int n=(i+1)%sides;
                float tone=Range(.82f,1.12f)*(row==2?.78f:1);
                Flat(mesh,rings[row-1,i],rings[row,i],rings[row,n],rings[row-1,n],
                    Color.Lerp(Silt,Chalk,.38f+row*.13f)*tone);
            }
            int crown=mesh.Add(new Vector3(x+lean*height,baseY+height,z),Chalk);
            for(int i=0;i<sides;i++)
                mesh.Face(crown,mesh.Add(rings[level.Length-1,(i+1)%sides],Chalk),mesh.Add(rings[level.Length-1,i],Chalk));
        }

        // A hexagonal basalt column rooted on the floor with a flat, slightly tilted capital.
        Vector3 HexColumn(ReefGeometry mesh,float x,float z,float radius,float height)
        {
            const int sides=6;
            float foot=Seafloor(true,x,z)-.14f,top=foot+height;
            float heading=Range(0,6.28f);
            var rings=new Vector3[3,sides];
            for(int i=0;i<sides;i++)
            {
                float a=heading+2*Mathf.PI*i/sides,r=radius*Range(.9f,1.1f);
                rings[0,i]=new Vector3(x+Mathf.Cos(a)*r,foot,z+Mathf.Sin(a)*r);
                rings[1,i]=new Vector3(rings[0,i].x,Mathf.Lerp(foot,top,.62f),rings[0,i].z);
                rings[2,i]=new Vector3(rings[0,i].x,top+(i%2==0?.07f:-.05f),rings[0,i].z);
            }
            for(int row=1;row<3;row++)
            for(int i=0;i<sides;i++)
            {
                int n=(i+1)%sides;
                float tone=Range(.78f,1.14f)*(row==1?.88f:1);
                Flat(mesh,rings[row-1,i],rings[row,i],rings[row,n],rings[row-1,n],RiftSlate*tone);
            }
            var cap=new Vector3(x,top+.04f,z);
            int centre=mesh.Add(cap,Mineral);
            for(int i=0;i<sides;i++)
                mesh.Face(centre,mesh.Add(rings[2,(i+1)%sides],Mineral),mesh.Add(rings[2,i],Mineral));
            return cap;
        }

        // Amber work lamp: the same practicals the crew sees on the hull, so home reads as home.
        void Lamp(ReefGeometry poles,ReefGeometry lenses,Vector3 foot,Color tone)
        {
            var head=foot+Vector3.up*1.52f;
            poles.Tube(new[]{foot,Vector3.Lerp(foot,head,.55f),head},new[]{.085f,.062f,.11f},PressureSteel,6);
            poles.Tube(new[]{head-Vector3.up*.09f,head+Vector3.up*.16f},new[]{.17f,.14f},TapeGold*.72f,7);
            lenses.Tube(new[]{head+Vector3.up*.02f,head+Vector3.up*.15f},new[]{.132f,.104f},tone,7);
        }

        void DressMass(ReefGeometry life,float x,float z,float radius,Color tint)
        {
            for(int i=0;i<6;i++)
            {
                float angle=Range(0,6.28f),spread=radius*Range(1.02f,1.46f);
                float px=x+Mathf.Cos(angle)*spread,pz=z+Mathf.Sin(angle)*spread;
                if(!ShallowClear(px,pz,.7f))continue;
                var at=new Vector3(px,Seafloor(false,px,pz)-.12f,pz);
                ScatterStone(life,at,Range(.55f,1.3f),tint*Range(.6f,1));
                if(i%2==0)Fan(life,at+Vector3.up*.12f,angle);
                if(i%3==0)Coral(life,at+Vector3.up*.08f,Range(.58f,1.02f),i%6==0);
            }
        }

        void BuildShoalLandmarks(ReefGeometry land,ReefGeometry life)
        {
            // North table: the flat slab frames the survey skiff branch.
            Slab(land,39,-24.5f,34,9.5f,6.2f,4.2f,.16f);
            DressMass(life,39,-24.5f,4.6f,Chalk);
            // West tilted table over the port branch.
            Slab(land,22,-46,35,7.4f,5.4f,4.8f,.24f);
            DressMass(life,22,-46,3.8f,Silt);
            // East pinnacle: the tall landmark beside the lane, right of the gate from the hoist.
            Spire(land,49.5f,-43,2.6f,12);
            Spire(land,54,-42.5f,1.6f,6);
            DressMass(life,49.5f,-43,2.8f,Chalk);
            var poles=new ReefGeometry();
            var lenses=new ReefGeometry();
            Lamp(poles,lenses,new Vector3(27.8f,Seafloor(false,27.8f,-37.2f)-.05f,-37.2f),ReturnMarker);
            Lamp(poles,lenses,new Vector3(38.5f,Seafloor(false,38.5f,-50.5f)-.05f,-50.5f),ReturnMarker);
            Publish("Runway lamps",poles,rock,false,shoal.transform);
            Publish("Runway lamp lenses",lenses,bioluminescence,false,shoal.transform)
                .GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        void BuildDeepLandmarks(ReefGeometry stone,ReefGeometry talus,ReefGeometry glow)
        {
            foreach(var needle in Concat(DeepNeedles,FarCrown))
            {
                var at=new Vector3(needle.x,Seafloor(true,needle.x,needle.z)-.12f,needle.z);
                BasaltNeedle(stone,at,needle.r,needle.h);
                for(int i=0;i<3;i++)
                {
                    float angle=Range(0,6.28f),distance=needle.r*Range(1.05f,1.9f);
                    float px=needle.x+Mathf.Cos(angle)*distance,pz=needle.z+Mathf.Sin(angle)*distance;
                    if(!DeepClear(px,pz,.6f))continue;
                    ScatterStone(talus,new Vector3(px,Seafloor(true,px,pz)-.12f,pz),Range(.5f,1.4f),Mineral*.8f);
                }
            }
            foreach(var buddy in DeepBuddies)
                BasaltNeedle(stone,new Vector3(buddy.x,Seafloor(true,buddy.x,buddy.z)-.12f,buddy.z),buddy.r,buddy.h);

            // A fallen column leans from the throat floor onto the lip needle at (34,-53):
            // it closes the west side of the throat mouth and leaves the east side as the slot.
            float dropFoot=Seafloor(true,40.2f,-57.6f)-.2f;
            float lipFoot=Seafloor(true,34.2f,-53.4f);
            RockSpan(stone,new[]{
                new Vector3(40.2f,dropFoot,-57.6f),
                new Vector3(38,dropFoot+5.6f,-55.8f),
                new Vector3(36,lipFoot+5.4f,-54.4f),
                new Vector3(34.2f,lipFoot+8.2f,-53.4f)},
                new[]{2.1f,1.7f,1.5f,1.1f},RiftSlate,.5f);

            // Berth terrace: hex columns and lanterns rim the causeway where the pale pillar stood.
            // Columns stand on the terrace rim, outside the 6 m landing circle around the berth.
            Vector2[] causeway={new(52.6f,-50.4f),new(50.9f,-58.4f),new(48.4f,-46.6f)};
            for(int i=0;i<causeway.Length;i++)
            {
                var cap=HexColumn(stone,causeway[i].x,causeway[i].y,Range(.52f,.86f),Range(1.3f,1.9f));
                if(i%2==0)Lantern(talus,glow,cap,Range(.72f,1.05f),i==2?VioletPolyp:BluePolyp);
            }
            Vector2[] eastStep={new(53.6f,-39.6f),new(54.8f,-37.8f),new(53.4f,-36.5f)};
            foreach(var at in eastStep)HexColumn(stone,at.x,at.y,Range(.46f,.72f),Range(1.1f,1.6f));

            // Beacon spire at the causeway head: the glowing landmark the hoist looks toward.
            var beaconTop=BasaltNeedle(stone,new Vector3(53,Seafloor(true,53,-47.5f)-.12f,-47.5f),1.5f,8);
            for(int i=0;i<3;i++)
            {
                float a=i*2.0944f;
                Lantern(talus,glow,beaconTop+new Vector3(Mathf.Cos(a)*.42f,-.55f,Mathf.Sin(a)*.34f),Range(.9f,1.15f),BluePolyp);
            }
            foreach(var at in new[]{new Vector2(54.6f,-49.4f),new Vector2(51.6f,-45.3f)})
                Lantern(talus,glow,new Vector3(at.x,Seafloor(true,at.x,at.y)-.15f,at.y),Range(.6f,.85f),VioletPolyp);

            // Lantern shelf: the lower terrace, lit like a camp above the basin.
            var shelfRock=Rock(stone,new Vector3(40.5f,Seafloor(true,40.5f,-68.5f)-.25f,-68.5f),1.3f,1.5f);
            Lantern(talus,glow,shelfRock,Range(.9f,1.2f),BluePolyp);
            var second=Rock(stone,new Vector3(39.5f,Seafloor(true,39.5f,-64.5f)-.2f,-64.5f),1.0f,1.1f);
            Lantern(talus,glow,second,Range(.7f,.95f),VioletPolyp);
            for(int i=0;i<4;i++)
            {
                float angle=Range(0,6.28f);
                float px=40+Mathf.Cos(angle)*2.6f,pz=-66.5f+Mathf.Sin(angle)*2.1f;
                var at=new Vector3(px,Seafloor(true,px,pz)-.12f,pz);
                ScatterStone(talus,at,Range(.4f,1.1f),Mineral*.86f);
                if(i%2==0)Lantern(talus,glow,at+Vector3.up*.1f,Range(.36f,.55f),BluePolyp);
            }

            // Far crown: a cold glow over the fissure, the last landmark before the way back.
            foreach(var needle in FarCrown)
            {
                float px=needle.x+needle.r*1.3f,pz=needle.z+1.1f;
                Lantern(talus,glow,new Vector3(px,Seafloor(true,px,pz)-.15f,pz),Range(1.1f,1.45f),BluePolyp);
            }

            // Threshold colony: what the hoist sees first, either side of the lip pad.
            Lantern(talus,glow,new Vector3(31.5f,Seafloor(true,31.5f,-43.2f)-.15f,-43.2f),1.3f,BluePolyp);
            Lantern(talus,glow,new Vector3(40.6f,Seafloor(true,40.6f,-30.2f)-.15f,-30.2f),1.1f,BluePolyp);
            Lantern(talus,glow,new Vector3(38.2f,Seafloor(true,38.2f,-50.2f)-.15f,-50.2f),.9f,VioletPolyp);

            var poles=new ReefGeometry();
            var lenses=new ReefGeometry();
            // Lamps fill the chain so no stretch of the route is more than 12 m from the next light.
            foreach(var mark in new[]{new Vector2(36,-50.5f),new Vector2(47.4f,-60.8f),new Vector2(44.6f,-69.2f),
                new Vector2(40.3f,-66.4f),new Vector2(49.6f,-42.6f),new Vector2(41.5f,-78.5f),new Vector2(43.2f,-87)})
                Lamp(poles,lenses,new Vector3(mark.x,Seafloor(true,mark.x,mark.y)-.05f,mark.y),
                    mark.y<-70?CyanMarker:ReturnMarker);
            Publish("Deep route lamps",poles,basalt,false,abyss.transform);
            Publish("Deep route lamp lenses",lenses,bioluminescence,false,abyss.transform)
                .GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;

            // Cold spill from the hull lamps far above, dropped through the palisade slots.
            var shafts=new ReefGeometry();
            foreach(var gap in new[]{new Vector2(56.2f,-56),new Vector2(57.4f,-48.5f),new Vector2(55.8f,-40.5f)})
            {
                var top=new Vector3(gap.x,Seafloor(true,gap.x,gap.y)+30,gap.y);
                shafts.Tube(new[]{top,top+new Vector3(-2.2f,-12,1.3f),top+new Vector3(-4.4f,-26,2.6f)},
                    new[]{1.5f,1.0f,.42f},new Color(.55f,.7f,.76f),9);
            }
            Publish("Palisade gaps / cold work-light shafts",shafts,lightColumns,false,abyss.transform);
        }
    }
}
