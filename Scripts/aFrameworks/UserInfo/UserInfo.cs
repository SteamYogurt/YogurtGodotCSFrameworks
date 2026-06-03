using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
public partial class UserInfo : Singleton<UserInfo>
{
    const string filePath = "user://userinfo.tscn";

    public static UserInfo LoadUserInfo()
    {
        if (!ResourceLoader.Exists(filePath))
        {
            return CreateDefaultUserInfo();
        }

        try
        {
            var packedScene = ResourceLoader.Load<PackedScene>(filePath);
            if (packedScene == null)
            {
                GD.PrintErr("加载用户信息失败：存档资源为空，已重置为默认设置");
                return CreateDefaultUserInfo();
            }

            var res = packedScene.InstantiateOrNull<UserInfo>();
            if (res == null)
            {
                GD.PrintErr("加载用户信息失败：无法实例化 UserInfo，已重置为默认设置");
                return CreateDefaultUserInfo();
            }

            res.RepairInvalidData();
            res.Init();
            return res;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"加载用户信息失败：{ex}，已重置为默认设置");
            return CreateDefaultUserInfo();
        }
    }

    static UserInfo CreateDefaultUserInfo()
    {
        var res = new UserInfo();
        res.Init();
        return res;
    }

    void RepairInvalidData()
    {
        masterVol = NormalizeFiniteValue(masterVol, 0);
        bgVol = NormalizeFiniteValue(bgVol, 0);
        effVol = NormalizeFiniteValue(effVol, 0);
        maxFps = NormalizeMaxFps(maxFps);

        if (!Enum.IsDefined(typeof(DisplayServer.WindowMode), windowMode))
        {
            windowMode = DisplayServer.WindowMode.Windowed;
        }

        if (!Enum.IsDefined(typeof(Language), language))
        {
            language = Language.en;
        }

        if (!Enum.IsDefined(typeof(Viewport.Msaa), msaa3D))
        {
            msaa3D = Viewport.Msaa.Disabled;
        }

    }
    static float NormalizeFiniteValue(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    static int NormalizeMaxFps(int value)
    {
        return value switch
        {
            30 => 30,
            60 => 60,
            90 => 90,
            120 => 120,
            144 => 144,
            165 => 165,
            240 => 240,
            _ => 144
        };
    }

    public static void Save()
    {
        if (Instance == null) return;
        var ps = new PackedScene();
        ps.Pack(Instance);
        ResourceSaver.Save(ps, filePath);
    }
    [Export]
    public bool hasSelectedLanguage = false;

    [Export]
    public float MasterVol
    {
        get
        {
            return masterVol;
        }
        set
        {
            masterVol = value;
            if (!inited) return;
            AudioServer.SetBusVolumeDb(0, masterVol);
        }
    }
    float masterVol = 0;

    [Export]
    public float BgVol
    {
        get
        {
            return bgVol;
        }
        set
        {
            bgVol = value;
            if (!inited) return;
            AudioServer.SetBusVolumeDb(1, bgVol);
        }
    }
    float bgVol;

    [Export]
    public float EffVol
    {
        get
        {
            return effVol;
        }
        set
        {
            effVol = value;
            if (!inited) return;
            AudioServer.SetBusVolumeDb(2, effVol);
        }
    }
    float effVol;

    [Export]
    public DisplayServer.WindowMode WindowMode
    {
        get
        {
            return windowMode;
        }
        set
        {
            windowMode = value;
            if (!inited) return;
            DisplayServer.WindowSetMode(windowMode, 0);
        }
    }
    DisplayServer.WindowMode windowMode = DisplayServer.WindowMode.Windowed;

    [Export]
    public Language Language
    {
        get
        {
            return language;
        }
        set
        {
            language = value;
            if (!inited) return;
            TranslationServer.SetLocale($"{language}");
        }
    }
    Language language = Language.en;

    [Export]
    public bool VSyncEnabled
    {
        get
        {
            return vSyncEnabled;
        }
        set
        {
            vSyncEnabled = value;
            if (!inited) return;
            DisplayServer.WindowSetVsyncMode(
                vSyncEnabled
                    ? DisplayServer.VSyncMode.Enabled
                    : DisplayServer.VSyncMode.Disabled
            );
        }
    }
    bool vSyncEnabled = false;

    [Export]
    public Viewport.Msaa Msaa3D
    {
        get
        {
            return msaa3D;
        }
        set
        {
            msaa3D = value;
            if (!inited) return;
            ApplyViewportSettings();
        }
    }
    Viewport.Msaa msaa3D = Viewport.Msaa.Disabled;

    [Export]
    public bool UseDebanding
    {
        get
        {
            return useDebanding;
        }
        set
        {
            useDebanding = value;
            if (!inited) return;
            ApplyViewportSettings();
        }
    }
    bool useDebanding = false;

    [Export]
    public int MaxFps
    {
        get
        {
            return maxFps;
        }
        set
        {
            maxFps = NormalizeMaxFps(value);
            if (!inited) return;
            Engine.MaxFps = maxFps;
        }
    }
    int maxFps = 144;

    bool inited = false;

    public void Init()
    {
        inited = true;

        if (AudioServer.BusCount == 1)
        {
            AudioServer.AddBus(1);
            AudioServer.AddBus(2);
            AudioServer.SetBusName(0, "master");
            AudioServer.SetBusName(1, "bgm");
            AudioServer.SetBusName(2, "eff");
        }

        WindowMode = windowMode;
        Language = language;
        VSyncEnabled = vSyncEnabled;
        Msaa3D = msaa3D;
        UseDebanding = useDebanding;
        MaxFps = maxFps;
        MasterVol = masterVol;
        BgVol = bgVol;
        EffVol = effVol;
    }
    public void TryShowLanguageSelect(Control root)
    {
        if (hasSelectedLanguage || root == null) return;
        if (root.GetNodeOrNull<Control>("FirstLanguageSelect") != null) return;

        var mask = new ColorRect();
        mask.Name = "FirstLanguageSelect";
        mask.Color = new Color(0, 0, 0, 0.75f);
        mask.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        mask.MouseFilter = Control.MouseFilterEnum.Stop;

        var panel = new PanelContainer();
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -170;
        panel.OffsetTop = -180;
        panel.OffsetRight = 170;
        panel.OffsetBottom = 180;
        mask.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vb);

        var title = new Label();
        title.Text = "语言设置";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(title);

        Button firstBtn = null;
        foreach (Language item in Enum.GetValues(typeof(Language)))
        {
            var btn = new Button();
            btn.Text = GetLanguageDisplayName(item);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.Pressed += () =>
            {
                Language = item;
                hasSelectedLanguage = true;
                Save();
                mask.QueueFree();
            };
            vb.AddChild(btn);
            firstBtn ??= btn;
        }

        root.AddChild(mask);
        firstBtn?.CallDeferred(Button.MethodName.GrabFocus);
    }

    string GetLanguageDisplayName(Language item)
    {
        return item switch
        {
            Language.en => "English",
            Language.zh_CN => "简体中文",
            Language.es => "Español",
            Language.ja => "日本語",
            Language.ko => "한국어",
            Language.de => "Deutsch",
            Language.zh_TW => "繁體中文",
            Language.ru => "Русский",
            Language.fr => "Français",
            Language.pt_BR => "Português (Brasil)",
            Language.it => "Italiano",
            _ => $"{item}"
        };
    }
    void ApplyViewportSettings()
    {
        var viewport = Main.Instance?.GetViewport();
        if (viewport == null)
        {
            return;
        }

        viewport.Msaa3D = msaa3D;
        viewport.UseDebanding = useDebanding;
    }


    public void Reset()
    {
        Language = Language.en;
        WindowMode = DisplayServer.WindowMode.Fullscreen;
        VSyncEnabled = false;
        Msaa3D = Viewport.Msaa.Disabled;
        UseDebanding = false;
        MaxFps = 144;
        MasterVol = 0;
        BgVol = 0;
        EffVol = 0;
    }
}