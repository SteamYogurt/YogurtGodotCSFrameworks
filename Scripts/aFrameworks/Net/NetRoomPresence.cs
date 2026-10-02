using System;

/// <summary>
/// 房间发现用的当前会话快照。由会话宿主注册，传输层不直接引用 Game。
/// </summary>
public sealed class NetRoomPresence
{
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public bool Joinable { get; init; } = true;
    public bool MidJoinable { get; init; } = true;
    public bool InMatch { get; init; }
    public string MatchId { get; init; } = "";
}

public static class NetRoomPresenceSource
{
    public static Func<NetRoomPresence> Read;
}
