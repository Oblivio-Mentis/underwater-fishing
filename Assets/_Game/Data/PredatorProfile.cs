using UnityEngine;

namespace Raid
{
    // Hunt cycle numbers for the reef angler (Фонарник). Territory offsets are relative to ThreatTuning.predatorSpawn.
    [CreateAssetMenu(menuName = "RAID/Predator profile")]
    public sealed class PredatorProfile : ScriptableObject
    {
        [Header("Territory")]
        public Vector3[] territoryOffsets = { Vector3.zero, new(6, -1.2f, -6), new(-2, .4f, -8), new(4, -2, 3) };
        [Min(1f)] public float territoryRadius = 7f;
        [Min(.1f)] public float territoryHeight = 2.2f;
        [Min(.1f)] public float bodyRadius = .53f;
        [Min(1f)] public float leashDistance = 30f;
        [Min(0f)] public float respawnMinDistance = 11f;
        [Min(0f)] public float respawnClearView = 20f;
        [Min(.1f)] public float respawnRetrySeconds = 2f;
        public int seed = 4471;

        [Header("Patrol")]
        [Min(.1f)] public float patrolSpeed = 1.5f;
        [Min(.1f)] public float recoverySpeed = 2.6f;
        [Min(1f)] public float patrolGoalSeconds = 9f;
        [Range(0f, 1f)] public float territorySwitchChance = .3f;
        [Min(.1f)] public float swimTurnRate = 4.7f;

        [Header("Senses")]
        [Min(.5f)] public float senseDistance = 5.25f;
        [Min(1f)] public float deepSpeedMultiplier = 1.14f;
        [Min(0f)] public float senseDelay = .3f;
        [Min(.1f)] public float sensePeriod = .18f;
        [Min(0f)] public float noiseDistance = 14f;
        [Min(0f)] public float noiseMemory = 8f;
        [Min(0f)] public float noiseCooldown = 8f;
        [Min(.1f)] public float losLossTimeout = 1.4f;

        [Header("Investigate and circle")]
        [Min(.1f)] public float investigateSpeed = 2.4f;
        [Min(1f)] public float circleRadiusMin = 4f;
        [Min(1f)] public float circleRadiusMax = 8f;
        [Min(0f)] public float circleHeightVariation = 1.6f;
        [Min(.1f)] public float circleSpeed = 2.9f;
        [Min(.1f)] public float circleRadialSpeed = 1.8f;
        [Range(0f, 1f)] public float arcReverseChance = .35f;
        [Min(.2f)] public float arcSecondsMin = 1.4f;
        [Min(.2f)] public float arcSecondsMax = 3.2f;
        [Min(0f)] public float stalkSecondsMin = 1.8f;
        [Min(0f)] public float stalkSecondsMax = 4f;
        [Min(0f)] public float closeStrikeDistance = 3.2f;

        [Header("Telegraph and charge")]
        [Min(.1f)] public float telegraphMin = .9f;
        [Min(.1f)] public float telegraphMax = 1.3f;
        [Min(0f)] public float aimLockSeconds = .2f;
        [Min(.1f)] public float telegraphTurnRate = 7f;
        [Min(0f)] public float telegraphDrift = .7f;
        [Min(.1f)] public float chargeSpeed = 7.5f;
        [Min(.1f)] public float chargeMaxSeconds = 2.2f;
        [Min(.05f)] public float biteRadius = .55f;
        [Min(0f)] public float biteShove = 2.4f;
        [Min(.1f)] public float overshootDistance = 3.6f;
        [Min(1f)] public float overshootTurnRate = 95f;
        [Min(1)] public int passesPerHunt = 2;

        [Header("Aggression reset")]
        [Min(1)] public int hitsToDisengage = 2;
        [Min(0f)] public float disengageCooldown = 8f;
        [Min(1)] public int bitesToAggressionReset = 2;
        [Min(0f)] public float satiatedCooldown = 16f;
        [Min(0f)] public float lostInterestCooldown = 3f;
        [Min(0f)] public float stunnedCooldown = 3.4f;
    }
}
