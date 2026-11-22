using UnityEngine;

namespace Raid
{
    internal static class ReefHabitats
    {
        internal sealed class Route
        {
            public readonly string Name;
            public readonly Vector3[] Waypoints;

            public Route(string name, params Vector3[] waypoints)
            {
                Name = name;
                Waypoints = waypoints;
            }
        }

        public static readonly Route[] Shelf =
        {
            new("Survey Wreck", new(36,-6.5f,-12),new(31,-7,-19),new(25,-8,-19),new(27,-6,-14)),
            new("Kelp Arch", new(42,-5,-35),new(37,-5,-33),new(34,-6,-38),new(40,-5.5f,-41)),
            new("Outer Ridge", new(56,-7,-58),new(52,-6,-49),new(46,-6.8f,-46),new(43,-7,-50))
        };

        public static readonly Route[] Rift =
        {
            new("Rift Lip", new(49,-7,-30),new(44,-7,-36),new(40,-8,-38),new(37,-7,-35)),
            new("Upper Needles", new(50,-7.5f,-43),new(44,-7.9f,-44.7f),new(41,-8.5f,-48),new(43.5f,-7,-53)),
            new("Outer Seam", new(59,-8,-64),new(54,-9,-57),new(49,-9,-54),new(45,-10,-52))
        };

        public static bool InShelter(Vector3 point) =>
            Nereid.NereidAirlock.Active?.Contains(point) == true ||
            Nereid.NereidVessel.Active?.ContainsSealed(point) == true;

        public static bool CanArrive(Vector3 point, float minimumDistance, float clearViewDistance)
        {
            if (InShelter(point)) return false;
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (diver == null || diver.IsDead || diver.IsInside) continue;
                Vector3 eye = diver.transform.position + Vector3.up * .6f;
                Vector3 offset = point - eye;
                float distance = offset.magnitude;
                if (distance < minimumDistance) return false;
                if (distance < clearViewDistance && Vector3.Dot(offset / distance, diver.AimDirection) > .56f &&
                    !Physics.Linecast(eye, point, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
    }
}
