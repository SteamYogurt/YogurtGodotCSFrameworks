using Godot;
using System;

public partial class MainLobbyRoomItem : Button
{
    public string RoomId { get; private set; } = string.Empty;
    public bool Joinable { get; private set; }
    public event Action<MainLobbyRoomItem> Selected;

    public override void _Ready()
    {
        ToggleMode = true;
        FocusMode = FocusModeEnum.All;
        Pressed += () => Selected?.Invoke(this);
    }

    public void Setup(string roomId, string roomName, string detailText, bool joinable)
    {
        RoomId = roomId ?? string.Empty;
        Joinable = joinable;
        Text = string.IsNullOrWhiteSpace(detailText)
            ? roomName
            : $"{roomName}\n{detailText}";
        TooltipText = Text;
    }

    public void SetSelected(bool selected)
    {
        ButtonPressed = selected;
    }
}
