using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    public struct LootSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public int HolderId;
        public CargoItem Item;
    }

    public sealed class NetworkLoot : NetworkBehaviour
    {
        public static readonly List<NetworkLoot> All = new List<NetworkLoot>();
        [SerializeField] private LootDefinition definition;
        private readonly SyncVar<LootSnapshot> snapshot = new SyncVar<LootSnapshot>();
        private DiverCargo holder;
        private Collider hitbox;
        private CargoItem item;
        public LootSnapshot State => snapshot.Value;
        public LootDefinition Definition => definition;
        public void Configure(LootDefinition data) => definition = data;
        private void Awake() { hitbox = GetComponent<Collider>(); }
        public override void OnStartNetwork() { All.Add(this); }
        public override void OnStopNetwork() { All.Remove(this); }
        public override void OnStartServer()
        {
            item = new CargoItem { Id = CargoIdentity.For(gameObject), Name = definition.DisplayName,
                Size = Mathf.Max(1, definition.CargoSize), Yield = definition.Yield };
            Publish();
        }
        public override void OnStopServer()
        { if (holder != null) holder.NotifyLootRemoved(this); holder = null; }
        private void FixedUpdate()
        {
            if (!IsServerStarted) return;
            if (holder != null && (!holder.IsServerStarted || !holder.Diver.IsActive)) Release(holder);
            if (holder != null)
            {
                Vector3 origin = holder.Diver.AimOrigin;
                Vector3 carryDelta = holder.Diver.AimDirection * 1.7f - Vector3.up * .65f;
                float travel = carryDelta.magnitude;
                float radius = Mathf.Max(definition.Shape.x, definition.Shape.y, definition.Shape.z) * .5f;
                foreach (var hit in Physics.SphereCastAll(origin, radius, carryDelta.normalized, travel, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider != hitbox && hit.collider.GetComponentInParent<NetworkDiver>() != holder.Diver)
                        travel = Mathf.Min(travel, Mathf.Max(.1f, hit.distance - .03f));
                transform.position = origin + carryDelta.normalized * travel;
                transform.rotation = Quaternion.Euler(0, holder.Diver.transform.eulerAngles.y, 0);
            }
            Publish();
        }
        private void Update()
        {
            if (!IsClientStarted || IsServerStarted) return;
            transform.position = Vector3.Lerp(transform.position, State.Position, 1 - Mathf.Exp(-18 * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, State.Rotation, 1 - Mathf.Exp(-18 * Time.deltaTime));
            if (hitbox != null) hitbox.isTrigger = State.HolderId >= 0;
        }
        private void Publish()
        {
            if (hitbox != null) hitbox.isTrigger = holder != null;
            snapshot.Value = new LootSnapshot { Position = transform.position, Rotation = transform.rotation,
                HolderId = holder == null ? -1 : holder.ObjectId, Item = item };
        }
        [Server] public bool TryCarry(DiverCargo carrier)
        {
            if (carrier == null || holder != null || carrier.HasLoot || carrier.HasFish || !carrier.Diver.IsActive || !item.IsValid) return false;
            holder = carrier; Publish(); return true;
        }
        [Server] public void Release(DiverCargo carrier)
        { if (holder != carrier) return; holder = null; Publish(); }
        public static NetworkLoot Find(int objectId)
        { foreach (var loot in All) if (loot.ObjectId == objectId) return loot; return null; }
    }
}
