using System;
using System.Collections.Generic;
using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnderwaterFishing.World;
using UnityEngine;

namespace UnderwaterFishing.Progression.Network
{
    public enum ProgressionTerminalKind : byte { Processing, Workshop, Offering }
    public sealed class ProgressionTerminal : MonoBehaviour
    {
        public static readonly List<ProgressionTerminal> All = new List<ProgressionTerminal>();
        public ProgressionTerminalKind Kind;
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool CanUse(NetworkDiver diver, float distance, bool localView)
        {
            if (diver == null || !diver.IsActive || !diver.State.Sheltered || !SubmarineInterior.Contains(diver.transform.position)) return false;
            Vector3 delta = transform.position - diver.AimOrigin;
            Vector3 look = localView ? diver.Eyes.transform.forward : diver.AimDirection;
            if (delta.magnitude > distance || Vector3.Dot(look, delta.normalized) < .25f) return false;
            foreach (var hit in Physics.RaycastAll(diver.AimOrigin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Ignore(diver, hit.collider)) continue;
                if (hit.collider.GetComponentInParent<ProgressionTerminal>() != this) return false;
            }
            return true;
        }
        public static ProgressionTerminal Focus(NetworkDiver diver, float distance)
        {
            if (diver == null || diver.Eyes == null) return null;
            var hits = Physics.RaycastAll(diver.AimOrigin, diver.Eyes.transform.forward, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (Ignore(diver, hit.collider)) continue;
                return hit.collider.GetComponentInParent<ProgressionTerminal>();
            }
            return null;
        }
        private static bool Ignore(NetworkDiver diver, Collider collider)
        {
            if (collider.GetComponentInParent<NetworkDiver>() == diver) return true;
            var loot = collider.GetComponentInParent<NetworkLoot>();
            if (loot != null && loot.State.HolderId == diver.ObjectId) return true;
            var fish = collider.GetComponentInParent<NetworkFish>();
            return fish != null && fish.State.HolderId == diver.ObjectId;
        }
    }
}
