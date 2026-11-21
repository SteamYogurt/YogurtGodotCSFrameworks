using Godot;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;

public partial class NetManager
{
    #region Sync / Spawn / Destroy / InitialPacket
    public void HostSpawnNetObject(INetObject netObject)
    {
        if (!IsHost || objectToId.ContainsKey(netObject)) return;
        uint id = idGenerator.GetNextId();
        byte[] fullPacket = PackSpawnMessage(netObject, id);
        if (fullPacket == null)
        {
            idGenerator.ReleaseId(id);
            return;
        }
        idToObject[id] = netObject;
        objectToId[netObject] = id;
        HostSendFramedPacketForObject(netObject, fullPacket);
    }

    private byte[] PackSpawnMessage(INetObject obj, uint id)
    {
        byte[] nameBytes = Utf8.GetBytes(obj.Info.ObjectName);
        int spawnBodyLen = 1 + 4 + 4 + nameBytes.Length;

        var fullVars = obj.GetFullStateVars();
        if (fullVars?.Count > 64)
        {
            GD.PrintErr($"[NetManager] {obj.Info.ObjectName} 的 Full NetVar 超过 64 个");
            return null;
        }
        List<byte[]> varDatas = new();
        int varsContentSize = 0;
        if (fullVars != null)
        {
            foreach (var v in fullVars)
            {
                byte[] d = GD.VarToBytes(v.Value);
                varDatas.Add(d);
                varsContentSize += 4 + d.Length;
                if (varsContentSize > NetWireLimits.MaxPacketBytes)
                    break;
            }
        }
        int initBodyLen = 1 + 4 + 2 + varsContentSize;

        int packetLen = 8 + spawnBodyLen + initBodyLen;
        if (packetLen > NetWireLimits.MaxPacketBytes)
        {
            GD.PrintErr($"[NetManager] {obj.Info.ObjectName} 的生成快照超出传输上限");
            return null;
        }
        byte[] finalResult = new byte[packetLen];
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
        if (content.Length < 8)
            return;
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        var payload = content[4..];
        if (id == 0 || !NetWireFrames.TryReadField(ref payload, out var name) || name.IsEmpty || !payload.IsEmpty)
            return;

        bool already = idToObject.ContainsKey(id) || lazyIdToObject.ContainsKey(id);

        switch (spawnDestroyGuard.DecideSpawn(id, already))
        {
            case NetSpawnDestroyGuard.SpawnDecision.SkipBecausePendingDestroy:
            case NetSpawnDestroyGuard.SpawnDecision.SkipBecauseDuplicate:
                return;
        }

        string objName = Utf8.GetString(name);

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
        if (content.Length < 6)
            return;
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(0, 4));
        ushort varCount = BinaryPrimitives.ReadUInt16LittleEndian(content[4..]);
        var payload = content[6..];
        if (id == 0 || varCount > 64 || !NetWireFrames.IsVariantSequence(payload, varCount))
            return;

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
            case NetSpawnDestroyGuard.InitialDecision.MissingLazy:
                return;
        }

        var obj = lazyIdToObject[id];
        var fullVars = obj.GetFullStateVars();
        int version = sessionVersion;
        for (int i = 0; i < varCount && i < (fullVars?.Count ?? 0); i++)
        {
            NetWireFrames.TryReadField(ref payload, out var slice);
            fullVars[i].Value = ReadVariant(slice);
            fullVars[i].ClearDirty();
            if (version != sessionVersion)
                return;
        }

        lazyIdToObject.Remove(id);
        idToObject[id] = obj;
        objectToId[obj] = id;
        idGenerator.SetUsed(id);
        obj.INetSpawn();
    }

    public void HostDestroyNetObject(INetObject netObject)
    {
        if (!IsHost) return;
        uint id = GetID(netObject);
        if (id == 0)
        {
            GD.PrintErr("意外HostDestroyNetObject id == 0");
            return;
        }

        byte[] buffer = new byte[1 + 4];
        buffer[0] = (byte)NetMsgType.DestroyObj;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1, 4), id);
        HostSendFramedPacketForObject(netObject, PackBody(buffer));
        RemoveNetObject(netObject);
    }

    private void ReadDestroyObj(ReadOnlySpan<byte> content)
    {
        if (content.Length != 4)
            return;
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(content);
        if (id == 0)
            return;
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
                    RemoveNetObject(obj);
                    obj.INetDestroy();
                }
                return;

            case NetSpawnDestroyGuard.DestroyDecision.MarkPendingBeforeSpawn:
                return;
        }
    }
    #endregion

    #region State & Input
    private void SendStateTick()
    {
        // Both scopes use the same reliable ordering as their object lifecycle.
        foreach (var (id, obj) in idToObject)
        {
            var packet = PackDirtyVars(NetMsgType.StateUpdate, id, obj.GetFullStateVars());
            if (packet == null)
                continue;
            var scope = obj.NetSyncScope;
            var batch = scope == NetSyncScope.Match ? matchBatch : sessionBatch;
            if (batch.WrittenCount + packet.Length > PacketBatchBytes)
                FlushStateBatch(batch, scope);
            if (packet.Length >= PacketBatchBytes)
                HostSendFramedPacketForScope(scope, packet);
            else
                batch.Write(packet);
        }
        FlushStateBatch(sessionBatch, NetSyncScope.Session);
        FlushStateBatch(matchBatch, NetSyncScope.Match);
    }

    private void FlushStateBatch(ArrayBufferWriter<byte> batch, NetSyncScope scope)
    {
        if (batch.WrittenCount == 0)
            return;
        HostSendFramedPacketForScope(scope, batch.WrittenSpan);
        batch.Clear();
    }

    private void SendInputTick()
    {
        foreach (var (id, obj) in idToObject)
        {
            if (!obj.HasAuthority())
                continue;
            var packet = PackDirtyVars(NetMsgType.Input, id, obj.GetInputStateVars());
            if (packet == null)
                continue;
            if (inputBatch.WrittenCount + packet.Length > PacketBatchBytes)
                FlushPeerBatch(inputBatch, HostId);
            if (packet.Length >= PacketBatchBytes)
                Transport.Send(packet, HostId);
            else
                inputBatch.Write(packet);
        }
        FlushPeerBatch(inputBatch, HostId);
    }

    public void HostSendStateData(INetObject netObject)
    {
        if (!IsHost) return;
        var packet = PackDirtyVars(NetMsgType.StateUpdate, GetID(netObject), netObject.GetFullStateVars());
        if (packet != null)
            HostSendFramedPacketForObject(netObject, packet);
    }

    public void SendInputData(INetObject netObject)
    {
        if (IsHost || Transport?.InRoom != true || !netObject.HasAuthority()) return;
        var packet = PackDirtyVars(NetMsgType.Input, GetID(netObject), netObject.GetInputStateVars());
        if (packet != null)
            Transport.Send(packet, HostId);
    }

    private static byte[] PackDirtyVars(NetMsgType type, uint id, List<NetVar> vars)
    {
        if (id == 0 || vars == null || vars.Count == 0)
            return null;
        ulong mask = NetVarMaskUtil.BuildMask(vars.Count, i => vars[i].IsDirty);
        if (mask == 0)
            return null;

        var values = new List<byte[]>();
        int packetLength = 17;
        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            var value = GD.VarToBytes(vars[i].Value);
            packetLength += sizeof(int) + value.Length;
            if (packetLength > NetWireLimits.MaxPacketBytes)
            {
                GD.PrintErr($"[NetManager] {type} 超出传输上限，netId={id}");
                return null;
            }
            values.Add(value);
        }

        byte[] packet = new byte[packetLength];
        BinaryPrimitives.WriteInt32LittleEndian(packet, packetLength - sizeof(int));
        packet[4] = (byte)type;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(5), id);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(9), mask);
        int pos = 17;
        int valueIndex = 0;
        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            var value = values[valueIndex++];
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(pos), value.Length);
            pos += sizeof(int);
            value.CopyTo(packet.AsSpan(pos));
            pos += value.Length;
            vars[i].ClearDirty();
        }
        return packet;
    }

    private void ReadObjStateData(ReadOnlySpan<byte> content)
    {
        if (content.Length < 12)
            return;
        var obj = GetNetObject(BinaryPrimitives.ReadUInt32LittleEndian(content));
        if (obj != null)
            ReadDirtyVars(content, obj.GetFullStateVars(), obj.HasAuthority(), clearDirty: true);
    }

    private void ReadInputData(ReadOnlySpan<byte> content, ulong sender)
    {
        if (content.Length < 12)
            return;
        var obj = GetNetObject(BinaryPrimitives.ReadUInt32LittleEndian(content));
        if (obj != null && CanPeerAccessObject(sender, obj) && obj.CanReceiveInputFrom(sender))
            ReadDirtyVars(content, obj.GetInputStateVars(), ignorePredictedVars: false, clearDirty: false);
    }

    private void ReadDirtyVars(ReadOnlySpan<byte> content, List<NetVar> vars, bool ignorePredictedVars, bool clearDirty)
    {
        if (vars == null || vars.Count == 0)
            return;

        ulong mask = BinaryPrimitives.ReadUInt64LittleEndian(content[4..]);
        var payload = content[12..];
        if (!NetWireFrames.IsVariantSequence(payload, System.Numerics.BitOperations.PopCount(mask)))
            return;

        int version = sessionVersion;
        foreach (int i in NetVarMaskUtil.EnumerateDirtyIndices(mask, vars.Count))
        {
            NetWireFrames.TryReadField(ref payload, out var value);
            if (ignorePredictedVars && vars[i].authorityIgnore)
                continue;
            vars[i].Value = ReadVariant(value);
            if (clearDirty)
                vars[i].ClearDirty();
            if (version != sessionVersion)
                return;
        }
    }
    #endregion
}
