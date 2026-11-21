using Xunit;

namespace Net.Tests;

public class SteamStoreTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ResolveIsDemo_UsesFlagOrExportFeature(bool flagged, bool exportFeature, bool expected)
    {
        Assert.Equal(expected, SteamStore.ResolveIsDemo(flagged, exportFeature));
    }

    [Theory]
    [InlineData(111u, 222u, 111u)]
    [InlineData(0u, 222u, 222u)]
    [InlineData(0u, 0u, 0u)]
    public void ResolveStoreAppId_PrefersExplicitStoreId(uint storeAppId, uint appId, uint expected)
    {
        Assert.Equal(expected, SteamStore.ResolveStoreAppId(storeAppId, appId));
    }

    [Fact]
    public void CanCheckFullGameOwnership_OnlyWhenStoreIdDiffers()
    {
        Assert.True(SteamStore.CanCheckFullGameOwnership(111, 222));
        Assert.False(SteamStore.CanCheckFullGameOwnership(0, 222));
        Assert.False(SteamStore.CanCheckFullGameOwnership(222, 222));
        Assert.False(SteamStore.CanCheckFullGameOwnership(480, 222));
    }

    [Theory]
    [InlineData(false, 111u, 111u, false, false)]
    [InlineData(true, 111u, 111u, false, true)]
    [InlineData(true, 222u, 111u, false, false)]
    [InlineData(true, 222u, 111u, true, true)]
    [InlineData(true, 480u, 111u, true, false)]
    [InlineData(true, 0u, 111u, false, false)]
    public void ResolveOwnsFullGame_UsesCurrentAppOrSubscription(
        bool steamInited, uint storeTarget, uint appId, bool subscribedToStore, bool expected)
    {
        Assert.Equal(expected, SteamStore.ResolveOwnsFullGame(steamInited, storeTarget, appId, subscribedToStore));
    }

    [Theory]
    [InlineData(true, false, 111u, true)]
    [InlineData(true, true, 111u, false)]
    [InlineData(false, false, 111u, false)]
    [InlineData(true, false, 0u, false)]
    [InlineData(true, false, 480u, false)]
    public void ShouldOfferWishlist_RequiresDemoAndStoreAndNoOwnership(
        bool isDemo, bool ownsFullGame, uint storeAppId, bool expected)
    {
        Assert.Equal(expected, SteamStore.ShouldOfferWishlist(isDemo, ownsFullGame, storeAppId));
    }

    [Fact]
    public void StoreUrls_UseAppId()
    {
        Assert.Equal("https://store.steampowered.com/app/4453480/", SteamStore.StorePageUrl(4453480));
        Assert.Equal("steam://store/4453480", SteamStore.SteamStoreProtocol(4453480));
        Assert.Equal("https://steamcommunity.com/app/4453480/", SteamStore.CommunityPageUrl(4453480));
        Assert.True(SteamStore.IsEditorPlaceholder(480));
        Assert.False(SteamStore.IsEditorPlaceholder(4453480));
    }
}
