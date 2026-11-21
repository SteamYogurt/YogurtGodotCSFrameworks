using System;
using Godot;

public partial class Main : Singleton<Main>
{
    public LanDiscoveryService lanDiscoveryService;

    [Export]
    AudioStreamPlayer bgmPlayer;
    [ExportGroup("PackedScene")]
    [Export]
    public PackedScene waitingPanelScene;
    [Export]
    public PackedScene transitionScene;
    [Export]
    public PackedScene menuScene;
    [Export]
    public PackedScene settingsScene;
    [Export]
    public PackedScene lobbyPanelScene;
    [Export]
    public PackedScene onlineRoomPanelScene;

    public CanvasLayer uiLayer => UIManager.Instance?.Layer;

    public static void Print(string content)
    {
        var transport = TransportManager.Instance?.Current;
        if (transport != null && NetManager.Instance.active)
            if (transport.AmIHost())
            {
                GD.PrintRich($"[color=lime][HOST ({TransportManager.Instance?.Current.LocalID})]{content}[/color] ");
            }
            else
            {
                GD.PrintRich($"[color=cyan][CLIENT ({TransportManager.Instance?.Current.LocalID})]{content}[/color]");
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
        AddChild(new ObjectPoolManager());
        AddChild(new NetManager());
        AddChild(new TransportManager());
        AddChild(new InputManager());
        if (GetNodeOrNull<SteamManager>("SteamManager") == null)
            AddChild(new SteamManager());
        lanDiscoveryService = new LanDiscoveryService();
        AddChild(lanDiscoveryService);
        AddChild(new UIManager());

        if (bgmPlayer != null)
        {
            bgmPlayer.Bus = UserInfo.BusMusic;
            bgmPlayer.Play();
        }
    }

    public void AddGame(Node game)
    {
        waitingForSessionUntil = 0;
        AddChild(game);
        if (UIManager.Instance != null)
        {
            MoveChild(UIManager.Instance, GetChildCount() - 1);
        }

        if (game is Game { IsOnline: true } onlineGame)
            ShowOnlineRoom(onlineGame);
        else
            UIManager.Instance?.HideWaiting();
    }
}
