using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        const float FastMovementScale = 4f;
        readonly Collider[] flightOverlaps = new Collider[32];
        Vector3? flightOrigin, lastClearFlightPosition;
        int flightCollisionMask;

        public bool CheatNoclip => _state.Value.Noclip;
        public bool CheatFast => _state.Value.FastMovement;

        public void RequestCheatMovement(bool noclip, bool fast)
        {
            if (NetworkObject != null && IsClientInitialized && IsOwner)
                SetCheatMovement(noclip, fast);
        }

        [ServerRpc(RequireOwnership = true)]
        void SetCheatMovement(bool noclip, bool fast)
        {
            var state = _state.Value;
            if (state.Dead) return;
            if (state.FastMovement && !fast) swimVelocity /= FastMovementScale;
            state.FastMovement = fast;
            if (state.Noclip != noclip)
            {
                if (noclip)
                {
                    flightCollisionMask = 0;
                    for (int layer = 0; layer < 32; layer++)
                        if (!Physics.GetIgnoreLayerCollision(gameObject.layer, layer))
                            flightCollisionMask |= 1 << layer;
                    _controller.enabled = false;
                    RememberFlightOrigin(transform.position);
                }
                else
                {
                    if (!FindFlightExit(out var exit))
                    {
                        // A moving obstacle can occupy an earlier safe point. Keep flying until a clear exit exists.
                        _state.Value = state;
                        return;
                    }
                    transform.position = exit;
                    _controller.enabled = true;
                    state.Position = exit;
                    state.Inside = IsSealedInterior(exit);
                }
                state.Noclip = noclip;
                _previousPosition = transform.position;
                swimVelocity = Vector3.zero;
                ResetIndoorMovement();
            }
            _state.Value = state;
        }

        CollisionFlags MoveSwimmer(Vector3 displacement)
        {
            if (!CheatNoclip) return _controller.Move(displacement);
            if (displacement.sqrMagnitude > 0f)
            {
                transform.position += displacement;
                if (FlightCapsuleClear(transform.position))
                    lastClearFlightPosition = transform.position;
            }
            return CollisionFlags.None;
        }

        void RememberFlightOrigin(Vector3 position)
        {
            flightOrigin = FlightCapsuleClear(position) ? position : (Vector3?)null;
            lastClearFlightPosition = flightOrigin;
        }

        bool FindFlightExit(out Vector3 position)
        {
            position = transform.position;
            if (FlightCapsuleClear(position)) return true;
            if (lastClearFlightPosition.HasValue && FlightCapsuleClear(lastClearFlightPosition.Value))
            {
                position = lastClearFlightPosition.Value;
                return true;
            }
            if (flightOrigin.HasValue && FlightCapsuleClear(flightOrigin.Value))
            {
                position = flightOrigin.Value;
                return true;
            }
            // Moving geometry or an expedition teleport may have invalidated both checkpoints.
            var spawn = Nereid.NereidDeckNavigation.CrewSpawn(OwnerId, new Vector3(0, 1.2f, 1));
            for (int ring = 0; ring <= 3; ring++)
            {
                int samples = ring == 0 ? 1 : 8;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * .25f;
                    position = spawn + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring;
                    if (FlightCapsuleClear(position)) return true;
                }
            }
            return false;
        }

        bool FlightCapsuleClear(Vector3 position)
        {
            var scale = transform.lossyScale;
            float radius = _controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, _controller.height * Mathf.Abs(scale.y) * .5f);
            var center = position + transform.TransformVector(_controller.center);
            var axis = transform.up * (halfHeight - radius);
            // A grounded CharacterController normally contacts the deck within its skin.
            float skin = Mathf.Min(radius * .25f, _controller.skinWidth * Mathf.Abs(scale.y));
            float queryRadius = Mathf.Max(.001f, radius - skin);
            var world = RaidWorld.Instance;
            if (world != null && Nereid.NereidVessel.Active != null &&
                MarineSpawn.Floor(world.CurrentDepth, center, out var ground) &&
                center.y - halfHeight + skin < ground.point.y)
                return false;
            int count = Physics.OverlapCapsuleNonAlloc(center - axis, center + axis, queryRadius,
                flightOverlaps, flightCollisionMask, QueryTriggerInteraction.Ignore);
            if (count == flightOverlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = flightOverlaps[i];
                if (hit != null && !hit.transform.IsChildOf(transform)) return false;
            }
            return true;
        }
    }
}
