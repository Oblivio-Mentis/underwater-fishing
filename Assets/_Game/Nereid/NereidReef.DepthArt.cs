using System;
using UnityEngine;

namespace Raid.Nereid
{
    public sealed partial class NereidReef
    {
        [Serializable]
        sealed class RockForm
        {
            public Vector3[] vertices;
            public int[] triangles;
        }

        readonly RockForm[] rockForms=new RockForm[3];
        static readonly string[] RockNames={"rock_largeC","rock_smallFlatB","rock_tallH"};
        static readonly Color Chalk=new(.225f,.292f,.234f);
        static readonly Color Silt=new(.072f,.132f,.137f);
        static readonly Color RiftSlate=new(.065f,.103f,.132f);
        static readonly Color Mineral=new(.127f,.166f,.183f);

        void LoadRockForms()
        {
            for(int i=0;i<RockNames.Length;i++)
            {
                var source=Resources.Load<TextAsset>("ReefRockForms/"+RockNames[i]);
                if(source!=null)rockForms[i]=JsonUtility.FromJson<RockForm>(source.text);
            }
        }

        void ScatterStone(ReefGeometry mesh,Vector3 position,float size,Color tint)
        {
            int style=random.Next(rockForms.Length);
            var form=rockForms[style];
            if(form==null)
            {
                Rock(mesh,position,size*.57f,size*.51f);
                return;
            }
            StampRock(mesh,form,position,new Vector3(size,size*Range(.75f,1.3f),size),
                Quaternion.Euler(0,Range(0,360),0),tint);
        }

        static void StampRock(ReefGeometry mesh,RockForm form,Vector3 position,Vector3 scale,Quaternion rotation,Color tint)
        {
            for(int i=0;i<form.triangles.Length;i+=3)
            {
                var a=form.vertices[form.triangles[i]];
                var b=form.vertices[form.triangles[i+1]];
                var c=form.vertices[form.triangles[i+2]];
                float smooth=Mathf.Clamp01((a.y+b.y+c.y)*.63f);
                var color=tint*Mathf.Lerp(.72f,1.19f,smooth);
                int face=mesh.Add(position+rotation*Vector3.Scale(a,scale),color);
                mesh.Add(position+rotation*Vector3.Scale(b,scale),color);
                mesh.Add(position+rotation*Vector3.Scale(c,scale),color);
                mesh.Face(face,face+1,face+2);
            }
        }

        static float IslandMask(float x,float z,float atX,float atZ,float radius,float shore)
        {
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(radius,shore,Vector2.Distance(new(x,z),new(atX,atZ))));
        }

        static float FishingFloorMask(float x,float z)
        {
            float pad=IslandMask(x,z,23,-28,5,10);
            pad=Mathf.Min(pad,IslandMask(x,z,29,-33,3,5));
            // Match the static berth positions used by NereidSalvage on both depths.
            foreach(var at in BerthIslands)pad=Mathf.Min(pad,IslandMask(x,z,at.x,at.y,2.45f,4.4f));
            return pad;
        }

        static readonly Vector2[] BerthIslands={new(24,-35),new(34,-33),new(32,-46),new(35,-48),new(46,-53),new(52,-38)};

        static float ShoalHeight(float x,float z)
        {
            float cove=1-IslandMask(x,z,35,-38,18,36);
            float lane=Mathf.PerlinNoise(x*.065f+5,z*.082f+21);
            float dune=.38f*Mathf.Sin(x*.34f+z*.25f+lane*3.1f)+.74f*(lane-.55f);
            return BedHeight(x,z)+(dune*cove+ShoalRelief(x,z)+FlankRelief(x,z,false))*
                FishingFloorMask(x,z)*LandingPadMask(x,z);
        }

        static float RiftAxis(float z)=>43.6f-(z+48)*.22f+1.7f*Mathf.Sin((z+46)*.10f);

        static float RiftHeight(float x,float z)
        {
            float extent=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-24,-40,z))*
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(-92,-66,z));
            float across=Mathf.Abs(x-RiftAxis(z));
            // The throat narrows and deepens the ravine between the landing and the outer seam.
            float throat=Mathf.Exp(-Mathf.Pow((z+61)/7f,2));
            float edging=1-.28f*throat;
            float ravine=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f*edging,8.6f*edging,across));
            float jagged=.62f+.38f*Mathf.PerlinNoise(x*.38f+6,z*.31f+14);
            // A real recessed bed replaces the shallows collider. Stay at the original height under gameplay fixtures.
            float bay=(8.8f+2.4f*Mathf.Sin(z*.17f+4.9f))*(1+.75f*throat);
            // Berth terraces fill the ravine where salvage must sit at the shallow bed height.
            float fill=Mathf.Max(CausewayMask(x,z),EastStep(x,z));
            float bed=BedHeight(x,z);
            float floor=bed+(ExpeditionRift(x,z)+FlankRelief(x,z,true)-extent*ravine*jagged*bay*(1-fill))*
                FishingFloorMask(x,z)*LandingPadMask(x,z);
            // The lantern shelf is cut into the throat's west bank: a terrace 5.2 m under the bed,
            // above the ravine floor and below the lip.
            return Mathf.Lerp(floor,bed-5.2f,LanternShelf(x,z));
        }

        static float TerrainX(int i)
        {
            // Preserve the existing eastern grid and match the flanks' resolution.
            if(i<11)return -141+i*(89f/11);
            if(i<47)return -52+(i-11)*(58f/36);
            if(i<=114)return 6+(i-47)*1.25f;
            return 89.75f+(i-114)*7.9f;
        }

        static float TerrainZ(int i)
        {
            if(i<12)return -194.4f+i*7.2f;
            if(i<=100)return -108+(i-12)*1.20f;
            if(i<=135)return -2.4f+(i-100)*1.40f;
            return 46.6f+(i-135)*7.2f;
        }

        const int Columns=132,Rows=154;

        // Analytic and interpolated heights diverge on steep banks. Root scenery
        // at the exact vertices and diagonal used by Terrain's collision mesh.
        static float Seafloor(bool deep,float x,float z)
        {
            static int Cell(float value,int count,Func<int,float> axis)
            {
                int low=0,high=count-1;
                while(high-low>1)
                {
                    int middle=(high+low)/2;
                    if(axis(middle)<=value)low=middle;
                    else high=middle;
                }
                return low;
            }
            int i=Cell(x,Columns,TerrainX),j=Cell(z,Rows,TerrainZ);
            float l=TerrainX(i),r=TerrainX(i+1),b=TerrainZ(j),t=TerrainZ(j+1);
            float u=Mathf.InverseLerp(l,r,x),v=Mathf.InverseLerp(b,t,z);
            float At(float px,float pz)=>deep?RiftHeight(px,pz):ShoalHeight(px,pz);
            float origin=At(l,b),corner=At(r,t),edge=u>v?At(r,b):At(l,t);
            return origin+Mathf.Max(u,v)*(edge-origin)+Mathf.Min(u,v)*(corner-edge);
        }

        static ReefGeometry Terrain(Func<float,float,float> height,bool deep,Func<float,float,float> route=null)
        {
            const int columns=Columns,rows=Rows;
            var bed=new ReefGeometry();
            for(int z=0;z<rows;z++)
            for(int x=0;x<columns;x++)
            {
                float wx=TerrainX(x),wz=TerrainZ(z);
                float y=height(wx,wz);
                float tide=Mathf.PerlinNoise(wx*.185f+4,wz*.196f+3);
                float current=Mathf.Sin(wx*.56f+wz*.34f+2*tide)*.18f;
                float bedSlope=Mathf.Abs(y-height(wx+.45f,wz))+Mathf.Abs(y-height(wx,wz+.45f));
                var grit=deep?Color.Lerp(RiftSlate,Mineral,tide*.64f):
                    Color.Lerp(Silt,Chalk,Mathf.Clamp01(tide*.85f+current));
                var stratum=deep?new Color(.073f,.096f,.126f):new Color(.102f,.150f,.148f);
                grit=Color.Lerp(grit,stratum,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,1.16f,bedSlope))*.82f);
                // The route is lighter sand than the reef around it, so the eye follows the floor.
                if(route!=null)grit=Color.Lerp(grit,deep?Mineral*.93f:PathSand,route(wx,wz)*.62f);
                bed.Add(new Vector3(wx,y,wz),grit);
                if(x>0 && z>0)
                {
                    int corner=z*columns+x;
                    bed.Quad(corner-columns-1,corner-1,corner,corner-columns);
                }
            }
            return bed;
        }

        void BuildCoralGarden(ReefGeometry life,ReefGeometry rubble)
        {
            // The shore breaks into two islands. Swim the wide sandy channel between them from the hoist.
            Vector2[] islands={new(29,-18.8f),new(49,-34),new(29,-42.5f),new(38.5f,-59.5f)};
            for(int j=0;j<islands.Length;j++)
            {
                for(int i=0;i<13;i++)
                {
                    float angle=i*2.39996f+Range(-.26f,.26f);
                    float radius=.7f+Mathf.Sqrt(i)*.64f;
                    float x=islands[j].x+Mathf.Cos(angle)*radius;
                    float z=islands[j].y+Mathf.Sin(angle)*radius;
                    if(!ShallowClear(x,z,.8f))continue;
                    var at=new Vector3(x,ShoalHeight(x,z)-.1f,z);
                    ScatterStone(rubble,at,Range(.64f,1.75f),Color.Lerp(Silt,Chalk,Range(.45f,.74f)));
                    at.y+=.22f;
                    if(i%3==0)
                    {
                        var tint=j==2?new Color(.27f,.10f,.37f):new Color(.59f,.16f,.20f);
                        if(!PlaceReefProp(life,"coral_table",at,Range(1.1f,1.64f),angle*Mathf.Rad2Deg,tint))
                            Coral(life,at,Range(.9f,1.3f),j!=2);
                    }
                    else if(i%3==1)
                    {
                        if(!PlaceReefProp(life,"coral_fan",at,Range(1.25f,2.25f),angle*Mathf.Rad2Deg,new Color(.51f,.16f,.28f)))
                            Fan(life,at,angle);
                    }
                    else Sponge(life,at,Range(.8f,1.55f));
                }
            }
        }

        void Coral(ReefGeometry mesh,Vector3 basePoint,float size,bool warm)
        {
            var bark=warm?new Color(.397f,.117f,.156f):new Color(.207f,.14f,.295f);
            var tips=warm?new Color(.62f,.268f,.204f):new Color(.372f,.236f,.445f);
            for(int i=0;i<7;i++)
            {
                float angle=i*2.39996f+Range(-.24f,.24f);
                var spread=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var fork=Vector3.Cross(spread,Vector3.up);
                float height=Range(.65f,1.3f);
                var joint=basePoint+(spread*.33f+Vector3.up*height*.61f)*size;
                var crown=joint+(spread*.20f+Vector3.up*height*.34f)*size;
                mesh.Tube(new[]{basePoint+spread*.11f*size,joint,crown},
                    new[]{.12f*size,.093f*size,.032f*size},bark,6,.81f);
                for(int side=-1;side<=1;side+=2)
                {
                    var branch=joint+(fork*(side*.18f)+Vector3.up*Range(.13f,.28f))*size;
                    var bud=branch+(spread*.1f+Vector3.up*Range(.10f,.22f))*size;
                    mesh.Tube(new[]{joint,branch,bud},new[]{.079f*size,.051f*size,.021f*size},tips,5);
                }
            }
        }

        ReefGeometry SurveyWreck()
        {
            var wreck=new ReefGeometry();
            var at=new Vector3(24.5f,ShoalHeight(24.5f,-16.4f)-.46f,-16.4f);
            var angle=Quaternion.Euler(-9,23,-14);
            Vector3 Place(float x,float y,float z)=>at+angle*new Vector3(x,y,z);
            Color iron=new(.153f,.085f,.059f),coating=new(.244f,.155f,.102f),patina=new(.087f,.20f,.182f);
            float[] stations={-3.1f,-2.36f,-1.47f,.14f,1.12f,2.16f,3.08f};
            float[] beams={.63f,1.05f,1.19f,1.26f,1.19f,.90f,.31f};
            var keel=new Vector3[stations.Length];
            var thickness=new float[stations.Length];
            for(int i=0;i<stations.Length;i++)
            {
                float along=stations[i],span=beams[i];
                keel[i]=Place(0,.17f,along); thickness[i]=.098f;
                var rib=new[]{
                    Place(-span,1.10f,along),Place(-span*.86f,.54f,along),
                    keel[i],Place(span*.84f,.57f,along),Place(span,1.06f+(i%3==0?.42f:.02f),along)
                };
                wreck.Tube(rib,new[]{.087f,.104f,.11f,.094f,.058f},i%2==0?coating:iron,7,.8f);
                if(i==0 || i==3 || i==4)continue;
                float before=stations[i-1],width=beams[i-1];
                for(int side=-1;side<=1;side+=2)
                {
                    float lip=side<0?1.0f:.78f;
                    Flat(wreck,Place(side*width*.64f,.44f,before),Place(side*span*.64f,.41f,along),
                        Place(side*span,lip,along),Place(side*width,lip,before),i%2==0?patina:coating);
                    if(i==5 && side>0)continue;
                    wreck.Tube(new[]{Place(side*width,lip+.07f,before),Place(side*span,lip+.07f,along)},
                        new[]{.058f,.048f},iron,5);
                }
            }
            wreck.Tube(keel,thickness,iron,8);
            wreck.Tube(new[]{Place(-.74f,.26f,1.78f),Place(.74f,.38f,1.62f),Place(.96f,.45f,1.34f)},
                new[]{.068f,.054f,.026f},coating,6);
            return wreck;
        }

        MeshCollider BuildRift()
        {
            var bed=Terrain(RiftHeight,true,DeepRouteTint);
            var rocks=new ReefGeometry();
            var glow=new ReefGeometry();
            // Named needle groups hold the walls; the gaps between them are the swim-throughs.
            BuildDeepLandmarks(bed,rocks,glow);
            RiftSaddle(bed,rocks,glow);
            SurveyTrail(true,abyss.transform);
            Vector2[] vents={new(31.5f,-44.5f),new(43,-46.5f),new(53.5f,-53.5f),new(55.6f,-52.4f),new(50.5f,-66.5f)};
            foreach(var at in vents)Vent(rocks,glow,new Vector3(at.x,RiftHeight(at.x,at.y)-.09f,at.y));
            Publish("Deep talus and extinct mineral vents",rocks,basalt,false,abyss.transform);
            Publish("Fissure vents and cold-light lantern colonies",glow,bioluminescence,false,abyss.transform)
                .GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var ground=Publish("Needle Rift floor and collision",bed,basalt,true,abyss.transform);
            return ground.GetComponent<MeshCollider>();
        }

        Vector3 BasaltNeedle(ReefGeometry mesh,Vector3 at,float radius,float height)
        {
            const int sides=6;
            float heading=Range(0,6.28f),lean=Range(-.20f,.21f);
            float[] taper={1,.90f,.79f,.74f,.43f,.28f};
            float[] layers={0,.14f,.43f,.52f,.91f,1};
            var edge=new float[sides];
            for(int i=0;i<sides;i++)edge[i]=Range(.82f,1.12f);
            var ridge=new Vector3[layers.Length,sides];
            for(int row=0;row<layers.Length;row++)
            for(int i=0;i<sides;i++)
            {
                float a=heading+2*Mathf.PI*i/sides,t=layers[row];
                float r=radius*taper[row]*edge[i];
                ridge[row,i]=at+new Vector3(Mathf.Cos(a)*r+t*height*lean,height*(t+.041f*Mathf.Sin(a)*t),Mathf.Sin(a)*r*.81f+t*radius*.43f);
            }
            int last=layers.Length-1;
            var crown=at+new Vector3(height*lean,height+.19f,radius*.43f);
            for(int i=0;i<sides;i++)
            {
                float tone=Range(.79f,1.14f);
                int j=(i+1)%sides;
                for(int row=1;row<=last;row++)
                    Flat(mesh,ridge[row-1,i],ridge[row,i],ridge[row,j],ridge[row-1,j],
                        Color.Lerp(RiftSlate,Mineral,layers[row]*.56f)*tone*(row==3?.57f:1));
                int center=mesh.Add(crown,Mineral*tone);
                int a=mesh.Add(ridge[last,j],Mineral),b=mesh.Add(ridge[last,i],Mineral);
                mesh.Face(center,a,b);
            }
            return crown;
        }

        void Vent(ReefGeometry rocks,ReefGeometry glow,Vector3 at)
        {
            int count=random.Next(4,7);
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f,r=.27f+i*.11f;
                var root=at+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                var shoulder=root+new Vector3(Mathf.Sin(a)*.12f,Range(.31f,.78f),Mathf.Cos(a)*.15f);
                var crown=shoulder+Vector3.up*.16f;
                float rim=Range(.055f,.096f);
                rocks.Tube(new[]{root,shoulder,crown},new[]{rim*1.57f,rim*.89f,rim},RiftSlate*1.19f,6);
                if(i%3==1)continue;
                glow.Tube(new[]{crown,crown+new Vector3(Mathf.Cos(a)*.022f,.045f,Mathf.Sin(a)*.022f)},
                    new[]{rim*.67f,rim*.51f},new Color(.092f,.42f,.415f),6,.72f);
            }
        }
    }
}
