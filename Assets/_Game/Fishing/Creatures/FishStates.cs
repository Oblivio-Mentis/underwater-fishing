using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Creatures
{
    internal abstract class FishBehaviour : BaseUniversalFsmState<FishContext>
    {
        public override void Enter(FishContext context)
        {
            context.State = (FishState)StateId;
            context.StateSeconds = 0f;
            context.Velocity = Vector3.zero;
            OnEnter(context);
        }
        protected virtual void OnEnter(FishContext context) { }
        public sealed override void Tick(FishContext context, float deltaTime)
        {
            context.StateSeconds += deltaTime;
            Simulate(context, deltaTime);
        }
        protected abstract void Simulate(FishContext context, float dt);
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishIdle : FishBehaviour
    {
        public override int StateId => (int)FishState.Idle;
        protected override void Simulate(FishContext context, float dt)
        {
            context.Recover(dt);
            if (context.ThreatWithin(context.Species.DetectionRadius)) context.Transition(FishState.Alerted);
            else if (context.StateSeconds >= context.Species.IdleSeconds) context.Transition(FishState.Roam);
        }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishRoam : FishBehaviour
    {
        public override int StateId => (int)FishState.Roam;
        protected override void OnEnter(FishContext context) => context.ChooseRoamTarget();
        protected override void Simulate(FishContext context, float dt)
        {
            context.Recover(dt);
            if (context.ThreatWithin(context.Species.DetectionRadius)) { context.Transition(FishState.Alerted); return; }
            context.RetargetSeconds -= dt;
            if (context.RetargetSeconds <= 0f || (context.Position - context.Target).sqrMagnitude < 0.5f)
                context.ChooseRoamTarget();
            context.Velocity = (context.Target - context.Position).normalized * Mathf.Max(0f, context.Species.SwimSpeed);
        }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishAlerted : FishBehaviour
    {
        public override int StateId => (int)FishState.Alerted;
        protected override void Simulate(FishContext context, float dt)
        {
            if (!context.ThreatWithin(context.Species.EscapeRadius)) context.Transition(FishState.Roam);
            else if (context.StateSeconds >= context.Species.AlertSeconds) context.Transition(FishState.Flee);
        }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishFlee : FishBehaviour
    {
        public override int StateId => (int)FishState.Flee;
        protected override void Simulate(FishContext context, float dt)
        {
            context.Recover(dt);
            if (!context.ThreatWithin(context.Species.EscapeRadius)) { context.Transition(FishState.Roam); return; }
            Vector3 direction = context.AwayFromDiver();
            direction.y *= 0.3f;
            context.Velocity = direction.normalized * Mathf.Max(0f, context.Species.FleeSpeed);
        }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishHooked : FishBehaviour
    {
        public override int StateId => (int)FishState.Hooked;
        protected override void Simulate(FishContext context, float dt)
        {
            if (context.StateSeconds >= context.Species.HookReactionSeconds) context.Transition(FishState.Struggling);
        }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishStruggling : FishBehaviour
    {
        public override int StateId => (int)FishState.Struggling;
        protected override void OnEnter(FishContext context) => context.RetargetSeconds = 0f;
        protected override void Simulate(FishContext context, float dt)
        {
            if (context.Reeling && context.Tension >= context.Species.MinimumWorkTension)
                context.Stamina = Mathf.Max(0f, context.Stamina - Mathf.Max(0f, context.Species.ReelExhaustionPerSecond) *
                    Mathf.Pow(context.Tension, Mathf.Clamp(context.Species.ReelWorkExponent, .1f, 1f)) * dt);
            else context.Recover(dt);
            if (context.Stamina <= 0f) { context.Transition(FishState.Exhausted); return; }

            context.RetargetSeconds -= dt;
            if (context.RetargetSeconds <= 0f)
            {
                context.StruggleDirection = (context.AwayFromDiver() + context.RandomDirection() * Mathf.Max(0f, context.Species.StruggleTurnJitter)).normalized;
                context.RetargetSeconds = Mathf.Max(0.01f, context.Species.StruggleRetargetSeconds);
            }
            float phase = Mathf.Repeat(context.StateSeconds, Mathf.Max(0.01f, context.Species.StrugglePulseSeconds)) / Mathf.Max(0.01f, context.Species.StrugglePulseSeconds);
            bool burst = phase < Mathf.Clamp01(context.Species.StrugglePulseDuty);
            Vector3 direction = context.StruggleDirection;
            if (context.Species.IsLarge && burst)
            {
                direction.y = -Mathf.Max(context.Species.LargeDiveStrength, Mathf.Abs(direction.y));
                direction.Normalize();
            }
            float energy = Mathf.Lerp(Mathf.Clamp01(context.Species.TiredSpeedFraction), 1f, context.Stamina / context.MaximumStamina);
            context.Velocity = direction * (Mathf.Max(0f, context.Species.StruggleSpeed) * energy * Mathf.Max(0f, burst ? context.Species.BurstSpeedMultiplier : context.Species.RestSpeedMultiplier));
        }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishExhausted : FishBehaviour
    {
        public override int StateId => (int)FishState.Exhausted;
        protected override void Simulate(FishContext context, float dt) { }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishCaptured : FishBehaviour
    {
        public override int StateId => (int)FishState.Captured;
        protected override void Simulate(FishContext context, float dt) { }
    }

    [UniversalFsmStateMetadata("Fishing")]
    internal sealed class FishEscaped : FishBehaviour
    {
        public override int StateId => (int)FishState.Escaped;
        protected override void OnEnter(FishContext context)
        {
            context.Stamina = Mathf.Max(context.Stamina, context.MaximumStamina * 0.35f);
            context.StruggleDirection = context.AwayFromDiver();
        }
        protected override void Simulate(FishContext context, float dt)
        {
            context.Recover(dt, 2f);
            context.Velocity = context.StruggleDirection * Mathf.Max(0f, context.Species.FleeSpeed);
            if (context.StateSeconds >= context.Species.EscapeCooldownSeconds)
                context.Transition(context.ThreatWithin(context.Species.EscapeRadius) ? FishState.Flee : FishState.Roam);
        }
    }
}
