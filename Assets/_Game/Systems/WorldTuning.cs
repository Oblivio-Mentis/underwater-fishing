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
    }
}
