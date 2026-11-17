using System;
using FishNet.Object;
using FishNet.Transporting;
using UnderwaterFishing.Expedition;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Progression.Network
{
    public sealed class ProgressionWorld : MonoBehaviour
    {
        public NetworkObject ProgressionPrefab;
        public event Action<UpgradeEffects> ApplyForNextDive;
        private NetworkSession session;
        private ExpeditionWorld expeditions;
        private NetworkProgression authority;
        private void Start()
        {
            session = GetComponent<NetworkSession>();
            expeditions = GetComponent<ExpeditionWorld>();
            if (expeditions != null)
            {
                expeditions.ServerPhaseChanged += PhaseChanged;
                expeditions.ExpeditionStarted += DiveStarted;
                expeditions.OfferingDue += OfferingDue;
                expeditions.RunReset += RunReset;
            }
            session.Manager.ServerManager.OnServerConnectionState += ServerState;
            if (session.Manager.IsServerStarted) Spawn();
        }
        private void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) Spawn();
            if (args.ConnectionState == LocalConnectionState.Stopped) authority = null;
        }
        private void Spawn()
        {
            if (authority != null || ProgressionPrefab == null) return;
            var instance = Instantiate(ProgressionPrefab);
            authority = instance.GetComponent<NetworkProgression>();
            authority.ApplyForNextDive += effects => ApplyForNextDive?.Invoke(effects);
            session.Manager.ServerManager.Spawn(instance);
        }
        private void PhaseChanged(ExpeditionPhase phase) { if (authority != null) authority.SetPhase(phase); }
        private void DiveStarted() { if (authority != null) authority.BeginExpedition(); }
        private void OfferingDue() { if (authority != null) authority.SetPhase(ExpeditionPhase.Offering); }
        private void RunReset()
        {
            if (authority == null) return;
            authority.ResetRun(); ApplyForNextDive?.Invoke(authority.Effects);
        }
        private void OnDestroy()
        {
            if (session != null && session.Manager != null) session.Manager.ServerManager.OnServerConnectionState -= ServerState;
            if (expeditions == null) return;
            expeditions.ServerPhaseChanged -= PhaseChanged;
            expeditions.ExpeditionStarted -= DiveStarted;
            expeditions.OfferingDue -= OfferingDue;
            expeditions.RunReset -= RunReset;
        }
    }
}
