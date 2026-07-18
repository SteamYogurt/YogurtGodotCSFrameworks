using Xunit;

namespace Net.Tests;

public class NetSpawnDestroyGuardTests
{
    [Fact]
    public void DestroyBeforeSpawn_ThenInitial_IsIgnoredAndPendingCleared()
    {
        var g = new NetSpawnDestroyGuard();

        Assert.Equal(
            NetSpawnDestroyGuard.DestroyDecision.MarkPendingBeforeSpawn,
            g.DecideDestroy(5, hasLazy: false, hasReady: false));

        Assert.Equal(
            NetSpawnDestroyGuard.SpawnDecision.SkipBecausePendingDestroy,
            g.DecideSpawn(5, alreadyRegisteredOrLazy: false));

        Assert.Equal(
            NetSpawnDestroyGuard.InitialDecision.IgnoreBecausePendingDestroy,
            g.DecideInitial(5, alreadyRegistered: false, hasLazy: false));

        Assert.Empty(g.PendingDestroyIds);
    }

    [Fact]
    public void SpawnThenDestroyLazy_KeepsPending_UntilInitial()
    {
        var g = new NetSpawnDestroyGuard();

        Assert.Equal(
            NetSpawnDestroyGuard.SpawnDecision.Accept,
            g.DecideSpawn(3, alreadyRegisteredOrLazy: false));

        Assert.Equal(
            NetSpawnDestroyGuard.DestroyDecision.DestroyLazyKeepPending,
            g.DecideDestroy(3, hasLazy: true, hasReady: false));

        Assert.Contains(3u, g.PendingDestroyIds);

        Assert.Equal(
            NetSpawnDestroyGuard.InitialDecision.IgnoreBecausePendingDestroy,
            g.DecideInitial(3, alreadyRegistered: false, hasLazy: false));

        Assert.Empty(g.PendingDestroyIds);
    }

    [Fact]
    public void NormalSpawnInitialDestroyReady()
    {
        var g = new NetSpawnDestroyGuard();

        Assert.Equal(NetSpawnDestroyGuard.SpawnDecision.Accept, g.DecideSpawn(1, false));
        Assert.Equal(NetSpawnDestroyGuard.InitialDecision.Accept, g.DecideInitial(1, false, hasLazy: true));
        Assert.Equal(NetSpawnDestroyGuard.DestroyDecision.DestroyReady, g.DecideDestroy(1, false, hasReady: true));
        Assert.Empty(g.PendingDestroyIds);
    }

    [Fact]
    public void DuplicateSpawn_IsSkipped()
    {
        var g = new NetSpawnDestroyGuard();
        Assert.Equal(
            NetSpawnDestroyGuard.SpawnDecision.SkipBecauseDuplicate,
            g.DecideSpawn(9, alreadyRegisteredOrLazy: true));
    }

    [Fact]
    public void DuplicateInitial_IsSkipped()
    {
        var g = new NetSpawnDestroyGuard();
        Assert.Equal(
            NetSpawnDestroyGuard.InitialDecision.IgnoreBecauseDuplicate,
            g.DecideInitial(9, alreadyRegistered: true, hasLazy: true));
    }

    [Fact]
    public void MissingLazy_WithoutPending_ReportsMissing()
    {
        var g = new NetSpawnDestroyGuard();
        Assert.Equal(
            NetSpawnDestroyGuard.InitialDecision.MissingLazy,
            g.DecideInitial(2, alreadyRegistered: false, hasLazy: false));
    }

    [Fact]
    public void InstantiateFailure_MarkPending_BlocksSpawn()
    {
        var g = new NetSpawnDestroyGuard();
        g.MarkPendingDestroy(7);
        Assert.Equal(
            NetSpawnDestroyGuard.SpawnDecision.SkipBecausePendingDestroy,
            g.DecideSpawn(7, false));
    }
}
