using System;

namespace UnderwaterFishing.Diving
{
    public enum DiverLife
    {
        Active,
        Incapacitated,
        Dead
    }

    [Serializable]
    public struct DivingState
    {
        public float Oxygen;
        public float Depth;
        public float AmbientPressure;
        /// <summary>World-space vertical speed in metres per second; positive means ascending.</summary>
        public float VerticalVelocity;
        public float Stress;
        public DiverLife Life;
        public float IncapacitatedRemaining;
    }
}
