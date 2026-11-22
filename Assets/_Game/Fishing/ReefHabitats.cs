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
            new("Kelp Gate", new(42,-5,-35),new(37,-5,-33),new(36,-6,-38),new(40,-6.5f,-43),new(43,-9.2f,-46)),
            new("Wreck Basin", new(53,-9,-60),new(48,-12,-67),new(46,-12.5f,-72),new(43,-12,-65),new(45,-10.5f,-56)),
            new("Far Ridge", new(58,-10,-79),new(54,-13,-84),new(50,-14,-80),new(50,-10,-74),new(55,-9,-72))
        };

        public static readonly Route[] Rift =
        {
            new("Rift Lip", new(49,-7,-30),new(44,-7,-36),new(40,-8,-38),new(37,-7,-35)),
            new("Upper Needles", new(50,-7.5f,-43),new(44,-7.9f,-44.7f),new(41,-8.5f,-48),new(43.5f,-7,-53)),
            new("Outer Seam", new(57,-12,-64),new(55,-15,-67),new(51,-21,-75),new(48,-22,-79),new(47,-20,-70),new(50,-15,-62)),
            new("Lantern Fault", new(44,-24,-82),new(47,-29,-88),new(48,-31,-94),new(45,-29,-91),new(43,-26,-86))
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
