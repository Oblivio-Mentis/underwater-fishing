using UnityEngine;

namespace Raid
{
    // One asset per species: how it moves, what it fears, where it likes to live.
    // Null on FishActor means the legacy per-species switch values (see Legacy).
    [CreateAssetMenu(menuName = "RAID/Fish species profile")]
    public sealed class FishSpeciesProfile : ScriptableObject
    {
        [Header("Habitat")]
        public FishHabitat habitat;
        public FishHabitatRole preferredHabitat = FishHabitatRole.Reef;
        [Tooltip("Seconds after the vessel arrives before this species swims in.")]
        [Min(0)] public float arrivalDelay = 1.5f;
        [Tooltip("Seconds spent in one zone before migrating to the next.")]
        [Min(5)] public float dwellSeconds = 63f;
        [Tooltip("Scatter around each zone waypoint when choosing the next target.")]
        [Min(0)] public float patrolRadius = 2.2f;
        [Tooltip("Beyond this distance from the zone anchor a fleeing fish bends back home.")]
        [Min(1)] public float returnDistance = 12.4f;

        [Header("Depth band (metres above the sea floor, from a raycast)")]
        [Min(0)] public float floorClearanceMin = .8f;
        [Min(0)] public float floorClearanceMax = 3.5f;

        [Header("Motion")]
        [Min(.1f)] public float cruiseSpeed = 1.65f;
        [Min(0)] public float hoverSpeed = .35f;
        [Min(.1f)] public float forageSpeed = .8f;
        [Min(.5f)] public float fleeSpeed = 5.9f;
        [Tooltip("m/s^2 reached while speeding up; braking is 1.6x this, fleeing 4x.")]
        [Min(.1f)] public float acceleration = 2.6f;
        [Tooltip("Degrees per second at full turning authority.")]
        [Min(5)] public float turnRate = 160f;
        [Tooltip("Metres. At speed the turn is limited to speed / radius.")]
        [Min(.1f)] public float turnRadius = .9f;
        [Tooltip("Roll degrees per unit of lateral turn.")]
        [Min(0)] public float bankStrength = 22f;
        [Tooltip("Multiplies the base body sweep radius (.19 small, .36 two-diver fish).")]
        [Min(.2f)] public float bodyRadiusMultiplier = 1f;

        [Header("Senses")]
        [Tooltip("A swimmer inside this range puts the fish on alert.")]
        [Min(.5f)] public float senseDistance = 4.1f;
        [Tooltip("A swimmer inside this range makes it flee once perceived.")]
        [Min(.3f)] public float closeDistance = 2.5f;
        [Tooltip("Seconds of alert before the fish commits to fleeing.")]
        [Min(0)] public float perceptionDelay = .3f;
        [Min(.5f)] public float fleeDuration = 2.6f;
        [Tooltip("Flee length after a harpoon passes close by.")]
        [Min(.5f)] public float shotFleeDuration = 3.4f;
        [Tooltip("0 = runs into open water, 1 = hides low near the floor after a scare.")]
        [Range(0, 1)] public float hidingPreference = .5f;
        [Tooltip("Seconds of cautious swimming after a flee before cruising again.")]
        [Min(0)] public float recoverSeconds = 2.2f;

        [Header("Pauses at waypoints")]
        [Header("Darts while cruising")]
        [Tooltip("Chance per second of a short burst of speed while cruising.")]
        [Range(0, 1)] public float dartChance = .22f;
        [Min(.1f)] public float dartSeconds = .55f;
        [Min(1)] public float dartSpeedMultiplier = 2.1f;
        [Range(0, 1)] public float hoverProbability = .3f;
        [Min(0)] public float hoverDurationMin = 1.2f, hoverDurationMax = 3f;
        [Range(0, 1)] public float forageProbability = .25f;
        [Min(0)] public float forageDurationMin = 2f, forageDurationMax = 4.5f;

        [Header("Anti-stuck")]
        [Tooltip("No progress for this long while trying to move triggers recovery.")]
        [Min(.3f)] public float stuckSeconds = 1.4f;

        static readonly System.Collections.Generic.Dictionary<string, FishSpeciesProfile> legacy = new();

        // The pre-profile behaviour: the numbers that used to live in switch(SpeciesId).
        public static FishSpeciesProfile Legacy(string speciesId)
        {
            if (legacy.TryGetValue(speciesId, out var found) && found != null) return found;
            var p = CreateInstance<FishSpeciesProfile>();
            p.hideFlags = HideFlags.HideAndDontSave;
            p.name = "Legacy " + speciesId;
            switch (speciesId)
            {
                case "Needlefish":
                    p.preferredHabitat = FishHabitatRole.Reef; p.arrivalDelay = 1.5f; p.dwellSeconds = 63f;
                    p.floorClearanceMin = .9f; p.floorClearanceMax = 3.4f; break;
                case "Lanternfish":
                    p.preferredHabitat = FishHabitatRole.Open; p.arrivalDelay = 6.8f; p.dwellSeconds = 63f;
                    p.floorClearanceMin = .7f; p.floorClearanceMax = 3.2f; break;
                case "Bristlefish":
                    p.preferredHabitat = FishHabitatRole.Shelter; p.arrivalDelay = 13.5f; p.dwellSeconds = 44f;
                    p.floorClearanceMin = .35f; p.floorClearanceMax = 1.6f; p.cruiseSpeed = 1.3f; p.fleeSpeed = 5.35f;
                    p.turnRadius = 1.6f; p.bankStrength = 29f; p.senseDistance = 3f; p.closeDistance = 1.9f; p.turnRate = 110f; break;
                default:
                    p.preferredHabitat = FishHabitatRole.Shelter; p.arrivalDelay = 19f; p.dwellSeconds = 63f;
                    p.floorClearanceMin = 1.2f; p.floorClearanceMax = 4f; p.cruiseSpeed = 1.3f; p.fleeSpeed = 5.35f;
                    p.turnRadius = 2.2f; p.bankStrength = 29f; p.senseDistance = 3f; p.closeDistance = 1.9f; p.turnRate = 90f; break;
            }
            legacy[speciesId] = p;
            return p;
        }
    }
}
