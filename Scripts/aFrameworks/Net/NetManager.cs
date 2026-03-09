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

    public enum NetEventSendType : byte
    {
        ToHost = 0,
        ToAll = 1,
        ToAllExceptSender = 2,
        ToPlayer
    }

    public enum RPCSendType : byte
    {
        ToAll,
        ToHost,
        ToAllExceptSender
    }
    #endregion

    #region fields
    private Dictionary<uint, INetObject> idToObject = new();
    private Dictionary<INetObject, uint> objectToId = new();
    private Dictionary<uint, INetObject> lazyIdToObject = new();
    public NetworkIdGenerator idGenerator = new NetworkIdGenerator();

    public TransportManager transportManager;
    public bool active = false;

    private int pingIntervalTick = 0;
    public long AvgRTT { get; private set; }

    public Action<string> EventCb = (_) => { };
    public Action<string, ulong> EventWithSenderCb = (_, __) => { };

    private byte[] contentData; // 用于房主转发的缓存
    private NetMsgType readHead;
    private int accum = 0;
    #endregion

    #region lifecycle
    public void Start()
    {
        active = true;
        transportManager = TransportManager.Instance;
        idToObject.Clear();
        objectToId.Clear();
        lazyIdToObject.Clear();
        idGenerator = new NetworkIdGenerator();
        AvgRTT = 0;
    }

    public void Deactive() => active = false;

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

    #region trans
    private void InternalSend(Span<byte> data, SendType type)
    {
        if (transportManager?.Current == null) return;
        transportManager.Current.Send(data.ToArray(), type);
    }

    /// <summary>
    /// 核心：给消息包装 4 字节的长度前缀 [Length][Body]
    /// </summary>
    private void SendPackedMessage(Span<byte> body, SendType type = SendType.AllOthers)
    {
        int len = body.Length;
        byte[] packet = new byte[len + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), (uint)len);
        body.CopyTo(packet.AsSpan(4));
        InternalSend(packet, type);
    }

    /// <summary>
    /// 房主转发逻辑：必须保留原始 Header
    /// </summary>
    private void Forward(SendType sendType)
    {
        if (contentData == null) return;
        int bodyLen = 1 + contentData.Length;
        byte[] packet = new byte[4 + bodyLen];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), (uint)bodyLen);
        packet[4] = (byte)readHead;
        contentData.CopyTo(packet.AsSpan(5));
        InternalSend(packet, sendType);
    }
    #endregion

    #region (AnalyseStream)
    private List<byte> _receiveBuffer = new();
    public void AnalyseStream(byte[] bytes)
    {
        if (!active) return;
        _receiveBuffer.AddRange(bytes); // 将新到的字节加入缓存

        while (_receiveBuffer.Count >= 4)
        {
            byte[] bufferArray = _receiveBuffer.ToArray();
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bufferArray.AsSpan(0, 4));
            if (_receiveBuffer.Count < 4 + length) break; // 数据还没收全，跳出循环等待下一次
            // --- 核心改动：局部变量处理 ---
            NetMsgType currentHead = (NetMsgType)bufferArray[4];
            byte[] currentContent = length > 1
                ? _receiveBuffer.GetRange(5, (int)length - 1).ToArray()
                : Array.Empty<byte>();

            // 临时赋值给全局，兼容旧的 Forward() 逻辑
            this.readHead = currentHead;
            this.contentData = currentContent;

            // 移除已处理的数据
            _receiveBuffer.RemoveRange(0, 4 + (int)length);

            // --- 派发消息 ---
            Span<byte> contentSpan = currentContent;

            switch (readHead)
            {
                case NetMsgType.Event: ReadEvent(contentSpan); break;
                case NetMsgType.EventWithSender: ReadEventWithSender(contentSpan); break;
                case NetMsgType.SpawnObj: ReadSpawnObj(contentSpan); break;
                case NetMsgType.InitialPacket: ReadObjInitialPacket(contentSpan); break;
                case NetMsgType.StateUpdate: ReadObjStateData(contentSpan); break;
                case NetMsgType.Input: ReadInputData(contentSpan); break;
                case NetMsgType.RPC: ReadRPC(contentSpan); break;
                case NetMsgType.Ping: ReadPing(contentSpan); break;
                case NetMsgType.Pong: ReadPong(contentSpan); break;
                case NetMsgType.DestroyObj: ReadDestroyObj(contentSpan); break;
            }
        }
    }
    #endregion

    #region (String Events)
    public void SendEvent(string evt, NetEventSendType sendType)
    {
        byte[] evtBytes = Encoding.UTF8.GetBytes(evt);
        byte[] buffer = new byte[2 + 4 + evtBytes.Length];
        buffer[0] = (byte)NetMsgType.Event;
        buffer[1] = (byte)sendType;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(2, 4), evtBytes.Length);
        evtBytes.CopyTo(buffer.AsSpan(6));

        SendPackedMessage(buffer, GetEventTarget(sendType));
    }

    private void ReadEvent(Span<byte> content)
    {
        NetEventSendType sendType = (NetEventSendType)content[0];

        if (sendType == NetEventSendType.ToPlayer)
        {
            ulong targetId = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(1, 8));
            int strLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(9, 4));
            string evt = Encoding.UTF8.GetString(content.Slice(13, strLen));

            // 如果我就是目标接收者，触发回调
            if (transportManager.Current.LocalID == targetId)
            {
                EventCb?.Invoke(evt);
            }

            // 如果我是主机且目标不是我，执行中转
            if (transportManager.Current.AmIHost() && targetId != transportManager.Current.LocalID)
            {
                // 重新包装并发送给指定目标
                transportManager.Current.Send(PackBody(content.ToArray()), targetId);
            }
        }
        else
        {
            // 原有的逻辑
            int strLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(1, 4));
            string evt = Encoding.UTF8.GetString(content.Slice(5, strLen));
            EventCb?.Invoke(evt);

            if (transportManager.Current.AmIHost())
            {
                if (sendType == NetEventSendType.ToAll) Forward(SendType.AllOthers);
                else if (sendType == NetEventSendType.ToAllExceptSender) Forward(SendType.OthersExceptSender);
            }
        }
    }

    public void SendEventWithSender(string evt, NetEventSendType sendType)
    {
        byte[] evtBytes = Encoding.UTF8.GetBytes(evt);
        byte[] buffer = new byte[2 + 8 + 4 + evtBytes.Length];
        buffer[0] = (byte)NetMsgType.EventWithSender;
        buffer[1] = (byte)sendType;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(2, 8), transportManager.Current.LocalID);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(10, 4), evtBytes.Length);
        evtBytes.CopyTo(buffer.AsSpan(14));

        SendPackedMessage(buffer, GetEventTarget(sendType));
    }

    private void ReadEventWithSender(Span<byte> content)
    {
        NetEventSendType sendType = (NetEventSendType)content[0];
        ulong senderId = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(1, 8));
        int strLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(9, 4));
        string evt = Encoding.UTF8.GetString(content.Slice(13, strLen));

        EventWithSenderCb?.Invoke(evt, senderId);

        if (transportManager.Current.AmIHost())
        {
            if (sendType == NetEventSendType.ToAll) Forward(SendType.AllOthers);
            else if (sendType == NetEventSendType.ToAllExceptSender) Forward(SendType.OthersExceptSender);
        }
    }

    private SendType GetEventTarget(NetEventSendType type)
    {
        if (!transportManager.Current.AmIHost()) return SendType.Host;
        return type == NetEventSendType.ToAllExceptSender ? SendType.OthersExceptSender : SendType.AllOthers;
    }

    public void SendEventToPlayer(ulong targetPeerId, string evt)
    {
        byte[] evtBytes = Encoding.UTF8.GetBytes(evt);
        // 布局: [MsgType][SendType][TargetID][StrLen][Data]
        // 长度: 1 + 1 + 8 + 4 + data
        byte[] buffer = new byte[1 + 1 + 8 + 4 + evtBytes.Length];

        buffer[0] = (byte)NetMsgType.Event;
        buffer[1] = (byte)NetEventSendType.ToPlayer;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(2, 8), targetPeerId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(10, 4), evtBytes.Length);
        evtBytes.CopyTo(buffer.AsSpan(14));

        // 如果我是客户端，发给主机中转；如果是主机，直接定向发送
        if (!transportManager.Current.AmIHost())
        {
            SendPackedMessage(buffer, SendType.Host);
        }
        else
        {
            // 只有主机能执行特定的 PeerId 发送
            transportManager.Current.Send(PackBody(buffer), targetPeerId);
        }
    }

    // 辅助工具：用于为主机手动发送特定 Peer 时包装长度前缀
    private byte[] PackBody(Span<byte> body)
    {
        byte[] packet = new byte[body.Length + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), (uint)body.Length);
        body.CopyTo(packet.AsSpan(4));
        return packet;
    }
    #endregion

    #region ( Sync / Spawn / Destroy / InitialPacket)
    /// <summary>
    /// 当新玩家加入时，房主调用此方法将场上所有已有的网络对象信息同步给该玩家
    /// </summary>
    /// <param name="peerId">新加入玩家的 ID</param>
    public void SyncAllNetObjectsToPlayer(ulong peerId)
    {
        if (!transportManager.Current.AmIHost()) return;

        Main.Print($"[Net Mgr] 正在向新玩家 {peerId} 同步所有存活对象...");

        foreach (var kvp in idToObject)
        {
            uint id = kvp.Key;
            INetObject obj = kvp.Value;

            // 构造 Spawn + Initial 复合包
            byte[] fullPacket = PackSpawnMessage(obj, id);

            // 注意：这里需要调用底层的发送，仅发送给特定的 peerId
            // 如果你的 TransportManager.Send 支持 peerId，请确保传入
            // 这里假设你的 transport 接口支持通过某种方式指定目标
            transportManager.Current.Send(fullPacket, peerId);
        }
    }
    public void HostSpawnNetObject(INetObject netObject)
    {
        if (!transportManager.Current.AmIHost()) return;
        uint id = idGenerator.GetNextId();
        idToObject[id] = netObject;
        objectToId[netObject] = id;

        // 生成复合包（Spawn + Initial）
        byte[] fullPacket = PackSpawnMessage(netObject, id);
        InternalSend(fullPacket, SendType.AllOthers);
    }

    private byte[] PackSpawnMessage(INetObject obj, uint id)
    {
        // 1. 序列化字符串
        byte[] nameBytes = Encoding.UTF8.GetBytes(obj.Info.ObjectName);
        int spawnBodyLen = 1 + 4 + 4 + nameBytes.Length; // Header + ID + StrLen + Data

        // 2. 序列化初始变量
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
        int initBodyLen = 1 + 4 + 2 + varsContentSize; // Header + ID + Count + Vars

        // 拼接两个完整的、带长度前缀的包
        byte[] finalResult = new byte[(4 + spawnBodyLen) + (4 + initBodyLen)];
        int cur = 0;

        // 包 1: SpawnObj
        BinaryPrimitives.WriteUInt32LittleEndian(finalResult.AsSpan(cur, 4), (uint)spawnBodyLen); cur += 4;
        finalResult[cur++] = (byte)NetMsgType.SpawnObj;
        BinaryPrimitives.WriteUInt32LittleEndian(finalResult.AsSpan(cur, 4), id); cur += 4;
        BinaryPrimitives.WriteInt32LittleEndian(finalResult.AsSpan(cur, 4), nameBytes.Length); cur += 4;
        nameBytes.CopyTo(finalResult.AsSpan(cur)); cur += nameBytes.Length;

        // 包 2: InitialPacket
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

    private void ReadSpawnObj(Span<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        int nameLen = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(4, 4));
        string objName = Encoding.UTF8.GetString(content.Slice(8, nameLen));

        var obj = ObjectPoolManager.GetPossibleObject<INetObject>(objName);
        lazyIdToObject[id] = obj;
    }

    private void ReadObjInitialPacket(Span<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        if (idToObject.ContainsKey(id))
        {
            GD.PrintErr($"ReadObjInitialPacket: id {id} 已存在，忽略重复初始包");
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
                fullVars[i].Value = GD.BytesToVar(slice);
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
        //Main.Print("主机通知销毁掉NetObj: " + netObject.Info.ObjectName);
        byte[] buffer = new byte[1 + 4];
        buffer[0] = (byte)NetMsgType.DestroyObj;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1, 4), id);
        SendPackedMessage(buffer, SendType.AllOthers);
        RemoveNetObject(netObject);
    }

    private void ReadDestroyObj(Span<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content);
        var obj = GetNetObject(id);
        if (obj != null)
        {
            obj.INetDestroy();
            RemoveNetObject(obj);
        }
        else
        {
            GD.PrintErr("意外ReadDestroyObj obj == null");
        }
    }
    #endregion

    #region (State & Input)
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

        ulong mask = 0;
        int checkCount = Math.Min(vars.Count, 64);
        for (int i = 0; i < checkCount; i++)
            if (vars[i].IsDirty) mask |= (1UL << i);

        if (mask == 0) return;

        // 计算长度并准备数据
        List<byte[]> dirtyData = new();
        foreach (var v in vars)
        {
            if (v.IsDirty)
            {
                byte[] d = GD.VarToBytes(v.Value);
                dirtyData.Add(d);
                v.ClearDirty();
            }
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
        SendPackedMessage(buffer);
    }

    private void ReadObjStateData(Span<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        var obj = GetNetObject(id);
        if (obj == null)
        {
            GD.PrintErr($"意外ReadObjStateData obj == null; id: {id}");
            return;
        }
        ulong mask = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(4, 8));
        var vars = obj.GetFullStateVars();
        int pos = 12;

        for (int i = 0; i < vars.Count; i++)
        {
            if ((mask & (1UL << i)) != 0)
            {
                int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
                if (!vars[i].authorityIgnore || !obj.HasAuthority())
                {
                    vars[i].Value = GD.BytesToVar(content.Slice(pos, len));
                    vars[i].ClearDirty();
                }
                pos += len;
            }
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

        ulong mask = 0;
        for (int i = 0; i < Math.Min(vars.Count, 64); i++)
            if (vars[i].IsDirty) mask |= (1UL << i);

        if (mask == 0) return;

        byte[] buffer = new byte[1 + 4 + 8 + 512]; // 预分配足够大或动态计算
        int p = 0;
        buffer[p++] = (byte)NetMsgType.Input;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), id); p += 4;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(p, 8), mask); p += 8;

        foreach (var v in vars)
        {
            if (v.IsDirty)
            {
                byte[] d = GD.VarToBytes(v.Value);
                // 简单的扩容检查
                if (p + 4 + d.Length > buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), d.Length); p += 4;
                d.CopyTo(buffer.AsSpan(p)); p += d.Length;
                v.ClearDirty();
            }
        }
        SendPackedMessage(buffer.AsSpan(0, p), SendType.Host);
    }

    private void ReadInputData(Span<byte> content)
    {
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        var obj = GetNetObject(id);
        if (obj == null)
        {
            GD.PrintErr("意外ReadInputData obj == null");
            return;
        }

        ulong mask = BinaryPrimitives.ReadUInt64LittleEndian(content.Slice(4, 8));
        var vars = obj.GetInputStateVars();
        int pos = 12;

        for (int i = 0; i < vars.Count; i++)
        {
            if ((mask & (1UL << i)) != 0)
            {
                int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
                vars[i].Value = GD.BytesToVar(content.Slice(pos, len));
                pos += len;
            }
        }
    }
    #endregion

    #region RPC
    public void SendRPC(INetObject target, byte rpcId, Variant[] args, RPCSendType sendType)
    {
        uint netId = GetID(target);
        if (netId == 0) return;

        List<byte[]> argDatas = new();
        int argsSize = 0;
        if (args != null)
        {
            foreach (var a in args)
            {
                byte[] d = GD.VarToBytes(a);
                argDatas.Add(d);
                argsSize += (4 + d.Length);
            }
        }

        byte[] buffer = new byte[1 + 1 + 4 + 1 + 2 + argsSize];
        int p = 0;
        buffer[p++] = (byte)NetMsgType.RPC;
        buffer[p++] = (byte)sendType;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(p, 4), netId); p += 4;
        buffer[p++] = rpcId;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(p, 2), (ushort)argDatas.Count); p += 2;
        foreach (var d in argDatas)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(p, 4), d.Length); p += 4;
            d.CopyTo(buffer.AsSpan(p)); p += d.Length;
        }

        SendPackedMessage(buffer, !transportManager.Current.AmIHost() ? SendType.Host :
            (sendType == RPCSendType.ToAllExceptSender ? SendType.OthersExceptSender : SendType.AllOthers));
    }

    private void ReadRPC(Span<byte> content)
    {
        RPCSendType sendType = (RPCSendType)content[0];
        uint netId = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(1, 4));
        byte rpcId = content[5];
        ushort argc = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(6, 2));

        Variant[] args = new Variant[argc];
        int pos = 8;
        for (int i = 0; i < argc; i++)
        {
            int len = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(pos, 4)); pos += 4;
            args[i] = GD.BytesToVar(content.Slice(pos, len).ToArray());
            pos += len;
        }

        var obj = GetNetObject(netId);
        if (obj != null) obj.GetNetRPCTable()?.Dispatch(rpcId, args);

        if (sendType == RPCSendType.ToAll && transportManager.Current.AmIHost())
            Forward(SendType.OthersExceptSender);
    }
    #endregion

    #region Ping
    private void UpdatePing(double delta)
    {
        pingIntervalTick++;
        if (pingIntervalTick > 120)
        {
            pingIntervalTick = 0;
            if (!transportManager.Current.AmIHost()) SendPing();
        }
    }

    private void SendPing()
    {
        byte[] buf = new byte[1 + 8];
        buf[0] = (byte)NetMsgType.Ping;
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(1, 8), (long)Time.GetTicksMsec());
        SendPackedMessage(buf, SendType.Host);
    }

    private void ReadPing(Span<byte> content)
    {
        if (!transportManager.Current.AmIHost()) return;
        byte[] pong = new byte[1 + content.Length];
        pong[0] = (byte)NetMsgType.Pong;
        content.CopyTo(pong.AsSpan(1));
        SendPackedMessage(pong, SendType.AllOthers);
    }

    private void ReadPong(Span<byte> content)
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
        GD.Print($"Last Read Header: {readHead}");
        GD.Print($"ContentData Null: {contentData == null}");
        GD.Print($"ContentData Length: {contentData?.Length ?? 0}");

        GD.Print("==============================================");
    }
    private uint _GetNextIdPreview()
    {
        return idGenerator?.PeekNextId() ?? 0;
    }
    #endregion
}

public class NetworkIdGenerator
{
    private uint _currentId = 1;
    public uint PeekNextId() => _currentId;
    private HashSet<uint> _usedIds = new();
    public uint GetNextId()
    {
        while (_usedIds.Contains(_currentId))
        {
            _currentId++;
            if (_currentId == 0) _currentId = 1;
        }
        _usedIds.Add(_currentId);
        return _currentId++;
    }
    public void ReleaseId(uint id) => _usedIds.Remove(id);
    public void SetUsed(uint id) => _usedIds.Add(id);
}