using System;
using System.Collections.Generic;
using System.Linq;
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
    bool _localCoopEnabled;
    int _localPlayerCount = 1;

    public bool IsLocalCoop => _localCoopEnabled;
    public int LocalPlayerCount => _localPlayerCount;
    public bool IsInBasement => IsGameEnableEnter();

    public void SetupFromContext(GameContext context)
    {
        context ??= new GameContext();
        MidJoinable = context.MidJoinable;
        MaxPlayerCount = Mathf.Max(context.MaxPlayers, 1);
        _localCoopEnabled = context.LocalCoopEnabled;
        _localPlayerCount = Mathf.Max(context.LocalPlayerCount, 1);
        InputManager.Instance?.SetLocalInputContext(_localCoopEnabled, _localPlayerCount);
        GameStateChanged?.Invoke();
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
        Player nextLocalPlayer = null;

        if (IsOnline)
        {
            var local = TransportManager.Instance?.Current?.LocalID;
            if (local != null)
            {
                foreach (Player player in players)
                {
                    if ((ulong)player.id.Value == local)
                    {
                        nextLocalPlayer = player;
                        break;
                    }
                }
            }
        }
        else
        {
            if (players.Count > 0)
                nextLocalPlayer = players[0];
        }

        if (localPlayer != nextLocalPlayer)
        {
            localPlayer = nextLocalPlayer;
            LocalPlayerChanged?.Invoke();
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
        if (player == null)
            return;

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
        if (player == null)
            return;

        players.Remove(player);
        UpdateLocalPlayer();
        if (notice)
        {
            PlayersChanged?.Invoke();
        }
    }
    public bool IsGameEnableEnter()
    {
        return MidJoinable && players.Count < MaxPlayerCount;
    }
    public void OnNetTransPlayerListChanged()
    {
        if (!isAuthorized) return;
        if (Game.instance == null) return;
        var transport = TransportManager.Instance?.Current;
        if (transport == null)
            return;

        // 比对找出要减去和新增的player
        var pList = transport.GetTempNetPlayerInfos() ?? new List<INetTransportPlayerInfo>();
        Main.Print($"\n检测到trans层player变动，同步更新game层player；" +
           $"\n更新前信息: \nplayers数:{players.Count} transPInfo数:{pList.Count}");
        Main.Print("");
        var localID = transport.LocalID;
        var netPlayerDict = pList
            .GroupBy(p => p.id)
            .ToDictionary(g => g.Key, g => g.First());
        List<Player> playerToRemove = players
            .Where(p => !netPlayerDict.ContainsKey((ulong)p.id.Value))
            .ToList();
        var playerDict = players
            .Where(p => p != null)
            .GroupBy(p => (ulong)p.id.Value)
            .ToDictionary(g => g.Key, g => g.First());
        List<INetTransportPlayerInfo> playerToAdd = pList
            .Where(p => !playerDict.ContainsKey(p.id))
            .GroupBy(p => p.id)
            .Select(g => g.First())
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
                if (player == null)
                {
                    GD.PrintErr($"无法创建Player对象: {pInfo.id}");
                    continue;
                }
                player.id.Value = pInfo.id;
                player.name.Value = string.IsNullOrWhiteSpace(pInfo.name)
                    ? $"Player_{pInfo.id}"
                    : pInfo.name;
                player.localSlotIndex.Value = GetNextLocalSlotIndex();
                player.ready.Value = false;
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
        isAuthorized = !IsOnline || TransportManager.Instance?.Current?.AmIHost() == true;
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
        if (IsOnline && TransportManager.Instance?.Current != null)
        {
            TransportManager.Instance.Current.HostQuit += OnHostQuit;
        }
    }
    public override void _EnterTree()
    {
        instance = this;
        UpdateState();
        InputManager.Instance?.SetLocalInputContext(_localCoopEnabled, _localPlayerCount);

        canvasLayer = new CanvasLayer();
        AddChild(canvasLayer);
        if (ResourceLoader.Exists("res://Scene/UI/GameUI/GameUI.tscn"))
        {
            gameUI = Global.GetObj<GameUI>("res://Scene/UI/GameUI/GameUI.tscn");
            canvasLayer.AddChild(gameUI);
        }
       
        if (IsOnline && TransportManager.Instance?.Current != null)
        {
            TransportManager.Instance.Current.NetPlayerListChanged
                += OnNetTransPlayerListChanged;
            OnNetTransPlayerListChanged();
            // 这里会生成本地的
        }
        else
        {
            var player = ObjectPoolManager.GetPossibleObject<Player>("player");
            if (player != null)
            {
                player.id.Value = 0UL;
                player.name.Value = "LocalPlayer";
                player.localSlotIndex.Value = 0;
                player.ready.Value = true;
                AuthorizedNetSpawn(player, IsOnline);
            }
        }

    }
    public override void _ExitTree()
    {
        if (instance == this) instance = null;
        if (IsOnline && TransportManager.Instance?.Current != null)
        {
            TransportManager.Instance.Current.NetPlayerListChanged
                -= OnNetTransPlayerListChanged;
            TransportManager.Instance.Current.HostQuit -= OnHostQuit;
        }
        InputManager.Instance?.ClearLocalInputContext();
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
        return !IsOnline || TransportManager.Instance?.Current?.AmIHost() == true;
    }

    public List<NetVar> GetInputStateVars()
    {
        return s_emptyInputStates;
    }
    static readonly List<NetVar> s_emptyInputStates = new();
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
        foreach (var player in players.ToArray())
        {
            RemovePlayer(player, false);
        }

        localPlayer = null;
        PlayersChanged?.Invoke();
        LocalPlayerChanged?.Invoke();
    }

    int GetNextLocalSlotIndex()
    {
        int slot = 0;
        var usedSlots = new HashSet<int>(players.Select(p => (int)p.localSlotIndex.Value));
        while (usedSlots.Contains(slot))
        {
            slot++;
        }

        return slot;
    }
}
