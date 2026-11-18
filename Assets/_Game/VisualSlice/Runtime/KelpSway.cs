using UnityEngine;

namespace Raid.VisualSlice
{
    /// <summary>Local decorative fronds. No gameplay transform or network state is written.</summary>
    public sealed class KelpSway : MonoBehaviour
    {
        public Transform[] blades;
        public float phase;
        public float amplitudeDegrees=4f;
        public float frequency=.32f;
        private Quaternion[] rest;

        private void Start()
        {
            if(blades==null) blades=new Transform[0];
            rest=new Quaternion[blades.Length];
            for(int i=0;i<blades.Length;i++) if(blades[i]!=null) rest[i]=blades[i].localRotation;
        }

        private void LateUpdate()
        {
            if(rest==null) return;
            for(int i=0;i<blades.Length;i++)
            {
                if(blades[i]==null) continue;
                float wave=Mathf.Sin(Time.time*frequency+phase+i*.7f);
                float cross=Mathf.Sin(Time.time*frequency*.71f+phase+i);
                blades[i].localRotation=rest[i]*Quaternion.Euler(cross*amplitudeDegrees*.45f,0,wave*amplitudeDegrees);
            }
        }
    }
}
