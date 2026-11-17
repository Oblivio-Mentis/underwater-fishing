using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cooperation
{
    public enum CollapseState : byte { Armed, Collapsing, Pinned, Cleared }
    public struct CollapseSnapshot { public CollapseState State; public int VictimId; public float ChangedAt; }

    public sealed class NetworkCollapseTrap : NetworkBehaviour
    {
        public static readonly List<NetworkCollapseTrap> All = new();
        public Transform Slab;
        public Vector3 TriggerSize = new(3, 3, 3);
        private readonly SyncVar<CollapseSnapshot> snapshot = new();
        private CollapseSnapshot current;
        public CollapseSnapshot State => snapshot.Value;
        public NetworkDiver Victim
        {
            get { foreach (var diver in NetworkDiver.All) if (diver.ObjectId == State.VictimId) return diver; return null; }
        }
        public Vector3 RescuePoint => Victim != null ? Victim.AimOrigin : transform.position;
        public override void OnStartNetwork() { All.Add(this); }
        public override void OnStopNetwork() { All.Remove(this); }
        public override void OnStartServer() { current.VictimId = -1; snapshot.Value = current; }
        public override void OnStopServer() { if (Victim != null) Victim.SetPinned(-1); }
        private void FixedUpdate()
        {
            if (!IsServerStarted) return;
            if (current.State == CollapseState.Armed)
            {
                var trigger = new Bounds(transform.position, TriggerSize);
                foreach (var diver in NetworkDiver.All)
                {
                    if (!diver.IsActive || !trigger.Contains(diver.transform.position)) continue;
                    current.VictimId = diver.ObjectId; current.State = CollapseState.Collapsing; current.ChangedAt = Time.time;
                    snapshot.Value = current; diver.SetPinned(ObjectId);
                    Debug.Log($"RAID_TRAP_PIN trap={ObjectId} victim={diver.ObjectId}"); break;
                }
            }
            else if (current.State == CollapseState.Collapsing && Time.time - current.ChangedAt > .65f)
            { current.State = CollapseState.Pinned; snapshot.Value = current; }
            else if ((current.State == CollapseState.Pinned || current.State == CollapseState.Collapsing) && Victim == null)
            { current.State = CollapseState.Cleared; snapshot.Value = current; }
        }
        public bool CanRescue(NetworkDiver helper) => helper != Victim && helper.IsActive && Victim != null
            && (State.State == CollapseState.Pinned || State.State == CollapseState.Collapsing);
        [Server] public void Clear(NetworkDiver helper)
        {
            if (!CanRescue(helper) || !DiverRescue.CanReach(helper, RescuePoint, Victim)) return;
            var victim = Victim;
            // The slab has no blocking collider: authoritative pinning is removed only after the exit is clear.
            current.State = CollapseState.Cleared; current.ChangedAt = Time.time; snapshot.Value = current;
            if (Slab != null) Slab.localPosition = Vector3.up * 3;
            victim.SetPinned(-1);
            Debug.Log($"RAID_TRAP_CLEAR trap={ObjectId} rescuer={helper.ObjectId} victim={victim.ObjectId}");
        }
        private void Update()
        {
            if (Slab == null) return;
            float height = State.State is CollapseState.Armed or CollapseState.Cleared ? 3 : .1f;
            Slab.localPosition = Vector3.MoveTowards(Slab.localPosition, Vector3.up * height, Time.deltaTime * 5);
        }
    }
}
