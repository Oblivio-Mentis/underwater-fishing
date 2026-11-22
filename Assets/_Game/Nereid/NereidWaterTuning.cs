using UnityEngine;

namespace Raid.Nereid
{
    [CreateAssetMenu(menuName = "RAID/NEREID water and kelp")]
    public sealed class NereidWaterTuning : ScriptableObject
    {
        [Tooltip("World X, Z, half length and half width in metres. Each grove has an open central channel.")]
        public Vector4[] groves;
        [Range(3,24)] public int holdfastsPerBank = 10;
        public Vector2 bladeHeight = new(3.2f,6.8f);
        [Min(.5f)] public float passageHalfWidth = 1.3f;
        [Min(.2f)] public float bendRadius = 2f;
        [Min(0)] public float bendMetres = 1.05f;
        [Min(10)] public float kelpVisibility = 42f;
        [Range(64,1024)] public int localFlakes = 480;
        [Range(16,160)] public int lampFlakes = 100;
        [Min(2)] public float localExtent = 9f;
        [Min(2)] public float lampExtent = 7f;
        [Range(0,1)] public float shallowDensity = .76f;
        [Range(0,1)] public float deepDensity = 1f;
    }
}
