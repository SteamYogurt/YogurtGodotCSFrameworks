using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

public partial class SteamTransport : INetTransport
{
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

    Callback<LobbyEnter_t> lobbyEnterCallback;
    Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
    Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequestCallback;
    Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

    public event Action NetPlayerListChanged;
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
        if (InRoom) SteamMatchmaking.LeaveLobby(currentLobby);
        UnregisterCallbacks();
    }

    public void CreateRoom()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
    }

    public void JoinRoom(string roomId)
    {
        if (ulong.TryParse(roomId, out ulong id))
        {
            SteamMatchmaking.JoinLobby(new CSteamID(id));
        }
    }

    public void LeaveRoom()
    {
        if (!InRoom) return;
        SteamMatchmaking.LeaveLobby(currentLobby);
        InRoom = false;
        ClearP2P();
        RoomStateChanged?.Invoke();
    }

    public bool AmIHost() => HostID == LocalID;

    public void Send(byte[] data, SendType type)
    {
        if (!InRoom) return;

        ulong myID = LocalID;
        ulong hostID = HostID;
        ulong sID = senderID.m_SteamID;

        // 优化：直接遍历 C# 缓存列表，不调用 Native API
        for (int i = 0; i < _cachedMemberIDs.Count; i++)
        {
            ulong memberID = _cachedMemberIDs[i];

            bool shouldSend = type switch
            {
                SendType.AllOthers => memberID != myID,
                SendType.Host => memberID == hostID,
                SendType.JustSender => memberID == sID,
                SendType.OthersExceptSender => memberID != myID && memberID != sID,
                _ => false
            };

            if (shouldSend) Send(data, memberID);
        }
    }

    public void Send(byte[] data, ulong targetSteamID)
    {
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
                        NetManager.Instance.AnalyseStream(data);
                    }

                    SteamNetworkingMessage_t.Release(messages[i]);
                }
            }
        }

        CheckAndUpdatePlayerCount();
    }

    void OnLobbyEnter(LobbyEnter_t e)
    {
        if (e.m_EChatRoomEnterResponse != 1) return;
        currentLobby = new CSteamID(e.m_ulSteamIDLobby);
        InRoom = true;
        UpdateHostID();
        ClearP2P();
        RefreshMemberCache(); // 强制刷新缓存
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