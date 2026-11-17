using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Fishing.Creatures;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Network
{
    public struct FishSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public FishState State;
        public float Stamina;
        public int HolderId;
    }

    public sealed class NetworkFish : NetworkBehaviour
    {
        public static readonly List<NetworkFish> All = new List<NetworkFish>();
        [SerializeField] private FishSpecies species;
        [SerializeField] private float waterCeiling = -2f, waterFloor = -37f, waterRadius = 37f;
        private readonly SyncVar<FishSnapshot> snapshot = new SyncVar<FishSnapshot>();
        private FishBrain brain;
        private DiverFishing holder;
        private Vector3 ropeVelocity;
        private CapsuleCollider hitbox;
        private readonly RaycastHit[] obstacles = new RaycastHit[12];
        public FishSnapshot State => snapshot.Value;
        public FishSpecies Species => species;
        public Vector3 ServerVelocity { get; private set; }
        public bool IsExhausted => brain != null && brain.IsExhausted;
        public void Configure(FishSpecies definition) => species = definition;
        private void Awake() { hitbox = GetComponent<CapsuleCollider>(); }
        public override void OnStartNetwork() { All.Add(this); }
        public override void OnStopNetwork() { All.Remove(this); }
        public override void OnStartServer()
        {
            brain = new FishBrain(species, transform.position, ObjectId * 7919 + 23);
            Publish();
        }
        public override void OnStopServer() { brain?.Dispose(); brain = null; holder = null; }

        private void Update()
        {
            if (!IsClientStarted || IsServerStarted) return;
            transform.position = Vector3.Lerp(transform.position, State.Position, 1f - Mathf.Exp(-18f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, State.Rotation, 1f - Mathf.Exp(-18f * Time.deltaTime));
        }
        private void FixedUpdate()
        {
            if (!IsServerStarted || brain == null) return;
            if (holder == null && (brain.CurrentState == FishState.Hooked || brain.CurrentState == FishState.Struggling || brain.CurrentState == FishState.Exhausted)) brain.Release();
            float dt = Time.fixedDeltaTime;
            Vector3 before = transform.position;
            if (brain.CurrentState == FishState.Captured)
            {
                hitbox.isTrigger = true;
                if (holder != null)
                {
                    transform.position = holder.Diver.AimOrigin + holder.Diver.AimDirection * species.CarryForwardDistance +
                        holder.Diver.transform.right * species.CarrySideOffset - Vector3.up * species.CarryDownOffset;
                    transform.rotation = Quaternion.LookRotation(holder.Diver.transform.right, Vector3.up);
                }
                ServerVelocity = (transform.position - before) / dt;
                Publish();
                return;
            }
            hitbox.isTrigger = false;
            Vector3 nearest = before;
            bool nearby = false;
            float distance = float.MaxValue;
            if (holder != null) { nearest = holder.Diver.transform.position; nearby = true; }
            else foreach (var diver in NetworkDiver.All)
            {
                if (!diver.IsActive || diver.State.Sheltered) continue;
                float sqr = (diver.transform.position - before).sqrMagnitude;
                if (sqr >= distance) continue;
                distance = sqr; nearest = diver.transform.position; nearby = true;
            }
            Vector3 desired = brain.Step(dt, before, nearest, nearby, holder != null && holder.Reeling, holder == null ? 0 : holder.NormalizedTension);
            Vector3 displacement = (desired + ropeVelocity) * dt;
            if (displacement.sqrMagnitude > .000001f)
            {
                Vector3 axis = displacement.normalized;
                float half = Mathf.Max(0, hitbox.height * .5f - hitbox.radius);
                Vector3 center = transform.TransformPoint(hitbox.center);
                int count = Physics.CapsuleCastNonAlloc(center - axis * half, center + axis * half, hitbox.radius, axis, obstacles,
                    displacement.magnitude, ~0, QueryTriggerInteraction.Ignore);
                float travel = displacement.magnitude;
                for (int i = 0; i < count; i++)
                    if (obstacles[i].collider != hitbox) travel = Mathf.Min(travel, Mathf.Max(0, obstacles[i].distance - .03f));
                displacement = axis * travel;
            }
            Vector3 next = before + displacement;
            next.y = Mathf.Clamp(next.y, waterFloor, waterCeiling);
            next.x = Mathf.Clamp(next.x, -waterRadius, waterRadius);
            next.z = Mathf.Clamp(next.z, -waterRadius, waterRadius);
            transform.position = next;
            ServerVelocity = (next - before) / dt;
            if (ServerVelocity.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(ServerVelocity.normalized);
            ropeVelocity *= Mathf.Exp(-species.RopeDrag * dt);
            Publish();
        }
        private void Publish()
        {
            if (brain == null) return;
            snapshot.Value = new FishSnapshot { Position = transform.position, Rotation = transform.rotation, State = brain.CurrentState,
                Stamina = brain.Stamina, HolderId = holder == null ? -1 : holder.ObjectId };
        }
        [Server] public bool TryHook(DiverFishing fishing)
        {
            if (holder != null || brain == null || !brain.TryHook()) return false;
            holder = fishing; Publish(); return true;
        }
        [Server] public bool TryCarry(DiverFishing fishing)
        {
            if (brain == null || (holder != null && holder != fishing)) return false;
            if (brain.CurrentState != FishState.Captured && !brain.Capture()) return false;
            holder = fishing; ropeVelocity = Vector3.zero; Publish(); return true;
        }
        [Server] public void Release(DiverFishing fishing)
        {
            if (holder != fishing) return;
            holder = null;
            if (brain != null && brain.CurrentState != FishState.Captured) brain.Release();
            Publish();
        }
        [Server] public void Pull(Vector3 acceleration, float dt)
        { ropeVelocity = Vector3.ClampMagnitude(ropeVelocity + acceleration * (dt / Mathf.Max(.1f, species.RopeMass)), 12f); }
        public static NetworkFish Find(int id)
        { foreach (var fish in All) if (fish.ObjectId == id) return fish; return null; }
    }
}
