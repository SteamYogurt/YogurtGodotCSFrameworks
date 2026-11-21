using Godot;

/// <summary>
/// Player settings. Created by <see cref="Main"/>, not an Autoload.
/// Locale, window, vsync, pixel snap, frame cap, and volume.
/// </summary>
public partial class UserInfo : Singleton<UserInfo>
{
    public const string SavePath = "user://user_info.cfg";
    public const string BusMusic = "Music";
    public const string BusSfx = "SFX";
    public const string WindowWindowed = "windowed";
    public const string WindowBorderless = "borderless";
    public const string WindowExclusive = "exclusive";

    public string Locale = "en";
    public string WindowMode = WindowBorderless;
    public bool Vsync = true;
    public bool IntegerScale;
    public bool PixelSnap = true;
    public int MaxFps;
    public float MasterVolume = 1f;
    public float MusicVolume = 1f;
    public float SfxVolume = 1f;
    public bool Muted;
    public Viewport.Msaa Msaa3D = Viewport.Msaa.Disabled;
    public bool UseDebanding;
    public bool InvertLookY;

    string appliedLocale = "";

    public static UserInfo LoadUserInfo()
    {
        return new UserInfo();
    }

    public static void Save()
    {
        Instance?.SaveToDisk();
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        LoadFromDisk();
    }

    public override void _Ready()
    {
        EnsureAudioBuses();
        Apply();
    }

    public void LoadFromDisk()
    {
        var cfg = new ConfigFile();
        Error err = cfg.Load(SavePath);
        if (err != Error.Ok)
        {
            Locale = Localization.ResolveLocale("");
            return;
        }

        Locale = Localization.ResolveLocale(cfg.GetValue("settings", "locale", Locale).AsString());
        WindowMode = SanitizeWindowMode(cfg.GetValue("settings", "window_mode", WindowMode).AsString());
        Vsync = cfg.GetValue("settings", "vsync", Vsync).AsBool();
        IntegerScale = cfg.GetValue("settings", "integer_scale", IntegerScale).AsBool();
        PixelSnap = cfg.GetValue("settings", "pixel_snap", PixelSnap).AsBool();
        MaxFps = SanitizeMaxFps(cfg.GetValue("settings", "max_fps", MaxFps).AsInt32());
        MasterVolume = SanitizeVolume(cfg.GetValue("settings", "master_volume", MasterVolume).AsSingle());
        MusicVolume = SanitizeVolume(cfg.GetValue("settings", "music_volume", MusicVolume).AsSingle());
        SfxVolume = SanitizeVolume(cfg.GetValue("settings", "sfx_volume", SfxVolume).AsSingle());
        Muted = cfg.GetValue("settings", "muted", Muted).AsBool();
        Msaa3D = SanitizeMsaa(cfg.GetValue("settings", "msaa_3d", (int)Msaa3D).AsInt32());
        UseDebanding = cfg.GetValue("settings", "use_debanding", UseDebanding).AsBool();
        InvertLookY = cfg.GetValue("settings", "invert_look_y", InvertLookY).AsBool();
    }

    public void SaveToDisk()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("meta", "version", 1);
        cfg.SetValue("settings", "locale", Locale);
        cfg.SetValue("settings", "window_mode", WindowMode);
        cfg.SetValue("settings", "vsync", Vsync);
        cfg.SetValue("settings", "integer_scale", IntegerScale);
        cfg.SetValue("settings", "pixel_snap", PixelSnap);
        cfg.SetValue("settings", "max_fps", MaxFps);
        cfg.SetValue("settings", "master_volume", MasterVolume);
        cfg.SetValue("settings", "music_volume", MusicVolume);
        cfg.SetValue("settings", "sfx_volume", SfxVolume);
        cfg.SetValue("settings", "muted", Muted);
        cfg.SetValue("settings", "msaa_3d", (int)Msaa3D);
        cfg.SetValue("settings", "use_debanding", UseDebanding);
        cfg.SetValue("settings", "invert_look_y", InvertLookY);
        Error err = cfg.Save(SavePath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"UserInfo: failed to save {SavePath} ({err})");
        }
    }

    public void Apply()
    {
        ApplyLocale();
        ApplyDisplay();
        ApplyAudio();
    }

    public void ApplyLocale()
    {
        Locale = Localization.ResolveLocale(Locale);
        if (appliedLocale == Locale)
        {
            return;
        }

        appliedLocale = Locale;
        TranslationServer.SetLocale(Locale);
    }

    public void ApplyAndSave()
    {
        Apply();
        SaveToDisk();
    }

    public void ResetToDefaults()
    {
        Locale = Localization.ResolveLocale("");
        WindowMode = WindowBorderless;
        Vsync = true;
        IntegerScale = false;
        PixelSnap = true;
        MaxFps = 0;
        MasterVolume = 1f;
        MusicVolume = 1f;
        SfxVolume = 1f;
        Muted = false;
        Msaa3D = Viewport.Msaa.Disabled;
        UseDebanding = false;
        InvertLookY = false;
        ApplyAndSave();
    }

    public void Reset()
    {
        ResetToDefaults();
    }

    public void ApplyDisplay()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        Window win = GetWindow();
        if (win == null)
        {
            return;
        }

        Window.ModeEnum wantedMode = WantedWindowMode();
        if (!WindowModeMatches(win, wantedMode))
        {
            win.Mode = wantedMode;
        }

        DisplayServer.VSyncMode wantedVsync = Vsync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled;
        if (DisplayServer.WindowGetVsyncMode() != wantedVsync)
        {
            DisplayServer.WindowSetVsyncMode(wantedVsync);
        }

        Window.ContentScaleStretchEnum wantedStretch = IntegerScale
            ? Window.ContentScaleStretchEnum.Integer
            : Window.ContentScaleStretchEnum.Fractional;
        if (win.ContentScaleStretch != wantedStretch)
        {
            win.ContentScaleStretch = wantedStretch;
        }

        Viewport vp = GetViewport();
        if (vp != null)
        {
            if (vp.Snap2DTransformsToPixel != PixelSnap)
            {
                vp.Snap2DTransformsToPixel = PixelSnap;
            }

            if (vp.Snap2DVerticesToPixel != PixelSnap)
            {
                vp.Snap2DVerticesToPixel = PixelSnap;
            }

            if (vp.Msaa3D != Msaa3D)
            {
                vp.Msaa3D = Msaa3D;
            }

            if (vp.UseDebanding != UseDebanding)
            {
                vp.UseDebanding = UseDebanding;
            }
        }

        int fps = Mathf.Max(MaxFps, 0);
        if (Engine.MaxFps != fps)
        {
            Engine.MaxFps = fps;
        }
    }

    public void ApplyAudio()
    {
        EnsureAudioBuses();
        SetBusVolume("Master", MasterVolume);
        SetBusVolume(BusMusic, MusicVolume);
        SetBusVolume(BusSfx, SfxVolume);
        int masterIdx = AudioServer.GetBusIndex("Master");
        if (masterIdx >= 0)
        {
            AudioServer.SetBusMute(masterIdx, Muted);
        }
    }

    public void EnsureAudioBuses()
    {
        EnsureBus(BusMusic);
        EnsureBus(BusSfx);
    }

    Window.ModeEnum WantedWindowMode()
    {
        return WindowMode switch
        {
            WindowBorderless => Window.ModeEnum.Fullscreen,
            WindowExclusive => Window.ModeEnum.ExclusiveFullscreen,
            _ => Window.ModeEnum.Windowed,
        };
    }

    static bool WindowModeMatches(Window win, Window.ModeEnum wanted)
    {
        if (wanted == Window.ModeEnum.Windowed)
        {
            return win.Mode != Window.ModeEnum.Fullscreen
                && win.Mode != Window.ModeEnum.ExclusiveFullscreen;
        }

        return win.Mode == wanted;
    }

    static void EnsureBus(string busName)
    {
        if (AudioServer.GetBusIndex(busName) >= 0)
        {
            return;
        }

        AudioServer.AddBus();
        int idx = AudioServer.BusCount - 1;
        AudioServer.SetBusName(idx, busName);
        AudioServer.SetBusSend(idx, "Master");
    }

    static void SetBusVolume(string busName, float linear)
    {
        int idx = AudioServer.GetBusIndex(busName);
        if (idx < 0)
        {
            return;
        }

        float clamped = Mathf.Clamp(linear, 0f, 1f);
        AudioServer.SetBusVolumeDb(idx, clamped <= 0.0001f ? -80f : Mathf.LinearToDb(clamped));
    }

    static string SanitizeWindowMode(string value)
    {
        return value switch
        {
            WindowBorderless or WindowExclusive or WindowWindowed => value,
            _ => WindowBorderless,
        };
    }

    static float SanitizeVolume(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 1f;
        }

        return Mathf.Clamp(value, 0f, 1f);
    }

    static int SanitizeMaxFps(int value)
    {
        return Mathf.Max(value, 0);
    }

    static Viewport.Msaa SanitizeMsaa(int value)
    {
        return value switch
        {
            (int)Viewport.Msaa.Msaa2X => Viewport.Msaa.Msaa2X,
            (int)Viewport.Msaa.Msaa4X => Viewport.Msaa.Msaa4X,
            (int)Viewport.Msaa.Msaa8X => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled,
        };
    }
}
