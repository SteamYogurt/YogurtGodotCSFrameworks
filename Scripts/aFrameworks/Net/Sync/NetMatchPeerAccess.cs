#nullable enable
using System.Collections.Generic;

internal sealed class NetMatchPeerAccess
{
    private readonly Dictionary<ulong, bool> overrides = new();
    public bool RequireAdmission { get; set; }
    public bool HasOverrides => overrides.Count != 0;

    public bool IsExplicitlyAdmitted(ulong peerId) => overrides.TryGetValue(peerId, out bool admitted) && admitted;

    public bool Allows(ulong peerId, List<ulong>? legacyPeers)
    {
        if (overrides.TryGetValue(peerId, out bool admitted))
            return admitted;
        return legacyPeers?.Contains(peerId) ?? !RequireAdmission;
    }

    public bool CanBroadcast(List<ulong>? legacyPeers) =>
        !RequireAdmission && overrides.Count == 0 && legacyPeers == null;

    public void Set(ulong peerId, bool admitted) => overrides[peerId] = admitted;

    public void RemoveDeparted(List<INetTransportPlayerInfo> players)
    {
        if (overrides.Count == 0)
            return;
        var connected = new HashSet<ulong>();
        foreach (var player in players)
            connected.Add(player.id);
        var departed = new List<ulong>();
        foreach (ulong peerId in overrides.Keys)
            if (!connected.Contains(peerId))
                departed.Add(peerId);
        foreach (ulong peerId in departed)
            overrides.Remove(peerId);
    }

    public void Clear()
    {
        overrides.Clear();
        RequireAdmission = false;
    }
}
