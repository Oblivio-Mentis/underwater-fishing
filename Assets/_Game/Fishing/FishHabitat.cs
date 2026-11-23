using System;
using UnityEngine;

namespace Raid
{
    public enum FishHabitatRole : byte { Reef, Open, Shelter }

    // Where fish live: zones per depth. A zone is a loose polyline of anchors with a
    // scatter radius; height is resolved against the sea floor at runtime (FishActor.Reef).
    [CreateAssetMenu(menuName = "RAID/Fish habitat")]
    public sealed class FishHabitat : ScriptableObject
    {
        [Serializable]
        public sealed class Zone
        {
            public string name = "Zone";
            [Min(1)] public int depthTier = 1;
            public FishHabitatRole role = FishHabitatRole.Reef;
            [Tooltip("Keep a small school within swimming distance of the airlock.")]
            public bool entrySchool;
            public Vector3[] anchors = Array.Empty<Vector3>();
            [Tooltip("Horizontal scatter around each anchor when picking a target.")]
            [Min(0)] public float radius = 2.4f;
            [Tooltip("World Y the fish never rises above here (the shelving water above the reef).")]
            public float ceiling = -4f;
            [Tooltip("Height above the anchor used when no sea floor is found under it.")]
            public float fallbackLane = .3f;
            public Vector3 Anchor => anchors.Length > 0 ? anchors[0] : Vector3.zero;
        }

        [Min(1)] public int populationPerDepth = 20;
        [Min(1)] public int fishPerZone = 4;
        public Zone[] zones = Array.Empty<Zone>();

        static Zone[] legacyShelf, legacyRift;

        // Zones for one depth, in asset order. Returns the count written into `into`.
        public int Collect(int depthTier, Zone[] into)
        {
            int n = 0;
            for (int i = 0; i < zones.Length && n < into.Length; i++)
                if (zones[i].depthTier == depthTier) into[n++] = zones[i];
            return n;
        }

        // The pre-asset routes from ReefHabitats, labelled so the role preference still works.
        public static Zone[] Legacy(int depthTier)
        {
            if (depthTier > 1) return legacyRift ??= Convert(ReefHabitats.Rift, 2, new[] {
                FishHabitatRole.Reef, FishHabitatRole.Open, FishHabitatRole.Shelter, FishHabitatRole.Shelter, FishHabitatRole.Open, FishHabitatRole.Reef });
            return legacyShelf ??= Convert(ReefHabitats.Shelf, 1, new[] {
                FishHabitatRole.Shelter, FishHabitatRole.Reef, FishHabitatRole.Shelter, FishHabitatRole.Open, FishHabitatRole.Open, FishHabitatRole.Reef, FishHabitatRole.Open });
        }

        static Zone[] Convert(ReefHabitats.Route[] routes, int depthTier, FishHabitatRole[] roles)
        {
            var result = new Zone[routes.Length];
            for (int i = 0; i < routes.Length; i++)
                result[i] = new Zone { name = routes[i].Name, depthTier = depthTier, role = roles[i], anchors = routes[i].Waypoints,
                    radius = 2.2f, ceiling = depthTier > 1 ? -6f : -4f };
            return result;
        }
    }
}
