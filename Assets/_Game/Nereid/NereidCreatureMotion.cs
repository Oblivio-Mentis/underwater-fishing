using UnityEngine;
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
        void Awake(){rest=transform.localScale;restPosition=transform.localPosition;block=new MaterialPropertyBlock();}
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
