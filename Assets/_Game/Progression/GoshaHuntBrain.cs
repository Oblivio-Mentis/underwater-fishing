using System;
using UnityEngine;

namespace Raid
{
    public enum GoshaHuntPhase : byte { None, Approach, Chase, Lunge, Caught, Failed }

    // The server-side chase: approach, chase, lunge, and the geometric grab test. Pure math, no
    // scene access. Obstacles are answered by the clear(from, to) callback the caller supplies.
    public sealed class GoshaHuntBrain
    {
        public static readonly Vector3 JawOffset = new(0, -.195f, 1.69f);

        public GoshaHuntStats Stats { get; private set; }
        public GoshaHuntPhase Phase { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Elapsed { get; private set; }
        public float LungeRemaining { get; private set; }
        public float LungeCooldown { get; private set; }
        public int Attempts { get; private set; }
        public uint LungeSequence { get; private set; }
        public string FailReason { get; private set; } = "";
        public bool Lunging => LungeRemaining > 0;

        public void Configure(GoshaHuntStats stats) => Stats = stats;

        public void Begin(Vector3 velocity)
        {
            Phase = GoshaHuntPhase.Approach;
            Velocity = velocity;
            Elapsed = 0; LungeRemaining = 0;
            Attempts++;
            FailReason = "";
        }

        public void Reset() { Phase = GoshaHuntPhase.None; Attempts = 0; Elapsed = 0; LungeRemaining = 0; FailReason = ""; }
        public void Fail(string reason) { Phase = GoshaHuntPhase.Failed; LungeRemaining = 0; FailReason = reason; }
        public void MarkCaught() { Phase = GoshaHuntPhase.Caught; LungeRemaining = 0; }
        public bool CanRetry => Attempts < Stats.Retries + 1;

        public static Vector3 Jaws(Vector3 hunter, Vector3 heading, float scale)
        {
            if (heading.sqrMagnitude < .0001f) heading = Vector3.forward;
            heading.Normalize();
            return hunter + heading * (JawOffset.z * scale) + Vector3.up * (JawOffset.y * scale);
        }

        /// <summary>
        /// One server tick. Returns the position the hunter wants to occupy after dt; the caller runs it
        /// through obstacle avoidance and then calls Confirm with what actually happened.
        /// attemptCapture is true only when the jaws are physically at the fish along a clear line.
        /// </summary>
        public Vector3 Step(float dt, Vector3 at, Vector3 heading, float scale, Vector3 fish, Vector3 fishVelocity,
            Func<Vector3, Vector3, bool> clear, out bool attemptCapture)
        {
            attemptCapture = false;
            if (Phase is GoshaHuntPhase.None or GoshaHuntPhase.Caught or GoshaHuntPhase.Failed) return at;
            Elapsed += dt;
            LungeCooldown = Mathf.Max(0, LungeCooldown - dt);
            var stats = Stats;
            Vector3 toFish = fish - at;
            float distance = toFish.magnitude;
            if (Elapsed > stats.ChaseTimeout) { Fail("timeout"); return at; }
            if (distance > stats.LoseDistance) { Fail("lost"); return at; }

            float speedNow = Velocity.magnitude;
            Vector3 currentHeading = speedNow > .05f ? Velocity / speedNow : heading.sqrMagnitude > .0001f ? heading.normalized : Vector3.forward;

            // Aim point: the fish itself, or where it will be when the hunter can get there.
            Vector3 aim = fish;
            if (stats.Intercept && fishVelocity.sqrMagnitude > .04f)
            {
                float closing = Mathf.Max(.8f, stats.MaxSpeed);
                float lead = Mathf.Min(stats.InterceptLead, distance / closing);
                var predicted = fish + fishVelocity * lead;
                if (clear == null || clear(fish, predicted)) aim = predicted;
            }
            // Keep the jaws, not the carapace centre, on the prey.
            Vector3 jawLead = currentHeading * (JawOffset.z * scale) + Vector3.up * (JawOffset.y * scale);
            Vector3 desired = aim - (at + jawLead);
            if (desired.sqrMagnitude < .0001f) desired = toFish.sqrMagnitude > .0001f ? toFish : currentHeading;
            desired.Normalize();

            bool fleeing = fishVelocity.magnitude > 1.9f;
            if (Phase == GoshaHuntPhase.Approach && (distance < 6f || fleeing)) Phase = GoshaHuntPhase.Chase;

            // Lunge: a short committed burst from close range when the path to the fish is open.
            if (LungeRemaining > 0)
            {
                LungeRemaining -= dt;
                if (LungeRemaining <= 0) { LungeRemaining = 0; Phase = GoshaHuntPhase.Chase; LungeCooldown = stats.LungeCooldown; }
            }
            else if (stats.HasLunge && Phase == GoshaHuntPhase.Chase && LungeCooldown <= 0 && distance <= stats.LungeTrigger &&
                     Vector3.Dot(currentHeading, desired) > .72f && (clear == null || clear(at, fish)))
            {
                Phase = GoshaHuntPhase.Lunge;
                LungeRemaining = stats.LungeSeconds;
                LungeSequence++;
            }

            float targetSpeed = Phase switch
            {
                GoshaHuntPhase.Approach => stats.MaxSpeed * .8f,
                GoshaHuntPhase.Lunge => stats.LungeSpeed,
                _ => stats.MaxSpeed
            };
            // A lunge commits: it turns at a third of the usual authority and does not brake.
            float turn = (Phase == GoshaHuntPhase.Lunge ? stats.TurnRate * .35f : stats.TurnRate) * Mathf.Deg2Rad * dt;
            Vector3 newHeading = Vector3.RotateTowards(currentHeading, desired, turn, 0);
            float accel = Phase == GoshaHuntPhase.Lunge ? stats.Acceleration * 3f : stats.Acceleration;
            float speed = Mathf.MoveTowards(speedNow, targetSpeed, accel * dt);
            Velocity = newHeading * speed;
            Vector3 next = at + Velocity * dt;

            Vector3 jaws = Jaws(next, newHeading, scale);
            float reach = stats.CaptureReach + (Phase == GoshaHuntPhase.Lunge ? stats.LungeReachBonus : 0);
            attemptCapture = (fish - jaws).sqrMagnitude <= reach * reach && (clear == null || clear(next, fish));
            return next;
        }

        /// <summary>Obstacle avoidance moved the hunter elsewhere: keep the velocity consistent with real motion.</summary>
        public void Confirm(Vector3 from, Vector3 to, float dt)
        {
            if (dt <= 0) return;
            Vector3 actual = (to - from) / dt;
            if ((actual - Velocity).sqrMagnitude > .01f) Velocity = Vector3.ClampMagnitude(actual, Mathf.Max(Velocity.magnitude, 1f));
        }

        /// <summary>Plain travel (escort, return) with the same inertia so swimming never snaps.</summary>
        public Vector3 Travel(float dt, Vector3 at, Vector3 goal, float speedLimit, float arrive)
        {
            Vector3 to = goal - at;
            float distance = to.magnitude;
            var stats = Stats;
            float speedNow = Velocity.magnitude;
            Vector3 currentHeading = speedNow > .05f ? Velocity / speedNow : to.sqrMagnitude > .0001f ? to.normalized : Vector3.forward;
            if (distance < .001f) { Velocity = Vector3.zero; return at; }
            float target = Mathf.Max(.6f, Mathf.Min(speedLimit, stats.MaxSpeed));
            // Brake to arrive instead of overshooting the goal.
            if (distance < arrive) target = Mathf.Min(target, Mathf.Max(.6f, distance * 2.2f));
            Vector3 newHeading = Vector3.RotateTowards(currentHeading, to.normalized, stats.TurnRate * Mathf.Deg2Rad * dt, 0);
            float speed = Mathf.MoveTowards(speedNow, target, stats.Acceleration * (target < speedNow ? 1.8f : 1f) * dt);
            Velocity = newHeading * speed;
            Vector3 step = Velocity * dt;
            if (step.magnitude >= distance) { Velocity = to / Mathf.Max(dt, .0001f); return goal; }
            return at + step;
        }

        public void Stop() { Velocity = Vector3.zero; LungeRemaining = 0; }
    }
}
