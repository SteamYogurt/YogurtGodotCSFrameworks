#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class LanPlayerInfo
{
    public ulong PlayerId;
    public string Name = "";
    public bool IsHost;
}

public partial class LanTransport : INetTransport
{
    internal const int DefaultPort = 7777;
    private const ulong LanHostId = 1;
    private const ulong BlockedHandshakeId = ulong.MaxValue;
    private const ulong HandshakeVersion = ((ulong)NetWireLimits.ProtocolVersion << 48) | 0x4E41_4C54_454E;
    private const int JoinTimeoutMs = 5000;
    private const int HeartbeatIntervalMs = 2000;
    private const int IoTimeoutMs = 10000;
    private const int ReceiveBudget = 100;
    private const int ReceiveByteBudget = 2 * 1024 * 1024;
    private const int MaxPendingJoins = 32;
    private const int KickFrameLength = -1;
    private static readonly byte[] HeartbeatFrame = Frame(default);
    private static readonly byte[] KickFrame = Frame(default, KickFrameLength);

    private readonly Action<byte[]> receive;
    private readonly int port;
    private readonly Dictionary<ulong, LanPlayerInfo> players = new();
    private readonly Dictionary<ulong, Connection> peers = new();
    private readonly List<ulong> peerOrder = new();
    private readonly List<ulong> disconnected = new();
    private readonly HashSet<IPAddress> blockedAddresses = new();
    private readonly List<Connection> closingPeers = new();
    private readonly List<(Connection Peer, Task<bool> Hello)> pendingJoins = new();
    private TcpListener? listener;
    private CancellationTokenSource? acceptLifetime;
    private Task<TcpClient>? accepting;
    private Connection? client;
    private Task<ulong>? joining;
    private ulong nextPlayerId = LanHostId + 1;
    private int receiveCursor;
    private int roomVersion;
    private bool peerListDirty;

    public LanTransport(Action<byte[]> receive) : this(receive, DefaultPort) { }

    internal LanTransport(Action<byte[]> receive, int port)
    {
        this.receive = receive;
        this.port = port;
    }

    internal int ListeningPort => (listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;
    public int MaxPlayers { get; set; } = 4;
    public bool InRoom { get; private set; }
    public ulong LocalID { get; private set; }
    public ulong HostID { get; private set; }
    public ulong CurrentSenderId { get; private set; }
    public bool LastJoinWasBlocked { get; private set; }
    public IReadOnlyDictionary<ulong, LanPlayerInfo> Players => players;

    public event Action? NetPlayerListChanged;
    public event Action? RoomJoined;
    public event Action? RoomJoinFailed;
    public event Action? RoomStateChanged;
    public event Action? HostQuit;
    public event Action? Kicked;

    public void Init() => ResetState();
    public void Free() => ResetState();
    public bool AmIHost() => InRoom && LocalID == LanHostId;

    public void CreateRoom()
    {
        ResetState();
        LastJoinWasBlocked = false;
        listener = new TcpListener(IPAddress.Any, port);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            ResetState();
            RoomJoinFailed?.Invoke();
            return;
        }

        acceptLifetime = new CancellationTokenSource();
        accepting = listener.AcceptTcpClientAsync(acceptLifetime.Token).AsTask();
        LocalID = HostID = LanHostId;
        InRoom = true;
        AddPlayer(HostID, "Host");
        RoomJoined?.Invoke();
        if (AmIHost())
            RoomStateChanged?.Invoke();
    }

    public void JoinRoom(string roomId)
    {
        ResetState();
        LastJoinWasBlocked = false;
        if (string.IsNullOrWhiteSpace(roomId))
        {
            RoomJoinFailed?.Invoke();
            return;
        }

        client = new Connection(new TcpClient());
        joining = client.ConnectAndReadId(roomId, port);
    }

    public void LeaveRoom()
    {
        ResetState();
        RoomStateChanged?.Invoke();
    }

    private void ResetState()
    {
        roomVersion++;
        acceptLifetime?.Cancel();
        acceptLifetime?.Dispose();
        acceptLifetime = null;
        listener?.Stop();
        listener = null;
        if (accepting != null)
            _ = DisposeAcceptedSocket(accepting);
        accepting = null;

        client?.Close();
        client = null;
        joining = null;
        foreach (var peer in peers.Values)
            peer.Close();
        peers.Clear();
        peerOrder.Clear();
        foreach (var peer in closingPeers)
            peer.Close();
        closingPeers.Clear();
        blockedAddresses.Clear();
        peerListDirty = false;
        foreach (var pending in pendingJoins)
            pending.Peer.Close();
        pendingJoins.Clear();
        players.Clear();
        nextPlayerId = LanHostId + 1;
        receiveCursor = 0;
        CurrentSenderId = LocalID = HostID = 0;
        InRoom = false;
    }

    private static async Task DisposeAcceptedSocket(Task<TcpClient> pending)
    {
        try
        {
            (await pending.ConfigureAwait(false)).Dispose();
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException or ObjectDisposedException) { }
    }

    public void Send(byte[] data, ulong peerId) => Send(data.AsSpan(), peerId);

    public void Send(ReadOnlySpan<byte> data, ulong peerId)
    {
        if (!InRoom || !IsValidPacket(data))
            return;

        Connection? target;
        if (AmIHost())
            peers.TryGetValue(peerId, out target);
        else
            target = peerId == HostID ? client : null;

        if (target != null)
            target.Send(Frame(data));
    }

    public void SendToAll(byte[] data, ulong excludePeerId = 0) =>
        SendToAll(data.AsSpan(), excludePeerId);

    public void SendToAll(ReadOnlySpan<byte> data, ulong excludePeerId = 0)
    {
        if (!AmIHost())
        {
            Send(data, HostID);
            return;
        }

        SendToSelected(data, null, excludePeerId);
    }

    public bool IsPeerConnected(ulong peerId) =>
        AmIHost() && peers.TryGetValue(peerId, out var peer) && !peer.IsClosed;

    public bool KickPeer(ulong peerId, bool blockRejoin)
    {
        if (!IsPeerConnected(peerId) || !peers.Remove(peerId, out var peer))
            return false;

        if (blockRejoin && peer.RemoteAddress is { } address)
            blockedAddresses.Add(address);
        peerOrder.Remove(peerId);
        players.Remove(peerId);
        peerListDirty = true;
        peer.SendAndClose(KickFrame);
        closingPeers.Add(peer);
        return true;
    }

    public void SendToSelected(ReadOnlySpan<byte> data, Predicate<ulong>? include, ulong excludePeerId = 0)
    {
        if (!AmIHost() || !IsValidPacket(data))
            return;
        byte[]? frame = null;
        foreach (var entry in peers)
            if (entry.Key != excludePeerId && (include == null || include(entry.Key)))
                entry.Value.Send(frame ??= Frame(data));
    }

    private static bool IsValidPacket(ReadOnlySpan<byte> data) =>
        data.Length > 0 && data.Length <= NetWireLimits.MaxPacketBytes;

    public void Poll()
    {
        if (joining is { IsCompleted: true } completed && client is { } pendingClient)
            CompleteJoin(completed, pendingClient);
        if (!InRoom)
            return;

        long now = Environment.TickCount64;
        if (AmIHost())
            PollHost(now);
        else if (client is { } connectedClient)
            PollClient(now, connectedClient);
    }

    private void CompleteJoin(Task<ulong> completed, Connection pendingClient)
    {
        ulong peerId = completed.GetAwaiter().GetResult();
        joining = null;
        if (peerId is 0 or BlockedHandshakeId)
        {
            LastJoinWasBlocked = peerId == BlockedHandshakeId;
            ResetState();
            RoomJoinFailed?.Invoke();
            return;
        }

        LocalID = peerId;
        HostID = LanHostId;
        InRoom = true;
        AddPlayer(HostID, "Host");
        AddPlayer(LocalID, "Me");
        pendingClient.Start(acceptKick: true);
        RoomJoined?.Invoke();
        if (InRoom && LocalID == peerId)
            RoomStateChanged?.Invoke();
    }

    private void PollHost(long now)
    {
        int version = roomVersion;
        closingPeers.RemoveAll(peer => peer.IsClosed);
        disconnected.Clear();
        foreach (var entry in peers)
        {
            if (entry.Value.IsClosed)
                disconnected.Add(entry.Key);
            else
                entry.Value.SendHeartbeatIfIdle(now);
        }
        foreach (ulong peerId in disconnected)
        {
            peers.Remove(peerId);
            peerOrder.Remove(peerId);
            players.Remove(peerId);
        }
        // Apply departures before admissions so a newly freed slot is immediately usable.
        if (peerListDirty || disconnected.Count > 0)
        {
            peerListDirty = false;
            NotifyPlayersChanged();
        }
        if (version != roomVersion)
            return;

        PollAccept();
        if (version != roomVersion)
            return;

        int bytes = 0;
        int emptyPeers = 0;
        for (int count = 0; count < ReceiveBudget && bytes < ReceiveByteBudget && emptyPeers < peerOrder.Count;)
        {
            if (receiveCursor >= peerOrder.Count)
                receiveCursor = 0;
            ulong peerId = peerOrder[receiveCursor++];
            var peer = peers[peerId];
            if (peer.IsClosed || !peer.TryReceive(out var data))
            {
                emptyPeers++;
                continue;
            }
            emptyPeers = 0;
            bytes += data.Length;
            count++;
            Dispatch(peerId, data);
            if (version != roomVersion)
                return;
        }
    }

    private void PollAccept()
    {
        if (listener is not { } activeListener || acceptLifetime is not { } lifetime)
            return;

        for (int i = 0; i < MaxPendingJoins && accepting is { IsCompleted: true } accepted; i++)
        {
            TcpClient? tcp = null;
            try
            {
                tcp = accepted.GetAwaiter().GetResult();
            }
            catch (SocketException) { }
            accepting = activeListener.AcceptTcpClientAsync(lifetime.Token).AsTask();
            if (tcp == null)
                continue;

            if (pendingJoins.Count + closingPeers.Count >= MaxPendingJoins)
            {
                tcp.Dispose();
                continue;
            }

            var connection = new Connection(tcp);
            pendingJoins.Add((connection, connection.ReadHello()));
        }

        bool changed = false;
        for (int i = 0; i < pendingJoins.Count;)
        {
            var (connection, hello) = pendingJoins[i];
            if (!hello.IsCompleted)
            {
                i++;
                continue;
            }
            pendingJoins.RemoveAt(i);
            if (!hello.GetAwaiter().GetResult())
            {
                connection.Close();
                continue;
            }

            bool blocked = connection.RemoteAddress is { } address && blockedAddresses.Contains(address);
            if (blocked || !CanAcceptPeer())
            {
                connection.Start();
                connection.SendAndClose(BuildHandshake(blocked ? BlockedHandshakeId : 0));
                closingPeers.Add(connection);
                continue;
            }

            ulong peerId = nextPlayerId++;
            connection.Send(BuildHandshake(peerId));
            connection.Start();
            peers.Add(peerId, connection);
            peerOrder.Add(peerId);
            AddPlayer(peerId, $"Client{peerId}");
            changed = true;
        }

        if (changed)
            NotifyPlayersChanged();
    }

    private bool CanAcceptPeer() => players.Count < Math.Clamp(MaxPlayers, 1, 250)
        && NetRoomPresenceSource.Read?.Invoke()?.AllowsJoining != false;

    private static byte[] BuildHandshake(ulong peerId)
    {
        var handshake = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(handshake, HandshakeVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(handshake.AsSpan(8), peerId);
        return handshake;
    }

    private void PollClient(long now, Connection connectedClient)
    {
        int version = roomVersion;
        int bytes = 0;
        connectedClient.SendHeartbeatIfIdle(now);
        for (int i = 0; i < ReceiveBudget && bytes < ReceiveByteBudget; i++)
        {
            if (!connectedClient.TryReceive(out var data))
            {
                if (connectedClient.WasKicked || connectedClient.IsClosed)
                {
                    bool kicked = connectedClient.WasKicked;
                    ResetState();
                    int resetVersion = roomVersion;
                    if (kicked)
                        Kicked?.Invoke();
                    else
                        HostQuit?.Invoke();
                    if (resetVersion == roomVersion)
                        RoomStateChanged?.Invoke();
                }
                return;
            }

            bytes += data.Length;
            Dispatch(HostID, data);
            if (version != roomVersion)
                return;
        }
    }

    private void Dispatch(ulong peerId, byte[] data)
    {
        CurrentSenderId = peerId;
        try
        {
            receive(data);
        }
        finally
        {
            CurrentSenderId = 0;
        }
    }

    private void AddPlayer(ulong peerId, string name) =>
        players.Add(peerId, new LanPlayerInfo { PlayerId = peerId, Name = name, IsHost = peerId == LanHostId });

    private void NotifyPlayersChanged()
    {
        int version = roomVersion;
        NetPlayerListChanged?.Invoke();
        if (version == roomVersion)
            RoomStateChanged?.Invoke();
    }

    public List<INetTransportPlayerInfo> GetTempNetPlayerInfos()
    {
        var list = new List<INetTransportPlayerInfo>(players.Count);
        foreach (var player in players.Values)
            list.Add(new INetTransportPlayerInfo { id = player.PlayerId, name = player.Name });
        return list;
    }
}
