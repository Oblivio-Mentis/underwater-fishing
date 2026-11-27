using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class DefenseAtmosphereView
    {
        const int CloudCapacity=110;
        static readonly int CloudBatchId=Shader.PropertyToID("_CloudBatch");
        static readonly int CloudPointsId=Shader.PropertyToID("_CloudPoints");
        static readonly int CloudHeadingsId=Shader.PropertyToID("_CloudHeadings");
        static readonly int CloudMotionId=Shader.PropertyToID("_CloudMotion");
        sealed class CloudBatch
        {
            public int count;
            public Mesh mesh;
            public MeshRenderer renderer;
            public Vector4[] points, headings, motion;
            public MaterialPropertyBlock[] properties;
        }

        readonly CloudBatch[] cloud=new CloudBatch[SpeciesCount];
        Material glowHaze;
        public int CloudCount {get;private set;}

        void BindCloud(DefenseAtmosphereSettings profile)
        {
            if(!material)return;
            glowHaze=new Material(material){name="Swarm · luminous eye haze",renderQueue=(int)RenderQueue.Transparent-20};
            glowHaze.SetInt("_SrcBlend",(int)BlendMode.One);
            glowHaze.SetInt("_DstBlend",(int)BlendMode.One);
            glowHaze.SetInt("_ZWrite",0);
            int total=Mathf.Clamp(profile.cloudCount,0,CloudCapacity*SpeciesCount);
            var coats=new[]{material,material,glowHaze};
            for(int species=0;species<SpeciesCount;species++)
            {
                int count=(total+SpeciesCount-1-species)/SpeciesCount;
                var model=meshes[species];
                if(!model || model.subMeshCount<2 || count<=0)continue;
                var combined=BuildCloudMesh(model,count);
                var node=new GameObject("Distant raid bank · "+ModelNames[species],typeof(MeshFilter),typeof(MeshRenderer));
                node.transform.SetParent(transform,false);
                node.GetComponent<MeshFilter>().sharedMesh=combined;
                var renderer=node.GetComponent<MeshRenderer>();
                renderer.sharedMaterials=coats;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;
                renderer.enabled=false;
                cloud[species]=new CloudBatch{
                    count=count,mesh=combined,renderer=renderer,points=new Vector4[CloudCapacity],
                    headings=new Vector4[CloudCapacity],motion=new Vector4[CloudCapacity],
                    properties=new[]{new MaterialPropertyBlock(),new MaterialPropertyBlock(),new MaterialPropertyBlock()}};
                CloudCount+=count;
            }
        }

        static Mesh BuildCloudMesh(Mesh source,int count)
        {
            var body=source.GetIndices(0);
            var eyes=source.GetIndices(1);
            var positions=source.vertices;
            var normals=source.normals;
            var paint=source.colors;
            var origin=new Vector3[2];
            var iris=new Color[2];
            var samples=new int[2];
            foreach(int index in eyes)
            {
                int side=positions[index].x<0?0:1;
                origin[side]+=positions[index];
                iris[side]+=paint[index];
                samples[side]++;
            }
            var points=new List<Vector3>(count*(positions.Length+8));
            var facing=new List<Vector3>(points.Capacity);
            var tint=new List<Color>(points.Capacity);
            var instance=new List<Vector4>(points.Capacity);
            var hides=new List<int>(count*body.Length);
            var pupils=new List<int>(count*eyes.Length);
            var halos=new List<int>(count*12);
            for(int swimmer=0;swimmer<count;swimmer++)
            {
                int offset=points.Count;
                points.AddRange(positions);
                facing.AddRange(normals);
                tint.AddRange(paint);
                for(int i=0;i<positions.Length;i++)instance.Add(new Vector4(swimmer,0,0,0));
                foreach(int id in body)hides.Add(offset+id);
                foreach(int id in eyes)pupils.Add(offset+id);
                for(int side=0;side<2;side++)
                {
                    if(samples[side]==0)continue;
                    int first=points.Count;
                    for(int i=0;i<4;i++)
                    {
                        points.Add(origin[side]/samples[side]);
                        facing.Add(side==0?Vector3.left:Vector3.right);
                        tint.Add(iris[side]/samples[side]);
                        instance.Add(new Vector4(swimmer,i%2==0?-1:1,i<2?-1:1,1));
                    }
                    halos.Add(first);halos.Add(first+1);halos.Add(first+2);
                    halos.Add(first+1);halos.Add(first+3);halos.Add(first+2);
                }
            }
            var result=new Mesh{name=source.name+" · shoal bank",indexFormat=IndexFormat.UInt32,subMeshCount=3};
            result.SetVertices(points);
            result.SetNormals(facing);
            result.SetColors(tint);
            result.SetUVs(1,instance);
            result.SetTriangles(hides,0,false);
            result.SetTriangles(pupils,1,false);
            result.SetTriangles(halos,2,false);
            // Individual points are placed in world space in the vertex stage.
            result.bounds=new Bounds(Vector3.zero,Vector3.one*960);
            result.UploadMeshData(true);
            return result;
        }

        void PresentCloud(DefenseAtmosphereSettings profile,float age,float approach,float retreat)
        {
            if(CloudCount==0)return;
            float arrival=phase==1?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.07f,.88f,warningReveal)):1;
            float fade=arrival*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.6f,8f,retreat)));
            if(fade<.015f){HideCloud();return;}
            float range=profile.cloudDrawingDistance*profile.cloudDistanceScale;
            for(int kind=0;kind<cloud.Length;kind++)
            {
                var batch=cloud[kind];
                if(batch==null)continue;
                for(int i=0;i<batch.count;i++)
                {
                    int slot=i*SpeciesCount+kind;
                    var pose=SampleShoal(raidSeed,slot+SlotCapacity,approach,approachSeconds,anchor,approachStart,
                        approachEnd,retreat,true,profile);
                    var position=pose.Position;
                    var heading=pose.Forward;
                    batch.points[i]=new Vector4(position.x,position.y,position.z,pose.Scale);
                    batch.headings[i]=new Vector4(heading.x,heading.y,heading.z,pose.Bank*Mathf.Deg2Rad);
                    batch.motion[i]=SampleStroke(raidSeed,slot+SlotCapacity,pose,age);
                }
                for(int pass=0;pass<batch.properties.Length;pass++)
                {
                    var block=batch.properties[pass];
                    block.SetFloat(CloudBatchId,1);
                    block.SetFloat(EyePass,pass);
                    block.SetFloat(Fade,fade);
                    block.SetFloat(Range,range);
                    block.SetFloat(Glow,profile.eyeGlow);
                    block.SetVector(Billow,new Vector4(SwimBeat[kind],FinFlex[kind],0,0));
                    block.SetVectorArray(CloudPointsId,batch.points);
                    block.SetVectorArray(CloudHeadingsId,batch.headings);
                    block.SetVectorArray(CloudMotionId,batch.motion);
                    batch.renderer.SetPropertyBlock(block,pass);
                }
                batch.renderer.enabled=true;
            }
        }

        void HideCloud()
        {
            foreach(var batch in cloud)if(batch!=null && batch.renderer)batch.renderer.enabled=false;
        }

        void DisposeCloud()
        {
            foreach(var batch in cloud)if(batch?.mesh)Destroy(batch.mesh);
            if(glowHaze)Destroy(glowHaze);
        }

        void OnDestroy()=>DisposeCloud();
    }
}
