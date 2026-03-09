using System;
using Godot;
using Godot.Collections;
using static Godot.DisplayServer;

public partial class UserInfo : Singleton<UserInfo>
{
    const string filePath = "user://userinfo.tscn";
    public static UserInfo LoadUserInfo()
    {
        UserInfo res = null;
        if (ResourceLoader.Exists(filePath))
        {
            res = ResourceLoader.Load<PackedScene>(filePath).InstantiateOrNull<UserInfo>();
            if(res == null)
            {
                GD.PrintErr("加载用户信息失败，已重置为默认设置");
            }
            else
            {
                res.Init();
                return res;
            }
        }
        res = new UserInfo();
        res.Init();
        return res;
    }
    public static void Save()
    {
        if (Instance == null) return;
        var ps = new PackedScene();
        ps.Pack(Instance);
        ResourceSaver.Save(ps, filePath);
    }

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
    DisplayServer.WindowMode windowMode = DisplayServer.WindowMode.ExclusiveFullscreen;

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
    public EResolution Resolution
    {
        get
        {
            return eResolution;
        }
        set
        {
            eResolution = value;
            if (!inited) return;
            ProjectSettings.SetSetting("rendering/scaling_3d/scale", ResoToClarity(eResolution));
        }
    }
    EResolution eResolution = EResolution.R100;
    float ResoToClarity(EResolution reso)
    {
        float scale = 1;
        scale = reso switch
        {
            EResolution.R100 => 1f,
            EResolution.R75 => 0.75f,
            EResolution.R50 => 0.75f,
            _ => 1f
        };
        return scale;
    }

    [Export]
    public bool enableCamShake = true;
    [Export]
    public bool useRandName = false;

    [Export]
    string lastTimeUsedChr = null;
    public string LastTimeUsedChr
    {
        get
        {
            return string.IsNullOrEmpty(lastTimeUsedChr) ?
        "hero_watermelon" : lastTimeUsedChr;
        }
        set
        {
            if (!string.IsNullOrEmpty(value) || ObjectPoolManager.ExistPossibleObject(lastTimeUsedChr))
                lastTimeUsedChr = value;
            else
            {
                GD.PrintErr($"存储的最近使用角色名称不存在:{value}");
            }
        }
    }

    [Export]
    public string lastGameMap;
    [Export]
    public EGameDifficulty lastGameDifficulty = EGameDifficulty.Normal;

    bool inited = false;
    public void Init()
    {
        inited = true;
        if (AudioServer.BusCount == 1)
        {
            AudioServer.AddBus(1);
            AudioServer.AddBus(2);
            AudioServer.SetBusName(0,"master");
            AudioServer.SetBusName(1,"bg");
            AudioServer.SetBusName(2,"eff");
        }
        WindowMode = windowMode;
        Language = language;
        Resolution = eResolution;
        MasterVol = masterVol;
        BgVol = bgVol;
        EffVol = effVol;
    }
}
public enum Language
{
    en,
    zh_CN,
    es,
    ja,
    ko,
    de,
    zh_TW,
    ru
}
public enum EResolution
{
    R100,
    R75,
    R50,
}