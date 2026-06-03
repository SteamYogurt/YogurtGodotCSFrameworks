using System;
using System.Collections.Generic;
using System.Drawing;
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
    public static void Print(string content)
    {
        var transport = TransportManager.Instance?.Current;
        if (transport != null && NetManager.Instance.active)
            if (transport.AmIHost())
            {
                // 主机：绿色
                GD.PrintRich($"[color=lime][HOST ({TransportManager.Instance?.Current.LocalID})]{content}[/color] ");
            }
            else
            {
                // 客户端：青色
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

        return;
        if (waitingPanelScene != null)
        {
            waitingPanel = waitingPanelScene.Instantiate<WaitingPanel>();
            waitingPanel.Visible = false;

            var wait_return_btn = waitingPanel.GetNodeOrNull<Button>("Panel/Cancel");
            if (wait_return_btn != null)
                wait_return_btn.Pressed += ResetAndReturnToMenu;
            else GD.PrintErr("wait_return_btn null");

            uiLayer.AddChild(waitingPanel);
            importantUIList.Add(waitingPanel);
        }
        if (transitionScene != null)
        {
            transition = Global.GetObj<Control>
         ("res://Scene/UI/Main/Transition.tscn");
            uiLayer.AddChild(transition);
            importantUIList.Add(transition);
        }
     

        settings = Global.GetObj<Settings>("res://addons_custom/Settings/Settings.tscn");
        settings.Visible = false;
        importantUIList.Add(settings);
        AddUI(settings);

        LoadMenu();
        bgmPlayer.Bus = "bg";
        bgmPlayer.Play();
    }

    public void AddGame(Node game)
    {
        AddChild(game);
        MoveChild(uiLayer, GetChildCount() - 1);
    }
}
