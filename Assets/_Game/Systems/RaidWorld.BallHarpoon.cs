using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        HarpoonTool ballLine;
        float ballLineIdle;
        readonly RaycastHit[] ballPathHits = new RaycastHit[16];

        // The sphere has no independent NetworkObject. Its single holder and position travel
        // with the ship snapshot; observers anchor the bolt to the interpolated visible sphere.
        internal bool TryBallAnchor(out Transform surface, out Vector3 center)
        {
            surface = goshaView != null ? goshaView.BallAnchor : null;
            if (surface == null && ballBody != null) surface = ballBody.transform;
            center = surface != null ? surface.position : Gosha.BallPosition;
            return surface != null;
        }

        internal bool TryHookBall(HarpoonTool source, GoshaBallBody body, RaycastHit hit, out HarpoonContact contact)
        {
            contact = default;
            if (!IsServerInitialized || ballLine != null || !pet.Active || transitionRemaining > 0
                || body != ballBody || hit.collider == null || hit.collider.attachedRigidbody == null
                || hit.collider.attachedRigidbody.transform != body.transform || !CanReelBall(source)
                || (pet.Ball != GoshaBallMode.Deck && pet.Ball != GoshaBallMode.Flying)) return false;

            // FireServer supplies the FIRST hit from the authoritative eye and aim.
            BeginBallRecovery();
            Vector3 center = body.Position;
            contact = new HarpoonContact { target = NetworkObject, ball = true,
                localPoint = hit.point - center, localNormal = hit.normal.normalized,
                localDirection = source.Motor.AimDirection.normalized };
            body.Tether();
            ballLine = source;
            ballLineIdle = ballAge = 0;
            pet.Ball = GoshaBallMode.Tethered;
            pet.Holder = source.OwnerId;
            pet.Thrower = -1;
            pet.BallPosition = center;
            gosha.Value = pet;
            return true;
        }

        bool CanReelBall(HarpoonTool tool) =>
            tool != null && tool.IsServerInitialized && tool.Owner != null && tool.Owner.IsActive
            && tool.gameObject.scene == gameObject.scene && tool.HarpoonInHands && tool.HookedFish == null
            && !tool.TetherActive && Alive(tool.Motor) && !tool.Motor.IsTrapped
            && !IsHoldingGosha(tool.Motor) && pet.Knocked != tool.OwnerId;

        internal void ReleaseBallLine(HarpoonTool source)
        {
            if (!IsServerInitialized || source != ballLine || pet.Ball != GoshaBallMode.Tethered) return;
            DropLooseBallLine();
        }

        void ClearBallLine()
        {
            if (ballLine != null) ballLine.FinishFishingLine();
            ballLine = null;
            ballLineIdle = 0;
        }

        void DropLooseBallLine()
        {
            ClearBallLine();
            DropBall(ballBody.Position, Vector3.zero, false);
            gosha.Value = pet;
        }

        void TickBallLine(float dt)
        {
            if (!CanReelBall(ballLine) || transitionRemaining > 0)
            {
                DropLooseBallLine();
                return;
            }

            Vector3 center = ballBody.Position, hand = ballLine.HandAnchor;
            pet.BallPosition = center;
            Vector3 offset = hand - center;
            float distance = offset.magnitude;
            if (distance > ballLine.tuning.maxRange || !BallPathClear(center, hand, 0))
            {
                DropLooseBallLine();
                return;
            }
            if (!ballLine.Reeling)
            {
                ballLineIdle += dt;
                if (ballLineIdle > tuning.ballLineIdleSeconds) DropLooseBallLine();
                return;
            }
            ballLineIdle = 0;
            float step = Mathf.Min(distance, Mathf.Max(.1f, tuning.ballReelSpeed) * dt);
            Vector3 next = Vector3.MoveTowards(center, hand, step);
            if (!BallPathClear(center, next, ballBody.Radius * .96f))
            {
                DropLooseBallLine();
                return;
            }
            if (distance <= tuning.ballHandReach && ballAge >= .18f)
            {
                var recipient = ballLine.Motor;
                if (!BallPathClear(center, Hand(recipient), ballBody.Radius * .96f))
                {
                    DropLooseBallLine();
                    return;
                }
                ClearBallLine();
                GiveBall(recipient);
                recipient.ConfirmInteraction();
                return;
            }
            ballBody.ReelTo(next);
        }

        // Ignore only the ball and its owner. A shut door, another diver, or hull geometry
        // cuts the line; the sphere sweep also stops the ball fitting through a narrow slit.
        bool BallPathClear(Vector3 start, Vector3 end, float radius)
        {
            Vector3 offset = end - start;
            float distance = offset.magnitude;
            if (distance < .0001f) return true;
            Vector3 direction = offset / distance;
            int count = radius > 0
                ? Physics.SphereCastNonAlloc(start, radius, direction, ballPathHits, distance, ballLine.tuning.hitMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(start, direction, ballPathHits, distance, ballLine.tuning.hitMask, QueryTriggerInteraction.Ignore);
            if (count == ballPathHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = ballPathHits[i].collider;
                if (collider == null || collider.transform == ballBody.transform || collider.GetComponentInParent<DiverMotor>() == ballLine.Motor) continue;
                return false;
            }
            return true;
        }
    }
}
