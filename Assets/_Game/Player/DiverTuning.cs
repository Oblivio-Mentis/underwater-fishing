using UnityEngine;

namespace Raid
{
    [CreateAssetMenu(menuName = "RAID/Diver tuning")]
    public sealed class DiverTuning : ScriptableObject
    {
        public float swimSpeed = 4f;
        public float sprintMultiplier = 1.6f;
        public float verticalSpeed = 2f;
        public float lookSensitivity = 0.12f;
        public float oxygenCapacity = 240f;
        public float oxygenRecovery = 20f;
        [Min(.1f)] public float suffocationDamagePerSecond = 4.5f;
        [Min(0)] public float onboardHealingPerSecond = 5f;
        public float safeDepth = 35f;
        public float inputTimeout = 0.3f;
        public float sendInterval = 0.05f;
        public float helplessWindow = 45f;
        public float rescueOxygen = 30f;
        public float rescueSeconds = 3f;
        public float trappedDuration = 12f;
        public float stressWarning = 50f;
        public float stressDanger = 85f;
        public float stressOxygenDrain = 6f;
    }
}
