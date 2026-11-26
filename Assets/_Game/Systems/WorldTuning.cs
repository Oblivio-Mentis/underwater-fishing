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

        [Header("Expedition equipment")]
        [Min(0)] public int rigRepairScrap=1;
        [Min(1)] public int sampleBasketCapacity=3;
        [Min(1)] public int maxSampleBaskets=8;
        [Min(.45f)] public float dispenserFlightSeconds=.68f;
        [Min(.05f)] public float dispenserParcelRadius=.09f;
        [Min(.1f)] public float dispenserCatchRadius=.56f;
        [Min(2)] public float dispenserReturnSeconds=18f;
        [Range(.05f,1)] public float soloTowSpeed=.38f,teamTowSpeed=.82f;

        [Header("Gosha and the deck ball")]
        [Range(.28f,.48f)] public float petScale=.365f;
        [Min(.5f)] public float petReach=2.05f;
        [Min(.2f)] public float patCooldown=1.35f;
        [Min(.5f)] public float petCrawlSpeed=2.3f, petFetchSpeed=4.8f, petChaseSpeed=5.8f;
        [Min(1)] public float petSwimSpeed=3.8f, petCatchUpSpeed=6.2f;
        [Min(5)] public float fetchTimeout=24f, lostCompanionSeconds=32f;
        [Min(1)] public float petOrderCooldown=42f, petOrderTimeout=110f, petSearchRadius=28f, petPocketWait=12f;
        [Range(.5f,1.5f)] public float playfulFallSeconds=1.2f;
        [Min(.1f)] public float ballReelSpeed=4f;
        [Min(.15f)] public float ballHandReach=.42f;
        [Min(.2f)] public float ballLineIdleSeconds=8f;
        [Min(1)] public float ballThrowSpeed=15.6f;
        [Min(.1f)] public float ballWindSeconds=.55f;
        [Min(.1f)] public float ballShortThrow=4.6f;
        [Min(0)] public float ballThrowLift=2f;
    }
}
