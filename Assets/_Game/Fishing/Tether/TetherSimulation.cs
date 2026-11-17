using System;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Tether
{
    /// <summary>Spatial spring-and-damper tether. Integrate returned accelerations after voluntary swim limits.</summary>
    public static class TetherSimulation
    {
        public static TetherState Attach(float distance, TetherSettings settings)
        {
            RequireSettings(settings);
            float maximum = NonNegative(settings.MaxLength);
            if (!Finite(distance) || distance < 0f || distance > maximum || maximum <= 0f)
                return default;
            float minimum = Mathf.Min(maximum, NonNegative(settings.MinLength));
            double length = (double)distance + NonNegative(settings.InitialSlack);
            return new TetherState
            {
                Length = (float)Math.Min(maximum, Math.Max(minimum, length)),
                Connected = true
            };
        }

        public static TetherForces Step(ref TetherState state, TetherSettings settings,
            Vector3 playerPosition, Vector3 fishPosition, Vector3 playerVelocity, Vector3 fishVelocity,
            bool reel, int helpers, float reelMultiplier, float dt)
        {
            RequireSettings(settings);
            if (!state.Connected || !Finite(dt) || dt <= 0f)
                return default;
            if (!Finite(playerPosition) || !Finite(fishPosition) || !Finite(playerVelocity)
                || !Finite(fishVelocity) || !Finite(state.Length) || state.Length < 0f)
                return Break(ref state, 0f);

            float maximum = NonNegative(settings.MaxLength);
            if (maximum <= 0f)
                return Break(ref state, 0f);
            float minimum = Mathf.Min(maximum, NonNegative(settings.MinLength));
            int assistants = Math.Min(Math.Max(0, helpers), Math.Max(0, settings.MaxAssistants));
            double length = Math.Min(maximum, Math.Max(minimum, state.Length));
            if (reel)
            {
                double speed = (double)NonNegative(settings.ReelSpeed) * NonNegative(reelMultiplier);
                speed *= 1d + (double)assistants * NonNegative(settings.HelperReelBonus);
                length = Math.Max(minimum, length - speed * dt);
            }
            state.Length = (float)length;

            // Double arithmetic keeps valid-but-extreme float coordinates from producing NaN directions.
            double x = (double)fishPosition.x - playerPosition.x;
            double y = (double)fishPosition.y - playerPosition.y;
            double z = (double)fishPosition.z - playerPosition.z;
            double distance = Math.Sqrt(x * x + y * y + z * z);
            Vector3 direction = distance > 0d
                ? new Vector3((float)(x / distance), (float)(y / distance), (float)(z / distance))
                : Vector3.zero;
            double relativeSpeed = ((double)fishVelocity.x - playerVelocity.x) * direction.x
                + ((double)fishVelocity.y - playerVelocity.y) * direction.y
                + ((double)fishVelocity.z - playerVelocity.z) * direction.z;
            double extension = Math.Max(0d, distance - length);
            // Slack cannot transmit damping force. Only separating motion adds damping tension.
            double tension = distance >= length
                ? extension * NonNegative(settings.Stiffness) + Math.Max(0d, relativeSpeed) * NonNegative(settings.Damping)
                : 0d;
            state.Tension = (float)Math.Min(float.MaxValue, tension);
            double threshold = Math.Max(0.01d, NonNegative(settings.BreakTension));
            float normalized = (float)Math.Min(float.MaxValue, tension / threshold);
            bool overload = tension >= threshold || distance > maximum + (double)NonNegative(settings.MaxRangeStretch);
            state.OverloadSeconds = overload ? NonNegative(state.OverloadSeconds) + dt : 0f;
            if (overload && state.OverloadSeconds >= NonNegative(settings.BreakGrace))
                return Break(ref state, normalized);

            double minimumPull = Math.Min(1d, Math.Max(0.05d, NonNegative(settings.MinimumAssistedPull)));
            double playerFraction = Math.Max(minimumPull, 1d - (double)assistants * NonNegative(settings.AssistanceReduction));
            double cap = NonNegative(settings.MaxAcceleration);
            float playerForce = (float)(Math.Min(cap, tension * NonNegative(settings.PlayerPullForce)) * playerFraction);
            float fishForce = (float)Math.Min(cap, tension * NonNegative(settings.FishPullForce));
            return new TetherForces
            {
                PlayerAcceleration = direction * playerForce,
                FishAcceleration = -direction * fishForce,
                NormalizedTension = normalized
            };
        }

        public static void Release(ref TetherState state) => state = default;

        private static TetherForces Break(ref TetherState state, float normalized)
        {
            Release(ref state);
            return new TetherForces { Broke = true, NormalizedTension = normalized };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static float NonNegative(float value) => Finite(value) ? Mathf.Max(0f, value) : 0f;
        private static void RequireSettings(TetherSettings settings)
        {
            if (ReferenceEquals(settings, null)) throw new ArgumentNullException(nameof(settings));
        }
    }
}
