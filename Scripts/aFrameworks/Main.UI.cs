using Godot;

public partial class Main
{
    public void AddUI(Node control)
    {
        uiLayer.AddChild(control);
        if (settings != null)
            uiLayer.MoveChild(settings, uiLayer.GetChildCount() - 1);
    }

    public void ResetAndReturnToMenu()
    {
        currentRoomFlowRequest = null;
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

    public void ShowWaitingPanel()
    {
        waitingPanel?.Show();
    }
}
