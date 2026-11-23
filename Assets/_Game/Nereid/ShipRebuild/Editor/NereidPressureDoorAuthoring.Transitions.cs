using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidPressureDoorAuthoring
    {
        // All dimensions here are metres in the saved door rig, including the half-scale room module.
        static void FitPressureTransitions()
        {
            using (var asset=new PrefabUtility.EditPrefabContentsScope(Kit))
            {
                var frame=asset.prefabContentsRoot.transform;
                var rig=frame.GetComponent<NereidPressureDoors>();
                if(!rig.seaAdapter) rig.seaAdapter=SeaAdapter(frame);
                if(!rig.cabinFrame.Find("Cargo bulkhead returns")) CabinReturns(rig.cabinFrame);
            }
            using (var asset=new PrefabUtility.EditPrefabContentsScope(Root+"/Prefabs/Rooms/AftTransfer.prefab"))
            {
                var opening=asset.prefabContentsRoot.transform.Find("Pump room passage");
                if(!opening.Find("Pressure infill")) PumpCoffer(opening);
            }
        }

        static void PhysicalShell(ShipSectionMesh casting,string meshName)
        {
            casting.Store(Root+"/Meshes/"+meshName+".asset");
            // The collider follows the pressure casting only. Gaskets, lamps and inspection fittings have no collision.
            var solid=casting.node.gameObject.AddComponent<MeshCollider>();
            solid.sharedMesh=casting.node.GetComponent<MeshFilter>().sharedMesh;
        }

        static void Facet(ShipSectionMesh casting,Material paint,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0) casting.Face(paint,d,c,b,a);
            else casting.Face(paint,a,b,c,d);
        }

        static void CabinReturns(Transform frame)
        {
            // Metres in the inner door's plane. The cassette ends at 3.27 m and the
            // surrounding cargo-room plating starts at 3.62 m to either side.
            var casting=new ShipSectionMesh(frame,Vector3.zero,"Cargo bulkhead returns",Vector3.zero);
            const float inside=3.22f, outside=3.87f, bottom=-.065f, top=4.95f;
            foreach(int side in new[]{-1,1})
            {
                var centre=new Vector3(side*(inside+outside)*.5f,(top+bottom)*.5f,0);
                casting.Box(Blue,centre,new(outside-inside,top-bottom,.48f),default,.018f);
                foreach(int face in new[]{-1,1})
                {
                    casting.Box(Iron,new(side*3.31f,2.42f,face*.26f),new(.115f,4.94f,.11f));
                    casting.Box(Enamel,new(side*3.60f,2.43f,face*.254f),new(.38f,4.61f,.051f));
                    foreach(float height in new[]{.46f,2.41f,4.16f})
                        casting.Box(Steel,new(side*3.45f,height,face*.295f),new(.16f,.078f,.045f));
                }
            }
            PhysicalShell(casting,"Cargo bulkhead jamb returns");
        }

        static void PumpCoffer(Transform door)
        {
            var web=new ShipSectionMesh(door,Vector3.zero,"Pressure infill",Vector3.zero);
            var fittings=new ShipSectionMesh(web.node,Vector3.zero,"Soffit joints and retaining flanges",Vector3.zero);
            float[] bays={-11.90f,-9.3f,-6.3f,-3.25f,0f,3.25f,6.3f,9.3f,11.90f};
            // The cargo ceiling meets the shoulder at 7.30 m. On the hall side the soffit rises to 9.18 m.
            Vector3[] Section(float x)
            {
                float haunch=Mathf.SmoothStep(0,1,Mathf.InverseLerp(6f,11.9f,Mathf.Abs(x)));
                return new[]{
                    new Vector3(x,7.17f,-.38f), new Vector3(x,7.67f,-.38f),
                    new Vector3(x,9.28f-haunch*.14f,.28f),new Vector3(x,9.28f-haunch*.14f,.80f),
                    new Vector3(x,7.67f,.14f),new Vector3(x,7.17f,.14f)
                };
            }
            var materials=new[]{Blue,Enamel,Iron,Enamel,Steel,Iron};
            Vector3[] normals={Vector3.back,new(0,.4f,-1),Vector3.up,new(0,-.4f,1),Vector3.forward,Vector3.down};
            for(int i=1;i<bays.Length;i++)
            {
                var a=Section(bays[i-1]);var b=Section(bays[i]);
                for(int j=0;j<a.Length;j++)
                {
                    int k=(j+1)%a.Length;
                    Facet(web,materials[j],a[j],b[j],b[k],a[k],normals[j]);
                }
                if(i==1) {System.Array.Reverse(a);web.Face(Blue,a);}
                if(i==bays.Length-1) web.Face(Blue,b);
            }
            foreach(float x in bays)
            {
                var sec=Section(x);
                fittings.Beam(Iron,sec[4]+Vector3.forward*.024f,sec[3]+Vector3.forward*.024f,.085f,.076f);
                fittings.Beam(Steel,sec[4]+Vector3.forward*.065f,sec[3]+Vector3.forward*.065f,.027f,.029f);
            }
            foreach(int side in new[]{-1,1})
            {
                var sec=Section(side*10.25f);
                Vector3 low=sec[4], high=sec[3];
                fittings.Beam(Ochre,Vector3.Lerp(low,high,.20f),Vector3.Lerp(low,high,.48f),.12f,.13f);
            }
            fittings.Beam(Iron,new(-11.8f,7.47f,.16f),new(11.8f,7.47f,.16f),.11f,.14f);
            fittings.Store(Root+"/Meshes/Pump header fastening ribs.asset");
            PhysicalShell(web,"Double-faced pump ceiling transition");
        }

        static Transform SeaAdapter(Transform rig)
        {
            var shell=new ShipSectionMesh(rig,Vector3.zero,"Sea flange adapter",Vector3.zero);
            var joints=new ShipSectionMesh(shell.node,Vector3.zero,"Facet gaskets and flange hardware",Vector3.zero);
            const int sides=12;
            // The outer ring traces the twelve facets of the existing hull lip at x=10.30 in hull coordinates.
            // The small end overlaps the rectangular frame above and to the sides of the two sea controls.
            Vector3 Rim(int i)
            {
                float a=i*Mathf.PI*2/sides;
                return new(8.68f,2f+3.30f*Mathf.Sin(a),3.86f*Mathf.Cos(a));
            }
            Vector3[] foot={
                new(9.035f,2f,3.04f),new(9.035f,3.36f,2.87f),new(9.035f,3.87f,1.65f),
                new(9.035f,3.87f,0),new(9.035f,3.87f,-1.65f),new(9.035f,3.36f,-2.87f),
                new(9.035f,2f,-3.04f),new(9.035f,.61f,-2.87f),new(9.035f,-.16f,-1.65f),
                new(9.035f,-.16f,0),new(9.035f,-.16f,1.65f),new(9.035f,.61f,2.87f)
            };
            // The old sea-side nose was art drawn at runtime. This one is an editable, structural return.
            // The loading deck is flush with the bottom reveal. The transom edge is above both control pads.
            float[] outerY={2,3.71f,4.40f,4.40f,4.40f,3.71f,2,-.35f,-1.34f,-1.34f,-1.34f,-.35f};
            float[] outerZ={3.42f,3.42f,2.52f,0,-2.52f,-3.42f,-3.42f,-3.42f,-2.52f,0,2.52f,3.42f};
            float[] lipY={2,3.42f,4.04f,4.04f,4.04f,3.42f,2,.48f,-.055f,-.055f,-.055f,.48f};
            float[] lipZ={2.84f,2.84f,2.23f,0,-2.23f,-2.84f,-2.84f,-2.84f,-2.23f,0,2.23f,2.84f};
            Vector3 Nose(int i)=>new(9.78f,outerY[i],outerZ[i]);
            Vector3 Lip(int i)=>new(9.65f,lipY[i],lipZ[i]);
            var depth=Vector3.left*.18f;
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;
                var a=Rim(i);var b=Rim(j);var c=foot[j];var d=foot[i];
                var n=Nose(i);var m=Nose(j);var l=Lip(i);var k=Lip(j);
                Vector3 radial=Vector3.ProjectOnPlane((a+b)*.5f-new Vector3(0,2f,0),Vector3.right).normalized;
                void Skin(Material front,Material rear,Vector3 first,Vector3 second,Vector3 third,Vector3 fourth,Vector3 normal)
                {
                    Facet(shell,front,first,second,third,fourth,normal);
                    Facet(shell,rear,first+depth,second+depth,third+depth,fourth+depth,-normal);
                }
                Skin(Blue,Iron,a,b,m,n,radial);
                Skin(Steel,Blue,n,m,k,l,Vector3.right);
                Skin(Enamel,Blue,l,k,c,d,Vector3.right*.5f-radial);
                Facet(shell,Iron,a,a+depth,b+depth,b,radial);
                Facet(shell,Steel,d,c,c+depth,d+depth,-radial);
                // Exposed joints run along the actual folds. Their dark lands contrast with the light metal of the flange.
                var f=Vector3.right*.024f;
                joints.Beam(Iron,n+f,l+f,.049f,.062f);
                joints.Beam(Iron,l+f,k+f,.069f,.083f);
                joints.Beam(Steel,l+Vector3.right*.018f,d+Vector3.right*.018f,.046f,.048f);
                if(i<6)
                {
                    var fixing=Vector3.Lerp(Vector3.Lerp(n,m,.5f),Vector3.Lerp(l,k,.5f),.53f);
                    joints.Tube(Iron,fixing+Vector3.right*.016f,fixing+Vector3.right*.080f,.075f,-1,6);
                }
            }
            joints.Store(Root+"/Meshes/Sea door adapter flange hardware.asset");
            PhysicalShell(shell,"Faceted docking collar closure");
            return shell.node;
        }
    }
}
