using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

public partial class NetManager
{
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

        SendOutboundToPeer(buffer, targetPeerId);
    }

    [Obsolete("Use SendEventToPeer")]
    public void SendEventToPlayer(ulong targetPeerId, string evt) => SendEventToPeer(targetPeerId, evt);

    private void ReadEvent(ReadOnlySpan<byte> content)
    {
        var payload = content;
        if (!TryReadRoute(ref payload, out byte flags, out ulong targetPeerId)
            || !NetWireFrames.TryReadField(ref payload, out var name) || !payload.IsEmpty)
            return;

        string eventName = Utf8.GetString(name);
        int version = sessionVersion;
        if (ShouldDeliverRoute(flags, targetPeerId))
            EventCb?.Invoke(eventName);
        RelayPacket(NetMsgType.Event, content, flags, targetPeerId, version);
    }

    private void ReadEventWithSender(ReadOnlySpan<byte> content)
    {
        if (content.Length < 9 || !NetRouting.IsValidFlags(content[0]))
            return;
        byte flags = content[0];
        ulong senderId = BinaryPrimitives.ReadUInt64LittleEndian(content[1..]);
        var payload = content[9..];
        if ((IsHost && senderId != Transport.CurrentSenderId)
            || !NetWireFrames.TryReadField(ref payload, out var name) || !payload.IsEmpty)
            return;
        int version = sessionVersion;
        if (ShouldDeliverOnReceive((NetSendFlags)flags))
            EventWithSenderCb?.Invoke(Utf8.GetString(name), senderId);
        RelayPacket(NetMsgType.EventWithSender, content, flags, 0, version);
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
        SendOutbound(buffer, flags, target);
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
        SendOutboundToPeer(buffer, targetPeerId, target);
    }

    public void SendCustomRawPacket(INetObject target, ushort packetId, ReadOnlySpan<byte> payload,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        uint netId = GetID(target);
        if (netId == 0 || Transport == null || flags == NetSendFlags.None) return;

        byte[] buffer = BuildCustomRawBody(flagsByte: (byte)flags, netId, packetId, payload, peerId: null);
        if (alsoRunLocally)
            target.GetNetCustomPacketTable()?.DispatchRaw(packetId, payload);
        SendOutbound(buffer, flags, target);
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
        SendOutboundToPeer(buffer, targetPeerId, target);
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

    private void SendOutboundToPeer(ReadOnlySpan<byte> body, ulong targetPeerId, INetObject target = null)
    {
        if (!IsHost)
            SendPackedToPeer(body, HostId);
        else if (target == null || CanPeerAccessObject(targetPeerId, target))
            SendPackedToPeer(body, targetPeerId);
    }

    private static bool TryReadRoute(ref ReadOnlySpan<byte> payload, out byte flags, out ulong targetPeerId)
    {
        flags = 0;
        targetPeerId = 0;
        if (payload.IsEmpty)
            return false;
        flags = payload[0];
        payload = payload[1..];
        if (!NetRouting.IsPeerTarget(flags))
            return NetRouting.IsValidFlags(flags);
        if (payload.Length < sizeof(ulong))
            return false;
        targetPeerId = BinaryPrimitives.ReadUInt64LittleEndian(payload);
        payload = payload[sizeof(ulong)..];
        return targetPeerId != 0;
    }

    private bool ShouldDeliverRoute(byte flags, ulong targetPeerId) =>
        NetRouting.IsPeerTarget(flags) ? LocalId == targetPeerId : ShouldDeliverOnReceive((NetSendFlags)flags);

    private INetObject ReadAccessibleObject(ref ReadOnlySpan<byte> payload)
    {
        if (payload.Length < sizeof(uint))
            return null;
        var target = GetNetObject(BinaryPrimitives.ReadUInt32LittleEndian(payload));
        payload = payload[sizeof(uint)..];
        if (target != null && IsHost)
        {
            ulong sender = Transport.CurrentSenderId;
            if (!CanPeerAccessObject(sender, target) || !target.CanReceiveRemoteCallFrom(sender))
                return null;
        }
        return target;
    }

    private void ReadCustomize(ReadOnlySpan<byte> content)
    {
        var payload = content;
        if (!TryReadRoute(ref payload, out byte flags, out ulong targetPeerId))
            return;
        var target = ReadAccessibleObject(ref payload);
        if (target == null || payload.Length < 3)
            return;

        ushort packetId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var mode = (NetCustomPayloadMode)payload[2];
        payload = payload[3..];
        int version = sessionVersion;
        bool deliver = ShouldDeliverRoute(flags, targetPeerId);
        switch (mode)
        {
            case NetCustomPayloadMode.VariantArray:
                if (!TryReadVariantList(ref payload, out int count))
                    return;
                if (deliver && target.GetNetCustomPacketTable() is { } variantTable)
                    variantTable.Dispatch(packetId, DecodeVariants(payload, count));
                break;
            case NetCustomPayloadMode.RawBytes:
                if (!NetWireFrames.TryReadField(ref payload, out var data) || !payload.IsEmpty)
                    return;
                if (deliver)
                    target.GetNetCustomPacketTable()?.DispatchRaw(packetId, data);
                break;
            default:
                return;
        }
        RelayPacket(NetMsgType.Customize, content, flags, targetPeerId, version, target);
    }

    private static bool TryReadVariantList(ref ReadOnlySpan<byte> payload, out int count)
    {
        count = 0;
        if (payload.Length < sizeof(ushort))
            return false;
        count = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        payload = payload[sizeof(ushort)..];
        return NetWireFrames.IsVariantSequence(payload, count);
    }

    private static Variant[] DecodeVariants(ReadOnlySpan<byte> payload, int count)
    {
        if (count == 0)
            return Array.Empty<Variant>();
        var args = new Variant[count];
        for (int i = 0; i < count; i++)
        {
            NetWireFrames.TryReadField(ref payload, out var data);
            args[i] = ReadVariant(data);
        }
        return args;
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
        SendOutbound(buffer, flags, target);
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
        SendOutboundToPeer(buffer, targetPeerId, target);
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
        var payload = content;
        if (!TryReadRoute(ref payload, out byte flags, out ulong targetPeerId))
            return;
        var target = ReadAccessibleObject(ref payload);
        if (target == null || payload.IsEmpty)
            return;
        byte rpcId = payload[0];
        payload = payload[1..];
        if (!TryReadVariantList(ref payload, out int count))
            return;

        int version = sessionVersion;
        if (ShouldDeliverRoute(flags, targetPeerId) && target.GetNetRPCTable() is { } table)
            table.Dispatch(rpcId, DecodeVariants(payload, count));
        RelayPacket(NetMsgType.RPC, content, flags, targetPeerId, version, target);
    }
    #endregion
}
