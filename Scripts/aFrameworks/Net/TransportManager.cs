using Godot;
using System;

public partial class TransportManager : Singleton<TransportManager>
{
    public INetTransport Current { get; private set; }
    public bool isSteam = false;
    public override void _EnterTree()
    {
        base._EnterTree();
        NetManager.Instance.EventCb += OnEvent;
    }
    public void UseSteam()
    {
        isSteam = true;
        Current?.Free();
        Current = new SteamTransport();
        Current.Init();
    }
    public void UseLan()
    {
        isSteam = false;
        Current?.Free();
        Current = new LanTransport();
        Current.Init();
    }
    public void Deactive()
    {
        Current?.Free();
    }

    public override void _Process(double delta)
    {
        Current?.Poll();
    }
    public void OnEvent(string @event)
    {
        if (Current != null && @event == "Kick")
        {
            //Current.LeaveRoom();
            Game.TryFreeGameAndReturn("Kicked");
        }
    }
    public void HostKick(ulong id)
    {

    }
}
