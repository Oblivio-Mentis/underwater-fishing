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
        // Deprecated since RAID-018: surge timing is per species in FishFightProfile. Kept so old assets load.
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
        [Header("Heavy fish solo tow")]
        [Min(.1f)] public float soloHeavyTugSpeed = 2.4f;
        [Min(.1f)] public float soloHeavyTugAcceleration = 3.5f;
        [Min(.1f)] public float soloHeavyEscapeSpeedMultiplier = 1f;
        [Range(.05f, 1f)] public float soloHeavyTensionMultiplier = .35f;
        [Range(0f, 1f)] public float soloHeavyFeedback = .82f;
        [Range(.1f, 1f)] public float gripTugMultiplier = .55f;
        [Min(.1f)] public float soloHeavyCameraImpulse = 2.5f;
        [Min(.001f)] public float soloHeavyCameraHit = .012f;
        [Min(0f)] public float fightTrembleMultiplier = 1f;
        [Min(.05f)] public float warningSeconds = .55f, minimumWindingSeconds = .4f;
        [Min(.02f)] public float slackAllowance = .18f;
        [Range(-1, 1)] public float pullDirectionDot = .35f;
        [Range(0,12)] public float fightFov = 7f;
        [Range(0,3)] public float burstFov = 2f;
        public float captureRange = .72f;
        [Min(0), Tooltip("Radius of the forgiving sphere-cast used when the exact aim ray misses a fish.")] public float hitAssistRadius = .16f;
        public float assistRange = 3f;
        public float assistBonus = 0.4f;
        public float obstructionGrace = 1.5f;
        public float escapeRecoverySeconds = 2f;
        public float respawnSeconds = 10f;
        public float snapshotInterval = 0.05f;
        public LayerMask hitMask = ~0;
        [Tooltip("One fight profile per species; matched by speciesId. Missing species fall back to the first entry.")]
        public FishFightProfile[] fightProfiles = System.Array.Empty<FishFightProfile>();

        static FishFightProfile fallbackFight;
        public FishFightProfile FightProfile(string species)
        {
            foreach (var profile in fightProfiles)
                if (profile != null && profile.speciesId == species) return profile;
            if (fightProfiles.Length > 0 && fightProfiles[0] != null) return fightProfiles[0];
            if (fallbackFight == null) fallbackFight = CreateInstance<FishFightProfile>();
            return fallbackFight;
        }
    }
}
