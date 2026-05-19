using Godot;

[GlobalClass]
public partial class DiscordEntryButton : SocialEntryButtonBase
{
    [ExportGroup("Discord")]
    [Export] public string targetUrl = "https://discord.com/";
    [Export] public string confirmTitle = "打开 Discord";
    [Export(PropertyHint.MultilineText)]
    public string confirmMessage = "即将使用系统默认浏览器打开 Discord 页面，是否继续？";

    protected override void HandleSocialPressed()
    {
        ShowConfirmDialog(confirmTitle, confirmMessage, () => OpenExternalUrl(targetUrl));
    }
}