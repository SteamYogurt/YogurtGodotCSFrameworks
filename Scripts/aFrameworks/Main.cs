using System;
using System.Collections.Generic;
using Godot;

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
        ClearAllUnimportantUI();
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
    public void ShowWaitingPanel()
    {
        waitingPanel.Show();
    }

    private Tween tween;
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
