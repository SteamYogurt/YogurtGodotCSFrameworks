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
    public bool Joinable { get; set; } = true;
}

public partial class SteamManager : Singleton<SteamManager>
{
    static AppId_t appId_T = new AppId_t(4453480);
    bool inited = false;
    public bool Inited => inited;

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
        Main.Instance.ClearAllUnimportantUI();
        if(Game.instance != null)
            Game.instance.QueueFree();
        Main.Instance.ShowWaitingPanel();
        NetManager.Instance.Start();
        TransportManager.Instance.UseSteam();
        SteamMatchmaking.JoinLobby(e.m_steamIDLobby);
    }
    public void RequestLobbyList()
    {
        if (!inited)
        {
            LobbyListUpdated?.Invoke(Array.Empty<SteamLobbyRoomInfo>());
            return;
        }

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
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
            string roomName = SteamMatchmaking.GetLobbyData(lobby, "room_name");
            string joinableRaw = SteamMatchmaking.GetLobbyData(lobby, "joinable");
            int playerCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
            int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby);
            bool joinable = joinableRaw != "0";

            if (string.IsNullOrWhiteSpace(roomName))
                roomName = $"Steam Lobby {lobby.m_SteamID}";

            if (memberLimit > 0 && playerCount >= memberLimit)
                joinable = false;

            rooms.Add(new SteamLobbyRoomInfo
            {
                LobbyId = lobby.m_SteamID,
                RoomName = roomName,
                PlayerCount = playerCount,
                MemberLimit = memberLimit,
                Joinable = joinable
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
        string roomName = string.IsNullOrWhiteSpace(context.RoomName) ? "Steam Room" : context.RoomName.Trim();

        SteamMatchmaking.SetLobbyData(lobby, "room_name", roomName);
        SteamMatchmaking.SetLobbyData(lobby, "joinable", context.MidJoinable ? "1" : "0");
        SteamMatchmaking.SetLobbyData(lobby, "max_players", context.MaxPlayers.ToString());
        SteamMatchmaking.SetLobbyData(lobby, "visibility", context.Visibility.ToString());
        SteamMatchmaking.SetLobbyJoinable(lobby, true);
        SteamMatchmaking.SetLobbyType(lobby, ToSteamLobbyType(context.Visibility));
    }
    static ELobbyType ToSteamLobbyType(EGameLobbyType type)
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
