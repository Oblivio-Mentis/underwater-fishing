using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed partial class NereidReef : MonoBehaviour
    {
        readonly List<Object> owned=new();
        readonly List<GameObject> retiredFloors=new();
        readonly System.Random random=new(17291);
        Material rock, bedMaterial, basalt, bioluminescence, lightColumns;
        GameObject shoal, abyss;
        int shownDepth;
        float Range(float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());

        void Awake()
        {
            var reefShader=Resources.Load<Shader>("NereidReef");
            var shaftShader=Resources.Load<Shader>("NereidLightColumns");
            if(reefShader==null || shaftShader==null)return;
            rock=CreateMaterial(reefShader,"Weathered reef stone and coral");
            bedMaterial=CreateMaterial(reefShader,"Current-washed limestone sand");
            bedMaterial.SetFloat("_Sand",1);
            basalt=CreateMaterial(reefShader,"Thin-bedded abyssal basalt");
            basalt.SetFloat("_Rift",1);
            bioluminescence=CreateMaterial(reefShader,"Abyssal cold-water polyps");
            bioluminescence.SetFloat("_Glow",1.38f);
            lightColumns=CreateMaterial(shaftShader,"Suspended water light");
            LoadRockForms();

            shoal=Region("01 / The Pale Shoals");
            abyss=Region("02 / The Needle Rift");
            var shallowBed=BuildShoal();
            var deepBed=BuildRift();
            Physics.SyncTransforms();
            // The berth masks in RiftHeight keep salvage at the same physical elevations on both maps.
            NereidSalvage.Place(shallowBed,rock,owned);
            gameObject.AddComponent<NereidWaterLife>().Build(shallowBed,deepBed);
            WaterBackground();
            RetireFlatFloors();
            ShowDepth(1);
        }

        Material CreateMaterial(Shader shader,string label)
        {
            var material=new Material(shader){name=label};
            owned.Add(material);
            return material;
        }

        GameObject Region(string label)
        {
            var region=new GameObject(label);
            region.transform.SetParent(transform,false);
            return region;
        }

        void RetireFlatFloors()
        {
            // NereidAuthoring still supplies an emergency deck-wide bed. Its flat visual
            // and collider must not bridge the new recessed rift when this component builds.
            var authoredBed=transform.parent.Find("Industrial fishing seabed");
            if(authoredBed!=null && authoredBed.gameObject.activeSelf)retiredFloors.Add(authoredBed.gameObject);
            foreach(var root in gameObject.scene.GetRootGameObjects())
                if(root.name=="Seabed" && root.activeSelf)retiredFloors.Add(root);
            foreach(var floor in retiredFloors)floor.SetActive(false);
        }

        void Update()
        {
            if(shoal==null || abyss==null)return;
            ShowDepth(RaidWorld.Instance!=null && RaidWorld.Instance.CurrentDepth>=2?2:1);
        }

        void ShowDepth(int depth)
        {
            if(shownDepth==depth)return;
            shownDepth=depth;
            shoal.SetActive(depth==1);
            abyss.SetActive(depth==2);
        }

        MeshCollider BuildShoal()
        {
            var land=Terrain(ShoalHeight,false);
            // The first pool is open from the cargo lift. A split arch marks the far haul route.
            var shelves=new[]{
                new Vector4(30,-39,3.5f,6.3f),new(34,-41,2.8f,4.6f),new(40,-24,4,5.5f),
                new(50,-43,2.7f,4.4f),new(55,-41,3.6f,6.5f),new(17,-48,4,4),
                new(23,-53,3.6f,4.5f),new(61,-59,4.4f,9.2f),new(56,-63,3.7f,5.1f),
                new(42,-10,5.3f,5.2f),new(65,-29,4.9f,10),new(29,-44,2.0f,3.2f)
            };
            var tops=new List<Vector3>();
            foreach(var s in shelves)tops.Add(Rock(land,new Vector3(s.x,ShoalHeight(s.x,s.y)-.32f,s.y),s.z,s.w));
            var life=new ReefGeometry();
            ShelfGate(land,life);
            Escarpment(land,0);Escarpment(land,1);
            var ground=Publish("Pale Shoals / shelf terraces and sand channels",land,bedMaterial,true,shoal.transform);

            for(int group=0;group<30;group++)
            {
                float x=Range(16,60),z=Range(-65,-15);
                if(Vector2.Distance(new Vector2(x,z),new Vector2(23,-28))<5)continue;
                var place=new Vector3(x,ShoalHeight(x,z),z);
                int fronds=random.Next(4,9);
                for(int i=0;i<fronds;i++) Kelp(life,place+new Vector3(Range(-1.6f,1.6f),0,Range(-1.6f,1.6f)),Range(.65f,2.1f));
            }
            var rubble=new ReefGeometry();
            for(int j=0;j<shelves.Length;j++)
            {
                var s=shelves[j];
                if(j%2==0)Fan(life,tops[j]-Vector3.up*.07f,j*1.27f);
                for(int k=0;k<9;k++)
                {
                    float angle=Range(0,6.28f),radius=Range(.96f,1.8f)*s.z;
                    float x=s.x+Mathf.Cos(angle)*radius,z=s.y+Mathf.Sin(angle)*radius;
                    if(NereidSalvage.Reserved(x,z))continue;
                    var p=new Vector3(x,ShoalHeight(x,z)-.12f,z);
                    float pebble=Range(.28f,1.02f);
                    ScatterStone(rubble,p,pebble,new Color(.16f,.213f,.186f));
                    if(new Vector2(x-23,z+28).sqrMagnitude<23)continue;
                    if(k<3)
                    {
                        int stems=random.Next(4,9);
                        for(int q=0;q<stems;q++)
                        {
                            var at=p+new Vector3(Range(-.8f,.8f),.06f,Range(-.8f,.8f));
                            Sponge(life,at,Range(.4f,.94f));
                        }
                        Fan(life,p+Vector3.right*pebble*.6f,angle);
                        if(k==1)Coral(life,p+Vector3.forward*.7f,Range(.72f,1.32f),j%2==0);
                    }
                    else for(int q=0;q<5;q++)
                        Kelp(life,p+new Vector3(Range(-.85f,.85f),.04f,Range(-.85f,.85f)),Range(.24f,.65f));
                }
            }
            BuildCoralGarden(life,rubble);
            Publish("Washed cobbles around the shelves",rubble,rock,false,shoal.transform);
            Publish("Fan corals, sponges and ribbon kelp",life,rock,false,shoal.transform);
            Publish("Broken survey skiff",SurveyWreck(),rock,true,shoal.transform);
            ExpeditionWreck();
            SurveyTrail(false,shoal.transform);
            var beams=new ReefGeometry();
            Vector2[] pools={new(22,-34),new(29,-24),new(32,-38),new(39,-43),new(49,-52),new(52,-43),new(43,-64),new(57,-74),new(35,-65),new(-19,14),new(19,10),new(14,27)};
            foreach(var pool in pools)
            {
                var bottom=new Vector3(pool.x,ShoalHeight(pool.x,pool.y)+.1f,pool.y);
                beams.Tube(new[]{bottom,bottom+new Vector3(-5,19,2.4f),bottom+new Vector3(-9.5f,42,4.4f)},
                    new[]{Range(1.7f,3.6f),Range(1.15f,2.3f),Range(.46f,.99f)},Color.white,11);
            }
            Publish("Sunbeams over the fishing grounds",beams,lightColumns,false,shoal.transform);
            return ground.GetComponent<MeshCollider>();
        }

        static float BedHeight(float x,float z)
        {
            // Keep the cargo pad level with the original, collidable seabed.
            float pad=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,10,Vector2.Distance(new(x,z),new(23,-28))));
            return -17.47f+pad*(Mathf.PerlinNoise(x*.13f,z*.12f)*.83f+Mathf.Sin(z*.7f+x*.28f)*.12f);
        }

        Vector3 Rock(ReefGeometry mesh,Vector3 basePoint,float width,float height)
        {
            const int sides=8;
            var radii=new float[sides];
            for(int i=0;i<sides;i++)radii[i]=Range(.77f,1.15f);
            float spin=Range(0,6),lean=Range(-.27f,.27f);
            float[] shape={1,.93f,.83f,.86f,.70f,.68f,.47f,.36f},elevation={0,.18f,.35f,.40f,.61f,.72f,.96f,1};
            var rings=new Vector3[shape.Length,sides];
            for(int j=0;j<shape.Length;j++)for(int i=0;i<sides;i++)
            {
                float angle=spin+2*Mathf.PI*i/sides,r=width*radii[i]*shape[j];
                float dip=.08f*height*Mathf.Sin(angle+.55f)*elevation[j];
                rings[j,i]=basePoint+new Vector3(Mathf.Cos(angle)*r+lean*height*elevation[j],height*elevation[j]+dip,Mathf.Sin(angle)*r*.81f);
            }
            var basalt=new Color(.037f,.073f,.098f);
            var lichen=new Color(.106f,.162f,.145f);
            int last=shape.Length-1;
            var top=basePoint+new Vector3(lean*height,height,0);
            for(int i=0;i<sides;i++)
            {
                int next=(i+1)%sides;
                float vein=Range(.79f,1.09f);
                for(int j=1;j<shape.Length;j++)
                {
                    var hue=Color.Lerp(basalt,lichen,elevation[j]*.77f)*vein*(j==3?.66f:1);
                    Flat(mesh,rings[j-1,i],rings[j,i],rings[j,next],rings[j-1,next],hue);
                }
                int c=mesh.Add(top,lichen),a=mesh.Add(rings[last,next],lichen*vein),b=mesh.Add(rings[last,i],lichen);
                mesh.Face(c,a,b);
            }
            return top;
        }

        static void Flat(ReefGeometry mesh,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tone)
        {
            int first=mesh.Add(a,tone*.94f);mesh.Add(b,tone);mesh.Add(c,tone*1.03f);mesh.Add(d,tone);
            mesh.Quad(first,first+1,first+2,first+3);
        }

        void Escarpment(ReefGeometry mesh,int layer)
        {
            // Folded ridges leave low saddles between exposed spurs around the cove.
            int count=layer==0?87:108;
            var points=new Vector3[count,4];
            var center=new Vector3(16,0,-25);
            for(int i=0;i<count;i++)
            {
                float angle=2*Mathf.PI*i/count;
                var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                float radius=(layer==0?56:88)+4*Mathf.Sin(angle*5+layer)+Range(-2.2f,2.2f);
                float strata=Mathf.PerlinNoise(5+Mathf.Cos(angle)*(3+layer),12+Mathf.Sin(angle)*(3+layer));
                float shelf=(layer==0?4:11)+(layer==0?30:49)*Mathf.Pow(strata,1.35f)+4*Mathf.Sin(angle*7+layer);
                var foot=center+outward*radius;
                float floor=ShoalHeight(foot.x,foot.z)-1.2f;
                points[i,0]=foot+Vector3.up*floor;
                points[i,1]=foot+outward*2.3f+Vector3.up*(floor+.38f*shelf);
                points[i,2]=foot+outward*5.4f+Vector3.up*(floor+shelf);
                points[i,3]=foot+outward*12+Vector3.up*(floor+shelf*Range(.62f,.86f));
            }
            for(int i=0;i<count;i++)
            {
                int next=(i+1)%count;
                for(int j=0;j<3;j++)
                {
                    var tone=Color.Lerp(new Color(.018f,.050f,.073f),new Color(.075f,.124f,.131f),(.2f+j*.28f)*Range(.76f,1.15f));
                    Flat(mesh,points[i,j],points[next,j],points[next,j+1],points[i,j+1],tone);
                }
            }
        }

        static void Sponge(ReefGeometry mesh,Vector3 p,float size)
        {
            const int sides=7;
            var axis=new Vector3(.13f,.92f,.11f)*size;
            var tint=new Color(.32f,.159f,.092f);
            for(int i=0;i<sides;i++)
            {
                float a=i*(2*Mathf.PI/sides),b=(i+1)*(2*Mathf.PI/sides);
                var va=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*size;
                var vb=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*size;
                Flat(mesh,p+va*.11f,p+axis+va*.16f,p+axis+vb*.16f,p+vb*.11f,tint);
                Flat(mesh,p+axis+va*.16f,p+axis+va*.096f,p+axis+vb*.096f,p+axis+vb*.16f,tint*1.48f);
                Flat(mesh,p+axis+va*.096f,p+axis*.78f+va*.059f,p+axis*.78f+vb*.059f,p+axis+vb*.096f,tint*.24f);
            }
        }

        void WaterBackground()
        {
            var shader=Resources.Load<Shader>("NereidWaterDistance");
            if(shader==null)return;
            var shell=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name="Water column beyond the cove";shell.transform.SetParent(transform,false);
            // The shader draws at camera-relative far depth for every observer.
            var collider=shell.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var backdrop=new Material(shader);owned.Add(backdrop);
            var renderer=shell.GetComponent<MeshRenderer>();renderer.sharedMaterial=backdrop;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*4000);
        }

        void Kelp(ReefGeometry mesh,Vector3 p,float size)
        {
            if(NereidSalvage.Reserved(p.x,p.z))return;
            float yaw=Range(0,6);
            var d=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw));
            var side=Vector3.Cross(Vector3.up,d);
            int start=0;
            for(int i=0;i<=5;i++)
            {
                float t=i/5f;
                var mid=p+Vector3.up*(size*2.1f*t)+d*size*(.5f*t+.42f*t*t)+side*Mathf.Sin(t*6+yaw)*size*.17f;
                float w=(Mathf.Sin(t*Mathf.PI)*.21f+.01f)*size;
                var colour=Color.Lerp(new Color(.02f,.069f,.06f),new Color(.105f,.245f,.122f),t);
                int a=mesh.Add(mid-side*w,colour);mesh.Add(mid+d*w*.25f,colour*1.1f);mesh.Add(mid+side*w,colour);
                if(i==0){start=a;continue;}
                for(int k=0;k<2;k++){int q=start+(i-1)*3+k;mesh.Quad(q,q+3,q+4,q+1);}
            }
        }

        static void Fan(ReefGeometry mesh,Vector3 p,float angle)
        {
            Vector3 axis=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
            Vector3 curl=Vector3.Cross(axis,Vector3.up);
            var tint=Color.Lerp(new Color(.31f,.119f,.17f),new Color(.58f,.338f,.176f),Mathf.Abs(Mathf.Sin(angle)));
            for(int k=-4;k<=4;k++)
            {
                float arm=k*.25f;
                Vector3 stem=p+axis*(arm*.56f)+Vector3.up*(.67f-.10f*Mathf.Abs(arm))+curl*(arm*arm*.14f);
                Vector3 tip=stem+axis*(arm*.48f)+Vector3.up*(.51f-.18f*Mathf.Abs(arm))+curl*.11f;
                mesh.Tube(new[]{p,stem,tip},new[]{.046f,.031f,.009f},tint,5);
                var from=Vector3.Lerp(stem,tip,.29f+.07f*Mathf.Sin(k*4.13f));
                var fork=from+Vector3.up*(.16f+.035f*Mathf.Cos(k*3.2f))-axis*.145f;
                mesh.Tube(new[]{stem,from,fork},new[]{.026f,.019f,.008f},tint*1.16f,5);
            }
        }

        GameObject Publish(string label,ReefGeometry geometry,Material material,bool solid,Transform parent)
        {
            var piece=new GameObject(label);
            // Authored points are world-space metres, independent of the 2x vessel scale.
            piece.transform.SetParent(parent,false);
            var mesh=geometry.Mesh(label);owned.Add(mesh);
            piece.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            if(material==lightColumns){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
            if(solid)piece.AddComponent<MeshCollider>().sharedMesh=mesh;
            return piece;
        }

        void OnDestroy()
        {
            foreach(var floor in retiredFloors)if(floor!=null)floor.SetActive(true);
            foreach(var asset in owned)if(asset!=null)Destroy(asset);
        }
    }
}
