using UnityEngine;

namespace Raid
{
    // Every number that shapes the pet's hunt, carry and delivery, indexed by the team rank
    // of the pet sector. Index 0 is the untrained helper; the arrays end at the top rank.
    [CreateAssetMenu(menuName = "RAID/Gosha hunt tuning")]
    public sealed class GoshaHuntTuning : ScriptableObject
    {
        [Header("Swimming by speed rank 0..5")]
        public float[] maxSpeed = { 3.2f, 3.8f, 4.3f, 4.3f, 4.8f, 5.6f };
        public float[] acceleration = { 4f, 5f, 6.5f, 6.5f, 7.5f, 9f };
        [Tooltip("Degrees per second.")] public float[] turnRate = { 150f, 160f, 200f, 200f, 240f, 280f };
        [Tooltip("Carrying N items multiplies speed by (1 - loadPenalty * (N-1)); the fourth claws rank halves the penalty.")]
        [Range(0, .4f)] public float loadPenalty = .12f;

        [Header("Lunge: opens at speed rank III, torpedo at V")]
        public float lungeSpeed = 7.5f, torpedoSpeed = 9.8f;
        public float lungeSeconds = .55f, torpedoSeconds = .8f;
        public float lungeCooldown = 4f, lungeCooldownRankIV = 2.6f;
        [Tooltip("Lunge starts inside this distance to the fish.")] public float lungeTrigger = 2.4f, torpedoTrigger = 3.6f;

        [Header("Capture")]
        [Tooltip("Jaw socket to fish centre distance that counts as a grab.")] public float captureReach = .62f;
        [Tooltip("Extra reach while lunging.")] public float lungeReachBonus = .14f;
        [Tooltip("First claws rank: a held catch no longer slips on a hard turn; reach grows by this.")] public float clawsReachBonus = .1f;

        [Header("Chase limits")]
        public float chaseTimeout = 14f, chaseTimeoutRankIV = 20f;
        [Tooltip("Distance from the hunter past which the target is lost.")] public float loseDistance = 26f;
        public int retriesPerOrder = 2, retriesRankIV = 4;
        [Tooltip("Seconds between habitat scans when nothing is available.")] public float scanBackoff = 1.5f;
        [Tooltip("Seconds a fish that escaped stays off the list.")] public float escapedCooldown = 9f;
        [Tooltip("Intercept rank: predicted lead time cap in seconds.")] public float interceptLead = 1.25f;

        [Header("Fish perception of the hunter")]
        [Tooltip("Multiplies the species sense distance while the hunter is chasing.")] public float huntSenseScale = 1f;
        [Tooltip("Multiplies the species sense distance while the hunter only escorts.")] public float escortSenseScale = .55f;

        [Header("Divers' interest: Do not touch theirs")]
        [Tooltip("Cosine of the aim cone in which a fish counts as the diver's target.")] [Range(.5f, 1f)] public float aimCone = .93f;
        public float aimRange = 16f;

        [Header("Carry by claws rank 0..5")]
        public int[] capacity = { 1, 1, 2, 3, 4, 4 };
        [Tooltip("Trawl: fish of one school caught during one pass.")] public int trawlCatches = 3;
        public float trawlSeconds = 2.6f, trawlCooldown = 18f;

        [Header("Materials by nose rank 0..5")]
        public float[] searchRadius = { 0, 18f, 24f, 28f, 32f, 40f };
        public float grabSeconds = 1.4f;

        [Header("Delivery")]
        public float handoverWait = 12f, handoverWaitRankI = 20f;
        public float cageWait = 25f;
        public float unloadSeconds = 1.1f;

        public static GoshaHuntTuning Default
        {
            get
            {
                if (fallback == null) { fallback = CreateInstance<GoshaHuntTuning>(); fallback.hideFlags = HideFlags.HideAndDontSave; }
                return fallback;
            }
        }
        static GoshaHuntTuning fallback;

        public GoshaHuntStats Stats(int speedRank, int autonomyRank, int clawsRank, int carried)
        {
            int s = Mathf.Clamp(speedRank, 0, maxSpeed.Length - 1);
            float load = Mathf.Max(0, carried - 1) * loadPenalty * (clawsRank >= 4 ? .5f : 1f);
            bool torpedo = speedRank >= 5;
            return new GoshaHuntStats
            {
                MaxSpeed = maxSpeed[s] * Mathf.Max(.35f, 1 - load),
                Acceleration = acceleration[Mathf.Min(s, acceleration.Length - 1)],
                TurnRate = turnRate[Mathf.Min(s, turnRate.Length - 1)] * (clawsRank >= 4 ? 1f : Mathf.Max(.6f, 1 - load)),
                HasLunge = speedRank >= 3,
                LungeSpeed = torpedo ? torpedoSpeed : lungeSpeed,
                LungeSeconds = torpedo ? torpedoSeconds : lungeSeconds,
                LungeCooldown = speedRank >= 4 ? lungeCooldownRankIV : lungeCooldown,
                LungeTrigger = torpedo ? torpedoTrigger : lungeTrigger,
                CaptureReach = captureReach + (clawsRank >= 1 ? clawsReachBonus : 0),
                LungeReachBonus = lungeReachBonus,
                ChaseTimeout = autonomyRank >= 4 ? chaseTimeoutRankIV : chaseTimeout,
                LoseDistance = loseDistance,
                Retries = autonomyRank >= 4 ? retriesRankIV : retriesPerOrder,
                Intercept = autonomyRank >= 7,
                InterceptLead = interceptLead,
                Capacity = capacity[Mathf.Clamp(clawsRank, 0, capacity.Length - 1)]
            };
        }
    }

    public struct GoshaHuntStats
    {
        public float MaxSpeed, Acceleration, TurnRate;
        public bool HasLunge, Intercept;
        public float LungeSpeed, LungeSeconds, LungeCooldown, LungeTrigger, LungeReachBonus;
        public float CaptureReach, ChaseTimeout, LoseDistance, InterceptLead;
        public int Retries, Capacity;
    }
}
