using UnityEngine;
namespace Raid
{
    [CreateAssetMenu(menuName="RAID/World tuning")]
    public sealed class WorldTuning : ScriptableObject
    {
        public Vector3 terminalPosition=new Vector3(0,1,-3);
        public Vector3 cargoPosition=new Vector3(4,.4f,13);
        public float interactionRange=4f;
        public float transitionSeconds=3f;
        public float shallowDepth=20f, deepDepth=60f;
        public float snapshotInterval=.25f;
        public float autoSaveInterval=15f;
        public float rescueRange=2.5f;

        [Header("Gosha and the deck ball")]
        [Min(.5f)] public float petReach=2.05f;
        [Min(.2f)] public float patCooldown=1.35f;
        [Min(.5f)] public float petCrawlSpeed=1.45f, petFetchSpeed=4.3f, petChaseSpeed=5.5f;
        [Min(1)] public float petSwimSpeed=3.8f, petCatchUpSpeed=6.2f;
        [Min(5)] public float fetchTimeout=24f, lostCompanionSeconds=32f;
        [Range(.5f,1.5f)] public float playfulFallSeconds=1.2f;
        [Min(1)] public float ballThrowSpeed=8.4f;
        [Min(.1f)] public float ballWindSeconds=.55f;
        [Min(.1f)] public float ballShortThrow=4.2f;
        [Min(0)] public float ballThrowLift=1.65f;
    }
}
