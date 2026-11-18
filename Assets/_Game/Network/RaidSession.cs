using System;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using FishNet.Transporting.Multipass;
using UnityEngine;

namespace Raid
{
    public sealed class RaidSession : MonoBehaviour
    {
        public NetworkManager manager;
        public Tugboat localTransport;
        public FishySteamworks.FishySteamworks steamTransport;
        public Multipass multipass;
        public Camera menuCamera;
        public SteamLobby lobby;
        public string Status { get; private set; } = "Выберите подключение";
        public bool Busy { get; private set; }
        public bool SteamMode { get; private set; }
        public bool Connected => manager.IsClientStarted;
        private string address = "127.0.0.1";
        private string lobbyId = "";
        private float startedAt;

        private void Start()
        {
            Application.runInBackground = true;
            manager.ClientManager.OnClientConnectionState += OnClientState;
            manager.ServerManager.OnServerConnectionState += OnServerState;
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-raid-host") >= 0) HostLocal();
            if (Array.IndexOf(args, "-raid-client") >= 0) JoinLocal();
        }

        private void Update()
        {
            if (menuCamera != null) menuCamera.gameObject.SetActive(!Connected);
            if (Busy && Time.realtimeSinceStartup - startedAt > 25f)
            {
                Disconnect();
                SetStatus("Истекло время подключения. Проверьте хост и сеть.");
            }
        }

        public void SetStatus(string message)
        {
            Status = message;
            Debug.Log("[RAID Session] " + message);
        }

        public bool SelectTransport(bool steam)
        {
            if (Busy || !manager.IsOffline) return false;
            SteamMode = steam;
            multipass.SetClientTransport(steam ? (Transport)steamTransport : localTransport);
            multipass.SetMaximumClients(5);
            Busy = true;
            startedAt = Time.realtimeSinceStartup;
            return true;
        }

        public void HostLocal()
        {
            if (!SelectTransport(false)) return;
            SetStatus("Запускается локальный хост…");
            if (!multipass.StartConnection(true, 0)) Disconnect();
        }

        public void JoinLocal()
        {
            if (!SelectTransport(false)) return;
            localTransport.SetClientAddress(address);
            SetStatus("Подключение к " + address);
            if (!manager.ClientManager.StartConnection()) Disconnect();
        }

        public void StartSteamHost()
        {
            SetStatus("Запускается Steam-хост…");
            if (!multipass.StartConnection(true, 1)) Disconnect();
        }

        public void StartSteamClient(string owner)
        {
            steamTransport.SetClientAddress(owner);
            SetStatus("Подключение к Steam-хосту…");
            if (!manager.ClientManager.StartConnection()) Disconnect();
        }

        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                if (SteamMode) lobby.PublishHost();
                else manager.ClientManager.StartConnection();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && Busy)
                Disconnect();
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Busy = false;
                SetStatus("В сессии. Esc — курсор и меню");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                Busy = false;
                if (SteamMode) lobby.Leave();
                manager.ServerManager.StopConnection(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SetStatus("Сессия завершена или хост отключён. Миграция хоста не выполняется.");
            }
        }

        public void Disconnect()
        {
            Busy = false;
            if (lobby != null) lobby.Leave();
            manager.ClientManager.StopConnection();
            manager.ServerManager.StopConnection(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.ClientManager.OnClientConnectionState -= OnClientState;
            manager.ServerManager.OnServerConnectionState -= OnServerState;
        }

        private void OnGUI()
        {
            if (Connected && Cursor.lockState == CursorLockMode.Locked) return;
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 16;
            GUILayout.BeginArea(new Rect(24,24,460,Screen.height-48), GUI.skin.box);
            GUILayout.Label("RAID / UNDERWATER FISHING");
            GUILayout.Label("FOUNDATION · 2–5 игроков · Windows");
            GUILayout.Space(12);
            GUILayout.Label(Status);
            if (Connected)
            {
                GUILayout.Label(SteamMode ? "Steam lobby: " + lobby.LobbyId : "Локальная сеть");
                if (SteamMode && GUILayout.Button("Пригласить друга через Steam")) lobby.Invite();
                if (GUILayout.Button("Отключиться")) Disconnect();
            }
            else
            {
                GUI.enabled = !Busy;
                if (GUILayout.Button("Создать закрытое Steam-лобби")) lobby.Create();
                GUILayout.Label("Steam Lobby ID (от хоста):");
                lobbyId = GUILayout.TextField(lobbyId);
                if (GUILayout.Button("Войти в Steam-лобби")) lobby.Join(lobbyId);
                GUILayout.Space(16);
                GUILayout.Label("Локальный тест / IP хоста:");
                address = GUILayout.TextField(address);
                if (GUILayout.Button("Локальный хост")) HostLocal();
                if (GUILayout.Button("Локальный клиент")) JoinLocal();
                GUI.enabled = true;
                if (Busy && GUILayout.Button("Отменить")) Disconnect();
            }
            GUILayout.Space(12);
            GUILayout.Label("WASD · мышь · Space/Ctrl вверх/вниз · Shift ускорение");
            GUILayout.Label("Steam 480 — только тест. Два Steam-игрока требуют два ПК и аккаунта.");
            GUILayout.EndArea();
        }
    }
}
