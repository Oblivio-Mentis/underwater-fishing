using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace UnderwaterFishing.Cooperation
{
    public sealed class CooperationWorld : MonoBehaviour
    {
        public NetworkObject TrapPrefab;
        private NetworkManager manager;
        private void Start()
        {
            manager = GetComponent<NetworkManager>(); manager.ServerManager.OnServerConnectionState += ServerState;
            if (manager.IsServerStarted) SpawnDefault();
        }
        private void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            SpawnDefault();
        }
        private void SpawnDefault()
        {
            if (GetComponent<UnderwaterFishing.Expedition.Network.ExpeditionWorld>() != null && !World.DevelopmentGym.Active) return;
            SpawnExpeditionTrap();
        }
        public void SpawnExpeditionTrap()
        {
            if (manager == null || !manager.IsServerStarted) return;
            ClearExpeditionTraps();
            var trap = Instantiate(TrapPrefab, new Vector3(16, -12, 10), Quaternion.identity);
            manager.ServerManager.Spawn(trap);
        }
        public void ClearExpeditionTraps()
        {
            if (manager == null || !manager.IsServerStarted) return;
            foreach (var trap in new System.Collections.Generic.List<NetworkCollapseTrap>(NetworkCollapseTrap.All))
                if (trap != null && trap.IsSpawned) manager.ServerManager.Despawn(trap.NetworkObject);
        }
        private void OnDestroy() { if (manager != null) manager.ServerManager.OnServerConnectionState -= ServerState; }
    }
}
