using Godot;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

public class SteamLobbyRoomInfo
{
    public ulong LobbyId { get; set; }
    public string RoomName { get; set; } = "";
    public int PlayerCount { get; set; }
    public int MemberLimit { get; set; }
    public bool InMatch { get; set; }
    public bool MidJoinable { get; set; } = true;
    public string MatchId { get; set; } = "";
    public bool Joinable { get; set; } = true;
}

public partial class SteamManager : Singleton<SteamManager>
{
    const uint DefaultAppId = 4453480;

    [ExportGroup("Steam")]
    /// <summary>勾选后菜单显示加入愿望单。导出特性 demo 也会打开。</summary>
    [Export]
    public bool IsDemo;

    /// <summary>本构建 AppID。大于 0 时写 SteamAppId / SteamGameId。0 则交给工作目录 steam_appid.txt。</summary>
    [Export]
    public uint AppId = DefaultAppId;

    /// <summary>正式版商店页。愿望单打开这个 id。0 时退回 AppId。不要填编辑器用的 480。</summary>
    [Export]
    public uint StoreAppId;

    bool inited = false;
    private static bool nativeResolverRegistered;
    private static IntPtr steamNativeHandle;
    public bool Inited => inited;
    public bool Available => inited;

    /// <summary>检视器勾选或导出特性 demo。</summary>
    public bool IsDemoBuild => SteamStore.ResolveIsDemo(IsDemo, OS.HasFeature("demo"));

    public uint StoreTarget => SteamStore.ResolveStoreAppId(StoreAppId, AppId);

    public string StorePageUrl => StoreTarget > 0 ? SteamStore.StorePageUrl(StoreTarget) : "";

    public string CommunityPageUrl => AppId > 0 ? SteamStore.CommunityPageUrl(AppId) : "";

    public bool OwnsFullGame
    {
        get
        {
            uint target = StoreTarget;
            bool subscribed = inited
                && SteamStore.CanCheckFullGameOwnership(target, AppId)
                && SteamApps.BIsSubscribedApp(new AppId_t(target));
            return SteamStore.ResolveOwnsFullGame(inited, target, AppId, subscribed);
        }
    }

    public bool ShouldOfferWishlist =>
        SteamStore.ShouldOfferWishlist(IsDemoBuild, OwnsFullGame, StoreTarget);

    public static bool OfferWishlist => Instance != null && Instance.ShouldOfferWishlist;

    public static string LocalPersonaName
    {
        get
        {
            if (Instance == null || !Instance.inited)
                return "";
            return SteamFriends.GetPersonaName() ?? "";
        }
    }

    public bool IsRelayNetworkReady
    {
        get
        {
            if (!inited)
                return false;
            var avail = SteamNetworkingUtils.GetRelayNetworkStatus(out _);
            return avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current;
        }
    }

    public void EnsureRelayNetworkAccess()
    {
        if (!inited)
            return;
        SteamNetworkingUtils.InitRelayNetworkAccess();
    }

    Callback<UserStatsReceived_t> userStatsReceiveCallback;
    Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
    CallResult<LobbyMatchList_t> lobbyMatchListCallResult;
    public event Action<IReadOnlyList<SteamLobbyRoomInfo>> LobbyListUpdated;
    public override void _EnterTree()
    {
        base._EnterTree();
        ProcessMode = ProcessModeEnum.Always;
        TryInitSteam();
    }

    void TryInitSteam()
    {
        if (inited)
            return;

        if (AppId > 0)
        {
            OS.SetEnvironment("SteamAppId", AppId.ToString());
            OS.SetEnvironment("SteamGameId", AppId.ToString());
        }

        try
        {
            RegisterNativeResolver();
            // Restart 必须在 Init 之前。编辑器里跳过，避免 F5 被 Steam 拉起关掉。
            if (AppId > 0 && !OS.HasFeature("editor") && SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                GetTree()?.Quit();
                return;
            }
            inited = SteamAPI.Init();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            GD.PrintErr($"[SteamManager] Steam 原生库不可用: {e.Message}");
            return;
        }

        if (!inited)
        {
            GD.PrintErr("steam api can't init!");
            return;
        }
        GD.Print("steam api inited!");
        GD.Print("steam user name:" + SteamFriends.GetPersonaName());
        SteamUserStats.RequestCurrentStats();
        EnsureRelayNetworkAccess();
        userStatsReceiveCallback = Callback<UserStatsReceived_t>.Create(OnStatsGot);
        joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    private static void RegisterNativeResolver()
    {
        if (!OperatingSystem.IsWindows() || nativeResolverRegistered)
            return;
        nativeResolverRegistered = true;
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, ResolveSteamNativeLibrary);
        }
        catch (InvalidOperationException)
        {
            // A project can supply its own resolver for Steamworks.NET.
        }
    }

    private static IntPtr ResolveSteamNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName is not ("steam_api64" or "steam_api64.dll"))
            return IntPtr.Zero;
        if (steamNativeHandle != IntPtr.Zero)
            return steamNativeHandle;
        string outputDir = Path.GetDirectoryName(typeof(SteamManager).Assembly.Location);
        if (!string.IsNullOrEmpty(outputDir))
            NativeLibrary.TryLoad(Path.Combine(outputDir, "steam_api64.dll"), out steamNativeHandle);
        if (steamNativeHandle == IntPtr.Zero)
            NativeLibrary.TryLoad(ProjectSettings.GlobalizePath("res://steam_api64.dll"), out steamNativeHandle);
        return steamNativeHandle;
    }
    #region Achi
    public void UnlockAchi(MyAchi achi)
    {
        if (!inited || IsDemoBuild) return;
        SteamUserStats.SetAchievement(achi.ToString());
        SteamUserStats.StoreStats();
    }
    public void OnStatsGot(UserStatsReceived_t userStatsReceived_T)
    {
        //GD.Print("接收到OnStatsGot回调，检查成就是否存在");
        var achiNames = Enum.GetNames(typeof(MyAchi));
        for (int i = 0; i < achiNames.Length; i++)
        {
            var achi = achiNames[i];
            if (!SteamUserStats.GetAchievement(achi, out bool achieved))
            {
                GD.PrintErr($"{achi}成就不存在！");
            }
        }
    }
    public enum MyAchi
    {
        //ACHI_STARTGAME,

    }
    #endregion
    void OnJoinRequested(GameLobbyJoinRequested_t e)
    {
        Main.Instance.StartSteamInviteJoin(e.m_steamIDLobby.m_SteamID);
    }
    public void RequestLobbyList()
    {
        if (!inited)
        {
            LobbyListUpdated?.Invoke(Array.Empty<SteamLobbyRoomInfo>());
            return;
        }

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            OnlineRoomListing.KeyGameId,
            OnlineRoomListing.GameId,
            ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            OnlineRoomListing.KeyProtocolVersion,
            OnlineRoomListing.ProtocolVersion,
            ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
        var apiCall = SteamMatchmaking.RequestLobbyList();
        lobbyMatchListCallResult?.Set(apiCall);
    }
    void OnLobbyMatchList(LobbyMatchList_t e, bool ioFailure)
    {
        if (ioFailure)
        {
            LobbyListUpdated?.Invoke(Array.Empty<SteamLobbyRoomInfo>());
            return;
        }

        var rooms = new List<SteamLobbyRoomInfo>((int)e.m_nLobbiesMatching);
        for (int i = 0; i < e.m_nLobbiesMatching; i++)
        {
            var lobby = SteamMatchmaking.GetLobbyByIndex(i);
            string gameId = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyGameId);
            if (!OnlineRoomListing.IsOurGame(gameId)
                || SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyProtocolVersion) != OnlineRoomListing.ProtocolVersion)
                continue;

            string roomName = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyRoomName);
            if (!OnlineRoomListing.HasUsableRoomName(roomName))
                continue;

            int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby);
            if (memberLimit <= 0
                && int.TryParse(SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyMaxPlayers), out int parsedMax))
            {
                memberLimit = parsedMax;
            }

            int playerCount = OnlineRoomListing.ParsePlayerCount(
                SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyPlayerCount));
            if (playerCount <= 0)
                playerCount = SteamMatchmaking.GetNumLobbyMembers(lobby);

            string flow = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyFlow);
            bool inMatch = flow == OnlineRoomListing.FlowInMatch;
            string matchId = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyMatchId) ?? "";
            bool midJoinable = OnlineRoomListing.ParseFlag(
                SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyMidJoinable),
                defaultValue: true);
            string joinableRaw = SteamMatchmaking.GetLobbyData(lobby, OnlineRoomListing.KeyJoinable);
            bool joinable = OnlineRoomListing.ParseFlag(joinableRaw, defaultValue: true)
                && OnlineRoomListing.CanJoin(playerCount, memberLimit, inMatch, midJoinable);

            rooms.Add(new SteamLobbyRoomInfo
            {
                LobbyId = lobby.m_SteamID,
                RoomName = roomName.Trim(),
                PlayerCount = playerCount,
                MemberLimit = memberLimit,
                InMatch = inMatch,
                MidJoinable = midJoinable,
                MatchId = matchId,
                Joinable = joinable,
            });
        }

        LobbyListUpdated?.Invoke(rooms);
    }
    public void ApplyCurrentLobbyMetadata(GameOnlineContext context)
    {
        if (!inited || context == null)
            return;

        if (TransportManager.Instance?.Current is not SteamTransport steamTransport
            || !steamTransport.InRoom || !steamTransport.AmIHost())
            return;

        var lobby = steamTransport.currentLobby;
        string roomName = OnlineRoomListing.ResolveRoomName(context.RoomName, ENetLobbyDisplayType.Steam);
        SteamMatchmaking.SetLobbyData(lobby, OnlineRoomListing.KeyRoomName, roomName);
        steamTransport.PublishPresence();
        SteamMatchmaking.SetLobbyData(lobby, "visibility", context.Visibility.ToString());
        // Joinable=false 的大厅不会出现在 RequestLobbyList。满员仍列出并置灰，不公开靠 LobbyType。
        SteamMatchmaking.SetLobbyJoinable(lobby, true);
        SteamMatchmaking.SetLobbyType(lobby, ToSteamLobbyType(context.Visibility));
    }
    public static ELobbyType ToSteamLobbyType(EGameLobbyType type)
    {
        return type switch
        {
            EGameLobbyType.Private => ELobbyType.k_ELobbyTypePrivate,
            EGameLobbyType.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
            _ => ELobbyType.k_ELobbyTypePublic,
        };
    }
    public override void _PhysicsProcess(double delta)
    {
        if (!inited) return;
        SteamAPI.RunCallbacks();
    }

    /// <summary>
    /// 打开正式版商店页（愿望单入口）。优先 Overlay，其次 steam://，最后浏览器。
    /// 不要用加购物车标志。
    /// </summary>
    public void OpenWishlist()
    {
        OpenStorePage();
    }

    public void OpenStorePage()
    {
        uint target = StoreTarget;
        if (target == 0)
        {
            GD.PushWarning("SteamManager: no store app id");
            return;
        }

        if (SteamStore.IsEditorPlaceholder(target))
        {
            GD.PushWarning("SteamManager: store app id is 480 (Spacewar). Set StoreAppId to the full game.");
            return;
        }

        if (inited && SteamUtils.IsOverlayEnabled())
        {
            SteamFriends.ActivateGameOverlayToStore(
                new AppId_t(target),
                EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
            return;
        }

        Error err = OS.ShellOpen(SteamStore.SteamStoreProtocol(target));
        if (err == Error.Ok)
            return;

        err = OS.ShellOpen(SteamStore.StorePageUrl(target));
        if (err != Error.Ok)
            GD.PushError($"SteamManager: failed to open store {target} ({err})");
    }

    /// <summary>已连上且 Overlay 可用时走 Overlay 网页，否则系统浏览器。</summary>
    public void OpenUrl(string url)
    {
        string target = (url ?? "").Trim();
        if (string.IsNullOrEmpty(target))
        {
            GD.PushWarning("SteamManager.OpenUrl: empty url");
            return;
        }

        if (inited && SteamUtils.IsOverlayEnabled())
        {
            SteamFriends.ActivateGameOverlayToWebPage(target);
            return;
        }

        Error err = OS.ShellOpen(target);
        if (err != Error.Ok)
            GD.PushError($"SteamManager.OpenUrl failed: {target} ({err})");
    }

    public static void OpenExternalUrl(string url)
    {
        if (Instance != null)
        {
            Instance.OpenUrl(url);
            return;
        }

        string target = (url ?? "").Trim();
        if (string.IsNullOrEmpty(target))
            return;

        Error err = OS.ShellOpen(target);
        if (err != Error.Ok)
            GD.PushError($"SteamManager.OpenExternalUrl failed: {target} ({err})");
    }

    public static void OpenWishlistPage()
    {
        if (Instance != null)
        {
            Instance.OpenWishlist();
            return;
        }

        GD.PushWarning("SteamManager.OpenWishlistPage: no instance");
    }
}
