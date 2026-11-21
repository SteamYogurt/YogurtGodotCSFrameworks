using Godot;

[GlobalClass]
public partial class WishlistEntryButton : Button
{
    public override void _Ready()
    {
        if (string.IsNullOrWhiteSpace(Text))
            Text = "加入愿望单";

        Pressed += OnWishlistPressed;
        RefreshVisibility();
        CallDeferred(nameof(RefreshVisibility));
    }

    public void RefreshVisibility()
    {
        Visible = SteamManager.OfferWishlist;
    }

    void OnWishlistPressed()
    {
        SteamManager.OpenWishlistPage();
    }
}
