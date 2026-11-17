using System;
using FishNet.Object;
using FishNet.Transporting;
using UnderwaterFishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Expedition.Network
{
    public sealed class ExpeditionWorld : MonoBehaviour
    {
        public NetworkObject ExpeditionPrefab;
        public event Action ExpeditionStarted;
        public event Action<bool> ExpeditionEnded;
        public event Action OfferingDue;
        public event Action RunReset;
        public event Action<ExpeditionPhase> ServerPhaseChanged;
        private NetworkSession session;
        private NetworkExpedition authority;
        private bool spawned;
        private void Start()
        {
            if (UnderwaterFishing.World.DevelopmentGym.Active) return;
            session = GetComponent<NetworkSession>();
            session.Manager.ServerManager.OnServerConnectionState += StateChanged;
            if (session.Manager.IsServerStarted) SpawnAuthority();
        }
        private void StateChanged(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Started) SpawnAuthority();
            if (state.ConnectionState == LocalConnectionState.Stopped) { spawned = false; authority = null; }
        }
        private void SpawnAuthority()
        {
            if (spawned || ExpeditionPrefab == null) return;
            spawned = true;
            var instance = Instantiate(ExpeditionPrefab);
            authority = instance.GetComponent<NetworkExpedition>();
            authority.ExpeditionStarted += () => ExpeditionStarted?.Invoke();
            authority.ExpeditionEnded += emergency => ExpeditionEnded?.Invoke(emergency);
            authority.OfferingDue += () => OfferingDue?.Invoke();
            authority.RunReset += () => RunReset?.Invoke();
            authority.ServerPhaseChanged += phase => ServerPhaseChanged?.Invoke(phase);
            session.Manager.ServerManager.Spawn(instance);
        }
        private void OnDestroy()
        { if (session != null && session.Manager != null) session.Manager.ServerManager.OnServerConnectionState -= StateChanged; }
    }
}
