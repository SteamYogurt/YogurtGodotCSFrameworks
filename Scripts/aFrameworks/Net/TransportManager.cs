using Godot;
using System;

public partial class TransportManager : Singleton<TransportManager>
{
    public INetTransport Current { get; private set; }
    public bool isSteam = false;
    public event Action NetPlayerListChanged;
    public event Action RoomJoined;
    public event Action RoomJoinFailed;
    public event Action RoomStateChanged;
    public event Action HostQuit;

    public override void _EnterTree()
    {
        base._EnterTree();
        NetManager.Instance.EventCb += OnEvent;
    }

    public void UseSteam()
    {
        isSteam = true;
        SetCurrent(new SteamTransport());
    }

    public void UseLan()
    {
        isSteam = false;
        SetCurrent(new LanTransport());
    }

    void SetCurrent(INetTransport transport)
    {
        UnbindCurrent(Current);
        Current?.Free();
        Current = transport;
        Current.Init();
        BindCurrent(Current);
    }

    void BindCurrent(INetTransport transport)
    {
        if (transport == null)
            return;

        transport.NetPlayerListChanged += OnCurrentNetPlayerListChanged;
        transport.RoomJoined += OnCurrentRoomJoined;
        transport.RoomJoinFailed += OnCurrentRoomJoinFailed;
        transport.RoomStateChanged += OnCurrentRoomStateChanged;
        transport.HostQuit += OnCurrentHostQuit;
    }

    void UnbindCurrent(INetTransport transport)
    {
        if (transport == null)
            return;

        transport.NetPlayerListChanged -= OnCurrentNetPlayerListChanged;
        transport.RoomJoined -= OnCurrentRoomJoined;
        transport.RoomJoinFailed -= OnCurrentRoomJoinFailed;
        transport.RoomStateChanged -= OnCurrentRoomStateChanged;
        transport.HostQuit -= OnCurrentHostQuit;
    }

    void OnCurrentNetPlayerListChanged()
    {
        NetPlayerListChanged?.Invoke();
    }

    void OnCurrentRoomJoined()
    {
        RoomJoined?.Invoke();
    }

    void OnCurrentRoomJoinFailed()
    {
        RoomJoinFailed?.Invoke();
    }

    void OnCurrentRoomStateChanged()
    {
        RoomStateChanged?.Invoke();
    }

    void OnCurrentHostQuit()
    {
        HostQuit?.Invoke();
    }

    public void Deactivate()
    {
        UnbindCurrent(Current);
        Current?.Free();
        Current = null;
    }
    
    public override void _Process(double delta)
    {
        Current?.Poll();
    }
    public void OnEvent(string @event)
    {
        if(Current != null && @event == "Kick")
        {
            //Current.LeaveRoom();
            Game.TryFreeGameAndReturn("Kicked or Started");
        }
    }
    public void HostKick(ulong id)
    {

    }
}
