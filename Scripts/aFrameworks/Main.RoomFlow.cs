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
        waitingPanel?.Hide();
        StopObservingTransport();
        NetManager.Instance.Start();
        if (request.DisplayType == ENetLobbyDisplayType.Steam)
        {
            TransportManager.Instance.UseSteam();
            if (TransportManager.Instance.Current is SteamTransport steamTransport)
                steamTransport.PendingCreateMaxPlayers = Mathf.Max(request.Context?.MaxPlayers ?? 4, 1);
        }
        else
        {
            TransportManager.Instance.UseLan();
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
    }

    void StopObservingTransport()
    {
        if (TransportManager.Instance == null)
            return;
        TransportManager.Instance.RoomJoined -= OnCurrentTransportRoomJoined;
        TransportManager.Instance.RoomJoinFailed -= OnCurrentTransportRoomJoinFailed;
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

            if (request.DisplayType == ENetLobbyDisplayType.Lan)
                lanDiscoveryService?.StartHosting(context.RoomName);
            else
                SteamManager.Instance?.ApplyCurrentLobbyMetadata(context);

            CreateOnlineGame(context);
            currentRoomFlowRequest = null;
            return;
        }

        var displayType = currentRoomFlowRequest?.DisplayType ?? ENetLobbyDisplayType.Lan;
        currentRoomFlowRequest = null;
        OnlineRoomJoined?.Invoke(displayType);
    }

    void OnCurrentTransportRoomJoinFailed()
    {
        Main.Print("进入房间失败，返回菜单");
        HandleRoomJoinFailed();
    }

    void HandleRoomJoinFailed()
    {
        currentRoomFlowRequest = null;
        ResetAndReturnToMenu();
    }
}
