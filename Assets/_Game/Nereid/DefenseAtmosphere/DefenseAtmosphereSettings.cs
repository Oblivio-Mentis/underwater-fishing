using UnityEngine;

namespace Raid.Nereid
{
    [CreateAssetMenu(menuName = "RAID/Defense/Atmosphere")]
    public sealed class DefenseAtmosphereSettings : ScriptableObject
    {
        [Header("Swarm")]
        [Range(35,60)] public int silhouetteCount = 53;
        [Min(6f)] public float orbitRadius = 56f;
        [Min(0f)] public float radiusVariation = 24f;
        [Tooltip("Radius where the lead pod can split into the first attack.")]
        [Min(18f)] public float handoffRadius = 32f;
        [Range(0f,9f)] public float heightVariation = 7.4f;
        [Range(.25f,3f)] public float swimSpeed = 1f;
        [Range(0f,.5f)] public float sizeVariation = .29f;
        [Min(10f)] public float clearDrawingDistance = 160f;
        [Min(10f)] public float darkDrawingDistance = 145f;
        [Range(0f,3f)] public float eyeGlow = 1.52f;
        [Range(100,550)] public int cloudCount=510;
        [Min(20)] public float cloudRadius=34;
        [Range(1,4)] public float cloudDistanceScale=3;
        [Min(10)] public float cloudHeight=23;
        [Min(35)] public float cloudDrawingDistance=64;
        [Range(.1f,.48f)] public float cloudOrbitSpeed=.12f;
        [Header("Suspended sediment")]
        [Range(0f,.32f)] public float currentOpacity=.20f;
        [Min(4f)] public float currentLength=23f;
        [Min(1f)] public float currentHeight=5.8f;
        public Material silhouetteMaterial;
        [Tooltip("Rammer, Remora, Thief, Abductor, Stalker. Use vertex colours and two submeshes, pigment then eyes.")]
        public Mesh[] silhouetteMeshes = new Mesh[5];

    }
}
