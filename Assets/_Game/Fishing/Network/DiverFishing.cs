using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Fishing.Creatures;
using UnderwaterFishing.Fishing.Tether;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Network
{
    public enum HarpoonStatus : byte { Ready, Miss, Hooked, Broken, Released, Carried }
    public struct FishingSnapshot
    {
        public int FishId, Helpers, HelpTargetId;
        public float Length, Tension, NormalizedTension;
        public bool Connected;
        public HarpoonStatus Status;
        public uint ShotSequence;
    }

    [RequireComponent(typeof(NetworkDiver))]
    public sealed class DiverFishing : NetworkBehaviour
    {
        public static readonly List<DiverFishing> All = new List<DiverFishing>();
        public static DiverFishing Local { get; private set; }
        private readonly SyncVar<FishingSnapshot> snapshot = new SyncVar<FishingSnapshot>();
        private NetworkDiver diver;
        private DiverInput input;
        private NetworkFish fish;
        private TetherState tether;
        private FishingSnapshot current;
        private bool reeling, helping;
        private float lastActions, nextActions, nextShot;
        private int previousHelpers;
        public NetworkDiver Diver => diver;
        public FishingSnapshot State => snapshot.Value;
        public NetworkFish CurrentFish => NetworkFish.Find(State.FishId);
        public bool Reeling => reeling && tether.Connected;
        public float NormalizedTension => tether.Tension / Mathf.Max(.01f, diver.Balance.Tether.BreakTension);
        public bool HasCatch => State.Status == HarpoonStatus.Carried && CurrentFish != null;
        private void Awake() { diver = GetComponent<NetworkDiver>(); }
        public override void OnStartNetwork() { All.Add(this); }
        public override void OnStopNetwork() { All.Remove(this); if (Local == this) Local = null; }
        public override void OnStartServer()
        {
            current = new FishingSnapshot { FishId = -1, HelpTargetId = -1, Status = HarpoonStatus.Ready };
            snapshot.Value = current;
        }
        public override void OnStartClient()
        {
            if (!IsOwner) return;
            Local = this; input = FindFirstObjectByType<DiverInput>();
        }
        public override void OnStopServer() { ReleaseFish(HarpoonStatus.Released); }
        private void Update()
        {
            if (!IsOwner || !IsClientStarted || input == null) return;
            if (diver.AutomationControlled) return;
            if (input.FirePressed) Fire();
            if (input.ReleasePressed) Release();
            if (input.InteractPressed) PickUp();
            if (Time.unscaledTime >= nextActions)
            {
                nextActions = Time.unscaledTime + .1f;
                Actions(input.ReelHeld, input.HelpHeld);
            }
        }
        [ServerRpc] public void Actions(bool reel, bool help)
        { reeling = reel && diver.IsActive; helping = help && diver.IsActive; lastActions = Time.unscaledTime; }
        [ServerRpc] public void Fire()
        {
            if (!diver.IsActive || fish != null || Time.unscaledTime < nextShot) return;
            nextShot = Time.unscaledTime + diver.Balance.HarpoonCooldown;
            current.ShotSequence++;
            current.Status = HarpoonStatus.Miss;
            if (Physics.Raycast(diver.AimOrigin, diver.AimDirection, out var hit, diver.Balance.Tether.MaxLength, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<NetworkFish>();
                if (target != null && Vector3.Distance(diver.AimOrigin, target.transform.position) <= diver.Balance.Tether.MaxLength && target.TryHook(this))
                {
                    fish = target;
                    tether = TetherSimulation.Attach(Vector3.Distance(diver.AimOrigin, fish.transform.position), diver.Balance.Tether);
                    current.FishId = fish.ObjectId; current.Status = HarpoonStatus.Hooked;
                    Debug.Log($"RAID_HOOK diver={ObjectId} fish={fish.ObjectId} species={fish.Species.DisplayName}");
                }
            }
            snapshot.Value = current;
        }
        [ServerRpc] public void Release() { ReleaseFish(HarpoonStatus.Released); }
        [ServerRpc] public void PickUp()
        {
            if (!diver.IsActive || current.Status == HarpoonStatus.Carried) return;
            if (fish == null)
            {
                if (!Physics.Raycast(diver.AimOrigin, diver.AimDirection, out var hit, diver.Balance.FishPickupDistance, ~0, QueryTriggerInteraction.Collide)) return;
                var target = hit.collider.GetComponentInParent<NetworkFish>();
                if (target == null || target.State.State != FishState.Captured || !target.TryCarry(this)) return;
                fish = target;
            }
            else
            {
                if (Vector3.Distance(diver.AimOrigin, fish.transform.position) > diver.Balance.FishPickupDistance) return;
                Vector3 delta = fish.transform.position - diver.AimOrigin;
                if (Physics.Raycast(diver.AimOrigin, delta.normalized, out var obstruction, delta.magnitude, ~0, QueryTriggerInteraction.Ignore) && obstruction.collider.GetComponentInParent<NetworkFish>() != fish) return;
                if (!fish.TryCarry(this)) return;
            }
            TetherSimulation.Release(ref tether);
            current.FishId = fish.ObjectId; current.Status = HarpoonStatus.Carried; current.Connected = false;
            snapshot.Value = current;
            Debug.Log($"RAID_CATCH diver={ObjectId} fish={fish.ObjectId}");
        }

        private void FixedUpdate()
        {
            if (!IsServerStarted) return;
            if (Time.unscaledTime - lastActions > .35f) { reeling = helping = false; }
            if (!diver.IsActive && fish != null) ReleaseFish(HarpoonStatus.Released);
            current.HelpTargetId = FindHelpTarget();
            diver.CarrySpeedMultiplier = current.Status == HarpoonStatus.Carried && fish != null
                ? (fish.Species.IsLarge ? diver.Balance.CarrySpeedLarge : diver.Balance.CarrySpeedSmall) : 1;
            if (fish != null && tether.Connected)
            {
                int helpers = 0;
                foreach (var other in All) if (other != this && other.current.HelpTargetId == ObjectId && other.helping && other.diver.IsActive) helpers++;
                var forces = TetherSimulation.Step(ref tether, diver.Balance.Tether, diver.AimOrigin, fish.transform.position,
                    diver.ServerVelocity, fish.ServerVelocity, reeling, helpers, 1f, Time.fixedDeltaTime);
                current.Helpers = Mathf.Min(helpers, diver.Balance.Tether.MaxAssistants);
                if (current.Helpers > 0 && previousHelpers == 0) Debug.Log($"RAID_ASSIST diver={ObjectId} fish={fish.ObjectId} helpers={current.Helpers}");
                previousHelpers = current.Helpers;
                diver.AddExternalVelocity(forces.PlayerAcceleration * Time.fixedDeltaTime);
                fish.Pull(forces.FishAcceleration, Time.fixedDeltaTime);
                if (forces.Broke) ReleaseFish(HarpoonStatus.Broken);
            }
            current.Length = tether.Length; current.Tension = tether.Tension; current.NormalizedTension = NormalizedTension; current.Connected = tether.Connected;
            snapshot.Value = current;
        }
        private int FindHelpTarget()
        {
            if (!helping || !diver.IsActive) return -1;
            float closest = diver.Balance.InteractionDistance;
            int target = -1;
            foreach (var other in All)
            {
                if (other == this || !other.tether.Connected || !other.diver.IsActive) continue;
                Vector3 delta = other.diver.AimOrigin - diver.AimOrigin;
                float distance = delta.magnitude;
                if (distance > closest || Vector3.Dot(diver.AimDirection, delta.normalized) < .35f) continue;
                if (Physics.Raycast(diver.AimOrigin, delta.normalized, out var hit, distance, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<NetworkDiver>() != other.diver) continue;
                target = other.ObjectId; closest = distance;
            }
            return target;
        }
        private void ReleaseFish(HarpoonStatus status)
        {
            if (fish != null) fish.Release(this);
            fish = null; TetherSimulation.Release(ref tether); reeling = false;
            diver.CarrySpeedMultiplier = 1;
            current.Status = status; current.FishId = -1; current.Connected = false; current.Helpers = 0;
            snapshot.Value = current;
        }
        [Server] public bool TakeCatch(out NetworkFish catchFish)
        {
            catchFish = null;
            if (fish == null || current.Status != HarpoonStatus.Carried) return false;
            catchFish = fish;
            fish = null; current.FishId = -1; current.Status = HarpoonStatus.Ready;
            diver.CarrySpeedMultiplier = 1; snapshot.Value = current; return true;
        }
    }
}
