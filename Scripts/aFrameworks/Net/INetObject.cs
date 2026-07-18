using System;
using System.Collections.Generic;
using Godot;

public interface INetObject : IGameObject
{
    public void HostInitialize()
    {

    }

    public bool HasAuthority()
    {
        return false;
    }

    public List<NetVar> GetInputStateVars();
    public List<NetVar> GetFullStateVars();
    public NetRPCTable GetNetRPCTable();

    public NetCustomPacketTable GetNetCustomPacketTable()
    {
        return null;
    }

    public void SendNetCustomPacket(ushort packetId, Variant[] args,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendCustomPacket(this, packetId, args, flags, alsoRunLocally);
    }

    public void SendNetCustomPacketToPeer(ulong targetPeerId, ushort packetId, Variant[] args,
        bool alsoRunLocally = true)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendCustomPacketToPeer(this, targetPeerId, packetId, args, alsoRunLocally);
    }

    public void SendNetCustomRawPacket(ushort packetId, ReadOnlySpan<byte> payload,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendCustomRawPacket(this, packetId, payload, flags, alsoRunLocally);
    }

    public void SendNetCustomRawPacketToPeer(ulong targetPeerId, ushort packetId, ReadOnlySpan<byte> payload,
        bool alsoRunLocally = true)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendCustomRawPacketToPeer(this, targetPeerId, packetId, payload, alsoRunLocally);
    }

    public void SendNetRPC(byte rpcId, Variant[] args,
        NetSendFlags flags = NetSendFlags.AllOthers, bool alsoRunLocally = true)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendRPC(this, rpcId, args, flags, alsoRunLocally);
    }

    public void SendNetRPCToPeer(ulong targetPeerId, byte rpcId, Variant[] args,
        bool alsoRunLocally = true)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendRPCToPeer(this, targetPeerId, rpcId, args, alsoRunLocally);
    }

    /// <summary>
    /// 收到初始包之后立即调用
    /// </summary>
    public void INetSpawn()
    {

    }

    /// <summary>
    /// 销毁或失效前调用
    /// </summary>
    public void INetDestroy()
    {

    }

    public bool IsNetInvalid()
    {
        return false;
    }
}
