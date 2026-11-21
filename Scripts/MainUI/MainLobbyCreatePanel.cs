using Godot;
using System;

public partial class MainLobbyCreatePanel : Control
{
    public ENetLobbyDisplayType DisplayType { get; set; }

    [Export] public Label modeLabel;
    [Export] public LineEdit roomNameEdit;
    [Export] public SpinBox maxPlayersSpin;
    [Export] public CheckBox midJoinableCheck;
    [Export] public OptionButton visibilityOption;
    [Export] public Control visibilityRow;
    [Export] public Button createButton;
    [Export] public Button cancelButton;

    MainLobbyPanel returnPanel;
    bool visibilityInitialized;

    public void BindReturnPanel(MainLobbyPanel panel)
    {
        returnPanel = panel;
    }

    public override void _Ready()
    {
        modeLabel ??= GetNodeOrNull<Label>("Panel/Root/VBox/ModeLabel");
        roomNameEdit ??= GetNodeOrNull<LineEdit>("Panel/Root/VBox/RoomNameEdit");
        maxPlayersSpin ??= GetNodeOrNull<SpinBox>("Panel/Root/VBox/MaxPlayersSpin");
        midJoinableCheck ??= GetNodeOrNull<CheckBox>("Panel/Root/VBox/MidJoinableCheck");
        visibilityOption ??= GetNodeOrNull<OptionButton>("Panel/Root/VBox/VisibilityOption");
        createButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/CreateButton");
        cancelButton ??= GetNodeOrNull<Button>("Panel/Root/VBox/Buttons/CancelButton");

        InitializeVisibilityOption();
        ApplyDisplayType();

        if (createButton != null)
            createButton.Pressed += OnCreatePressed;
        if (cancelButton != null)
            cancelButton.Pressed += OnCancelPressed;

        VisibilityChanged += OnVisibilityChanged;
        OnVisibilityChanged();
    }

    void InitializeVisibilityOption()
    {
        if (visibilityInitialized || visibilityOption == null)
            return;

        visibilityInitialized = true;
        visibilityOption.Clear();
        visibilityOption.AddItem("公开", (int)EGameLobbyType.Public);
        visibilityOption.AddItem("仅受邀", (int)EGameLobbyType.Private);
        visibilityOption.AddItem("仅好友", (int)EGameLobbyType.FriendsOnly);
        visibilityOption.Selected = 0;
    }

    void ApplyDisplayType()
    {
        if (modeLabel != null)
            modeLabel.Text = DisplayType == ENetLobbyDisplayType.Steam ? "创建 Steam 房间" : "创建 LAN 房间";

        if (roomNameEdit != null)
            roomNameEdit.PlaceholderText = OnlineRoomListing.FallbackRoomName(DisplayType);

        if (visibilityRow != null)
            visibilityRow.Visible = DisplayType == ENetLobbyDisplayType.Steam;
        else if (visibilityOption != null)
            visibilityOption.Disabled = DisplayType == ENetLobbyDisplayType.Lan;
    }

    void OnCreatePressed()
    {
        var context = new GameOnlineContext
        {
            RoomName = roomNameEdit?.Text?.Trim(),
            MaxPlayers = Math.Max((int)(maxPlayersSpin?.Value ?? 4), 1),
            MidJoinable = midJoinableCheck?.ButtonPressed ?? true,
            Visibility = GetVisibility(),
        };
        Main.Instance.StartCreateLobby(context, DisplayType);
    }

    EGameLobbyType GetVisibility()
    {
        return visibilityOption is { Selected: >= 0 } option
            ? (EGameLobbyType)option.GetSelectedId()
            : EGameLobbyType.Public;
    }

    void OnCancelPressed()
    {
        Hide();
        returnPanel?.RestoreFromCreatePanel();
        QueueFree();
    }

    void OnVisibilityChanged()
    {
        if (!Visible)
            return;

        if (roomNameEdit != null)
            roomNameEdit.GrabFocus();
        else
            UIUtils.FindFirstFocusButton(this)?.GrabFocus();
    }
}
