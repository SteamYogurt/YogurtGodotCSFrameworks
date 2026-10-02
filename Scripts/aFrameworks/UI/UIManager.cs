using System.Collections.Generic;
using Godot;

/// <summary>
/// Screen layer for <see cref="Main"/>. Persistent nodes (waiting, settings, transition)
/// stay up; menus and lobby panels are transient.
/// Does not reference <c>Game</c>.
/// </summary>
public partial class UIManager : Singleton<UIManager>
{
	public const string DefaultLobbyPanelPath = "res://Scene/UI/Main/MainLobbyPanel.tscn";
	public const string DefaultSettingsPath = "res://addons_custom/Settings/Settings.tscn";
	public const string DefaultTransitionPath = "res://Scene/UI/Main/Transition.tscn";

	readonly HashSet<Node> persistent = new();

	public CanvasLayer Layer { get; private set; }

	CanvasItem waiting;
	Control settings;
	Control transition;
	Tween transitionTween;

	public override void _Ready()
	{
		base._Ready();
		Layer = new CanvasLayer { Layer = 10, Name = "UILayer" };
		AddChild(Layer);

		TryMountSettings();
		TryMountTransition();
		MountWaiting();
		ShowMenu();
	}

	public void Add(Node control)
	{
		if (control == null || Layer == null)
		{
			return;
		}

		Layer.AddChild(control);
		RaiseSettings();
	}

	public void AddPersistent(Node control)
	{
		if (control == null)
		{
			return;
		}

		persistent.Add(control);
		Add(control);
	}

	public void ClearTransient()
	{
		if (Layer == null)
		{
			return;
		}

		foreach (Node child in Layer.GetChildren())
		{
			if (!persistent.Contains(child))
			{
				child.QueueFree();
			}
		}
	}

	public void ShowMenu()
	{
		ClearTransient();
		HideWaiting();
		Add(CreateMenu());
	}

	public void OpenLobby(ENetLobbyDisplayType displayType)
	{
		MainLobbyPanel panel = CreateLobbyPanel();
		if (panel == null)
		{
			GD.PrintErr("找不到大厅面板场景。可指定 Main.lobbyPanelScene，或提供 " + DefaultLobbyPanelPath);
			return;
		}

		panel.DisplayType = displayType;
		Add(panel);
	}

	public void ShowWaiting()
	{
		if (waiting == null)
		{
			return;
		}

		waiting.Show();
		if (waiting.GetParent() == Layer)
		{
			Layer.MoveChild(waiting, Layer.GetChildCount() - 1);
		}

		RaiseSettings();
	}

	public void HideWaiting()
	{
		waiting?.Hide();
	}

	public void PlayTransition(bool forward)
	{
		if (transition == null || !GodotObject.IsInstanceValid(transition))
		{
			return;
		}

		if (transition.Material is not ShaderMaterial)
		{
			return;
		}

		transitionTween?.Kill();
		float target = forward ? 1f : 0f;
		SetTransitionProgress(1 - target);
		transitionTween = CreateTween();
		transitionTween.TweenMethod(
			Callable.From<float>(SetTransitionProgress),
			1 - target,
			target,
			0.7f);
	}

	void SetTransitionProgress(float value)
	{
		if (transition?.Material is ShaderMaterial material)
		{
			material.SetShaderParameter("progress", value);
		}
	}

	Control CreateMenu()
	{
		PackedScene scene = Main.Instance?.menuScene;
		if (scene != null)
		{
			return scene.Instantiate<Control>();
		}

		return new MainMenuPanel();
	}

	MainLobbyPanel CreateLobbyPanel()
	{
		PackedScene scene = Main.Instance?.lobbyPanelScene;
		if (scene != null)
		{
			return scene.Instantiate<MainLobbyPanel>();
		}

		if (!ResourceLoader.Exists(DefaultLobbyPanelPath))
		{
			return null;
		}

		return GD.Load<PackedScene>(DefaultLobbyPanelPath).Instantiate<MainLobbyPanel>();
	}

	void MountWaiting()
	{
		PackedScene scene = Main.Instance?.waitingPanelScene;
		if (scene != null)
		{
			waiting = scene.Instantiate<CanvasItem>();
		}
		else
		{
			waiting = new WaitingOverlay();
		}

		waiting.Visible = false;
		AddPersistent(waiting);
	}

	void TryMountSettings()
	{
		PackedScene scene = Main.Instance?.settingsScene;
		if (scene == null && ResourceLoader.Exists(DefaultSettingsPath))
		{
			scene = GD.Load<PackedScene>(DefaultSettingsPath);
		}

		if (scene == null)
		{
			return;
		}

		settings = scene.Instantiate<Control>();
		settings.Visible = false;
		AddPersistent(settings);
	}

	void TryMountTransition()
	{
		PackedScene scene = Main.Instance?.transitionScene;
		if (scene == null && ResourceLoader.Exists(DefaultTransitionPath))
		{
			scene = GD.Load<PackedScene>(DefaultTransitionPath);
		}

		if (scene == null)
		{
			return;
		}

		transition = scene.Instantiate<Control>();
		AddPersistent(transition);
	}

	void RaiseSettings()
	{
		if (settings != null && GodotObject.IsInstanceValid(settings) && settings.GetParent() == Layer)
		{
			Layer.MoveChild(settings, Layer.GetChildCount() - 1);
		}
	}
}
