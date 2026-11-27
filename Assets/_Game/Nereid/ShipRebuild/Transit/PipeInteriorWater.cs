using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Raid.Nereid
{
    internal readonly struct PipeSpill
    {
        readonly Vector3 start, launch, approach;
        internal readonly Vector3 Impact;

        internal PipeSpill(Vector3 start,Vector3 heading,Vector3? receiver)
        {
            this.start=start;
            Impact=receiver??start+heading*1.1f+Vector3.down*1.1f;
            float flight=Vector3.Distance(start,Impact);
            launch=start+heading*Mathf.Clamp(flight*.51f,.29f,1.25f);
            approach=Impact+Vector3.up*Mathf.Clamp(flight*.30f,.25f,1.2f);
        }

        internal Vector3 Point(float t)
        {
            float u=1-t;
            return u*u*u*start+3*u*u*t*launch+3*u*t*t*approach+t*t*t*Impact;
        }

        internal Vector3 Tangent(float t)
        {
            float u=1-t;
            return (u*u*(launch-start)+2*u*t*(approach-launch)+t*t*(Impact-approach)).normalized;
        }
    }

    // Rings continue across the outlet on the same mesh, using identical cross-section and UVs.
    internal sealed class PipeInteriorWater : IDisposable
    {
        const int BodyFacets=8,SurfaceFacets=5,RingSize=BodyFacets+SurfaceFacets+2,SpillRings=18;
        static readonly int Flow=Shader.PropertyToID("_Window");
        readonly Transform route;
        readonly NereidPipeRoute settings;
        readonly bool ownsMaterial;
        readonly Mesh mesh;
        readonly MeshRenderer draw;
        readonly Material material;
        readonly MaterialPropertyBlock block=new();
        Vector3[] vertices;
        Vector2[] uv;
        Vector4[] flowData;
        int boreRings;
        Vector3 outletUp,outletTangent,lastImpact;
        float radius,boreLength;
        bool spillInitialized;

        internal PipeInteriorWater(NereidPipeRoute settings)
        {
            this.settings=settings;
            route=settings.transform;
            var node=new GameObject("Continuous pipe flow",typeof(MeshFilter),typeof(MeshRenderer));
            node.hideFlags=HideFlags.DontSave;
            node.transform.SetParent(route,false);
            mesh=new Mesh{name="Pipe water and outflow",indexFormat=IndexFormat.UInt32};
            mesh.MarkDynamic();
            node.GetComponent<MeshFilter>().sharedMesh=mesh;
            draw=node.GetComponent<MeshRenderer>();
            draw.shadowCastingMode=ShadowCastingMode.Off;
            draw.receiveShadows=false;
            var shader=Resources.Load<Shader>("RaidPipeWater");
            if(settings.flowWaterMaterial)material=settings.flowWaterMaterial;
            else if(shader){material=new Material(shader);ownsMaterial=true;}
            draw.sharedMaterial=material;
            Hide();
        }

        internal void Rebuild(IReadOnlyList<Vector3> samples,IReadOnlyList<float> distance,float boreRadius)
        {
            mesh.Clear();
            boreRings=0;
            spillInitialized=false;
            if(samples.Count<2 || distance[^1]<.001f)return;
            boreRings=samples.Count;
            boreLength=distance[^1];
            radius=boreRadius;
            int rings=boreRings+SpillRings;
            vertices=new Vector3[rings*RingSize];
            uv=new Vector2[vertices.Length];
            flowData=new Vector4[vertices.Length];
            Vector3 up=Vector3.zero;
            for(int i=0;i<boreRings;i++)
            {
                int before=Mathf.Max(0,i-1),after=Mathf.Min(boreRings-1,i+1);
                var forward=(samples[after]-samples[before]).normalized;
                if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
                var gravity=Vector3.ProjectOnPlane(Vector3.up,forward);
                var previous=Vector3.ProjectOnPlane(up,forward);
                if(previous.sqrMagnitude<.01f)previous=gravity.sqrMagnitude>.01f?gravity:
                    Vector3.ProjectOnPlane(Vector3.right,forward);
                up=Vector3.Lerp(previous.normalized,gravity.normalized,Mathf.SmoothStep(0,1,gravity.magnitude)).normalized;
                if(up.sqrMagnitude<.01f)up=previous.normalized;
                WriteRing(i,samples[i],up,forward,distance[i],distance[i]/boreLength,1,1,1);
                outletTangent=forward;
            }
            outletUp=up;
            int face=0;
            var indices=new int[(rings-1)*(BodyFacets+SurfaceFacets)*6];
            for(int i=1;i<rings;i++)
            {
                int row=i*RingSize;
                for(int edge=0;edge<BodyFacets;edge++)Panel(indices,ref face,row+edge);
                for(int edge=0;edge<SurfaceFacets;edge++)Panel(indices,ref face,row+BodyFacets+1+edge);
            }
            mesh.vertices=vertices;
            mesh.triangles=indices;
        }

        void WriteRing(int ring,Vector3 center,Vector3 up,Vector3 forward,float distance,float along,
            float width,float height,float opacity)
        {
            var across=Vector3.Cross(up,forward).normalized*(radius*width);
            var vertical=up*(radius*height);
            int row=ring*RingSize;
            for(int edge=0;edge<=BodyFacets;edge++)
            {
                float angle=Mathf.Lerp(.13f,-Mathf.PI-.13f,edge/(float)BodyFacets);
                float level=Mathf.Sin(angle);
                int vertex=row+edge;
                vertices[vertex]=center+across*Mathf.Cos(angle)+vertical*level;
                uv[vertex]=new Vector2(distance,level);
                flowData[vertex]=new Vector4(along,0,Mathf.Max(0,level)*radius*height,opacity);
            }
            for(int edge=0;edge<=SurfaceFacets;edge++)
            {
                float x=2*edge/(float)SurfaceFacets-1;
                float level=Mathf.Sin(.13f)+(1-x*x)*.08f;
                int vertex=row+BodyFacets+1+edge;
                vertices[vertex]=center+across*(x*Mathf.Cos(.13f))+vertical*level;
                uv[vertex]=new Vector2(distance,x);
                flowData[vertex]=new Vector4(along,1,level*radius*height,opacity);
            }
        }

        void ShapeSpill(PipeSpill spill)
        {
            Vector3 impact=route.InverseTransformPoint(spill.Impact);
            if(spillInitialized && (lastImpact-impact).sqrMagnitude<.000001f)return;
            spillInitialized=true;
            lastImpact=impact;
            Vector3 up=outletUp,previous=outletTangent;
            Vector3 last=spill.Point(0);
            float distance=boreLength;
            for(int ring=1;ring<=SpillRings;ring++)
            {
                float t=ring/(float)SpillRings;
                Vector3 center=spill.Point(t);
                Vector3 tangent=route.InverseTransformVector(spill.Tangent(t)).normalized;
                if(tangent.sqrMagnitude<.01f)tangent=previous;
                up=Quaternion.FromToRotation(previous,tangent)*up;
                distance+=route.InverseTransformVector(center-last).magnitude;
                float taper=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.73f,t));
                WriteRing(boreRings+ring-1,route.InverseTransformPoint(center),up,tangent,distance,1+t,
                    Mathf.Lerp(1,settings.spillWidth,taper),Mathf.Lerp(1,settings.spillHeight,taper),
                    1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,t)));
                previous=tangent;last=center;
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uv);
            mesh.SetUVs(1,flowData);
            mesh.RecalculateBounds();
        }

        static void Panel(int[] indices,ref int face,int left)
        {
            indices[face++]=left-RingSize;indices[face++]=left;indices[face++]=left-RingSize+1;
            indices[face++]=left-RingSize+1;indices[face++]=left;indices[face++]=left+1;
        }

        internal void Present(float head,float tail,float boreSeconds,PipeSpill spill)
        {
            if(!material || boreRings==0)return;
            ShapeSpill(spill);
            block.SetVector(Flow,new Vector4(head,tail,boreSeconds,Mathf.Max(.08f,settings.spillDuration)));
            draw.SetPropertyBlock(block);
            draw.enabled=true;
        }

        static void Release(Object asset)
        {
            if(Application.isPlaying)Object.Destroy(asset);else Object.DestroyImmediate(asset);
        }

        internal void Hide(){if(draw)draw.enabled=false;}
        public void Dispose()
        {
            if(draw)Release(draw.gameObject);
            if(mesh)Release(mesh);
            if(ownsMaterial && material)Release(material);
        }
    }
}
