using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    // Low-poly forms share indexed rings, vertex colours, and a single draw per material.
    sealed class ReefGeometry
    {
        readonly List<Vector3> points=new();
        readonly List<Color> colours=new();
        readonly List<int> indices=new();
        public int Add(Vector3 point, Color colour)
        {
            points.Add(point);colours.Add(colour);return points.Count-1;
        }
        public void Face(int a,int b,int c) {indices.Add(a);indices.Add(b);indices.Add(c);}
        public void Quad(int a,int b,int c,int d) {Face(a,b,c);Face(a,c,d);}

        public void Tube(Vector3[] path, float[] radii, Color color, int sides=8, float height=1)
        {
            int start=points.Count;
            Vector3 axis=Vector3.zero;
            for(int i=0;i<path.Length;i++)
            {
                var tangent=(path[Mathf.Min(i+1,path.Length-1)]-path[Mathf.Max(i-1,0)]).normalized;
                if(i==0) axis=Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.9f?Vector3.right:Vector3.Cross(tangent,Vector3.up).normalized;
                axis=Vector3.ProjectOnPlane(axis,tangent).normalized;
                var other=Vector3.Cross(tangent,axis);
                for(int j=0;j<sides;j++)
                {
                    float angle=2*Mathf.PI*j/sides;
                    Add(path[i]+radii[i]*(axis*Mathf.Cos(angle)+other*(height*Mathf.Sin(angle))),color*(.9f+.1f*Mathf.Sin(angle+.8f)));
                }
                if(i==0) continue;
                for(int j=0;j<sides;j++)
                {
                    int a=start+(i-1)*sides+j,b=start+(i-1)*sides+(j+1)%sides;
                    Quad(a,b,b+sides,a+sides);
                }
            }
            int cap=Add(path[0],color),end=Add(path[path.Length-1],color);
            for(int j=0;j<sides;j++)
            {
                int a=start+j,b=start+(j+1)%sides,extension=(path.Length-1)*sides;
                Face(cap,b,a);Face(end,a+extension,b+extension);
            }
        }

        public Mesh Mesh(string name)
        {
            var result=new Mesh{name=name};
            if(points.Count>65534) result.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            result.SetVertices(points);result.SetColors(colours);result.SetTriangles(indices,0);result.RecalculateNormals();result.RecalculateBounds();
            return result;
        }
    }
}
