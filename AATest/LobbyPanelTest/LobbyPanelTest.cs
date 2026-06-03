using Godot;
using System;

public partial class LobbyPanelTest : Control
{
    [Export] public Button steamLobbyPanelButton;
    [Export] public Button lanLobbyPanelButton;
    [Export] public PackedScene mainLobbyPanelScene;

    public override void _Ready()
    {
        if(steamLobbyPanelButton != null)
        {
            steamLobbyPanelButton.Pressed += () =>
            {
                var panel = mainLobbyPanelScene.Instantiate<MainLobbyPanel>();
                panel.DisplayType = ENetLobbyDisplayType.Steam;
                GetTree().Root.AddChild(panel);
            };
        }
        if(lanLobbyPanelButton != null)
        {
            lanLobbyPanelButton.Pressed += () =>
            {
                var panel = mainLobbyPanelScene.Instantiate<MainLobbyPanel>();
                panel.DisplayType = ENetLobbyDisplayType.Lan;
                GetTree().Root.AddChild(panel);
            };
        }
    }
}
