using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid.Editor
{
    // Each hull plate has a single normal across its two triangles. Neighbouring plates meet at hard chines.
    // Each physical assembly is its own asset with its own pivot and materials.
    internal sealed class OceanEnvelopeMesh
    {
        readonly List<Vector3> positions = new();
        readonly List<Vector2> uv = new();
        readonly List<Vector3> normals = new();
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
            var tri=triangles[index];
            var grid=new Vector3[rows+1,cols+1];
            var coords=new Vector2[rows+1,cols+1];
            float along=0;
            for(int i=0;i<=rows;i++)
            {
                float across=0;
                for(int j=0;j<=cols;j++)
                {
                    var p=point((float)i/rows,(float)j/cols)-pivot;
                    grid[i,j]=p;
                    if(j==0&&i>0)along+=Vector3.Distance(p,grid[i-1,0]);
                    if(j>0)across+=Vector3.Distance(p,grid[i,j-1]);
                    coords[i,j]=new Vector2(along,across);
                }
            }
            for(int i=0;i<rows;i++)for(int j=0;j<cols;j++)
            {
                Vector3 a=grid[i,j],b=grid[i,j+1],c=grid[i+1,j],d=grid[i+1,j+1];
                Vector3 first=Vector3.Cross(b-a,c-a),second=Vector3.Cross(d-b,c-b);
                if(first.sqrMagnitude+second.sqrMagnitude<1e-12f)continue;
                var n=(first+second).normalized;
                bool reverse=Vector3.Dot(n,outward((i+.5f)/rows,(j+.5f)/cols))<0;
                if(back)reverse=!reverse;
                if(reverse)n=-n;
                int start=positions.Count;
                positions.Add(a);positions.Add(b);positions.Add(c);positions.Add(d);
                normals.Add(n);normals.Add(n);normals.Add(n);normals.Add(n);
                uv.Add(coords[i,j]);uv.Add(coords[i,j+1]);uv.Add(coords[i+1,j]);uv.Add(coords[i+1,j+1]);
                if(first.sqrMagnitude>1e-12f){tri.Add(start);tri.Add(start+(reverse?2:1));tri.Add(start+(reverse?1:2));}
                if(second.sqrMagnitude>1e-12f){tri.Add(start+1);tri.Add(start+(reverse?2:3));tri.Add(start+(reverse?3:2));}
            }
        }

        internal void Ring(Material mat,Func<float,Vector3> curve,float radius,int steps,int radial=5)
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
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create=!mesh;
            if(create)mesh=new Mesh();
            else mesh.Clear(false);
            mesh.name="Faceted "+node.name;
            mesh.indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Count;
            for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(triangles[i],i);
            mesh.SetNormals(normals);mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(create)AssetDatabase.CreateAsset(mesh,path);
            else EditorUtility.SetDirty(mesh);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            if(solid)node.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
}
