using System;
using Godot;

public enum ERoomFlowActionType
{
    None,
    Create,
    Join
}

public class RoomFlowRequest
{
    public ERoomFlowActionType ActionType;
    public ENetLobbyDisplayType DisplayType;
    public string RoomId;
    public GameOnlineContext Context;
}

public partial class Main
{
    RoomFlowRequest currentRoomFlowRequest;
    ENetLobbyDisplayType roomDisplayType;
    ulong waitingForSessionUntil;

    public event Action<GameOnlineContext, ENetLobbyDisplayType> OnlineRoomCreated;
    public event Action<ENetLobbyDisplayType> OnlineRoomJoined;

    public void StartCreateLobby(GameOnlineContext context, ENetLobbyDisplayType displayType)
    {
        if (context == null)
            context = new GameOnlineContext();

        currentRoomFlowRequest = new RoomFlowRequest
        {
            ActionType = ERoomFlowActionType.Create,
            DisplayType = displayType,
            Context = context
        };

        EnsureNetworkServices(currentRoomFlowRequest);
        ClearAllUnimportantUI();
        ShowWaitingPanel();
        TransportManager.Instance.Current?.CreateRoom();
    }

    public void StartJoinLobby(string roomId, ENetLobbyDisplayType displayType)
    {
        currentRoomFlowRequest = new RoomFlowRequest
        {
            ActionType = ERoomFlowActionType.Join,
            DisplayType = displayType,
            RoomId = roomId
        };

        EnsureNetworkServices(currentRoomFlowRequest);
        ClearAllUnimportantUI();
        ShowWaitingPanel();
        TransportManager.Instance.Current?.JoinRoom(roomId);
    }

    public void StartSteamInviteJoin(ulong roomId)
    {
        if (Game.instance != null)
            Game.instance.QueueFree();

        StartJoinLobby(roomId.ToString(), ENetLobbyDisplayType.Steam);
    }

    public void EnsureNetworkServices(RoomFlowRequest request)
    {
        if (request == null)
            return;

        lanDiscoveryService?.StopAll();
        roomDisplayType = request.DisplayType;
        UIManager.Instance.HideWaiting();
        StopObservingTransport();
        NetManager.Instance.Start();
        if (request.DisplayType == ENetLobbyDisplayType.Steam)
        {
            TransportManager.Instance.UseSteam();
            if (TransportManager.Instance.Current is SteamTransport steamTransport)
            {
                var context = OnlineRoomContextApply.Normalize(request.Context, request.DisplayType);
                request.Context = context;
                steamTransport.PendingCreateMaxPlayers = context.MaxPlayers;
                steamTransport.PendingCreateMidJoinable = context.MidJoinable;
                steamTransport.PendingCreateLobbyType = SteamManager.ToSteamLobbyType(context.Visibility);
            }
        }
        else
        {
            TransportManager.Instance.UseLan();
            if (request.ActionType == ERoomFlowActionType.Create
                && TransportManager.Instance.Current is LanTransport lanTransport)
            {
                request.Context = OnlineRoomContextApply.Normalize(request.Context, request.DisplayType);
                lanTransport.MaxPlayers = request.Context.MaxPlayers;
            }
        }

        ObserveCurrentTransport();
    }

    public void CreateOnlineGame(GameOnlineContext context)
    {
        Game.PendingOnlineContext = context;
        var game = ObjectPoolManager.GetPossibleObject<Game>("Game");
        game.IsOnline = true;
        Game.AuthorizedNetSpawn(game, true);
        OnlineRoomCreated?.Invoke(context, currentRoomFlowRequest?.DisplayType ?? ENetLobbyDisplayType.Lan);
    }

    void ObserveCurrentTransport()
    {
        if (TransportManager.Instance == null)
            return;
        TransportManager.Instance.RoomJoined += OnCurrentTransportRoomJoined;
        TransportManager.Instance.RoomJoinFailed += OnCurrentTransportRoomJoinFailed;
        TransportManager.Instance.HostQuit += OnCurrentTransportHostQuit;
        TransportManager.Instance.Kicked += OnKicked;
    }

    void StopObservingTransport()
    {
        if (TransportManager.Instance == null)
            return;
        TransportManager.Instance.RoomJoined -= OnCurrentTransportRoomJoined;
        TransportManager.Instance.RoomJoinFailed -= OnCurrentTransportRoomJoinFailed;
        TransportManager.Instance.HostQuit -= OnCurrentTransportHostQuit;
        TransportManager.Instance.Kicked -= OnKicked;
    }

    void OnCurrentTransportRoomJoined()
    {
        var transport = TransportManager.Instance.Current;
        if (transport == null || !transport.InRoom)
            return;

        Main.Print("检测到成功进入房间");
        ShowWaitingPanel();

        if (currentRoomFlowRequest?.ActionType == ERoomFlowActionType.Create)
        {
            Main.Print("尝试创建在线游戏");
            var request = currentRoomFlowRequest;
            var context = request.Context;

            OnlineRoomContextApply.ApplyAfterRoomCreated(context, request.DisplayType);
            CreateOnlineGame(context);
            currentRoomFlowRequest = null;
            return;
        }

        var displayType = currentRoomFlowRequest?.DisplayType ?? ENetLobbyDisplayType.Lan;
        currentRoomFlowRequest = null;
        waitingForSessionUntil = Time.GetTicksMsec() + 15000;
        OnlineRoomJoined?.Invoke(displayType);
    }

    public override void _Process(double delta)
    {
        if (waitingForSessionUntil != 0 && Time.GetTicksMsec() >= waitingForSessionUntil)
            OnCurrentTransportRoomJoinFailed();
    }

    void OnCurrentTransportHostQuit() => ReturnToLobby("与房主的连接已断开。");

    void OnKicked() => ReturnToLobby("房主已将你移出房间。");

    void OnCurrentTransportRoomJoinFailed()
    {
        var transport = TransportManager.Instance.Current;
        string notice = transport switch
        {
            { LastJoinWasBlocked: true } => "房主已禁止你重新加入此房间。",
            _ when waitingForSessionUntil != 0 => "等待房间同步超时。",
            SteamTransport steam when !string.IsNullOrEmpty(steam.LastRoomFailMessageKey)
                => steam.LastRoomFailMessageKey + "。",
            _ when currentRoomFlowRequest?.ActionType == ERoomFlowActionType.Create => "创建房间失败。",
            _ => "无法加入房间。请检查地址或刷新房间列表。",
        };
        ReturnToLobby(notice);
    }
}
