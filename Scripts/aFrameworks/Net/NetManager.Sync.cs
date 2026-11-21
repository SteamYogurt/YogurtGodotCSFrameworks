using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;

public partial class NetManager
{
    private readonly NetMatchPeerAccess matchAccess = new();

    /// <summary>主机在生成对局对象前启用。默认继续兼容全员对局。</summary>
    public bool RequireMatchAdmission
    {
        get => matchAccess.RequireAdmission;
        set => matchAccess.RequireAdmission = value;
    }

    public bool IsPeerAdmittedToMatch(ulong peerId) =>
        IsHost && peerId != 0 && (peerId == LocalId
            || (Transport.IsPeerConnected(peerId) && matchAccess.Allows(peerId, GetLegacyMatchPeers())));

    public bool AdmitPeerToMatch(ulong peerId)
    {
        if (!IsConnectedRemotePeer(peerId))
            return false;

        if (matchAccess.IsExplicitlyAdmitted(peerId))
            return true;
        if (!SendObjectSnapshot(peerId, NetSyncScope.Match) || !IsConnectedRemotePeer(peerId))
        {
            matchAccess.Set(peerId, false);
            SendMatchReset(peerId);
            return false;
        }
        matchAccess.Set(peerId, true);
        return true;
    }

    public bool RevokePeerFromMatch(ulong peerId)
    {
        if (!IsConnectedRemotePeer(peerId) || !IsPeerAdmittedToMatch(peerId))
            return false;

        matchAccess.Set(peerId, false);
        SendMatchReset(peerId);
        return true;
    }

    private void SendMatchReset(ulong peerId)
    {
        ReadOnlySpan<byte> body = stackalloc byte[] { (byte)NetMsgType.MatchReset };
        SendPackedToPeer(body, peerId);
    }

    private void ReadMatchReset(ReadOnlySpan<byte> content)
    {
        if (!content.IsEmpty)
            return;
        int version = sessionVersion;
        var ordered = CollectOrderedObjects(NetSyncScope.Match, includeInvalid: true);
        for (int i = ordered.Count - 1; i >= 0 && version == sessionVersion; i--)
        {
            var obj = ordered[i].Value;
            RemoveNetObject(obj);
            ReleaseRemoteObject(obj);
        }
    }

    private static void ReleaseRemoteObject(INetObject obj)
    {
        try
        {
            obj.INetDestroy();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[NetManager] 同步对象清理失败: {e}");
        }
    }

    public void SyncForJoiningPeer(ulong peerId) => SyncObjectsToPlayer(peerId, null);

    public void SyncAllNetObjectsToPlayer(ulong peerId) => SyncObjectsToPlayer(peerId, null);

    public void SyncObjectsToPlayer(ulong peerId, NetSyncScope? scopeFilter)
    {
        if (!IsConnectedRemotePeer(peerId))
            return;

        if (scopeFilter is null or NetSyncScope.Session)
        {
            if (!SendObjectSnapshot(peerId, NetSyncScope.Session))
                return;
        }
        if ((scopeFilter is null or NetSyncScope.Match) && IsPeerAdmittedToMatch(peerId))
        {
            if (!SendObjectSnapshot(peerId, NetSyncScope.Match))
            {
                matchAccess.Set(peerId, false);
                SendMatchReset(peerId);
            }
        }
    }

    internal void OnTransportPlayersChanged()
    {
        if (IsHost && matchAccess.HasOverrides && Transport.GetTempNetPlayerInfos() is { } players)
            matchAccess.RemoveDeparted(players);
    }

    private bool IsConnectedRemotePeer(ulong peerId) =>
        active && IsHost && peerId != 0 && peerId != LocalId && Transport.IsPeerConnected(peerId);

    private List<ulong> GetLegacyMatchPeers() =>
        NetMatchAdmission.CollectRemotePeers?.Invoke(LocalId);

    private bool CanPeerAccessObject(ulong peerId, INetObject target) =>
        target.NetSyncScope != NetSyncScope.Match || IsPeerAdmittedToMatch(peerId);

    private const int PacketBatchBytes = 48 * 1024;

    private List<KeyValuePair<uint, INetObject>> CollectOrderedObjects(NetSyncScope scope, bool includeInvalid = false)
    {
        var ordered = new List<KeyValuePair<uint, INetObject>>();
        foreach (var entry in idToObject)
            if (NetSyncSelectUtil.IncludeInSync(entry.Value.NetSyncScope, scope)
                && (includeInvalid || !entry.Value.IsNetInvalid()))
                ordered.Add(entry);
        ordered.Sort(static (a, b) =>
            NetSyncSelectUtil.CompareMidJoinOrder(a.Value.MidJoinSyncPriority, a.Key, b.Value.MidJoinSyncPriority, b.Key));
        return ordered;
    }

    private bool SendObjectSnapshot(ulong peerId, NetSyncScope scope)
    {
        var batch = new ArrayBufferWriter<byte>();
        foreach (var entry in CollectOrderedObjects(scope))
        {
            byte[] packet = PackSpawnMessage(entry.Value, entry.Key);
            if (packet == null)
                return false;
            if (batch.WrittenCount + packet.Length > PacketBatchBytes)
                FlushPeerBatch(batch, peerId);
            if (packet.Length >= PacketBatchBytes)
                Transport.Send(packet, peerId);
            else
                batch.Write(packet);
        }
        FlushPeerBatch(batch, peerId);
        return true;
    }

    private void FlushPeerBatch(ArrayBufferWriter<byte> batch, ulong peerId)
    {
        if (batch.WrittenCount == 0)
            return;
        Transport.Send(batch.WrittenSpan, peerId);
        batch.Clear();
    }

    private void HostSendFramedPacketForObject(INetObject target, ReadOnlySpan<byte> packet, ulong excludePeerId = 0) =>
        HostSendFramedPacketForScope(target.NetSyncScope, packet, excludePeerId);

    private void HostSendFramedPacketForScope(NetSyncScope scope, ReadOnlySpan<byte> packet, ulong excludePeerId = 0)
    {
        if (scope != NetSyncScope.Match)
        {
            Transport.SendToAll(packet, excludePeerId);
            return;
        }

        var legacyPeers = GetLegacyMatchPeers();
        if (matchAccess.CanBroadcast(legacyPeers))
        {
            Transport.SendToAll(packet, excludePeerId);
            return;
        }

        Transport.SendToSelected(packet, peerId => matchAccess.Allows(peerId, legacyPeers), excludePeerId);
    }
}
