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
            [Tooltip("Optional launch markers for groups. Empty uses natural entries from the shoal itself.")]
            public Transform[] defenseApproaches;
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
        [Tooltip("Radius of the projectile entrance in world metres. Marker scale is ignored.")]
        [Min(.01f)] public float apertureHalfWidth=1.04f, apertureHalfHeight=.49f;
        [Min(.16f)] public float apertureDepth=.85f;

        void OnDrawGizmosSelected()
        {
            if(!chargeAperture)return;
            var saved=Gizmos.matrix;
            Gizmos.matrix=Matrix4x4.TRS(chargeAperture.position,chargeAperture.rotation,Vector3.one);
            Gizmos.color=new Color(1,.51f,.23f,.9f);
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/12, b=(i+1)*Mathf.PI/12;
                var p=new Vector3(Mathf.Cos(a)*apertureHalfWidth,Mathf.Sin(a)*apertureHalfHeight,-.16f);
                var q=new Vector3(Mathf.Cos(b)*apertureHalfWidth,Mathf.Sin(b)*apertureHalfHeight,-.16f);
                Gizmos.DrawLine(p,q);
                Vector3 depth=Vector3.forward*apertureDepth;
                Gizmos.DrawLine(p+depth,q+depth);
                if(i%6==0)Gizmos.DrawLine(p,p+depth);
            }
            Gizmos.matrix=saved;
        }
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
