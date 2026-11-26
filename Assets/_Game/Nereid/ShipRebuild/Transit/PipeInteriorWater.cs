using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Raid.Nereid
{
    // A separate stream sits inside the metal and glass bore and follows the authored bends.
    internal sealed class PipeInteriorWater : IDisposable
    {
        const int BodyFacets=8, SurfaceFacets=5, RingSize=BodyFacets+SurfaceFacets+2;
        static readonly int Flow=Shader.PropertyToID("_Window");
        readonly Mesh mesh;
        readonly MeshRenderer draw;
        readonly Material material;
        readonly MaterialPropertyBlock block=new();

        internal PipeInteriorWater(Transform route)
        {
            var node=new GameObject("Water inside the pipe",typeof(MeshFilter),typeof(MeshRenderer));
            node.transform.SetParent(route,false);
            mesh=new Mesh{name="Half-filled pipe bore"};
            node.GetComponent<MeshFilter>().sharedMesh=mesh;
            draw=node.GetComponent<MeshRenderer>();
            draw.shadowCastingMode=ShadowCastingMode.Off;
            draw.receiveShadows=false;
            var shader=Resources.Load<Shader>("RaidPipeWater");
            if(shader)
            {
                material=new Material(shader);
                draw.sharedMaterial=material;
            }
            Hide();
        }

        internal void Rebuild(IReadOnlyList<Vector3> samples,IReadOnlyList<float> distance,float boreRadius)
        {
            mesh.Clear();
            if(samples.Count<2 || distance[^1]<.001f)return;
            float radius=boreRadius*.90f;
            var vertices=new Vector3[samples.Count*RingSize];
            var uv=new Vector2[vertices.Length];
            var data=new Vector4[vertices.Length];
            var indices=new int[(samples.Count-1)*(BodyFacets+SurfaceFacets)*6];
            Vector3 up=Vector3.zero;
            int face=0;
            for(int i=0;i<samples.Count;i++)
            {
                var center=samples[i];
                var forward=(samples[Mathf.Min(i+1,samples.Count-1)]-samples[Mathf.Max(0,i-1)]).normalized;
                var gravity=Vector3.ProjectOnPlane(Vector3.up,forward);
                var previous=Vector3.ProjectOnPlane(up,forward);
                if(previous.sqrMagnitude<.01f)previous=gravity.sqrMagnitude>.01f?gravity:
                    Vector3.ProjectOnPlane(Vector3.right,forward);
                up=Vector3.Lerp(previous.normalized,gravity.normalized,Mathf.SmoothStep(0,1,gravity.magnitude)).normalized;
                if(up.sqrMagnitude<.01f)up=previous.normalized;
                var across=Vector3.Cross(up,forward).normalized;
                float along=distance[i]/distance[^1];
                int row=i*RingSize;
                for(int edge=0;edge<=BodyFacets;edge++)
                {
                    float fraction=edge/(float)BodyFacets;
                    float angle=Mathf.Lerp(.13f,-Mathf.PI-.13f,fraction);
                    float height=Mathf.Sin(angle);
                    vertices[row+edge]=center+radius*(across*Mathf.Cos(angle)+up*height);
                    uv[row+edge]=new Vector2(distance[i],height);
                    data[row+edge]=new Vector4(along,0,Mathf.Max(0,height)*radius,0);
                    if(i>0 && edge>0)Panel(indices,ref face,row+edge-1);
                }
                for(int edge=0;edge<=SurfaceFacets;edge++)
                {
                    float fraction=edge/(float)SurfaceFacets,x=2*fraction-1;
                    int vertex=row+BodyFacets+1+edge;
                    float hump=(1-x*x)*.08f;
                    vertices[vertex]=center+radius*(across*(x*Mathf.Cos(.13f))+up*(Mathf.Sin(.13f)+hump));
                    uv[vertex]=new Vector2(distance[i],x);
                    data[vertex]=new Vector4(along,1,hump*radius,0);
                    if(i>0 && edge>0)Panel(indices,ref face,vertex-1);
                }
            }
            mesh.vertices=vertices;
            mesh.uv=uv;
            mesh.SetUVs(1,data);
            mesh.triangles=indices;
        }

        static void Panel(int[] indices,ref int face,int left)
        {
            indices[face++]=left-RingSize;indices[face++]=left;indices[face++]=left-RingSize+1;
            indices[face++]=left-RingSize+1;indices[face++]=left;indices[face++]=left+1;
        }

        internal void Present(float head,float tail,float pressure)
        {
            if(!material || mesh.vertexCount==0)return;
            block.SetVector(Flow,new Vector4(head,tail,pressure,0));
            draw.SetPropertyBlock(block);
            draw.enabled=true;
        }

        internal void Hide(){if(draw)draw.enabled=false;}
        public void Dispose()
        {
            if(draw)Object.Destroy(draw.gameObject);
            if(mesh)Object.Destroy(mesh);
            if(material)Object.Destroy(material);
        }
    }
}
