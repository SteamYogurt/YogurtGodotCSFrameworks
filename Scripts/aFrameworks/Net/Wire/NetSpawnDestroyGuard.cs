using System.Collections.Generic;

/// <summary>
/// Spawn / Initial / Destroy 乱序防护（pendingDestroyIds）。
/// </summary>
public sealed class NetSpawnDestroyGuard
{
    private readonly HashSet<uint> _pendingDestroyIds = new();

    public IReadOnlyCollection<uint> PendingDestroyIds => _pendingDestroyIds;

    public void Clear() => _pendingDestroyIds.Clear();

    public enum SpawnDecision
    {
        Accept,
        SkipBecausePendingDestroy,
        SkipBecauseDuplicate,
    }

    public enum InitialDecision
    {
        Accept,
        IgnoreBecausePendingDestroy,
        IgnoreBecauseDuplicate,
        MissingLazy,
    }

    public enum DestroyDecision
    {
        DestroyLazyKeepPending,
        DestroyReady,
        MarkPendingBeforeSpawn,
    }

    public SpawnDecision DecideSpawn(uint id, bool alreadyRegisteredOrLazy)
    {
        if (_pendingDestroyIds.Contains(id))
            return SpawnDecision.SkipBecausePendingDestroy;
        if (alreadyRegisteredOrLazy)
            return SpawnDecision.SkipBecauseDuplicate;
        return SpawnDecision.Accept;
    }

    /// <summary>lazy 实例化失败时调用，与 Destroy-before-Spawn 同等对待。</summary>
    public void MarkPendingDestroy(uint id) => _pendingDestroyIds.Add(id);

    public InitialDecision DecideInitial(uint id, bool alreadyRegistered, bool hasLazy)
    {
        if (_pendingDestroyIds.Remove(id))
            return InitialDecision.IgnoreBecausePendingDestroy;
        if (alreadyRegistered)
            return InitialDecision.IgnoreBecauseDuplicate;
        if (!hasLazy)
            return InitialDecision.MissingLazy;
        return InitialDecision.Accept;
    }

    public DestroyDecision DecideDestroy(uint id, bool hasLazy, bool hasReady)
    {
        if (hasLazy)
        {
            _pendingDestroyIds.Add(id);
            return DestroyDecision.DestroyLazyKeepPending;
        }

        if (hasReady)
            return DestroyDecision.DestroyReady;

        _pendingDestroyIds.Add(id);
        return DestroyDecision.MarkPendingBeforeSpawn;
    }
}
