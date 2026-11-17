using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnderwaterFishing.Player;
using UnderwaterFishing.Network.Steam;
using UnityEngine;

namespace UnderwaterFishing.Network
{
    public sealed class NetworkSession : MonoBehaviour
    {
        public NetworkManager Manager;
        public NetworkObject DiverPrefab;
        public Tugboat LanTransport;
        public Camera LobbyCamera;
        [Range(5, 60)] public int ConnectionTimeoutSeconds = 10;
        public string Status { get; private set; } = "Не подключено";
        public bool Connected => Manager.IsClientStarted || Manager.IsServerStarted;
        public int PlayerCount => NetworkDiver.All.Count;
        private float telemetryAt;

        private void Start()
        {
            ushort timeout = (ushort)Mathf.Clamp(ConnectionTimeoutSeconds, 5, 60);
            Manager.ServerManager.SetRemoteClientTimeout(RemoteTimeoutType.Development, timeout);
            Manager.ClientManager.SetRemoteServerTimeout(RemoteTimeoutType.Development, timeout);
            Manager.SceneManager.OnClientLoadedStartScenes += Loaded;
            Manager.ClientManager.OnClientConnectionState += ClientState;
            Manager.ServerManager.OnRemoteConnectionState += RemoteState;
            string[] args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "--raid-host") >= 0) HostLan();
            int join = System.Array.IndexOf(args, "--raid-join");
            if (join >= 0 && join + 1 < args.Length) JoinLan(args[join + 1]);
            int quit = System.Array.IndexOf(args, "--raid-exit-after");
            if (quit >= 0 && quit + 1 < args.Length && float.TryParse(args[quit + 1], out float seconds)) Invoke(nameof(Quit), seconds);
        }
        public void HostLan()
        {
            if (Connected) return;
            if (!((RoutingTransport)Manager.TransportManager.Transport).Select(LanTransport)) return;
            Status = "Запуск LAN-хоста…";
            if (Manager.ServerManager.StartConnection()) Manager.ClientManager.StartConnection("127.0.0.1");
        }
        public void JoinLan(string address)
        {
            if (Connected) return;
            if (!((RoutingTransport)Manager.TransportManager.Transport).Select(LanTransport)) return;
            Status = "Подключение к " + address;
            Manager.ClientManager.StartConnection(address);
        }
        public void Disconnect()
        {
            Manager.ClientManager.StopConnection();
            Manager.ServerManager.StopConnection(true);
            Status = "Сессия завершена";
        }
        private void Loaded(NetworkConnection connection, bool asServer)
        {
            if (!asServer || connection.FirstObject != null) return;
            Vector3 spawn = new Vector3(-4, -7, -6);
            for (int slot = 0; slot < 10; slot++)
            {
                spawn = new Vector3(-4 + slot % 5 * 2, -7, -6 - slot / 5 * 3);
                bool occupied = false;
                foreach (var existing in NetworkDiver.All)
                    if ((existing.transform.position - spawn).sqrMagnitude < 2f) { occupied = true; break; }
                if (!occupied) break;
            }
            var diver = Instantiate(DiverPrefab, spawn, Quaternion.identity);
            Manager.ServerManager.Spawn(diver, connection);
            Manager.SceneManager.AddOwnerToDefaultScene(diver);
            Debug.Log($"RAID_SPAWN client={connection.ClientId} object={diver.ObjectId}");
        }
        private void RemoteState(NetworkConnection connection, RemoteConnectionStateArgs state)
        { Debug.Log($"RAID_PEER client={connection.ClientId} state={state.ConnectionState} count={Manager.ServerManager.Clients.Count}"); }
        private void ClientState(ClientConnectionStateArgs state)
        {
            Debug.Log($"RAID_CLIENT state={state.ConnectionState}");
            Status = state.ConnectionState == LocalConnectionState.Started ? "Подключено" : state.ConnectionState.ToString();
            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                FindFirstObjectByType<DiverInput>().SetMenu(true);
                LobbyCamera.enabled = true;
            }
        }
        private void Update()
        {
            LobbyCamera.enabled = NetworkDiver.Local == null;
            if (Time.unscaledTime < telemetryAt) return;
            telemetryAt = Time.unscaledTime + 2;
            if (Connected) Debug.Log($"RAID_SESSION players={PlayerCount} server={Manager.IsServerStarted} client={Manager.IsClientStarted}");
        }
        private void Quit() { Disconnect(); Application.Quit(); }
        private void OnDestroy()
        {
            if (Manager == null) return;
            Manager.SceneManager.OnClientLoadedStartScenes -= Loaded;
            Manager.ClientManager.OnClientConnectionState -= ClientState;
            Manager.ServerManager.OnRemoteConnectionState -= RemoteState;
        }
    }
}
