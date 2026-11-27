using UnityEngine;

namespace Raid.Nereid
{
    [CreateAssetMenu(menuName = "RAID/Defense/Atmosphere")]
    public sealed class DefenseAtmosphereSettings : ScriptableObject
    {
        [Header("Swarm")]
        [Range(35,60)] public int silhouetteCount = 53;
        [Min(6f)] public float orbitRadius = 17f;
        [Min(0f)] public float radiusVariation = 8f;
        [Range(0f,9f)] public float heightVariation = 5.7f;
        [Range(.25f,3f)] public float swimSpeed = 1f;
        [Range(0f,.5f)] public float sizeVariation = .29f;
        [Min(10f)] public float clearDrawingDistance = 58f;
        [Min(10f)] public float darkDrawingDistance = 43f;
        [Min(0f)] public float giveWayDistance = 6f;
        [Range(0f,3f)] public float eyeGlow = 1.35f;
        [Range(100,550)] public int cloudCount=510;
        [Min(20)] public float cloudRadius=37;
        [Min(10)] public float cloudHeight=27;
        [Min(35)] public float cloudDrawingDistance=104;
        [Range(.1f,.48f)] public float cloudOrbitSpeed=.23f;
        public Material silhouetteMaterial;
        [Tooltip("Rammer, Remora, Thief, Abductor, Stalker. Use vertex colours and two submeshes, pigment then eyes.")]
        public Mesh[] silhouetteMeshes = new Mesh[5];

    }
}
