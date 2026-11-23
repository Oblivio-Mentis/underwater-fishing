using System;
using UnityEngine;

namespace Raid.Nereid
{
    public enum NereidRoomRole { Command, Reservoir, Workshop, Cargo, Equipment, Feeding, Airlock, Companion }

    [DisallowMultipleComponent]
    public sealed class NereidShipLayout : MonoBehaviour
    {
        [Serializable] public struct StationSocket
        {
            public NereidRoomRole role;
            public Transform anchor;
        }

        [Serializable] public struct PressureVolume
        {
            public Transform room;
            public Bounds localBounds;
        }

        public StationSocket[] stations = Array.Empty<StationSocket>();
        public PressureVolume[] dryCompartments = Array.Empty<PressureVolume>();

        public Transform Socket(NereidRoomRole role)
        {
            foreach (var station in stations)
                if (station.role == role) return station.anchor;
            return null;
        }

        public bool ContainsSealed(Vector3 world)
        {
            foreach (var volume in dryCompartments)
                if (volume.room != null && volume.localBounds.Contains(volume.room.InverseTransformPoint(world))) return true;
            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var matrix = Gizmos.matrix;
            Gizmos.color = new Color(.2f,.95f,.83f,.75f);
            foreach (var volume in dryCompartments)
            {
                if (volume.room == null) continue;
                Gizmos.matrix = volume.room.localToWorldMatrix;
                Gizmos.DrawWireCube(volume.localBounds.center, volume.localBounds.size);
            }
            Gizmos.matrix = matrix;
        }
#endif
    }
}
