using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

public partial class TestNetTrans : Control
{
    public static TestNetTrans instance;
    // 测试是否能正常通信
    [Export] public Button hostLobbyButton;
    [Export] public Button joinLobbyButton;
    [Export] public Button hostStartGameButton;
    public override void _Ready()
    {
        instance = this;
        AddChild(new NetManager());
        AddChild(new TransportManager());
        AddChild(new ObjectPoolManager());
        if(hostLobbyButton != null)
        {
            hostLobbyButton.Pressed += () =>
            {
                if (!SteamManager.Instance.Inited) return;
                TransportManager.Instance.UseLan();
                var transport = TransportManager.Instance.Current;
                transport.CreateRoom();
                NetManager.Instance.Start();
            };
            
        }
        if(joinLobbyButton != null)
        {
            joinLobbyButton.Pressed += () =>
            {
                TransportManager.Instance.UseLan();
                var transport = TransportManager.Instance.Current;
                transport.JoinRoom("127.0.0.1");
                NetManager.Instance.Start();
            };
           
        }
        if(hostStartGameButton != null)
        {
            hostStartGameButton.Pressed += () =>
            {
                var game = ObjectPoolManager.GetPossibleObject<Game>("Game");
                game.IsOnline = true;
                Game.AuthorizedNetSpawn(game, true);
            };
          
        }
    }
}
