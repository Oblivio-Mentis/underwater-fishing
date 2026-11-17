using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Cargo;
using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Expedition;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Fishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Progression.Network
{
    public struct UpgradeLevel { public CrewUpgrade Upgrade; public int Level; }
    public struct SonarCharge { public int OwnerId, Count; }
    public struct SonarContact { public Vector3 Position; public string Name; public bool IsFish; }
    public struct ProgressionSnapshot
    {
        public uint Revision;
        public int Quota, SuccessfulOfferings, LastOfferingValue;
        public OfferingResult LastOffering;
        public ExpeditionPhase Phase;
        public float OfferingSeconds;
    }

    public sealed class NetworkProgression : NetworkBehaviour
    {
        public static NetworkProgression Instance { get; private set; }
        [SerializeField] private ProgressionConfig config;
        private readonly SyncVar<ProgressionSnapshot> snapshot = new SyncVar<ProgressionSnapshot>();
        private readonly SyncList<ResourceAmount> stocks = new SyncList<ResourceAmount>();
        private readonly SyncList<UpgradeLevel> levels = new SyncList<UpgradeLevel>();
        private readonly SyncList<SonarCharge> charges = new SyncList<SonarCharge>();
        private ProgressionModel model;
        private ProgressionModel costView;
        private float nextClockPublish;
        public ProgressionSettings Settings => config.Settings;
        public ProgressionSnapshot State => snapshot.Value;
        public IReadOnlyList<ResourceAmount> Stocks => stocks;
        public IReadOnlyList<UpgradeLevel> Levels => levels;
        public ProgressionModel ServerModel => IsServerStarted ? model : null;
        public UpgradeEffects Effects => UpgradeEffects.From(Settings, Level);
        public event Action Changed;
        // Called on the server once at the start of each dive; consumers create runtime settings copies.
        public event Action<UpgradeEffects> ApplyForNextDive;
        public void Configure(ProgressionConfig data) { config = data; }
        public override void OnStartNetwork() { Instance = this; }
        public override void OnStopNetwork() { if (Instance == this) Instance = null; }
        public override void OnStartServer()
        {
            model = new ProgressionModel(Settings); model.Changed += Publish; Publish();
        }
        public override void OnStopServer()
        { if (model != null) model.Changed -= Publish; model = null; }
        public int Level(CrewUpgrade upgrade)
        { foreach (var row in levels) if (row.Upgrade == upgrade) return row.Level; return 0; }
        public int Charges(int ownerId)
        { foreach (var row in charges) if (row.OwnerId == ownerId) return row.Count; return 0; }
        public ResourceAmount[] UpgradeCost(CrewUpgrade upgrade)
        {
            if (costView == null) costView = new ProgressionModel(Settings);
            return costView.UpgradeCost(upgrade, Level(upgrade));
        }
        private void Publish()
        {
            if (model == null) return;
            stocks.Clear(); foreach (var pair in model.Stocks) stocks.Add(new ResourceAmount(pair.Key, pair.Value));
            levels.Clear(); foreach (CrewUpgrade upgrade in Enum.GetValues(typeof(CrewUpgrade))) levels.Add(new UpgradeLevel { Upgrade = upgrade, Level = model.Level(upgrade) });
            charges.Clear(); foreach (var pair in model.SonarCharges) charges.Add(new SonarCharge { OwnerId = pair.Key, Count = pair.Value });
            snapshot.Value = new ProgressionSnapshot { Revision = model.Revision, Quota = model.Quota,
                SuccessfulOfferings = model.SuccessfulOfferings, LastOfferingValue = model.LastOfferingValue,
                LastOffering = model.LastOffering, Phase = model.Phase, OfferingSeconds = model.OfferingSeconds };
            Changed?.Invoke();
        }
        private void FixedUpdate()
        {
            if (!IsServerStarted || model == null || model.Phase != ExpeditionPhase.Offering || model.OfferingResolved) return;
            if (model.StepOffering(Time.fixedDeltaTime)) ResolveOffering();
            else if (Time.unscaledTime >= nextClockPublish)
            {
                nextClockPublish = Time.unscaledTime + .2f;
                var state = snapshot.Value; state.OfferingSeconds = model.OfferingSeconds; snapshot.Value = state;
            }
        }
        [Server] public void SetPhase(ExpeditionPhase phase)
        {
            // Older expedition adapters emitted their outer Offering event after a nested CompleteOffering.
            if (model == null || (phase == ExpeditionPhase.Offering && model.OfferingResolved)) return;
            model.SetPhase(phase);
        }
        [Server] public void BeginExpedition()
        {
            if (model == null) return;
            model.SetPhase(ExpeditionPhase.Expedition);
            NetworkCargoStation.Instance?.SetWinchSpeed(model.Effects.WinchMultiplier);
            ApplyForNextDive?.Invoke(model.Effects);
        }
        [Server] public void ResetRun()
        { model?.ResetRun(); NetworkCargoStation.Instance?.SetWinchSpeed(1); }
        [Server] public void RemoveCrew(int ownerId) { model?.RemoveCrew(ownerId); }
        public bool CanUse(DiverProgression actor, ProgressionTerminalKind kind, bool localView = false)
        {
            if (actor == null || IsServerStarted && model == null) return false;
            var phase = IsServerStarted ? model.Phase : State.Phase;
            if (phase != ExpeditionPhase.Base && !(kind == ProgressionTerminalKind.Offering && phase == ExpeditionPhase.Offering)) return false;
            foreach (var terminal in ProgressionTerminal.All)
                if (terminal.Kind == kind && terminal.CanUse(actor.Diver, Settings.TerminalDistance, localView)) return true;
            return false;
        }
        [Server] public bool Process(DiverProgression actor, ulong id)
        { return CanUse(actor, ProgressionTerminalKind.Processing) && model.TryProcess(CargoAdapter.Create(), id); }
        [Server] public bool Reserve(DiverProgression actor, ulong id, bool reserved)
        { return CanUse(actor, ProgressionTerminalKind.Offering) && model.TryReserve(CargoAdapter.Create(), id, reserved); }
        [Server] public bool Buy(DiverProgression actor, CrewUpgrade upgrade, int expectedLevel, uint expectedRevision)
        { return CanUse(actor, ProgressionTerminalKind.Workshop) && model.TryBuy(upgrade, expectedLevel, expectedRevision); }
        [Server] public bool CraftSonar(DiverProgression actor, int expectedCharges, uint expectedRevision)
        { return CanUse(actor, ProgressionTerminalKind.Workshop) && model.TryCraftSonar(actor.OwnerId, expectedCharges, expectedRevision); }
        [Server] public bool SubmitOffering(DiverProgression actor, uint expectedRevision)
        {
            var cargo = CargoAdapter.Create();
            if (!CanUse(actor, ProgressionTerminalKind.Offering) || model.Phase != ExpeditionPhase.Offering
                || model.OfferingResolved || model.Revision != expectedRevision || cargo == null
                || ProgressionModel.ReservedValue(cargo.Hold) < model.Quota) return false;
            ResolveOffering(); return true;
        }
        [Server] public bool UseSonar(DiverProgression actor, int expectedCharges, out SonarContact[] contacts)
        {
            contacts = Array.Empty<SonarContact>();
            if (actor == null || !actor.Diver.IsActive || model == null || !model.TryUseSonar(actor.OwnerId, expectedCharges)) return false;
            Vector3 origin = actor.Diver.transform.position;
            float range = Mathf.Max(0, Settings.SonarRange);
            var hits = new List<SonarContact>();
            foreach (var fish in NetworkFish.All)
                if (fish.IsServerStarted && fish.State.HolderId < 0 && (fish.transform.position - origin).sqrMagnitude <= range * range)
                    hits.Add(new SonarContact { Position = fish.transform.position, Name = fish.Species.DisplayName, IsFish = true });
            foreach (var loot in NetworkLoot.All)
                if (loot.IsServerStarted && loot.State.HolderId < 0 && (loot.transform.position - origin).sqrMagnitude <= range * range)
                    hits.Add(new SonarContact { Position = loot.transform.position, Name = loot.State.Item.Name });
            contacts = hits.ToArray(); return true;
        }
        [Server] public void ResolveOffering()
        {
            if (model == null || model.Phase != ExpeditionPhase.Offering || model.OfferingResolved) return;
            var cargo = NetworkCargoStation.Instance;
            var expedition = NetworkExpedition.Instance;
            if (cargo == null || expedition == null) return;
            bool prior = cargo.HoldOperationsAllowed;
            bool resolved, success;
            // Only this synchronous transaction may use the hold outside Base; player RPCs still fail the phase guard.
            cargo.SetHoldOperationsAllowed(true);
            try { resolved = model.ResolveOffering(CargoAdapter.Create(), out success); }
            finally { cargo.SetHoldOperationsAllowed(prior); }
            if (!resolved) return;
            Debug.Log($"RAID_OFFERING success={success} value={model.LastOfferingValue} nextQuota={model.Quota}");
            expedition.CompleteOffering(success);
        }
        private sealed class CargoAdapter : IProtectedCargo
        {
            private readonly NetworkCargoStation station;
            private CargoAdapter(NetworkCargoStation authority) { station = authority; }
            public static CargoAdapter Create() => NetworkCargoStation.Instance?.ServerModel == null ? null : new CargoAdapter(NetworkCargoStation.Instance);
            public IReadOnlyList<CargoItem> Hold => station.ServerModel.Hold;
            public bool TryConsume(ulong id, CargoDisposition disposition, out CargoItem consumed) => station.TryConsumeProtected(id, disposition, out consumed);
            public bool TryReserve(ulong id, bool reserve)
            {
                bool prior = station.HoldOperationsAllowed;
                station.SetHoldOperationsAllowed(true);
                try { return station.TryReserve(id, reserve); }
                finally { station.SetHoldOperationsAllowed(prior); }
            }
        }
    }
}
