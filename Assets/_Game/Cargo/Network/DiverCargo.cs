using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    public struct DiverCargoSnapshot { public int LootId; }

    [RequireComponent(typeof(NetworkDiver))]
    public sealed class DiverCargo : NetworkBehaviour
    {
        public static DiverCargo Local { get; private set; }
        private readonly SyncVar<DiverCargoSnapshot> snapshot = new SyncVar<DiverCargoSnapshot>();
        private NetworkDiver diver;
        private DiverFishing fishing;
        private NetworkLoot heldLoot;
        private float nextRequest;
        public NetworkDiver Diver => diver;
        public DiverCargoSnapshot State => snapshot.Value;
        public NetworkLoot HeldLoot => IsServerStarted ? heldLoot : NetworkLoot.Find(State.LootId);
        public bool HasLoot => HeldLoot != null;
        public bool HasFish => fishing != null && fishing.HasCatch;
        public float CarrySpeedMultiplier => HasLoot ? Mathf.Clamp(HeldLoot.Definition.CarrySpeedMultiplier, .1f, 1) : 1;
        private float Range => NetworkCargoStation.Instance == null ? diver.Balance.InteractionDistance
            : Mathf.Max(diver.Balance.InteractionDistance, NetworkCargoStation.Instance.Settings.InteractionDistance);
        private void Awake() { diver = GetComponent<NetworkDiver>(); fishing = GetComponent<DiverFishing>(); }
        public override void OnStartServer() { Publish(); }
        public override void OnStartClient() { if (IsOwner) Local = this; }
        public override void OnStopClient() { if (Local == this) Local = null; }
        public override void OnStopServer() { Drop(); }
        private void FixedUpdate()
        {
            if (!IsServerStarted) return;
            if (!diver.IsActive && heldLoot != null) Drop();
        }
        /// <summary>Owner input router: returns true only when this E press was directed at cargo or loot.</summary>
        public bool TryInteract()
        {
            if (!IsOwner || !IsClientStarted || CargoInteraction.Focus(diver, Range, true) == null) return false;
            RequestInteract(); return true;
        }
        public string InteractionHint
        {
            get
            {
                Component focus = CargoInteraction.Focus(diver, Range, true);
                if (focus is NetworkCargoStation station) return station.InteractionHint(this);
                if (focus is NetworkLoot loot)
                    return HasLoot || HasFish ? "Руки заняты · доставьте добычу к контейнеру" : "E — взять: " + loot.Definition.DisplayName;
                return HasLoot ? "Предмет в руках · R отпустить" : "";
            }
        }
        [ServerRpc] public void RequestInteract()
        {
            if (!diver.IsActive || Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .2f;
            Component focus = CargoInteraction.Focus(diver, Range);
            if (focus is NetworkCargoStation station) { station.TryInteract(this); return; }
            if (focus is not NetworkLoot loot || HasLoot || HasFish
                || !CargoInteraction.CanReach(diver, loot.transform.position, Range, loot.gameObject)) return;
            if (!loot.TryCarry(this)) return;
            heldLoot = loot; Publish();
            Debug.Log($"RAID_LOOT_PICKUP diver={ObjectId} item={loot.State.Item.Id}");
        }
        [ServerRpc] public void ReleaseLoot() { Drop(); }
        private void Drop()
        {
            if (heldLoot != null) heldLoot.Release(this);
            heldLoot = null; Publish();
        }
        [Server] public bool TakeLoot(out NetworkLoot loot)
        {
            loot = heldLoot;
            if (loot == null || loot.State.HolderId != ObjectId) return false;
            heldLoot = null; Publish(); return true;
        }
        [Server] public void NotifyLootRemoved(NetworkLoot source)
        { if (heldLoot != source) return; heldLoot = null; Publish(); }
        private void Publish() { snapshot.Value = new DiverCargoSnapshot { LootId = heldLoot == null ? -1 : heldLoot.ObjectId }; }
    }
}
