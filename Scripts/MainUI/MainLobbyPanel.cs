using Godot;
using System;
using System.Collections.Generic;

public partial class MainLobbyPanel : Control
{
    public ENetLobbyDisplayType DisplayType { get; set; }

    [Export] public Label titleLabel;
    [Export] public Label statusLabel;
    [Export] public VBoxContainer roomListContainer;
    [Export] public Button joinButton;
    [Export] public Button refreshButton;
    [Export] public Button createButton;
    [Export] public Button backButton;

    readonly List<MainLobbyRoomItem> roomItems = new();
    MainLobbyRoomItem selectedItem;

    public override void _Ready()
    {
        titleLabel ??= GetNodeOrNull<Label>("Panel/Root/VBox/Header/TitleLabel");
        statusLabel ??= GetNodeOrNull<Label>("Panel/Root/VBox/StatusLabel");
        roomListContainer ??= GetNodeOrNull<VBoxContainer>("Panel/Root/VBox/RoomScroll/RoomList");
        joinButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/JoinButton");
        refreshButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/RefreshButton");
        createButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/CreateButton");
        backButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/BackButton");

        if (joinButton != null)
            joinButton.Pressed += OnJoinPressed;
        if (refreshButton != null)
            refreshButton.Pressed += RefreshRooms;
        if (createButton != null)
            createButton.Pressed += OpenCreatePanel;
        if (backButton != null)
            backButton.Pressed += Main.Instance.ResetAndReturnToMenu;

        VisibilityChanged += OnVisibilityChanged;

        ApplyDisplayType();
        Main.Instance.EnsureNetworkServices(DisplayType);
        SubscribeRoomSources();
        RefreshRooms();
    }

    public override void _ExitTree()
    {
        UnsubscribeRoomSources();
        if (DisplayType == ENetLobbyDisplayType.Lan && Main.Instance != null)
            Main.Instance?.lanDiscoveryService?.StopBrowsing();
    }

    public void RestoreFromCreatePanel()
    {
        Show();
        RefreshRooms();
        FocusDefaultButton();
    }

    void ApplyDisplayType()
    {
        if (titleLabel != null)
            titleLabel.Text = DisplayType == ENetLobbyDisplayType.Steam ? "Steam 大厅" : "LAN 大厅";
        SetStatus("请选择一个房间。", clearSelection: true);
    }

    void SubscribeRoomSources()
    {
        if (DisplayType == ENetLobbyDisplayType.Steam)
            SteamManager.Instance.LobbyListUpdated += OnSteamLobbyListUpdated;
        else if (Main.Instance?.lanDiscoveryService != null)
            Main.Instance.lanDiscoveryService.RoomsChanged += OnLanRoomsChanged;
    }

    void UnsubscribeRoomSources()
    {
        if (SteamManager.Instance != null)
            SteamManager.Instance.LobbyListUpdated -= OnSteamLobbyListUpdated;
        if (Main.Instance?.lanDiscoveryService != null)
            Main.Instance.lanDiscoveryService.RoomsChanged -= OnLanRoomsChanged;
    }

    void OnVisibilityChanged()
    {
        if (!Visible)
            return;

        FocusDefaultButton();
    }

    public void FocusDefaultButton()
    {
        var button = UIUtils.FindFirstFocusButton(this);
        button?.GrabFocus();
    }

    void RefreshRooms()
    {
        SetStatus("正在刷新房间列表...", clearSelection: true);
        if (DisplayType == ENetLobbyDisplayType.Steam)
        {
            SteamManager.Instance?.RequestLobbyList();
            return;
        }

        Main.Instance?.lanDiscoveryService?.StartBrowsing();
        RenderLanRooms(Main.Instance?.lanDiscoveryService?.GetRooms());
    }

    void OpenCreatePanel()
    {
        var panel = Global.GetObj<MainLobbyCreatePanel>("res://Scene/UI/Main/MainLobbyCreatePanel.tscn");
        panel.DisplayType = DisplayType;
        panel.BindReturnPanel(this);
        Hide();
        Main.Instance.AddUI(panel);
    }

    void OnJoinPressed()
    {
        if (selectedItem == null || string.IsNullOrWhiteSpace(selectedItem.RoomId))
            return;

        Main.Instance.StartJoinLobby(selectedItem.RoomId, DisplayType);
    }

    void OnRoomItemSelected(MainLobbyRoomItem item)
    {
        selectedItem = item;
        foreach (var roomItem in roomItems)
            roomItem.SetSelected(roomItem == item);

        if (joinButton != null)
            joinButton.Disabled = !item.Joinable;

        SetStatus(item.Joinable ? "已选择房间，可加入。" : "该房间当前不可加入。", clearSelection: false);
    }

    void OnSteamLobbyListUpdated(IReadOnlyList<SteamLobbyRoomInfo> rooms)
    {
        RenderRooms(rooms, room => room.LobbyId.ToString(), room => room.RoomName,
            room => FormatDetail(room.PlayerCount, room.MemberLimit, room.Joinable), room => room.Joinable);
    }

    void OnLanRoomsChanged()
    {
        RenderLanRooms(Main.Instance?.lanDiscoveryService?.GetRooms());
    }

    void RenderLanRooms(IReadOnlyList<LanDiscoveredRoomInfo> rooms)
    {
        RenderRooms(rooms, room => room.HostAddress, room => room.RoomName,
            room => FormatDetail(room.PlayerCount, 0, room.Joinable), room => room.Joinable);
    }

    void RenderRooms<T>(IReadOnlyList<T> rooms, Func<T, string> getRoomId, Func<T, string> getRoomName,
        Func<T, string> getDetail, Func<T, bool> getJoinable)
    {
        ClearRoomItems();

        if (rooms == null || rooms.Count == 0)
        {
            SetStatus("未找到可用房间。", clearSelection: true);
            return;
        }

        foreach (var room in rooms)
        {
            var item = Global.GetObj<MainLobbyRoomItem>("res://Scene/UI/Main/MainLobbyRoomItem.tscn");
            item.Setup(getRoomId(room), getRoomName(room), getDetail(room), getJoinable(room));
            item.Selected += OnRoomItemSelected;
            roomListContainer?.AddChild(item);
            roomItems.Add(item);
        }

        SetStatus($"已刷新到 {rooms.Count} 个房间。", clearSelection: true);
        if (roomItems.Count > 0)
            OnRoomItemSelected(roomItems[0]);
    }

    string FormatDetail(int playerCount, int memberLimit, bool joinable)
    {
        string countText = memberLimit > 0 ? $"人数: {playerCount}/{memberLimit}" : $"人数: {playerCount}";
        return joinable ? countText : $"{countText} | 不可加入";
    }

    void ClearRoomItems()
    {
        selectedItem = null;
        if (joinButton != null)
            joinButton.Disabled = true;

        for (int i = 0; i < roomItems.Count; i++)
        {
            var item = roomItems[i];
            if (item == null)
                continue;
            item.Selected -= OnRoomItemSelected;
            item.QueueFree();
        }
        roomItems.Clear();
    }

    void SetStatus(string text, bool clearSelection)
    {
        if (statusLabel != null)
            statusLabel.Text = text;
        if (clearSelection && joinButton != null)
            joinButton.Disabled = true;
    }
}
