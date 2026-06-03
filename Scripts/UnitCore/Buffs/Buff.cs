using System;
using System.Collections.Generic;
using System.IO;
using Godot;

[GlobalClass]
public partial class Buff : Resource
{
    public static List<Buff> allBuffs = new List<Buff>();
    public static System.Collections.Generic.Dictionary<StringName, Buff> allBuffsDict
        = new System.Collections.Generic.Dictionary<StringName, Buff>();
    public static uint numID = 0;

    public static void BuffInit()
    {
        LoadFrom("res://Data/Buff/");
    }

    static void LoadFrom(string openPath)
    {
        if (openPath[openPath.Length - 2] == '_') return;
        string[] psArr = ResourceLoader.ListDirectory(openPath);
        foreach (string ps in psArr)
        {
            if (ps[ps.Length - 1] != '/')
            {
                if (Path.GetExtension(ps) == ".res")
                {
                    if (ps[ps.Length - 5] == '_') continue;
                    var buff = GD.Load<Buff>(openPath + ps);
                    buff.buffId = Path.GetFileNameWithoutExtension(ps);
                    if (buff.buffInfo == null)
                    {
                        GD.PrintErr("---------------------");
                        GD.PrintErr(buff.buffId);
                        GD.PrintErr("buff.buffInfo == null");
                        GD.PrintErr("---------------------");
                    }
                    else
                    {
                        allBuffs.Add(buff);
                        allBuffsDict.Add(buff.buffId, buff);
                        GD.Print("加载了buff资源: " + buff.buffId);
                    }
                }
            }
            else
            {
                LoadFrom(openPath + ps);
            }
        }
    }

    public Buff GetCopy()
    {
        var copy = this.Duplicate() as Buff;
        copy.buffId = this.BuffID;
        return copy;
    }

    [Export]
    public BuffInfo buffInfo;

    public static ulong numBId;

    public StringName BuffID
    {
        get
        {
            if (buffId == null)
            {
                buffId = (++numBId).ToString();
            }
            return buffId;
        }
    }

    StringName buffId;

    public virtual void OnEnter(BuffInstance instance)
    {
    }

    public virtual void OnTick(BuffInstance instance, float delta) { }

    public virtual void OnRefresh(BuffInstance instance, int stacks = 1)
    {
        instance.RefreshResolvedBuffValues();
        instance.DurationTimer = instance.GetResolvedDuration();

        int maxStacks = instance.GetResolvedMaxStacks();
        if (buffInfo.infiniteStacks || instance.Stacks < maxStacks)
        {
            instance.Stacks += stacks;
            if (!buffInfo.infiniteStacks && instance.Stacks > maxStacks)
            {
                instance.Stacks = maxStacks;
            }

            OnStackChanged(instance);
        }
    }

    public virtual void OnStackChanged(BuffInstance instance) { }

    public virtual void OnExit(BuffInstance instance)
    {
    }

    public string GetBuffName()
    {
        if (buffInfo.overrideBuffName) return Tr(buffInfo.buffName);
        return null;
    }

    public virtual string GetBuffDes()
    {
        if (buffInfo.overrideBuffDes) return Tr(buffInfo.buffDes);
        return null;
    }

    public string GetStackStr()
    {
        List<string> parts = new List<string>();
        if (!buffInfo.infiniteStacks)
        {
            parts.Add(string.Format(Tr("最大{0}层"), buffInfo.maxStacks));
        }
        if (buffInfo.duration > 0)
        {
            parts.Add(string.Format(Tr("持续{0}s"), buffInfo.duration));
        }
        return parts.Count > 0 ? " " + string.Join(",", parts) : string.Empty;
    }
}