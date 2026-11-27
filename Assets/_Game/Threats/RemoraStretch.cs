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

        internal void Apply(MaterialPropertyBlock properties)
        {
            properties.SetVector(Jaw,new Vector4(mouth.x,mouth.y,mouth.z,inverseLength));
            properties.SetVector(Forward,direction);
            properties.SetVector(Pull,displacement);
        }

        internal Vector3 Point(Vector3 world)
        {
            float aft=Vector3.Dot(mouth-world,direction)*inverseLength;
            float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.98f,aft));
            return world+displacement*weight;
        }
    }
}
