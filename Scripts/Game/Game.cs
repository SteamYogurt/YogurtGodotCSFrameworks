using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
public partial class Game : Node,INetObject
{
    public static Game instance;
    public static GameOnlineContext PendingOnlineContext { get; set; }
    private readonly Dictionary<ulong, Player> _playersById = new();
    private readonly List<Player> _localPlayers = new();
    private readonly HashSet<ulong> _pendingPlayerDestroyIds = new();
    private bool _initialOnlinePlayersBuilt = false;
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
        var game = instance;
        if (game != null)
        {
            if (game.IsOnline)
            {
                var trans = TransportManager.Instance.Current;
                trans?.LeaveRoom();
            }
        }
        Main.Instance.ResetAndReturnToMenu();
        // 加入一个消息显示
    }
    void OnHostQuit()
    {
        TryFreeGameAndReturn("Host Quit");
    }
    bool _localCoopEnabled = false;
    int _localPlayerCount = 1;
    // 这个仅在单机的时候有效

    public bool IsLocalCoop => _localCoopEnabled;
    public int LocalPlayerCount => _localPlayerCount;

    public void OnlineSetupFromContext(GameOnlineContext context)
    {
        context ??= new GameOnlineContext();
        MidJoinable = context.MidJoinable;
        MaxPlayerCount = Mathf.Max(context.MaxPlayers, 1);
        GameStateChanged?.Invoke();
    }
    public void HostInitialize()
    {
        if (IsOnline)
        {
            OnlineSetupFromContext(PendingOnlineContext);
            PendingOnlineContext = null;
        }
    }
    [Export]
    public ObjectInfo Info { get; set; }
    public CanvasLayer canvasLayer;
    public GameUI gameUI;

    readonly NetVar isOnline = new(false);
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
    readonly List<Player> players = new();
    public List<Player> Players => players;
    public List<Player> LocalPlayers => _localPlayers;
    // 仅离线状态可能会有多个本地玩家
    public event Action PlayersChanged;
    public event Action LocalPlayerChanged;
    void UpdateLocalPlayer()
    {
        RefreshLocalPlayers();
        LocalPlayerChanged?.Invoke();
    }
    public Player GetPlayer(ulong playerID)
    {
        return _playersById.TryGetValue(playerID, out var player) ? player : null;
    }
    public void AddPlayer(Player player, bool notice = true)
    {
        if (player == null)
            return;

        // 防止重复添加（按 playerId）
        ulong pid;
        try
        {
            pid = player.PlayerId;
            if (_playersById.TryGetValue(pid, out var existingPlayer))
            {
                if (ReferenceEquals(existingPlayer, player))
                    return;

                GD.PrintErr($"AddPlayer: player {pid} 已存在，拒绝重复对象");
                return;
            }
        }
        catch
        {
            GD.PrintErr("AddPlayer: 读取 PlayerId 失败");
            return;
        }

        _pendingPlayerDestroyIds.Remove(pid);
        _playersById[pid] = player;
        players.Add(player);
        UpdateLocalPlayer();
        Main.Print("增加新玩家，总数: " + players.Count);
        if (notice)
        {
            PlayersChanged?.Invoke();
        }
    }
    public void RemovePlayer(Player player, bool notice = true)
    {
        if (player == null)
            return;

        bool removed = players.Remove(player);
        if (!removed)
            return;

        if (_playersById.TryGetValue(player.PlayerId, out var existingPlayer) && ReferenceEquals(existingPlayer, player))
        {
            _playersById.Remove(player.PlayerId);
        }

        _pendingPlayerDestroyIds.Remove(player.PlayerId);
        UpdateLocalPlayer();
        if (notice)
        {
            PlayersChanged?.Invoke();
        }
    }
    public bool IsGameEnableEnter()
    {
        return true;
        return  _playersById.Count < MaxPlayerCount;
    }
    public void OnNetTransPlayerListChanged()
    {
        if (!IsAuthorized) return;
        if (Game.instance == null) return;

        var transport = TransportManager.Instance?.Current;
        if (transport == null)
            return;

        var pList = transport.GetTempNetPlayerInfos() ?? new List<INetTransportPlayerInfo>();
        Main.Print($"\n检测到trans层player变动，同步更新game层player；" +
           $"\n更新前信息: \nplayers数:{_playersById.Count} transPInfo数:{pList.Count}");

        var localID = transport.LocalID;
        var netPlayerDict = pList
            .Where(p => p != null)
            .GroupBy(p => p.id)
            .ToDictionary(g => g.Key, g => g.First());

        List<Player> playerToRemove = _playersById.Values
            .Where(p => p != null && !_pendingPlayerDestroyIds.Contains(p.PlayerId))
            .Where(p => !netPlayerDict.ContainsKey(p.PlayerId))
            .ToList();

        List<INetTransportPlayerInfo> playerToAdd = pList
            .Where(p => !_playersById.ContainsKey(p.id))
            .Where(p => !_pendingPlayerDestroyIds.Contains(p.id))
            .ToList();

        foreach (var player in playerToRemove)
        {
            _pendingPlayerDestroyIds.Add(player.PlayerId);
            AuthorizedNetDestroy(player, true);
        }

        int availableSlots = Mathf.Max(MaxPlayerCount - _playersById.Count - _pendingPlayerDestroyIds.Count, 0);
        if (!IsGameEnableEnter() || availableSlots <= 0)
        {
            GD.Print("游戏不允许进入，直接踢出玩家");
            foreach (var pInfo in playerToAdd)
            {
                NetManager.Instance.SendEventToPlayer(pInfo.id, "Kick");
            }
            return;
        }

        // 首次在线玩家构建时，不做 mid-join 快照同步。
        // 因为当时已在房间里的客户端，已经收到了 Game 的广播生成包。
        bool allowMidJoinSnapshot = _initialOnlinePlayersBuilt;

        if (allowMidJoinSnapshot)
        {
            var newRemotePeerIds = playerToAdd
                .Select(p => p.id)
                .Where(id => id != localID)
                .Distinct()
                .ToList();

            foreach (var peerId in newRemotePeerIds)
            {
                NetManager.Instance.SyncAllNetObjectsToPlayer(peerId);
            }
        }

        foreach (var pInfo in playerToAdd.Take(availableSlots))
        {
            if (_playersById.ContainsKey(pInfo.id) || _pendingPlayerDestroyIds.Contains(pInfo.id))
                continue;

            var player = ObjectPoolManager.GetPossibleObject<Player>("Player");
            if (player == null)
            {
                GD.PrintErr($"无法创建Player对象: {pInfo.id}");
                continue;
            }

            player.PlayerId = pInfo.id;
            player.PlayerName = string.IsNullOrWhiteSpace(pInfo.name)
                ? $"Player_{pInfo.id}"
                : pInfo.name;
            player.LocalSlotIndex = -1;
            AuthorizedNetSpawn(player, true);
        }

        _initialOnlinePlayersBuilt = true;

        if (_playersById.Count > pList.Count)
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
    bool isAuthorized = false;
    readonly NetVar midJoinable = new(true);
    public bool IsAuthorized => isAuthorized;
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
    readonly NetVar maxPlayerCount = new();
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
        Main.Print($"Game Entered Tree, authority: {isAuthorized}");
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
            foreach (var player in CreateOfflineLocalPlayers())
            {
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
        if (!IsAuthorized) return;
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
        return isAuthorized;
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
        TestNetTrans.instance.AddChild(this);
        return;
        Main.Instance.AddGame(this);
        Main.Instance.waitingPanel?.Hide();
        //Main.Instance.AddChild(this);
    }
    public void INetDestroy()
    {
        QueueFree();
    }

    void RefreshLocalPlayers()
    {
        _localPlayers.Clear();
        if (IsOnline)
            return;

        foreach (var player in players.Where(p => p != null && p.IsLocal).OrderBy(p => p.LocalSlotIndex))
        {
            _localPlayers.Add(player);
        }
    }

    IEnumerable<Player> CreateOfflineLocalPlayers()
    {
        int playerCount = IsLocalCoop ? _localPlayerCount : 1;
        for (int i = 0; i < playerCount; i++)
        {
            var player = ObjectPoolManager.GetPossibleObject<Player>("Player");
            if (player == null)
            {
                GD.PrintErr($"无法创建离线本地Player对象: slot {i}");
                continue;
            }

            player.PlayerId = (ulong)i;
            player.PlayerName = $"LocalPlayer{i + 1}";
            player.LocalSlotIndex = i;
            yield return player;
        }
    }

    int GetNextLocalSlotIndex()
    {
        int slot = 0;
        var usedSlots = new HashSet<int>(players.Select(p => p.LocalSlotIndex).Where(p => p >= 0));
        while (usedSlots.Contains(slot))
        {
            slot++;
        }

        return slot;
    }
}
