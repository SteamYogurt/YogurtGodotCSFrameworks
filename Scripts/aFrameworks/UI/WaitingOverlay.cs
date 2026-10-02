using Godot;

/// <summary>
/// Waiting screen used when <see cref="Main.waitingPanelScene"/> is not assigned.
/// </summary>
public partial class WaitingOverlay : Control
{
	public override void _Ready()
	{
		SetAnchorsPreset(Control.LayoutPreset.FullRect);
		MouseFilter = Control.MouseFilterEnum.Stop;

		ColorRect background = new ColorRect();
		background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		background.Color = new Color(0, 0, 0, 0.55f);
		background.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(background);

		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(center);

		VBoxContainer box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 12);
		center.AddChild(box);

		Label label = new Label { Text = "请稍候…" };
		label.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(label);

		Button cancel = new Button
		{
			Text = "取消",
			CustomMinimumSize = new Vector2(160, 36)
		};
		cancel.Pressed += () => Main.Instance?.ResetAndReturnToMenu();
		box.AddChild(cancel);

		VisibilityChanged += () =>
		{
			if (Visible)
			{
				cancel.GrabFocus();
			}
		};
	}
}
