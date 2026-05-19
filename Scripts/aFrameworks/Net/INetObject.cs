using System;
using System.Collections.Generic;
using System.IO;
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
        NetCustomPacketSendType sendType = NetCustomPacketSendType.ToAll)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.SendCustomPacket(this, packetId, args, sendType);
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


