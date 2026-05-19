using Godot;
using System;

[GlobalClass]
public abstract partial class SocialEntryButtonBase : SimpleButton
{
    [ExportGroup("Dialog")]
    [Export] public bool playClickEffect = true;
    [Export] public Color overlayColor = new Color(0f, 0f, 0f, 0.65f);
    [Export] public Vector2 dialogMinSize = new Vector2(520f, 260f);
    [Export] public Vector2 qrCodeSize = new Vector2(220f, 220f);

    public override void _Ready()
    {
        base._Ready();
        PressedDlg += OnSocialPressed;
    }

    private void OnSocialPressed()
    {
        //if (playClickEffect)
        //    UIStatic.PlayClickEffect();

        HandleSocialPressed();
        CallDeferred(nameof(Cancel));
    }

    protected abstract void HandleSocialPressed();

    protected void OpenExternalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            GD.PushWarning($"{GetType().Name} 未配置目标链接。");
            return;
        }

        Error err = OS.ShellOpen(url);
        if (err != Error.Ok)
            GD.PushError($"{GetType().Name} 打开链接失败：{url}，错误：{err}");
    }

    protected void ShowConfirmDialog(string title, string message, Action onConfirm)
    {
        Control overlay = CreateOverlay();
        VBoxContainer content = CreateDialogContent(overlay, title);

        Label messageLabel = CreateMessageLabel(message);
        content.AddChild(messageLabel);

        HBoxContainer buttonRow = CreateButtonRow();
        content.AddChild(buttonRow);

        Button confirmButton = CreateButton("继续");
        confirmButton.Pressed += () =>
        {
            overlay.QueueFree();
            onConfirm?.Invoke();
        };
        buttonRow.AddChild(confirmButton);

        Button cancelButton = CreateButton("取消");
        cancelButton.Pressed += () => overlay.QueueFree();
        buttonRow.AddChild(cancelButton);

        AddOverlayToUi(overlay);
        confirmButton.CallDeferred(Button.MethodName.GrabFocus);
    }

    protected void ShowInfoDialog(string title, string message, Texture2D qrTexture, string copyText)
    {
        Control overlay = CreateOverlay();
        VBoxContainer content = CreateDialogContent(overlay, title);

        Label messageLabel = CreateMessageLabel(message);
        content.AddChild(messageLabel);

        if (qrTexture != null)
        {
            CenterContainer qrCenter = new CenterContainer();
            qrCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            content.AddChild(qrCenter);

            TextureRect qrRect = new TextureRect();
            qrRect.Texture = qrTexture;
            qrRect.CustomMinimumSize = qrCodeSize;
            qrRect.ExpandMode = ExpandModeEnum.IgnoreSize;
            qrRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            qrCenter.AddChild(qrRect);
        }

        HBoxContainer buttonRow = CreateButtonRow();
        content.AddChild(buttonRow);

        if (!string.IsNullOrWhiteSpace(copyText))
        {
            Button copyButton = CreateButton("复制群号");
            copyButton.Pressed += () => DisplayServer.ClipboardSet(copyText);
            buttonRow.AddChild(copyButton);
        }

        Button closeButton = CreateButton("关闭");
        closeButton.Pressed += () => overlay.QueueFree();
        buttonRow.AddChild(closeButton);

        AddOverlayToUi(overlay);
        closeButton.CallDeferred(Button.MethodName.GrabFocus);
    }

    private void AddOverlayToUi(Control overlay)
    {
        Node parent = Main.Instance.uiLayer;
        parent.AddChild(overlay);
    }

    private Control CreateOverlay()
    {
        Control overlay = new Control();
        overlay.Name = $"{GetType().Name}_Overlay";
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.OffsetLeft = 0f;
        overlay.OffsetTop = 0f;
        overlay.OffsetRight = 0f;
        overlay.OffsetBottom = 0f;
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;

        ColorRect background = new ColorRect();
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.OffsetLeft = 0f;
        background.OffsetTop = 0f;
        background.OffsetRight = 0f;
        background.OffsetBottom = 0f;
        background.Color = overlayColor;
        background.MouseFilter = Control.MouseFilterEnum.Stop;
        overlay.AddChild(background);

        return overlay;
    }

    private VBoxContainer CreateDialogContent(Control overlay, string title)
    {
        CenterContainer center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.OffsetLeft = 0f;
        center.OffsetTop = 0f;
        center.OffsetRight = 0f;
        center.OffsetBottom = 0f;
        center.MouseFilter = Control.MouseFilterEnum.Stop;
        overlay.AddChild(center);

        PanelContainer panel = new PanelContainer();
        panel.CustomMinimumSize = dialogMinSize;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        center.AddChild(panel);

        MarginContainer margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        VBoxContainer content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 16);
        margin.AddChild(content);

        Label titleLabel = new Label();
        titleLabel.Text = title;
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(titleLabel);

        return content;
    }

    private Label CreateMessageLabel(string message)
    {
        Label label = new Label();
        label.Text = message;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private HBoxContainer CreateButtonRow()
    {
        HBoxContainer row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 12);
        return row;
    }

    private Button CreateButton(string text)
    {
        Button button = new Button();
        button.Text = text;
        button.CustomMinimumSize = new Vector2(120f, 42f);
        return button;
    }
}