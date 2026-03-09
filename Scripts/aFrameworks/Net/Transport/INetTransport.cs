using Godot;
using System;
using System.Collections.Generic;

public partial interface INetTransport
{
    bool InRoom { get; }
    ulong LocalID { get; }
    ulong HostID { get; }
    void Init();
    void Free();
    void CreateRoom();
    void JoinRoom(string roomId);
    void LeaveRoom();
    bool AmIHost();
    void Send(byte[] data, SendType type);
    void Send(byte[] data, ulong id);
    void Poll();

    /// <summary>
    /// 这个只能即时读一次，懒得一直互通维护
    /// </summary>
    /// <returns></returns>
    public List<INetTransportPlayerInfo> GetTempNetPlayerInfos();
    public event Action NetPlayerListChanged;
    public event Action RoomStateChanged;
    public event Action HostQuit;
}
public class INetTransportPlayerInfo
{
    public string name;
    public ulong id;
}
public enum SendType
{
    AllOthers,
    Host,
    OthersExceptSender,
    JustSender
}