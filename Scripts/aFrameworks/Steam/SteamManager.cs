using Godot;
using Steamworks;
using System;
using System.Collections.Generic;

public class SteamLobbyRoomInfo
{
    public ulong LobbyId { get; set; }
    public string RoomName { get; set; } = "";
    public int PlayerCount { get; set; }
    public int MemberLimit { get; set; }
    public bool InMatch { get; set; }
    public bool MidJoinable { get; set; } = true;
    public string MatchId { get; set; } = "";
    public bool Joinable { get; set; } = true;
}

public partial class SteamManager : Singleton<SteamManager>
{
    static AppId_t appId_T = new AppId_t(4453480);
    bool inited = false;
    public bool Inited => inited;

    public static string LocalPersonaName
    {
        get
        {
            if (Instance == null || !Instance.inited)
                return "";
            return SteamFriends.GetPersonaName() ?? "";
        }
    }

    public bool IsRelayNetworkReady
    {
        get
        {
            if (!inited)
                return false;
            var avail = SteamNetworkingUtils.GetRelayNetworkStatus(out _);
            return avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current;
        }
    }

    public void EnsureRelayNetworkAccess()
    {
        if (!inited)
            return;
        SteamNetworkingUtils.InitRelayNetworkAccess();
    }

    Callback<UserStatsReceived_t> userStatsReceiveCallback;
    Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
    CallResult<LobbyMatchList_t> lobbyMatchListCallResult;
    public event Action<IReadOnlyList<SteamLobbyRoomInfo>> LobbyListUpdated;
    public override void _EnterTree()
    {
        base._EnterTree();
        inited = SteamAPI.Init();
        SteamAPI.RestartAppIfNecessary(appId_T);
        if (!inited)
        {
            GD.PrintErr("steam api can't init!");
            return;
        }
        GD.Print("steam api inited!");
        GD.Print("steam user name:" + SteamFriends.GetPersonaName());
        SteamUserStats.RequestCurrentStats();
        EnsureRelayNetworkAccess();
        userStatsReceiveCallback = Callback<UserStatsReceived_t>.Create(OnStatsGot);
        joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }
    #region Achi
    public void UnlockAchi(MyAchi achi)
    {
        if (!inited) return;
        SteamUserStats.SetAchievement(achi.ToString());
        SteamUserStats.StoreStats();
    }
    public void OnStatsGot(UserStatsReceived_t userStatsReceived_T)
    {
        //GD.Print("接收到OnStatsGot回调，检查成就是否存在");
        var achiNames = Enum.GetNames(typeof(MyAchi));
        for (int i = 0; i < achiNames.Length; i++)
        {
            var achi = achiNames[i];
            if (!SteamUserStats.GetAchievement(achi, out bool achieved))
            {
                GD.PrintErr($"{achi}成就不存在！");
            }
        }
    }
    public enum MyAchi
    {
        //ACHI_STARTGAME,

    }
    #endregion
    void OnJoinRequested(GameLobbyJoinRequested_t e)
    {
        Main.Instance.StartSteamInviteJoin(e.m_steamIDLobby.m_SteamID);
    }
    public void RequestLobbyList()
    {
        if (!inited)
        {
            LobbyListUpdated?.Invoke(Array.Empty<SteamLobbyRoomInfo>());
            return;
        }

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            OnlineRoomListing.KeyGameId,
            OnlineRoomListing.GameId,
            ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
        var apiCall = SteamMatchmaking.RequestLobbyList();
        lobbyMatchListCallResult?.Set(apiCall);
    }
    void OnLobbyMatchList(LobbyMatchList_t e, bool ioFailure)
    {
        if (ioFailure)
        {
            LobbyListUpdated?.Invoke(Array.Empty<SteamLobbyRoomInfo>());
            return;
        }

        var rooms = new List<SteamLobbyRoomInfo>((int)e.m_nLobbiesMatching);
        for (int i = 0; i < e.m_nLobbiesMatching; i++)
        {
            var lobby = SteamMatchmaking.GetLobbyByIndex(i);
            string gameId = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyGameId);
            if (!OnlineRoomListing.IsOurGame(gameId))
                continue;

            string roomName = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyRoomName);
            if (!OnlineRoomListing.HasUsableRoomName(roomName))
                continue;

            int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby);
            if (memberLimit <= 0
                && int.TryParse(SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyMaxPlayers), out int parsedMax))
            {
                memberLimit = parsedMax;
            }

            int playerCount = OnlineRoomListing.ParsePlayerCount(
                SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyPlayerCount));
            if (playerCount <= 0)
                playerCount = SteamMatchmaking.GetNumLobbyMembers(lobby);

            string flow = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyFlow);
            bool inMatch = flow == OnlineRoomListing.FlowInMatch;
            string matchId = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyMatchId) ?? "";
            bool midJoinable = OnlineRoomListing.ParseFlag(
                SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyMidJoinable),
                defaultValue: true);
            string joinableRaw = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyJoinable);
            bool joinable = !string.IsNullOrEmpty(joinableRaw)
                ? OnlineRoomListing.ParseFlag(joinableRaw, defaultValue: true)
                : OnlineRoomListing.CanJoin(playerCount, memberLimit, inMatch, midJoinable);

            rooms.Add(new SteamLobbyRoomInfo
            {
                LobbyId = lobby.m_SteamID,
                RoomName = roomName.Trim(),
                PlayerCount = playerCount,
                MemberLimit = memberLimit,
                InMatch = inMatch,
                MidJoinable = midJoinable,
                MatchId = matchId,
                Joinable = joinable,
            });
        }

        LobbyListUpdated?.Invoke(rooms);
    }
    public void ApplyCurrentLobbyMetadata(GameOnlineContext context)
    {
        if (!inited || context == null)
            return;

        if (TransportManager.Instance?.Current is not SteamTransport steamTransport || !steamTransport.InRoom)
            return;

        var lobby = steamTransport.currentLobby;
        string roomName = OnlineRoomListing.ResolveRoomName(context.RoomName, ENetLobbyDisplayType.Steam);
        var presence = NetRoomPresenceSource.Read?.Invoke();
        int playerCount = presence?.PlayerCount ?? steamTransport.GetTempNetPlayerInfos()?.Count ?? 1;
        bool inMatch = presence?.InMatch ?? false;
        string matchId = presence?.MatchId ?? "";
        bool currentlyJoinable = presence?.Joinable
            ?? OnlineRoomListing.CanJoin(playerCount, context.MaxPlayers, false, context.MidJoinable);

        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyGameId, OnlineRoomListing.GameId);
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyRoomName, roomName);
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyJoinable, currentlyJoinable ? "1" : "0");
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyMidJoinable, context.MidJoinable ? "1" : "0");
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyFlow,
            inMatch ? OnlineRoomListing.FlowInMatch : OnlineRoomListing.FlowWaiting);
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyMatchId, matchId);
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyMaxPlayers, context.MaxPlayers.ToString());
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyPlayerCount, playerCount.ToString());
        SteamMatchmaking.SetLobbyData(lobby, "visibility", context.Visibility.ToString());
        // Joinable=false 的大厅不会出现在 RequestLobbyList。满员仍列出并置灰，不公开靠 LobbyType。
        SteamMatchmaking.SetLobbyJoinable(lobby, true);
        SteamMatchmaking.SetLobbyType(lobby, ToSteamLobbyType(context.Visibility));
    }
    public static ELobbyType ToSteamLobbyType(EGameLobbyType type)
    {
        return type switch
        {
            EGameLobbyType.Private => ELobbyType.k_ELobbyTypePrivate,
            EGameLobbyType.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
            _ => ELobbyType.k_ELobbyTypePublic,
        };
    }
    public override void _PhysicsProcess(double delta)
    {
        if (!inited) return;
        SteamAPI.RunCallbacks();
    }
}
