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
        public float oxygenCapacity = 120f;
        public float oxygenRecovery = 20f;
        public float safeDepth = 35f;
        public float inputTimeout = 0.3f;
        public float sendInterval = 0.05f;
    }
}
