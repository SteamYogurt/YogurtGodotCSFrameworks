using System.Diagnostics;
using Xunit;

public sealed class LanTransportTests
{
    [Fact]
    public async Task HandshakePrecedesDeliveryAndBurstSurvivesPollBudget()
    {
        var received = new List<int>();
        ulong sender = 0;
        bool joined = false;
        var host = new LanTransport(_ => { }, 0);
        LanTransport client = null!;
        try
        {
            host.CreateRoom();
            client = new LanTransport(data =>
            {
                Assert.True(joined);
                Assert.Equal(client.HostID, client.CurrentSenderId);
                sender = client.CurrentSenderId;
                received.Add(BitConverter.ToInt32(data));
            }, host.ListeningPort);
            client.RoomJoined += () =>
            {
                Assert.NotEqual(0UL, client.LocalID);
                Assert.NotEqual(client.LocalID, client.HostID);
                joined = true;
            };
            host.NetPlayerListChanged += () =>
            {
                if (host.Players.Count == 2)
                    foreach (int number in Enumerable.Range(0, 240))
                        host.SendToAll(BitConverter.GetBytes(number));
            };

            client.JoinRoom("127.0.0.1");
            Assert.False(client.InRoom);
            await PumpUntil(() => host.Players.Count == 2, host);
            await Task.Delay(150);
            await PumpUntil(() => received.Count == 240, host, client);
            Assert.Equal(Enumerable.Range(0, 240), received);
            Assert.Equal(host.LocalID, sender);
            Assert.Equal(0UL, client.CurrentSenderId);

            var hostQuit = false;
            client.HostQuit += () => hostQuit = true;
            host.LeaveRoom();
            await PumpUntil(() => hostQuit, client);
            Assert.False(client.InRoom);
        }
        finally
        {
            client?.Free();
            host.Free();
        }
    }

    [Fact]
    public async Task KickRemovesPeerAndCanBlockRejoinUntilRoomCloses()
    {
        var host = new LanTransport(_ => { }, 0) { MaxPlayers = 2 };
        try
        {
            host.CreateRoom();
            var client = new LanTransport(_ => { }, host.ListeningPort);
            int kicked = 0;
            int hostQuit = 0;
            int joinFailures = 0;
            client.Kicked += () => kicked++;
            client.HostQuit += () => hostQuit++;
            client.RoomJoinFailed += () => joinFailures++;
            try
            {
                client.JoinRoom("127.0.0.1");
                await PumpUntil(() => client.InRoom, host, client);
                ulong firstId = client.LocalID;
                Assert.False(client.KickPeer(client.HostID, blockRejoin: true));
                Assert.False(host.KickPeer(host.LocalID, blockRejoin: true));
                Assert.True(host.KickPeer(firstId, blockRejoin: false));
                Assert.False(host.IsPeerConnected(firstId));
                Assert.Single(host.Players);
                await PumpUntil(() => kicked == 1, host, client);
                Assert.False(client.InRoom);

                client.JoinRoom("127.0.0.1");
                await PumpUntil(() => client.InRoom, host, client);
                ulong secondId = client.LocalID;
                Assert.NotEqual(firstId, secondId);
                Assert.True(host.KickPeer(secondId, blockRejoin: true));
                Assert.False(host.KickPeer(secondId, blockRejoin: true));
                await PumpUntil(() => kicked == 2, host, client);
                Assert.Equal(0, hostQuit);

                client.JoinRoom("127.0.0.1");
                await PumpUntil(() => joinFailures == 1, host, client);
                Assert.True(client.LastJoinWasBlocked);
                Assert.False(client.InRoom);
                Assert.Single(host.Players);
            }
            finally
            {
                client.Free();
            }

            host.LeaveRoom();
            host.CreateRoom();
            var nextClient = new LanTransport(_ => { }, host.ListeningPort);
            try
            {
                nextClient.JoinRoom("127.0.0.1");
                await PumpUntil(() => nextClient.InRoom, host, nextClient);
                Assert.False(nextClient.LastJoinWasBlocked);
            }
            finally
            {
                nextClient.Free();
            }
        }
        finally
        {
            host.Free();
        }
    }

    [Fact]
    public async Task RejectsFullOrClosedMatchesAndReusesFreedSlots()
    {
        NetRoomPresenceSource.Read = () => new NetRoomPresence { InMatch = true, MidJoinable = false };
        var host = new LanTransport(_ => { }, 0) { MaxPlayers = 2 };
        LanTransport first = null!;
        LanTransport second = null!;
        try
        {
            host.CreateRoom();
            first = new LanTransport(_ => { }, host.ListeningPort);
            second = new LanTransport(_ => { }, host.ListeningPort);
            int failures = 0;
            second.RoomJoinFailed += () => failures++;
            second.JoinRoom("127.0.0.1");
            await PumpUntil(() => failures == 1, host, second);
            Assert.Single(host.Players);

            NetRoomPresenceSource.Read = () => new NetRoomPresence { InMatch = true, MidJoinable = true };
            first.JoinRoom("127.0.0.1");
            await PumpUntil(() => first.InRoom, host, first);
            second.JoinRoom("127.0.0.1");
            await PumpUntil(() => failures == 2, host, first, second);
            Assert.Equal(2, host.Players.Count);

            ulong oldId = first.LocalID;
            first.LeaveRoom();
            await PumpUntil(() => host.Players.Count == 1, host);
            second.JoinRoom("127.0.0.1");
            await PumpUntil(() => second.InRoom, host, second);
            Assert.NotEqual(oldId, second.LocalID);
            Assert.Equal(2, host.Players.Count);
        }
        finally
        {
            first?.Free();
            second?.Free();
            host.Free();
            NetRoomPresenceSource.Read = null!;
        }
    }

    private static async Task PumpUntil(Func<bool> complete, params LanTransport[] transports)
    {
        var elapsed = Stopwatch.StartNew();
        while (!complete())
        {
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(4), "LAN operation timed out");
            foreach (var transport in transports)
                transport.Poll();
            await Task.Delay(5);
        }
    }
}
