using UnityEngine;

namespace Raid
{
    /// <summary>Separate opt-in tuning; existing fishing and progression assets are not changed.</summary>
    [CreateAssetMenu(menuName = "RAID/Friend Tether Tuning")]
    public sealed class FriendTetherTuning : ScriptableObject
    {
        [Min(2f)] public float maximumRange = 12f;
        [Min(.1f)] public float pullSpeed = 2.5f;
        [Min(.8f)] public float stopDistance = 1.8f;
        [Range(0f, .5f)] public float lineOfSightGrace = .15f;
        [Min(.2f)] public float idleReleaseSeconds = 1f;
        [Min(1f)] public float maximumDuration = 10f;
        [Min(3f)] public float optOutSeconds = 3f;
        [Range(.1f, 1f)] public float towedFishingDragMultiplier = .5f;
    }
}
