using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Godot;
public partial class Game : Node,INetObject
{
    public static Game instance;
    public static void AuthorizedNetSpawn(INetObject netObject, bool isOnline)
    {
        if (isOnline && TransportManager.Instance?.Current?.AmIHost() == false)
        {
            GD.PrintErr($"错误的AuthorizedNetSpawn调用 ObjName:{netObject.Info.ObjectName}");
            return;
        }
        netObject.HostInitialize();
        if (isOnline)
        {
            NetManager.Instance.HostSpawnNetObject(netObject);
        }
        netObject.INetSpawn();
    }
    public static void AuthorizedNetDestroy(INetObject netObject, bool isOnline)
    {
        if (isOnline && TransportManager.Instance?.Current?.AmIHost() == false)
        {
            GD.PrintErr("错误的AuthorizedNetSpawn调用");
            return;
        }
        if (isOnline)
        {
            NetManager.Instance.HostDestroyNetObject(netObject);
        }
        netObject.INetDestroy();
    }
    public static void TryFreeGameAndReturn(string showBoxContent)
    {
        Main.Instance.ResetAndReturnToMenu();
        // 加入一个消息显示
    }
    void OnHostQuit()
    {
        TryFreeGameAndReturn("Host Quit");
    }
    public void SetupFromContext(GameContext context)
    {
        MidJoinable = context.MidJoinable;
        MaxPlayerCount = context.MaxPlayers;
    }
    public void HostInitialize()
    {

    }
    [Export]
    public ObjectInfo Info { get; set; }
    public CanvasLayer canvasLayer;
    public GameUI gameUI;

    NetVar isOnline = new NetVar(false);
    public bool IsOnline
    {
        get
        {
            return (bool)isOnline.Value;
        }
        set
        {
            isOnline.Value = value;
        }
    }
    #region Players
    public List<Player> players = new List<Player>();
    public List<Player> Players => players;
    public Player localPlayer;
    public event Action PlayersChanged;
    public event Action LocalPlayerChanged;
    void UpdateLocalPlayer()
    {
        if (IsOnline)
        {
            var local = TransportManager.Instance.Current.LocalID;
            foreach (Player player in players)
            {
                if ((ulong)player.id.Value == local)
                {
                    if(localPlayer != player)
                    {
                        localPlayer = player;
                        LocalPlayerChanged?.Invoke();
                    }
                    break;
                }
            }
        }
        else
        {
            if (players.Count > 0) localPlayer = players[0];
        }

    }
    public Player GetPlayer(ulong playerID)
    {
        foreach (Player player in players)
        {
            if ((ulong)player.id.Value == playerID) return player;
        }
        return null;
    }
    public void AddPlayer(Player player, bool notice = true)
    {
        // 防止重复添加（按 player.id）
        try
        {
            ulong pid = (ulong)player.id.Value;
            if (players.Any(p => (ulong)p.id.Value == pid))
            {
                Main.Print($"AddPlayer: player {pid} 已存在，忽略重复添加");
                return;
            }
        }
        catch
        {
            // ignore
        }
        players.Add(player);
        UpdateLocalPlayer();
        GD.Print("增加新玩家，总数: " + players.Count);
        if (notice)
        {
            PlayersChanged?.Invoke();
        }
    }
    public void RemovePlayer(Player player, bool notice = true)
    {
        players.Remove(player);
        UpdateLocalPlayer();
        if (notice)
        {
            PlayersChanged?.Invoke();
        }
    }
    public bool IsGameEnableEnter()
    {
        return  MidJoinable;
    }
    public void OnNetTransPlayerListChanged()
    {
        if (!isAuthorized) return;
        if (Game.instance == null) return;
        // 比对找出要减去和新增的player
        var pList = TransportManager.Instance.Current.GetTempNetPlayerInfos();
        Main.Print($"\n检测到trans层player变动，同步更新game层player；" +
           $"\n更新前信息: \nplayers数:{players.Count} transPInfo数:{pList.Count}");
        Main.Print("");
        var localID = TransportManager.Instance.Current.LocalID;
        var netPlayerDict = pList.ToDictionary(p => p.id);
        List<Player> playerToRemove = players
            .Where(p => !netPlayerDict.ContainsKey((ulong)p.id.Value))
            .ToList();
        var playerDict = players.ToDictionary(p => (ulong)p.id.Value);
        List<INetTransportPlayerInfo> playerToAdd = pList
            .Where(p => !playerDict.ContainsKey(p.id))
            .ToList();
        foreach (var player in playerToRemove)
        {
            AuthorizedNetDestroy(player, true);
        }
        if (IsGameEnableEnter())
        {
            foreach (var pInfo in playerToAdd)
            {
                if (pInfo.id != localID)
                    NetManager.Instance.SyncAllNetObjectsToPlayer(pInfo.id);
                var player = ObjectPoolManager.GetPossibleObject<Player>("player");
                player.id.Value = pInfo.id;
                player.name.Value = pInfo.name;
                AuthorizedNetSpawn(player, true);
            }
        }
        else
        {
            // 游戏不允许进入，直接踢出
            foreach (var pInfo in playerToAdd)
            {
                NetManager.Instance.SendEventToPlayer(pInfo.id, "Kick");
            }
        }
        if (players.Count > pList.Count)
        {
            GD.PrintErr("更新列表后realPlayerCount > pList.Count");
        }
    }
    public void CallOnPlayerStatusChanged(Player player)
    {
        PlayerStatusChanged?.Invoke(player);
    }
    public event Action<Player> PlayerStatusChanged;
    #endregion

    #region State
    public bool isAuthorized = true;
    public NetVar midJoinable = new NetVar();
    public bool MidJoinable
    {
        get
        {
            return (bool)midJoinable.Value;
        }
        set
        {
            midJoinable.Value = value;
            GameStateChanged?.Invoke();
        }
    }
    public NetVar maxPlayerCount = new NetVar();
    public int MaxPlayerCount
    {
        get
        {
            return (int)maxPlayerCount.Value;
        }
        set
        {
            maxPlayerCount.Value = value;
            GameStateChanged?.Invoke();
        }
    }
    void UpdateState()
    {
        isAuthorized = !IsOnline || TransportManager.Instance.Current.AmIHost();
        GameStateChanged?.Invoke();
    }
    public event Action GameStateChanged;
    #endregion 

    #region GamePause
    List<object> _requestPauseObjs = new List<object>();
    public void RequestPause(object obj)
    {
        if (!_requestPauseObjs.Contains(obj))
        {
            _requestPauseObjs.Add(obj);
        }
        CheckPause();
    }
    public void RevokePause(object obj)
    {
        _requestPauseObjs.Remove(obj);
        CheckPause();
    }
    void CheckPause()
    {
        ProcessMode = _requestPauseObjs.Count > 0 ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
    }
    #endregion

    public void CallOnNewMsg(string msg)
    {
        OnNewMsg?.Invoke(msg);
    }
    public event Action<string> OnNewMsg;
  
    public override void _Ready()
    {
        if (IsOnline)
        {
            TransportManager.Instance.Current.HostQuit += OnHostQuit;
        }
        if (isAuthorized)
        {
        }
    }
    public override void _EnterTree()
    {
        instance = this;
        UpdateState();

        canvasLayer = new CanvasLayer();
        AddChild(canvasLayer);
        gameUI = Global.GetObj<GameUI>("res://Scene/UI/GameUI/GameUI.tscn");
        canvasLayer.AddChild(gameUI);
       
        if (IsOnline)
        {
            TransportManager.Instance.Current.NetPlayerListChanged
                += OnNetTransPlayerListChanged;
            OnNetTransPlayerListChanged();
            // 这里会生成本地的
        }
        else
        {
            var player = ObjectPoolManager.GetPossibleObject<Player>("player");
            AuthorizedNetSpawn(player, IsOnline);
        }

    }
    public override void _ExitTree()
    {
        if (instance == this) instance = null;
        //Engine.TimeScale = 1;
        InputManager.Instance.SetCaptureRequest(false);
    }
    public override void _Process(double delta)
    {
     
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!isAuthorized) return;
        if(@event is InputEventKey key && key.Pressed && !key.IsEcho())
        {
            if(key.Keycode == Key.X)
            {
                //Status = GameStatus.Room;
            }
            if (OS.HasFeature("editor"))
            {
                if(key.Keycode == Key.F1)
                {
                    Global.TakeScreenshot();
                }
            }
        }
    }

    public bool HasAuthority()
    {
        return false;
    }

    public List<NetVar> GetInputStateVars()
    {
        return null;
    }
    List<NetVar> fullStates;
    public List<NetVar> GetFullStateVars()
    {
        if (fullStates == null)
        {
            fullStates = new List<NetVar>()
            {
                isOnline,
                midJoinable,
                maxPlayerCount
            };
        }
        return fullStates;
    }
    NetRPCTable netRPCTable;
    public NetRPCTable GetNetRPCTable()
    {
        if(netRPCTable == null)
        {
            netRPCTable = new NetRPCTable();
        }
        return netRPCTable;
    }
    public void INetSpawn()
    {
        Main.Instance.AddGame(this);
        Main.Instance.waitingPanel.Hide();
        //Main.Instance.AddChild(this);
    }
    public void INetDestroy()
    {

    }
}
