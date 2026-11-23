using System;
using UnityEngine;

namespace Raid
{
    [Serializable]
    public struct FloatRange
    {
        public float min, max;
        public FloatRange(float min, float max) { this.min = min; this.max = max; }
        public float Sample(System.Random rng) => min + (float)rng.NextDouble() * Mathf.Max(0, max - min);
    }

    // One asset per species. Every number the fight brain uses lives here.
    [CreateAssetMenu(menuName = "RAID/Fish fight profile")]
    public sealed class FishFightProfile : ScriptableObject
    {
        public string speciesId = "Lanternfish";

        [Header("Phase durations (s)")]
        public FloatRange hookShock = new(.4f, .7f);
        public FloatRange lateral = new(.8f, 1.6f);
        public FloatRange windup = new(.5f, .7f);
        public FloatRange surge = new(.6f, .9f);
        public FloatRange weakened = new(.9f, 1.6f);
        public FloatRange lastEscape = new(.35f, .55f);

        [Header("Surge timing")]
        [Tooltip("Planned gap between surge starts; the brain picks one value per surge.")]
        public FloatRange surgeInterval = new(2.2f, 3.8f);
        [Tooltip("Hard floor between two surge starts, whatever the weights say.")]
        [Min(.1f)] public float surgeCooldown = 1.8f;

        [Header("Decision weights")]
        [Min(0)] public float lateralWeight = 1.4f;
        [Min(0)] public float surgeWeight = .8f;
        [Min(0)] public float weakenedWeight = 1.6f;
        [Tooltip("Surge weight is scaled by 0.5 + stamina * this.")]
        [Min(0)] public float staminaSurgeBias = 1f;
        [Tooltip("Weakened is only considered below this stamina fraction.")]
        [Range(0, 1)] public float weakenedThreshold = .35f;
        [Tooltip("Surge weight multiplier with two or more hooks.")]
        [Range(0, 2)] public float twoHookSurgeFactor = .7f;
        [Tooltip("Surge weight multiplier while partners pull together.")]
        [Range(0, 2)] public float coordinatedSurgeFactor = 1.3f;
        [Tooltip("Surge weight multiplier while a line is obstructed by rock.")]
        [Range(0, 1)] public float obstructedSurgeFactor = .25f;
        [Tooltip("Extra surge weight as the fish nears the hand: weight *= 1 + bias * (1 - distance01).")]
        [Min(0)] public float nearSurgeBias = .8f;

        [Header("Motion")]
        [Tooltip("Sideways speed (m/s) at the peak of a lateral arc.")]
        [Min(0)] public float lateralStrength = 1.1f;
        [Tooltip("Turn rate (rad/s) while struggling: lower = more inertia in the arcs.")]
        [Min(.1f)] public float turnRate = 2.8f;
        [Min(1)] public float surgeSpeedMultiplier = 2f;
        [Range(0, 1)] public float weakenedSpeedMultiplier = .25f;
        [Tooltip("Stamina recovery multiplier while Weakened and not being reeled.")]
        [Min(0)] public float weakenedRecovery = 2.5f;
        [Tooltip("Tension rise multiplier while Weakened.")]
        [Range(0, 1)] public float weakenedTension = .55f;

        [Header("Run cost")]
        [Tooltip("Stamina drain multiplier while the fish runs (a surge); the fish tires itself out.")]
        [Min(0)] public float surgeFatigue = 1.8f;

        [Header("Tug")]
        [Tooltip("Seconds for TugStrength to ramp 0 -> 1 after a surge begins.")]
        [Min(.02f)] public float tugAttackSeconds = .18f;
        [Tooltip("Tug strength with two or more hooks: weaker, never zero.")]
        [Range(.05f, 1)] public float twoHookTugFactor = .55f;

        [Header("Last escape near the hand")]
        [Range(0, 1)] public float lastEscapeChance = .45f;
        [Tooltip("Hand range for a last escape, in multiples of captureRange.")]
        [Min(1)] public float lastEscapeRange = 2.4f;
        [Range(0, 1)] public float lastEscapeTug = .8f;
    }
}
