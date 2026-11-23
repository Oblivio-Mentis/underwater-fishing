using UnityEngine;

namespace Raid.Nereid
{
    public sealed class NereidDeckNavigation : MonoBehaviour
    {
        public Transform[] crewSpawns;
        public Transform[] companionWaypoints;
        public Transform[] raiderApproach;
        public Collider reservoirGlass;
        public NereidShipLayout rooms;
        public static NereidDeckNavigation Active { get; private set; }

        public static Vector3[] DefaultCompanionWaypoints => (Vector3[])GoshaDeck.DefaultMarks.Clone();

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        public static Vector3 CrewSpawn(int index,Vector3 fallback)
        {
            var spawns = Active ? Active.crewSpawns : null;
            if (spawns == null || spawns.Length == 0) return fallback;
            var socket = spawns[Mathf.Abs(index % spawns.Length)];
            return socket ? socket.position : fallback;
        }
    }
}
