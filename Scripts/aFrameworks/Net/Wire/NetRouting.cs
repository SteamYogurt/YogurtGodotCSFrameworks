using System;

/// <summary>
/// 发送/接收路由纯逻辑（无 Transport / Godot 依赖），供 NetManager 与单元测试共用。
/// </summary>
public static class NetRouting
{
    /// <summary>点对点消息在 flags 字节上的标记（与 NetSendFlags 低位不重叠）。</summary>
    public const byte PeerTargetMarker = 0x80;

    public static bool IsPeerTarget(byte flagsByte) => flagsByte == PeerTargetMarker;

    /// <summary>收包端：本机是否应执行 handler。</summary>
    public static bool ShouldDeliverOnReceive(bool amIHost, NetSendFlags flags)
    {
        if (amIHost) return (flags & NetSendFlags.Host) != 0;
        return (flags & NetSendFlags.Clients) != 0;
    }

    /// <summary>发送端：是否因 alsoRunLocally 立刻本地执行（与 flags 正交）。</summary>
    public static bool ShouldRunLocallyOnSend(bool alsoRunLocally) => alsoRunLocally;

    /// <summary>主机收到后是否应转发给其他客机（排除原发送者由调用方处理）。</summary>
    public static bool ShouldHostForwardToClients(NetSendFlags flags)
        => (flags & NetSendFlags.Clients) != 0;

    /// <summary>主机自己发起时是否应对客机 SendToAll。</summary>
    public static bool ShouldHostOutboundToClients(NetSendFlags flags)
        => (flags & NetSendFlags.Clients) != 0;

    /// <summary>客机业务出站是否只能发往主机。</summary>
    public static bool ClientOutboundMustGoToHost => true;
}
