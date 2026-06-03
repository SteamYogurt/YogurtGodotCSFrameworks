using System;
using System.Collections.Generic;
using Godot;

public partial class Player : Node, INetObject
{
    public Player()
    {
        playerId.OnValueChanged += OnStatusChanged;
        playerName.OnValueChanged += OnStatusChanged;
    }
    [Export]
    public ObjectInfo Info { get; set; }

    public readonly NetVar playerId = new((ulong)0);
    public readonly NetVar playerName = new(string.Empty);

    private int _localSlotIndex = -1;
    private List<NetVar> _fullStateVars;
    private readonly List<NetVar> _inputStateVars = new();
    private NetRPCTable _netRPCTable;

    public ulong PlayerId
    {
        get => (ulong)playerId.Value;
        set => playerId.Value = value;
    }

    public string PlayerName
    {
        get => (string)playerName.Value;
        set => playerName.Value = value;
    }

    public int LocalSlotIndex
    {
        get
        {
            if(_localSlotIndex < 0)
            {
                return 0;
            }
            return _localSlotIndex;
        }
        set => _localSlotIndex = value;
    }

    public bool IsLocal => hasAuthority;
    public void HostInitialize()
    {

    }
    void OnStatusChanged()
    {
        UpdateAuthority();
        Game.instance?.CallOnPlayerStatusChanged(this);
    }

    bool hasAuthority = false;
    public void UpdateAuthority()
    {
        if (Game.instance == null)
        {
            hasAuthority = false;
            GD.PrintErr("Game instance is null. Cannot determine authority.");
            return;
        }

        if (!Game.instance.IsOnline)
        {
            hasAuthority = true;
            return;
        }

        var transport = TransportManager.Instance?.Current;
        hasAuthority = transport != null && transport.LocalID == PlayerId;
    }
    public bool HasAuthority()
    {
        return hasAuthority;
    }

    public List<NetVar> GetInputStateVars()
    {
        return _inputStateVars;
    }

    public List<NetVar> GetFullStateVars()
    {
        _fullStateVars ??= new List<NetVar>
        {
            playerId,
            playerName,
        };

        return _fullStateVars;
    }

    public NetRPCTable GetNetRPCTable()
    {
        _netRPCTable ??= new NetRPCTable();
        return _netRPCTable;
    }

    public void INetSpawn()
    {
        if (Game.instance == null)
            return;

        if (GetParent() != Game.instance)
        {
            GetParent()?.RemoveChild(this);
            Game.instance.AddChild(this);
        }
        UpdateAuthority();
        Main.Print($"玩家生成, ID: {PlayerId}, 名称: {PlayerName}, authority: {hasAuthority}");
        Game.instance.AddPlayer(this);
    }

    public void INetDestroy()
    {
        if (Game.instance != null)
        {
            Game.instance.RemovePlayer(this);
        }

        GetParent()?.RemoveChild(this);

        if (Info?.Pool != null)
        {
            Info.Pool.ReturnObjectToPool(this);
        }
        else
        {
            QueueFree();
        }
    }

    public bool IsNetInvalid()
    {
        return !IsInsideTree() && GetParent() == null && Info?.Pool == null;
    }
}
