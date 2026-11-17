using UnityEngine;

namespace UnderwaterFishing.World
{
    public sealed class SubmarineInterior : MonoBehaviour
    {
        public static SubmarineInterior Instance { get; private set; }
        [SerializeField] private Vector3 size = new Vector3(15.6f, 5f, 25.6f);
        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static bool Contains(Vector3 point)
        {
            return Instance != null && new Bounds(Instance.transform.position, Instance.size).Contains(point);
        }
    }
}
