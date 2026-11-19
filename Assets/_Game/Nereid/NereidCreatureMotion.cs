using UnityEngine;
namespace Raid.Nereid
{
    public sealed class NereidCreatureMotion : MonoBehaviour
    {
        public Renderer[] eyes;
        Vector3 rest;
        MaterialPropertyBlock block;
        void Awake(){rest=transform.localScale;block=new MaterialPropertyBlock();}
        void LateUpdate()
        {
            float breath=Mathf.Sin(Time.time*.9f)*.004f;
            transform.localScale=Vector3.Scale(rest,new Vector3(1+breath,1-breath*.7f,1));
            var w=RaidWorld.Instance;float intensity=1.7f;
            if(w!=null&&w.NetworkObject!=null&&w.IsClientInitialized&&w.View!=null)
                intensity=w.FishRoute.active&&w.FishRoute.feeding?2.7f:w.View.hungerRemaining<20?2.2f:1.7f;
            block.SetColor("_EmissionColor",new Color(.9f,.3f,.055f)*intensity);
            if(eyes!=null)foreach(var eye in eyes)if(eye!=null)eye.SetPropertyBlock(block);
        }
    }
}
