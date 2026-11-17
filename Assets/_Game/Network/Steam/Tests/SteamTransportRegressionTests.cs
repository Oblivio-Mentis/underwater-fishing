using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using FishySteamworks;
using FishySteamworks.Client;
using FishySteamworks.Server;
using NUnit.Framework;
using Steamworks;
using UnityEngine;

namespace UnderwaterFishing.Network.Steam.Tests
{
    public sealed class SteamTransportRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner;
        private FishySteamworks.FishySteamworks transport;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Steam transport regression fixture");
            // No Initialize or StartConnection: no Steam API is called by the fixture.
            transport = owner.AddComponent<FishySteamworks.FishySteamworks>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(owner);

        [TestCase(0, 0, 4)]
        [TestCase(0, 1, 4)]
        [TestCase(0, 8, 4)]
        [TestCase(3, 0, 4)]
        [TestCase(3, 1, 4)]
        [TestCase(3, 8, 4)]
        [TestCase(0, 0, 0)]
        [TestCase(3, 0, 0)]
        public void ChannelTailPreservesPayloadAtEveryCapacityBoundary(int offset, int spare, int count)
        {
            var source = new byte[offset + count + spare];
            for (int i = 0; i < source.Length; i++) source[i] = 0xCD;
            for (int i = 0; i < count; i++) source[offset + i] = (byte)(10 + i);
            ArraySegment<byte> packet = PacketProbe.Append(new ArraySegment<byte>(source, offset, count), 1);
            Assert.That(packet.Count, Is.EqualTo(count + 1));
            for (int i = 0; i < count; i++) Assert.That(packet.Array[packet.Offset + i], Is.EqualTo(10 + i));
            Assert.That(packet.Array[packet.Offset + count], Is.EqualTo(1));
            for (int i = 0; i < offset; i++) Assert.That(source[i], Is.EqualTo(0xCD));
            if (spare > 0) Assert.That(packet.Array, Is.SameAs(source));
            else Assert.That(packet.Array, Is.Not.SameAs(source));
        }

        [Test]
        public void ReceiveRejectsEmptyOversizedNullAndUnknownChannelPackets()
        {
            Assert.That(PacketProbe.Decode(new byte[4], 0, out _, out _), Is.False);
            Assert.That(PacketProbe.Decode(new byte[4], -1, out _, out _), Is.False);
            Assert.That(PacketProbe.Decode(new byte[4], 5, out _, out _), Is.False);
            Assert.That(PacketProbe.Decode(null, 1, out _, out _), Is.False);
            Assert.That(PacketProbe.Decode(new byte[] { 10, 255 }, 2, out _, out _), Is.False);
        }

        [TestCase((byte)0)]
        [TestCase((byte)1)]
        public void ReceiveReturnsPayloadAndValidatedChannel(byte expectedChannel)
        {
            var bytes = new byte[] { 10, 20, expectedChannel };
            Assert.That(PacketProbe.Decode(bytes, bytes.Length, out var segment, out byte channel), Is.True);
            Assert.That(segment.Count, Is.EqualTo(2));
            Assert.That(segment.Array[segment.Offset], Is.EqualTo(10));
            Assert.That(segment.Array[segment.Offset + 1], Is.EqualTo(20));
            Assert.That(channel, Is.EqualTo(expectedChannel));
        }

        [Test]
        public void InvalidClientSocketStillDisposesStartupCallbackAndDeadline()
        {
            var socket = Client();
            var callback = new DisposalProbe();
            Set(socket, typeof(ClientSocket), "_onLocalConnectionStateCallback", callback);
            Set(socket, typeof(ClientSocket), "_connectTimeout", 100f);
            Invoke(socket, typeof(ClientSocket), "StopConnection");
            Assert.That(callback.DisposeCount, Is.EqualTo(1));
            Assert.That(Get(socket, typeof(ClientSocket), "_onLocalConnectionStateCallback"), Is.Null);
            Assert.That(Get(socket, typeof(ClientSocket), "_connectTimeout"), Is.EqualTo(-1f));
            Assert.That(socket.Closed, Is.Empty);
            Invoke(socket, typeof(ClientSocket), "StopConnection");
            Assert.That(callback.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ClientDeadlineStopsOnMainThreadAtExactDeadline()
        {
            var socket = Client();
            Set(socket, typeof(ClientSocket), "_socket", new HSteamNetConnection(11));
            Set(socket, typeof(ClientSocket), "_connectTimeout", 110f);
            SetState(socket, LocalConnectionState.Starting);
            var states = new List<LocalConnectionState>();
            transport.OnClientConnectionState += args => states.Add(args.ConnectionState);
            Assert.That(Invoke(socket, typeof(ClientSocket), "CheckTimeout", 109.9f), Is.False);
            Assert.That(Invoke(socket, typeof(ClientSocket), "CheckTimeout", 110f), Is.True);
            Assert.That(socket.Closed, Is.EqualTo(new[] { new HSteamNetConnection(11) }));
            Assert.That(states, Is.EqualTo(new[] { LocalConnectionState.Stopping, LocalConnectionState.Stopped }));
            Assert.That(Invoke(socket, typeof(ClientSocket), "CheckTimeout", 200f), Is.False);
        }

        [Test]
        public void ClientIgnoresAnotherSocketAndClearsDeadlineAfterOwnConnection()
        {
            var socket = Client();
            Set(socket, typeof(ClientSocket), "_socket", new HSteamNetConnection(11));
            Set(socket, typeof(ClientSocket), "_connectTimeout", 110f);
            SetState(socket, LocalConnectionState.Starting);
            var changed = new SteamNetConnectionStatusChangedCallback_t
            {
                m_hConn = new HSteamNetConnection(12),
                m_info = new SteamNetConnectionInfo_t { m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected }
            };
            Invoke(socket, typeof(ClientSocket), "OnLocalConnectionState", changed);
            Assert.That(State(socket), Is.EqualTo(LocalConnectionState.Starting));
            changed.m_hConn = new HSteamNetConnection(11);
            Invoke(socket, typeof(ClientSocket), "OnLocalConnectionState", changed);
            Assert.That(State(socket), Is.EqualTo(LocalConnectionState.Started));
            Assert.That(Get(socket, typeof(ClientSocket), "_connectTimeout"), Is.EqualTo(-1f));
        }

        [Test]
        public void InvalidServerListenerStillClosesRemotesAndClearsReusableState()
        {
            var socket = Server();
            var callback = new DisposalProbe();
            Set(socket, typeof(ServerSocket), "_onRemoteConnectionStateCallback", callback);
            Connections(socket).Add(new HSteamNetConnection(11), 0);
            Ids(socket).Add(new CSteamID(76561198000000001), 0);
            var pending = (List<ServerSocket.ConnectionChange>)Get(socket, typeof(ServerSocket), "_pendingConnectionChanges");
            pending.Add(new ServerSocket.ConnectionChange(1, new HSteamNetConnection(12), new CSteamID(76561198000000002)));
            ((Queue<int>)Get(socket, typeof(ServerSocket), "_cachedConnectionIds")).Enqueue(4);
            Set(socket, typeof(ServerSocket), "_nextConnectionId", 5);
            Set(socket, typeof(ServerSocket), "_clientHostStarted", true);
            byte[] queuedBuffer = EnqueueLocalPacket(socket);
            Invoke(socket, typeof(ServerSocket), "StopConnection");
            Assert.That(callback.DisposeCount, Is.EqualTo(1));
            Assert.That(Get(socket, typeof(ServerSocket), "_onRemoteConnectionStateCallback"), Is.Null);
            Assert.That(socket.ClosedConnections, Is.EquivalentTo(new[] { new HSteamNetConnection(11), new HSteamNetConnection(12) }));
            Assert.That(socket.ClosedListeners, Is.Empty);
            Assert.That(Connections(socket).Count, Is.Zero);
            Assert.That(Connections(socket).Second.Count, Is.Zero);
            Assert.That(Ids(socket).Count, Is.Zero);
            Assert.That(Ids(socket).Second.Count, Is.Zero);
            Assert.That(pending, Is.Empty);
            Assert.That((ICollection)Get(socket, typeof(ServerSocket), "_cachedConnectionIds"), Is.Empty);
            Assert.That((ICollection)Get(socket, typeof(ServerSocket), "_clientHostIncoming"), Is.Empty);
            Assert.That(Get(socket, typeof(ServerSocket), "_nextConnectionId"), Is.EqualTo(0));
            Assert.That(Get(socket, typeof(ServerSocket), "_clientHostStarted"), Is.False);
            Assert.That(PooledArrays(), Does.Contain(queuedBuffer));
        }

        [Test]
        public void ServerStopDuringEnumerationDefersClearingAndPublishesStopStates()
        {
            var socket = Server();
            Connections(socket).Add(new HSteamNetConnection(11), 0);
            Set(socket, typeof(ServerSocket), "_socket", new HSteamListenSocket(9));
            Set(socket, typeof(ServerSocket), "_iteratingConnections", true);
            SetState(socket, LocalConnectionState.Started);
            var states = new List<LocalConnectionState>();
            transport.OnServerConnectionState += args => states.Add(args.ConnectionState);
            using (var iterator = Connections(socket).First.GetEnumerator())
            {
                Assert.That(iterator.MoveNext(), Is.True);
                Invoke(socket, typeof(ServerSocket), "StopConnection");
                Assert.That(Connections(socket).Count, Is.EqualTo(1));
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(socket.ClosedConnections, Is.Empty);
            }
            Set(socket, typeof(ServerSocket), "_iteratingConnections", false);
            Invoke(socket, typeof(ServerSocket), "ProcessPendingConnectionChanges");
            Assert.That(Connections(socket).Count, Is.Zero);
            Assert.That(socket.ClosedConnections, Is.EqualTo(new[] { new HSteamNetConnection(11) }));
            Assert.That(socket.ClosedListeners, Is.EqualTo(new[] { new HSteamListenSocket(9) }));
            Assert.That(states, Is.EqualTo(new[] { LocalConnectionState.Stopping, LocalConnectionState.Stopped }));
        }

        [Test]
        public void LocalReceiveCanStopServerWithoutLosingPooledPacketOrEnumeratingClosedRemotes()
        {
            var socket = Server();
            Connections(socket).Add(new HSteamNetConnection(11), 0);
            SetState(socket, LocalConnectionState.Started);
            byte[] queuedBuffer = EnqueueLocalPacket(socket);
            transport.OnServerReceivedData += args => Invoke(socket, typeof(ServerSocket), "StopConnection");
            Invoke(socket, typeof(ServerSocket), "IterateIncoming");
            Assert.That(State(socket), Is.EqualTo(LocalConnectionState.Stopped));
            Assert.That(Connections(socket).Count, Is.Zero);
            Assert.That(socket.ClosedConnections, Is.EqualTo(new[] { new HSteamNetConnection(11) }));
            Assert.That(PooledArrays(), Does.Contain(queuedBuffer));
        }

        [Test]
        public void ServerIgnoresConnectionStatusFromAnotherListenSocket()
        {
            var socket = Server();
            Set(socket, typeof(ServerSocket), "_socket", new HSteamListenSocket(9));
            SetState(socket, LocalConnectionState.Started);
            var changed = new SteamNetConnectionStatusChangedCallback_t
            {
                m_hConn = new HSteamNetConnection(11),
                m_info = new SteamNetConnectionInfo_t
                {
                    m_hListenSocket = new HSteamListenSocket(10),
                    m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting
                }
            };
            // An unfiltered callback would attempt the real Steam AcceptConnection API here.
            Invoke(socket, typeof(ServerSocket), "OnRemoteConnectionState", changed);
            Assert.That(Connections(socket).Count, Is.Zero);
            Assert.That(socket.ClosedConnections, Is.Empty);
        }

        private ClientProbe Client()
        {
            var socket = new ClientProbe();
            Set(socket, typeof(CommonSocket), "Transport", transport);
            return socket;
        }

        private ServerProbe Server()
        {
            var socket = new ServerProbe();
            Set(socket, typeof(CommonSocket), "Transport", transport);
            return socket;
        }

        private static byte[] EnqueueLocalPacket(ServerSocket socket)
        {
            Type packetType = typeof(CommonSocket).Assembly.GetType("FishySteamworks.LocalPacket", true);
            object packet = Activator.CreateInstance(packetType, new object[] { new ArraySegment<byte>(new byte[] { 1, 2, 3 }), (byte)0 });
            object queue = Get(socket, typeof(ServerSocket), "_clientHostIncoming");
            queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { packet });
            return (byte[])packetType.GetField("Data").GetValue(packet);
        }

        private static Queue<byte[]> PooledArrays() => (Queue<byte[]>)typeof(ByteArrayPool).GetField("_byteArrays", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static BidirectionalDictionary<HSteamNetConnection, int> Connections(ServerSocket socket) => (BidirectionalDictionary<HSteamNetConnection, int>)Get(socket, typeof(ServerSocket), "_steamConnections");
        private static BidirectionalDictionary<CSteamID, int> Ids(ServerSocket socket) => (BidirectionalDictionary<CSteamID, int>)Get(socket, typeof(ServerSocket), "_steamIds");
        private static object Get(object target, Type type, string field) => type.GetField(field, PrivateInstance).GetValue(target);
        private static void Set(object target, Type type, string field, object value) => type.GetField(field, PrivateInstance).SetValue(target, value);
        private static void SetState(CommonSocket socket, LocalConnectionState state) => Set(socket, typeof(CommonSocket), "_connectionState", state);
        private static LocalConnectionState State(CommonSocket socket) => (LocalConnectionState)Get(socket, typeof(CommonSocket), "_connectionState");
        private static object Invoke(object target, Type type, string method, params object[] arguments)
        {
            foreach (MethodInfo candidate in type.GetMethods(PrivateInstance))
                if (candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
                    return candidate.Invoke(target, arguments);
            throw new MissingMethodException(type.FullName, method);
        }

        private sealed class DisposalProbe : IDisposable
        {
            public int DisposeCount;
            public void Dispose() => DisposeCount++;
        }

        private sealed class PacketProbe : CommonSocket
        {
            public static ArraySegment<byte> Append(ArraySegment<byte> data, byte channel) => AppendChannel(data, channel);
            public static bool Decode(byte[] buffer, int length, out ArraySegment<byte> payload, out byte channel) => TryDecodePacket(buffer, length, out payload, out channel);
        }

        private sealed class ClientProbe : ClientSocket
        {
            public readonly List<HSteamNetConnection> Closed = new();
            protected override void CloseConnectionSocket(HSteamNetConnection socket) => Closed.Add(socket);
        }

        private sealed class ServerProbe : ServerSocket
        {
            public readonly List<HSteamNetConnection> ClosedConnections = new();
            public readonly List<HSteamListenSocket> ClosedListeners = new();
            protected override void CloseConnectionSocket(HSteamNetConnection socket) => ClosedConnections.Add(socket);
            protected override void CloseListenSocket(HSteamListenSocket socket) => ClosedListeners.Add(socket);
        }
    }
}
