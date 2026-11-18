using System;
using FishNet.Authenticating;
using FishNet.Connection;
namespace Raid
{
    public sealed class LobbyAuthenticator : Authenticator
    {
        public RaidSession session;
        public SteamLobby lobby;
        public override event Action<NetworkConnection,bool> OnAuthenticationResult;
        public override void OnRemoteConnection(NetworkConnection connection)
        {
            bool allowed=session!=null && (!session.SteamMode || (lobby!=null && lobby.ContainsMember(connection.GetAddress())));
            OnAuthenticationResult?.Invoke(connection,allowed);
        }
    }
}
