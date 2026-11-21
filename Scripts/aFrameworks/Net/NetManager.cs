using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Text;

public partial class NetManager : Singleton<NetManager>
{
    #region enum
    public enum NetMsgType : byte
    {
        Event = 1,
        EventWithSender = 2,
        Action = 101,
        SpawnObj = 102,
        StateUpdate = 103,
        DestroyObj = 104,
        Customize = 105,
        InitialPacket = 106,
        Input = 107,
        Request = 108,
        RPC = 109,
        MatchReset = 110,
        Ping = 254,
        Pong = 255
    }

    /// <summary>点对点事件在包内 flags 字节上的标记（与 NetSendFlags 位不重叠）。</summary>
    private const byte WirePeerTargetMarker = NetRouting.PeerTargetMarker;
    #endregion

    #region fields
    private readonly Dictionary<uint, INetObject> idToObject = new();
    private readonly Dictionary<INetObject, uint> objectToId = new();
    private readonly Dictionary<uint, INetObject> lazyIdToObject = new();
    private readonly NetSpawnDestroyGuard spawnDestroyGuard = new();
    public NetworkIdGenerator idGenerator = new NetworkIdGenerator();

    public TransportManager transportManager;
    public bool active = false;

    private int pingIntervalTick = 0;
    public long AvgRTT { get; private set; }
    public bool HasRttSample { get; private set; }

    public Action<string> EventCb = (_) => { };
    public Action<string, ulong> EventWithSenderCb = (_, __) => { };

    private int sessionVersion;
    private int accum = 0;
    private readonly List<INetObject> invalidObjects = new();
    private readonly ArrayBufferWriter<byte> sessionBatch = new();
    private readonly ArrayBufferWriter<byte> matchBatch = new();
    private readonly ArrayBufferWriter<byte> inputBatch = new();
    private static readonly Encoding Utf8 = Encoding.UTF8;
    #endregion

    #region lifecycle
    public void Start()
    {
        if (active)
            Deactivate();
        sessionVersion++;
        active = true;
        transportManager = TransportManager.Instance;
        idGenerator = new NetworkIdGenerator();
        pingIntervalTick = accum = 0;
        AvgRTT = 0;
        HasRttSample = false;
    }

    public void Deactivate()
    {
        sessionVersion++;
        active = false;
        matchAccess.Clear();
        var uninitialized = lazyIdToObject.Count == 0 ? null : new List<INetObject>(lazyIdToObject.Values);
        lazyIdToObject.Clear();
        idToObject.Clear();
        objectToId.Clear();
        spawnDestroyGuard.Clear();
        invalidObjects.Clear();
        sessionBatch.Clear();
        matchBatch.Clear();
        inputBatch.Clear();
        if (uninitialized != null)
            foreach (var obj in uninitialized)
                ReleaseRemoteObject(obj);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!active || Transport?.InRoom != true) return;

        UpdatePing(delta);
        accum++;

        invalidObjects.Clear();
        foreach (var obj in idToObject.Values)
        {
            if (obj.IsNetInvalid())
            {
                invalidObjects.Add(obj);
            }
        }
        foreach (var obj in invalidObjects)
        {
            if (IsHost)
                HostDestroyNetObject(obj);
            else
                RemoveNetObject(obj);
        }

        if (accum <= 4)
            return;

        accum = 0;
        if (IsHost)
            SendStateTick();
        else
            SendInputTick();
    }
    #endregion

    #region routing helpers
    private INetTransport Transport => transportManager?.Current;
    private bool IsHost => Transport != null && Transport.AmIHost();
    private ulong LocalId => Transport?.LocalID ?? 0;
    private ulong HostId => Transport?.HostID ?? 0;

    /// <summary>收包端：本机是否属于 flags 描述的网上接收者。</summary>
    private bool ShouldDeliverOnReceive(NetSendFlags flags)
        => NetRouting.ShouldDeliverOnReceive(IsHost, flags);

    /// <summary>
    /// 发送端本地投递，与 flags 完全正交：
    /// alsoRunLocally=true → 调用方跑一次；false → 调用方不跑。
    /// </summary>
    private static void DeliverOnSendIfNeeded(bool alsoRunLocally, Action deliver)
    {
        if (NetRouting.ShouldRunLocallyOnSend(alsoRunLocally))
            deliver();
    }

    private static byte[] PackBody(ReadOnlySpan<byte> body)
    {
        if (body.Length > NetWireLimits.MaxPacketBytes - sizeof(int))
        {
            GD.PrintErr($"[NetManager] 消息超出 {NetWireLimits.MaxPacketBytes} 字节的传输上限");
            return null;
        }
        byte[] packet = new byte[body.Length + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), (uint)body.Length);
        body.CopyTo(packet.AsSpan(4));
        return packet;
    }

    private void SendPackedToPeer(ReadOnlySpan<byte> body, ulong peerId)
    {
        if (Transport == null || peerId == 0) return;
        var packet = PackBody(body);
        if (packet != null)
            Transport.Send(packet, peerId);
    }

    /// <summary>
    /// 出站物理路径：客机永远只发给主机；主机按 Clients 位广播（可排除一人）。
    /// 不含本地投递。
    /// </summary>
    private void SendOutbound(ReadOnlySpan<byte> body, NetSendFlags flags, INetObject target = null)
    {
        if (Transport == null) return;

        if (!IsHost)
        {
            SendPackedToPeer(body, HostId);
            return;
        }

        if (NetRouting.ShouldHostOutboundToClients(flags) && PackBody(body) is { } packet)
        {
            if (target == null)
                Transport.SendToAll(packet);
            else
                HostSendFramedPacketForObject(target, packet);
        }
    }

    private void RelayPacket(NetMsgType type, ReadOnlySpan<byte> content, byte flags, ulong targetPeerId,
        int version, INetObject target = null)
    {
        if (version != sessionVersion || !active || !IsHost || (target != null && GetID(target) == 0))
            return;
        ulong origin = Transport.CurrentSenderId;
        if (NetRouting.IsPeerTarget(flags))
        {
            if (targetPeerId == LocalId || targetPeerId == origin || !Transport.IsPeerConnected(targetPeerId)
                || (target != null && !CanPeerAccessObject(targetPeerId, target)))
                return;
        }
        else if (!NetRouting.ShouldHostForwardToClients((NetSendFlags)flags))
        {
            return;
        }

        var packet = new byte[5 + content.Length];
        BinaryPrimitives.WriteInt32LittleEndian(packet, 1 + content.Length);
        packet[4] = (byte)type;
        content.CopyTo(packet.AsSpan(5));
        if (NetRouting.IsPeerTarget(flags))
            Transport.Send(packet, targetPeerId);
        else if (target == null)
            Transport.SendToAll(packet, origin);
        else
            HostSendFramedPacketForObject(target, packet, origin);
    }
    #endregion

    #region ProcessIncoming
    public void ProcessIncoming(byte[] bytes) => ProcessIncoming(bytes.AsSpan());

    public void ProcessIncoming(ReadOnlySpan<byte> bytes)
    {
        var transport = Transport;
        if (!active || transport?.InRoom != true || !NetWireFrames.IsPacket(bytes))
            return;
        bool isHost = transport.AmIHost();
        ulong sender = transport.CurrentSenderId;
        if (sender == 0 || sender == transport.LocalID
            || (isHost ? !transport.IsPeerConnected(sender) : sender != transport.HostID))
            return;

        int version = sessionVersion;
        while (active && version == sessionVersion && ReferenceEquals(transport, Transport)
            && transport.InRoom && sender == transport.CurrentSenderId
            && NetWireFrames.TryReadField(ref bytes, out var body))
        {
            var type = (NetMsgType)body[0];
            var content = body[1..];
            try
            {
                switch (type)
                {
                    case NetMsgType.Event: ReadEvent(content); break;
                    case NetMsgType.EventWithSender: ReadEventWithSender(content); break;
                    case NetMsgType.SpawnObj when !isHost: ReadSpawnObj(content); break;
                    case NetMsgType.InitialPacket when !isHost: ReadObjInitialPacket(content); break;
                    case NetMsgType.StateUpdate when !isHost: ReadObjStateData(content); break;
                    case NetMsgType.DestroyObj when !isHost: ReadDestroyObj(content); break;
                    case NetMsgType.MatchReset when !isHost: ReadMatchReset(content); break;
                    case NetMsgType.Customize: ReadCustomize(content); break;
                    case NetMsgType.RPC: ReadRPC(content); break;
                    case NetMsgType.Input when isHost: ReadInputData(content, sender); break;
                    case NetMsgType.Ping when isHost: ReadPing(content); break;
                    case NetMsgType.Pong when !isHost: ReadPong(content); break;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[NetManager] {type} 处理失败: {e}");
            }
        }
    }
    #endregion

    #region Ping
    private void UpdatePing(double delta)
    {
        pingIntervalTick++;
        if (pingIntervalTick > 120)
        {
            pingIntervalTick = 0;
            if (!IsHost) SendPing();
        }
    }

    private void SendPing()
    {
        byte[] buf = new byte[1 + 8];
        buf[0] = (byte)NetMsgType.Ping;
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(1, 8), (long)Time.GetTicksMsec());
        SendPackedToPeer(buf, HostId);
    }

    private void ReadPing(ReadOnlySpan<byte> content)
    {
        if (!IsHost || Transport == null || content.Length != sizeof(long)) return;

        ulong replyTo = Transport.CurrentSenderId;
        if (replyTo == 0) return;

        byte[] pong = new byte[1 + content.Length];
        pong[0] = (byte)NetMsgType.Pong;
        content.CopyTo(pong.AsSpan(1));
        SendPackedToPeer(pong, replyTo);
    }

    private void ReadPong(ReadOnlySpan<byte> content)
    {
        if (content.Length != sizeof(long)) return;

        long sendTime = BinaryPrimitives.ReadInt64LittleEndian(content);
        long rtt = (long)Time.GetTicksMsec() - sendTime;
        if (rtt < 0) rtt = 0;

        HasRttSample = true;
        AvgRTT = AvgRTT == 0 ? rtt : (long)(AvgRTT * 0.7f + rtt * 0.3f);
    }
    #endregion

    #region utils
    private static Variant ReadVariant(ReadOnlySpan<byte> data) => GD.BytesToVar(data.ToArray());
    public uint GetID(INetObject netObject) => objectToId.TryGetValue(netObject, out var id) ? id : 0;
    public INetObject GetNetObject(uint id) => idToObject.TryGetValue(id, out var obj) ? obj : null;
    public void RemoveNetObject(INetObject obj)
    {
        if (!objectToId.TryGetValue(obj, out var id)) return;
        idGenerator.ReleaseId(id);
        objectToId.Remove(obj);
        idToObject.Remove(id);
    }
    #endregion

    #region Debug
    public void DebugDump()
    {
        GD.Print("========== NetManager DEBUG DUMP ==========");

        GD.Print($"Active: {active}");
        GD.Print($"Transport Null: {transportManager == null}");
        GD.Print($"Transport Current Null: {transportManager?.Current == null}");

        if (transportManager?.Current != null)
        {
            GD.Print($"Am I Host: {transportManager.Current.AmIHost()}");
            GD.Print($"LocalID: {transportManager.Current.LocalID}");
        }

        GD.Print($"AvgRTT: {AvgRTT}");
        GD.Print($"PingIntervalTick: {pingIntervalTick}");
        GD.Print($"Accum Tick: {accum}");

        GD.Print("----- Net Object Maps -----");

        GD.Print($"idToObject Count: {idToObject.Count}");
        foreach (var kv in idToObject)
        {
            string objName = kv.Value?.Info?.ObjectName ?? "NULL";
            GD.Print($"  ID: {kv.Key} -> Obj: {objName}  Authority: {kv.Value?.HasAuthority()}");
        }

        GD.Print($"objectToId Count: {objectToId.Count}");
        foreach (var kv in objectToId)
        {
            string objName = kv.Key?.Info?.ObjectName ?? "NULL";
            GD.Print($"  Obj: {objName} -> ID: {kv.Value}");
        }

        GD.Print("----- Lazy Objects (Spawned but not Initialized) -----");
        GD.Print($"lazyIdToObject Count: {lazyIdToObject.Count}");
        foreach (var kv in lazyIdToObject)
        {
            string objName = kv.Value?.Info?.ObjectName ?? "NULL";
            GD.Print($"  Lazy ID: {kv.Key} -> Obj: {objName}");
        }

        GD.Print("----- ID Generator State -----");
        GD.Print($"Next ID Candidate: {_GetNextIdPreview()}");

        GD.Print("==============================================");
    }
    private uint _GetNextIdPreview()
    {
        return idGenerator?.PeekNextId() ?? 0;
    }
    #endregion
}
