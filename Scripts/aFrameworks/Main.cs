using Godot;
using System;

public partial class Main : Singleton<Main>
{
    public CanvasLayer uiLayer;
    public override void _Ready()
    {
        base._Ready();
        AddChild(UserInfo.LoadUserInfo());
        uiLayer = new CanvasLayer();
        AddChild(uiLayer);
        AddChild(new ObjectPoolManager());
        AddChild(new TransportManager());
        AddChild(new NetManager());
    }

}
