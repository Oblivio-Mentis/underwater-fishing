using System;
using UnityEngine;

namespace UnderwaterFishing.Diving
{
    public static class SwimMath
    {
        /// <summary>Clamp only player propulsion. Add fish pulls, currents, and impulses after this call.</summary>
        public static Vector3 ClampVoluntaryVelocity(Vector3 velocity, float maxHorizontal, float maxUp, float maxDown)
        {
            maxHorizontal = NonNegative(maxHorizontal);
            maxUp = NonNegative(maxUp);
            maxDown = NonNegative(maxDown);
            float x = FiniteOrZero(velocity.x);
            float z = FiniteOrZero(velocity.z);
            double horizontal = Math.Sqrt((double)x * x + (double)z * z);
            if (horizontal > maxHorizontal)
            {
                double scale = maxHorizontal / horizontal;
                x = (float)(x * scale);
                z = (float)(z * scale);
            }
            return new Vector3(x, Math.Min(maxUp, Math.Max(-maxDown, FiniteOrZero(velocity.y))), z);
        }

        private static float FiniteOrZero(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private static float NonNegative(float value) => Math.Max(0f, FiniteOrZero(value));
    }
}
