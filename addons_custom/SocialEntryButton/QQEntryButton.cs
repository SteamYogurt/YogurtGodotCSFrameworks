using Godot;

[GlobalClass]
public partial class QQEntryButton : SocialEntryButtonBase
{
    [ExportGroup("QQ")]
    [Export] public string groupNumber = "";
    [Export] public Texture2D groupQrCode;
    [Export] public string dialogTitle = "加入 QQ 群";
    [Export(PropertyHint.MultilineText)]
    public string dialogMessage = "点击下方可复制群号，或使用二维码加入群聊。";

    public override void _EnterTree()
    {
        base._EnterTree();
        dialogMinSize = new Vector2(360f, 220f);
        qrCodeSize = new Vector2(128f, 128f);
    }

    protected override void HandleSocialPressed()
    {
        string message = string.IsNullOrWhiteSpace(groupNumber)
            ? dialogMessage
            : $"QQ群号：{groupNumber}\n{dialogMessage}";

        ShowInfoDialog(dialogTitle, message, groupQrCode, groupNumber);
    }
}