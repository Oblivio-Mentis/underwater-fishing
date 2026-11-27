using UnityEngine;

namespace Raid
{
    internal struct RemoraStretch
    {
        static readonly int Jaw=Shader.PropertyToID("_RemoraJaw");
        static readonly int Forward=Shader.PropertyToID("_RemoraForward");
        static readonly int Pull=Shader.PropertyToID("_RemoraPull");

        public Vector3 mouth, direction, displacement;
        public float inverseLength;

        internal static float InverseLength(float scale) => 1/Mathf.Max(.2f,2.25f*scale);

        internal void Apply(MaterialPropertyBlock properties)
        {
            properties.SetVector(Jaw,new Vector4(mouth.x,mouth.y,mouth.z,inverseLength));
            properties.SetVector(Forward,direction);
            properties.SetVector(Pull,displacement);
        }

        internal Vector3 Point(Vector3 world)
        {
            if(inverseLength<=0)return world;
            Vector3 fromJaw=world-mouth;
            float along=Vector3.Dot(fromJaw,direction);
            float aft=-along*inverseLength;
            float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.97f,aft));
            float load=Mathf.Clamp01(displacement.magnitude*inverseLength);
            float taper=load*weight*(.61f-.26f*weight);
            return world+displacement*weight-(fromJaw-direction*along)*taper;
        }
    }
}
