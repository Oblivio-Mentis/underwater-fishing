using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed class DefenseSignal : MonoBehaviour
    {
        struct Surface
        {
            public Renderer skin, mask, edge;
            public Mesh hull;
        }

        static Material mask, edge;
        static readonly int Opacity=Shader.PropertyToID("_Opacity");
        readonly List<Surface> surfaces=new();
        MaterialPropertyBlock ink;
        bool showing;

        internal static DefenseSignal Attach(Transform model)
        {
            if(!model)return null;
            var shader=Resources.Load<Shader>("RaidFishOutline");
            if(!shader)return null;
            if(!mask)
            {
                mask=DroppedItemView.OutlinePass(shader,0,Color.clear,2995,true,16);
                edge=DroppedItemView.OutlinePass(shader,2.7f,new Color(1.30f,.135f,.095f,.86f),3008,false,16);
            }
            var signal=model.gameObject.AddComponent<DefenseSignal>();
            signal.ink=new MaterialPropertyBlock();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.GetComponent<SquidTongueView>())continue;
                var skin=renderer as SkinnedMeshRenderer;
                var mesh=skin?skin.sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if(!mesh)continue;
                var hull=mesh.isReadable?DroppedItemView.CreateOutlineHull(mesh):null;
                if(skin && hull)
                {
                    hull.bindposes=mesh.bindposes;
                    hull.boneWeights=mesh.boneWeights;
                }
                signal.surfaces.Add(new Surface{
                    skin=renderer,hull=hull,mask=MakePass(renderer,skin,hull?hull:mesh,mask),
                    edge=MakePass(renderer,skin,hull?hull:mesh,edge)
                });
            }
            return signal;
        }

        static Renderer MakePass(Renderer source,SkinnedMeshRenderer skin,Mesh mesh,Material material)
        {
            Renderer pass;
            if(skin)
            {
                var node=new GameObject("Defense silhouette");
                node.transform.SetParent(source.transform,false);
                var baked=node.AddComponent<SkinnedMeshRenderer>();
                baked.sharedMesh=mesh;
                baked.bones=skin.bones;
                baked.rootBone=skin.rootBone;
                baked.localBounds=skin.localBounds;
                baked.quality=skin.quality;
                baked.shadowCastingMode=ShadowCastingMode.Off;
                baked.receiveShadows=false;
                baked.enabled=false;
                pass=baked;
            }
            else pass=DroppedItemView.OutlinePart(source.transform,mesh,material);
            pass.localBounds=source.localBounds;
            var paints=new Material[mesh.subMeshCount];
            System.Array.Fill(paints,material);
            pass.sharedMaterials=paints;
            return pass;
        }

        internal void Present(bool active,bool rescue=false,RemoraStretch stretch=default)
        {
            if(!active && !showing)return;
            showing=active;
            float beat=.5f+.5f*Mathf.Sin(Time.time*(rescue?9.5f:5f));
            ink.SetFloat(Opacity,Mathf.Lerp(rescue?.71f:.58f,1,beat));
            stretch.Apply(ink);
            foreach(var part in surfaces)
            {
                bool enabled=active && part.skin && part.skin.enabled;
                part.mask.enabled=part.edge.enabled=enabled;
                if(enabled)
                {
                    part.mask.SetPropertyBlock(ink);
                    part.edge.SetPropertyBlock(ink);
                }
            }
        }

        void OnDestroy()
        {
            foreach(var part in surfaces)if(part.hull)Destroy(part.hull);
        }
    }
}
