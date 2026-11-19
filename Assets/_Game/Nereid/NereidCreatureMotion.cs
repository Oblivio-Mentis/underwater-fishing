using UnityEngine;
using System.Collections.Generic;
namespace Raid.Nereid
{
    [DefaultExecutionOrder(700)]
    public sealed class NereidCreatureMotion : MonoBehaviour
    {
        public Renderer[] eyes;
        Vector3 rest;
        Vector3 restPosition;
        float swallowedAt=-10,burpedAt=-10,chargeUntil=-10,chargeDuration=.44f,burpSeverity=1;
        MaterialPropertyBlock block;
        readonly List<SkinnedMeshRenderer> articulated = new();
        void Awake()
        {
            rest=transform.localScale;restPosition=transform.localPosition;block=new MaterialPropertyBlock();
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                var original=filter.GetComponent<MeshRenderer>();
                if(original==null || mesh==null || mesh.GetBlendShapeIndex("NereidInhale")<0)continue;
                // Keep the original MeshFilter and collider and animate a GPU-skinned copy of the renderer.
                var go=new GameObject("Breathing surface");
                go.transform.SetParent(filter.transform,false);
                var animated=go.AddComponent<SkinnedMeshRenderer>();
                animated.sharedMesh=mesh;
                animated.sharedMaterials=original.sharedMaterials;
                animated.shadowCastingMode=original.shadowCastingMode;
                animated.receiveShadows=original.receiveShadows;
                var limits=mesh.bounds;limits.Expand(Vector3.one*3);animated.localBounds=limits;
                articulated.Add(animated);
                if(eyes!=null)for(int i=0;i<eyes.Length;i++)if(eyes[i]==original)eyes[i]=animated;
                original.enabled=false;
            }
        }
        public void ConfirmedSwallow(){swallowedAt=Time.time;}
        public void PrepareBurp(float duration){chargeDuration=Mathf.Max(.2f,duration);chargeUntil=Time.time+chargeDuration;}
        public void ConfirmedBurp(float strength=1){burpedAt=Time.time;chargeUntil=-10;burpSeverity=Mathf.Clamp(strength,1,2.35f);}
        public void ClearFeedingReaction(){swallowedAt=burpedAt=chargeUntil=-10;}
        void LateUpdate()
        {
            float breath=Mathf.Sin(Time.time*.9f)*.004f;
            float age=Time.time-swallowedAt;
            float swallow=age>=0&&age<.55f?Mathf.Sin(age/.55f*Mathf.PI):0;
            age=Time.time-burpedAt;
            float release=(age>=0&&age<1.5f?Mathf.Sin(Mathf.Clamp01(age/.65f)*Mathf.PI)*Mathf.Exp(-age*.7f):0)*Mathf.Sqrt(burpSeverity);
            float charge=chargeUntil>Time.time?1-(chargeUntil-Time.time)/chargeDuration:0;
            float phase=Time.time*.79f;
            foreach(var surface in articulated)
            {
                var mesh=surface.sharedMesh;
                surface.SetBlendShapeWeight(mesh.GetBlendShapeIndex("NereidInhale"),(Mathf.Sin(phase)*.5f+.5f)*94+charge*28);
                surface.SetBlendShapeWeight(mesh.GetBlendShapeIndex("NereidReach"),Mathf.Sin(phase*.72f)*94);
                surface.SetBlendShapeWeight(mesh.GetBlendShapeIndex("NereidCurl"),Mathf.Cos(phase*.72f)*94);
            }
            transform.localScale=Vector3.Scale(rest,new Vector3(1+breath+swallow*.012f+release*.025f,1-breath*.7f+swallow*.009f+charge*.022f-release*.012f,1));
            transform.localPosition=restPosition+Vector3.forward*(release*.045f);
            var w=RaidWorld.Instance;float intensity=1.7f;
            if(w!=null&&w.NetworkObject!=null&&w.IsClientInitialized&&w.View!=null)
                intensity=w.FishRoute.active&&w.FishRoute.feeding?2.7f:w.View.hungerRemaining<20?2.2f:1.7f;
            intensity+=swallow*.45f+charge*.65f+release*1.0f;
            block.SetColor("_EmissionColor",new Color(.9f,.3f,.055f)*intensity);
            if(eyes!=null)foreach(var eye in eyes)if(eye!=null)eye.SetPropertyBlock(block);
        }
        void OnDisable()
        {
            ClearFeedingReaction();if(block==null)return;
            transform.localScale=rest;transform.localPosition=restPosition;block.Clear();
            if(eyes!=null)foreach(var eye in eyes)if(eye!=null)eye.SetPropertyBlock(block);
        }
    }
}
