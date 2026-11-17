using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using BaseTransport = FishNet.Transporting.Transport;

namespace UnderwaterFishing.Network.Steam
{
    /// <summary>
    /// Stable FishNet endpoint. Selects exactly one transport while offline, preserving
    /// the manager's event subscriptions and leaving unused Steam sockets uninitialized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoutingTransport : BaseTransport
    {
        public BaseTransport InitialTransport;
        public BaseTransport ActiveTransport { get; private set; }
        public bool IsStopped => _clientState == LocalConnectionState.Stopped && _serverState == LocalConnectionState.Stopped;

        private readonly HashSet<BaseTransport> _initialized = new();
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;
        private LocalConnectionState _serverState = LocalConnectionState.Stopped;

        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

        public override void Initialize(NetworkManager manager, int transportIndex)
        {
            base.Initialize(manager, transportIndex);
            if (!Select(InitialTransport))
                Debug.LogError("RoutingTransport requires an initial LAN transport.", this);
        }

        public bool Select(BaseTransport next)
        {
            if (next == null || next == this || next is RoutingTransport || !IsStopped || NetworkManager == null)
                return false;
            if (next == ActiveTransport) return true;
            if (!_initialized.Contains(next))
            {
                next.Initialize(NetworkManager, Index);
                _initialized.Add(next);
            }
            Unsubscribe();
            ActiveTransport = next;
            ActiveTransport.OnClientConnectionState += HandleClientConnectionState;
            ActiveTransport.OnServerConnectionState += HandleServerConnectionState;
            ActiveTransport.OnRemoteConnectionState += HandleRemoteConnectionState;
            ActiveTransport.OnClientReceivedData += HandleClientReceivedDataArgs;
            ActiveTransport.OnServerReceivedData += HandleServerReceivedDataArgs;
            ActiveTransport.SetMaximumClients(SteamLobbyService.MaximumPlayers);
            return true;
        }

        private void Unsubscribe()
        {
            if (ActiveTransport == null) return;
            ActiveTransport.OnClientConnectionState -= HandleClientConnectionState;
            ActiveTransport.OnServerConnectionState -= HandleServerConnectionState;
            ActiveTransport.OnRemoteConnectionState -= HandleRemoteConnectionState;
            ActiveTransport.OnClientReceivedData -= HandleClientReceivedDataArgs;
            ActiveTransport.OnServerReceivedData -= HandleServerReceivedDataArgs;
        }

        public override void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            _clientState = args.ConnectionState;
            OnClientConnectionState?.Invoke(args);
        }

        public override void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            _serverState = args.ConnectionState;
            OnServerConnectionState?.Invoke(args);
        }

        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs args) => OnRemoteConnectionState?.Invoke(args);
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs args) => OnClientReceivedData?.Invoke(args);
        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs args) => OnServerReceivedData?.Invoke(args);
        public override string GetConnectionAddress(int id) => ActiveTransport == null ? string.Empty : ActiveTransport.GetConnectionAddress(id);
        public override LocalConnectionState GetConnectionState(bool server) => server ? _serverState : _clientState;
        public override RemoteConnectionState GetConnectionState(int id) => ActiveTransport == null ? RemoteConnectionState.Stopped : ActiveTransport.GetConnectionState(id);
        public override void SendToServer(byte channel, ArraySegment<byte> segment) => ActiveTransport?.SendToServer(channel, segment);
        public override void SendToClient(byte channel, ArraySegment<byte> segment, int id) => ActiveTransport?.SendToClient(channel, segment, id);
        public override void IterateIncoming(bool server) => ActiveTransport?.IterateIncoming(server);
        public override void IterateOutgoing(bool server) => ActiveTransport?.IterateOutgoing(server);
        public override bool StartConnection(bool server) => ActiveTransport != null && ActiveTransport.StartConnection(server);
        public override bool StopConnection(bool server) => ActiveTransport != null && ActiveTransport.StopConnection(server);
        public override bool StopConnection(int id, bool immediately) => ActiveTransport != null && ActiveTransport.StopConnection(id, immediately);
        // FishNet caches MTUs during initialization: use a stable limit safe for both supported transports.
        public override int GetMTU(byte channel) => 1000;
        public override float GetPacketLoss(bool server) => ActiveTransport == null ? 0f : ActiveTransport.GetPacketLoss(server);
        public override bool IsLocalTransport(int id) => ActiveTransport != null && ActiveTransport.IsLocalTransport(id);
        public override float GetTimeout(bool server) => ActiveTransport == null ? -1f : ActiveTransport.GetTimeout(server);
        public override void SetTimeout(float value, bool server) => ActiveTransport?.SetTimeout(value, server);
        public override int GetMaximumClients() => SteamLobbyService.MaximumPlayers;
        public override void SetMaximumClients(int value) => ActiveTransport?.SetMaximumClients(Mathf.Clamp(value, 1, SteamLobbyService.MaximumPlayers));
        public override void SetClientAddress(string address) => ActiveTransport?.SetClientAddress(address);
        public override string GetClientAddress() => ActiveTransport == null ? string.Empty : ActiveTransport.GetClientAddress();
        public override void SetServerBindAddress(string address, IPAddressType addressType) => ActiveTransport?.SetServerBindAddress(address, addressType);
        public override string GetServerBindAddress(IPAddressType addressType) => ActiveTransport == null ? string.Empty : ActiveTransport.GetServerBindAddress(addressType);
        public override void SetPort(ushort port) => ActiveTransport?.SetPort(port);
        public override ushort GetPort() => ActiveTransport == null ? (ushort)0 : ActiveTransport.GetPort();

        public override void Shutdown()
        {
            foreach (BaseTransport transport in _initialized)
                if (transport != null) transport.Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
            Unsubscribe();
        }
    }
}
