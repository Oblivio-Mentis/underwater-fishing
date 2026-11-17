using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;
using SteamTransport = FishySteamworks.FishySteamworks;

namespace UnderwaterFishing.Network.Steam
{
    public sealed class SteamLobbyInfo
    {
        public ulong Id { get; }
        public string Name { get; }
        public ulong HostSteamId { get; }
        public int Members { get; }
        public int Capacity { get; }

        public SteamLobbyInfo(ulong id, string name, ulong hostSteamId, int members, int capacity)
        {
            Id = id;
            Name = name;
            HostSteamId = hostSteamId;
            Members = members;
            Capacity = capacity;
        }
    }

    /// <summary>Steam discovery and invitations. FishNet still owns all gameplay and connection authority.</summary>
    public sealed class SteamLobbyService : MonoBehaviour
    {
        public const int MaximumPlayers = 5;
        public const string GameIdentifier = "underwater-fishing-raid";
        public const string Protocol = "raid-core-1";

        public NetworkManager Manager;
        public SteamTransport Transport;
        [SerializeField] private uint appId = 480;

        public bool Available { get; private set; }
        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "Steam: select Find or Create to connect.";
        public ulong CurrentLobby { get; private set; }
        public IReadOnlyList<SteamLobbyInfo> Lobbies => _lobbies;
        public event Action Changed;

        private readonly List<SteamLobbyInfo> _lobbies = new();
        private FishNet.Transporting.Transport _offlineTransport;
        private bool _transportInitialized;
        private bool _ownsSteamApi;
        private bool _networkSubscribed;
        private bool _closing;
        private bool _networkRequested;
        private float _connectionDeadline;
        private ulong _hostSteamId;
        private uint _effectiveAppId;
        private CallResult<LobbyCreated_t> _created;
        private CallResult<LobbyEnter_t> _joined;
        private CallResult<LobbyMatchList_t> _found;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyChatUpdate_t> _membershipChanged;
        private Callback<LobbyDataUpdate_t> _lobbyDataChanged;

        private void Start()
        {
            SubscribeNetwork();
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (arguments[i].Equals("+connect_lobby", StringComparison.OrdinalIgnoreCase)
                    && ulong.TryParse(arguments[i + 1], out ulong lobby))
                {
                    JoinLobby(lobby);
                    break;
                }
            }
        }

        private void Update()
        {
            SubscribeNetwork();
            if (!Available) return;
            try { SteamAPI.RunCallbacks(); }
            catch (Exception error)
            {
                CloseSession("Steam callback error: " + error.Message);
                Available = false;
            }
            if (_connectionDeadline > 0f && Time.realtimeSinceStartup > _connectionDeadline)
                CloseSession("Steam host connection timed out. Check the host is still online and retry.");
            RestoreOfflineTransportIfStopped();
        }

        /// <summary>Optional menu initialization; this does not initialize or select the Steam transport.</summary>
        public bool TryInitialize()
        {
            if (Available) return true;
            _effectiveAppId = ResolveAppId();
            if (_effectiveAppId == 0 || (_effectiveAppId == 480 && !Application.isEditor && !Debug.isDebugBuild))
            {
                SetStatus("Steam unavailable: configure the game's App ID. App ID 480 is for development builds only.");
                return false;
            }
            try
            {
                if (!SteamAPI.IsSteamRunning())
                {
                    SetStatus("Steam unavailable: start Steam and sign in, then retry.");
                    return false;
                }
                if (!CallbackDispatcher.IsInitialized)
                {
                    Environment.SetEnvironmentVariable("SteamAppId", _effectiveAppId.ToString());
                    Environment.SetEnvironmentVariable("SteamGameId", _effectiveAppId.ToString());
                    if (SteamAPI.InitEx(out string error) != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                    {
                        SetStatus("Steam unavailable: " + error);
                        return false;
                    }
                    _ownsSteamApi = true;
                }
                if (SteamUtils.GetAppID().m_AppId != _effectiveAppId)
                {
                    SetStatus("Steam unavailable: the running Steam App ID differs from the configured App ID.");
                    if (_ownsSteamApi) SteamAPI.Shutdown();
                    _ownsSteamApi = false;
                    return false;
                }
                _created = CallResult<LobbyCreated_t>.Create(OnCreated);
                _joined = CallResult<LobbyEnter_t>.Create(OnJoined);
                _found = CallResult<LobbyMatchList_t>.Create(OnFound);
                _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
                _membershipChanged = Callback<LobbyChatUpdate_t>.Create(OnMembershipChanged);
                _lobbyDataChanged = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataChanged);
                Available = true;
                SetStatus("Steam ready: " + SteamFriends.GetPersonaName());
                return true;
            }
            catch (Exception error)
            {
                SetStatus("Steam unavailable: " + error.Message);
                return false;
            }
        }

        public void ConfigureAppId(uint value)
        {
            if (Available || CurrentLobby != 0 || IsBusy)
            {
                SetStatus("Leave Steam and restart the game to change its App ID.");
                return;
            }
            appId = value;
        }

        public void CreateLobby()
        {
            if (!CanBeginSession() || !TryInitialize()) return;
            IsBusy = true;
            _created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaximumPlayers));
            SetStatus("Creating Steam lobby...");
        }

        public void FindLobbies()
        {
            if (IsBusy || !TryInitialize()) return;
            IsBusy = true;
            _lobbies.Clear();
            SteamMatchmaking.AddRequestLobbyListStringFilter("game", GameIdentifier, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("protocol", Protocol, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("build", Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            _found.Set(SteamMatchmaking.RequestLobbyList());
            SetStatus("Finding compatible Steam lobbies...");
        }

        public void JoinLobby(ulong id)
        {
            if (id == 0) { SetStatus("Enter a valid Steam lobby ID."); return; }
            if (!CanBeginSession() || !TryInitialize()) return;
            IsBusy = true;
            _joined.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));
            SetStatus("Joining Steam lobby...");
        }

        public void InviteFriends()
        {
            if (!Available || CurrentLobby == 0) { SetStatus("Create or join a Steam lobby first."); return; }
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(CurrentLobby));
        }

        public void LeaveLobby() => CloseSession("Left Steam lobby.");

        private void OnCreated(LobbyCreated_t result, bool failed)
        {
            IsBusy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK)
            {
                SetStatus("Steam lobby creation failed: " + result.m_eResult);
                return;
            }
            CurrentLobby = result.m_ulSteamIDLobby;
            _hostSteamId = SteamUser.GetSteamID().m_SteamID;
            CSteamID lobby = new(CurrentLobby);
            bool published = SteamMatchmaking.SetLobbyData(lobby, "game", GameIdentifier)
                && SteamMatchmaking.SetLobbyData(lobby, "protocol", Protocol)
                && SteamMatchmaking.SetLobbyData(lobby, "build", Application.version)
                && SteamMatchmaking.SetLobbyData(lobby, "host", _hostSteamId.ToString())
                && SteamMatchmaking.SetLobbyData(lobby, "name", SteamFriends.GetPersonaName() + " — Underwater Fishing")
                && SteamMatchmaking.SetLobbyMemberLimit(lobby, MaximumPlayers);
            if (!published || !SelectSteamTransport())
            {
                CloseSession("Could not configure Steam lobby or transport.");
                return;
            }
            _networkRequested = true;
            _connectionDeadline = Time.realtimeSinceStartup + 30f;
            if (!Manager.ServerManager.StartConnection() || !Manager.ClientManager.StartConnection(_hostSteamId.ToString()))
            {
                CloseSession("Could not start the Steam host.");
                return;
            }
            SteamMatchmaking.SetLobbyJoinable(lobby, true);
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + CurrentLobby);
            SetStatus("Steam lobby " + CurrentLobby + " · host · up to 5 divers");
        }

        private void OnJoined(LobbyEnter_t result, bool failed)
        {
            IsBusy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                SetStatus("Steam lobby join failed: " + result.m_EChatRoomEnterResponse);
                return;
            }
            CurrentLobby = result.m_ulSteamIDLobby;
            CSteamID lobby = new(CurrentLobby);
            if (!IsCompatible(lobby)
                || !ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, "host"), out _hostSteamId)
                || SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID != _hostSteamId
                || SteamMatchmaking.GetNumLobbyMembers(lobby) > MaximumPlayers
                || !SelectSteamTransport())
            {
                CloseSession("Lobby incompatible, full, or its host is unavailable.");
                return;
            }
            _networkRequested = true;
            _connectionDeadline = Time.realtimeSinceStartup + 30f;
            if (!Manager.ClientManager.StartConnection(_hostSteamId.ToString()))
            {
                CloseSession("Could not connect to the Steam host.");
                return;
            }
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + CurrentLobby);
            SetStatus("Connecting to Steam host in lobby " + CurrentLobby + "...");
        }

        private void OnFound(LobbyMatchList_t result, bool failed)
        {
            IsBusy = false;
            if (failed) { SetStatus("Steam lobby search failed. Retry when Steam is online."); return; }
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(i);
                if (!IsCompatible(lobby)) continue;
                ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, "host"), out ulong host);
                _lobbies.Add(new SteamLobbyInfo(lobby.m_SteamID,
                    SteamMatchmaking.GetLobbyData(lobby, "name"), host,
                    SteamMatchmaking.GetNumLobbyMembers(lobby), SteamMatchmaking.GetLobbyMemberLimit(lobby)));
            }
            SetStatus(_lobbies.Count + " compatible Steam lobbies found.");
        }

        private bool IsCompatible(CSteamID lobby) =>
            SteamMatchmaking.GetLobbyData(lobby, "game") == GameIdentifier
            && SteamMatchmaking.GetLobbyData(lobby, "protocol") == Protocol
            && SteamMatchmaking.GetLobbyData(lobby, "build") == Application.version;

        private void OnJoinRequested(GameLobbyJoinRequested_t request)
        {
            if (CurrentLobby == request.m_steamIDLobby.m_SteamID) return;
            // Changing an active session requires leaving it from the menu first.
            JoinLobby(request.m_steamIDLobby.m_SteamID);
        }

        private void OnMembershipChanged(LobbyChatUpdate_t update)
        {
            if (update.m_ulSteamIDLobby != CurrentLobby || CurrentLobby == 0) return;
            EChatMemberStateChange change = (EChatMemberStateChange)update.m_rgfChatMemberStateChange;
            const EChatMemberStateChange departed = EChatMemberStateChange.k_EChatMemberStateChangeLeft
                | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
                | EChatMemberStateChange.k_EChatMemberStateChangeKicked
                | EChatMemberStateChange.k_EChatMemberStateChangeBanned;
            if (update.m_ulSteamIDUserChanged == _hostSteamId && (change & departed) != 0)
                CloseSession("The Steam host left. Session ended.");
            else
                Changed?.Invoke();
        }

        private void OnLobbyDataChanged(LobbyDataUpdate_t update)
        {
            if (CurrentLobby == 0 || update.m_ulSteamIDLobby != CurrentLobby || _hostSteamId == 0) return;
            if (SteamMatchmaking.GetLobbyOwner(new CSteamID(CurrentLobby)).m_SteamID != _hostSteamId)
                CloseSession("Steam lobby owner changed. Session ended; host migration is unsupported.");
            else
                Changed?.Invoke();
        }

        private bool CanBeginSession()
        {
            if (IsBusy) { SetStatus("Wait for the current Steam request."); return false; }
            if (CurrentLobby != 0 || !NetworkIsStopped())
            {
                SetStatus("Leave the current session before creating or joining another lobby.");
                return false;
            }
            if (Manager == null || !Manager.Initialized || Transport == null)
            {
                SetStatus("Steam unavailable: network scene is not configured.");
                return false;
            }
            return true;
        }

        private bool SelectSteamTransport()
        {
            if (!NetworkIsStopped()) return false;
            FishNet.Transporting.Transport previous = Manager.TransportManager.Transport;
            if (previous != Transport) _offlineTransport = previous;
            if (!_transportInitialized)
            {
                if (previous != Transport) Transport.Initialize(Manager, 0);
                _transportInitialized = true;
            }
            Transport.SetMaximumClients(MaximumPlayers);
            Manager.TransportManager.Transport = Transport;
            SubscribeNetwork();
            return true;
        }

        private void SubscribeNetwork()
        {
            if (_networkSubscribed || Manager == null || !Manager.Initialized) return;
            Manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            Manager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            Manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            _networkSubscribed = true;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs state)
        {
            if (_closing || CurrentLobby == 0 || !_networkRequested) return;
            if (state.ConnectionState == LocalConnectionState.Stopped)
                CloseSession("Steam connection closed. Session ended.");
            else if (state.ConnectionState == LocalConnectionState.Started)
            {
                _connectionDeadline = 0f;
                SetStatus("Connected to Steam lobby " + CurrentLobby);
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs state)
        {
            if (!_closing && CurrentLobby != 0 && _networkRequested
                && state.ConnectionState == LocalConnectionState.Stopped)
                CloseSession("Steam host stopped. Session ended.");
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs state)
        {
            if (CurrentLobby == 0 || state.ConnectionState != RemoteConnectionState.Started) return;
            if (Manager.ServerManager.Clients.Count > MaximumPlayers) connection.Disconnect(true);
        }

        private bool NetworkIsStopped()
        {
            if (Manager == null || !Manager.Initialized || Manager.TransportManager.Transport == null) return true;
            FishNet.Transporting.Transport active = Manager.TransportManager.Transport;
            return active.GetConnectionState(true) == LocalConnectionState.Stopped
                && active.GetConnectionState(false) == LocalConnectionState.Stopped
                && !Manager.IsServerStarted && !Manager.IsClientStarted;
        }

        private void CloseSession(string message)
        {
            if (_closing) return;
            _closing = true;
            bool ownedSession = _networkRequested || (CurrentLobby != 0 && Manager != null
                && Manager.Initialized && Manager.TransportManager.Transport == Transport);
            _created?.Cancel();
            _joined?.Cancel();
            _found?.Cancel();
            IsBusy = false;
            if (ownedSession && Manager != null && Manager.Initialized)
            {
                Manager.ClientManager.StopConnection();
                Manager.ServerManager.StopConnection(true);
            }
            if (Available && CurrentLobby != 0)
            {
                try
                {
                    SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobby));
                    SteamFriends.ClearRichPresence();
                }
                catch (Exception error) { Debug.LogWarning("Steam lobby cleanup: " + error.Message); }
            }
            CurrentLobby = 0;
            _hostSteamId = 0;
            _networkRequested = false;
            _connectionDeadline = 0f;
            _closing = false;
            RestoreOfflineTransportIfStopped();
            SetStatus(message);
        }

        private void RestoreOfflineTransportIfStopped()
        {
            if (CurrentLobby == 0 && _offlineTransport != null && Manager != null && Manager.Initialized
                && Manager.TransportManager.Transport == Transport && NetworkIsStopped())
                Manager.TransportManager.Transport = _offlineTransport;
        }

        private uint ResolveAppId()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if ((argument.Equals("-steamAppId", StringComparison.OrdinalIgnoreCase)
                    || argument.Equals("--steam-app-id", StringComparison.OrdinalIgnoreCase))
                    && i + 1 < arguments.Length && uint.TryParse(arguments[i + 1], out uint commandId))
                    return commandId;
            }
            if (uint.TryParse(Environment.GetEnvironmentVariable("SteamAppId"), out uint environmentId)) return environmentId;
            if (uint.TryParse(Environment.GetEnvironmentVariable("SteamGameId"), out environmentId)) return environmentId;
            return appId;
        }

        private void SetStatus(string message)
        {
            Status = message;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            CloseSession("Steam session closed.");
            if (_networkSubscribed && Manager != null)
            {
                Manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                Manager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                Manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }
            _created?.Dispose();
            _joined?.Dispose();
            _found?.Dispose();
            _joinRequested?.Dispose();
            _membershipChanged?.Dispose();
            _lobbyDataChanged?.Dispose();
            if (_ownsSteamApi) SteamAPI.Shutdown();
            Available = false;
        }
    }
}
