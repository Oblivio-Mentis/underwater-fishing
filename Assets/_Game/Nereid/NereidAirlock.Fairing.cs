using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class NereidAirlock
    {
        readonly List<Mesh> fairingMeshes=new();

        void BuildMarineCollar()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var hub=new GameObject("Flared sea lock / docking collar").transform;
            hub.SetParent(transform,false);

            var outer=new ReefGeometry();
            var joining=new ReefGeometry();
            var throat=new ReefGeometry();
            var markers=new ReefGeometry();
            var platform=new ReefGeometry();
            var pipe=new ReefGeometry();
            var positions=new[]{
                LockRim(.035f,4.56f,-2.72f,5.58f,1.13f),
                LockRim(1.42f,4.48f,-2.49f,5.35f,1.10f),
                LockRim(3.36f,4.15f,-2.06f,5.10f,.98f),
                LockRim(5.75f,3.87f,-1.77f,4.91f,.88f),
                LockRim(7.64f,3.69f,-1.46f,4.71f,.82f),
                LockRim(9.37f,3.43f,-1.26f,4.46f,.78f),
                LockRim(9.62f,3.31f,-1.19f,4.38f,.76f)
            };
            for(int i=1;i<positions.Length;i++)
            {
                NereidFairing.Join(outer,positions[i-1],positions[i]);
                if(i%2==1)NereidFairing.Seam(joining,positions[i],.057f);
            }

            // The pressure leaves are behind this wide relief. The rectangular
            // opening within the collar covers the two actual control pad rays.
            var innerLip=LockRim(9.60f,2.71f,-.065f,3.99f,.47f);
            var backing=LockRim(8.86f,2.66f,-.078f,3.90f,.43f);
            NereidFairing.Join(joining,positions[positions.Length-1],innerLip);
            NereidFairing.Join(throat,backing,innerLip,true);
            var doorOpening=LockRim(8.87f,1.54f,-.032f,3.256f,.054f);
            NereidFairing.Join(throat,backing,doorOpening);
            NereidFairing.Seam(joining,innerLip,.034f);
            for(int side=-1;side<=1;side+=2)
            {
                CollarTube(markers,new[]{new Vector3(9.41f,.58f,side*2.806f),new Vector3(9.41f,1.05f,side*2.80f)},.055f);
                CollarTube(markers,new[]{new Vector3(9.39f,3.27f,side*2.79f),new Vector3(9.39f,3.46f,side*2.72f),
                    new Vector3(9.41f,3.77f,side*2.47f)},.055f);
                CollarTube(pipe,new[]{new Vector3(1.61f,-1.54f,side*4.30f),new Vector3(4.83f,-1.94f,side*3.73f),
                    new Vector3(7.24f,-1.09f,side*3.58f),new Vector3(9.53f,-.45f,side*3.89f)},.13f);
                CollarTube(pipe,new[]{new Vector3(1.99f,-2.51f,side*2.99f),new Vector3(4.51f,-1.99f,side*3.47f),
                    new Vector3(7.33f,-.85f,side*4.13f),new Vector3(8.93f,-.44f,side*4.43f)},.124f);
            }
            DockGangway(platform,joining);

            var skinShader=Resources.Load<Shader>("NereidHullCoating");
            var skin=Metal("Pressure lock cladding",new Color(.132f,.193f,.208f),.42f);
            if(skinShader!=null)
            {
                skin.shader=skinShader;
                skin.SetColor("_BaseColor",new(.132f,.193f,.208f));
                skin.SetFloat("_Metallic",.35f);
                skin.SetFloat("_Smoothness",.43f);
            }
            FairingPart(hub,"Tapered pressure chamber shoulders",outer,skin);
            FairingPart(hub,"Welded dock flange and sill plating",joining,trim);
            FairingPart(hub,"Recessed relief into the pressure doors",throat,steel);
            FairingPart(hub,"Caged amber door markers",markers,amber);
            FairingPart(hub,"Sloped dock cradle mounts",pipe,skin);
            FairingPart(hub,"Cast non-slip boarding platform",platform,skin);
        }

        void DockGangway(ReefGeometry mesh,ReefGeometry strakes)
        {
            var vessel=transform.parent;
            var platform=vessel.Find("Stern platform");
            if(platform!=null && platform.TryGetComponent<Renderer>(out var oldDeck))oldDeck.enabled=false;
            // The bevels hug the existing collider. Reach to the sea pads and the underside cargo light is retained.
            Vector3[] plan={
                new(4.57f,-.018f,-4.32f),new(7.74f,-.018f,-4.64f),new(9.47f,-.018f,-4.64f),
                new(9.62f,-.018f,-4.34f),new(9.62f,-.018f,4.34f),new(9.47f,-.018f,4.64f),
                new(7.74f,-.018f,4.64f),new(4.57f,-.018f,4.32f)
            };
            int top=mesh.Add(new Vector3(7.08f,-.018f,0),Color.white);
            for(int i=0;i<plan.Length;i++)
            {
                Vector3 a=plan[i],b=plan[(i+1)%plan.Length];
                int p=mesh.Add(a,Color.white),q=mesh.Add(b,Color.white);
                mesh.Face(top,q,p);
                var belowA=Vector3.Lerp(a,new(a.x,-1.12f,0),.24f);
                var belowB=Vector3.Lerp(b,new(b.x,-1.12f,0),.24f);
                int r=mesh.Add(belowB,Color.white),s=mesh.Add(belowA,Color.white);
                mesh.Quad(p,q,r,s);
            }
            for(int i=0;i<6;i++)
            {
                float z=-3.99f+i*1.60f;
                CollarTube(strakes,new[]{new Vector3(9.20f,-.041f,z),new Vector3(9.52f,-.051f,z)},.036f);
            }
        }

        void Lining(string label,Vector3 point,Vector3 size,Material finish,Vector3 normal,Vector3 right,Vector3 up)
        {
            var barrier=Box(label,point,size,finish,true);
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            barrier.GetComponent<MeshRenderer>().enabled=false;
            var geometry=new ReefGeometry();
            Vector3 center=normal*.5f;
            Vector3 x=right*.5f,y=up*.5f;
            geometry.Add(center-x-y,Color.white);
            geometry.Add(center+x-y,Color.white);
            geometry.Add(center+x+y,Color.white);
            geometry.Add(center-x+y,Color.white);
            geometry.Quad(0,1,2,3);
            FairingPart(barrier,label+" inner lining",geometry,finish);
        }

        static Vector3[] LockRim(float x,float halfWidth,float bottom,float top,float bevel)
            => new[]{new Vector3(x,bottom,-halfWidth+bevel),new Vector3(x,bottom,halfWidth-bevel),
                new Vector3(x,bottom+bevel,halfWidth),new Vector3(x,top-bevel,halfWidth),
                new Vector3(x,top,halfWidth-bevel),new Vector3(x,top,-halfWidth+bevel),
                new Vector3(x,top-bevel,-halfWidth),new Vector3(x,bottom+bevel,-halfWidth)};

        static void CollarTube(ReefGeometry shape,Vector3[] sections,float radius)
        {
            var radii=new float[sections.Length];
            for(int i=0;i<radii.Length;i++)radii[i]=radius;
            shape.Tube(sections,radii,Color.white,8);
        }

        void FairingPart(Transform parent,string label,ReefGeometry geometry,Material finish)
        {
            var obj=new GameObject(label);
            obj.transform.SetParent(parent,false);
            var mesh=geometry.Mesh(label);
            fairingMeshes.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=finish;
        }
    }
}
