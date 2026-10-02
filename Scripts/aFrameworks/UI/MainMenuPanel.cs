using Godot;

/// <summary>
/// Boot menu used when <see cref="Main.menuScene"/> is not assigned.
/// </summary>
public partial class MainMenuPanel : Control
{
	public override void _Ready()
	{
		SetAnchorsPreset(Control.LayoutPreset.FullRect);
		MouseFilter = Control.MouseFilterEnum.Stop;

		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(center);

		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 12);
		center.AddChild(box);

		Label title = new Label { Text = "主菜单" };
		title.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(title);

		Button steam = CreateButton("Steam 大厅");
		steam.Pressed += () => UIManager.Instance.OpenLobby(ENetLobbyDisplayType.Steam);
		box.AddChild(steam);

		Button lan = CreateButton("局域网大厅");
		lan.Pressed += () => UIManager.Instance.OpenLobby(ENetLobbyDisplayType.Lan);
		box.AddChild(lan);

		steam.GrabFocus();
	}

	static Button CreateButton(string text)
	{
		return new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(220, 40)
		};
	}
}
