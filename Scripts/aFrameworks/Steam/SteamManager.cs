using Godot;
using Steamworks;
using System;

public partial class SteamManager : Singleton<SteamManager>
{
    static AppId_t appId_T = new AppId_t(4453480);
    bool inited = false;
    public bool Inited => inited;

    Callback<UserStatsReceived_t> userStatsReceiveCallback;
    Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
    public override void _EnterTree()
    {
        base._EnterTree();
        inited = SteamAPI.Init();
        SteamAPI.RestartAppIfNecessary(appId_T);
        if (!inited)
        {
            GD.PrintErr("steam api can't init!");
            return;
        }
        GD.Print("steam api inited!");
        GD.Print("steam user name:" + SteamFriends.GetPersonaName());
        SteamUserStats.RequestCurrentStats();
        userStatsReceiveCallback = Callback<UserStatsReceived_t>.Create(OnStatsGot);
        joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
    }
    #region Achi
    public void UnlockAchi(MyAchi achi)
    {
        if (!inited) return;
        SteamUserStats.SetAchievement(achi.ToString());
        SteamUserStats.StoreStats();
    }
    public void OnStatsGot(UserStatsReceived_t userStatsReceived_T)
    {
        //GD.Print("接收到OnStatsGot回调，检查成就是否存在");
        var achiNames = Enum.GetNames(typeof(MyAchi));
        for (int i = 0; i < achiNames.Length; i++)
        {
            var achi = achiNames[i];
            if (!SteamUserStats.GetAchievement(achi, out bool achieved))
            {
                GD.PrintErr($"{achi}成就不存在！");
            }
        }
    }
    public enum MyAchi
    {
        //ACHI_STARTGAME,

    }
    #endregion
    void OnJoinRequested(GameLobbyJoinRequested_t e)
    {
        Main.Instance.ClearAllUnimportantUI();
        if(Game.instance != null)
            Game.instance.QueueFree();
        Main.Instance.ShowWaitingPanel();
        NetManager.Instance.Start();
        TransportManager.Instance.UseSteam();
        SteamMatchmaking.JoinLobby(e.m_steamIDLobby);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (!inited) return;
        SteamAPI.RunCallbacks();
    }
}
