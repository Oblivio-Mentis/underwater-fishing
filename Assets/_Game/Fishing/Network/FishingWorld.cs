using FishNet.Object;
using FishNet.Transporting;
using UnderwaterFishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Network
{
    public sealed class FishingWorld : MonoBehaviour
    {
        public NetworkObject SmallFish, LargeFish;
        public Vector3[] SmallSpawns = { new Vector3(16, -10, 0), new Vector3(23, -18, -12), new Vector3(3, -22, 20) };
        public Vector3[] LargeSpawns = { new Vector3(25, -20, 8) };
        private NetworkSession session;
        private bool spawned;
        private void Start()
        {
            session = GetComponent<NetworkSession>();
            session.Manager.ServerManager.OnServerConnectionState += StateChanged;
            if (session.Manager.IsServerStarted) SpawnFish();
        }
        private void StateChanged(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Started) SpawnFish();
            if (state.ConnectionState == LocalConnectionState.Stopped) spawned = false;
        }
        private void SpawnFish()
        {
            if (spawned || SmallFish == null || LargeFish == null) return;
            spawned = true;
            foreach (var position in SmallSpawns) Spawn(SmallFish, position);
            foreach (var position in LargeSpawns) Spawn(LargeFish, position);
        }
        private void Spawn(NetworkObject prefab, Vector3 position)
        { session.Manager.ServerManager.Spawn(Instantiate(prefab, position, Quaternion.identity)); }
        private void OnDestroy()
        { if (session != null && session.Manager != null) session.Manager.ServerManager.OnServerConnectionState -= StateChanged; }
    }
}
