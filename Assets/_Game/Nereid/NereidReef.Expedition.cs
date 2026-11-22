using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class NereidReef
    {
        static readonly Color PressureSteel=new(.114f,.203f,.218f);
        static readonly Color RubbedSteel=new(.201f,.249f,.238f);
        static readonly Color Rust=new(.314f,.151f,.079f);
        static readonly Color TapeGold=new(.61f,.37f,.085f);
        static readonly Color ReturnMarker=new(.86f,.46f,.10f);
        static readonly Color CyanMarker=new(.07f,.68f,.82f);
        static readonly Color BluePolyp=new(.074f,.35f,.93f);
        static readonly Color VioletPolyp=new(.34f,.12f,.75f);
        readonly Dictionary<string,ReefProp> reefProps=new();

        sealed class ReefProp
        {
            internal readonly List<Vector3> points=new();
            internal readonly List<int> indices=new();
            internal Bounds bounds;

            internal ReefProp(GameObject source)
            {
                foreach(var part in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh=part.sharedMesh;
                    if(mesh==null || !mesh.isReadable || mesh.vertexCount==0)continue;
                    var matrix=source.transform.worldToLocalMatrix*part.transform.localToWorldMatrix;
                    int offset=points.Count;
                    foreach(var vertex in mesh.vertices)
                    {
                        var position=matrix.MultiplyPoint3x4(vertex);
                        if(points.Count==0)bounds=new Bounds(position,Vector3.zero);
                        else bounds.Encapsulate(position);
                        points.Add(position);
                    }
                    foreach(int index in mesh.triangles)indices.Add(index+offset);
                }
            }
        }

        // Imported props share the batch and underwater shading used by their authored surroundings.
        bool PlaceReefProp(ReefGeometry geometry,string name,Vector3 at,float size,float yaw,Color color)
        {
            if(!reefProps.TryGetValue(name,out var prop))
            {
                var preset=Resources.Load<GameObject>("ExpeditionAssets/ReodesuxzLocal/"+name);
                prop=preset==null?null:new ReefProp(preset);
                reefProps[name]=prop;
            }
            if(prop==null || prop.indices.Count==0 || prop.bounds.size.y<.01f)return false;
            var pivot=new Vector3(prop.bounds.center.x,prop.bounds.min.y,prop.bounds.center.z);
            var scale=size/Mathf.Max(prop.bounds.size.x,prop.bounds.size.y,prop.bounds.size.z);
            var rotation=Quaternion.Euler(0,yaw,0);
            int start=-1;
            foreach(var point in prop.points)
            {
                float elevation=(point.y-pivot.y)/prop.bounds.size.y;
                int index=geometry.Add(at+rotation*((point-pivot)*scale),color*Mathf.Lerp(.52f,1.36f,elevation));
                if(start<0)start=index;
            }
            for(int i=0;i<prop.indices.Count;i+=3)
                geometry.Face(start+prop.indices[i],start+prop.indices[i+1],start+prop.indices[i+2]);
            return true;
        }

        // Blended bathymetric breaks leave two benches and a gradual approach to their cliffs.
        static float Bench(float x,float z,float atX,float atZ,float width,float length,float turn)
        {
            float dx=x-atX,dz=z-atZ;
            float c=Mathf.Cos(turn),s=Mathf.Sin(turn);
            var p=new Vector2((dx*c+dz*s)/width,(dz*c-dx*s)/length);
            float outline=p.magnitude+.13f*(Mathf.PerlinNoise(x*.17f+8,z*.16f+13)-.5f);
            float bank=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1.12f,outline));
            float step=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.62f,outline));
            return .64f*bank+.36f*step;
        }

        static float ExpeditionShelf(float x,float z)
        {
            float west=6.1f*Bench(x,z,29,-56,8.3f,15,-.19f);
            float east=8.3f*Bench(x,z,60,-48,10.3f,19,.28f);
            float crown=9.2f*Bench(x,z,72,-78,10.7f,15,-.38f);
            float spur=6.4f*Bench(x,z,29,-82,10,13,.21f);
            float crest=Mathf.Max(Mathf.Max(west,east),Mathf.Max(crown,spur));
            float basin=5.2f*Bench(x,z,49,-72,16,20,-.37f);
            return crest-basin*(1-Mathf.Clamp01(crest*.12f));
        }

        static float ExpeditionRift(float x,float z)
        {
            float west=5.6f*Bench(x,z,31,-69,11,21,.32f);
            float east=7.3f*Bench(x,z,65,-66,10,29,-.28f);
            float crest=8.0f*Bench(x,z,57,-99,16,11,.38f);
            float drop=16.3f*Bench(x,z,51,-77,15.4f,29,-.27f);
            float fissure=7.6f*Bench(x,z,46,-88,7.9f,15,-.15f);
            return Mathf.Max(west,Mathf.Max(east,crest))-drop-fissure;
        }

        void ShelfGate(ReefGeometry spurs,ReefGeometry life)
        {
            // Either climb over the crest or keep the wreck framed through the span.
            var arch=new ReefGeometry();
            var curve=new[]{
                new Vector3(33.7f,ShoalHeight(33.7f,-47f)-.8f,-47f),
                new(35.5f,-9.0f,-48.2f),new(37.9f,-5.0f,-48.8f),
                new(41.1f,-3.5f,-49.1f),new(45.0f,-3.2f,-48.7f),
                new(48.7f,-5.7f,-48.1f),new(51.5f,-10.3f,-48.8f),
                new(53.5f,ShoalHeight(53.5f,-49.6f)-.8f,-49.6f)
            };
            arch.Tube(curve,new[]{2.75f,2.48f,2.18f,1.96f,2.22f,2.08f,2.51f,2.8f},
                new Color(.135f,.198f,.191f),9,1.16f);
            for(int i=1;i<curve.Length-1;i++)
            {
                Vector3 at=curve[i]+Vector3.up*2.04f;
                ScatterStone(life,at,Range(.7f,1.35f),Chalk*.79f);
                if(i%2!=0)Coral(life,at,Range(.91f,1.25f),i%3!=1);
            }
            Publish("Ridge gate / limestone arch",arch,rock,true,shoal.transform);
            Vector4[] places={
                new(30,-52,3.5f,8.2f),new(25,-63,4.9f,13.4f),new(29,-69,4.7f,11),
                new(36,-68,2.9f,6.4f),new(23,-81,5.8f,13.5f),new(62,-42,4.3f,10),
                new(62,-54,4.1f,12),new(66,-62,4.7f,16.6f),new(71,-68,4.1f,13),
                new(71,-77,4.4f,9.5f),new(64,-82,3.7f,10),new(56,-90,4.4f,14),
                new(35,-91,4.5f,12.5f)
            };
            foreach(var f in places)
            {
                var point=new Vector3(f.x,ShoalHeight(f.x,f.y)-.5f,f.y);
                var top=Rock(spurs,point,f.z,f.w);
                for(int i=0;i<3;i++)ScatterStone(life,top+new Vector3(Range(-.92f,.92f),-.14f,Range(-.85f,.85f)),
                    Range(.71f,1.45f),Chalk*.86f);
                if(f.w<13)Fan(life,top-Vector3.up*.17f,f.x*.6f);
            }
        }

        void RiftSaddle(ReefGeometry stone,ReefGeometry talus,ReefGeometry glow)
        {
            Vector4[] spires={
                new(34,-61,3.4f,13),new(34,-68,3.9f,18.4f),new(37,-73,2.9f,16.5f),
                new(34,-83,4.2f,19),new(35,-92,3.5f,18),new(38,-99,4.7f,24.5f),
                new(63,-65,3.9f,11.5f),new(65,-74,3.8f,17),new(65,-84,3.6f,13.3f),
                new(61,-93,3.7f,16.5f),new(57,-101,4.4f,18.8f),new(50,-106,4.0f,15.4f)
            };
            foreach(var f in spires)
            {
                var at=new Vector3(f.x,RiftHeight(f.x,f.y)-.5f,f.y);
                BasaltNeedle(stone,at,f.z,f.w);
                var rubble=at+new Vector3(Range(-2.8f,2.8f),0,Range(-3.3f,3.3f));
                rubble.y=RiftHeight(rubble.x,rubble.z)-.15f;
                ScatterStone(talus,rubble,Range(1.6f,2.8f),Mineral*.76f);
            }
            var arch=new Vector3[]{
                new(37.3f,RiftHeight(37.3f,-71.7f)-.65f,-71.7f),new(39.9f,-19.6f,-72.2f),
                new(43.8f,-16.7f,-73.6f),new(50.8f,-15.9f,-74.4f),
                new(56.1f,-17.8f,-75.1f),new(61.9f,RiftHeight(61.9f,-75.3f)-.7f,-75.3f)
            };
            stone.Tube(arch,new[]{2.64f,2.42f,1.9f,2.18f,2.2f,2.4f},RiftSlate,8,1.21f);
            Vector2[] fields={
                new(42,-56),new(50,-63),new(42,-67),new(56,-69),new(41.5f,-77),
                new(51,-80),new(43,-86),new(48.4f,-88.5f),new(53.2f,-91.4f),new(47,-95)
            };
            for(int j=0;j<fields.Length;j++)
            for(int i=0;i<7;i++)
            {
                float angle=i*2.39996f+Range(-.21f,.21f),radius=.32f+i*.42f;
                var p=new Vector3(fields[j].x+Mathf.Cos(angle)*radius,0,fields[j].y+Mathf.Sin(angle)*radius);
                p.y=RiftHeight(p.x,p.z)-.13f;
                float size=i==0?Range(1.3f,2.45f):Range(.34f,1.25f);
                var hue=j%3==1?VioletPolyp:BluePolyp;
                if(i%3!=2 || !PlaceReefProp(glow,"sea_anemone",p,size,angle*Mathf.Rad2Deg,hue))
                    Lantern(talus,glow,p,size,hue);
                if(i%3==0)ScatterStone(talus,p-Vector3.up*.18f,Range(.84f,1.55f),RiftSlate);
            }
        }

        void Lantern(ReefGeometry stalk,ReefGeometry glow,Vector3 foot,float size,Color signal)
        {
            var shoulder=foot+new Vector3(Range(-.2f,.2f),.71f,Range(-.21f,.21f))*size;
            var crown=shoulder+Vector3.up*.37f*size;
            stalk.Tube(new[]{foot,Vector3.Lerp(foot,shoulder,.54f),shoulder,crown},
                new[]{.149f*size,.065f*size,.079f*size,.15f*size},Color.Lerp(RiftSlate,signal,.59f),7);
            const int blades=11;
            int first=glow.Add(crown+Vector3.up*.24f*size,signal*.85f);
            for(int ring=0;ring<3;ring++)
            {
                float r=ring==0?.29f:ring==1?.58f:.65f;
                float lift=ring==0?.21f:ring==1?.10f:.035f;
                for(int i=0;i<blades;i++)
                {
                    float a=i*Mathf.PI*2/blades;
                    float s=size*(1+.073f*Mathf.Sin(3*a+foot.x));
                    glow.Add(crown+s*new Vector3(Mathf.Cos(a)*r,lift,Mathf.Sin(a)*r),signal*(ring==2?1.61f:1.08f));
                }
            }
            for(int i=0;i<blades;i++)
            {
                int n=(i+1)%blades;
                glow.Face(first,first+1+n,first+1+i);
                for(int layer=0;layer<2;layer++)
                {
                    int a=first+1+layer*blades,b=a+blades;
                    glow.Quad(a+i,a+n,b+n,b+i);
                }
                if(i%2!=0)continue;
                float angle=i*Mathf.PI*2/blades;
                var edge=crown+new Vector3(Mathf.Cos(angle)*.59f,.06f,Mathf.Sin(angle)*.59f)*size;
                glow.Tube(new[]{crown-Vector3.up*.09f*size,Vector3.Lerp(crown,edge,.75f)-Vector3.up*.08f*size,edge},
                    new[]{.019f*size,.017f*size,.012f*size},signal*1.32f,4);
            }
        }

        void ExpeditionWreck()
        {
            var shell=new ReefGeometry();
            var trim=new ReefGeometry();
            var lamps=new ReefGeometry();
            var center=new Vector3(47.5f,ShoalHeight(47.5f,-68)+3.6f,-68);
            var attitude=Quaternion.Euler(-6,-27,12);
            Vector3 Local(float x,float y,float z)=>center+attitude*new Vector3(x,y,z);
            const int sides=12,bays=7;
            Vector3 Ring(int sector,int bay,float inset=0)
            {
                float a=sector*Mathf.PI*2/sides;
                float taper=bay==0||bay==bays?.9f:1;
                return Local(Mathf.Cos(a)*(3.7f-inset)*taper,Mathf.Sin(a)*(3.2f-inset)*taper,(bay-3.5f)*2.10f);
            }
            for(int bay=0;bay<=bays;bay++)
            {
                for(int sector=0;sector<sides;sector++)
                {
                    bool breach=bay>=3 && bay<=5 && sector>=1 && sector<=4;
                    if(breach || bay==bays && sector>=7 && sector<=9)continue;
                    var a=Ring(sector,bay);
                    var b=Ring(sector+1,bay);
                    shell.Tube(new[]{a,b},new[]{.21f,.21f},bay%3==0?Rust:RubbedSteel,5);
                    if(bay==bays || (bay*19+sector*7)%11<3 || bay<=1 && (sector==0 || sector==5))continue;
                    var d=Ring(sector,bay+1);
                    var hue=(bay*3+sector)%5==0?Rust:PressureSteel*Range(.74f,1.23f);
                    Flat(shell,a,b,Ring(sector+1,bay+1),d,hue);
                    Flat(shell,Ring(sector,bay,.13f),Ring(sector,bay+1,.13f),
                        Ring(sector+1,bay+1,.13f),Ring(sector+1,bay,.13f),hue*.65f);
                    if(sector%4==0)trim.Tube(new[]{a,d},new[]{.098f,.098f},TapeGold*.66f,5);
                }
                if(bay==bays)continue;
                float start=(bay-3.5f)*2.10f;
                if(bay!=3)Flat(shell,Local(-2.2f,-1.61f,start),Local(-2.2f,-1.61f,start+2.10f),
                    Local(2.2f,-1.61f,start+2.10f),Local(2.2f,-1.61f,start),RubbedSteel*.59f);
                if(bay%2!=0)continue;
                trim.Tube(new[]{Local(-1.68f,1.07f,start-.49f),Local(-1.68f,1.07f,start+.49f)},new[]{.13f,.13f},PressureSteel,6);
                lamps.Tube(new[]{Local(-1.68f,.96f,start-.42f),Local(-1.68f,.96f,start+.42f)},new[]{.065f,.065f},ReturnMarker,6);
            }
            Publish("Survey wreck / flooded pressure fuselage",shell,rock,true,shoal.transform);
            var anchorAt=new Vector3(43.4f,ShoalHeight(43.4f,-62.9f)-.08f,-62.9f);
            if(!PlaceReefProp(trim,"ship_anchor",anchorAt,2.4f,49,Rust))
            {
                trim.Tube(new[]{anchorAt,anchorAt+Vector3.up*1.05f,anchorAt+Vector3.up*1.88f},
                    new[]{.22f,.15f,.12f},Rust,8);
                trim.Tube(new[]{anchorAt+new Vector3(-.91f,.55f,0),anchorAt,anchorAt+new Vector3(.91f,.55f,0)},
                    new[]{.094f,.23f,.094f},PressureSteel,7);
            }
            Publish("Survey wreck / cable runs and warning paint",trim,rock,false,shoal.transform);
            Publish("Survey wreck / failing amber circuits",lamps,bioluminescence,false,shoal.transform);
        }

        void SurveyTrail(bool deep,Transform region)
        {
            Vector2[] route=deep
                ? new Vector2[]{new(26,-29),new(32,-37.5f),new(39,-45.4f),new(46,-55.8f),
                    new(50,-65),new(47.8f,-73.4f),new(46,-82),new(47,-91.2f)}
                : new Vector2[]{new(26,-29),new(33,-36),new(41,-43),new(44.2f,-54),
                    new(44,-62.6f),new(49.5f,-72.8f),new(54,-82)};
            var towers=new ReefGeometry();
            var lights=new ReefGeometry();
            for(int i=0;i<route.Length;i++)
            {
                var p=route[i];
                float height=deep?RiftHeight(p.x,p.y):ShoalHeight(p.x,p.y);
                var at=new Vector3(p.x,height-.13f,p.y);
                var light=at+Vector3.up*(i==0?2.12f:1.34f);
                towers.Tube(new[]{at,Vector3.Lerp(at,light,.24f),light},new[]{.4f,.105f,.12f},PressureSteel,6);
                towers.Tube(new[]{light-Vector3.up*.19f,light+Vector3.up*.30f},new[]{.28f,.23f},TapeGold*.80f,8);
                var tone=i<=1?ReturnMarker:CyanMarker;
                lights.Tube(new[]{light+Vector3.up*.135f,light+Vector3.up*.265f},new[]{.287f,.244f},tone,8);
                for(int foot=0;foot<3;foot++)
                {
                    float a=foot*Mathf.PI*2/3;
                    var step=at+new Vector3(Mathf.Sin(a)*.45f,.19f,Mathf.Cos(a)*.45f);
                    towers.Tube(new[]{step,at+Vector3.up*.4f},new[]{.07f,.069f},Rust,5);
                }
            }
            Publish("Survey trail / return beacons",towers,rock,false,region);
            var lenses=Publish("Survey trail / luminous lenses",lights,bioluminescence,false,region)
                .GetComponent<MeshRenderer>();
            lenses.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}
