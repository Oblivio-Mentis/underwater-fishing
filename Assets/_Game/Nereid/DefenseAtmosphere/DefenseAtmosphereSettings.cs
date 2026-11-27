using UnityEngine;
using UnityEngine.Audio;

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
        public Material silhouetteMaterial;
        [Tooltip("Rammer, Remora, Thief, Abductor, Stalker. Use vertex colours and two submeshes, pigment then eyes.")]
        public Mesh[] silhouetteMeshes = new Mesh[5];

        [Header("Sound")]
        [Range(0f,1f)] public float soundLevel = .8f;
        [Range(0f,1f)] public float scoreLevel = .22f;
        [Range(0f,1f)] public float swarmVolume = .42f;
        [Range(1.3f,18f)] public float swarmCallInterval = 4.7f;
        public AudioMixerGroup effectsOutput;
        public AudioMixerGroup scoreOutput;
        public AudioClip localRoar, distantRoar, score, fieldHum;
        public AudioClip shieldImpact, shieldCritical, shieldBreak, shieldRestore;
        public AudioClip[] darkCalls = new AudioClip[3];
        public AudioClip[] creatureStrikes = new AudioClip[5];
        public AudioClip[] creatureAbilities = new AudioClip[5];
    }
}
