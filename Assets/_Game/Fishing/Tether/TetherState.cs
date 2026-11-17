using System;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Tether
{
    [Serializable]
    public struct TetherState
    {
        public float Length;
        public float Tension;
        public float OverloadSeconds;
        public bool Connected;
    }

    public struct TetherForces
    {
        public Vector3 PlayerAcceleration;
        public Vector3 FishAcceleration;
        public float NormalizedTension;
        public bool Broke;
    }
}
