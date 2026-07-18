using Godot;
using System;
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
        Ping = 254,
        Pong = 255
    }

    /// <summary>点对点事件在包内 flags 字节上的标记（与 NetSendFlags 位不重叠）。</summary>
    private const byte WirePeerTargetMarker = NetRouting.PeerTargetMarker;
    #endregion

    #region fields
    private Dictionary<uint, INetObject> idToObject = new();
    private Dictionary<INetObject, uint> objectToId = new();
    private Dictionary<uint, INetObject> lazyIdToObject = new();
    private readonly NetSpawnDestroyGuard spawnDestroyGuard = new();
    public NetworkIdGenerator idGenerator = new NetworkIdGenerator();

    public TransportManager transportManager;
    public bool active = false;

    private int pingIntervalTick = 0;
    public long AvgRTT { get; private set; }

    public Action<string> EventCb = (_) => { };
    public Action<string, ulong> EventWithSenderCb = (_, __) => { };

    private ArraySegment<byte> forwardedContent = default;
    private NetMsgType forwardedHead;
    private int accum = 0;
    private static readonly Encoding Utf8 = Encoding.UTF8;
    #endregion

    #region lifecycle
    public void Start()
    {
        active = true;
        transportManager = TransportManager.Instance;
        idToObject.Clear();
        objectToId.Clear();
        lazyIdToObject.Clear();
        spawnDestroyGuard.Clear();
        _receiveBuffer.Clear();
        idGenerator = new NetworkIdGenerator();
        AvgRTT = 0;
    }

    public void Deactivate()
    {
        active = false;
        _receiveBuffer.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!active || transportManager?.Current == null) return;

        UpdatePing(delta);
        accum++;

        var toRemove = new List<INetObject>();
        foreach (var obj in idToObject.Values)
        {
            if (obj.IsNetInvalid())
            {
                toRemove.Add(obj);
            }
        }
        foreach (var obj in toRemove)
        {
            RemoveNetObject(obj);
        }

        if (transportManager.Current.AmIHost())
        {
            if (accum > 4)
            {
                accum = 0;
                foreach (var obj in idToObject.Values)
                {
                    HostSendStateData(obj);
                }
            }
        }
        else
        {
            if (accum > 4)
            {
                accum = 0;
                foreach (var obj in idToObject.Values)
                {
                    if (obj.HasAuthority())
                        SendInputData(obj);
                }
            }
        }
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

    private byte[] PackBody(ReadOnlySpan<byte> body)
    {
        byte[] packet = new byte[body.Length + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), (uint)body.Length);
        body.CopyTo(packet.AsSpan(4));
        return packet;
    }

    private void SendPackedToPeer(ReadOnlySpan<byte> body, ulong peerId)
    {
        if (Transport == null || peerId == 0) return;
        Transport.Send(PackBody(body), peerId);
    }

    private void SendPackedToAllClients(ReadOnlySpan<byte> body, ulong excludePeerId = 0)
    {
        if (Transport == null) return;
        Transport.SendToAll(PackBody(body), excludePeerId);
    }

    /// <summary>
    /// 出站物理路径：客机永远只发给主机；主机按 Clients 位广播（可排除一人）。
    /// 不含本地投递。
    /// </summary>
    private void SendOutbound(ReadOnlySpan<byte> body, NetSendFlags flags, ulong excludePeerId = 0)
    {
        if (Transport == null) return;

        if (!IsHost)
        {
            SendPackedToPeer(body, HostId);
            return;
        }

        if (NetRouting.ShouldHostOutboundToClients(flags))
            SendPackedToAllClients(body, excludePeerId);
    }

    /// <summary>主机把当前正在处理的消息转发给其他客机（排除原发送者，避免回声）。</summary>
    private void ForwardToClientsExcludingOrigin()
    {
        if (!IsHost || Transport == null || forwardedContent.Array == null) return;

        int bodyLen = 1 + forwardedContent.Count;
        byte[] packet = new byte[4 + bodyLen];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), (uint)bodyLen);
        packet[4] = (byte)forwardedHead;
        forwardedContent.AsSpan().CopyTo(packet.AsSpan(5));

        ulong exclude = Transport.CurrentSenderId;
        Transport.SendToAll(packet, exclude);
    }

    private void HostForwardClientsIfNeeded(NetSendFlags flags)
    {
        if (!IsHost) return;
        if (!NetRouting.ShouldHostForwardToClients(flags)) return;
        ForwardToClientsExcludingOrigin();
    }
    #endregion

    #region ProcessIncoming
    private readonly List<byte> _receiveBuffer = new();

    public void ProcessIncoming(byte[] bytes) => ProcessIncoming(bytes.AsSpan());

    public void ProcessIncoming(ReadOnlySpan<byte> bytes)
    {
        if (!active) return;
        for (int i = 0; i < bytes.Length; i++)
            _receiveBuffer.Add(bytes[i]);

        while (_receiveBuffer.Count >= 4)
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_receiveBuffer).Slice(0, 4));
            if (length == 0)
            {
                GD.PrintErr("ProcessIncoming: 收到长度为 0 的包，丢弃长度头");
                _receiveBuffer.RemoveRange(0, 4);
                continue;
            }
            if (_receiveBuffer.Count < 4 + length) break;

            byte[] packet = _receiveBuffer.GetRange(4, (int)length).ToArray();
            NetMsgType currentHead = (NetMsgType)packet[0];
            var currentContent = new ArraySegment<byte>(packet, 1, Math.Max(packet.Length - 1, 0));

            forwardedHead = currentHead;
            forwardedContent = currentContent;
            _receiveBuffer.RemoveRange(0, 4 + (int)length);

            ReadOnlySpan<byte> contentSpan = currentContent.AsSpan();
            switch (currentHead)
            {
                case NetMsgType.Event: ReadEvent(contentSpan); break;
                case NetMsgType.EventWithSender: ReadEventWithSender(contentSpan); break;
                case NetMsgType.SpawnObj: ReadSpawnObj(contentSpan); break;
                case NetMsgType.InitialPacket: ReadObjInitialPacket(contentSpan); break;
                case NetMsgType.StateUpdate: ReadObjStateData(contentSpan); break;
                case NetMsgType.Customize: ReadCustomize(contentSpan); break;
                case NetMsgType.Input: ReadInputData(contentSpan); break;
                case NetMsgType.RPC: ReadRPC(contentSpan); break;
                case NetMsgType.Ping: ReadPing(contentSpan); break;
                case NetMsgType.Pong: ReadPong(contentSpan); break;
                case NetMsgType.DestroyObj: ReadDestroyObj(contentSpan); break;
            }
        }
    }
    #endregion

    #region String Events
    /// <param name="alsoRunLocally">与 flags 正交：true 则调用方本地执行一次；false 则调用方不执行。</param>
    public void SendEvent(string evt, NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        if (Transport == null || flags == NetSendFlags.None) return;

        byte[] evtBytes = Utf8.GetBytes(evt);
        byte[] buffer = new byte[2 + 4 + evtBytes.Length];
        buffer[0] = (byte)NetMsgType.Event;
        buffer[1] = (byte)flags;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(2, 4), evtBytes.Length);
        evtBytes.CopyTo(buffer.AsSpan(6));

        DeliverOnSendIfNeeded(alsoRunLocally, () => EventCb?.Invoke(evt));
        SendOutbound(buffer, flags);
    }

    public void SendEventWithSender(string evt, NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        if (Transport == null || flags == NetSendFlags.None) return;

        byte[] evtBytes = Utf8.GetBytes(evt);
        byte[] buffer = new byte[2 + 8 + 4 + evtBytes.Length];
        buffer[0] = (byte)NetMsgType.EventWithSender;
        buffer[1] = (byte)flags;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(2, 8), LocalId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(10, 4), evtBytes.Length);
        evtBytes.CopyTo(buffer.AsSpan(14));

        DeliverOnSendIfNeeded(alsoRunLocally, () => EventWithSenderCb?.Invoke(evt, LocalId));
        SendOutbound(buffer, flags);
    }

    public void SendEventToPeer(ulong targetPeerId, string evt)
    {
        if (Transport == null || targetPeerId == 0) return;

        if (targetPeerId == LocalId)
        {
            EventCb?.Invoke(evt);
            return;
        }

        byte[] evtBytes = Utf8.GetBytes(evt);
        byte[] buffer = new byte[1 + 1 + 8 + 4 + evtBytes.Length];
        buffer[0] = (byte)NetMsgType.Event;
        buffer[1] = WirePeerTargetMarker;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(2, 8), targetPeerId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(10, 4), evtBytes.Length);
        evtBytes.CopyTo(buffer.AsSpan(14));

        if (!IsHost)
            SendPackedToPeer(buffer, HostId);
        else
            Transport.Send(PackBody(buffer), targetPeerId);
    }

    [Obsolete("Use SendEventToPeer")]
    public void SendEventToPlayer(ulong targetPeerId, string evt) => SendEventToPeer(targetPeerId, evt);

    private void ReadEvent(ReadOnlySpan<byte> content)
    {
        byte flagsByte = content[0];

        if (NetRouting.IsPeerTarget(flagsByte))
        {
            ulong targetId = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(1, 8));
            int strLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(9, 4));
            string evt = Utf8.GetString(content.Slice(13, strLen));

            if (LocalId == targetId)
                EventCb?.Invoke(evt);

            if (IsHost && targetId != LocalId)
            {
                byte[] body = new byte[1 + content.Length];
                body[0] = (byte)NetMsgType.Event;
                content.CopyTo(body.AsSpan(1));
                Transport.Send(PackBody(body), targetId);
            }
            return;
        }

        var flags = (NetSendFlags)flagsByte;
        int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(1, 4));
        string eventName = Utf8.GetString(content.Slice(5, len));

        if (ShouldDeliverOnReceive(flags))
            EventCb?.Invoke(eventName);

        HostForwardClientsIfNeeded(flags);
    }

    private void ReadEventWithSender(ReadOnlySpan<byte> content)
    {
        var flags = (NetSendFlags)content[0];
        ulong senderId = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(1, 8));
        int strLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(9, 4));
        string evt = Utf8.GetString(content.Slice(13, strLen));

        if (ShouldDeliverOnReceive(flags))
            EventWithSenderCb?.Invoke(evt, senderId);

        HostForwardClientsIfNeeded(flags);
    }
    #endregion

    #region Sync / Spawn / Destroy / InitialPacket
    public void SyncAllNetObjectsToPlayer(ulong peerId)
    {
        if (!IsHost) return;

        Main.Print($"[Net Mgr] 正在向新玩家 {peerId} 同步所有存活对象...");

        foreach (var kvp in idToObject)
        {
            uint id = kvp.Key;
            INetObject obj = kvp.Value;
            byte[] fullPacket = PackSpawnMessage(obj, id);
            Transport.Send(fullPacket, peerId);
        }
    }

    public void HostSpawnNetObject(INetObject netObject)
    {
        if (!IsHost) return;
        uint id = idGenerator.GetNextId();
        idToObject[id] = netObject;
        objectToId[netObject] = id;

        byte[] fullPacket = PackSpawnMessage(netObject, id);
        Transport.SendToAll(fullPacket);
    }

    private byte[] PackSpawnMessage(INetObject obj, uint id)
    {
        byte[] nameBytes = Utf8.GetBytes(obj.Info.ObjectName);
        int spawnBodyLen = 1 + 4 + 4 + nameBytes.Length;

        var fullVars = obj.GetFullStateVars();
        List<byte[]> varDatas = new();
        int varsContentSize = 0;
        if (fullVars != null)
        {
            foreach (var v in fullVars)
            {
                byte[] d = GD.VarToBytes(v.Value);
                varDatas.Add(d);
                varsContentSize += 4 + d.Length;
            }
        }
        int initBodyLen = 1 + 4 + 2 + varsContentSize;

        byte[] finalResult = new byte[(4 + spawnBodyLen) + (4 + initBodyLen)];
        int cur = 0;

        BinaryPrimitives.WriteUInt32LittleEndian(finalResult.AsSpan(cur, 4), (uint)spawnBodyLen); cur += 4;
        finalResult[cur++] = (byte)NetMsgType.SpawnObj;
        BinaryPrimitives.WriteUInt32LittleEndian(finalResult.AsSpan(cur, 4), id); cur += 4;
        BinaryPrimitives.WriteInt32LittleEndian(finalResult.AsSpan(cur, 4), nameBytes.Length); cur += 4;
        nameBytes.CopyTo(finalResult.AsSpan(cur)); cur += nameBytes.Length;

        BinaryPrimitives.WriteUInt32LittleEndian(finalResult.AsSpan(cur, 4), (uint)initBodyLen); cur += 4;
        finalResult[cur++] = (byte)NetMsgType.InitialPacket;
        BinaryPrimitives.WriteUInt32LittleEndian(finalResult.AsSpan(cur, 4), id); cur += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(finalResult.AsSpan(cur, 2), (ushort)varDatas.Count); cur += 2;
        foreach (var d in varDatas)
        {
            BinaryPrimitives.WriteInt32LittleEndian(finalResult.AsSpan(cur, 4), d.Length); cur += 4;
            d.CopyTo(finalResult.AsSpan(cur)); cur += d.Length;
        }

        return finalResult;
    }

    private void ReadSpawnObj(ReadOnlySpan<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        bool already = idToObject.ContainsKey(id) || lazyIdToObject.ContainsKey(id);

        switch (spawnDestroyGuard.DecideSpawn(id, already))
        {
            case NetSpawnDestroyGuard.SpawnDecision.SkipBecausePendingDestroy:
                return;
            case NetSpawnDestroyGuard.SpawnDecision.SkipBecauseDuplicate:
                GD.PrintErr($"ReadSpawnObj: id {id} 已存在，忽略重复 SpawnObj");
                return;
        }

        int nameLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(4, 4));
        string objName = Utf8.GetString(content.Slice(8, nameLen));

        var obj = ObjectPoolManager.GetPossibleObject<INetObject>(objName);
        if (obj == null)
        {
            GD.PrintErr($"ReadSpawnObj: 无法实例化 {objName}，记入 pendingDestroy id:{id}");
            spawnDestroyGuard.MarkPendingDestroy(id);
            return;
        }
        lazyIdToObject[id] = obj;
    }

    private void ReadObjInitialPacket(ReadOnlySpan<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        bool hasLazy = lazyIdToObject.ContainsKey(id);

        switch (spawnDestroyGuard.DecideInitial(id, idToObject.ContainsKey(id), hasLazy))
        {
            case NetSpawnDestroyGuard.InitialDecision.IgnoreBecausePendingDestroy:
                if (lazyIdToObject.TryGetValue(id, out var stale))
                {
                    lazyIdToObject.Remove(id);
                    stale.INetDestroy();
                }
                return;
            case NetSpawnDestroyGuard.InitialDecision.IgnoreBecauseDuplicate:
                GD.PrintErr($"ReadObjInitialPacket: id {id} 已存在，忽略重复初始包");
                return;
            case NetSpawnDestroyGuard.InitialDecision.MissingLazy:
                GD.PrintErr($"ReadObjInitialPacket: 找不到 lazy obj id:{id}");
                return;
        }

        if (!lazyIdToObject.TryGetValue(id, out var obj))
        {
            GD.PrintErr($"ReadObjInitialPacket: 找不到 lazy obj id:{id}");
            return;
        }
        lazyIdToObject.Remove(id);

        ushort varCount = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(4, 2));
        int pos = 6;
        var fullVars = obj.GetFullStateVars();

        for (int i = 0; i < varCount; i++)
        {
            int dataLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4));
            pos += 4;
            var slice = content.Slice(pos, dataLen);
            pos += dataLen;

            if (fullVars != null && i < fullVars.Count)
            {
                fullVars[i].Value = ReadVariant(slice);
                fullVars[i].ClearDirty();
            }
        }

        idToObject[id] = obj;
        objectToId[obj] = id;
        idGenerator.SetUsed(id);
        obj.INetSpawn();
    }

    public void HostDestroyNetObject(INetObject netObject)
    {
        uint id = GetID(netObject);
        if (id == 0)
        {
            GD.PrintErr("意外HostDestroyNetObject id == 0");
            return;
        }

        byte[] buffer = new byte[1 + 4];
        buffer[0] = (byte)NetMsgType.DestroyObj;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1, 4), id);
        SendPackedToAllClients(buffer);
        RemoveNetObject(netObject);
    }

    private void ReadDestroyObj(ReadOnlySpan<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content);
        bool hasLazy = lazyIdToObject.ContainsKey(id);
        bool hasReady = idToObject.ContainsKey(id);

        switch (spawnDestroyGuard.DecideDestroy(id, hasLazy, hasReady))
        {
            case NetSpawnDestroyGuard.DestroyDecision.DestroyLazyKeepPending:
                if (lazyIdToObject.TryGetValue(id, out var lazyObj))
                {
                    lazyIdToObject.Remove(id);
                    lazyObj.INetDestroy();
                }
                return;

            case NetSpawnDestroyGuard.DestroyDecision.DestroyReady:
                var obj = GetNetObject(id);
                if (obj != null)
                {
                    obj.INetDestroy();
                    RemoveNetObject(obj);
                }
                return;

            case NetSpawnDestroyGuard.DestroyDecision.MarkPendingBeforeSpawn:
                return;
        }
    }
    #endregion

    #region State & Input
    public void HostSendStateData(INetObject netObject)
    {
        var vars = netObject.GetFullStateVars();
        if (vars == null || vars.Count == 0) return;
        uint id = GetID(netObject);
        if (id == 0)
        {
            GD.PrintErr("意外HostSendStateData id == 0");
            return;
        }

        ulong mask = NetVarMaskUtil.BuildMask(vars.Count, i => vars[i].IsDirty);
        if (mask == 0) return;

        // 只序列化 mask 覆盖的前 64 个变量，与接收端按 bit 读取一致
        List<byte[]> dirtyData = new();
        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            byte[] d = GD.VarToBytes(vars[i].Value);
            dirtyData.Add(d);
            vars[i].ClearDirty();
        }

        byte[] buffer = new byte[1 + 4 + 8 + (dirtyData.Count * 4) + GetTotalLen(dirtyData)];
        int p = 0;
        buffer[p++] = (byte)NetMsgType.StateUpdate;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), id); p += 4;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(p, 8), mask); p += 8;
        foreach (var d in dirtyData)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), d.Length); p += 4;
            d.CopyTo(buffer.AsSpan(p)); p += d.Length;
        }
        SendPackedToAllClients(buffer);
    }

    private void ReadObjStateData(ReadOnlySpan<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        var obj = GetNetObject(id);
        if (obj == null)
        {
            GD.PrintErr($"意外ReadObjStateData obj == null; id: {id}");
            return;
        }
        var vars = obj.GetFullStateVars();
        if (vars == null || vars.Count == 0) return;

        ulong mask = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(4, 8));
        int pos = 12;
        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
            if (!vars[i].authorityIgnore || !obj.HasAuthority())
            {
                vars[i].Value = ReadVariant(content.Slice(pos, len));
                vars[i].ClearDirty();
            }
            pos += len;
        }
    }

    public void SendInputData(INetObject netObject)
    {
        var vars = netObject.GetInputStateVars();
        if (vars == null || vars.Count == 0) return;
        uint id = GetID(netObject);
        if (id == 0)
        {
            GD.PrintErr("意外SendInputData id == 0");
            return;
        }

        ulong mask = NetVarMaskUtil.BuildMask(vars.Count, i => vars[i].IsDirty);
        if (mask == 0) return;

        byte[] buffer = new byte[1 + 4 + 8 + 512];
        int p = 0;
        buffer[p++] = (byte)NetMsgType.Input;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), id); p += 4;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(p, 8), mask); p += 8;

        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            byte[] d = GD.VarToBytes(vars[i].Value);
            if (p + 4 + d.Length > buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), d.Length); p += 4;
            d.CopyTo(buffer.AsSpan(p)); p += d.Length;
            vars[i].ClearDirty();
        }
        SendPackedToPeer(buffer.AsSpan(0, p), HostId);
    }

    private void ReadInputData(ReadOnlySpan<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        var obj = GetNetObject(id);
        if (obj == null)
        {
            GD.PrintErr("意外ReadInputData obj == null");
            return;
        }

        var vars = obj.GetInputStateVars();
        if (vars == null || vars.Count == 0) return;

        ulong mask = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(4, 8));
        int pos = 12;
        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
            vars[i].Value = ReadVariant(content.Slice(pos, len));
            pos += len;
        }
    }
    #endregion

    #region Custom Packet
    private enum NetCustomPayloadMode : byte
    {
        VariantArray = 0,
        RawBytes = 1
    }

    public void SendCustomPacket(INetObject target, ushort packetId, Variant[] args,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || flags == NetSendFlags.None) return;

        byte[] buffer = BuildCustomVariantBody(flagsByte: (byte)flags, netId, packetId, args, peerId: null);
        DeliverOnSendIfNeeded(alsoRunLocally,
            () => target.GetNetCustomPacketTable()?.Dispatch(packetId, args));
        SendOutbound(buffer, flags);
    }

    public void SendCustomPacketToPeer(INetObject target, ulong targetPeerId, ushort packetId, Variant[] args,
        bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || targetPeerId == 0) return;

        if (targetPeerId == LocalId)
        {
            DeliverOnSendIfNeeded(alsoRunLocally,
                () => target.GetNetCustomPacketTable()?.Dispatch(packetId, args));
            return;
        }

        byte[] buffer = BuildCustomVariantBody(WirePeerTargetMarker, netId, packetId, args, targetPeerId);
        SendOutboundToPeer(buffer, targetPeerId);
    }

    public void SendCustomRawPacket(INetObject target, ushort packetId, ReadOnlySpan<byte> payload,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || flags == NetSendFlags.None) return;

        byte[] buffer = BuildCustomRawBody(flagsByte: (byte)flags, netId, packetId, payload, peerId: null);
        if (alsoRunLocally)
            target.GetNetCustomPacketTable()?.DispatchRaw(packetId, payload);
        SendOutbound(buffer, flags);
    }

    public void SendCustomRawPacketToPeer(INetObject target, ulong targetPeerId, ushort packetId,
        ReadOnlySpan<byte> payload, bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || targetPeerId == 0) return;

        if (targetPeerId == LocalId)
        {
            if (alsoRunLocally)
                target.GetNetCustomPacketTable()?.DispatchRaw(packetId, payload);
            return;
        }

        byte[] buffer = BuildCustomRawBody(WirePeerTargetMarker, netId, packetId, payload, targetPeerId);
        SendOutboundToPeer(buffer, targetPeerId);
    }

    private byte[] BuildCustomVariantBody(byte flagsByte, uint netId, ushort packetId, Variant[] args, ulong? peerId)
    {
        List<byte[]> argDatas = new();
        int argsSize = 0;
        if (args != null)
        {
            foreach (var a in args)
            {
                byte[] d = GD.VarToBytes(a);
                argDatas.Add(d);
                argsSize += 4 + d.Length;
            }
        }

        int peerExtra = peerId.HasValue ? 8 : 0;
        byte[] buffer = new byte[1 + 1 + peerExtra + 4 + 2 + 1 + 2 + argsSize];
        int p = 0;
        buffer[p++] = (byte)NetMsgType.Customize;
        buffer[p++] = flagsByte;
        if (peerId.HasValue)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(p, 8), peerId.Value);
            p += 8;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), netId); p += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(p, 2), packetId); p += 2;
        buffer[p++] = (byte)NetCustomPayloadMode.VariantArray;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(p, 2), (ushort)argDatas.Count); p += 2;
        foreach (var d in argDatas)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), d.Length); p += 4;
            d.CopyTo(buffer.AsSpan(p)); p += d.Length;
        }
        return buffer;
    }

    private byte[] BuildCustomRawBody(byte flagsByte, uint netId, ushort packetId, ReadOnlySpan<byte> payload, ulong? peerId)
    {
        int peerExtra = peerId.HasValue ? 8 : 0;
        byte[] buffer = new byte[1 + 1 + peerExtra + 4 + 2 + 1 + 4 + payload.Length];
        int p = 0;
        buffer[p++] = (byte)NetMsgType.Customize;
        buffer[p++] = flagsByte;
        if (peerId.HasValue)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(p, 8), peerId.Value);
            p += 8;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), netId); p += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(p, 2), packetId); p += 2;
        buffer[p++] = (byte)NetCustomPayloadMode.RawBytes;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), payload.Length); p += 4;
        payload.CopyTo(buffer.AsSpan(p));
        return buffer;
    }

    private void SendOutboundToPeer(ReadOnlySpan<byte> body, ulong targetPeerId)
    {
        if (!IsHost)
            SendPackedToPeer(body, HostId);
        else
            Transport.Send(PackBody(body), targetPeerId);
    }

    private void HostRelayPeerPacket(NetMsgType msgType, ReadOnlySpan<byte> content, ulong targetPeerId)
    {
        if (!IsHost || targetPeerId == LocalId) return;
        byte[] body = new byte[1 + content.Length];
        body[0] = (byte)msgType;
        content.CopyTo(body.AsSpan(1));
        Transport.Send(PackBody(body), targetPeerId);
    }

    private void ReadCustomize(ReadOnlySpan<byte> content)
    {
        byte flagsByte = content[0];
        int pos = 1;

        if (NetRouting.IsPeerTarget(flagsByte))
        {
            ulong targetPeerId = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(pos, 8));
            pos += 8;
            DispatchCustomizePayload(content, pos, deliver: LocalId == targetPeerId);
            HostRelayPeerPacket(NetMsgType.Customize, content, targetPeerId);
            return;
        }

        var flags = (NetSendFlags)flagsByte;
        DispatchCustomizePayload(content, pos, deliver: ShouldDeliverOnReceive(flags));
        HostForwardClientsIfNeeded(flags);
    }

    private void DispatchCustomizePayload(ReadOnlySpan<byte> content, int pos, bool deliver)
    {
        uint netId = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
        ushort packetId = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(pos, 2)); pos += 2;
        NetCustomPayloadMode mode = (NetCustomPayloadMode)content[pos++];

        if (!deliver) return;

        var table = GetNetObject(netId)?.GetNetCustomPacketTable();
        if (mode == NetCustomPayloadMode.VariantArray)
        {
            ushort argc = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(pos, 2));
            pos += 2;
            Variant[] args = new Variant[argc];
            for (int i = 0; i < argc; i++)
            {
                int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4));
                pos += 4;
                args[i] = GD.BytesToVar(content.Slice(pos, len).ToArray());
                pos += len;
            }
            table?.Dispatch(packetId, args);
        }
        else if (mode == NetCustomPayloadMode.RawBytes)
        {
            int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4));
            pos += 4;
            table?.DispatchRaw(packetId, content.Slice(pos, len));
        }
        else
        {
            GD.PrintErr($"ReadCustomize: 未知 payload mode: {(byte)mode}");
        }
    }
    #endregion

    #region RPC
    /// <param name="alsoRunLocally">
    /// 与 flags 正交。true（默认）：调用方本地 Dispatch 一次。false：调用方不执行。
    /// </param>
    public void SendRPC(INetObject target, byte rpcId, Variant[] args,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || flags == NetSendFlags.None) return;

        byte[] buffer = BuildRPCBody(flagsByte: (byte)flags, netId, rpcId, args, peerId: null);
        DeliverOnSendIfNeeded(alsoRunLocally,
            () => target.GetNetRPCTable()?.Dispatch(rpcId, args));
        SendOutbound(buffer, flags);
    }

    public void SendRPCToPeer(INetObject target, ulong targetPeerId, byte rpcId, Variant[] args,
        bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || targetPeerId == 0) return;

        if (targetPeerId == LocalId)
        {
            DeliverOnSendIfNeeded(alsoRunLocally,
                () => target.GetNetRPCTable()?.Dispatch(rpcId, args));
            return;
        }

        byte[] buffer = BuildRPCBody(WirePeerTargetMarker, netId, rpcId, args, targetPeerId);
        SendOutboundToPeer(buffer, targetPeerId);
    }

    private byte[] BuildRPCBody(byte flagsByte, uint netId, byte rpcId, Variant[] args, ulong? peerId)
    {
        List<byte[]> argDatas = new();
        int argsSize = 0;
        if (args != null)
        {
            foreach (var a in args)
            {
                byte[] d = GD.VarToBytes(a);
                argDatas.Add(d);
                argsSize += 4 + d.Length;
            }
        }

        int peerExtra = peerId.HasValue ? 8 : 0;
        byte[] buffer = new byte[1 + 1 + peerExtra + 4 + 1 + 2 + argsSize];
        int p = 0;
        buffer[p++] = (byte)NetMsgType.RPC;
        buffer[p++] = flagsByte;
        if (peerId.HasValue)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(p, 8), peerId.Value);
            p += 8;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), netId); p += 4;
        buffer[p++] = rpcId;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(p, 2), (ushort)argDatas.Count); p += 2;
        foreach (var d in argDatas)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), d.Length); p += 4;
            d.CopyTo(buffer.AsSpan(p)); p += d.Length;
        }
        return buffer;
    }

    private void ReadRPC(ReadOnlySpan<byte> content)
    {
        byte flagsByte = content[0];
        int pos = 1;

        if (NetRouting.IsPeerTarget(flagsByte))
        {
            ulong targetPeerId = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(pos, 8));
            pos += 8;
            DispatchRPCPayload(content, pos, deliver: LocalId == targetPeerId);
            HostRelayPeerPacket(NetMsgType.RPC, content, targetPeerId);
            return;
        }

        var flags = (NetSendFlags)flagsByte;
        DispatchRPCPayload(content, pos, deliver: ShouldDeliverOnReceive(flags));
        HostForwardClientsIfNeeded(flags);
    }

    private void DispatchRPCPayload(ReadOnlySpan<byte> content, int pos, bool deliver)
    {
        uint netId = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
        byte rpcId = content[pos++];
        ushort argc = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(pos, 2)); pos += 2;

        Variant[] args = new Variant[argc];
        for (int i = 0; i < argc; i++)
        {
            int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
            args[i] = ReadVariant(content.Slice(pos, len));
            pos += len;
        }

        if (!deliver) return;
        var obj = GetNetObject(netId);
        if (obj != null) obj.GetNetRPCTable()?.Dispatch(rpcId, args);
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
        if (!IsHost || Transport == null) return;

        ulong replyTo = Transport.CurrentSenderId;
        if (replyTo == 0) return;

        byte[] pong = new byte[1 + content.Length];
        pong[0] = (byte)NetMsgType.Pong;
        content.CopyTo(pong.AsSpan(1));
        SendPackedToPeer(pong, replyTo);
    }

    private void ReadPong(ReadOnlySpan<byte> content)
    {
        long sendTime = BinaryPrimitives.ReadInt64LittleEndian(content);
        long rtt = (long)Time.GetTicksMsec() - sendTime;
        AvgRTT = AvgRTT == 0 ? rtt : (long)(AvgRTT * 0.7f + rtt * 0.3f);
    }
    #endregion

    #region utils
    private int GetTotalLen(List<byte[]> list)
    {
        int sum = 0;
        foreach (var b in list) sum += b.Length;
        return sum;
    }
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

        GD.Print("----- Last Packet State -----");
        GD.Print($"Last Read Header: {forwardedHead}");
        GD.Print($"ContentData Null: {forwardedContent.Array == null}");
        GD.Print($"ContentData Length: {forwardedContent.Count}");

        GD.Print("==============================================");
    }
    private uint _GetNextIdPreview()
    {
        return idGenerator?.PeekNextId() ?? 0;
    }
    #endregion
}
