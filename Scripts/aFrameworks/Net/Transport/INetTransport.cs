#nullable enable
using System;
using System.Collections.Generic;

public partial interface INetTransport
{
    bool InRoom { get; }
    ulong LocalID { get; }
    ulong HostID { get; }
    bool LastJoinWasBlocked { get; }

    /// <summary>
    /// 当前正在处理的入站包来源 peer。未在 Poll 处理包时为 0。
    /// 供 NetManager 转发时排除原发送者、以及 Pong 回包。
    /// </summary>
    ulong CurrentSenderId { get; }

    void Init();
    void Free();
    void CreateRoom();
    void JoinRoom(string roomId);
    void LeaveRoom();
    bool AmIHost();
    bool KickPeer(ulong peerId, bool blockRejoin);

    /// <summary>发给指定 peer。客机侧仅当 target 为 HostID 时有效。</summary>
    void Send(ReadOnlySpan<byte> data, ulong peerId);
    void Send(byte[] data, ulong peerId);

    /// <summary>
    /// 主机：发给所有已连接客机，可选排除一人。
    /// 客机：忽略 exclude，一律发给主机（兜底；正常应由 NetManager 直接 Send 主机）。
    /// </summary>
    void SendToAll(ReadOnlySpan<byte> data, ulong excludePeerId = 0);
    void SendToAll(byte[] data, ulong excludePeerId = 0);

    bool IsPeerConnected(ulong peerId)
    {
        var players = GetTempNetPlayerInfos();
        return players?.Exists(player => player.id == peerId) == true;
    }

    void SendToSelected(ReadOnlySpan<byte> data, Predicate<ulong>? include, ulong excludePeerId = 0)
    {
        if (!AmIHost() || GetTempNetPlayerInfos() is not { } players) return;
        foreach (var player in players)
            if (player.id != LocalID && player.id != excludePeerId && (include == null || include(player.id)))
                Send(data, player.id);
    }

    void Poll();

    /// <summary>
    /// 返回传输层成员的瞬时视图。
    /// </summary>
    public List<INetTransportPlayerInfo>? GetTempNetPlayerInfos();
    public event Action? NetPlayerListChanged;
    public event Action? RoomJoined;
    public event Action? RoomJoinFailed;
    public event Action? RoomStateChanged;
    public event Action? HostQuit;
    public event Action? Kicked;
}

public class INetTransportPlayerInfo
{
    public string name = "";
    public ulong id;
}
