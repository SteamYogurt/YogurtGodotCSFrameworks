using System;
using Godot;

public partial class Main
{
    INetTransport observedTransport;
    GameOnlineContext pendingCreateContext;
    ENetLobbyDisplayType currentLobbyDisplayType;
    private Tween tween;

    public event Action<GameOnlineContext, ENetLobbyDisplayType> OnlineRoomCreated;
    public event Action<ENetLobbyDisplayType> OnlineRoomJoined;

    public void AddUI(Node control)
    {
        uiLayer.AddChild(control);
        if (settings != null)
            uiLayer.MoveChild(settings, uiLayer.GetChildCount() - 1);
    }

    public void ResetAndReturnToMenu()
    {
        pendingCreateContext = null;
        Game.PendingOnlineContext = null;
        ClearAllUnimportantUI();
        StopObservingTransport();
        TransportManager.Instance.Deactive();
        NetManager.Instance.Deactive();
        if (Game.instance != null)
        {
            Game.instance.QueueFree();
        }
        lanDiscoveryService?.StopAll();
        waitingPanel?.Hide();
        LoadMenu();
    }

    public void ClearAllUnimportantUI()
    {
        foreach (var ui in uiLayer.GetChildren())
        {
            if (!importantUIList.Contains(ui)) ui.QueueFree();
        }
    }

    public void LoadMenu()
    {
        var menu = Global.GetObj<Control>("res://Scene/UI/Main/Menu.tscn");
        AddUI(menu);
    }

    public void OpenLobbyPanel(ENetLobbyDisplayType displayType)
    {
        var panel = Global.GetObj<MainLobbyPanel>("res://Scene/UI/Main/MainLobbyPanel.tscn");
        panel.DisplayType = displayType;
        AddUI(panel);
    }

    public void StartCreateLobby(GameOnlineContext context, ENetLobbyDisplayType displayType)
    {
        if (context == null)
            context = new GameOnlineContext();

        pendingCreateContext = context;
        EnsureNetworkServices(displayType);
        ClearAllUnimportantUI();
        ShowWaitingPanel();
        TransportManager.Instance.Current?.CreateRoom();
    }

    public void StartJoinLobby(string roomId, ENetLobbyDisplayType displayType)
    {
        EnsureNetworkServices(displayType);
        pendingCreateContext = null;
        ClearAllUnimportantUI();
        ShowWaitingPanel();
        TransportManager.Instance.Current?.JoinRoom(roomId);
    }

    public void EnsureNetworkServices(ENetLobbyDisplayType displayType)
    {
        currentLobbyDisplayType = displayType;
        lanDiscoveryService?.StopAll();
        waitingPanel?.Hide();
        StopObservingTransport();
        NetManager.Instance.Start();
        if (displayType == ENetLobbyDisplayType.Steam)
        {
            TransportManager.Instance.UseSteam();
            if (TransportManager.Instance.Current is SteamTransport steamTransport)
                steamTransport.PendingCreateMaxPlayers = Mathf.Max(pendingCreateContext?.MaxPlayers ?? 4, 1);
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
        OnlineRoomCreated?.Invoke(context, currentLobbyDisplayType);
    }

    public void ShowWaitingPanel()
    {
        waitingPanel?.Show();
    }

    void ObserveCurrentTransport()
    {
        observedTransport = TransportManager.Instance.Current;
        if (observedTransport != null)
            observedTransport.RoomJoined += OnCurrentTransportRoomJoined;
    }

    void StopObservingTransport()
    {
        if (observedTransport != null)
            observedTransport.RoomJoined -= OnCurrentTransportRoomJoined;
        observedTransport = null;
    }

    void OnCurrentTransportRoomJoined()
    {
        var transport = TransportManager.Instance.Current;
        if (transport == null || !transport.InRoom)
            return;

        Main.Print("检测到成功进入房间");
        ShowWaitingPanel();

        if (pendingCreateContext != null)
        {
            Main.Print("尝试创建在线游戏");
            var context = pendingCreateContext;
            pendingCreateContext = null;

            if (currentLobbyDisplayType == ENetLobbyDisplayType.Lan)
                lanDiscoveryService?.StartHosting(context.RoomName);
            else
                SteamManager.Instance?.ApplyCurrentLobbyMetadata(context);

            CreateOnlineGame(context);
            return;
        }

        OnlineRoomJoined?.Invoke(currentLobbyDisplayType);
    }

    public void PlayTransition(bool forward)
    {
        tween?.Kill();
        float target = forward ? 1f : 0f;
        SetProgress(1 - target);
        tween = CreateTween();
        tween.TweenMethod(
            Callable.From<float>(SetProgress),
            1 - target,
            target,
            0.7f
        );
    }

    private void SetProgress(float value)
    {
        var mat = transition.Material as ShaderMaterial;
        mat.SetShaderParameter("progress", value);
    }
}
