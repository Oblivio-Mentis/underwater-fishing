using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    // Only called by the authoritative fish/director. The selected world position travels in their snapshots.
    internal static class MarineSpawn
    {
        static NereidReef reef;
        internal static System.Random NewStream(int salt) => new(System.Guid.NewGuid().GetHashCode() ^ salt);

        internal static Vector3 Scatter(System.Random stream, float radius)
        {
            float angle = (float)stream.NextDouble() * Mathf.PI * 2;
            float distance = Mathf.Sqrt((float)stream.NextDouble()) * radius;
            return new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
        }

        internal static bool Floor(int depth, Vector3 point, out RaycastHit ground)
        {
            ground = default;
            if (!reef) reef = Object.FindAnyObjectByType<NereidReef>();
            var bed = reef ? reef.BedFor(depth) : null;
            if (!bed || !bed.enabled || !bed.gameObject.activeInHierarchy) return false;
            var bounds = bed.bounds;
            var top = new Vector3(point.x, bounds.max.y + 1, point.z);
            return bed.Raycast(new Ray(top, Vector3.down), out ground, bounds.size.y + 2);
        }

        internal static bool InOpenWater(int depth, Vector3 point, float radius)
        {
            return Floor(depth, point, out var ground) && point.y > ground.point.y + radius &&
                   !ReefHabitats.InShelter(point) && !FishActor.InCage(point) &&
                   !Physics.CheckSphere(point, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        internal static bool ClearLeg(Vector3 point, Vector3 toward, float radius, float maxDistance)
        {
            var leg = toward - point;
            var distance = Mathf.Min(leg.magnitude, maxDistance);
            return distance < .02f || !Physics.SphereCast(point, radius, leg.normalized, out _, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
