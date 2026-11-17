using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Data;
using UnderwaterFishing.Diving;
using UnderwaterFishing.Player;
using UnderwaterFishing.World;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Network
{
    public struct ExpeditionSnapshot
    {
        public ExpeditionPhase Phase;
        public int Day, CompletedDays, Crew, RequiredCrew, PendingCrew, Consents;
        public float RemainingSeconds, PhaseSeconds;
        public bool ReturnRequested, Emergency, DevelopmentMode, FastMode;
        public uint Revision;
    }

    public sealed class NetworkExpedition : NetworkBehaviour
    {
        public static NetworkExpedition Instance { get; private set; }
        [SerializeField] private GameBalance balance;
        [SerializeField] private ExpeditionSettings settings;
        [SerializeField] private Vector3 crewAnchor = new Vector3(0, -7, -6);
        [SerializeField] private TextMesh terminalLabel;
        private readonly SyncVar<ExpeditionSnapshot> snapshot = new SyncVar<ExpeditionSnapshot>();
        private readonly SyncList<int> consentingCrew = new SyncList<int>();
        private readonly List<ExpeditionCrewStatus> crew = new List<ExpeditionCrewStatus>(5);
        private ExpeditionModel model;
        private uint publishedRevision = uint.MaxValue;
        private bool fastMode;
        public ExpeditionSnapshot State => snapshot.Value;
        public ExpeditionSettings Settings => settings;
        public bool IsAtBase => State.Phase == ExpeditionPhase.Base;
        public bool GameplayEnabled => State.Phase == ExpeditionPhase.Expedition;
        public event Action ExpeditionStarted;
        public event Action<bool> ExpeditionEnded;
        public event Action OfferingDue;
        public event Action RunReset;
        public event Action<ExpeditionPhase> ServerPhaseChanged;

        public void Configure(GameBalance gameBalance, ExpeditionSettings phaseSettings, TextMesh label)
        { balance = gameBalance; settings = phaseSettings; terminalLabel = label; }
        public override void OnStartNetwork() { Instance = this; }
        public override void OnStopNetwork() { if (Instance == this) Instance = null; }
        public override void OnStartServer()
        {
            fastMode = (Application.isEditor || Debug.isDebugBuild)
                && Array.IndexOf(Environment.GetCommandLineArgs(), "--raid-fast-expeditions") >= 0;
            model = new ExpeditionModel(settings, balance.DevelopmentMode,
                fastMode ? settings.FastExpeditionSeconds : balance.ExpeditionSeconds);
            model.PhaseChanged += PhaseChanged;
            RefreshCrew(); SetCargoAccess(); Publish();
        }
        public override void OnStopServer()
        { if (model != null) model.PhaseChanged -= PhaseChanged; model = null; }

        private void FixedUpdate()
        {
            if (!IsServerStarted || model == null) return;
            RefreshCrew();
            var cargo = NetworkCargoStation.Instance;
            if (model.Phase == ExpeditionPhase.Return && !model.Emergency && cargo != null
                && cargo.State.State == Cargo.WinchState.Deployed && cargo.State.ContainerCount > 0) cargo.StartLift();
            model.Step(Time.fixedDeltaTime, cargo == null || cargo.CurrentLoadProtected);
            SetCargoAccess(); Publish();
        }
        private void RefreshCrew()
        {
            crew.Clear();
            foreach (var diver in NetworkDiver.All)
            {
                if (!diver.IsServerStarted || diver.OwnerId < 0) continue;
                crew.Add(new ExpeditionCrewStatus { Id = diver.OwnerId, Connected = true,
                    Alive = diver.State.Diving.Life != DiverLife.Dead, Aboard = SubmarineInterior.Contains(diver.transform.position) });
            }
            model.UpdateCrew(crew);
        }
        private void PhaseChanged(ExpeditionPhase previous, ExpeditionPhase next)
        {
            Debug.Log($"RAID_EXPEDITION phase={next} day={model.Day} completed={model.CompletedDays} emergency={model.Emergency}");
            SetCargoAccess();
            if (next == ExpeditionPhase.Expedition)
            {
                RelocateCrew();
                ExpeditionStarted?.Invoke();
            }
            if (next == ExpeditionPhase.Debrief)
            {
                // Let world owners release fish/traps/loose loot before resetting the crew's vitals.
                ExpeditionEnded?.Invoke(model.Emergency);
                NetworkCargoStation.Instance?.ResetExpedition();
                RelocateCrew();
            }
            if (next == ExpeditionPhase.Offering) OfferingDue?.Invoke();
            ServerPhaseChanged?.Invoke(next);
        }
        private void RelocateCrew()
        {
            int slot = 0;
            foreach (var diver in NetworkDiver.All)
            {
                if (!diver.IsServerStarted) continue;
                diver.Relocate(crewAnchor + new Vector3(-4 + slot % 5 * 2, 0, -slot / 5 * 3)); slot++;
            }
        }
        private void SetCargoAccess()
        {
            var cargo = NetworkCargoStation.Instance;
            bool allowed = model != null && model.Phase == ExpeditionPhase.Base;
            if (cargo != null && cargo.IsServerStarted && cargo.HoldOperationsAllowed != allowed) cargo.SetHoldOperationsAllowed(allowed);
        }
        private void Publish()
        {
            if (model == null) return;
            if (publishedRevision != model.Revision)
            {
                publishedRevision = model.Revision; consentingCrew.Clear();
                foreach (var member in crew) if (model.HasConsented(member.Id)) consentingCrew.Add(member.Id);
            }
            snapshot.Value = new ExpeditionSnapshot { Phase = model.Phase, Day = model.Day, CompletedDays = model.CompletedDays,
                Crew = model.CrewCount, RequiredCrew = model.RequiredCrew, PendingCrew = model.PendingCrewCount,
                Consents = model.ConsentCount, RemainingSeconds = model.ExpeditionRemaining, PhaseSeconds = model.PhaseRemaining,
                ReturnRequested = model.ReturnRequested, Emergency = model.Emergency, DevelopmentMode = model.DevelopmentMode,
                FastMode = fastMode, Revision = model.Revision };
        }
        public bool HasConsented(int ownerId) => consentingCrew.Contains(ownerId);
        public bool CanUseTerminal(NetworkDiver diver, bool localView = false)
        {
            if (diver == null || !diver.IsActive) return false;
            Vector3 delta = transform.position - diver.AimOrigin;
            Vector3 look = localView && diver.IsOwner ? diver.Eyes.transform.forward : diver.AimDirection;
            if (delta.magnitude > settings.TerminalDistance || Vector3.Dot(look, delta.normalized) < .35f) return false;
            foreach (var hit in Physics.RaycastAll(diver.AimOrigin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<NetworkDiver>() == diver) continue;
                if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform)) return false;
            }
            return true;
        }
        public bool IsFocused(NetworkDiver diver)
        {
            if (!CanUseTerminal(diver, true)) return false;
            return Physics.Raycast(diver.AimOrigin, diver.Eyes.transform.forward, out var hit, settings.TerminalDistance, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<NetworkExpedition>() == this;
        }
        public string TerminalHint => State.Phase == ExpeditionPhase.Base
            ? $"E — экспедиция: Затонувший остов · экипаж {State.Crew}/{State.RequiredCrew}+"
            : State.Phase == ExpeditionPhase.Expedition ? "E — запросить возвращение экипажа"
            : State.Phase == ExpeditionPhase.Failure ? "Забег завершён · начать новый через терминал" : "Переход: " + State.Phase;

        [Server] public bool TryStart(DiverExpedition actor)
        {
            if (model == null || actor == null || !CanUseTerminal(actor.Diver)) return false;
            RefreshCrew(); bool accepted = model.TryStart(actor.OwnerId); Publish(); return accepted;
        }
        [Server] public bool TryRequestReturn(DiverExpedition actor)
        {
            if (model == null || actor == null || !CanUseTerminal(actor.Diver)) return false;
            RefreshCrew(); bool accepted = model.TryRequestReturn(actor.OwnerId); Publish(); return accepted;
        }
        [Server] public bool TrySetConsent(DiverExpedition actor, bool consent)
        {
            if (model == null || actor == null || actor.Diver.State.Diving.Life == DiverLife.Dead) return false;
            RefreshCrew(); bool accepted = model.TrySetReturnConsent(actor.OwnerId, consent); Publish(); return accepted;
        }
        [Server] public void RemoveCrew(int ownerId) { model?.RemoveCrew(ownerId); Publish(); }
        [Server] public bool CompleteOffering(bool success)
        { bool accepted = model != null && model.CompleteOffering(success); Publish(); return accepted; }
        [Server] public bool TryResetRun(DiverExpedition actor)
        {
            if (model == null || model.Phase != ExpeditionPhase.Failure || actor == null || !CanUseTerminal(actor.Diver)) return false;
            model.ResetRun(); NetworkCargoStation.Instance?.ResetRun(); RelocateCrew(); RunReset?.Invoke(); Publish(); return true;
        }
        private void Update()
        {
            if (terminalLabel == null || !IsClientStarted) return;
            terminalLabel.text = $"ЗАТОНУВШИЙ ОСТОВ\n{State.Phase} · день {State.Day}\n" +
                (State.Phase == ExpeditionPhase.Expedition ? $"Осталось {State.RemainingSeconds:0} c" : "Экспедиционный терминал");
        }
    }
}
