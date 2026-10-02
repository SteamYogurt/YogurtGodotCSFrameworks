using Godot;

public partial class Main
{
    public void AddUI(Node control)
    {
        UIManager.Instance.Add(control);
    }

    public void ResetAndReturnToMenu()
    {
        currentRoomFlowRequest = null;
        Game.PendingOnlineContext = null;
        StopObservingTransport();
        TransportManager.Instance.Deactivate();
        NetManager.Instance.Deactivate();
        if (Game.instance != null)
        {
            Game.instance.QueueFree();
        }
        lanDiscoveryService?.StopAll();
        UIManager.Instance.ShowMenu();
    }

    public void ClearAllUnimportantUI()
    {
        UIManager.Instance.ClearTransient();
    }

    public void LoadMenu()
    {
        UIManager.Instance.ShowMenu();
    }

    public void OpenLobbyPanel(ENetLobbyDisplayType displayType)
    {
        UIManager.Instance.OpenLobby(displayType);
    }

    public void ShowWaitingPanel()
    {
        UIManager.Instance.ShowWaiting();
    }

    public void PlayTransition(bool forward)
    {
        UIManager.Instance.PlayTransition(forward);
    }

    void OnKicked()
    {
        Game.TryFreeGameAndReturn("Kicked or Started");
    }
}
