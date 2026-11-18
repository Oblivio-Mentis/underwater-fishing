using System;

namespace UnderwaterFishing.Diving
{
    /// <summary>Deterministic gameplay rules. The caller owns time, shelter checks, and rescue eligibility.</summary>
    public static class DivingSimulation
    {
        public static PressureCondition GetPressureCondition(DivingState state, DivingSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (state.Life != DiverLife.Active) return PressureCondition.Incapacitated;
            float maximum = NonNegative(settings.CriticalStress);
            float warning = Math.Min(maximum, NonNegative(settings.WarningStress));
            float critical = Math.Max(warning, Math.Min(maximum, NonNegative(settings.CriticalWarningStress)));
            return state.Stress >= critical ? PressureCondition.Critical
                : state.Stress >= warning ? PressureCondition.Warning : PressureCondition.Normal;
        }
        public static DivingState Create(DivingSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new DivingState
            {
                Oxygen = NonNegative(settings.OxygenCapacity),
                AmbientPressure = 1f,
                Life = DiverLife.Active
            };
        }

        public static void Step(ref DivingState state, DivingSettings settings, float depth,
            float verticalVelocity, bool sheltered, bool sprinting, float dt)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!IsFinite(dt) || dt <= 0f) return;

            state.Depth = NonNegative(depth);
            state.AmbientPressure = 1f + state.Depth / 10f;
            state.VerticalVelocity = IsFinite(verticalVelocity) ? verticalVelocity : 0f;
            if (state.Life == DiverLife.Dead) return;

            float capacity = NonNegative(settings.OxygenCapacity);
            float criticalStress = NonNegative(settings.CriticalStress);
            state.Oxygen = Clamp(NonNegative(state.Oxygen), 0f, capacity);
            state.Stress = NonNegative(state.Stress);

            float oxygenRate = sheltered ? NonNegative(settings.RefillRate)
                : -NonNegative(settings.OxygenDrain) * (sprinting ? NonNegative(settings.SprintOxygenMultiplier) : 1f);
            float depthExcess = Math.Max(0f, state.Depth - NonNegative(settings.SafeDepth));
            float speedExcess = Math.Max(0f, state.VerticalVelocity - NonNegative(settings.SafeAscentSpeed))
                + Math.Max(0f, -state.VerticalVelocity - NonNegative(settings.SafeDescentSpeed));
            float stressRate = !sheltered && (depthExcess > 0f || speedExcess > 0f)
                ? depthExcess * NonNegative(settings.StressGainDepth) + speedExcess * NonNegative(settings.StressGainSpeed)
                : -NonNegative(settings.StressRecovery);

            float remaining = dt;
            if (state.Life == DiverLife.Active)
            {
                float timeToCritical = float.PositiveInfinity;
                if (state.Oxygen <= 0f || state.Stress >= criticalStress)
                    timeToCritical = 0f;
                else
                {
                    if (oxygenRate < 0f) timeToCritical = state.Oxygen / -oxygenRate;
                    if (stressRate > 0f)
                        timeToCritical = Math.Min(timeToCritical, (criticalStress - state.Stress) / stressRate);
                }

                float activeTime = Math.Min(remaining, timeToCritical);
                AdvanceVitals(ref state, capacity, oxygenRate, stressRate, activeTime);
                remaining -= activeTime;
                if (timeToCritical <= dt)
                {
                    state.Life = DiverLife.Incapacitated;
                    state.IncapacitatedRemaining = NonNegative(settings.IncapacitatedSeconds);
                }
            }

            if (state.Life == DiverLife.Incapacitated)
            {
                // Shelter can stabilize vitals, but a different diver must still perform rescue.
                float incapacitatedTime = Math.Min(remaining, NonNegative(state.IncapacitatedRemaining));
                AdvanceVitals(ref state, capacity, oxygenRate, stressRate, incapacitatedTime);
                state.IncapacitatedRemaining = Math.Max(0f, NonNegative(state.IncapacitatedRemaining) - remaining);
                if (state.IncapacitatedRemaining <= 0f) state.Life = DiverLife.Dead;
            }
        }

        /// <summary>Call only after another diver's range and eligibility have been verified.</summary>
        public static bool Rescue(ref DivingState state, DivingSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (state.Life != DiverLife.Incapacitated || state.IncapacitatedRemaining <= 0f) return false;
            float oxygen = Math.Min(NonNegative(settings.OxygenCapacity),
                Math.Max(NonNegative(state.Oxygen), NonNegative(settings.RescueOxygen)));
            if (oxygen <= 0f || NonNegative(settings.CriticalStress) <= 0f) return false;
            state.Oxygen = oxygen;
            state.Stress = Math.Min(NonNegative(state.Stress), NonNegative(settings.CriticalStress) * 0.5f);
            state.Life = DiverLife.Active;
            state.IncapacitatedRemaining = 0f;
            return true;
        }

        private static void AdvanceVitals(ref DivingState state, float capacity,
            float oxygenRate, float stressRate, float dt)
        {
            state.Oxygen = Clamp(state.Oxygen + oxygenRate * dt, 0f, capacity);
            state.Stress = Math.Max(0f, state.Stress + stressRate * dt);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float NonNegative(float value) => IsFinite(value) ? Math.Max(0f, value) : 0f;
        private static float Clamp(float value, float minimum, float maximum) => Math.Min(maximum, Math.Max(minimum, value));
    }
}
