using Godot;
using System.Linq;

public partial class Main
{
    public void AddUI(Node control)
    {
        UIManager.Instance.Add(control);
    }

    public void ResetAndReturnToMenu()
    {
        ResetRoomSession();
        UIManager.Instance.ShowMenu();
    }

    public void ReturnToLobby(string notice = "")
    {
        ResetRoomSession();
        UIManager.Instance.OpenLobby(roomDisplayType, notice);
    }

    void ResetRoomSession()
    {
        currentRoomFlowRequest = null;
        waitingForSessionUntil = 0;
        Game.PendingOnlineContext = null;
        StopObservingTransport();
        TransportManager.Instance.Deactivate();
        NetManager.Instance.Deactivate();
        if (Game.instance != null)
        {
            Game.instance.QueueFree();
        }
        lanDiscoveryService?.StopAll();
        UIManager.Instance.HideWaiting();
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

    void ShowOnlineRoom(Game game)
    {
        var transport = TransportManager.Instance.Current;
        var panel = UIManager.Instance.ShowOnlineRoom(roomDisplayType);
        if (transport == null || panel == null)
            return;

        void RefreshRoster() => panel.SetRoster(game.RoomName, game.MaxPlayerCount,
            transport.HostID, transport.LocalID, game.Players.Select(p =>
                new INetTransportPlayerInfo { id = p.PlayerId, name = p.PlayerName }));
        void RefreshPlayer(Player player)
        {
            if (game.GetPlayer(player.PlayerId) == player)
                RefreshRoster();
        }

        game.PlayersChanged += RefreshRoster;
        game.PlayerStatusChanged += RefreshPlayer;
        game.GameStateChanged += RefreshRoster;
        panel.LeaveRequested += () => ReturnToLobby();
        panel.InviteRequested += () =>
        {
            if (transport is SteamTransport steam && !steam.TryOpenInviteDialog())
                panel.ShowNotice("Steam 游戏内界面不可用。");
        };
        panel.KickRequested += peerId =>
        {
            if (!TransportManager.Instance.HostKick(peerId))
                panel.ShowNotice("该玩家已离开，无法踢出。");
        };
        panel.TreeExiting += () =>
        {
            game.PlayersChanged -= RefreshRoster;
            game.PlayerStatusChanged -= RefreshPlayer;
            game.GameStateChanged -= RefreshRoster;
        };
        RefreshRoster();
    }
}
