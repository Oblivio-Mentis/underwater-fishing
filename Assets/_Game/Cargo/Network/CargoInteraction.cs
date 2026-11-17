using System;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    public static class CargoInteraction
    {
        public static Component Focus(NetworkDiver diver, float range, bool localView = false)
        {
            if (diver == null || !diver.IsActive) return null;
            Vector3 direction = localView && diver.IsOwner ? diver.Eyes.transform.forward : diver.AimDirection;
            RaycastHit[] hits = Physics.RaycastAll(diver.AimOrigin, direction, range, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (IgnoreOwnCargo(diver, hit.collider)) continue;
                var station = hit.collider.GetComponentInParent<NetworkCargoStation>();
                if (station != null) return station;
                var loot = hit.collider.GetComponentInParent<NetworkLoot>();
                return loot;
            }
            return null;
        }
        public static bool CanReach(NetworkDiver diver, Vector3 center, float range, GameObject target)
        {
            Vector3 delta = center - diver.AimOrigin;
            float distance = delta.magnitude;
            if (distance > range || Vector3.Dot(diver.AimDirection, delta.normalized) < .25f) return false;
            RaycastHit[] hits = Physics.RaycastAll(diver.AimOrigin, delta.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (IgnoreOwnCargo(diver, hit.collider)) continue;
                if (hit.collider.gameObject != target && !hit.collider.transform.IsChildOf(target.transform)) return false;
            }
            return true;
        }
        private static bool IgnoreOwnCargo(NetworkDiver diver, Collider collider)
        {
            if (collider.GetComponentInParent<NetworkDiver>() == diver) return true;
            var loot = collider.GetComponentInParent<NetworkLoot>();
            if (loot != null && loot.State.HolderId == diver.ObjectId) return true;
            var fish = collider.GetComponentInParent<NetworkFish>();
            return fish != null && fish.State.HolderId == diver.ObjectId
                && diver.GetComponent<DiverFishing>()?.HasCatch == true;
        }
    }
}
