using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace Raid
{
    /// <summary>Steam matchmaking only; gameplay and connection state belong to RaidSession.</summary>
    public sealed class SteamLobby : MonoBehaviour
    {
        public RaidSession session;
        private const string Game = "raid-underwater-fishing-2026";
        private const string Protocol = "nereid-submarine-expedition-v7";
        private CSteamID currentLobby;
        private CSteamID originalHost;
        private bool initialized;
        public bool IsInitialized => initialized;
        public bool OverlayActive {get;private set;}
        private Callback<GameOverlayActivated_t> overlayChanged;
        private int generation;
        private readonly List<IDisposable> requests = new List<IDisposable>();
        private Callback<GameLobbyJoinRequested_t> inviteRequested;
        private Callback<LobbyChatUpdate_t> membersChanged;
        private Callback<LobbyDataUpdate_t> dataChanged;
        public string LobbyId => currentLobby.IsValid() ? currentLobby.m_SteamID.ToString() : "";
        public bool ContainsMember(string address)
        {
            if(!initialized || !currentLobby.IsValid() || !ulong.TryParse(address,out ulong id)) return false;
            int count=SteamMatchmaking.GetNumLobbyMembers(currentLobby);
            for(int i=0;i<count;i++) if(SteamMatchmaking.GetLobbyMemberByIndex(currentLobby,i).m_SteamID==id) return true;
            return false;
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "+connect_lobby");
            if (index >= 0 && index + 1 < args.Length) Join(args[index + 1]);
        }

        private bool EnsureSteam()
        {
            if (initialized) return true;
            try
            {
                ESteamAPIInitResult result = SteamAPI.InitEx(out string error);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    session.SetStatus("Steam не запущен или недоступен: " + result + " / " + error);
                    return false;
                }
                initialized = true;
                overlayChanged=Callback<GameOverlayActivated_t>.Create(value=>OverlayActive=value.m_bActive!=0);
                inviteRequested = Callback<GameLobbyJoinRequested_t>.Create(OnInvite);
                membersChanged = Callback<LobbyChatUpdate_t>.Create(OnMembersChanged);
                dataChanged = Callback<LobbyDataUpdate_t>.Create(OnDataChanged);
                return true;
            }
            catch (Exception error)
            {
                session.SetStatus("Не удалось загрузить Steam API: " + error.Message);
                return false;
            }
        }
        public void Enable()
        {
            if(EnsureSteam()) session.SetStatus("Steam подключён. Можно принять приглашение или ввести Lobby ID.");
        }

        private void Update()
        {
            if (initialized) SteamAPI.RunCallbacks();
        }

        public void Create()
        {
            if (session.Busy || session.Connected || !EnsureSteam() || !session.SelectTransport(true)) return;
            int ticket = ++generation;
            session.SetStatus("Создание Steam-лобби для друзей…");
            CallResult<LobbyCreated_t> request = null;
            request = CallResult<LobbyCreated_t>.Create((result, failed) =>
            {
                requests.Remove(request);
                request.Dispose();
                if (ticket != generation)
                {
                    LeaveStale(result.m_ulSteamIDLobby);
                    return;
                }
                if (failed || result.m_eResult != EResult.k_EResultOK)
                {
                    Fail("Steam не создал лобби: " + result.m_eResult);
                    return;
                }
                currentLobby = new CSteamID(result.m_ulSteamIDLobby);
                originalHost = SteamUser.GetSteamID();
                if (!SetData("raid_game", Game) || !SetData("raid_protocol", Protocol) ||
                    !SetData("raid_host", originalHost.m_SteamID.ToString()) || !SetData("raid_ready", "0"))
                {
                    Fail("Не удалось записать параметры Steam-лобби.");
                    return;
                }
                session.StartSteamHost();
            });
            requests.Add(request);
            request.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 5));
        }

        public void Join(string id)
        {
            if (session.Busy || session.Connected) return;
            if (!ulong.TryParse(id, out ulong value) || !new CSteamID(value).IsLobby())
            {
                session.SetStatus("Введите корректный Steam Lobby ID, полученный от хоста.");
                return;
            }
            if (!EnsureSteam() || !session.SelectTransport(true)) return;
            int ticket = ++generation;
            session.SetStatus("Вход в Steam-лобби…");
            CallResult<LobbyEnter_t> request = null;
            request = CallResult<LobbyEnter_t>.Create((result, failed) =>
            {
                requests.Remove(request);
                request.Dispose();
                if (ticket != generation)
                {
                    LeaveStale(result.m_ulSteamIDLobby);
                    return;
                }
                if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    Fail("Вход в Steam-лобби отклонён: " + result.m_EChatRoomEnterResponse);
                    return;
                }
                currentLobby = new CSteamID(result.m_ulSteamIDLobby);
                if (ReadData("raid_game") != Game || ReadData("raid_protocol") != Protocol ||
                    !ulong.TryParse(ReadData("raid_host"), out ulong host) ||
                    SteamMatchmaking.GetLobbyOwner(currentLobby).m_SteamID != host)
                {
                    Fail("Лобби принадлежит другой игре или несовместимой версии RAID.");
                    return;
                }
                originalHost = new CSteamID(host);
                if (ReadData("raid_ready") != "1")
                {
                    Fail("Хост ещё не готов. Повторите подключение после его запуска.");
                    return;
                }
                session.StartSteamClient(host.ToString());
            });
            requests.Add(request);
            request.Set(SteamMatchmaking.JoinLobby(new CSteamID(value)));
        }

        public void PublishHost()
        {
            if (!initialized || !currentLobby.IsValid() || originalHost != SteamUser.GetSteamID()) return;
            if (!SetData("raid_ready", "1"))
            {
                Fail("Steam не подтвердил готовность лобби.");
                return;
            }
            session.StartSteamClient(originalHost.m_SteamID.ToString());
        }

        public void Invite()
        {
            if (!initialized || !currentLobby.IsValid()) return;
            if (!SteamUtils.IsOverlayEnabled())
            {
                session.SetStatus("Steam Overlay недоступен. Передайте другу Lobby ID: " + LobbyId);
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobby);
        }

        public void Leave()
        {
            ++generation;
            CSteamID oldLobby = currentLobby;
            currentLobby = default;
            originalHost = default;
            if (initialized && oldLobby.IsValid()) SteamMatchmaking.LeaveLobby(oldLobby);
            // Pending results remain registered so cancelled create/join requests can leave their late lobby.
        }

        private void LeaveStale(ulong id)
        {
            if (id != 0 && id != currentLobby.m_SteamID) SteamMatchmaking.LeaveLobby(new CSteamID(id));
        }

        private void OnInvite(GameLobbyJoinRequested_t invitation)
        {
            if (session.Busy || session.Connected)
            {
                session.SetStatus("Завершите текущую сессию перед входом по приглашению.");
                return;
            }
            Join(invitation.m_steamIDLobby.m_SteamID.ToString());
        }

        private void OnMembersChanged(LobbyChatUpdate_t change)
        {
            if (change.m_ulSteamIDLobby != currentLobby.m_SteamID || !currentLobby.IsValid()) return;
            uint departed = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                EChatMemberStateChange.k_EChatMemberStateChangeBanned);
            if ((change.m_rgfChatMemberStateChange & departed) != 0 &&
                (change.m_ulSteamIDUserChanged == originalHost.m_SteamID ||
                 change.m_ulSteamIDUserChanged == SteamUser.GetSteamID().m_SteamID))
                Fail("Хост или текущий игрок покинул Steam-лобби. Сессия завершена без миграции хоста.");
        }

        private void OnDataChanged(LobbyDataUpdate_t change)
        {
            if (!currentLobby.IsValid() || change.m_ulSteamIDLobby != currentLobby.m_SteamID || !originalHost.IsValid()) return;
            if (SteamMatchmaking.GetLobbyOwner(currentLobby) != originalHost)
                Fail("Steam-хост покинул лобби. Сессия завершена без миграции хоста.");
        }

        private string ReadData(string key) => SteamMatchmaking.GetLobbyData(currentLobby, key);
        private bool SetData(string key, string value) => SteamMatchmaking.SetLobbyData(currentLobby, key, value);

        private void Fail(string message)
        {
            session.Disconnect();
            session.SetStatus(message);
        }

        private void OnDestroy()
        {
            Leave();
            foreach (IDisposable request in requests) request.Dispose();
            requests.Clear();
            inviteRequested?.Dispose();
            membersChanged?.Dispose();
            dataChanged?.Dispose();
            overlayChanged?.Dispose();
            if (initialized) SteamAPI.Shutdown();
            initialized = false;
        }
    }
}
