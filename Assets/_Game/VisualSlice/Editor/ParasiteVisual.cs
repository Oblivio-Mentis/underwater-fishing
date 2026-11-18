using System.Collections.Generic;
using UnityEngine;

namespace Raid.VisualSlice.Editor
{
    /// <summary>A leech-like bow parasite. Every generated object is presentation geometry only.</summary>
    public static class ParasiteVisual
    {
        public static void Build(Transform parent)
        {
            var root=VisualKit.Group("Bow parasite / attached oral disc",parent,new Vector3(-5f,1f,11.5f));
            var skin=VisualKit.Mat("Creature");
            var dark=VisualKit.Mat("CreatureDark");
            var pale=VisualKit.Mat("CreatureLight");
            var glow=VisualKit.Mat("Glow");
            var gold=VisualKit.Mat("Amber");

            // The flattened oral foot intersects the left bow shoulder; the airlock remains clear.
            var attachment=VisualKit.Group("Fixed suction attachment",root);
            Lobe("Compressed fleshy seal",attachment,new Vector3(-.3f,-.1f,-1f),new Vector3(4.65f,4.8f,.65f),pale);
            Ring("Parasite outer attachment rim",attachment,new Vector3(-.3f,-.1f,-.94f),2.05f,.28f,new Vector3(1,1.05f,1),skin);
            Lobe("Recessed oral cavity",attachment,new Vector3(-.3f,-.1f,-1.37f),new Vector3(3.7f,3.85f,.16f),dark);
            Ring("Parasite inner mouth rim",attachment,new Vector3(-.3f,-.1f,-1.42f),1.43f,.12f,new Vector3(1,1.05f,1),pale);
            for(int i=0;i<14;i++)
            {
                float angle=(i+.5f)*Mathf.PI*2/14;
                var origin=new Vector3(-.3f+Mathf.Cos(angle)*1.54f,-.1f+Mathf.Sin(angle)*1.64f,-1.49f);
                Vector3 direction=new Vector3(-Mathf.Cos(angle),-Mathf.Sin(angle),-.17f).normalized;
                Cone("Inward oral tooth "+i,attachment,origin,new Vector3(.18f,.18f,.53f),Quaternion.LookRotation(direction),pale);
            }

            var mantle=VisualKit.Group("Breathing mantle",root);
            var body=VisualKit.MeshObject("Parasite mantle faceted",mantle,MantleMesh(),skin);
            body.name="Single sculpted mantle";
            float[] z={.8f,2f,3.25f,4.5f,5.7f};
            float[] rx={2.2f,2.5f,2.64f,2.4f,1.95f};
            float[] ry={2.08f,2.4f,2.43f,2.14f,1.65f};
            for(int i=0;i<z.Length;i++)
            {
                float cx=-.4f-z[i]*.15f,cy=.15f+Mathf.Sin(z[i]*.4f)*.45f;
                var arch=new Vector3[13];var radii=new float[13];
                for(int j=0;j<arch.Length;j++)
                {
                    float a=-.12f+j/(float)(arch.Length-1)*(Mathf.PI+.24f);
                    arch[j]=new Vector3(cx+Mathf.Cos(a)*rx[i],cy+Mathf.Sin(a)*ry[i],z[i]);
                    radii[j]=.12f+Mathf.Sin(j*Mathf.PI/(arch.Length-1))*.075f;
                }
                VisualKit.Tube("Parasite dorsal ridge "+i,mantle,arch,radii,pale,7);
                var plate=VisualKit.MeshObject("Parasite dorsal plate "+i,mantle,PlateMesh(rx[i]*.68f,1.05f,.5f),dark);
                plate.transform.localPosition=new Vector3(cx,cy+ry[i]-.2f,z[i]+.04f);
                plate.transform.localRotation=Quaternion.Euler(-9,0,0);
            }

            var organs=VisualKit.Group("Bioluminescent organs",mantle);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
            {
                float size=1f-i*.18f;
                var eye=VisualKit.Group("Sensory eye "+side+" "+i,organs,
                    new Vector3(side>0?1.55f-i*.17f:-3.25f-i*.1f,.9f+i*.22f,2.6f+i*.93f));
                eye.localRotation=Quaternion.Euler(-8,side*55,side*-8);
                Lobe("Recessed socket",eye,Vector3.zero,new Vector3(.94f,.68f,.33f)*size,dark);
                Lobe("Amber iris",eye,new Vector3(0,0,.18f)*size,new Vector3(.55f,.4f,.2f)*size,gold);
                Lobe("Luminous eye",eye,new Vector3(0,0,.275f)*size,new Vector3(.29f,.29f,.105f)*size,glow);
                Lobe("Vertical pupil",eye,new Vector3(0,0,.327f)*size,new Vector3(.068f,.27f,.06f)*size,dark);
                var brow=VisualKit.MeshObject("Parasite eye brow",eye,PlateMesh(.53f,.32f,.16f),pale);
                brow.transform.localPosition=new Vector3(0,.3f,.1f)*size;brow.transform.localScale=Vector3.one*size;
                brow.transform.localRotation=Quaternion.Euler(57,0,0);
            }
            // Small luminous gill pores provide orientation without lighting the whole ocean.
            for(int i=0;i<6;i++)
                Lobe("Gill pore "+i,organs,new Vector3(1.5f-i*.28f,-.4f,1.1f+i*.8f),new Vector3(.13f,.28f,.18f),glow);

            var joints=new List<Transform>();
            Tendril(root,joints,0,new[]{new Vector3(-2.1f,1.35f,1.6f),new Vector3(-3.3f,2.8f,.7f),new Vector3(-3.2f,3.4f,-2),new Vector3(-1.7f,3.7f,-4.5f),new Vector3(-.6f,3.4f,-5.2f)});
            Tendril(root,joints,1,new[]{new Vector3(.9f,1.6f,2),new Vector3(1.6f,3,.2f),new Vector3(.8f,3.7f,-1.8f),new Vector3(-1.4f,3.8f,-3.2f),new Vector3(-2.8f,3.2f,-3.8f)});
            Tendril(root,joints,2,new[]{new Vector3(-2.6f,.2f,2.4f),new Vector3(-4.2f,.4f,1.4f),new Vector3(-4.5f,.1f,-1),new Vector3(-3.8f,-.5f,-3),new Vector3(-2.9f,-.6f,-4)});
            Tendril(root,joints,3,new[]{new Vector3(-2.3f,-1.3f,2.2f),new Vector3(-3.3f,-2.8f,1.4f),new Vector3(-2.8f,-3.7f,-.8f),new Vector3(-1.4f,-4,-2.6f),new Vector3(-.3f,-3.3f,-3.7f)});
            Tendril(root,joints,4,new[]{new Vector3(.9f,-1.1f,1.9f),new Vector3(1.8f,-2.2f,.5f),new Vector3(1.65f,-3.3f,-1.7f),new Vector3(.8f,-3.1f,-3.3f),new Vector3(.1f,-2.2f,-3.8f)});
            Tendril(root,joints,5,new[]{new Vector3(-1.7f,-1.4f,4.8f),new Vector3(-2.7f,-2.3f,6),new Vector3(-3.7f,-2.2f,6.9f),new Vector3(-4.2f,-1.1f,7.3f),new Vector3(-3.8f,-.6f,6.6f)});
            Tendril(root,joints,6,new[]{new Vector3(-1.4f,1.8f,5),new Vector3(-2.5f,2.7f,6),new Vector3(-3.1f,2.2f,7),new Vector3(-2.7f,1.3f,7.6f),new Vector3(-2,1.4f,7)});
            Tendril(root,joints,7,new[]{new Vector3(1.45f,.4f,1.8f),new Vector3(2.1f,.25f,.3f),new Vector3(2.15f,-.1f,-1),new Vector3(1.9f,-.1f,-2.5f),new Vector3(1.45f,.3f,-3.4f)});

            var animation=root.gameObject.AddComponent<ParasiteMotion>();
            animation.breathingMantle=mantle;animation.tentacleJoints=joints.ToArray();animation.luminousOrgans=organs;
        }

        private static void Tendril(Transform parent,List<Transform> joints,int index,Vector3[] anchors)
        {
            Vector3 pivot=anchors[0];var joint=VisualKit.Group("Visual tendril pivot "+index,parent,pivot);joints.Add(joint);
            var points=new List<Vector3>();var radii=new List<float>();
            for(int segment=0;segment<anchors.Length-1;segment++)for(int step=0;step<6;step++)
            {
                float t=step/6f;
                Vector3 p0=anchors[Mathf.Max(0,segment-1)],p1=anchors[segment],p2=anchors[segment+1],p3=anchors[Mathf.Min(anchors.Length-1,segment+2)];
                points.Add(Catmull(p0,p1,p2,p3,t)-pivot);
                float u=(segment+t)/(anchors.Length-1);radii.Add(Mathf.Lerp(.64f,.045f,Mathf.Pow(u,.8f)));
            }
            points.Add(anchors[anchors.Length-1]-pivot);radii.Add(.02f);
            VisualKit.Tube("Parasite tendril "+index,joint,points.ToArray(),radii.ToArray(),VisualKit.Mat("Creature"),10);
            var vein=new Vector3[points.Count];var veinRadius=new float[points.Count];
            for(int i=0;i<points.Count;i++){vein[i]=points[i]+Vector3.forward*radii[i]*.82f;veinRadius[i]=radii[i]*.16f;}
            VisualKit.Tube("Parasite tendril seam "+index,joint,vein,veinRadius,VisualKit.Mat("CreatureLight"),5);
            for(int i=5;i<points.Count-3;i+=5)
            {
                float cup=radii[i]*.62f;
                Ring("Parasite sucker ring",joint,points[i]+Vector3.forward*radii[i]*.85f,1,.25f,Vector3.one*cup,VisualKit.Mat("CreatureLight"));
                Lobe("Sucker depression",joint,points[i]+Vector3.forward*(radii[i]*.85f-.015f),new Vector3(cup*1.4f,cup*1.4f,.09f),VisualKit.Mat("CreatureDark"));
            }
        }

        private static Vector3 Catmull(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float t)
            =>.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);

        private static Mesh MantleMesh()
        {
            float[] zs={-1.02f,-.1f,.85f,2f,3.3f,4.7f,6.2f,7.35f};
            float[] xs={1.26f,1.94f,2.38f,2.65f,2.75f,2.5f,1.8f,.04f};
            float[] ys={1.48f,1.9f,2.2f,2.5f,2.55f,2.3f,1.55f,.04f};
            var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=16;
            for(int r=0;r<zs.Length;r++)for(int s=0;s<sides;s++)
            {
                float a=s*Mathf.PI*2/sides;
                float lobes=1f+Mathf.Cos(a*3+r*.37f)*.035f;
                vertices.Add(new Vector3(-.35f-zs[r]*.15f+Mathf.Cos(a)*xs[r]*lobes,.15f+Mathf.Sin(zs[r]*.4f)*.45f+Mathf.Sin(a)*ys[r]*lobes,zs[r]));
            }
            for(int r=0;r<zs.Length-1;r++)for(int s=0;s<sides;s++)
            {int a=r*sides+s,b=r*sides+(s+1)%sides,c=a+sides,d=b+sides;triangles.AddRange(new[]{a,b,c,b,d,c});}
            return FlatMesh("Parasite mantle",vertices,triangles);
        }

        private static Mesh PlateMesh(float halfWidth,float length,float height)
        {
            var v=new List<Vector3>{new Vector3(-halfWidth,0,-length*.45f),new Vector3(0,height,-length*.65f),new Vector3(halfWidth,0,-length*.45f),new Vector3(-halfWidth*.75f,0,length*.5f),new Vector3(0,height*.5f,length*.68f),new Vector3(halfWidth*.75f,0,length*.5f),new Vector3(0,-.07f,0)};
            return FlatMesh("Organic ridge plate",v,new List<int>{0,3,1,1,3,4,1,4,2,2,4,5,0,1,6,1,2,6,2,5,6,5,4,6,4,3,6,3,0,6});
        }

        private static void Lobe(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            const int sides=12,rings=8;var v=new List<Vector3>();var t=new List<int>();
            for(int r=0;r<=rings;r++)for(int s=0;s<sides;s++)
            {float a=s*Mathf.PI*2/sides,b=r*Mathf.PI/rings;v.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))*.5f);}
            for(int r=0;r<rings;r++)for(int s=0;s<sides;s++){int a=r*sides+s,b=r*sides+(s+1)%sides,c=a+sides,d=b+sides;t.AddRange(new[]{a,b,c,b,d,c});}
            var mesh=new Mesh{name="Parasite ellipsoid"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();
            var go=VisualKit.MeshObject("Parasite reusable ellipsoid",parent,mesh,material);go.name=name;go.transform.localPosition=position;go.transform.localScale=scale;
        }

        private static void Ring(string name,Transform parent,Vector3 position,float major,float tube,Vector3 scale,Material material)
        {
            const int segments=16,sides=6;var v=new List<Vector3>();var t=new List<int>();
            for(int a=0;a<segments;a++)for(int b=0;b<sides;b++)
            {float u=a*Mathf.PI*2/segments,w=b*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(u)*(major+tube*Mathf.Cos(w)),Mathf.Sin(u)*(major+tube*Mathf.Cos(w)),tube*Mathf.Sin(w)));}
            for(int a=0;a<segments;a++)for(int b=0;b<sides;b++)
            {int p=a*sides+b,q=((a+1)%segments)*sides+b,r=a*sides+(b+1)%sides,s=((a+1)%segments)*sides+(b+1)%sides;t.AddRange(new[]{p,q,r,r,q,s});}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();
            var go=VisualKit.MeshObject(name,parent,mesh,material);go.transform.localPosition=position;go.transform.localScale=scale;
        }

        private static void Cone(string name,Transform parent,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;v.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0));}v.Add(Vector3.forward);v.Add(Vector3.zero);
            for(int i=0;i<6;i++)t.AddRange(new[]{i,(i+1)%6,6,(i+1)%6,i,7});
            var go=VisualKit.MeshObject("Parasite reusable tooth",parent,FlatMesh("Tooth",v,t),material);go.name=name;
            go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;
        }

        private static Mesh FlatMesh(string name,List<Vector3> vertices,List<int> indices)
        {
            var flat=new Vector3[indices.Count];var triangles=new int[indices.Count];
            for(int i=0;i<indices.Count;i++){flat[i]=vertices[indices[i]];triangles[i]=i;}
            var mesh=new Mesh{name=name};mesh.vertices=flat;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
