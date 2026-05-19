using Godot;
using System;

public class GameContext
{
    public string RoomName { get; set; }
    public int MaxPlayers { get; set; }
    public EGameLobbyType Visibility { get; set; }
    public bool MidJoinable { get; set; }
    public bool LocalCoopEnabled { get; set; }
    public int LocalPlayerCount { get; set; } = 1;
}
public enum EGameLobbyType
{
    Public,
    Private,
    FriendsOnly
}