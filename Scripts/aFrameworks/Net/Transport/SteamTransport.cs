using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

public partial class SteamTransport : INetTransport
{
    public int PendingCreateMaxPlayers { get; set; } = 4;
    public bool InRoom { get; private set; }
    public ulong LocalID => SteamUser.GetSteamID().m_SteamID;

    private ulong _cachedHostID;
    public ulong HostID => _cachedHostID;

    // --- 性能优化缓存字段 ---
    private readonly List<ulong> _cachedMemberIDs = new List<ulong>();
    private int roomPlayersCount = 0;
    // -----------------------

    const int P2P_CHANNEL = 0;
    public CSteamID currentLobby;
    CSteamID senderID;
    bool hasRaisedRoomJoined;

    Callback<LobbyEnter_t> lobbyEnterCallback;
    Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
    Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequestCallback;
    Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

    public event Action NetPlayerListChanged;
    public event Action RoomJoined;
    public event Action RoomJoinFailed;
    public event Action RoomStateChanged;
    public event Action HostQuit;

    private void UpdateHostID()
    {
        _cachedHostID = SteamMatchmaking.GetLobbyOwner(currentLobby).m_SteamID;
    }

    void RegisterCallbacks()
    {
        sessionRequestCallback = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    void UnregisterCallbacks()
    {
        lobbyEnterCallback?.Dispose();
        lobbyDataUpdateCallback?.Dispose();
        sessionRequestCallback?.Dispose();
        lobbyChatUpdateCallback?.Dispose();
    }

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
        int maxPlayers = Mathf.Max(PendingCreateMaxPlayers, 1);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
    }

    public void JoinRoom(string roomId)
    {
        if (ulong.TryParse(roomId, out ulong id))
        {
            SteamMatchmaking.JoinLobby(new CSteamID(id));
            return;
        }

        RoomJoinFailed?.Invoke();
    }

    public void LeaveRoom()
    {
        if (!InRoom) return;
        ResetState(true);
    }

    void ResetState(bool notifyRoomStateChanged)
    {
        if (InRoom)
            SteamMatchmaking.LeaveLobby(currentLobby);

        InRoom = false;
        hasRaisedRoomJoined = false;
        currentLobby = default;
        _cachedHostID = 0;
        roomPlayersCount = 0;
        _cachedMemberIDs.Clear();
        ClearP2P();
        if (notifyRoomStateChanged)
            RoomStateChanged?.Invoke();
    }

    public bool AmIHost() => HostID == LocalID;

    public ulong CurrentSenderId => senderID.m_SteamID;

    public void Send(byte[] data, ulong targetSteamID)
    {
        Send(data.AsSpan(), targetSteamID);
    }

    public void Send(ReadOnlySpan<byte> data, ulong targetSteamID)
    {
        if (!InRoom) return;
        if (targetSteamID == 0 || targetSteamID == LocalID) return;

        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID(new CSteamID(targetSteamID));

        unsafe
        {
            fixed (byte* p = data)
            {
                SteamNetworkingMessages.SendMessageToUser(
                    ref identity,
                    (IntPtr)p,
                    (uint)data.Length,
                    Constants.k_nSteamNetworkingSend_ReliableNoNagle,
                    P2P_CHANNEL
                );
            }
        }
    }

    public void SendToAll(byte[] data, ulong excludePeerId = 0)
    {
        SendToAll(data.AsSpan(), excludePeerId);
    }

    public void SendToAll(ReadOnlySpan<byte> data, ulong excludePeerId = 0)
    {
        if (!InRoom) return;

        ulong myID = LocalID;
        if (!AmIHost())
        {
            Send(data, HostID);
            return;
        }

        for (int i = 0; i < _cachedMemberIDs.Count; i++)
        {
            ulong memberID = _cachedMemberIDs[i];
            if (memberID == myID) continue;
            if (excludePeerId != 0 && memberID == excludePeerId) continue;
            Send(data, memberID);
        }
    }

    public void Poll()
    {
        if (!InRoom) return;

        IntPtr[] messages = new IntPtr[64];
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(P2P_CHANNEL, messages, messages.Length);

        if (count > 0)
        {
            unsafe
            {
                for (int i = 0; i < count; i++)
                {
                    // 核心优化：直接指针转换，跳过 Marshal.PtrToStructure
                    SteamNetworkingMessage_t* msgPtr = (SteamNetworkingMessage_t*)messages[i];

                    byte[] data = new byte[msgPtr->m_cbSize];
                    Marshal.Copy(msgPtr->m_pData, data, 0, msgPtr->m_cbSize);

                    senderID = msgPtr->m_identityPeer.GetSteamID();

                    if (senderID.m_SteamID != LocalID)
                    {
                        NetManager.Instance.ProcessIncoming(data);
                    }

                    SteamNetworkingMessage_t.Release(messages[i]);
                }
            }
        }

        CheckAndUpdatePlayerCount();
    }

    void OnLobbyEnter(LobbyEnter_t e)
    {
        if (e.m_EChatRoomEnterResponse != 1)
        {
            RoomJoinFailed?.Invoke();
            return;
        }
        var enteredLobby = new CSteamID(e.m_ulSteamIDLobby);
        bool sameLobbyReenter = InRoom && currentLobby == enteredLobby;

        currentLobby = enteredLobby;
        InRoom = true;
        UpdateHostID();
        ClearP2P();
        RefreshMemberCache(); // 强制刷新缓存

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

    void CheckAndUpdatePlayerCount()
    {
        int currentCount = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        if (roomPlayersCount != currentCount)
        {
            roomPlayersCount = currentCount;
            GD.Print($"[SteamTransport] 人数变动: {currentCount}");

            RefreshMemberCache();
            UpdateHostID();

            if (!IsMemberInCurrentLobby(new CSteamID(HostID)))
            {
                HostQuit?.Invoke();
            }

            NetPlayerListChanged?.Invoke();
            RoomStateChanged?.Invoke();
        }
    }

    // 新增：手动刷新成员列表缓存，避免每帧循环调用 Native API
    private void RefreshMemberCache()
    {
        _cachedMemberIDs.Clear();
        int count = SteamMatchmaking.GetNumLobbyMembers(currentLobby);
        for (int i = 0; i < count; i++)
        {
            _cachedMemberIDs.Add(SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i).m_SteamID);
        }
    }

    void ClearP2P()
    {
        IntPtr[] messages = new IntPtr[32];
        int count;
        do
        {
            count = SteamNetworkingMessages.ReceiveMessagesOnChannel(P2P_CHANNEL, messages, messages.Length);
            for (int i = 0; i < count; i++)
                SteamNetworkingMessage_t.Release(messages[i]);
        } while (count > 0);
    }

    private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t e)
    {
        if (IsMemberInCurrentLobby(e.m_identityRemote.GetSteamID()))
        {
            SteamNetworkingMessages.AcceptSessionWithUser(ref e.m_identityRemote);
        }
    }

    private bool IsMemberInCurrentLobby(CSteamID user)
    {
        return _cachedMemberIDs.Contains(user.m_SteamID);
    }

    void OnLobbyChatUpdate(LobbyChatUpdate_t e)
    {
        if (e.m_ulSteamIDLobby != currentLobby.m_SteamID) return;
        CheckAndUpdatePlayerCount();
    }
}