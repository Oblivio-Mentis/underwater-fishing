using UnityEngine;

namespace Raid
{
    [DisallowMultipleComponent]
    public sealed class HarpoonVisualRig : MonoBehaviour
    {
        public Transform housing, bolt, drum, slide, muzzle;
        public Renderer lineLamp;
        [Tooltip("Tip position in the detachable bolt's local frame.")] public Vector3 boltTipLocal;
        [Tooltip("Cable attachment in the detachable bolt's local frame.")] public Vector3 boltNockLocal;
        public bool Ready => housing && bolt && drum && slide && muzzle;
    }
}
