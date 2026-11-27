using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    internal sealed class PipeSkin
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uv = new();
        readonly List<(Vector3 centre,Vector3 lateral,Vector3 up)> frames = new();
        readonly List<Material> materials = new();
        readonly List<List<int>> faces = new();
        readonly Material frame, enamel;
        readonly float radius;
        readonly int sides;
        Vector3 side;

        internal PipeSkin(float radius, int sides, Material frame, Material enamel)
        {
            this.radius = radius;
            this.sides = sides;
            this.frame = frame;
            this.enamel = enamel;
        }

        internal void Section(List<Vector3> points,int start,int steps,Material surface,PipeSection section)
        {
            int previous = -1;
            float travel = 0;
            for (int j=0; j<=steps; j++)
            {
                int i=start+j;
                var p=points[i];
                var forward=(points[Mathf.Min(points.Count-1,i+1)]-points[Mathf.Max(0,i-1)]).normalized;
                if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
                side = Vector3.ProjectOnPlane(side,forward).normalized;
                if (side.sqrMagnitude < .5f) side = Basis(forward);
                var up=Vector3.Cross(forward,side);
                if(frames.Count==i)frames.Add((p,side,up));
                int row=Ring(p,side,up,radius,travel);
                if (previous>=0) Join(previous,row,surface);
                previous=row;
                if (j < steps) travel+=Vector3.Distance(p,points[i+1]);
            }

            Vector3 direction=(points[start+1]-points[start]).normalized;
            if(direction.sqrMagnitude>.1f)
            {
                if(start==0) SocketCollar(points[start],direction);
                else if(start>steps || Vector3.Distance(points[start],points[0])>radius*.94f)
                    Collar(points[start],direction,radius,frame);
            }
            if (start+steps==points.Count-1)
            {
                direction=(points[^1]-points[^2]).normalized;
                if(direction.sqrMagnitude>.1f) SocketCollar(points[^1],-direction);
            }
            int mid=start+steps/2;
            direction=(points[mid+1]-points[mid-1]).normalized;
            if (direction.sqrMagnitude < .1f) return;
            var at=points[mid];
            if (section==PipeSection.ElbowFlange) Collar(at,direction,radius*1.04f,frame);
            if (section==PipeSection.Hanger)
            {
                Collar(at,direction,radius,frame);
                var up=Vector3.ProjectOnPlane(Vector3.up,direction).normalized;
                if(up.sqrMagnitude < .1f) up=Vector3.right;
                Rod(at+up*radius,at+up*(radius+.38f),radius*.12f,frame);
                Rod(at+up*(radius+.38f)-direction*.20f,at+up*(radius+.38f)+direction*.20f,radius*.16f,enamel);
            }
            if (section==PipeSection.Valve)
            {
                Collar(at,direction,radius,frame);
                var outward=Basis(direction);
                var hub=at+outward*radius*1.56f;
                Rod(at+outward*radius,hub,radius*.12f,frame);
                Wheel(hub,outward,radius*.48f,enamel);
                var arm=Basis(outward);
                for(int i=0;i<3;i++)
                {
                    var spoke=Quaternion.AngleAxis(i*120,outward)*arm*radius*.48f;
                    Rod(hub,hub+spoke,radius*.045f,enamel);
                }
            }
        }

        // A shallow compression fitting with a rebate for the glass. The centre stays open.
        void SocketCollar(Vector3 at,Vector3 intoPipe)
        {
            var tangent=Basis(intoPipe);
            var bitangent=Vector3.Cross(intoPipe,tangent);
            float r=radius;
            var face=at-intoPipe*(r*.12f);
            var seat=at+intoPipe*(r*.11f);
            var shoulder=at+intoPipe*(r*.34f);
            var taper=at+intoPipe*(r*.55f);
            int inner=Ring(face,tangent,bitangent,r*.87f,0);
            int edge=Ring(face,tangent,bitangent,r*1.22f,.2f);
            int back=Ring(seat,tangent,bitangent,r*1.22f,.3f);
            int seal=Ring(shoulder,tangent,bitangent,r*1.10f,.45f);
            int end=Ring(taper,tangent,bitangent,r*.985f,.6f);
            Join(inner,edge,frame);Join(edge,back,frame);Join(back,seal,enamel);Join(seal,end,frame);
            for(int i=0;i<8;i++)
            {
                float angle=(i+.5f)*Mathf.PI*.25f;
                var radial=tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle);
                Rod(face+radial*r*1.095f-intoPipe*r*.045f,face+radial*r*1.095f,r*.052f,frame);
            }
        }

        internal void ApplyCollision(Mesh mesh,float wallThickness)
        {
            var solids=new List<Vector3>(frames.Count*sides*2);
            var indices=new List<int>(frames.Count*sides*12);
            float inside=Mathf.Max(radius*.50f,radius-wallThickness);
            foreach(var ring in frames)
                foreach(float r in new[]{radius,inside})
                    for(int i=0;i<sides;i++)
                    {
                        float angle=i*Mathf.PI*2/sides;
                        solids.Add(ring.centre+r*(ring.lateral*Mathf.Cos(angle)+ring.up*Mathf.Sin(angle)));
                    }

            for(int ring=1;ring<frames.Count;ring++)
            {
                int first=(ring-1)*sides*2,next=ring*sides*2;
                for(int i=0;i<sides;i++)
                {
                    int j=(i+1)%sides;
                    Quad(first+i,first+j,next+j,next+i);
                    Quad(first+sides+j,first+sides+i,next+sides+i,next+sides+j);
                }
            }
            if(frames.Count>1)
                for(int i=0;i<sides;i++)
                {
                    int j=(i+1)%sides,last=(frames.Count-1)*sides*2;
                    Quad(j,i,sides+i,sides+j);
                    Quad(last+i,last+j,last+sides+j,last+sides+i);
                }

            mesh.SetVertices(solids);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateBounds();

            void Quad(int a,int b,int c,int d)
            {
                indices.Add(a);indices.Add(b);indices.Add(c);
                indices.Add(a);indices.Add(c);indices.Add(d);
            }
        }

        void Collar(Vector3 at,Vector3 axis,float r,Material material)
        {
            var a=Basis(axis);
            var b=Vector3.Cross(axis,a);
            float depth=r*.14f;
            int inner=Ring(at-axis*depth,a,b,r*.985f,0);
            int lip=Ring(at-axis*depth,a,b,r*1.12f,.1f);
            int back=Ring(at+axis*depth,a,b,r*1.12f,.2f);
            int rim=Ring(at+axis*depth,a,b,r*.985f,.3f);
            Join(inner,lip,material); Join(lip,back,material); Join(back,rim,material);
        }

        void Wheel(Vector3 at,Vector3 normal,float r,Material material)
        {
            var a=Basis(normal);
            var b=Vector3.Cross(normal,a);
            int previous=-1;
            for(int i=0;i<=sides;i++)
            {
                float angle=i*2*Mathf.PI/sides;
                var radial=a*Mathf.Cos(angle)+b*Mathf.Sin(angle);
                int row=Ring(at+radial*r,radial,-normal,r*.13f,(float)i/sides);
                if(previous>=0) Join(previous,row,material);
                previous=row;
            }
        }

        void Rod(Vector3 a,Vector3 b,float r,Material material)
        {
            var axis=(b-a).normalized;
            var u=Basis(axis);
            var v=Vector3.Cross(axis,u);
            Join(Ring(a,u,v,r,0),Ring(b,u,v,r,1),material);
        }

        static Vector3 Basis(Vector3 axis) => Vector3.Cross(Mathf.Abs(axis.y) < .85f ? Vector3.up : Vector3.right,axis).normalized;

        int Ring(Vector3 at, Vector3 a, Vector3 b, float r, float length)
        {
            int start=vertices.Count;
            for (int i=0;i<=sides;i++)
            {
                float t=(float)i/sides;
                vertices.Add(at+r*(a*Mathf.Cos(t*Mathf.PI*2)+b*Mathf.Sin(t*Mathf.PI*2)));
                uv.Add(new Vector2(t,length));
            }
            return start;
        }

        void Join(int a,int b, Material material)
        {
            int slot=materials.IndexOf(material);
            if(slot<0){slot=materials.Count; materials.Add(material); faces.Add(new List<int>());}
            var indices=faces[slot];
            for(int i=0;i<sides;i++)
            {
                indices.Add(a+i); indices.Add(a+i+1); indices.Add(b+i);
                indices.Add(a+i+1); indices.Add(b+i+1); indices.Add(b+i);
            }
        }

        internal Material[] Apply(Mesh mesh)
        {
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uv);
            mesh.subMeshCount=materials.Count;
            for(int i=0;i<faces.Count;i++) mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return materials.ToArray();
        }
    }
}

