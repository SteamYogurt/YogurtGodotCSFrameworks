using Godot;

[GlobalClass]
public partial class SteamEntryButton : SocialEntryButtonBase
{
    [ExportGroup("Steam")]
    [Export] public string targetUrl = "https://store.steampowered.com/";
    [Export] public string confirmTitle = "打开 Steam";
    [Export(PropertyHint.MultilineText)]
    public string confirmMessage = "即将使用系统默认浏览器打开 Steam 页面，是否继续？";

    protected override void HandleSocialPressed()
    {
        ShowConfirmDialog(confirmTitle, confirmMessage, () => OpenExternalUrl(targetUrl));
    }
}