#if !FISHYSTEAMWORKS
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishySteamworks
{

    public abstract class CommonSocket
    {

        #region Public.
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private LocalConnectionState _connectionState = LocalConnectionState.Stopped;
        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal LocalConnectionState GetLocalConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected virtual void SetLocalConnectionState(LocalConnectionState connectionState, bool server)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            _connectionState = connectionState;

            if (server)
                Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState, Transport.Index));
            else
                Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState, Transport.Index));
        }
        #endregion

        #region Protected.
        /// <summary>
        /// True if using PeerToPeer.
        /// </summary>
        protected bool PeerToPeer = false;
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected Transport Transport = null;
        /// <summary>
        /// Pointers for received messages per connection.
        /// </summary>
        protected IntPtr[] MessagePointers = new IntPtr[MAX_MESSAGES];
        /// <summary>
        /// Buffer used to receive data.
        /// </summary>
        protected byte[] InboundBuffer = null;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum number of messages which can be received per connection.
        /// </summary>
        protected const int MAX_MESSAGES = 256;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal virtual void Initialize(Transport t)
        {
            Transport = t;
            //Get whichever channel has max MTU and resize buffer.
            int maxMTU = Transport.GetMTU(0);
            maxMTU = Math.Max(maxMTU, Transport.GetMTU(1));
            InboundBuffer = new byte[maxMTU];
        }

        /// <summary>
        /// Gets bytes for address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected byte[] GetIPBytes(string address)
        {
            //If address is required then make sure it can be parsed.
            if (!string.IsNullOrEmpty(address))
            {
                if (!IPAddress.TryParse(address, out IPAddress result))
                {
                    Transport.NetworkManager.LogError($"Could not parse address {address} to IPAddress.");
                    return null;
                }
                else
                {
                    return result.GetAddressBytes();
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Sends data over the steamConnection.
        /// </summary>
        /// <param name="steamConnection"></param>
        /// <param name="segment"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        protected EResult Send(HSteamNetConnection steamConnection, ArraySegment<byte> segment, byte channelId)
        {
            segment = AppendChannel(segment, channelId);
            GCHandle pinnedArray = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            try
            {
                IntPtr pData = pinnedArray.AddrOfPinnedObject() + segment.Offset;
                int sendFlag = (channelId == (byte)Channel.Unreliable) ? Constants.k_nSteamNetworkingSend_Unreliable : Constants.k_nSteamNetworkingSend_Reliable;
#if UNITY_SERVER
                EResult result = SteamGameServerNetworkingSockets.SendMessageToConnection(steamConnection, pData, (uint)segment.Count, sendFlag, out long _);
#else
                EResult result = SteamNetworkingSockets.SendMessageToConnection(steamConnection, pData, (uint)segment.Count, sendFlag, out long _);
#endif
                if (result != EResult.k_EResultOK)
                    Transport.NetworkManager.LogWarning($"Send issue: {result}");
                return result;
            }
            finally { pinnedArray.Free(); }
        }

        /// <summary>Appends the wire channel without assuming spare capacity in a pooled buffer.</summary>
        protected static ArraySegment<byte> AppendChannel(ArraySegment<byte> segment, byte channelId)
        {
            if (segment.Array == null) throw new ArgumentException("A packet requires a backing array.", nameof(segment));
            int length = checked(segment.Count + 1);
            byte[] data = segment.Array;
            int offset = segment.Offset;
            if (data.Length - offset < length)
            {
                data = new byte[length];
                Buffer.BlockCopy(segment.Array, offset, data, 0, segment.Count);
                offset = 0;
            }
            data[offset + segment.Count] = channelId;
            return new ArraySegment<byte>(data, offset, length);
        }

        /// <summary>
        /// Clears a queue.
        /// </summary>
        /// <param name="queue"></param>
        internal void ClearQueue(ConcurrentQueue<LocalPacket> queue)
        {
            while (queue.TryDequeue(out LocalPacket p))
                ByteArrayPool.Store(p.Data);
        }
        /// <summary>
        /// Clears a queue.
        /// </summary>
        /// <param name="queue"></param>
        internal void ClearQueue(Queue<LocalPacket> queue)
        {
            while (queue.Count > 0)
            {
                LocalPacket p = queue.Dequeue();
                ByteArrayPool.Store(p.Data);
            }
        }

        /// <summary>
        /// Returns a message from the steam network.
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected bool GetMessage(IntPtr ptr, byte[] buffer, out ArraySegment<byte> segment, out byte channel)
        {
            segment = default;
            channel = 0;
            if (ptr == IntPtr.Zero) return false;
            try
            {
                SteamNetworkingMessage_t data = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptr);
                if (!IsPacketLengthValid(data.m_cbSize, buffer) || data.m_pData == IntPtr.Zero) return false;
                Marshal.Copy(data.m_pData, buffer, 0, data.m_cbSize);
                return TryDecodePacket(buffer, data.m_cbSize, out segment, out channel);
            }
            finally { SteamNetworkingMessage_t.Release(ptr); }
        }

        private static bool IsPacketLengthValid(int length, byte[] buffer) => buffer != null && length > 0 && length <= buffer.Length;

        protected static bool TryDecodePacket(byte[] buffer, int length, out ArraySegment<byte> segment, out byte channel)
        {
            segment = default;
            channel = 0;
            if (!IsPacketLengthValid(length, buffer)) return false;
            byte receivedChannel = buffer[length - 1];
            if (receivedChannel != (byte)Channel.Reliable && receivedChannel != (byte)Channel.Unreliable) return false;
            channel = receivedChannel;
            segment = new ArraySegment<byte>(buffer, 0, length - 1);
            return true;
        }
    }

}
#endif
