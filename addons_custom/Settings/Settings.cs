using Godot;

public partial class Settings : Control
{
    static readonly string[] WindowIds =
    {
        UserInfo.WindowWindowed,
        UserInfo.WindowBorderless,
        UserInfo.WindowExclusive,
    };

    [Export] OptionButton Language;
    [Export] OptionButton WindowMode;
    [Export] CheckBox reverseYCheck;

    [Export] HSlider MasterSlider;
    [Export] HSlider BgSlider;
    [Export] HSlider EffSlider;

    [Export] OptionButton Resolution;
    [Export] CheckBox VSync;
    [Export] OptionButton Msaa;

    [Export] Button ResetButton;
    [Export] Button ReturnButton;

    bool loading;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        InitGamePlay();
        InitAudio();
        InitVisual();
        ResetButton.Pressed += ResetToDefault;
        ReturnButton.Pressed += () => UIManager.Instance?.HideSettings();
        Hide();
    }

    public void Present()
    {
        LoadFromUserInfo();
        Show();
    }

    public void Dismiss()
    {
        Hide();
    }

    void InitGamePlay()
    {
        Language.Clear();
        for (int i = 0; i < Localization.Locales.Length; i++)
        {
            Language.AddItem(Localization.NativeName(Localization.Locales[i]), i);
        }

        Language.ItemSelected += id =>
        {
            if (loading || UserInfo.Instance == null)
            {
                return;
            }

            if (id < 0 || id >= Localization.Locales.Length)
            {
                return;
            }

            UserInfo.Instance.Locale = Localization.Locales[id];
            UserInfo.Instance.ApplyLocale();
            UserInfo.Instance.SaveToDisk();
        };

        WindowMode.Clear();
        WindowMode.AddItem("窗口");
        WindowMode.AddItem("无边框全屏");
        WindowMode.AddItem("独占全屏");

        WindowMode.ItemSelected += id =>
        {
            if (loading || UserInfo.Instance == null)
            {
                return;
            }

            if (id < 0 || id >= WindowIds.Length)
            {
                return;
            }

            UserInfo.Instance.WindowMode = WindowIds[id];
            UserInfo.Instance.ApplyDisplay();
            UserInfo.Instance.SaveToDisk();
        };

        if (reverseYCheck != null)
        {
            reverseYCheck.Toggled += on =>
            {
                if (loading || UserInfo.Instance == null)
                {
                    return;
                }

                UserInfo.Instance.InvertLookY = on;
                UserInfo.Instance.SaveToDisk();
            };
        }
    }

    void InitAudio()
    {
        SetupSlider(MasterSlider, v => UserInfo.Instance.MasterVolume = v, () => UserInfo.Instance.ApplyAudio());
        SetupSlider(BgSlider, v => UserInfo.Instance.MusicVolume = v, () => UserInfo.Instance.ApplyAudio());
        SetupSlider(EffSlider, v => UserInfo.Instance.SfxVolume = v, () => UserInfo.Instance.ApplyAudio());
    }

    void SetupSlider(HSlider slider, System.Action<float> assign, System.Action apply)
    {
        slider.MinValue = 0;
        slider.MaxValue = 100;
        slider.Step = 1;
        slider.ValueChanged += v =>
        {
            if (loading || UserInfo.Instance == null)
            {
                return;
            }

            assign(Mathf.Clamp((float)v / 100f, 0f, 1f));
            apply();
        };
        slider.DragEnded += _ =>
        {
            if (loading)
            {
                return;
            }

            UserInfo.Instance?.SaveToDisk();
        };
    }

    void InitVisual()
    {
        if (Resolution != null)
        {
            if (Resolution.GetParent() is CanvasItem parent)
            {
                parent.Hide();
            }
            else
            {
                Resolution.Hide();
            }
        }

        VSync.Toggled += on =>
        {
            if (loading || UserInfo.Instance == null)
            {
                return;
            }

            UserInfo.Instance.Vsync = on;
            UserInfo.Instance.ApplyDisplay();
            UserInfo.Instance.SaveToDisk();
        };

        Msaa.Clear();
        Msaa.AddItem("Off");
        Msaa.AddItem("2x");
        Msaa.AddItem("4x");
        Msaa.AddItem("8x");
        Msaa.ItemSelected += id =>
        {
            if (loading || UserInfo.Instance == null)
            {
                return;
            }

            UserInfo.Instance.Msaa3D = id switch
            {
                1 => Viewport.Msaa.Msaa2X,
                2 => Viewport.Msaa.Msaa4X,
                3 => Viewport.Msaa.Msaa8X,
                _ => Viewport.Msaa.Disabled,
            };
            UserInfo.Instance.ApplyDisplay();
            UserInfo.Instance.SaveToDisk();
        };
    }

    void LoadFromUserInfo()
    {
        if (UserInfo.Instance == null)
        {
            return;
        }

        loading = true;
        UserInfo u = UserInfo.Instance;

        int locIdx = Localization.IndexOf(u.Locale);
        if (locIdx >= 0)
        {
            Language.Select(locIdx);
        }

        int wm = System.Array.IndexOf(WindowIds, u.WindowMode);
        WindowMode.Select(wm >= 0 ? wm : 0);

        if (reverseYCheck != null)
        {
            reverseYCheck.SetPressedNoSignal(u.InvertLookY);
        }

        MasterSlider.Value = u.MasterVolume * 100f;
        BgSlider.Value = u.MusicVolume * 100f;
        EffSlider.Value = u.SfxVolume * 100f;

        VSync.SetPressedNoSignal(u.Vsync);
        Msaa.Select(MsaaIndex(u.Msaa3D));
        loading = false;
    }

    static int MsaaIndex(Viewport.Msaa mode)
    {
        return mode switch
        {
            Viewport.Msaa.Msaa2X => 1,
            Viewport.Msaa.Msaa4X => 2,
            Viewport.Msaa.Msaa8X => 3,
            _ => 0,
        };
    }

    void ResetToDefault()
    {
        UserInfo.Instance?.ResetToDefaults();
        LoadFromUserInfo();
    }
}
