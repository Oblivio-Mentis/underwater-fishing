using System;
using UnityEngine;

namespace Raid
{
    // Readable behaviour under the system mode: what the body is doing right now.
    // Free swimming: Cruise..Recover. Hooked fight sub-phases: HookShock..LastEscape.
    public enum FishBehaviour : byte
    {
        None = 0,
        Cruise, Hover, Forage, Alert, Flee, Recover,
        HookShock, Lateral, SurgeWindup, Surge, Weakened, LastEscape, Carried
    }

    public struct FishFightInput
    {
        public float Dt;
        /// <summary>0..1 share of initial stamina left.</summary>
        public float Stamina01;
        /// <summary>0..1 hand distance over max line range.</summary>
        public float Distance01;
        public int Hooks;
        public bool Coordinated, Obstructed, Reeling, NearHand, Exhausted;
    }

    // Pure fight logic: which sub-phase the hooked fish is in and how hard it pulls.
    // Deterministic for a given seed. No scene, no allocations after construction.
    public sealed class FishFightBrain
    {
        readonly FishFightProfile profile;
        System.Random rng;
        float clock, phaseStart, phaseLength, nextSurgeAt, lastSurgeAt, tug;
        bool lastEscapeSpent;
        int lateralSign = 1;

        public FishBehaviour CurrentPhase { get; private set; } = FishBehaviour.None;
        public bool SurgeWarning => CurrentPhase == FishBehaviour.SurgeWindup;
        public bool Surging => CurrentPhase == FishBehaviour.Surge || CurrentPhase == FishBehaviour.LastEscape;
        public bool Weakened => CurrentPhase == FishBehaviour.Weakened;
        /// <summary>0..1 pull on the lines, ramps up at a surge and decays after it.</summary>
        public float TugStrength => tug;
        /// <summary>-1..1 sideways drive: sign is the arc side, magnitude follows the phase envelope.</summary>
        public float Lateral { get; private set; }
        /// <summary>True only on the tick a surge or last escape began.</summary>
        public bool SurgeStarted { get; private set; }
        /// <summary>True only on the tick the phase changed.</summary>
        public bool PhaseChanged { get; private set; }
        public float LastSurgeAt => lastSurgeAt;
        public float Clock => clock;

        public FishFightBrain(FishFightProfile profile, int seed)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Reset(seed);
        }

        public void Reset(int seed)
        {
            rng = new System.Random(seed);
            clock = 0; tug = 0; Lateral = 0;
            lastSurgeAt = -profile.surgeCooldown;
            nextSurgeAt = profile.surgeInterval.Sample(rng);
            lastEscapeSpent = false;
            lateralSign = rng.NextDouble() < .5 ? -1 : 1;
            CurrentPhase = FishBehaviour.None;
            Enter(FishBehaviour.HookShock, profile.hookShock);
        }

        public FishFightProfile Profile => profile;

        /// <summary>Another harpoon landed: the body jolts before resuming the fight.</summary>
        public void Shock()
        {
            if (Surging) return;
            Enter(FishBehaviour.HookShock, profile.hookShock);
        }

        /// <summary>The arc ran into rock: swing to the other side instead of grinding on it.</summary>
        public void Deflect() => lateralSign = -lateralSign;

        public void Tick(in FishFightInput input)
        {
            SurgeStarted = false;
            PhaseChanged = false;
            clock += input.Dt;
            float progress = phaseLength > 0 ? Mathf.Clamp01((clock - phaseStart) / phaseLength) : 1;

            bool surgeAllowed = !input.Exhausted && !input.Obstructed && clock - lastSurgeAt >= profile.surgeCooldown;
            if (input.Exhausted && !Weakened && !Surging)
                Enter(FishBehaviour.Weakened, profile.weakened);
            else if (CurrentPhase == FishBehaviour.Lateral && surgeAllowed && clock >= nextSurgeAt)
                Enter(FishBehaviour.SurgeWindup, profile.windup);
            else if (progress >= 1) Advance(input, surgeAllowed);

            progress = phaseLength > 0 ? Mathf.Clamp01((clock - phaseStart) / phaseLength) : 1;
            float target = CurrentPhase == FishBehaviour.Surge ? (input.Hooks >= 2 ? profile.twoHookTugFactor : 1)
                : CurrentPhase == FishBehaviour.LastEscape ? profile.lastEscapeTug : 0;
            float rate = (target > tug ? 1 : 2) / profile.tugAttackSeconds;
            tug = Mathf.MoveTowards(tug, target, rate * input.Dt);

            float envelope = CurrentPhase switch
            {
                FishBehaviour.Lateral => Mathf.Sin(progress * Mathf.PI),
                FishBehaviour.Surge or FishBehaviour.LastEscape => .25f,
                FishBehaviour.SurgeWindup => .35f * (1 - progress),
                FishBehaviour.Weakened => .15f * Mathf.Sin(progress * Mathf.PI),
                _ => 0
            };
            Lateral = lateralSign * envelope;
        }

        void Advance(in FishFightInput input, bool surgeAllowed)
        {
            switch (CurrentPhase)
            {
                case FishBehaviour.SurgeWindup:
                    // The warning stays on screen until the cooldown lets the body go.
                    if (clock - lastSurgeAt < profile.surgeCooldown || input.Exhausted) { phaseLength += input.Dt * 2; return; }
                    BeginSurge(FishBehaviour.Surge, profile.surge);
                    return;
                case FishBehaviour.Surge:
                case FishBehaviour.LastEscape:
                    bool spent = input.Exhausted || input.Stamina01 < profile.weakenedThreshold;
                    Enter(spent ? FishBehaviour.Weakened : FishBehaviour.Lateral, spent ? profile.weakened : profile.lateral);
                    return;
            }
            if (input.Exhausted) { Enter(FishBehaviour.Weakened, profile.weakened); return; }
            if (input.NearHand && !lastEscapeSpent && surgeAllowed && input.Stamina01 > .05f && rng.NextDouble() < profile.lastEscapeChance)
            {
                lastEscapeSpent = true;
                BeginSurge(FishBehaviour.LastEscape, profile.lastEscape);
                return;
            }
            if (surgeAllowed && clock >= nextSurgeAt) { Enter(FishBehaviour.SurgeWindup, profile.windup); return; }

            float lateralW = profile.lateralWeight;
            float weakenedW = input.Stamina01 < profile.weakenedThreshold
                ? profile.weakenedWeight * (1 - input.Stamina01 / Mathf.Max(.01f, profile.weakenedThreshold)) : 0;
            float surgeW = surgeAllowed
                ? profile.surgeWeight * (.5f + input.Stamina01 * profile.staminaSurgeBias)
                    * (input.Hooks >= 2 ? profile.twoHookSurgeFactor : 1)
                    * (input.Coordinated ? profile.coordinatedSurgeFactor : 1)
                    * (1 + profile.nearSurgeBias * (1 - Mathf.Clamp01(input.Distance01)))
                : 0;
            if (input.Obstructed) surgeW *= profile.obstructedSurgeFactor;
            float roll = (float)rng.NextDouble() * (lateralW + weakenedW + surgeW);
            if (roll < lateralW) Enter(FishBehaviour.Lateral, profile.lateral);
            else if (roll < lateralW + weakenedW) Enter(FishBehaviour.Weakened, profile.weakened);
            else Enter(FishBehaviour.SurgeWindup, profile.windup);
        }

        void BeginSurge(FishBehaviour kind, FloatRange length)
        {
            lastSurgeAt = clock;
            nextSurgeAt = clock + Mathf.Max(profile.surgeCooldown, profile.surgeInterval.Sample(rng));
            SurgeStarted = true;
            Enter(kind, length);
        }

        void Enter(FishBehaviour phase, FloatRange length)
        {
            if (phase == FishBehaviour.Lateral && rng.NextDouble() < .65) lateralSign = -lateralSign;
            PhaseChanged |= phase != CurrentPhase;
            CurrentPhase = phase;
            phaseStart = clock;
            phaseLength = Mathf.Max(.02f, length.Sample(rng));
        }
    }
}
