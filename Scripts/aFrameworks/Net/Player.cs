using System;
using System.Collections.Generic;
using Godot;

public partial class Player : Node, INetObject
{
    [Export]
    public ObjectInfo Info { get; set; }

    public readonly NetVar id = new((ulong)0);
    public readonly NetVar name = new(string.Empty);
    public readonly NetVar localSlotIndex = new(0);
    public readonly NetVar ready = new(false);

    private List<NetVar> _fullStateVars;
    private readonly List<NetVar> _inputStateVars = new();
    private NetRPCTable _netRPCTable;

    public ulong PlayerId
    {
        get => (ulong)id.Value;
        set => id.Value = value;
    }

    public string PlayerName
    {
        get => (string)name.Value;
        set => name.Value = value;
    }

    public int LocalSlotIndex
    {
        get => (int)localSlotIndex.Value;
        set => localSlotIndex.Value = value;
    }

    public bool IsReady
    {
        get => (bool)ready.Value;
        set => ready.Value = value;
    }

    public void HostInitialize()
    {
        ready.Value = false;
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
            id,
            name,
            localSlotIndex,
            ready
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
