/// <summary>
/// Steam 商店页 / 愿望单的纯规则。不碰 Steamworks，便于单测。
/// Steam 没有静默加愿望单 API，商店页才是可点入口。
/// </summary>
public static class SteamStore
{
    public const uint SpacewarAppId = 480;

    public static bool ResolveIsDemo(bool flagged, bool exportFeature)
    {
        return flagged || exportFeature;
    }

    public static uint ResolveStoreAppId(uint storeAppId, uint appId)
    {
        return storeAppId > 0 ? storeAppId : appId;
    }

    public static bool CanCheckFullGameOwnership(uint storeAppId, uint appId)
    {
        return storeAppId > 0 && storeAppId != appId && !IsEditorPlaceholder(storeAppId);
    }

    /// <summary>
    /// 已连上且商店目标就是本构建，视为已有。跨 AppID 才看订阅结果。
    /// </summary>
    public static bool ResolveOwnsFullGame(bool steamInited, uint storeTarget, uint appId, bool subscribedToStore)
    {
        if (!steamInited || storeTarget == 0 || IsEditorPlaceholder(storeTarget))
            return false;
        return storeTarget == appId || subscribedToStore;
    }

    public static bool ShouldOfferWishlist(bool isDemo, bool ownsFullGame, uint storeAppId)
    {
        return isDemo && !ownsFullGame && storeAppId > 0 && !IsEditorPlaceholder(storeAppId);
    }

    public static bool IsEditorPlaceholder(uint appId)
    {
        return appId == SpacewarAppId;
    }

    public static string StorePageUrl(uint appId)
    {
        return $"https://store.steampowered.com/app/{appId}/";
    }

    public static string SteamStoreProtocol(uint appId)
    {
        return $"steam://store/{appId}";
    }

    public static string CommunityPageUrl(uint appId)
    {
        return $"https://steamcommunity.com/app/{appId}/";
    }
}
