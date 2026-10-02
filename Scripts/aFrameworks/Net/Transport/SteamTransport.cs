using System;
using System.Collections.Generic;
using Godot;
using Steamworks;

public partial class SteamTransport : INetTransport
{
    public int PendingCreateMaxPlayers { get; set; } = 4;
    public ELobbyType PendingCreateLobbyType { get; set; } = ELobbyType.k_ELobbyTypePublic;
    public bool InRoom { get; private set; }
    public ulong LocalID => SteamUser.GetSteamID().m_SteamID;

    /// <summary>进房时记下的原始房主，不跟随 Steam 的 Lobby Owner 转移。</summary>
    private ulong _cachedHostID;
    public ulong HostID => _cachedHostID;

    public string LastRoomFailMessageKey { get; private set; } = "";

    private readonly List<ulong> _cachedMemberIDs = new List<ulong>();
    private int roomPlayersCount = 0;

    public CSteamID currentLobby;
    CSteamID senderID;
    bool hasRaisedRoomJoined;
    bool hostQuitRaised;

    enum PendingRoomOp
    {
        None,
        Create,
        Join
    }

    PendingRoomOp pendingRoomOp;
    ulong pendingJoinLobbyId;
    ulong pendingCreatedLobbyId;
    ulong pendingOpStartedMs;
    ulong lobbyApiIssuedMs;
    bool lobbyApiIssued;

    const ulong RelayWaitTimeoutMs = 12000;
    const ulong LobbyWaitTimeoutMs = 15000;
    const ulong RelayRetryIntervalMs = 2000;
    ulong lastRelayRetryMs;

    Callback<LobbyEnter_t> lobbyEnterCallback;
    Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
    Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequestCallback;
    Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    CallResult<LobbyCreated_t> lobbyCreatedCallResult;

    public event Action NetPlayerListChanged;
    public event Action RoomJoined;
    public event Action RoomJoinFailed;
    public event Action RoomStateChanged;
    public event Action HostQuit;

    bool IsAcceptingSessions => InRoom || pendingRoomOp != PendingRoomOp.None;

    public List<INetTransportPlayerInfo> GetTempNetPlayerInfos()
    {
        if (!InRoom) return null;

        var list = new List<INetTransportPlayerInfo>(_cachedMemberIDs.Count);
        foreach (var id in _cachedMemberIDs)
        {
            list.Add(new INetTransportPlayerInfo
            {
                id = id,
                name = SteamFriends.GetFriendPersonaName(new CSteamID(id))
            });
        }
        return list;
    }

    public void Init()
    {
        GD.Print("[SteamTransport] Init");
        ResetState(false);
        if (!SteamManager.Instance.Inited)
        {
            GD.PrintErr("[SteamTransport] Init Failed");
            return;
        }
        SteamManager.Instance.EnsureRelayNetworkAccess();
        RegisterCallbacks();
    }

    public void Free()
    {
        GD.Print("[SteamTransport] Free");
        ResetState(false);
        UnregisterCallbacks();
    }

    public void CreateRoom()
    {
        LastRoomFailMessageKey = "";
        if (SteamManager.Instance == null || !SteamManager.Instance.Inited)
        {
            FailPendingRoomOp("创建房间失败");
            return;
        }

        BeginPendingRoomOp(PendingRoomOp.Create, 0);
    }

    public void JoinRoom(string roomId)
    {
        LastRoomFailMessageKey = "";
        if (SteamManager.Instance == null || !SteamManager.Instance.Inited)
        {
            FailPendingRoomOp("加入房间失败");
            return;
        }

        if (!ulong.TryParse(roomId, out ulong id) || id == 0)
        {
            FailPendingRoomOp("加入房间失败");
            return;
        }

        BeginPendingRoomOp(PendingRoomOp.Join, id);
    }

    public void LeaveRoom()
    {
        if (!InRoom && pendingRoomOp == PendingRoomOp.None) return;
        ResetState(true);
    }

    void BeginPendingRoomOp(PendingRoomOp op, ulong joinLobbyId)
    {
        pendingRoomOp = op;
        pendingJoinLobbyId = joinLobbyId;
        pendingCreatedLobbyId = 0;
        pendingOpStartedMs = NowMs();
        lobbyApiIssued = false;
        lobbyApiIssuedMs = 0;
        lastRelayRetryMs = 0;
        SteamManager.Instance?.EnsureRelayNetworkAccess();
    }

    void ResetState(bool notifyRoomStateChanged)
    {
        pendingRoomOp = PendingRoomOp.None;
        pendingJoinLobbyId = 0;
        pendingCreatedLobbyId = 0;
        lobbyApiIssued = false;
        ClearPendingSends();

        if (InRoom)
            SteamMatchmaking.LeaveLobby(currentLobby);

        InRoom = false;
        hasRaisedRoomJoined = false;
        hostQuitRaised = false;
        currentLobby = default;
        _cachedHostID = 0;
        roomPlayersCount = 0;
        _cachedMemberIDs.Clear();
        ClearP2P();
        if (notifyRoomStateChanged)
            RoomStateChanged?.Invoke();
    }

    public bool AmIHost() => HostID != 0 && HostID == LocalID;

    public ulong CurrentSenderId => senderID.m_SteamID;

    public void Poll()
    {
        TickPendingRoomOp();
        FlushPendingSends();
        if (!InRoom) return;

        ReceiveP2P();
        CheckAndUpdatePlayerCount();
    }

    void TickPendingRoomOp()
    {
        if (pendingRoomOp == PendingRoomOp.None || InRoom)
            return;

        ulong now = NowMs();
        var steam = SteamManager.Instance;
        if (steam == null || !steam.Inited)
        {
            FailPendingRoomOp(pendingRoomOp == PendingRoomOp.Create ? "创建房间失败" : "加入房间失败");
            return;
        }

        if (!steam.IsRelayNetworkReady)
        {
            if (now - pendingOpStartedMs >= RelayWaitTimeoutMs)
            {
                FailPendingRoomOp("Steam 网络连接超时");
                return;
            }

            if (lastRelayRetryMs == 0 || now - lastRelayRetryMs >= RelayRetryIntervalMs)
            {
                lastRelayRetryMs = now;
                steam.EnsureRelayNetworkAccess();
            }
            return;
        }

        if (!lobbyApiIssued)
        {
            lobbyApiIssued = true;
            lobbyApiIssuedMs = now;
            IssueLobbyApi();
            return;
        }

        if (now - lobbyApiIssuedMs >= LobbyWaitTimeoutMs)
        {
            FailPendingRoomOp(pendingRoomOp == PendingRoomOp.Create ? "创建房间失败" : "加入房间失败");
        }
    }

    void IssueLobbyApi()
    {
        if (pendingRoomOp == PendingRoomOp.Create)
        {
            int maxPlayers = Mathf.Max(PendingCreateMaxPlayers, 1);
            var apiCall = SteamMatchmaking.CreateLobby(PendingCreateLobbyType, maxPlayers);
            lobbyCreatedCallResult?.Set(apiCall);
            return;
        }

        if (pendingRoomOp == PendingRoomOp.Join)
            SteamMatchmaking.JoinLobby(new CSteamID(pendingJoinLobbyId));
    }

    void FailPendingRoomOp(string messageKey)
    {
        LastRoomFailMessageKey = messageKey ?? "无法进入房间";
        if (pendingCreatedLobbyId != 0)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(pendingCreatedLobbyId));
            pendingCreatedLobbyId = 0;
        }
        pendingRoomOp = PendingRoomOp.None;
        pendingJoinLobbyId = 0;
        lobbyApiIssued = false;
        RoomJoinFailed?.Invoke();
    }

    void RegisterCallbacks()
    {
        sessionRequestCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        lobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
    }

    void UnregisterCallbacks()
    {
        lobbyEnterCallback?.Dispose();
        lobbyDataUpdateCallback?.Dispose();
        sessionRequestCallback?.Dispose();
        lobbyChatUpdateCallback?.Dispose();
        lobbyCreatedCallResult?.Dispose();
        lobbyEnterCallback = null;
        lobbyDataUpdateCallback = null;
        sessionRequestCallback = null;
        lobbyChatUpdateCallback = null;
        lobbyCreatedCallResult = null;
    }

    void OnLobbyCreated(LobbyCreated_t e, bool ioFailure)
    {
        if (pendingRoomOp != PendingRoomOp.Create)
            return;

        if (ioFailure || e.m_eResult != EResult.k_EResultOK)
        {
            GD.PrintErr($"[SteamTransport] CreateLobby failed: ioFailure={ioFailure} result={e.m_eResult}");
            FailPendingRoomOp("创建房间失败");
            return;
        }

        pendingCreatedLobbyId = e.m_ulSteamIDLobby;
    }

    void OnLobbyEnter(LobbyEnter_t e)
    {
        if (e.m_EChatRoomEnterResponse != 1)
        {
            FailPendingRoomOp(pendingRoomOp == PendingRoomOp.Create ? "创建房间失败" : "加入房间失败");
            return;
        }
        var enteredLobby = new CSteamID(e.m_ulSteamIDLobby);
        bool sameLobbyReenter = InRoom && currentLobby == enteredLobby;

        currentLobby = enteredLobby;
        InRoom = true;
        pendingRoomOp = PendingRoomOp.None;
        pendingJoinLobbyId = 0;
        pendingCreatedLobbyId = 0;
        lobbyApiIssued = false;
        // 进房当下的 Owner 即本局原始主机；之后 Steam 转移 Owner 也不改 HostID。
        _cachedHostID = SteamMatchmaking.GetLobbyOwner(currentLobby).m_SteamID;
        RefreshMemberCache();

        if (!sameLobbyReenter && !hasRaisedRoomJoined)
        {
            hasRaisedRoomJoined = true;
            RoomJoined?.Invoke();
        }

        RoomStateChanged?.Invoke();
    }

    void OnLobbyDataUpdate(LobbyDataUpdate_t e)
    {
        if (e.m_ulSteamIDLobby != currentLobby.m_SteamID) return;
        CheckAndUpdatePlayerCount();
    }

    void OnLobbyChatUpdate(LobbyChatUpdate_t e)
    {
        if (e.m_ulSteamIDLobby != currentLobby.m_SteamID) return;
        CheckAndUpdatePlayerCount();

        const uint leftMask =
            (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft
            | (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
            | (uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked
            | (uint)EChatMemberStateChange.k_EChatMemberStateChangeBanned;
        if ((e.m_rgfChatMemberStateChange & leftMask) == 0)
            return;
        if (e.m_ulSteamIDUserChanged == _cachedHostID)
            NotifyOriginalHostLeft();
    }

    void CheckAndUpdatePlayerCount()
    {
        if (!InRoom) return;

        int currentCount = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        if (roomPlayersCount != currentCount)
        {
            roomPlayersCount = currentCount;
            GD.Print($"[SteamTransport] 人数变动: {currentCount}");

            RefreshMemberCache();

            if (hasRaisedRoomJoined
                && currentCount > 0
                && _cachedHostID != 0
                && _cachedHostID != LocalID
                && !_cachedMemberIDs.Contains(_cachedHostID))
            {
                NotifyOriginalHostLeft();
                return;
            }

            NetPlayerListChanged?.Invoke();
            RoomStateChanged?.Invoke();
        }
    }

    void NotifyOriginalHostLeft()
    {
        if (hostQuitRaised || _cachedHostID == 0 || _cachedHostID == LocalID)
            return;

        hostQuitRaised = true;
        LastRoomFailMessageKey = "主机已退出";
        GD.Print("[SteamTransport] 原始主机已离开");
        HostQuit?.Invoke();
        ResetState(true);
    }

    void RefreshMemberCache()
    {
        _cachedMemberIDs.Clear();
        int count = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < count; i++)
        {
            _cachedMemberIDs.Add(SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i).m_SteamID);
        }
    }

    static ulong NowMs() => Time.GetTicksMsec();
}
