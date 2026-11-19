using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class NereidReef : MonoBehaviour
    {
        readonly List<Object> owned=new();
        readonly System.Random random=new(17291);
        Material rock, lightColumns;
        float Range(float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());

        void Awake()
        {
            var reefShader=Resources.Load<Shader>("NereidReef");
            var shaftShader=Resources.Load<Shader>("NereidLightColumns");
            if(reefShader==null || shaftShader==null)return;
            rock=new Material(reefShader){name="Basalt, sand and coral"};owned.Add(rock);
            lightColumns=new Material(shaftShader){name="Suspended water light"};owned.Add(lightColumns);

            var land=new ReefGeometry();
            const int columns=28,rows=30;
            for(int z=0;z<rows;z++) for(int x=0;x<columns;x++)
            {
                float wx=6+2.4f*x,wz=-72+2.4f*z;
                float y=BedHeight(wx,wz);
                var grit=Color.Lerp(new Color(.079f,.19f,.19f),new Color(.25f,.32f,.247f),Mathf.PerlinNoise(wx*.29f,wz*.23f));
                land.Add(new Vector3(wx,y,wz),grit);
                if(x>0&&z>0){int c=z*columns+x;land.Quad(c-columns-1,c-1,c,c-columns);}
            }
            // The first pool is open from the cargo lift. A split arch marks the far haul route.
            var shelves=new[]{
                new Vector4(30,-39,4.4f,8),new(34,-41,3.3f,4.2f),new(40,-24,4,5.5f),
                new(43,-46,4,11),new(53,-47,4,12),new(55,-42,3.5f,6),
                new(17,-48,5,4),new(23,-53,4,5.5f),new(60,-59,7,10),new(52,-63,4,6),
                new(42,-10,6,6),new(63,-29,5,10)
            };
            foreach(var s in shelves) Rock(land,new Vector3(s.x,BedHeight(s.x,s.y)-.32f,s.y),s.z,s.w);
            var archTint=new Color(.145f,.208f,.231f);
            land.Tube(new[]{new Vector3(43,-8,-46),new(44,-5.8f,-46.5f),new(47,-4.3f,-47.2f),new(50,-4.6f,-47),new(52.8f,-6.1f,-47)},
                new[]{1.45f,1.45f,1.25f,1.2f,1.65f},archTint,7,.84f);
            Publish("Basalt arch and tidal sand",land,rock,true);

            var life=new ReefGeometry();
            for(int group=0;group<30;group++)
            {
                float x=Range(16,60),z=Range(-65,-15);
                if(Vector2.Distance(new Vector2(x,z),new Vector2(23,-28))<5)continue;
                var place=new Vector3(x,BedHeight(x,z),z);
                int fronds=random.Next(4,9);
                for(int i=0;i<fronds;i++) Kelp(life,place+new Vector3(Range(-1.6f,1.6f),0,Range(-1.6f,1.6f)),Range(.65f,2.1f));
            }
            // Pale branching fans mark the clam bed; amber sponges mark the basalt notch.
            foreach(var s in shelves)
            {
                var p=new Vector3(s.x-1.4f,BedHeight(s.x,s.y)+s.w*.72f,s.y-.3f);
                for(int i=0;i<5;i++) Fan(life,p+new Vector3(Range(-.75f,.75f),Range(-.4f,.4f),Range(-.7f,.7f)),i*.83f);
            }
            Publish("Ribbon kelp and fan colonies",life,rock,false);
            var beams=new ReefGeometry();
            Vector2[] pools={new(22,-34),new(29,-24),new(32,-38),new(38,-40),new(46,-46),new(52,-43),new(-19,14),new(19,10),new(14,27)};
            foreach(var pool in pools)
            {
                var bottom=new Vector3(pool.x,-16.3f,pool.y);
                beams.Tube(new[]{bottom,bottom+new Vector3(-5,20,2.4f),bottom+new Vector3(-9.5f,39,4.4f)},new[]{Range(1.4f,2.2f),Range(.9f,1.55f),Range(.55f,.95f)},Color.white,12);
            }
            Publish("Sunbeams over the fishing grounds",beams,lightColumns,false);
        }

        static float BedHeight(float x,float z)
        {
            // Keep the cargo pad level with the original, collidable seabed.
            float pad=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,10,Vector2.Distance(new(x,z),new(23,-28))));
            return -17.47f+pad*(Mathf.PerlinNoise(x*.13f,z*.12f)*.83f+Mathf.Sin(z*.7f+x*.28f)*.12f);
        }

        void Rock(ReefGeometry mesh,Vector3 basePoint,float width,float height)
        {
            int sides=9,start=0;
            var radii=new float[sides];
            for(int i=0;i<sides;i++)radii[i]=Range(.78f,1.15f);
            float spin=Range(0,6),lean=Range(-.26f,.26f);
            float[] shape={1,.94f,.73f,.44f,.02f},elevation={0,.31f,.67f,.93f,1};
            for(int j=0;j<shape.Length;j++)for(int i=0;i<sides;i++)
            {
                float angle=spin+2*Mathf.PI*i/sides;
                float r=width*radii[i]*shape[j];
                var point=basePoint+new Vector3(Mathf.Cos(angle)*r+lean*height*elevation[j],height*elevation[j]+(j==0?0:Range(-.18f,.18f)),Mathf.Sin(angle)*r*.81f);
                var tint=Color.Lerp(new Color(.062f,.112f,.146f),new Color(.23f,.31f,.284f),elevation[j]*.74f+Range(0,.22f));
                int vertex=mesh.Add(point,tint);
                if(j==0 && i==0) start=vertex;
                if(j==0) continue;
                int a=start+(j-1)*sides+i,b=start+(j-1)*sides+(i+1)%sides;
                mesh.Quad(a,a+sides,b+sides,b);
            }
        }

        void Kelp(ReefGeometry mesh,Vector3 p,float size)
        {
            float yaw=Range(0,6);
            var d=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw));
            var side=Vector3.Cross(Vector3.up,d);
            int start=0;
            for(int i=0;i<=5;i++)
            {
                float t=i/5f;
                var mid=p+Vector3.up*(size*2.1f*t)+d*size*(.5f*t+.42f*t*t)+side*Mathf.Sin(t*6+yaw)*size*.17f;
                float w=(Mathf.Sin(t*Mathf.PI)*.21f+.01f)*size;
                var colour=Color.Lerp(new Color(.04f,.12f,.105f),new Color(.18f,.39f,.21f),t);
                int a=mesh.Add(mid-side*w,colour);mesh.Add(mid+d*w*.25f,colour*1.1f);mesh.Add(mid+side*w,colour);
                if(i==0){start=a;continue;}
                for(int k=0;k<2;k++){int q=start+(i-1)*3+k;mesh.Quad(q,q+3,q+4,q+1);}
            }
        }

        static void Fan(ReefGeometry mesh,Vector3 p,float angle)
        {
            Vector3 axis=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var tint=new Color(.52f,.354f,.216f);
            for(int k=-2;k<=2;k++)
            {
                Vector3 stem=p+axis*(k*.23f)+Vector3.up*(.66f-.045f*Mathf.Abs(k));
                Vector3 tip=stem+axis*(k*.17f)+Vector3.up*.5f;
                mesh.Tube(new[]{p,stem,tip},new[]{.068f,.054f,.012f},tint,5);
                mesh.Tube(new[]{stem,stem+Vector3.up*.23f-axis*.2f},new[]{.041f,.015f},tint*1.16f,5);
            }
        }

        void Publish(string label,ReefGeometry geometry,Material material,bool solid)
        {
            var piece=new GameObject(label);
            // Authored points are world-space metres, independent of the 2x vessel scale.
            piece.transform.SetParent(transform,false);
            var mesh=geometry.Mesh(label);owned.Add(mesh);
            piece.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            if(material==lightColumns){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
            if(solid)piece.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        void OnDestroy(){foreach(var asset in owned)if(asset!=null)Destroy(asset);}
    }
}
