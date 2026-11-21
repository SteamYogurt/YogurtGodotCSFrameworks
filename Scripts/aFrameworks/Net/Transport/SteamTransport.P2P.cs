using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Godot;
using Steamworks;

public partial class SteamTransport
{
    private const int P2PChannel = 0;
    private const int SendFlags = Constants.k_nSteamNetworkingSend_ReliableNoNagle;
    private const uint HandshakeMagic = ((uint)NetWireLimits.ProtocolVersion << 24) | 0x0050544E;
    private const int HandshakeBytes = 21;
    private const byte Hello = 1;
    private const byte Welcome = 2;
    private const byte Rejected = 3;
    private const byte RejectedBlocked = 4;
    private const ulong RetryIntervalMs = 50;
    private const ulong RetryTimeoutMs = 8000;

    private readonly IntPtr[] incomingMessages = new IntPtr[64];
    private readonly HashSet<ulong> readyPeers = new();
    private readonly HashSet<ulong> failedPeers = new();
    private readonly Dictionary<ulong, PendingPeerSends> pendingSends = new();
    private readonly List<ulong> retryPeers = new();
    private ulong failedHost;
    private bool peerListDirty;

    private sealed class PendingPeerSends
    {
        public readonly Queue<(byte[] Data, ulong EnqueuedAt)> Packets = new();
        public int Bytes;
        public ulong RetryAt;
    }

    public ulong CurrentSenderId { get; private set; }

    public void Send(byte[] data, ulong peerId) => Send(data.AsSpan(), peerId);

    public void Send(ReadOnlySpan<byte> data, ulong peerId)
    {
        if (InRoom && (AmIHost() ? readyPeers.Contains(peerId) : peerId == HostID))
            QueueOrSend(data, peerId);
    }

    public void SendToAll(byte[] data, ulong excludePeerId = 0) =>
        SendToAll(data.AsSpan(), excludePeerId);

    public void SendToAll(ReadOnlySpan<byte> data, ulong excludePeerId = 0)
    {
        if (!InRoom)
            return;
        if (!AmIHost())
        {
            Send(data, HostID);
            return;
        }

        SendToSelected(data, null, excludePeerId);
    }

    public bool IsPeerConnected(ulong peerId) =>
        AmIHost() && readyPeers.Contains(peerId) && !failedPeers.Contains(peerId);

    public void SendToSelected(ReadOnlySpan<byte> data, Predicate<ulong> include, ulong excludePeerId = 0)
    {
        if (!AmIHost())
            return;
        foreach (ulong peerId in readyPeers)
            if (peerId != excludePeerId && (include == null || include(peerId)))
                Send(data, peerId);
    }

    private bool QueueOrSend(ReadOnlySpan<byte> data, ulong peerId)
    {
        if (data.IsEmpty || data.Length > NetWireLimits.MaxPacketBytes
            || failedPeers.Contains(peerId) || peerId == failedHost)
            return false;

        ulong now = NowMs();
        if (!pendingSends.TryGetValue(peerId, out var pending))
        {
            var result = SendNow(data, peerId);
            if (result == EResult.k_EResultOK)
                return true;
            if (result != EResult.k_EResultLimitExceeded)
            {
                FailPeer(peerId, result.ToString());
                return false;
            }
            pending = new PendingPeerSends { RetryAt = now + RetryIntervalMs };
            pendingSends.Add(peerId, pending);
        }

        if (pending.Bytes + data.Length > NetWireLimits.MaxQueuedBytes
            || pending.Packets.Count >= NetWireLimits.MaxQueuedPackets)
        {
            FailPeer(peerId, "发送队列已满");
            return false;
        }
        pending.Packets.Enqueue((data.ToArray(), now));
        pending.Bytes += data.Length;
        return true;
    }

    private static unsafe EResult SendNow(ReadOnlySpan<byte> data, ulong peerId)
    {
        var identity = MakeIdentity(peerId);
        fixed (byte* bytes = data)
            return SteamNetworkingMessages.SendMessageToUser(
                ref identity, (IntPtr)bytes, (uint)data.Length, SendFlags, P2PChannel);
    }

    private void FlushPendingSends()
    {
        if (pendingSends.Count == 0)
            return;

        ulong now = NowMs();
        retryPeers.Clear();
        retryPeers.AddRange(pendingSends.Keys);
        foreach (ulong peerId in retryPeers)
        {
            var pending = pendingSends[peerId];
            if (now < pending.RetryAt)
                continue;

            for (int i = 0; i < 32 && pending.Packets.TryPeek(out var packet); i++)
            {
                if (now - packet.EnqueuedAt >= RetryTimeoutMs)
                {
                    FailPeer(peerId, "可靠消息发送超时");
                    break;
                }
                var result = SendNow(packet.Data, peerId);
                if (result == EResult.k_EResultLimitExceeded)
                {
                    pending.RetryAt = now + RetryIntervalMs;
                    break;
                }
                if (result != EResult.k_EResultOK)
                {
                    FailPeer(peerId, result.ToString());
                    break;
                }
                pending.Packets.Dequeue();
                pending.Bytes -= packet.Data.Length;
            }
            if (pending.Packets.Count == 0)
                pendingSends.Remove(peerId);
        }
    }

    private static SteamNetworkingIdentity MakeIdentity(ulong peerId)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(new CSteamID(peerId));
        return identity;
    }

    private void FailPeer(ulong peerId, string reason)
    {
        if (!(AmIHost() ? failedPeers.Add(peerId) : peerId == HostID && failedHost == 0))
            return;

        GD.PrintErr($"[SteamTransport] {peerId} 的连接失败: {reason}");
        ClosePeerSession(peerId);
        if (AmIHost())
            peerListDirty = true;
        else
            failedHost = peerId;
    }

    private void ClosePeerSession(ulong peerId)
    {
        pendingSends.Remove(peerId);
        var identity = MakeIdentity(peerId);
        SteamNetworkingMessages.CloseSessionWithUser(ref identity);
    }

    private unsafe void ReceiveP2P()
    {
        int version = roomVersion;
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(P2PChannel, incomingMessages, incomingMessages.Length);
        try
        {
            for (int i = 0; i < count && version == roomVersion; i++)
            {
                var message = (SteamNetworkingMessage_t*)incomingMessages[i];
                ulong peerId = message->m_identityPeer.GetSteamID().m_SteamID;
                if (message->m_cbSize <= 0 || message->m_cbSize > NetWireLimits.MaxPacketBytes
                    || !CanTalkTo(peerId))
                    continue;

                var data = new ReadOnlySpan<byte>((void*)message->m_pData, message->m_cbSize);
                if (data.Length == HandshakeBytes && BinaryPrimitives.ReadUInt32LittleEndian(data) == HandshakeMagic)
                {
                    ReadHandshake(peerId, data);
                    continue;
                }
                if (!InRoom || (AmIHost() && !readyPeers.Contains(peerId)))
                    continue;

                CurrentSenderId = peerId;
                try
                {
                    NetManager.Instance?.ProcessIncoming(data);
                }
                finally
                {
                    CurrentSenderId = 0;
                }
            }
        }
        finally
        {
            for (int i = 0; i < count; i++)
                SteamNetworkingMessage_t.Release(incomingMessages[i]);
        }
    }

    private void SendHandshake(ulong peerId, byte kind, ulong nonce)
    {
        Span<byte> data = stackalloc byte[HandshakeBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(data, HandshakeMagic);
        data[4] = kind;
        BinaryPrimitives.WriteUInt64LittleEndian(data[5..], currentLobby.m_SteamID);
        BinaryPrimitives.WriteUInt64LittleEndian(data[13..], nonce);
        QueueOrSend(data, peerId);
    }

    private void ReadHandshake(ulong peerId, ReadOnlySpan<byte> data)
    {
        if (BinaryPrimitives.ReadUInt64LittleEndian(data[5..]) != currentLobby.m_SteamID)
            return;

        ulong nonce = BinaryPrimitives.ReadUInt64LittleEndian(data[13..]);
        if (AmIHost() && data[4] == Hello && !readyPeers.Contains(peerId))
        {
            if (blockedPeers.Contains(peerId))
            {
                SendHandshake(peerId, RejectedBlocked, nonce);
                return;
            }
            var presence = NetRoomPresenceSource.Read?.Invoke();
            if (kickedMembers.Contains(peerId)
                || readyPeers.Count + 1 >= Math.Max(1, PendingCreateMaxPlayers)
                || presence?.AllowsJoining == false)
            {
                SendHandshake(peerId, Rejected, nonce);
                return;
            }
            SendHandshake(peerId, Welcome, nonce);
            if (!failedPeers.Contains(peerId))
            {
                readyPeers.Add(peerId);
                // Notify now, before dispatching any later application packet from this peer.
                NotifyPlayersChanged();
            }
        }
        else if (!AmIHost() && !InRoom && nonce == joinNonce)
        {
            if (data[4] == Welcome)
                CompleteRoomJoin();
            else if (data[4] == Rejected)
                FailPendingRoomOp("无法进入房间");
            else if (data[4] == RejectedBlocked)
                FailPendingRoomOp("房主已禁止你重新加入此房间", blocked: true);
        }
    }

    private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t e)
    {
        var identity = e.m_identityRemote;
        if (CanTalkTo(identity.GetSteamID().m_SteamID))
            SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
    }

    private void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t e)
    {
        ulong peerId = e.m_info.m_identityRemote.GetSteamID().m_SteamID;
        if (CanTalkTo(peerId))
            FailPeer(peerId, e.m_info.m_eEndReason.ToString());
    }
}
