using Godot;
using System;

public partial class MainLobbyRoomItem : Button
{
    public string RoomId { get; private set; } = string.Empty;
    public bool Joinable { get; private set; }
    public event Action<MainLobbyRoomItem> Selected;
    public event Action Activated;

    [Export] public Label RoomNameLabel { get; set; }
    [Export] public Label DetailLabel { get; set; }

    public override void _Ready()
    {
        ToggleMode = true;
        Pressed += () => Selected?.Invoke(this);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Joinable || @event is not InputEventMouseButton
            { ButtonIndex: MouseButton.Left, DoubleClick: true, Pressed: true })
            return;

        AcceptEvent();
        Selected?.Invoke(this);
        Activated?.Invoke();
    }

    public void Setup(string roomId, string roomName, string detailText, bool joinable)
    {
        RoomId = roomId ?? string.Empty;
        Joinable = joinable;
        if (RoomNameLabel != null && DetailLabel != null)
        {
            RoomNameLabel.Text = roomName;
            DetailLabel.Text = detailText;
        }
        else
        {
            Text = $"{roomName}  {detailText}";
        }
        TooltipText = $"{roomName}\n{detailText}";
    }

    public void SetSelected(bool selected) => SetPressedNoSignal(selected);
}