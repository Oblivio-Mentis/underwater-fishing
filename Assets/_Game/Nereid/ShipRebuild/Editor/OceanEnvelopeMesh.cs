using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid.Editor
{
    // Rows and columns share vertices within each surface so the loft reads as continuous formed metal.
    // Each physical assembly is its own asset with its own pivot and materials.
    internal sealed class OceanEnvelopeMesh
    {
        readonly List<Vector3> positions = new();
        readonly List<Vector2> uv = new();
        readonly List<List<int>> triangles = new();
        readonly List<Material> materials = new();
        readonly Vector3 pivot;
        internal readonly Transform node;

        internal OceanEnvelopeMesh(Transform parent,string name,Vector3 pivot)
        {
            this.pivot=pivot;
            node=new GameObject(name).transform;node.SetParent(parent,false);node.localPosition=pivot;
        }

        internal void Surface(Material finish,int rows,int cols,Func<float,float,Vector3> point,
            Func<float,float,Vector3> outward,bool back=false)
        {
            int index=materials.IndexOf(finish);
            if(index<0){index=materials.Count;materials.Add(finish);triangles.Add(new List<int>());}
            var tri=triangles[index];var start=positions.Count;
            float along=0;
            Vector3 lastCentre=point(0,.5f);
            for(int i=0;i<=rows;i++)
            {
                float u=(float)i/rows;
                Vector3 centre=point(u,.5f);
                along+=Vector3.Distance(lastCentre,centre);
                lastCentre=centre;
                float cross=0;Vector3 last=point(u,0);
                for(int j=0;j<=cols;j++)
                {
                    var vertex=point(u,(float)j/cols);
                    cross+=Vector3.Distance(last,vertex);last=vertex;
                    positions.Add(vertex-pivot);
                    uv.Add(new Vector2(along+pivot.z*.37f,cross+pivot.x*.23f)/2.84f);
                }
            }
            for(int i=0;i<rows;i++)for(int j=0;j<cols;j++)
            {
                int a=start+i*(cols+1)+j,b=a+1,c=a+cols+1,d=c+1;
                Vector3 n=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);
                if(n.sqrMagnitude<1e-12f)n=Vector3.Cross(positions[d]-positions[c],positions[b]-positions[c]);
                bool ccw=Vector3.Dot(n,outward((i+.5f)/rows,(j+.5f)/cols))>=0;
                if(back)ccw=!ccw;
                if(ccw){tri.Add(a);tri.Add(b);tri.Add(c);tri.Add(b);tri.Add(d);tri.Add(c);}
                else {tri.Add(a);tri.Add(c);tri.Add(b);tri.Add(b);tri.Add(c);tri.Add(d);}
            }
        }

        internal void Ring(Material mat,Func<float,Vector3> curve,float radius,int steps,int radial=8)
        {
            Vector3 Spine(float t)=>curve(Mathf.Clamp01(t));
            Vector3 Direction(float t,float angle)
            {
                var forward=(Spine(t+.001f)-Spine(t-.001f)).normalized;
                var axis=Mathf.Abs(forward.y)<.95f?Vector3.up:Vector3.right;
                var a=Vector3.Cross(forward,axis).normalized;var b=Vector3.Cross(forward,a);
                return a*Mathf.Cos(angle)+b*Mathf.Sin(angle);
            }
            Surface(mat,steps,radial,(u,v)=>Spine(u)+Direction(u,v*2*Mathf.PI)*radius,
                (u,v)=>Direction(u,v*2*Mathf.PI));
        }

        internal void Quad(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
            => Surface(m,1,1,(u,v)=>Vector3.Lerp(Vector3.Lerp(a,b,u),Vector3.Lerp(d,c,u),v),(u,v)=>normal);

        internal void Save(string path,bool solid=false)
        {
            var mesh=new Mesh{name=node.name,indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Count;
            for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
            else AssetDatabase.CreateAsset(mesh,path);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            if(solid)node.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
}
