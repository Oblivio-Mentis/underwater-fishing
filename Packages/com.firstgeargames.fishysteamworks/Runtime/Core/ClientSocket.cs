#if !FISHYSTEAMWORKS
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using Steamworks;
using System;
using UnityEngine;

namespace FishySteamworks.Client
{
    public class ClientSocket : CommonSocket
    {
        #region Private.
        /// <summary>
        /// Called when local connection state changes.
        /// </summary>
        private IDisposable _onLocalConnectionStateCallback;
        /// <summary>
        /// SteamId for host.
        /// </summary>
        private CSteamID _hostSteamID = CSteamID.Nil;
        /// <summary>
        /// Socket to use.
        /// </summary>
        private HSteamNetConnection _socket;
        /// <summary>
        /// When connect should timeout in unscaled time.
        /// </summary>
        private float _connectTimeout = -1f;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum time to wait before a timeout occurs when trying ot connect.
        /// </summary>
        private const float CONNECT_TIMEOUT_DURATION = 30f;
        #endregion

        /// <summary>
        /// Checks of a connect attempt should time out.
        /// </summary>
        private bool CheckTimeout(float now)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Starting || _connectTimeout < 0f || now < _connectTimeout)
                return false;
            StopConnection();
            return true;
        }

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal bool StartConnection(string address, ushort port, bool peerToPeer)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Stopped) return false;
            StopConnection();
            try
            {
                if (_onLocalConnectionStateCallback == null)
                    _onLocalConnectionStateCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnLocalConnectionState);

                base.PeerToPeer = peerToPeer;
                //If address is required then make sure it can be parsed.
                byte[] ip = (!peerToPeer) ? base.GetIPBytes(address) : null;
                if (!peerToPeer && ip == null)
                {
                    StopConnection();
                    return false;
                }

                base.SetLocalConnectionState(LocalConnectionState.Starting, false);
                _connectTimeout = Time.realtimeSinceStartup + CONNECT_TIMEOUT_DURATION;
                SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[] { };
                if (base.PeerToPeer)
                {
                    _hostSteamID = new CSteamID(UInt64.Parse(address));
                    SteamNetworkingIdentity smi = new SteamNetworkingIdentity();
                    smi.SetSteamID(_hostSteamID);
                    _socket = SteamNetworkingSockets.ConnectP2P(ref smi, 0, options.Length, options);
                }
                else
                {
                    SteamNetworkingIPAddr addr = new SteamNetworkingIPAddr();
                    addr.Clear();
                    addr.SetIPv6(ip, port);
                    _socket = SteamNetworkingSockets.ConnectByIPAddress(ref addr, 0, options);
                }
            }
            catch
            {
                StopConnection();
                return false;
            }
            if (_socket == HSteamNetConnection.Invalid)
            {
                StopConnection();
                return false;
            }
            return true;
        }


        /// <summary>
        /// Called when local connection state changes.
        /// </summary>
        private void OnLocalConnectionState(SteamNetConnectionStatusChangedCallback_t args)
        {
            if (_socket == HSteamNetConnection.Invalid || args.m_hConn != _socket) return;
            if (args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                _connectTimeout = -1f;
                base.SetLocalConnectionState(LocalConnectionState.Started, false);
            }
            else if (args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer || args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                base.Transport.NetworkManager.Log($"Connection was closed by peer, {args.m_info.m_szEndDebug}");
                StopConnection();
            }
            else
            {
                base.Transport.NetworkManager.Log($"Connection state changed: {args.m_info.m_eState.ToString()} - {args.m_info.m_szEndDebug}");
            }
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            bool wasRunning = base.GetLocalConnectionState() != LocalConnectionState.Stopped;
            // Clear ownership before callbacks can re-enter cleanup, including failed native starts.
            HSteamNetConnection socket = _socket;
            _socket = HSteamNetConnection.Invalid;
            _connectTimeout = -1f;
            _hostSteamID = CSteamID.Nil;
            IDisposable callback = _onLocalConnectionStateCallback;
            _onLocalConnectionStateCallback = null;
            callback?.Dispose();
            try
            {
                if (socket != HSteamNetConnection.Invalid) CloseConnectionSocket(socket);
            }
            finally
            {
                if (base.GetLocalConnectionState() != LocalConnectionState.Stopped && base.GetLocalConnectionState() != LocalConnectionState.Stopping)
                {
                    base.SetLocalConnectionState(LocalConnectionState.Stopping, false);
                    base.SetLocalConnectionState(LocalConnectionState.Stopped, false);
                }
            }
            return wasRunning;
        }

        protected virtual void CloseConnectionSocket(HSteamNetConnection socket) =>
            SteamNetworkingSockets.CloseConnection(socket, 0, string.Empty, false);

        /// <summary>
        /// Iterations data received.
        /// </summary>
        internal void IterateIncoming()
        {
            if (CheckTimeout(Time.realtimeSinceStartup)) return;
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            int messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(_socket, base.MessagePointers, MAX_MESSAGES);
            if (messageCount > 0)
            {
                for (int i = 0; i < messageCount; i++)
                {
                    if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                    {
                        SteamNetworkingMessage_t.Release(base.MessagePointers[i]);
                        continue;
                    }
                    if (!base.GetMessage(base.MessagePointers[i], InboundBuffer, out ArraySegment<byte> segment, out byte channel)) continue;
                    base.Transport.HandleClientReceivedDataArgs(new ClientReceivedDataArgs(segment, (Channel)channel, Transport.Index));
                }
            }
        }

        /// <summary>
        /// Queues data to be sent to server.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            EResult res = base.Send(_socket, segment, channelId);
            if (res == EResult.k_EResultNoConnection || res == EResult.k_EResultInvalidParam)
            {
                base.Transport.NetworkManager.Log($"Connection to server was lost.");
                StopConnection();
            }
            else if (res != EResult.k_EResultOK)
            {
                base.Transport.NetworkManager.LogError($"Could not send: {res.ToString()}");
            }
        }


        /// <summary>
        /// Sends queued data to server.
        /// </summary>
        internal void IterateOutgoing()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            SteamNetworkingSockets.FlushMessagesOnConnection(_socket);
        }

    }
}
#endif // !DISABLESTEAMWORKS
