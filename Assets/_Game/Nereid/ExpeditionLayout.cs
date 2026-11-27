using UnityEngine;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RAID/Expedition layout")]
    public sealed class ExpeditionLayout : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Depth
        {
            public bool anglerEnabled=true, raiderEnabled=true, clamEnabled=true, eelEnabled=true, krakenEnabled=true;
            [Tooltip("Centres of arrival and patrol zones for the angler.")]
            public Transform[] anglerTerritories;
            [Tooltip("Centre of raider arrival. The entrance still needs an open swim path.")]
            public Transform raiderArrival;
            [Tooltip("Sites projected onto the seabed.")]
            public Transform clamSite, krakenSite;
            [Tooltip("Ordered patrol loop. Eels also arrive around these legs.")]
            public Transform[] eelPatrol;
        }

        [Header("Mob arrivals by expedition depth")]
        public Depth shallows=new(), rift=new();
        [Header("Ship and companion")]
        public Transform dispenser, goshaDen, goshaApproach;
        public GoshaModel goshaModel;
        [Tooltip("Positions and rotations of delivered pieces, in order.")]
        public Transform[] goshaDenSlots;
        [Tooltip("Finds are placed on the seabed below these markers.")]
        public Transform[] goshaJunkSites;
        public Mesh[] goshaJunkMeshes, goshaShelterMeshes;

        [Header("Bow creature")]
        public Transform chargeAperture, flameOutlet;
        [Min(.01f)] public float apertureHalfWidth=1.04f, apertureHalfHeight=.49f;
        [Min(0)] public float flameFirstDamage=86, flameDamagePerSecond=24;
        public Mesh gulletMesh;
        [Header("Seabed scavengers")]
        [Min(1)] public int crabFishPerPile=5, concurrentCrabHauls=4;
        [Min(.25f)] public float crabPileRadius=2.8f;
        [Min(0)] public float crabAttractionSeconds=4f;
        [Tooltip("Optional fissures first preferred near a fish pile. Unassigned areas can still attract crabs.")]
        public Transform[] crabDens;
        public Material gulletMaterial;

        public Depth ForDepth(int depth) => depth>=2?rift:shallows;
        internal static T At<T>(T[] points,int index) where T:Object =>
            points!=null && index>=0 && index<points.Length?points[index]:null;
    }
}
