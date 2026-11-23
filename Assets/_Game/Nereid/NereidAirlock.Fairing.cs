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
            var shoulder=new[]{
                LockRim(-.35f,5.30f,-2.29f,5.36f,1.47f),
                LockRim(.91f,5.27f,-2.17f,5.31f,1.39f),
                LockRim(2.57f,4.93f,-1.94f,5.06f,1.23f),
                LockRim(4.72f,4.42f,-1.73f,4.86f,1.05f),
                LockRim(6.86f,3.95f,-1.57f,4.66f,.95f),
                LockRim(8.92f,3.58f,-1.46f,4.47f,.88f),
                LockRim(9.71f,3.37f,-1.34f,4.31f,.82f)
            };
            bool castNose=authored && authored.seaAdapter;
            int shoulderCount=castNose?shoulder.Length-1:shoulder.Length;
            for(int i=1;i<shoulderCount;i++)NereidFairing.Join(outer,shoulder[i-1],shoulder[i]);
            NereidFairing.Seam(joining,shoulder[3],.040f);
            NereidFairing.Seam(joining,shoulder[5],.059f);

            // Both controls stay ahead of the backing and within the full width
            // of the wet vestibule. The actual door and deck are independent.
            var lip=LockRim(9.72f,2.80f,-.071f,4.06f,.47f);
            var back=LockRim(8.88f,2.67f,-.073f,3.92f,.36f);
            var opening=LockRim(8.87f,1.52f,-.028f,3.25f,.041f);
            if(!castNose)
            {
                NereidFairing.Join(joining,shoulder[^1],lip);
                NereidFairing.Join(throat,back,lip,true);
                NereidFairing.Join(throat,back,opening);
                NereidFairing.Seam(joining,lip,.042f);
            }
            foreach(int sign in new[]{-1,1})
            {
                CollarTube(markers,new[]{new Vector3(9.69f,.63f,sign*2.91f),new Vector3(9.69f,.96f,sign*2.91f)},.034f);
                CollarTube(markers,new[]{new Vector3(9.69f,3.37f,sign*2.91f),new Vector3(9.69f,3.61f,sign*2.81f),
                    new Vector3(9.69f,3.80f,sign*2.60f)},.039f);
                // The lower cradle converges from the hull rib into the dock sill.
                CollarTube(pipe,new[]{new Vector3(.57f,-1.79f,sign*4.11f),new Vector3(4.14f,-2.13f,sign*3.68f),
                    new Vector3(7.10f,-1.73f,sign*3.29f),new Vector3(9.76f,-.52f,sign*4.06f)},.145f);
                CollarTube(pipe,new[]{new Vector3(2.27f,-2.11f,sign*3.03f),new Vector3(4.64f,-2.21f,sign*2.93f),
                    new Vector3(7.14f,-1.62f,sign*3.90f),new Vector3(9.55f,-.27f,sign*4.39f)},.093f);
            }
            DockGangway(platform,joining);

            var skinShader=Resources.Load<Shader>("NereidHullCoating");
            var skin=Metal("Pressure lock cladding",new Color(.111f,.167f,.184f),.42f);
            if(skinShader!=null)
            {
                skin.shader=skinShader;
                skin.SetColor("_BaseColor",new(.111f,.167f,.184f));
                skin.SetFloat("_Metallic",.35f);
                skin.SetFloat("_Smoothness",.43f);
            }
            NereidSurfaceFinish.Apply(skin,NereidSurfaceFinish.Surface.Hull,exterior:true);
            FairingPart(hub,"Tapered pressure chamber shoulders",outer,skin);
            var porcelain=Metal("Silt-resistant docking ceramic",new Color(.48f,.56f,.54f),.42f);
            NereidSurfaceFinish.Apply(porcelain,NereidSurfaceFinish.Surface.Ceramic,exterior:true);
            FairingPart(hub,"Ceramic boarding surround and weld bands",joining,porcelain);
            if(!castNose) FairingPart(hub,"Recessed relief into the pressure doors",throat,steel);
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

        static Vector3[] LockRim(float x,float w,float low,float high,float b)
            => new[]{new Vector3(x,low,-w+b),new Vector3(x,low,w-b),
                new Vector3(x,low+b*.23f,w-b*.34f),new Vector3(x,low+b*.71f,w),
                new Vector3(x,high-b*.71f,w),new Vector3(x,high-b*.23f,w-b*.34f),
                new Vector3(x,high,w-b),new Vector3(x,high,-w+b),
                new Vector3(x,high-b*.23f,-w+b*.34f),new Vector3(x,high-b*.71f,-w),
                new Vector3(x,low+b*.71f,-w),new Vector3(x,low+b*.23f,-w+b*.34f)};

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
