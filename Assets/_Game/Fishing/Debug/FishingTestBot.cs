using UnderwaterFishing.Fishing.Creatures;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Debugging
{
    /// <summary>Opt-in development driver. Every action still travels through the owning client's RPCs.</summary>
    public sealed class FishingTestBot : MonoBehaviour
    {
        [Min(1f)] public float DeadlineSeconds = 90f;
        public string Status { get; private set; } = "Disabled";
        private NetworkDiver diver;
        private DiverFishing fishing;
        private NetworkFish target;
        private DiverFishing hunter;
        private Vector3 firstWaypoint, previousFishPosition, estimatedFishVelocity;
        private float started = -1f, nextActions, nextLog, nextShot, targetAcquired, previousFishAt;
        private float aimYaw, aimPitch;
        private int waypoint;
        private bool helper, configured, finished, reel;

        private void Awake()
        {
            if (!Application.isEditor && !UnityEngine.Debug.isDebugBuild) { enabled = false; return; }
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != "--raid-fishing-bot") continue;
                helper = string.Equals(args[i + 1], "helper", System.StringComparison.OrdinalIgnoreCase);
                configured = helper || string.Equals(args[i + 1], "hunter", System.StringComparison.OrdinalIgnoreCase);
                break;
            }
            if (!configured) { enabled = false; return; }
            diver = GetComponent<NetworkDiver>();
            fishing = GetComponent<DiverFishing>();
            if (diver == null || fishing == null) { enabled = false; return; }
        }

        private void Update()
        {
            if (!configured || !diver.IsOwner || !diver.IsClientStarted || finished) return;
            float now = Time.unscaledTime;
            if (started < 0f)
            {
                started = now;
                firstWaypoint = new Vector3(diver.State.Position.x, -7f, 0f);
                aimYaw = diver.State.Yaw;
                UnityEngine.Debug.Log($"RAID_BOT_STARTED role={Role} diver={diver.ObjectId}");
            }
            if (!diver.IsActive) { Finish(false, "diver incapacitated or dead"); return; }
            if (now - started >= DeadlineSeconds) { Finish(false, "deadline"); return; }

            Vector3 worldMovement = Vector3.zero;
            Vector3 lookTarget = diver.State.Position + Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward * 5f;
            bool help = false, fire = false, pickup = false;
            if (waypoint < 2)
            {
                Vector3 point = waypoint == 0 ? firstWaypoint : new Vector3(11f, -7f, 0f);
                Status = waypoint == 0 ? "Navigate cabin" : "Navigate hatch";
                worldMovement = MoveToward(point);
                lookTarget = point;
                if (Vector3.Distance(diver.State.Position, point) <= 0.55f) waypoint++;
            }
            else if (helper)
                FollowHunter(ref worldMovement, ref lookTarget, ref help);
            else
                Hunt(now, ref worldMovement, ref lookTarget, ref fire, ref pickup);

            if (finished) return;
            AimAt(lookTarget);
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, aimYaw, 0f)) * Vector3.ClampMagnitude(worldMovement, 1f);
            diver.SetAutomationInput(local, aimYaw, aimPitch, false);
            if (now >= nextActions)
            {
                nextActions = now + 0.1f;
                fishing.Actions(reel, help);
            }
            if (fire) fishing.Fire();
            if (pickup) fishing.PickUp();
            if (now >= nextLog)
            {
                nextLog = now + 2f;
                LogDiagnostics();
            }
        }

        private string Role => helper ? "helper" : "hunter";
        private Vector3 EyePosition => diver.State.Position + Vector3.up * 0.6f;
        private Vector3 MoveToward(Vector3 point)
        {
            // Decelerate before narrow waypoints instead of switching directions at full swim speed.
            return Vector3.ClampMagnitude((point - diver.State.Position) * (1.3f / Mathf.Max(0.1f, diver.Balance.SwimSpeed)), 1f);
        }
        private void AimAt(Vector3 point)
        {
            Vector3 delta = point - EyePosition;
            if (delta.sqrMagnitude < 0.0001f) return;
            aimYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            aimPitch = Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -85f, 85f);
        }

        private void Hunt(float now, ref Vector3 movement, ref Vector3 lookTarget, ref bool fire, ref bool pickup)
        {
            NetworkFish attached = fishing.CurrentFish;
            if (fishing.State.Status == HarpoonStatus.Carried && attached != null && attached.State.State == FishState.Captured)
            {
                UnityEngine.Debug.Log($"RAID_BOT_CAUGHT fish={attached.ObjectId}");
                Finish(true, "carried");
                return;
            }
            if (attached != null && fishing.State.Status == HarpoonStatus.Hooked)
            {
                SetTarget(attached, now);
                EstimateFishVelocity(now);
                lookTarget = target.State.Position + estimatedFishVelocity * 0.07f;
                float distance = Vector3.Distance(EyePosition, target.State.Position);
                if (target.State.State == FishState.Exhausted)
                {
                    Status = "Approach exhausted fish";
                    reel = false;
                    movement = MoveToward(target.State.Position - Vector3.up * 0.6f);
                    pickup = distance <= diver.Balance.FishPickupDistance - 0.1f;
                    return;
                }
                Status = "Manage moving fish and line";
                float tension = fishing.State.NormalizedTension;
                if (tension >= 0.7f) reel = false;
                else if (tension <= 0.45f) reel = true;
                var rope = diver.Balance.Tether;
                float extension = 0.3f * rope.BreakTension / Mathf.Max(0.1f, rope.Stiffness);
                float standOff = Mathf.Clamp(fishing.State.Length + extension, 2f, Mathf.Max(2f, rope.MaxLength - 1f));
                Vector3 away = EyePosition - target.State.Position;
                if (away.sqrMagnitude < 0.01f) away = -Vector3.forward;
                Vector3 desiredPosition = target.State.Position + away.normalized * standOff - Vector3.up * 0.6f;
                movement = MoveToward(desiredPosition) + estimatedFishVelocity / Mathf.Max(0.1f, diver.Balance.SwimSpeed);
                return;
            }

            reel = false;
            if (target == null || target.State.State == FishState.Captured || target.Species.IsLarge || target.State.HolderId >= 0)
                SetTarget(FindSmallFish(), now);
            if (target == null) { Status = "Wait for available small fish"; return; }
            EstimateFishVelocity(now);
            Status = "Aim and approach small fish";
            lookTarget = target.State.Position + estimatedFishVelocity * 0.08f;
            Vector3 approach = target.State.Position - EyePosition;
            float range = approach.magnitude;
            Vector3 stand = range > 0.01f ? approach / range : Vector3.forward;
            movement = MoveToward(target.State.Position - stand * 3.5f - Vector3.up * 0.6f);
            if (range < diver.Balance.Tether.MaxLength - 1f && now - targetAcquired >= 0.25f && now >= nextShot)
            {
                nextShot = now + Mathf.Max(0.8f, diver.Balance.HarpoonCooldown + 0.15f);
                fire = true;
            }
        }

        private NetworkFish FindSmallFish()
        {
            NetworkFish closest = null;
            float range = float.MaxValue;
            foreach (var candidate in NetworkFish.All)
            {
                if (candidate == null || candidate.Species.IsLarge || candidate.State.State == FishState.Captured || candidate.State.HolderId >= 0) continue;
                float squared = (candidate.State.Position - diver.State.Position).sqrMagnitude;
                if (squared >= range) continue;
                range = squared; closest = candidate;
            }
            return closest;
        }
        private void SetTarget(NetworkFish next, float now)
        {
            if (target == next) return;
            target = next;
            targetAcquired = now;
            previousFishAt = now;
            previousFishPosition = next == null ? Vector3.zero : next.State.Position;
            estimatedFishVelocity = Vector3.zero;
        }
        private void EstimateFishVelocity(float now)
        {
            if (target == null) return;
            Vector3 position = target.State.Position;
            if ((position - previousFishPosition).sqrMagnitude > 0.000001f && now - previousFishAt >= 0.01f)
            {
                Vector3 measured = (position - previousFishPosition) / (now - previousFishAt);
                estimatedFishVelocity = Vector3.Lerp(estimatedFishVelocity, Vector3.ClampMagnitude(measured, 8f), 0.5f);
                previousFishPosition = position;
                previousFishAt = now;
            }
            else if (now - previousFishAt > 0.5f) estimatedFishVelocity = Vector3.zero;
        }

        private void FollowHunter(ref Vector3 movement, ref Vector3 lookTarget, ref bool help)
        {
            if (hunter == null || !hunter.Diver.IsActive)
            {
                hunter = null;
                foreach (var candidate in DiverFishing.All)
                    if (candidate != fishing && candidate.Diver.IsActive && !candidate.IsOwner) { hunter = candidate; break; }
            }
            if (hunter == null || hunter.Diver.State.Sheltered || hunter.Diver.State.Position.x < 9.5f)
            {
                Status = "Wait clear of hatch";
                movement = MoveToward(new Vector3(12f, -7f, 2.2f));
                lookTarget = new Vector3(8f, -6.4f, 0f);
                return;
            }
            if (hunter.State.Status == HarpoonStatus.Carried && hunter.CurrentFish != null)
            {
                UnityEngine.Debug.Log($"RAID_BOT_HELPER_DONE diver={hunter.ObjectId}");
                Finish(true, "hunter carried fish");
                return;
            }
            Status = "Follow hunter and hold help";
            Quaternion heading = Quaternion.Euler(0f, hunter.Diver.State.Yaw, 0f);
            Vector3 offset = heading * new Vector3(1.55f, 0f, -0.7f);
            movement = MoveToward(hunter.Diver.State.Position + offset);
            lookTarget = hunter.Diver.State.Position + Vector3.up * 0.6f;
            help = true;
        }

        private void Finish(bool success, string reason)
        {
            finished = true;
            diver.SetAutomationInput(Vector3.zero, aimYaw, aimPitch, false);
            fishing.Actions(false, false);
            if (!success)
                UnityEngine.Debug.Log($"RAID_BOT_TIMEOUT role={Role} phase={Status} reason={reason} seconds={Time.unscaledTime - started:0.0}");
            Status = success ? "Completed: " + reason : "Failed: " + reason;
        }
        private void LogDiagnostics()
        {
            NetworkFish fish = fishing.CurrentFish != null ? fishing.CurrentFish : target;
            string detail = fish == null ? "fish=none" : $"fish={fish.ObjectId} fishState={fish.State.State} stamina={fish.State.Stamina:0.0}";
            UnityEngine.Debug.Log($"RAID_BOT_DIAG role={Role} phase={Status} pos={diver.State.Position} {detail} line={fishing.State.Length:0.0} tension={fishing.State.NormalizedTension:0.00} helpers={fishing.State.Helpers}");
        }
    }
}
