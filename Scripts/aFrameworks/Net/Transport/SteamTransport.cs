using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Godot;
using Steamworks;

public partial class SteamTransport : INetTransport
{
    private enum RoomOperation { None, Create, Join }
    private const ulong RelayWaitTimeoutMs = 12000;
    private const ulong LobbyWaitTimeoutMs = 15000;
    private const ulong HandshakeTimeoutMs = 12000;
    private const ulong MemberRefreshIntervalMs = 500;
    private const uint LobbyKickMagic = ((uint)NetWireLimits.ProtocolVersion << 24) | 0x4B434B;
    private const int LobbyKickBytes = sizeof(uint) + sizeof(ulong);

    private readonly HashSet<ulong> members = new();
    private readonly HashSet<ulong> previousMembers = new();
    private readonly HashSet<ulong> blockedPeers = new();
    private readonly HashSet<ulong> kickedMembers = new();
    private readonly byte[] lobbyChatBuffer = new byte[4096];
    private RoomOperation pendingOperation;
    private ulong pendingLobby;
    private ulong deadline;
    private ulong nextRelayRetry;
    private ulong nextMemberRefresh;
    private ulong joinNonce;
    private bool lobbyApiIssued;
    private bool localKickPending;
    private int roomVersion;
    private (int Players, int MaxPlayers, bool Joinable, bool MidJoinable, bool InMatch, string MatchId)? publishedPresence;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    private Callback<LobbyChatMsg_t> lobbyChatMessageCallback;
    private Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequestCallback;
    private Callback<SteamNetworkingMessagesSessionFailed_t> sessionFailedCallback;

    public int PendingCreateMaxPlayers { get; set; } = 4;
    public bool PendingCreateMidJoinable { get; set; } = true;
    public ELobbyType PendingCreateLobbyType { get; set; } = ELobbyType.k_ELobbyTypePublic;
    public bool InRoom { get; private set; }
    public ulong LocalID => SteamUser.GetSteamID().m_SteamID;
    public ulong HostID { get; private set; }
    public CSteamID currentLobby { get; private set; }
    public string LastRoomFailMessageKey { get; private set; } = "";
    public bool LastJoinWasBlocked { get; private set; }

    public event Action NetPlayerListChanged;
    public event Action RoomJoined;
    public event Action RoomJoinFailed;
    public event Action RoomStateChanged;
    public event Action HostQuit;
    public event Action Kicked;

    public bool AmIHost() => HostID != 0 && HostID == LocalID;

    public List<INetTransportPlayerInfo> GetTempNetPlayerInfos()
    {
        if (!InRoom)
            return null;

        var list = new List<INetTransportPlayerInfo>(members.Count);
        if (AmIHost())
        {
            list.Add(PlayerInfo(LocalID));
            foreach (ulong peerId in readyPeers)
                if (!failedPeers.Contains(peerId))
                    list.Add(PlayerInfo(peerId));
        }
        else
        {
            foreach (ulong peerId in members)
                list.Add(PlayerInfo(peerId));
        }
        return list;
    }

    private static INetTransportPlayerInfo PlayerInfo(ulong id) =>
        new() { id = id, name = SteamFriends.GetFriendPersonaName(new CSteamID(id)) };

    public void Init()
    {
        if (SteamManager.Instance?.Inited != true)
            return;
        SteamManager.Instance.EnsureRelayNetworkAccess();
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        lobbyChatMessageCallback = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
        sessionRequestCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        sessionFailedCallback = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
    }

    public void Free()
    {
        ResetState();
        lobbyChatUpdateCallback?.Dispose();
        lobbyChatMessageCallback?.Dispose();
        sessionRequestCallback?.Dispose();
        sessionFailedCallback?.Dispose();
    }

    public void CreateRoom() => BeginRoomOperation(RoomOperation.Create, 0);

    public void JoinRoom(string roomId)
    {
        LastJoinWasBlocked = false;
        if (!ulong.TryParse(roomId, out ulong id) || id == 0)
        {
            FailPendingRoomOp("加入房间失败");
            return;
        }
        BeginRoomOperation(RoomOperation.Join, id);
    }

    public void LeaveRoom()
    {
        if (currentLobby.m_SteamID == 0 && pendingOperation == RoomOperation.None)
            return;
        ResetState();
        RoomStateChanged?.Invoke();
    }

    private void BeginRoomOperation(RoomOperation operation, ulong lobbyId)
    {
        ResetState();
        LastRoomFailMessageKey = "";
        LastJoinWasBlocked = false;
        pendingOperation = operation;
        pendingLobby = lobbyId;
        deadline = NowMs() + RelayWaitTimeoutMs;
        nextRelayRetry = 0;
        SteamManager.Instance?.EnsureRelayNetworkAccess();
    }

    private void ResetState()
    {
        roomVersion++;
        if (currentLobby.m_SteamID != 0)
        {
            ulong local = LocalID;
            foreach (ulong peerId in members)
                if (peerId != local)
                    ClosePeerSession(peerId);
            SteamMatchmaking.LeaveLobby(currentLobby);
            // Reset can be called from inside a receive callback. Leave that batch
            // untouched so its native messages are released by its own finally.
            var discarded = new IntPtr[incomingMessages.Length];
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(P2PChannel, discarded, discarded.Length);
            for (int i = 0; i < count; i++)
                SteamNetworkingMessage_t.Release(discarded[i]);
        }
        pendingSends.Clear();
        readyPeers.Clear();
        failedPeers.Clear();
        members.Clear();
        previousMembers.Clear();
        blockedPeers.Clear();
        kickedMembers.Clear();
        pendingOperation = RoomOperation.None;
        lobbyApiIssued = false;
        peerListDirty = false;
        localKickPending = false;
        publishedPresence = null;
        InRoom = false;
        currentLobby = default;
        HostID = failedHost = CurrentSenderId = 0;
    }

    public void Poll()
    {
        if (localKickPending)
        {
            ResetState();
            Kicked?.Invoke();
            return;
        }

        TickRoomOperation();
        if (currentLobby.m_SteamID == 0)
            return;

        ulong now = NowMs();
        if (now >= nextMemberRefresh)
        {
            RefreshMemberCache();
            if (AmIHost())
                PublishPresence();
        }
        if (!AmIHost() && (!members.Contains(HostID) || failedHost != 0))
        {
            if (!InRoom)
                FailPendingRoomOp("无法进入房间");
            else
            {
                LastRoomFailMessageKey = "主机已退出";
                ResetState();
                HostQuit?.Invoke();
            }
            return;
        }

        if (peerListDirty)
        {
            peerListDirty = false;
            readyPeers.ExceptWith(failedPeers);
            int version = roomVersion;
            NotifyPlayersChanged();
            if (version != roomVersion)
                return;
        }
        FlushPendingSends();
        ReceiveP2P();
    }

    private void TickRoomOperation()
    {
        if (pendingOperation == RoomOperation.None)
            return;

        var steam = SteamManager.Instance;
        ulong now = NowMs();
        if (steam?.Inited != true || now >= deadline)
        {
            FailPendingRoomOp(steam?.IsRelayNetworkReady == false
                ? "Steam 网络连接超时"
                : pendingOperation == RoomOperation.Create ? "创建房间失败" : "加入房间失败");
            return;
        }
        if (lobbyApiIssued)
            return;
        if (!steam.IsRelayNetworkReady)
        {
            if (now >= nextRelayRetry)
            {
                nextRelayRetry = now + 2000;
                steam.EnsureRelayNetworkAccess();
            }
            return;
        }

        lobbyApiIssued = true;
        deadline = now + LobbyWaitTimeoutMs;
        int version = roomVersion;
        if (pendingOperation == RoomOperation.Create)
        {
            var call = SteamMatchmaking.CreateLobby(PendingCreateLobbyType, Math.Clamp(PendingCreateMaxPlayers, 1, 250));
            CallResult<LobbyCreated_t> result = null;
            result = CallResult<LobbyCreated_t>.Create((e, ioFailure) =>
            {
                result.Dispose();
                bool success = !ioFailure && e.m_eResult == EResult.k_EResultOK;
                HandleLobbyResult(version, success, e.m_ulSteamIDLobby, creating: true);
            });
            result.Set(call);
        }
        else
        {
            var call = SteamMatchmaking.JoinLobby(new CSteamID(pendingLobby));
            CallResult<LobbyEnter_t> result = null;
            result = CallResult<LobbyEnter_t>.Create((e, ioFailure) =>
            {
                result.Dispose();
                bool success = !ioFailure && e.m_EChatRoomEnterResponse == 1;
                HandleLobbyResult(version, success, e.m_ulSteamIDLobby, creating: false);
            });
            result.Set(call);
        }
    }

    private void HandleLobbyResult(int version, bool success, ulong lobbyId, bool creating)
    {
        var lobby = new CSteamID(lobbyId);
        if (version != roomVersion)
        {
            // Steam cannot cancel lobby API calls. A late success can still create membership.
            if (success && !IsCurrentOrPendingLobby(lobbyId))
                SteamMatchmaking.LeaveLobby(lobby);
            return;
        }
        if (!success)
        {
            FailPendingRoomOp(creating ? "创建房间失败" : "加入房间失败");
            return;
        }

        currentLobby = lobby;
        HostID = creating ? LocalID : SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
        RefreshMemberCache();
        if (creating)
        {
            SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyGameId, OnlineRoomListing.GameId);
            SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyProtocolVersion, OnlineRoomListing.ProtocolVersion);
            CompleteRoomJoin();
            return;
        }
        if (!OnlineRoomListing.IsOurGame(SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyGameId))
            || SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyProtocolVersion) != OnlineRoomListing.ProtocolVersion)
        {
            FailPendingRoomOp("无法进入房间");
            return;
        }
        if (HostID == 0 || HostID == LocalID || !members.Contains(HostID))
        {
            FailPendingRoomOp("无法进入房间");
            return;
        }

        joinNonce = (ulong)Random.Shared.NextInt64(1, long.MaxValue);
        deadline = NowMs() + HandshakeTimeoutMs;
        SendHandshake(HostID, Hello, joinNonce);
    }

    private bool IsCurrentOrPendingLobby(ulong lobbyId)
    {
        var current = TransportManager.Instance?.Current as SteamTransport;
        return current != null && (current.currentLobby.m_SteamID == lobbyId
            || (current.pendingOperation == RoomOperation.Join && current.pendingLobby == lobbyId));
    }

    private void CompleteRoomJoin()
    {
        InRoom = true;
        pendingOperation = RoomOperation.None;
        int version = roomVersion;
        RoomJoined?.Invoke();
        if (version == roomVersion)
            RoomStateChanged?.Invoke();
    }

    private void FailPendingRoomOp(string messageKey, bool blocked = false)
    {
        LastRoomFailMessageKey = messageKey;
        LastJoinWasBlocked = blocked;
        ResetState();
        RoomJoinFailed?.Invoke();
    }

    private bool CanTalkTo(ulong peerId)
    {
        if (currentLobby.m_SteamID == 0 || peerId == 0 || peerId == LocalID)
            return false;
        if (!AmIHost())
            return peerId == HostID && failedHost == 0;
        if (!members.Contains(peerId))
            RefreshMemberCache();
        return members.Contains(peerId) && !failedPeers.Contains(peerId);
    }

    public bool KickPeer(ulong peerId, bool blockRejoin)
    {
        if (!IsPeerConnected(peerId))
            return false;

        if (blockRejoin)
            blockedPeers.Add(peerId);
        kickedMembers.Add(peerId);
        readyPeers.Remove(peerId);
        ClosePeerSession(peerId);
        peerListDirty = true;

        // Lobby chat can deliver the exit notice after the P2P connection is closed.
        BinaryPrimitives.WriteUInt32LittleEndian(lobbyChatBuffer, LobbyKickMagic);
        BinaryPrimitives.WriteUInt64LittleEndian(lobbyChatBuffer.AsSpan(sizeof(uint)), peerId);
        if (!SteamMatchmaking.SendLobbyChatMsg(currentLobby, lobbyChatBuffer, LobbyKickBytes))
            GD.PushWarning("[SteamTransport] 无法发送踢出通知，游戏连接已移除。");
        return true;
    }

    public bool TryOpenInviteDialog()
    {
        if (!InRoom || !AmIHost() || !SteamUtils.IsOverlayEnabled())
            return false;

        SteamFriends.ActivateGameOverlayInviteDialog(currentLobby);
        return true;
    }

    private void OnLobbyChatMessage(LobbyChatMsg_t message)
    {
        if (currentLobby.m_SteamID == 0 || message.m_ulSteamIDLobby != currentLobby.m_SteamID
            || HostID == 0 || AmIHost() || message.m_ulSteamIDUser != HostID)
            return;

        int size = SteamMatchmaking.GetLobbyChatEntry(currentLobby, (int)message.m_iChatID,
            out var sender, lobbyChatBuffer, lobbyChatBuffer.Length, out var entryType);
        if (size != LobbyKickBytes || sender.m_SteamID != HostID
            || entryType != EChatEntryType.k_EChatEntryTypeChatMsg
            || BinaryPrimitives.ReadUInt32LittleEndian(lobbyChatBuffer) != LobbyKickMagic
            || BinaryPrimitives.ReadUInt64LittleEndian(lobbyChatBuffer.AsSpan(sizeof(uint))) != LocalID)
            return;

        localKickPending = true;
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t e)
    {
        if (e.m_ulSteamIDLobby != currentLobby.m_SteamID)
            return;
        if (InRoom && !AmIHost())
            peerListDirty = true;
        const uint leftMask = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft
            | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
            | EChatMemberStateChange.k_EChatMemberStateChangeKicked
            | EChatMemberStateChange.k_EChatMemberStateChangeBanned);
        if ((e.m_rgfChatMemberStateChange & leftMask) != 0)
        {
            ulong peerId = e.m_ulSteamIDUserChanged;
            ClosePeerSession(peerId);
            members.Remove(peerId);
            failedPeers.Remove(peerId);
            kickedMembers.Remove(peerId);
            if (readyPeers.Remove(peerId))
                peerListDirty = true;
            if (peerId == HostID && !AmIHost())
                failedHost = peerId;
        }
        nextMemberRefresh = 0;
    }

    private void RefreshMemberCache()
    {
        nextMemberRefresh = NowMs() + MemberRefreshIntervalMs;
        previousMembers.Clear();
        previousMembers.UnionWith(members);
        members.Clear();
        int count = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < count; i++)
        {
            ulong memberId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i).m_SteamID;
            if (memberId != 0)
                members.Add(memberId);
        }
        previousMembers.ExceptWith(members);
        foreach (ulong departed in previousMembers)
        {
            if (departed != LocalID)
                ClosePeerSession(departed);
            failedPeers.Remove(departed);
            kickedMembers.Remove(departed);
            if (readyPeers.Remove(departed))
                peerListDirty = true;
        }
    }

    private void NotifyPlayersChanged()
    {
        int version = roomVersion;
        NetPlayerListChanged?.Invoke();
        if (version == roomVersion)
            RoomStateChanged?.Invoke();
    }

    internal void PublishPresence()
    {
        if (!InRoom || !AmIHost())
            return;
        var presence = NetRoomPresenceSource.Read?.Invoke();
        var state = (
            Players: presence?.PlayerCount ?? readyPeers.Count + 1,
            MaxPlayers: presence?.MaxPlayers ?? PendingCreateMaxPlayers,
            Joinable: presence?.AllowsJoining ?? readyPeers.Count + 1 < PendingCreateMaxPlayers,
            MidJoinable: presence?.MidJoinable ?? PendingCreateMidJoinable,
            InMatch: presence?.InMatch ?? false,
            MatchId: presence?.MatchId ?? "");
        if (publishedPresence.HasValue && publishedPresence.Value == state)
            return;

        // Keep the lobby listable even while the game rejects new peers.
        publishedPresence = state;
        SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyPlayerCount, state.Players.ToString());
        SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyMaxPlayers, state.MaxPlayers.ToString());
        SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyJoinable, state.Joinable ? "1" : "0");
        SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyMidJoinable, state.MidJoinable ? "1" : "0");
        SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyFlow,
            state.InMatch ? OnlineRoomListing.FlowInMatch : OnlineRoomListing.FlowWaiting);
        SteamMatchmaking.SetLobbyData(currentLobby, OnlineRoomListing.KeyMatchId, state.MatchId);
    }

    private static ulong NowMs() => Time.GetTicksMsec();
}
