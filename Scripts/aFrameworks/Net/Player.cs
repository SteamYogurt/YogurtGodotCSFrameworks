using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using Godot;

public partial class Player : Node, INetObject
{
    public NetVar id = new NetVar(default(ulong));
    // botID从1开始，单独的，无所谓
    public NetVar name = new NetVar(string.Empty);
    NetVar isBot = new NetVar(default(bool));
    NetVar isLeft = new NetVar(default(bool));
    NetVar spawnedSync = new NetVar(false);
    public bool IsHost()
    {
        if(!Game.instance.IsOnline)return true;
        return Id == TransportManager.Instance.Current.HostID;
    }
    public ulong Id
    {
        get
        {
            return (ulong)id.Value;
        }
        set
        {
            var sync = Game.instance.GetSyncLayer(this);
            if (sync != null) sync.PlayerId = value;
            id.Value = value;
        }
    }
    public string PlayerName
    {
        get
        {
            return (string)name.Value;
        }
        set
        {
            name.Value = value;
        }
    }
    public event Action PlayerNameChanged
    {
        add { name.OnValueChanged += value; }
        remove { name.OnValueChanged -= value; }
    }
    public bool IsBot
    {
        get
        {
            return (bool)isBot.Value;
        }
        set
        {
            isBot.Value = value;
        }
    }
    public bool IsLeft
    {
        get
        {
            return (bool)isLeft.Value;
        }
        set
        {
            isLeft.Value = value;
        }
    }
    public bool SpawnedSync
    {
        get
        {
            return (bool)spawnedSync.Value;
        }
        set
        {
            spawnedSync.Value = value;
        }
    }
    // 本地可以即时修改
    // 区分是否新生成的角色，或是替换了机器人，已有操作层
    [Export]
    public ObjectInfo Info { get; set; }
    public override void _EnterTree()
    {
        if (Game.instance.IsOnline && !IsBot && hasAuthority)
        {
            CallDeferred(MethodName.Call_SendMsg, Tr("Joined Room"));
        }
        id.OnValueChanged += OnStatusChanged;
        name.OnValueChanged += OnStatusChanged;
        isBot.OnValueChanged += OnStatusChanged;
        isLeft.OnValueChanged += OnStatusChanged;

        Game.instance.OnGameStatusChanged += OnGameStatusChanged;
        OnGameStatusChanged();
    }
    public override void _ExitTree()
    {
        Game.instance.OnGameStatusChanged -= OnGameStatusChanged;
    }

    public void Call_SendMsg(string msg)
    {
        var vars = new Variant[1];
        vars[0] = msg;
        if (!SpawnedSync && Game.instance.IsOnline)
        {
            NetManager.Instance.SendRPC(this, 1, vars, NetManager.RPCSendType.ToAll);
        }
        SendMsg(vars);
    }
    void SendMsg(Variant[] p)
    {
        var msg = PlayerName + ": " +(string)p[0];
        Game.instance.CallOnNewMsg(msg);
    }

    public void Call_ChangeTeam(bool isLeft)
    {
        var vars = new Variant[1];
        vars[0] = isLeft;
        if (Game.instance.IsOnline && !Game.instance.isAuthorized)
        {
            NetManager.Instance.SendRPC(this, 2, vars, NetManager.RPCSendType.ToHost);
        }
        ChangeTeam(vars);
        // 本地可以直接跑，任何情况
    }
    void ChangeTeam(Variant[] p)
    {
        var isLeft = (bool)p[0];
        IsLeft = isLeft;
    }

    public void Call_SpawnSyncLayer(string kartName,string chrName)
    {
        if (!hasAuthority) return;
        var vars = new Variant[2];
        vars[0] = kartName;
        vars[1] = chrName;
        if (Game.instance.IsOnline)
        {
            NetManager.Instance.SendRPC(this, 3, vars, NetManager.RPCSendType.ToHost);
        }
        HostSpawnSyncLayer(vars);
    }
    void HostSpawnSyncLayer(Variant[] p)
    {
        if (!Game.instance.isAuthorized) return;
        var kartName = (string)p[0];
        var chrName = (string)p[1];
        var syncLayer = ObjectPoolManager.GetPossibleObject<SyncLayer>("SyncLayer");
        syncLayer.KartName = kartName;
        syncLayer.ChrName = chrName;
        syncLayer.PlayerId = Id;
        syncLayer.player = this;// host init用得到
        SpawnedSync = true;
        Game.AuthorizedNetSpawn(syncLayer, Game.instance.IsOnline);
    }

    void OnGameStatusChanged()
    {
        var status = Game.instance.Status;
        if(status == Game.GameStatus.Game)
        {
            if (!SpawnedSync && hasAuthority)
            {
                if(!IsBot)
                    Call_SpawnSyncLayer(UserInfo.Instance.KartName, UserInfo.Instance.ChrName);
                else
                {
                    // bot从解锁的载具和角色里面选
                    Call_SpawnSyncLayer("Kart01", "Chr01");
                }
            }
        }
        if(status != Game.GameStatus.Game)
        {
            SpawnedSync = false;
        }
    }
    void OnStatusChanged()
    {
        UpdateAuthority();
        Game.instance.CallOnPlayerStatusChanged(this);
    }
    #region INetObject


    public List<INetObject> belongingNetObjects = new List<INetObject>();
    public NetRPCTable netRPCTable;

    bool hasAuthority = false;
    public void UpdateAuthority()
    {
        if(Game.instance == null)
        {
            GD.PrintErr("player生成的时候 game未生成");
        }
        if(IsBot) hasAuthority = Game.instance.isAuthorized;
        else hasAuthority = !Game.instance.IsOnline
            || (ulong)id.Value == TransportManager.Instance.Current.LocalID;
    }
    public bool HasAuthority()
    {
        return hasAuthority;
    }
    // 是否为本地玩家 或者主机端对bot控制

    public List<NetVar> GetInputStateVars()
    {
        return null;
    }
    public List<NetVar> fullStateVars;
    public List<NetVar> GetFullStateVars()
    {
        if (fullStateVars == null)
        {
            fullStateVars = new List<NetVar>()
            {
                id,
                name,
                isBot,
                isLeft,
                spawnedSync
            };
        }
        return fullStateVars;
    }
    public NetRPCTable GetNetRPCTable()
    {
        if (netRPCTable == null)
        {
            netRPCTable = new NetRPCTable();
            netRPCTable.Register(1, SendMsg);
            netRPCTable.Register(2, ChangeTeam);
            netRPCTable.Register(3, HostSpawnSyncLayer);
        }
        return netRPCTable;
    }
    public void INetSpawn()
    {
        UpdateAuthority();
        Game.instance.AddPlayer(this);
        Game.instance.AddChild(this);
    }
    public void INetDestroy()
    {
        Game.instance.RemovePlayer(this);
        QueueFree();
        foreach (var belonging in belongingNetObjects)
        {
            Game.AuthorizedNetDestroy(belonging, Game.instance.IsOnline);
        }

    }
    #endregion

}
