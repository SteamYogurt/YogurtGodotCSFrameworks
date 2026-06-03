using Godot;
using System;

public class GameOnlineContext
{
    public string RoomName { get; set; } = "Default Name";
    public int MaxPlayers { get; set; } = 8;
    public EGameLobbyType Visibility { get; set; } = EGameLobbyType.Public;
    public bool MidJoinable { get; set; } = true;
}
public enum EGameLobbyType
{
    Public,
    Private,
    FriendsOnly
}