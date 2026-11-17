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
        private void Start() { manager = GetComponent<NetworkManager>(); manager.ServerManager.OnServerConnectionState += ServerState; }
        private void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            var trap = Instantiate(TrapPrefab, new Vector3(16, -12, 10), Quaternion.identity);
            manager.ServerManager.Spawn(trap);
        }
        private void OnDestroy() { if (manager != null) manager.ServerManager.OnServerConnectionState -= ServerState; }
    }
}
