using System;

/// <summary>
/// 网上接收者集合（按位组合），写入包内。
/// 与 alsoRunLocally 正交：flags 不决定调用方是否本地执行。
/// 物理上客机只发主机，主机再按位转发；网上永不发给自己。
/// </summary>
[Flags]
public enum NetSendFlags : byte
{
    None = 0,

    /// <summary>主机应通过网络收到并处理。主机自己发送时不会网络回环，若也要执行请 alsoRunLocally: true。</summary>
    Host = 1 << 0,

    /// <summary>各客机应通过网络收到并处理（发送端自己不含在内；要跑请用 alsoRunLocally）。</summary>
    Clients = 1 << 1,

    /// <summary>仅主机。</summary>
    HostOnly = Host,

    /// <summary>主机 + 所有客机（发送端是否本地执行只看 alsoRunLocally）。</summary>
    AllOthers = Host | Clients,
}
