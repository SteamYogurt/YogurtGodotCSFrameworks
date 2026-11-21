using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MainOnlineRoomPanel : Control
{
    public ENetLobbyDisplayType DisplayType { get; set; }
    public event Action LeaveRequested;
    public event Action<ulong> KickRequested;
    public event Action InviteRequested;

    [Export] public Label ModeLabel { get; set; }
    [Export] public Label RoomNameLabel { get; set; }
    [Export] public Label CountLabel { get; set; }
    [Export] public Label NoticeLabel { get; set; }
    [Export] public VBoxContainer Roster { get; set; }
    [Export] public Button LeaveButton { get; set; }
    [Export] public Button InviteButton { get; set; }
    [Export] public ConfirmationDialog KickDialog { get; set; }

    ulong pendingKickId;

    public override void _Ready()
    {
        ModeLabel.Text = DisplayType == ENetLobbyDisplayType.Steam ? "Steam 联机" : "局域网联机";
        LeaveButton.Pressed += () => LeaveRequested?.Invoke();
        InviteButton.Pressed += () => InviteRequested?.Invoke();
        KickDialog.Confirmed += () =>
        {
            ulong id = pendingKickId;
            pendingKickId = 0;
            if (id != 0)
                KickRequested?.Invoke(id);
        };
        KickDialog.Canceled += () => pendingKickId = 0;
        LeaveButton.GrabFocus();
    }

    public void SetRoster(string roomName, int capacity, ulong hostId, ulong localId,
        IEnumerable<INetTransportPlayerInfo> members)
    {
        var ordered = members.OrderByDescending(p => p.id == hostId)
            .ThenByDescending(p => p.id == localId).ThenBy(p => p.id).ToArray();
        bool isHost = hostId != 0 && localId == hostId;
        InviteButton.Visible = isHost && DisplayType == ENetLobbyDisplayType.Steam;
        if (pendingKickId != 0 && (!isHost || !ordered.Any(p => p.id == pendingKickId)))
        {
            pendingKickId = 0;
            KickDialog.Hide();
        }

        RoomNameLabel.Text = string.IsNullOrWhiteSpace(roomName) ? "联机房间" : roomName;
        RoomNameLabel.TooltipText = RoomNameLabel.Text;
        CountLabel.Text = capacity > 0 ? $"{ordered.Length} / {capacity}" : $"{ordered.Length} 人";
        var focused = GetViewport().GuiGetFocusOwner();
        bool restoreFocus = focused != null && Roster.IsAncestorOf(focused);
        foreach (Node row in Roster.GetChildren())
        {
            Roster.RemoveChild(row);
            row.QueueFree();
        }

        foreach (var member in ordered)
            AddMember(member, member.id == hostId, member.id == localId, isHost && member.id != localId);
        if (restoreFocus)
            LeaveButton.GrabFocus();
    }

    public void ShowNotice(string text)
    {
        NoticeLabel.Text = text;
        NoticeLabel.Visible = !string.IsNullOrEmpty(text);
    }

    void AddMember(INetTransportPlayerInfo member, bool host, bool local, bool canKick)
    {
        string name = string.IsNullOrWhiteSpace(member.name) ? $"玩家 {member.id}" : member.name;
        var card = new PanelContainer { ThemeTypeVariation = "RowPanel" };
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        var label = new Label
        {
            Text = name,
            TooltipText = name,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Roster.AddChild(card);
        card.AddChild(row);
        row.AddChild(label);
        if (host || local)
        {
            row.AddChild(new Label
            {
                Text = host ? (local ? "房主 · 你" : "房主") : "你",
                ThemeTypeVariation = "AccentLabel",
            });
        }

        if (!canKick)
            return;
        var kick = new Button { Text = "踢出", ThemeTypeVariation = "DangerButton" };
        row.AddChild(kick);
        kick.Pressed += () => ConfirmKick(member.id, name);
    }

    void ConfirmKick(ulong peerId, string name)
    {
        pendingKickId = peerId;
        string target = name.Length > 40 ? name[..40] + "…" : name;
        KickDialog.DialogText = DisplayType == ENetLobbyDisplayType.Lan
            ? $"踢出「{target}」？此来源 IP 在房间关闭前无法重新加入。"
            : $"踢出「{target}」？此 Steam 账号在房间关闭前无法重新加入。";
        KickDialog.PopupCentered(new Vector2I(390, 0));
        KickDialog.GetCancelButton().GrabFocus();
    }
}
