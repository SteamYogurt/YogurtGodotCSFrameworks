using Godot;
using System;
using System.Collections.Generic;

public partial interface INetTransport
{
    bool InRoom { get; }
    ulong LocalID { get; }
    ulong HostID { get; }

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

    /// <summary>发给指定 peer。客机侧仅当 target 为 HostID 时有效。</summary>
    void Send(ReadOnlySpan<byte> data, ulong peerId);
    void Send(byte[] data, ulong peerId);

    /// <summary>
    /// 主机：发给所有已连接客机，可选排除一人。
    /// 客机：忽略 exclude，一律发给主机（兜底；正常应由 NetManager 直接 Send 主机）。
    /// </summary>
    void SendToAll(ReadOnlySpan<byte> data, ulong excludePeerId = 0);
    void SendToAll(byte[] data, ulong excludePeerId = 0);

    void Poll();

    /// <summary>
    /// 这个只能即时读一次，懒得一直互通维护
    /// </summary>
    public List<INetTransportPlayerInfo> GetTempNetPlayerInfos();
    public event Action NetPlayerListChanged;
    public event Action RoomJoined;
    public event Action RoomJoinFailed;
    public event Action RoomStateChanged;
    public event Action HostQuit;
}

public class INetTransportPlayerInfo
{
    public string name;
    public ulong id;
}
