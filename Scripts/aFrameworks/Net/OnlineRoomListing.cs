using System;
using Godot;

/// <summary>
/// Steam LobbyData 与 LAN 广播共用的房间列表键。新项目改 <see cref="GameId"/>。
/// </summary>
public static class OnlineRoomListing
{
    public const string GameId = "YogurtGodotFrameworks";

    public const string KeyGameId = "game_id";
    public const string KeyRoomName = "room_name";
    public const string KeyJoinable = "joinable";
    public const string KeyMidJoinable = "mid_joinable";
    public const string KeyFlow = "flow";
    public const string KeyMatchId = "match_id";
    public const string KeyMaxPlayers = "max_players";
    public const string KeyPlayerCount = "player_count";

    public const string FlowWaiting = "waiting";
    public const string FlowInMatch = "match";

    public static bool IsOurGame(string gameId)
        => string.Equals(gameId, GameId, StringComparison.Ordinal);

    public static bool HasUsableRoomName(string roomName)
        => !string.IsNullOrWhiteSpace(roomName);

    public static bool ParseFlag(string raw, bool defaultValue)
    {
        if (string.IsNullOrEmpty(raw))
            return defaultValue;
        return raw != "0";
    }

    public static int ParsePlayerCount(string raw)
    {
        if (int.TryParse(raw, out int count) && count >= 0)
            return count;
        return -1;
    }

    public static string FallbackRoomName(ENetLobbyDisplayType displayType)
    {
        if (displayType == ENetLobbyDisplayType.Steam)
        {
            string persona = SteamManager.LocalPersonaName.Trim();
            if (!string.IsNullOrEmpty(persona))
                return string.Format(TranslateOrKey("{0}的房间"), persona);
            return TranslateOrKey("Steam 房间");
        }

        return TranslateOrKey("局域网房间");
    }

    static string TranslateOrKey(string key)
    {
        string translated = TranslationServer.Translate(key);
        return string.IsNullOrEmpty(translated) ? key : translated;
    }

    public static string ResolveRoomName(string roomName, ENetLobbyDisplayType displayType)
        => HasUsableRoomName(roomName) ? roomName.Trim() : FallbackRoomName(displayType);

    public static bool IsFull(int playerCount, int memberLimit)
        => memberLimit > 0 && playerCount >= memberLimit;

    /// <summary>
    /// 可点击加入：未满员，且尚未开局（或开局后仍允许中途加入）。
    /// </summary>
    public static bool CanJoin(int playerCount, int memberLimit, bool inMatch, bool midJoinable)
    {
        if (IsFull(playerCount, memberLimit))
            return false;
        if (inMatch && !midJoinable)
            return false;
        return true;
    }

    public static OnlineRoomListEntry FromSteam(SteamLobbyRoomInfo room)
    {
        if (room == null)
            return null;

        return new OnlineRoomListEntry
        {
            RoomId = room.LobbyId.ToString(),
            RoomName = room.RoomName ?? "",
            PlayerCount = room.PlayerCount,
            MemberLimit = room.MemberLimit,
            InMatch = room.InMatch,
            MidJoinable = room.MidJoinable,
            MatchId = room.MatchId ?? "",
            CanJoin = room.Joinable,
        };
    }

    public static OnlineRoomListEntry FromLan(LanDiscoveredRoomInfo room)
    {
        if (room == null)
            return null;

        bool hasListingV2 = IsOurGame(room.GameId);
        bool inMatch = room.InMatch;
        bool midJoinable = hasListingV2 ? room.MidJoinable : true;
        int limit = room.MaxPlayers;
        bool canJoin = hasListingV2
            ? CanJoin(room.PlayerCount, limit, inMatch, midJoinable)
            : room.Joinable;

        return new OnlineRoomListEntry
        {
            RoomId = room.HostAddress ?? "",
            RoomName = room.RoomName ?? "",
            PlayerCount = room.PlayerCount,
            MemberLimit = limit,
            InMatch = inMatch,
            MidJoinable = midJoinable,
            MatchId = room.MatchId ?? "",
            CanJoin = canJoin,
        };
    }
}

public sealed class OnlineRoomListEntry
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public int PlayerCount { get; set; }
    public int MemberLimit { get; set; }
    public bool InMatch { get; set; }
    public bool MidJoinable { get; set; } = true;
    public string MatchId { get; set; } = "";
    public bool CanJoin { get; set; } = true;
}
