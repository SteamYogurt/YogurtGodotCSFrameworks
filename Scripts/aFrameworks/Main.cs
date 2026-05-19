using System;
using System.Collections.Generic;
using Godot;

public enum ENetLobbyDisplayType
{
    Steam,
    Lan
}

public partial class Main : Singleton<Main>
{
    public CanvasLayer uiLayer;
    public Settings settings;
    public WaitingPanel waitingPanel;
    public Control transition;
    public LanDiscoveryService lanDiscoveryService;
    List<Node> importantUIList = new List<Node>();
    INetTransport observedTransport;
    GameContext pendingCreateContext;
    ENetLobbyDisplayType currentLobbyDisplayType;
    public event Action<GameContext, ENetLobbyDisplayType> OnlineRoomCreated;
    public event Action<ENetLobbyDisplayType> OnlineRoomJoined;

    [Export]
    AudioStreamPlayer bgmPlayer;
    public static void Print(string content)
    {
        var transport = TransportManager.Instance?.Current;
        if (transport != null && NetManager.Instance.active)
            if (transport.AmIHost())
            {
                // 主机：绿色
                GD.PrintRich($"[color=lime][HOST ({TransportManager.Instance?.Current.LocalID})][/color] {content}");
            }
            else
            {
                // 客户端：青色
                GD.PrintRich($"[color=cyan][CLIENT]({TransportManager.Instance?.Current.LocalID})[/color] {content}");
            }
        else
        {
            GD.Print(content);
        }
    }
    public override void _Ready()
    {
        base._Ready();
        AddChild(UserInfo.LoadUserInfo());
        uiLayer = new CanvasLayer();
        uiLayer.Layer = 10;
        AddChild(uiLayer);
        AddChild(new ObjectPoolManager());
        AddChild(new NetManager());
        AddChild(new TransportManager());
        AddChild(new InputManager());
        AddChild(new SteamManager());
        lanDiscoveryService = new LanDiscoveryService();
        AddChild(lanDiscoveryService);
        waitingPanel = Global.GetObj<WaitingPanel>
            ("res://Scene/UI/Main/WaitingPanel.tscn");

        waitingPanel.Visible = false;
        var wait_return_btn = waitingPanel.GetNodeOrNull<Button>("Panel/Cancel");
        if (wait_return_btn != null)
            wait_return_btn.Pressed += ResetAndReturnToMenu;
        else GD.PrintErr("wait_return_btn null");
        uiLayer.AddChild(waitingPanel);
        importantUIList.Add(waitingPanel);

        transition = Global.GetObj<Control>
            ("res://Scene/UI/Main/Transition.tscn");
        uiLayer.AddChild(transition);
        importantUIList.Add(transition);

        settings = Global.GetObj<Settings>("res://addons_custom/Settings/Settings.tscn");
        settings.Visible = false;
        importantUIList.Add(settings);
        AddUI(settings);

        LoadMenu();
        bgmPlayer.Bus = "bg";
        bgmPlayer.Play();
    }


    public void AddUI(Node control)
    {
        uiLayer.AddChild(control);
        if (settings != null)
            uiLayer.MoveChild(settings, uiLayer.GetChildCount() - 1);
    }
    public void AddGame(Node game)
    {
        AddChild(game);
        MoveChild(uiLayer, GetChildCount() - 1);
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
        waitingPanel.Hide();
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
    public void StartCreateLobby(GameContext context, ENetLobbyDisplayType displayType)
    {
        if (context == null)
            context = new GameContext();

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
        waitingPanel.Hide();
        StopObservingTransport();
        NetManager.Instance.Start();
        if (displayType == ENetLobbyDisplayType.Steam)
        {
            TransportManager.Instance.UseSteam();
            if (TransportManager.Instance.Current is SteamTransport steamTransport)
                steamTransport.PendingCreateMaxPlayers = Mathf.Max(pendingCreateContext?.MaxPlayers ?? 4, 1);
        }
        else
            TransportManager.Instance.UseLan();
        ObserveCurrentTransport();
    }
    public void CreateOnlineGame(GameContext context)
    {
        Game.PendingOnlineContext = context;
        // 这里预留给外部更完整的游戏实例创建与初始化逻辑。
        OnlineRoomCreated?.Invoke(context, currentLobbyDisplayType);
    }
    public void ShowWaitingPanel()
    {
        waitingPanel.Show();
    }

    private Tween tween;
    void ObserveCurrentTransport()
    {
        observedTransport = TransportManager.Instance.Current;
        if (observedTransport != null)
            observedTransport.RoomStateChanged += OnCurrentTransportRoomStateChanged;
    }
    void StopObservingTransport()
    {
        if (observedTransport != null)
            observedTransport.RoomStateChanged -= OnCurrentTransportRoomStateChanged;
        observedTransport = null;
    }
    void OnCurrentTransportRoomStateChanged()
    {
        var transport = TransportManager.Instance.Current;
        if (transport == null || !transport.InRoom)
            return;

        ShowWaitingPanel();

        if (pendingCreateContext != null)
        {
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
