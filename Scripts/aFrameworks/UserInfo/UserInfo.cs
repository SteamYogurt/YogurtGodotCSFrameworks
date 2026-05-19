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
            EResolution.R50 => 0.50f,
            _ => 1f
        };
        return scale;
    }
    [Export]
    public bool reverseY = false;

    [Export]
    public string MapName
    {
        get
        {
            if (string.IsNullOrEmpty(mapName))
            {
                return "Map01";
            }
            return mapName;
        }
        set
        {
            mapName = value;
        }
    }
    string mapName;
    [Export]
    public string KartName
    {
        get
        {
            if (string.IsNullOrEmpty(kartName))
            {
                return "Kart01";
            }
            return kartName;
        }
        set
        {
            kartName = value;
        }
    }
    string kartName;
    [Export]
    public string ChrName
    {
        get
        {
            if (string.IsNullOrEmpty(chrName))
            {
                return "Chr01";
            }
            return chrName;
        }
        set
        {
            chrName = value;
        }
    }
    string chrName;

    [Export]
    public Array<string> unlockedKarts = new Array<string>() { "Kart01" };
    [Export]
    public Array<string> unlockedChrs= new Array<string>() { "Chr01" };
    public bool IsKartUnlocked(string id)
    {
        return unlockedKarts.Contains(id);
    }

    public bool IsChrUnlocked(string id)
    {
        return unlockedChrs.Contains(id);
    }

    public void UnlockKart(string id)
    {
        if (!unlockedKarts.Contains(id))
            unlockedKarts.Add(id);
    }

    public void UnlockChr(string id)
    {
        if (!unlockedChrs.Contains(id))
            unlockedChrs.Add(id);
    }

    [Export]
    public int cash = 0;
    public event Action CashChanged;
    public bool CanAfford(int price)
    {
        return cash >= price;
    }

    public bool SpendCash(int price)
    {
        if (cash < price)
            return false;

        cash -= price;
        CashChanged?.Invoke();
        return true;
    }

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

    public void Reset()
    {
        Language = Language.en;
        WindowMode = DisplayServer.WindowMode.ExclusiveFullscreen;
        MasterVol = 0;
        BgVol = 0;
        EffVol = 0;
        Resolution = EResolution.R100;
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