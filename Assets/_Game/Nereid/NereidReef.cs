using UnityEngine;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed partial class NereidReef : MonoBehaviour
    {
        [SerializeField] GameObject shoal, abyss;
        [SerializeField] MeshRenderer horizon;
        [SerializeField] MeshCollider shallowBed, deepBed;
        internal MeshCollider BedFor(int depth) => depth >= 2 ? deepBed : shallowBed;
        internal MeshCollider Seabed => BedFor(shownDepth);
        int shownDepth;

        public GameObject Shoal => shoal;
        public GameObject Rift => abyss;

        void Awake()
        {
            // The horizon shader follows the camera at far depth. Its small source sphere stays at the origin.
            if (horizon) horizon.localBounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
            SelectDepth();
        }

        void Update() => SelectDepth();

        void SelectDepth()
        {
            int depth = RaidWorld.Instance && RaidWorld.Instance.CurrentDepth >= 2 ? 2 : 1;
            if (depth == shownDepth || !shoal || !abyss) return;
            shownDepth = depth;
            shoal.SetActive(depth == 1);
            abyss.SetActive(depth == 2);
        }
    }
}
