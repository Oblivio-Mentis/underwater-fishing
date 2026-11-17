using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    public struct CargoSnapshot
    {
        public WinchState State;
        public Vector3 Position;
        public int ContainerCount, UsedCapacity, Capacity, HoldCount;
        public float Progress;
        public uint Revision;
    }

    public sealed class NetworkCargoStation : NetworkBehaviour
    {
        public static NetworkCargoStation Instance { get; private set; }
        [SerializeField] private CargoSettings settings;
        [SerializeField] private Vector3 deployedPosition = new Vector3(4, -14, 6), holdPosition = new Vector3(4, -7, 6);
        [SerializeField] private TextMesh statusLabel;
        [SerializeField] private LineRenderer cable;
        private readonly SyncVar<CargoSnapshot> snapshot = new SyncVar<CargoSnapshot>();
        private readonly SyncList<CargoItem> containerItems = new SyncList<CargoItem>();
        private readonly SyncList<CargoItem> protectedItems = new SyncList<CargoItem>();
        private CargoModel model;
        private Collider hitbox;
        private uint publishedRevision = uint.MaxValue;
        public CargoSnapshot State => snapshot.Value;
        public IReadOnlyList<CargoItem> Container => containerItems;
        public IReadOnlyList<CargoItem> ProtectedHold => protectedItems;
        public Vector3 DeployedPosition => deployedPosition;
        public Vector3 HoldPosition => holdPosition;
        public CargoSettings Settings => settings;
        public bool HoldOperationsAllowed { get; private set; }
        public bool CurrentLoadProtected => model != null && model.CurrentLoadProtected;
        public CargoModel ServerModel => IsServerStarted ? model : null;
        public void Configure(CargoSettings data, Vector3 deployed, Vector3 hold, TextMesh label, LineRenderer rope)
        { settings = data; deployedPosition = deployed; holdPosition = hold; statusLabel = label; cable = rope; }
        private void Awake() { hitbox = GetComponent<Collider>(); }
        public override void OnStartNetwork() { Instance = this; }
        public override void OnStopNetwork() { if (Instance == this) Instance = null; }
        public override void OnStartServer()
        {
            model = new CargoModel(settings);
            model.StateChanged += StateChanged;
            transform.position = deployedPosition;
            Publish();
        }
        public override void OnStopServer()
        {
            if (model == null) return;
            model.StateChanged -= StateChanged; model.Dispose(); model = null;
        }
        private void StateChanged(WinchState before, WinchState next)
        {
            Debug.Log($"RAID_WINCH state={next} container={model.Container.Count} hold={model.Hold.Count}");
            if (before == WinchState.Unloading && next == WinchState.Descending)
                Debug.Log($"RAID_CARGO_UNLOADED hold={model.Hold.Count}");
        }
        private void FixedUpdate()
        {
            if (!IsServerStarted || model == null) return;
            model.Step(Time.fixedDeltaTime);
            transform.position = PositionFor(model.State, model.Progress);
            Publish();
        }
        private Vector3 PositionFor(WinchState state, float progress)
        {
            if (state == WinchState.Ascending) return Vector3.Lerp(deployedPosition, holdPosition, progress);
            if (state == WinchState.Descending) return Vector3.Lerp(holdPosition, deployedPosition, progress);
            return state == WinchState.Unloading ? holdPosition : deployedPosition;
        }
        private void Update()
        {
            if (!IsClientStarted) return;
            if (!IsServerStarted) transform.position = Vector3.Lerp(transform.position, State.Position, 1 - Mathf.Exp(-18 * Time.deltaTime));
            if (hitbox != null) hitbox.enabled = State.State == WinchState.Deployed;
            if (statusLabel != null)
            {
                statusLabel.text = $"КОНТЕЙНЕР · {State.State}\n{State.ContainerCount} шт · {State.UsedCapacity}/{State.Capacity}\nОтсек: {State.HoldCount}";
                if (NetworkDiver.Local != null) statusLabel.transform.rotation = NetworkDiver.Local.Eyes.transform.rotation;
            }
            if (cable != null)
            { cable.positionCount = 2; cable.SetPosition(0, holdPosition + Vector3.up * 1.4f); cable.SetPosition(1, transform.position + Vector3.up); }
        }
        private void Publish()
        {
            if (model == null) return;
            if (hitbox != null) hitbox.enabled = model.State == WinchState.Deployed;
            if (publishedRevision != model.Revision)
            {
                publishedRevision = model.Revision;
                containerItems.Clear(); containerItems.AddRange(model.Container);
                protectedItems.Clear(); protectedItems.AddRange(model.Hold);
            }
            snapshot.Value = new CargoSnapshot { State = model.State, Position = transform.position,
                ContainerCount = model.Container.Count, UsedCapacity = model.UsedCapacity, Capacity = model.Capacity,
                HoldCount = model.Hold.Count, Progress = model.Progress, Revision = model.Revision };
        }
        public string InteractionHint(DiverCargo actor)
        {
            if (State.State != WinchState.Deployed) return "Контейнер перемещается · продолжайте охоту";
            if (actor != null && (actor.HasLoot || actor.HasFish)) return "E — загрузить добычу";
            return State.ContainerCount > 0 ? "E — поднять и разгрузить контейнер" : "Принесите улов или найденные предметы";
        }
        [Server] public bool TryInteract(DiverCargo actor)
        {
            if (actor == null || model == null || model.State != WinchState.Deployed || !actor.Diver.IsActive
                || !CargoInteraction.CanReach(actor.Diver, transform.position, settings.InteractionDistance, gameObject)) return false;
            if (actor.HasLoot) return DepositLoot(actor);
            if (actor.HasFish) return DepositFish(actor);
            bool lifted = model.StartLift(); Publish(); return lifted;
        }
        private bool DepositLoot(DiverCargo actor)
        {
            NetworkLoot loot = actor.HeldLoot;
            if (loot == null || loot.State.HolderId != actor.ObjectId || !model.CanLoad(loot.State.Item)) return false;
            // The server executes these synchronous operations without yielding; no second request can claim the item.
            if (!actor.TakeLoot(out NetworkLoot taken) || taken != loot) return false;
            if (!model.TryLoad(loot.State.Item)) throw new System.InvalidOperationException("Cargo changed during an atomic deposit.");
            Debug.Log($"RAID_CARGO_LOAD item={loot.State.Item.Id} name={loot.State.Item.Name} size={loot.State.Item.Size}");
            Despawn(loot.NetworkObject); Publish(); return true;
        }
        private bool DepositFish(DiverCargo actor)
        {
            DiverFishing fishing = actor.GetComponent<DiverFishing>();
            NetworkFish fish = fishing.CurrentFish;
            if (fish == null || !fishing.HasCatch || fish.State.HolderId != fishing.ObjectId) return false;
            var species = fish.Species;
            var item = new CargoItem { Id = CargoIdentity.For(fish.gameObject), Name = species.DisplayName,
                IsFish = true, Size = species.CargoSize, OfferingValue = species.OfferingValue,
                Yield = new CargoYield { Bio = species.BioYield, Rare = species.RareYield } };
            if (!model.CanLoad(item) || !fishing.TakeCatch(out NetworkFish taken) || taken != fish) return false;
            if (!model.TryLoad(item)) throw new System.InvalidOperationException("Cargo changed during an atomic fish deposit.");
            Debug.Log($"RAID_CARGO_LOAD item={item.Id} name={item.Name} size={item.Size}");
            Despawn(fish.NetworkObject); Publish(); return true;
        }
        [Server] public bool StartLift()
        { bool lifted = model != null && model.StartLift(); Publish(); return lifted; }
        [Server] public void SetWinchSpeed(float multiplier)
        { if (model != null) model.SpeedMultiplier = Mathf.Clamp(multiplier, .1f, 20); }
        [Server] public void SetHoldOperationsAllowed(bool allowed) { HoldOperationsAllowed = allowed; }
        [Server] public bool TryReserve(ulong id, bool reserve)
        { bool result = model != null && model.TrySetReserved(id, reserve, HoldOperationsAllowed); Publish(); return result; }
        [Server] public bool TryConsumeProtected(ulong id, CargoDisposition disposition, out CargoItem item)
        {
            item = default;
            bool result = model != null && model.TryConsumeProtected(id, disposition, HoldOperationsAllowed, out item);
            Publish(); return result;
        }
        [Server] public void ResetExpedition()
        { model?.ResetExpedition(); transform.position = deployedPosition; Publish(); }
        [Server] public void ResetRun()
        { model?.ResetRun(); transform.position = deployedPosition; Publish(); }
    }
}
