using Xunit;

namespace Net.Tests;

public class NetRoutingTests
{
    [Theory]
    [InlineData(true, NetSendFlags.Host, true)]
    [InlineData(true, NetSendFlags.Clients, false)]
    [InlineData(true, NetSendFlags.AllOthers, true)]
    [InlineData(true, NetSendFlags.None, false)]
    [InlineData(false, NetSendFlags.Host, false)]
    [InlineData(false, NetSendFlags.Clients, true)]
    [InlineData(false, NetSendFlags.AllOthers, true)]
    [InlineData(false, NetSendFlags.None, false)]
    public void ShouldDeliverOnReceive_MatchesRoleAndFlags(bool amIHost, NetSendFlags flags, bool expected)
    {
        Assert.Equal(expected, NetRouting.ShouldDeliverOnReceive(amIHost, flags));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldRunLocallyOnSend_IsOrthogonalToFlags(bool alsoRunLocally, bool expected)
    {
        Assert.Equal(expected, NetRouting.ShouldRunLocallyOnSend(alsoRunLocally));
    }

    [Theory]
    [InlineData(NetSendFlags.Clients, true)]
    [InlineData(NetSendFlags.AllOthers, true)]
    [InlineData(NetSendFlags.Host, false)]
    [InlineData(NetSendFlags.None, false)]
    public void ShouldHostForwardToClients(NetSendFlags flags, bool expected)
    {
        Assert.Equal(expected, NetRouting.ShouldHostForwardToClients(flags));
        Assert.Equal(expected, NetRouting.ShouldHostOutboundToClients(flags));
    }

    [Fact]
    public void HostOnly_IsAliasOfHost()
    {
        Assert.Equal(NetSendFlags.Host, NetSendFlags.HostOnly);
    }

    [Fact]
    public void PeerTargetMarker_DoesNotOverlapFlags()
    {
        Assert.Equal(0x80, NetRouting.PeerTargetMarker);
        Assert.False(NetRouting.IsPeerTarget((byte)NetSendFlags.AllOthers));
        Assert.True(NetRouting.IsPeerTarget(NetRouting.PeerTargetMarker));
        Assert.Equal(0, (byte)NetSendFlags.AllOthers & NetRouting.PeerTargetMarker);
    }

    [Fact]
    public void ClientOutboundMustGoToHost()
    {
        Assert.True(NetRouting.ClientOutboundMustGoToHost);
    }

    /// <summary>
    /// 场景矩阵：本地执行与网上投递相互独立。
    /// </summary>
    [Theory]
    // alsoRun, flags, expectLocal, expectReceiveIfHost, expectReceiveIfClient, expectForward
    [InlineData(true, NetSendFlags.AllOthers, true, true, true, true)]
    [InlineData(false, NetSendFlags.AllOthers, false, true, true, true)]
    [InlineData(true, NetSendFlags.Host, true, true, false, false)]
    [InlineData(false, NetSendFlags.Host, false, true, false, false)]
    [InlineData(true, NetSendFlags.Clients, true, false, true, true)]
    [InlineData(false, NetSendFlags.Clients, false, false, true, true)]
    public void ScenarioMatrix(
        bool alsoRunLocally,
        NetSendFlags flags,
        bool expectLocal,
        bool expectDeliverHost,
        bool expectDeliverClient,
        bool expectForward)
    {
        Assert.Equal(expectLocal, NetRouting.ShouldRunLocallyOnSend(alsoRunLocally));
        Assert.Equal(expectDeliverHost, NetRouting.ShouldDeliverOnReceive(amIHost: true, flags));
        Assert.Equal(expectDeliverClient, NetRouting.ShouldDeliverOnReceive(amIHost: false, flags));
        Assert.Equal(expectForward, NetRouting.ShouldHostForwardToClients(flags));
    }
}
