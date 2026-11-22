using UnityEngine;

namespace Raid
{
    [CreateAssetMenu(menuName = "RAID/Fishing tuning")]
    public sealed class FishingTuning : ScriptableObject
    {
        public float maxRange = 20f;
        public float fireCooldown = 0.6f;
        public float heartbeatInterval = 0.08f;
        public float inputTimeout = 0.35f;
        public float hookSettleSeconds = 0.3f;
        public float swimRadius = 3f;
        public float roamSpeed = 1.45f;
        public float burstPeriod = 3f;
        public float burstDuration = 0.8f;
        public float initialStamina = 7f;
        public float exhaustionRate = 1f;
        public float staminaRecovery = 0.15f;
        public float tensionRise = 0.09f;
        public float burstTensionMultiplier = 4f;
        public float tensionRelease = 0.5f;
        public float reelSpeed = 2.5f;
        public float escapeSpeed = 0.8f;
        public float tugSpeed = 3.5f;
        [Min(.1f)] public float tugAcceleration = 12f, maxTugSpeed = 4.5f;
        [Min(.05f)] public float warningSeconds = .55f, minimumWindingSeconds = .4f;
        [Min(.02f)] public float slackAllowance = .18f;
        [Range(-1, 1)] public float pullDirectionDot = .35f;
        [Range(0,12)] public float fightFov = 7f;
        [Range(0,3)] public float burstFov = 2f;
        public float captureRange = .72f;
        public float assistRange = 3f;
        public float assistBonus = 0.4f;
        public float obstructionGrace = 1.5f;
        public float escapeRecoverySeconds = 2f;
        public float respawnSeconds = 10f;
        public float snapshotInterval = 0.05f;
        public LayerMask hitMask = ~0;
    }
}
