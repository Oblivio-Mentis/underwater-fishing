using System;
using UnityEngine;

namespace UnderwaterFishing.Diving
{
    [Serializable]
    public sealed class DivingSettings
    {
        [Min(0f)] public float OxygenCapacity = 180f;
        [Min(0f)] public float OxygenDrain = 1f;
        [Min(0f)] public float SprintOxygenMultiplier = 2f;
        [Min(0f)] public float RefillRate = 12f;
        [Min(0f)] public float SafeDepth = 35f;
        [Min(0f)] public float SafeAscentSpeed = 2f;
        [Min(0f)] public float SafeDescentSpeed = 4f;
        [Min(0f)] public float StressGainDepth = 0.7f;
        [Min(0f)] public float StressGainSpeed = 5f;
        [Min(0f)] public float StressRecovery = 4f;
        [Min(0f)] public float CriticalStress = 100f;
        [Min(0f)] public float WarningStress = 40f;
        [Min(0f)] public float CriticalWarningStress = 75f;
        [Min(0f)] public float RescueOxygen = 30f;
        [Min(0f)] public float IncapacitatedSeconds = 35f;
    }
}
