using System;
using System.Collections.Generic;
using Godot;

public partial class Player : Node, INetObject
{
    [Export]
    public ObjectInfo Info { get; set; }

    public readonly NetVar playerId = new((ulong)0);
    public readonly NetVar playerName = new(string.Empty);
    public readonly NetVar isLocal = new(false);
    public readonly NetVar isReady = new(false);
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
        get => _localSlotIndex;
        set => _localSlotIndex = value;
    }

    public bool IsLocal
    {
        get => (bool)isLocal.Value;
        set => isLocal.Value = value;
    }

    public bool IsReady
    {
        get => (bool)isReady.Value;
        set => isReady.Value = value;
    }

    public void HostInitialize()
    {
        IsReady = false;
    }

    public bool HasAuthority()
    {
        if (Game.instance == null)
            return false;

        if (!Game.instance.IsOnline)
            return true;

        var transport = TransportManager.Instance?.Current;
        return transport != null && transport.LocalID == PlayerId;
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
            isLocal,
            isReady
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
