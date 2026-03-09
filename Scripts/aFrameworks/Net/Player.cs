using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using Godot;

public partial class Player : Node, INetObject
{
    public NetVar id = new NetVar(default(ulong));
    public NetVar name = new NetVar(string.Empty);
    [Export]
    public ObjectInfo Info { get; set; }

    public override void _EnterTree()
    {
        UpdateAuthority();
        if (hasAuthority)
        {
            CallDeferred(MethodName.Call_SendMsg, Tr("Joined Room"));
        }
       
    }
    public override void _ExitTree()
    {

    }

    public void Call_SendMsg(string msg)
    {
        var vars = new Variant[1];
        vars[0] = msg;
        
    }
    void SendMsg(Variant[] p)
    {
        var msg = (string)p[0];
    }


   
    #region INetObject

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
    public List<INetObject> belongingNetObjects = new List<INetObject>();
    public NetRPCTable netRPCTable;

    bool hasAuthority = false;
    public void UpdateAuthority()
    {
        if(Game.instance == null)
        {
            GD.PrintErr("player生成的时候 game未生成");
        }
        hasAuthority = !Game.instance.IsOnline
            || (ulong)id.Value == TransportManager.Instance.Current.LocalID;
    }
    public bool HasAuthority()
    {
        return hasAuthority;
    }
    // 是否为本地玩家
    public List<NetVar> fullStateVars;
    public List<NetVar> GetInputStateVars()
    {
        return null;
    }
    public List<NetVar> GetFullStateVars()
    {
        if (fullStateVars == null)
        {
            fullStateVars = new List<NetVar>()
            {
                id,
                name,
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
        }
        return netRPCTable;
    }
    public void INetSpawn()
    {
    }
    public void INetDestroy()
    {

    }
    #endregion

}
