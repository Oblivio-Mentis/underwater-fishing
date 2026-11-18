using UnityEngine;
namespace Raid.VisualSlice
{
    public sealed class FishVisualMotion : MonoBehaviour
    {
        public Transform tail;
        public FishActor actor;
        private Quaternion initial;
        private void Start() {if(tail!=null)initial=tail.localRotation;}
        private void LateUpdate()
        {
            if(tail==null)return;
            bool struggle=actor!=null && actor.Mode==FishMode.Struggling;
            tail.localRotation=initial*Quaternion.Euler(0,Mathf.Sin(Time.time*(struggle?13:5)+transform.position.x)*(struggle?32:18),0);
        }
    }
}
