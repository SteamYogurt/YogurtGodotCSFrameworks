using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

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
    [Export] public Label noticeLabel;
    [Export] public Control directJoinRow;
    [Export] public LineEdit addressEdit;
    [Export] public Button connectButton;
    [Export] public PackedScene lobbyEntranceItemScene;
    [Export] public PackedScene createPanelScene;

    readonly List<MainLobbyRoomItem> roomItems = new();
    MainLobbyRoomItem selectedItem;
    bool IsLan => DisplayType == ENetLobbyDisplayType.Lan;

    public override void _Ready()
    {
        titleLabel ??= GetNodeOrNull<Label>("Panel/Root/VBox/Header/TitleLabel");
        statusLabel ??= GetNodeOrNull<Label>("Panel/Root/VBox/StatusLabel");
        roomListContainer ??= GetNodeOrNull<VBoxContainer>("Panel/Root/VBox/RoomScroll/RoomList");
        joinButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/JoinButton");
        refreshButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/RefreshButton");
        createButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/CreateButton");
        backButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/BackButton");

        if (titleLabel != null)
            titleLabel.Text = IsLan ? "局域网大厅" : "Steam 大厅";
        if (joinButton != null)
        {
            joinButton.Disabled = true;
            joinButton.Pressed += JoinSelectedRoom;
        }
        if (refreshButton != null)
            refreshButton.Pressed += RefreshRooms;
        if (createButton != null)
            createButton.Pressed += OpenCreatePanel;
        if (backButton != null)
            backButton.Pressed += Main.Instance.ResetAndReturnToMenu;
        if (directJoinRow != null)
            directJoinRow.Visible = IsLan;
        if (addressEdit != null && connectButton != null)
        {
            addressEdit.TextChanged += text => connectButton.Disabled = string.IsNullOrWhiteSpace(text);
            addressEdit.TextSubmitted += _ => JoinAddress();
            connectButton.Pressed += JoinAddress;
            connectButton.Disabled = string.IsNullOrWhiteSpace(addressEdit.Text);
        }

        if (IsLan)
            Main.Instance.lanDiscoveryService.RoomsChanged += OnLanRoomsChanged;
        else if (SteamManager.Instance != null)
            SteamManager.Instance.LobbyListUpdated += OnSteamLobbyListUpdated;

        RefreshRooms();
        FocusDefaultButton();
    }

    public override void _ExitTree()
    {
        if (IsLan && Main.Instance?.lanDiscoveryService is { } lan)
        {
            lan.RoomsChanged -= OnLanRoomsChanged;
            lan.StopBrowsing();
        }
        else if (SteamManager.Instance != null)
        {
            SteamManager.Instance.LobbyListUpdated -= OnSteamLobbyListUpdated;
        }
    }

    public void RestoreFromCreatePanel()
    {
        Show();
        RefreshRooms();
        FocusDefaultButton();
    }

    public void ShowNotice(string text)
    {
        if (noticeLabel == null)
            return;
        noticeLabel.Text = text;
        noticeLabel.Visible = !string.IsNullOrEmpty(text);
    }

    public void FocusDefaultButton()
    {
        Button button = selectedItem;
        button ??= createButton?.Disabled == false ? createButton : backButton;
        button?.GrabFocus();
    }

    void RefreshRooms()
    {
        if (IsLan)
        {
            Main.Instance.lanDiscoveryService.StartBrowsing();
            OnLanRoomsChanged();
            return;
        }

        bool available = SteamManager.Instance?.Inited == true;
        if (createButton != null)
            createButton.Disabled = !available;
        if (statusLabel != null)
            statusLabel.Text = available ? "正在刷新…" : "请启动 Steam 后重开游戏。";
        if (available)
            SteamManager.Instance.RequestLobbyList();
    }

    void OpenCreatePanel()
    {
        if (createPanelScene == null)
            return;
        var panel = createPanelScene.Instantiate<MainLobbyCreatePanel>();
        panel.DisplayType = DisplayType;
        panel.BindReturnPanel(this);
        Hide();
        Main.Instance.AddUI(panel);
    }

    void JoinSelectedRoom()
    {
        if (selectedItem?.Joinable == true && !string.IsNullOrEmpty(selectedItem.RoomId))
            Main.Instance.StartJoinLobby(selectedItem.RoomId, DisplayType);
    }

    void JoinAddress()
    {
        string address = addressEdit?.Text.Trim();
        if (IsLan && !string.IsNullOrEmpty(address))
            Main.Instance.StartJoinLobby(address, DisplayType);
    }

    void SelectRoom(MainLobbyRoomItem item)
    {
        selectedItem = item;
        foreach (var roomItem in roomItems)
            roomItem.SetSelected(roomItem == item);
        if (joinButton != null)
            joinButton.Disabled = !item.Joinable;
    }

    void OnSteamLobbyListUpdated(IReadOnlyList<SteamLobbyRoomInfo> rooms) =>
        RenderEntries(rooms.Select(OnlineRoomListing.FromSteam));

    void OnLanRoomsChanged() =>
        RenderEntries(Main.Instance.lanDiscoveryService.GetRooms().Select(OnlineRoomListing.FromLan));

    void RenderEntries(IEnumerable<OnlineRoomListEntry> entries)
    {
        var rooms = entries.Where(room => room != null && !string.IsNullOrEmpty(room.RoomId))
            .OrderByDescending(room => room.CanJoin)
            .ThenBy(room => room.RoomName, StringComparer.CurrentCulture).ToArray();
        if (statusLabel != null)
            statusLabel.Text = rooms.Length == 0 ? "暂未发现房间" : $"{rooms.Length} 个房间";
        if (lobbyEntranceItemScene == null || roomListContainer == null)
            return;

        string selection = selectedItem?.RoomId;
        bool listFocused = roomItems.Contains(GetViewport().GuiGetFocusOwner() as MainLobbyRoomItem);
        selectedItem = null;
        if (joinButton != null)
            joinButton.Disabled = true;
        foreach (var item in roomItems)
        {
            item.Selected -= SelectRoom;
            item.Activated -= JoinSelectedRoom;
            roomListContainer.RemoveChild(item);
            item.QueueFree();
        }
        roomItems.Clear();

        foreach (var room in rooms)
        {
            var item = lobbyEntranceItemScene.Instantiate<MainLobbyRoomItem>();
            item.Setup(room.RoomId, room.RoomName, FormatDetail(room), room.CanJoin);
            item.Selected += SelectRoom;
            item.Activated += JoinSelectedRoom;
            roomListContainer.AddChild(item);
            roomItems.Add(item);
        }

        var selected = roomItems.Find(item => item.RoomId == selection) ?? roomItems.FirstOrDefault();
        if (selected != null)
            SelectRoom(selected);
        if (listFocused)
            FocusDefaultButton();
    }

    static string FormatDetail(OnlineRoomListEntry room)
    {
        string count = room.MemberLimit > 0
            ? $"{room.PlayerCount} / {room.MemberLimit}"
            : $"{room.PlayerCount} 人";
        if (OnlineRoomListing.IsFull(room.PlayerCount, room.MemberLimit))
            return $"{count}  ·  已满";
        if (!room.CanJoin)
            return $"{count}  ·  暂停加入";
        return room.InMatch ? $"{count}  ·  对局中" : count;
    }
}