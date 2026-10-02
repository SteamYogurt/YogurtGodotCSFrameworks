using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

public partial class SteamTransport
{
    const int P2P_CHANNEL = 0;
    const int SendFlags =
        Constants.k_nSteamNetworkingSend_ReliableNoNagle
        | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;
    const int MaxPendingSends = 48;
    const ulong SendRetryIntervalMs = 150;
    const ulong SendExpireMs = 8000;

    readonly List<PendingOutbound> pendingSends = new();

    sealed class PendingOutbound
    {
        public ulong Target;
        public byte[] Data;
        public ulong FirstAttemptMs;
        public ulong NextAttemptMs;
    }

    public void Send(byte[] data, ulong targetSteamID)
    {
        Send(data.AsSpan(), targetSteamID);
    }

    public void Send(ReadOnlySpan<byte> data, ulong targetSteamID)
    {
        if (!InRoom) return;
        if (targetSteamID == 0 || targetSteamID == LocalID) return;
        if (data.IsEmpty) return;

        if (!TrySendNow(data, targetSteamID))
            EnqueueSend(data.ToArray(), targetSteamID);
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

    bool TrySendNow(ReadOnlySpan<byte> data, ulong targetSteamID)
    {
        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID(new CSteamID(targetSteamID));

        EResult result;
        unsafe
        {
            fixed (byte* p = data)
            {
                result = SteamNetworkingMessages.SendMessageToUser(
                    ref identity,
                    (IntPtr)p,
                    (uint)data.Length,
                    SendFlags,
                    P2P_CHANNEL);
            }
        }

        return result == EResult.k_EResultOK;
    }

    void EnqueueSend(byte[] data, ulong targetSteamID)
    {
        if (data == null || data.Length == 0)
            return;
        if (pendingSends.Count >= MaxPendingSends)
        {
            pendingSends.RemoveAt(0);
            GD.PrintErr("[SteamTransport] 发送队列已满，丢弃最早的待发消息");
        }

        ulong now = NowMs();
        pendingSends.Add(new PendingOutbound
        {
            Target = targetSteamID,
            Data = data,
            FirstAttemptMs = now,
            NextAttemptMs = now + SendRetryIntervalMs,
        });
    }

    void FlushPendingSends()
    {
        if (!InRoom || pendingSends.Count == 0)
            return;

        ulong now = NowMs();
        for (int i = pendingSends.Count - 1; i >= 0; i--)
        {
            var pending = pendingSends[i];
            if (now - pending.FirstAttemptMs >= SendExpireMs)
            {
                GD.PrintErr($"[SteamTransport] 发给 {pending.Target} 的消息重试超时");
                pendingSends.RemoveAt(i);
                continue;
            }

            if (now < pending.NextAttemptMs)
                continue;

            if (TrySendNow(pending.Data, pending.Target))
                pendingSends.RemoveAt(i);
            else
                pending.NextAttemptMs = now + SendRetryIntervalMs;
        }
    }

    void ClearPendingSends()
    {
        pendingSends.Clear();
    }

    void ReceiveP2P()
    {
        IntPtr[] messages = new IntPtr[64];
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(P2P_CHANNEL, messages, messages.Length);
        if (count <= 0)
            return;

        unsafe
        {
            for (int i = 0; i < count; i++)
            {
                SteamNetworkingMessage_t* msgPtr = (SteamNetworkingMessage_t*)messages[i];

                byte[] data = new byte[msgPtr->m_cbSize];
                Marshal.Copy(msgPtr->m_pData, data, 0, msgPtr->m_cbSize);

                senderID = msgPtr->m_identityPeer.GetSteamID();
                if (senderID.m_SteamID != LocalID)
                    NetManager.Instance.ProcessIncoming(data);

                SteamNetworkingMessage_t.Release(messages[i]);
            }
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

    void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t e)
    {
        // 进房过程中成员缓存可能还没刷完，主机第一包会先于 LobbyEnter 到达。
        if (!IsAcceptingSessions)
            return;

        var identity = e.m_identityRemote;
        SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
    }
}
