using Godot;
using System;

public partial class Settings : Control
{
    /* ---------------- GamePlay ---------------- */

    [Export] OptionButton Language;
    [Export] OptionButton WindowMode;
    [Export] CheckBox reverseYCheck;

    /* ---------------- Audio ---------------- */

    [Export] HSlider MasterSlider;
    [Export] HSlider BgSlider;
    [Export] HSlider EffSlider;

    /* ---------------- Visual ---------------- */

    [Export] OptionButton Resolution;
    [Export] CheckBox VSync;
    [Export] OptionButton Msaa;

    /* ---------------- Other ---------------- */

    [Export] Button ResetButton;
    [Export] Button ReturnButton;

    bool loading = false;

    public override void _Ready()
    {
        VisibilityChanged += () =>
        {
            if (Visible)
            {
                //var first = Global.FindFirstFocusableNode(this);
                //first?.GrabFocus();
                //if (first == null)
                //{
                //    GD.PrintErr("settings 界面没找到可聚焦");
                //}
            }
        };

        InitGamePlay();
        InitAudio();
        InitVisual();

        ResetButton.Pressed += ResetToDefault;
        ReturnButton.Pressed += Hide;
        LoadFromUserInfo();
    }

    /* ---------------- GamePlay ---------------- */

    void InitGamePlay()
    {
        //foreach (Language l in Enum.GetValues(typeof(Language)))
        //    Language.AddItem(l.ToString());

        WindowMode.AddItem("Windowed");
        WindowMode.AddItem("Fullscreen");
        WindowMode.AddItem("ExclusiveFullscreen");

        Language.ItemSelected += id =>
        {
            if (loading) return;

            UserInfo.Instance.Language = (Language)id;
        };

        WindowMode.ItemSelected += id =>
        {
            if (loading) return;

            var mode = id switch
            {
                0 => DisplayServer.WindowMode.Windowed,
                1 => DisplayServer.WindowMode.Fullscreen,
                2 => DisplayServer.WindowMode.ExclusiveFullscreen,
                _ => DisplayServer.WindowMode.Windowed
            };

            UserInfo.Instance.WindowMode = mode;
        };

        reverseYCheck.Toggled += v =>
        {
            if (loading) return;
            UserInfo.Instance.reverseY = reverseYCheck.ButtonPressed;
        };
    }

    /* ---------------- Audio ---------------- */

    void InitAudio()
    {
        SetupSlider(MasterSlider, v =>
        {
            UserInfo.Instance.MasterVol = Global.GetDb(v);
        });

        SetupSlider(BgSlider, v =>
        {
            UserInfo.Instance.BgVol = Global.GetDb(v);
        });

        SetupSlider(EffSlider, v =>
        {
            UserInfo.Instance.EffVol = Global.GetDb(v);
        });
    }

    void SetupSlider(HSlider slider, Action<float> onChange)
    {
        slider.MinValue = 0;
        slider.MaxValue = 100;
        slider.Step = 1;

        slider.ValueChanged += v =>
        {
            if (loading) return;
            onChange((float)v);
        };
    }

    /* ---------------- Visual ---------------- */

    void InitVisual()
    {
        foreach (EResolution r in Enum.GetValues(typeof(EResolution)))
            Resolution.AddItem(r.ToString());

        Resolution.ItemSelected += id =>
        {
            if (loading) return;
            UserInfo.Instance.Resolution = (EResolution)id;
        };

        VSync.Toggled += v =>
        {
            if (loading) return;

            DisplayServer.WindowSetVsyncMode(
                v ? DisplayServer.VSyncMode.Enabled
                  : DisplayServer.VSyncMode.Disabled
            );
        };

        Msaa.AddItem("Off");
        Msaa.AddItem("2x");
        Msaa.AddItem("4x");
        Msaa.AddItem("8x");

        Msaa.ItemSelected += id =>
        {
            if (loading) return;

            var mode = id switch
            {
                1 => Viewport.Msaa.Msaa2X,
                2 => Viewport.Msaa.Msaa4X,
                3 => Viewport.Msaa.Msaa8X,
                _ => Viewport.Msaa.Disabled
            };

            GetViewport().Msaa3D = mode;
        };
    }

    /* ---------------- Load ---------------- */

    void LoadFromUserInfo()
    {
        loading = true;

        var u = UserInfo.Instance;

        Language.Select((int)u.Language);

        int wm = u.WindowMode switch
        {
            DisplayServer.WindowMode.Windowed => 0,
            DisplayServer.WindowMode.Maximized => 1,
            DisplayServer.WindowMode.ExclusiveFullscreen => 2,
            _ => 0
        };

        WindowMode.Select(wm);
        reverseYCheck.ButtonPressed = UserInfo.Instance.reverseY;

        MasterSlider.Value = Global.GetVolValue(u.MasterVol);
        BgSlider.Value = Global.GetVolValue(u.BgVol);
        EffSlider.Value = Global.GetVolValue(u.EffVol);

        Resolution.Select((int)u.Resolution);

        loading = false;
    }

    /* ---------------- Reset ---------------- */

    void ResetToDefault()
    {
        UserInfo.Instance.Reset();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
        GetViewport().Msaa3D = Viewport.Msaa.Disabled;
        LoadFromUserInfo();

        UserInfo.Save();
    }
    
    public override void _ExitTree()
    {
        UserInfo.Save();
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Hide();
            AcceptEvent();
        }
    }
}