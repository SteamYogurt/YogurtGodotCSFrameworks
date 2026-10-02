using Xunit;

namespace Net.Tests;

public class NetSyncSelectUtilTests
{
    [Theory]
    [InlineData(NetSyncScope.Session, null, true)]
    [InlineData(NetSyncScope.Match, null, true)]
    [InlineData(NetSyncScope.Session, NetSyncScope.Session, true)]
    [InlineData(NetSyncScope.Match, NetSyncScope.Session, false)]
    [InlineData(NetSyncScope.Match, NetSyncScope.Match, true)]
    [InlineData(NetSyncScope.Session, NetSyncScope.Match, false)]
    public void IncludeInSync_RespectsOptionalFilter(
        NetSyncScope objectScope,
        NetSyncScope? filter,
        bool expected)
    {
        Assert.Equal(expected, NetSyncSelectUtil.IncludeInSync(objectScope, filter));
    }

    [Fact]
    public void CompareMidJoinOrder_PriorityDescThenNetIdAsc()
    {
        Assert.True(NetSyncSelectUtil.CompareMidJoinOrder(
            priorityA: 100, netIdA: 2,
            priorityB: 50, netIdB: 1) < 0);

        Assert.True(NetSyncSelectUtil.CompareMidJoinOrder(
            priorityA: 0, netIdA: 1,
            priorityB: 0, netIdB: 2) < 0);

        Assert.Equal(0, NetSyncSelectUtil.CompareMidJoinOrder(
            priorityA: 50, netIdA: 7,
            priorityB: 50, netIdB: 7));
    }

    [Fact]
    public void Sort_SessionRootThenMatchThenDefault()
    {
        var items = new List<(int priority, uint netId, NetSyncScope scope)>
        {
            (0, 30, NetSyncScope.Match),
            (50, 20, NetSyncScope.Match),
            (100, 10, NetSyncScope.Session),
            (0, 11, NetSyncScope.Session),
        };

        items.Sort((a, b) =>
            NetSyncSelectUtil.CompareMidJoinOrder(a.priority, a.netId, b.priority, b.netId));

        Assert.Equal(100, items[0].priority);
        Assert.Equal(50, items[1].priority);
        Assert.Equal(11u, items[2].netId);
        Assert.Equal(30u, items[3].netId);

        var sessionOnly = items
            .Where(i => NetSyncSelectUtil.IncludeInSync(i.scope, NetSyncScope.Session))
            .ToList();
        Assert.Equal(2, sessionOnly.Count);
        Assert.All(sessionOnly, i => Assert.Equal(NetSyncScope.Session, i.scope));
    }
}
