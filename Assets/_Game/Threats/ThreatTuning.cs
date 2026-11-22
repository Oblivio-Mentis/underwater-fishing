using UnityEngine;

namespace Raid
{
    [CreateAssetMenu(menuName = "RAID/Threat Tuning")]
    public sealed class ThreatTuning : ScriptableObject
    {
        public Vector3 predatorSpawn = new Vector3(12f, -5f, 22f);
        public Vector3 waveSpawn = new Vector3(0f, 1.5f, 30f);
        public Vector3 airlockWaypoint = new Vector3(0f, 1.5f, 9f);
        public Vector3 reservoirPoint = new Vector3(0f, .6f, -3f);
        public Vector3 reservoirApproachOffset = new Vector3(0f, 1f, 2.3f);
        [Min(.01f)] public float snapshotInterval = .1f;
        [Min(.1f)] public float predatorSpeed = 2.5f;
        [Min(.1f)] public float detectionRange = 16f;
        [Min(.1f)] public float attackRange = 2f;
        [Min(.1f)] public float attackInterval = 5f;
        [Min(0f)] public float attackDamage = 14f;
        [Min(1f)] public float predatorHealth = 12f;
        [Min(1f)] public float predatorRespawnSeconds = 25f;
        [Min(1f)] public float depthTwoStrength = 2f;
        [Min(0f)] public float patrolRadius = 5f;
        [Min(.01f)] public float patrolSpeed = .5f;
        [Min(.1f)] public float pursuitSpeed = 2f;
        [Min(.05f)] public float raiderBodyRadius = .65f;
        [Min(1f)] public float waveHealth = 10f;
        [Min(1f)] public float waveSeconds = 45f;
        [Min(.1f)] public float stockAttackInterval = 5f;
        [Min(1)] public int maximumWaveAttacks = 3;
        [Min(.1f)] public float strikeRange = 20f;
        [Min(.1f)] public float strikeRadius = 1.25f;
        [Min(1f)] public float maximumStrikePower = 100f;
    }
}
