using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Buffers.Binary;
public class LanPlayerInfo
{
    public ulong PlayerId;
    public string Name;
    public bool IsHost;
}

public partial class LanTransport : INetTransport
{
    public bool InRoom { get; private set; }
    public ulong LocalID { get; private set; }
    public ulong HostID { get; private set; }

    private readonly Dictionary<ulong, LanPlayerInfo> players = new();
    public IReadOnlyDictionary<ulong, LanPlayerInfo> Players => players;

    public event Action NetPlayerListChanged;
    public event Action RoomStateChanged;
    public event Action HostQuit;

    private TcpListener listener;
    private TcpClient client;

    private volatile bool running;
    private Thread acceptThread;

    private ulong nextPlayerId = 2;
    private bool isHost => LocalID == HostID;

    private const int PORT = 7777;

    // 接收队列
    private readonly ConcurrentQueue<(ulong senderId, byte[] data)> recvQueue = new();
    private readonly ConcurrentQueue<ulong> disconnectedQueue = new();

    private volatile bool playerListDirty;

    private const int HEARTBEAT_INTERVAL_MS = 2000;
    private const int HEARTBEAT_TIMEOUT_MS = 6000;

    // 用于在 Poll 循环中记录当前正在处理的数据包来源 ID，以便 Send 方法处理 OthersExceptSender
    private ulong _currentProcessingSenderId = 0;

    private enum PacketType : byte
    {
        Normal = 0,
        Heartbeat = 1
    }

    private class ConnectionState
    {
        public ulong PeerId;
        public TcpClient Tcp;
        public NetworkStream Stream;
        public Thread Thread;
        public DateTime LastRecvTime;
        public DateTime LastHeartbeatSendTime;

        // 写锁：确保同一时间只有一个线程向该 Stream 写入
        public readonly object WriteLock = new object();
    }

    // Host：所有客户端连接
    private readonly Dictionary<ulong, ConnectionState> hostConnections = new();
    private readonly object hostConnLock = new();

    // Client：客户端对Host的写锁
    private readonly object clientWriteLock = new object();

    #region INetTransport Implementation

    public void Init() => LeaveRoom();
    public void Free() => LeaveRoom();

    public void CreateRoom()
    {
        LeaveRoom();

        LocalID = 1;
        HostID = 1;
        InRoom = true;

        players[1] = new LanPlayerInfo
        {
            PlayerId = 1,
            Name = "Host",
            IsHost = true
        };

        try
        {
            listener = new TcpListener(IPAddress.Any, PORT);
            listener.Start();

            running = true;
            acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            acceptThread.Start();

            playerListDirty = true;
            RoomStateChanged?.Invoke();
            GD.Print("Room Created. LocalID: " + LocalID);
        }
        catch (Exception e)
        {
            GD.PrintErr("Create Room Failed: " + e.Message);
            LeaveRoom();
        }
    }

    public void JoinRoom(string roomId)
    {
        LeaveRoom();

        try
        {
            // 在 LAN 模式下，roomId 被视为 IP 地址
            client = new TcpClient();
            // 增加超时控制
            var result = client.BeginConnect(roomId, PORT, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));

            if (!success)
            {
                client.Close();
                throw new Exception("Connection timeout");
            }

            client.EndConnect(result);

            running = true;
            var t = new Thread(ClientReceiveLoop) { IsBackground = true };
            t.Start();

            InRoom = true;
            RoomStateChanged?.Invoke();
            GD.Print("Joined Room.");
        }
        catch (Exception e)
        {
            GD.PrintErr("Join Failed: " + e.Message);
            LeaveRoom();
        }
    }

    public void LeaveRoom()
    {
        running = false;

        try { listener?.Stop(); } catch { }
        listener = null;

        if (acceptThread != null && acceptThread.IsAlive)
            acceptThread.Join(100);
        acceptThread = null;

        lock (hostConnLock)
        {
            foreach (var c in hostConnections.Values)
            {
                try { c.Tcp?.Close(); } catch { }
            }
            hostConnections.Clear();
        }

        try { client?.Close(); } catch { }
        client = null;

        players.Clear();
        while (recvQueue.TryDequeue(out _)) { }
        while (disconnectedQueue.TryDequeue(out _)) { }

        LocalID = 0;
        HostID = 0;
        InRoom = false;
        _currentProcessingSenderId = 0;

        playerListDirty = true;
        RoomStateChanged?.Invoke();
    }

    public bool AmIHost() => LocalID == HostID;

    /// <summary>
    /// 发送数据。
    /// 根据 SendType 自动处理目标。
    /// SenderID 始终为 LocalID (直接发送者)。
    /// </summary>
    public void Send(byte[] data, SendType type)
    {
        Send(data.AsSpan(), type);
    }

    public void Send(ReadOnlySpan<byte> data, SendType type)
    {
        if (!InRoom) return;

        if (isHost)
        {
            lock (hostConnLock)
            {
                foreach (var kv in hostConnections)
                {
                    ulong targetId = kv.Key;

                    // 过滤器逻辑：
                    if (type == SendType.Host) continue; // Host 不需要通过网络发给自己

                    // AllOthers: 发给所有人（除了自己，因为自己是Host，不在 hostConnections 里，所以不需要额外判断）

                    // OthersExceptSender: 发给除了"当前处理消息的来源"以外的人
                    // 这里的 _currentProcessingSenderId 是在 Poll() 中设置的
                    if (type == SendType.OthersExceptSender && targetId == _currentProcessingSenderId) continue;

                    // JustSender: 只发给"当前处理消息的来源"
                    if (type == SendType.JustSender && targetId != _currentProcessingSenderId) continue;

                    var conn = kv.Value;
                    lock (conn.WriteLock)
                    {
                        // 始终使用 LocalID 作为包头的 Sender，符合"只记录直接发包的人"的要求
                        SendFrame(conn.Stream, PacketType.Normal, LocalID, data);
                    }
                }
            }
        }
        else
        {
            // 客户端逻辑
            // 客户端只能发给 Host。
            // 对于客户端来说，SendType 通常用于告诉 Host 怎么转发，但 TCP 层只能发给 Host。
            // 具体的转发逻辑通常需要包含在 data 数据包内部（由 NetManager 处理），
            // 或者 Transport 层默认全部发给 Host，由 Host 根据上下文决定。

            // 这里我们简单处理：只要不是发给 JustSender 且 Sender 是自己（那没意义），就发给 Host
            if (client != null && client.Connected)
            {
                lock (clientWriteLock)
                {
                    SendFrame(client.GetStream(), PacketType.Normal, LocalID, data);
                }
            }
        }
    }

    public void Send(byte[] data, ulong targetId)
    {
        Send(data.AsSpan(), targetId);
    }

    public void Send(ReadOnlySpan<byte> data, ulong targetId)
    {
        if (!InRoom) return;

        if (isHost)
        {
            lock (hostConnLock)
            {
                if (hostConnections.TryGetValue(targetId, out var conn))
                {
                    lock (conn.WriteLock)
                    {
                        SendFrame(conn.Stream, PacketType.Normal, LocalID, data);
                    }
                }
            }
        }
        else if (targetId == HostID)
        {
            if (client != null && client.Connected)
            {
                lock (clientWriteLock)
                {
                    SendFrame(client.GetStream(), PacketType.Normal, LocalID, data);
                }
            }
        }
    }

    public void Poll()
    {
        // 处理最多 50 个包防止卡死主线程
        int count = 0;
        while (recvQueue.TryDequeue(out var packet) && count < 100)
        {
            count++;

            // 设置当前上下文 SenderID
            // 如果 NetManager 在 AnalyseStream 内部调用了 Send(..., OthersExceptSender)
            // Send 方法会使用这个 ID 进行过滤
            _currentProcessingSenderId = packet.senderId;

            // 调用上层逻辑 (不做任何签名修改)
            NetManager.Instance.AnalyseStream(packet.data);
        }

        // 处理完后重置，避免后续逻辑误用
        _currentProcessingSenderId = 0;

        while (disconnectedQueue.TryDequeue(out var id))
        {
            // 关键逻辑：如果掉线的人是 Host，且我不是 Host
            if (!isHost && id == HostID)
            {
                GD.Print("[LanTransport] Host connection lost.");
                HostQuit?.Invoke();
                // 注意：房主没了，这个房间也就没意义了
                LeaveRoom();
                return; // 房主退了，直接结束本次 Poll
            }

            if (players.Remove(id))
            {
                GD.Print($"Player {id} disconnected");
                playerListDirty = true;
            }
        }

        if (playerListDirty)
        {
            playerListDirty = false;
            NetPlayerListChanged?.Invoke();
            RoomStateChanged?.Invoke();
        }
    }

    public List<INetTransportPlayerInfo> GetTempNetPlayerInfos()
    {
        if (!InRoom) return new List<INetTransportPlayerInfo>();

        var list = new List<INetTransportPlayerInfo>();
        foreach (var p in players.Values)
        {
            list.Add(new INetTransportPlayerInfo
            {
                id = p.PlayerId,
                name = p.Name
            });
        }
        return list;
    }

    #endregion

    #region Host Threads

    private void AcceptLoop()
    {
        while (running && listener != null)
        {
            try
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(10);
                    continue;
                }

                var tcp = listener.AcceptTcpClient();
                ulong id = nextPlayerId++;

                // 发送握手包 (同步发送，还没加入 hostConnections，暂时不需要锁)
                SendHandshake(tcp, id, HostID);

                var conn = new ConnectionState
                {
                    PeerId = id,
                    Tcp = tcp,
                    Stream = tcp.GetStream(),
                    LastRecvTime = DateTime.UtcNow,
                    LastHeartbeatSendTime = DateTime.UtcNow
                };

                conn.Thread = new Thread(() => HostClientLoop(conn))
                {
                    IsBackground = true
                };

                lock (hostConnLock)
                    hostConnections[id] = conn;

                players[id] = new LanPlayerInfo
                {
                    PlayerId = id,
                    Name = "Client" + id,
                    IsHost = false
                };

                playerListDirty = true;
                conn.Thread.Start();
                GD.Print($"Client {id} connected.");
            }
            catch (Exception ex)
            {
                if (running) GD.PrintErr("Accept Error: " + ex.Message);
            }
        }
    }

    private void HostClientLoop(ConnectionState conn)
    {
        try
        {
            while (running && conn.Tcp != null && conn.Tcp.Connected)
            {
                bool hasData = false;
                try
                {
                    hasData = conn.Stream.DataAvailable || conn.Tcp.Client.Poll(1000, SelectMode.SelectRead);
                }
                catch
                {
                    break;
                }

                if (hasData)
                {
                    if (!ReadFrame(conn.Stream, out var type, out var sender, out var payload))
                        break;

                    conn.LastRecvTime = DateTime.UtcNow;

                    if (type == PacketType.Normal)
                    {
                        recvQueue.Enqueue((conn.PeerId, payload));
                    }
                }

                var now = DateTime.UtcNow;

                if ((now - conn.LastHeartbeatSendTime).TotalMilliseconds > HEARTBEAT_INTERVAL_MS)
                {
                    lock (conn.WriteLock)
                    {
                        SendFrame(conn.Stream, PacketType.Heartbeat, LocalID, Array.Empty<byte>());
                    }
                    conn.LastHeartbeatSendTime = now;
                }

                if ((now - conn.LastRecvTime).TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
                {
                    GD.Print($"Client {conn.PeerId} timed out.");
                    break;
                }

                Thread.Sleep(1);
            }
        }
        catch (Exception e)
        {
            if (running) GD.PrintErr($"Client Loop Error {conn.PeerId}: {e.Message}");
        }
        finally
        {
            try { conn.Tcp?.Close(); } catch { }
            lock (hostConnLock)
                hostConnections.Remove(conn.PeerId);
            disconnectedQueue.Enqueue(conn.PeerId);
        }
    }
    #endregion

    #region Client Thread

    private void ClientReceiveLoop()
    {
        bool hostDisconnected = false;

        try
        {
            var stream = client.GetStream();
            var buf = ReadExact(stream, 16);
            if (buf == null) throw new Exception("Handshake failed");

            LocalID = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0, 8));
            HostID = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(8, 8));

            players[HostID] = new LanPlayerInfo
            {
                PlayerId = HostID,
                Name = "Host",
                IsHost = true
            };

            players[LocalID] = new LanPlayerInfo
            {
                PlayerId = LocalID,
                Name = "Me",
                IsHost = false
            };

            playerListDirty = true;
            GD.Print($"Handshake success. My ID: {LocalID}");

            DateTime lastRecv = DateTime.UtcNow;
            DateTime lastHb = DateTime.UtcNow;

            while (running && client != null && client.Connected)
            {
                bool hasData = false;
                try
                {
                    hasData = stream.DataAvailable || client.Client.Poll(1000, SelectMode.SelectRead);
                }
                catch
                {
                    if (running) hostDisconnected = true;
                    break;
                }

                if (hasData)
                {
                    if (!ReadFrame(stream, out var type, out var sender, out var payload))
                    {
                        if (running) hostDisconnected = true;
                        break;
                    }

                    lastRecv = DateTime.UtcNow;

                    if (type == PacketType.Normal)
                        recvQueue.Enqueue((sender, payload));
                }

                var now = DateTime.UtcNow;

                if ((now - lastHb).TotalMilliseconds > HEARTBEAT_INTERVAL_MS)
                {
                    lock (clientWriteLock)
                    {
                        SendFrame(stream, PacketType.Heartbeat, LocalID, Array.Empty<byte>());
                    }
                    lastHb = now;
                }

                if ((now - lastRecv).TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
                {
                    GD.Print("Host timed out.");
                    hostDisconnected = true;
                    break;
                }

                Thread.Sleep(1);
            }
        }
        catch (Exception e)
        {
            if (running)
            {
                hostDisconnected = true;
                GD.PrintErr("Client Loop Error: " + e.Message);
            }
        }
        finally
        {
            if (hostDisconnected && running)
            {
                disconnectedQueue.Enqueue(HostID);
            }

            running = false;
        }
    }
    #endregion

    #region Packet

    private void SendHandshake(TcpClient tcp, ulong id, ulong hostId)
    {
        try
        {
            var stream = tcp.GetStream();
            byte[] buf = new byte[16];
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0, 8), id);
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(8, 8), hostId);
            stream.Write(buf, 0, 16);
        }
        catch { }
    }

    private void SendFrame(NetworkStream stream, PacketType type, ulong senderId, ReadOnlySpan<byte> payload)
    {
        try
        {
            int totalLen = 1 + 8 + 4 + payload.Length;
            byte[] fullPacket = new byte[totalLen];

            fullPacket[0] = (byte)type;
            BinaryPrimitives.WriteUInt64LittleEndian(fullPacket.AsSpan(1, 8), senderId);
            BinaryPrimitives.WriteInt32LittleEndian(fullPacket.AsSpan(9, 4), payload.Length);
            if (!payload.IsEmpty)
                payload.CopyTo(fullPacket.AsSpan(13));

            stream.Write(fullPacket, 0, totalLen);
        }
        catch (Exception)
        {
            // 发送失败通常意味着连接断开
        }
    }

    private bool ReadFrame(NetworkStream stream, out PacketType type, out ulong sender, out byte[] payload)
    {
        type = 0;
        sender = 0;
        payload = null;

        try
        {
            int t = stream.ReadByte();
            if (t < 0) return false;
            type = (PacketType)t;

            var sid = ReadExact(stream, 8);
            var lenBuf = ReadExact(stream, 4);
            if (sid == null || lenBuf == null) return false;

            sender = BinaryPrimitives.ReadUInt64LittleEndian(sid);
            int len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);

            if (len < 0 || len > 1024 * 1024 * 10) // 10MB Limit
                return false;

            payload = len > 0 ? ReadExact(stream, len) : Array.Empty<byte>();
            return payload != null;
        }
        catch
        {
            return false;
        }
    }

    private byte[] ReadExact(NetworkStream stream, int len)
    {
        byte[] buf = new byte[len];
        int read = 0;
        while (read < len)
        {
            int r = stream.Read(buf, read, len - read);
            if (r <= 0) return null;
            read += r;
        }
        return buf;
    }

    #endregion
}