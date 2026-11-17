using System.Collections.Generic;
using FishNet.Object;
using FishNet.Transporting;
using UnderwaterFishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Cargo.Network
{
    public sealed class CargoWorld : MonoBehaviour
    {
        public NetworkObject StationPrefab, ScrapPrefab, ElectronicsPrefab;
        public Vector3[] ScrapSpawns = { new Vector3(12, -12, 4), new Vector3(18, -25, 15) };
        public Vector3[] ElectronicsSpawns = { new Vector3(20, -26, 17), new Vector3(7, -20, -13) };
        private NetworkSession session;
        private bool spawned;
        private readonly List<NetworkObject> spawnedLoot = new List<NetworkObject>();
        private void Start()
        {
            session = GetComponent<NetworkSession>();
            session.Manager.ServerManager.OnServerConnectionState += StateChanged;
            if (session.Manager.IsServerStarted) SpawnWorld();
        }
        private void StateChanged(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Started) SpawnWorld();
            if (state.ConnectionState == LocalConnectionState.Stopped) { spawned = false; spawnedLoot.Clear(); }
        }
        private void SpawnWorld()
        {
            if (spawned || StationPrefab == null) return;
            spawned = true;
            session.Manager.ServerManager.Spawn(Instantiate(StationPrefab));
            if (GetComponent<UnderwaterFishing.Expedition.Network.ExpeditionWorld>() == null || UnderwaterFishing.World.DevelopmentGym.Active) SpawnExpeditionLoot();
        }
        /// <summary>Root expedition controller calls after changing phase; server owns all world item creation.</summary>
        public void SpawnExpeditionLoot()
        {
            if (session == null || !session.Manager.IsServerStarted) return;
            ClearExpeditionLoot();
            foreach (Vector3 p in ScrapSpawns) SpawnLoot(ScrapPrefab, p);
            foreach (Vector3 p in ElectronicsSpawns) SpawnLoot(ElectronicsPrefab, p);
        }
        private void SpawnLoot(NetworkObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            var loot = Instantiate(prefab, position, Quaternion.identity);
            session.Manager.ServerManager.Spawn(loot); spawnedLoot.Add(loot);
        }
        public void ClearExpeditionLoot()
        {
            if (session == null || !session.Manager.IsServerStarted) return;
            // Release owner-held references before despawning the world at the end of a dive.
            foreach (var loot in spawnedLoot)
                if (loot != null && loot.IsSpawned) session.Manager.ServerManager.Despawn(loot);
            spawnedLoot.Clear();
        }
        private void OnDestroy()
        { if (session != null && session.Manager != null) session.Manager.ServerManager.OnServerConnectionState -= StateChanged; }
    }
}
