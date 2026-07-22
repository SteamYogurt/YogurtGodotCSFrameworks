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

    public static void BuffInit(string path = "res://Data/Buff/")
    {
        LoadFrom(path);
    }

    public static void LoadFrom(string openPath)
    {
        allBuffs.Clear();
        allBuffsDict.Clear();
        LoadFromRecursive(openPath);
    }

    static void LoadFromRecursive(string openPath)
    {
        if (openPath.Length >= 2 && openPath[openPath.Length - 2] == '_')
        {
            return;
        }

        string[] psArr = ResourceLoader.ListDirectory(openPath);
        foreach (string ps in psArr)
        {
            if (ps[ps.Length - 1] != '/')
            {
                string extension = Path.GetExtension(ps);
                if (extension != ".res" && extension != ".tres")
                {
                    continue;
                }

                if (ps.Length >= 5 && ps[ps.Length - 5] == '_')
                {
                    continue;
                }

                var buff = GD.Load<Buff>(openPath + ps);
                if (buff == null)
                {
                    continue;
                }

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
            else
            {
                LoadFromRecursive(openPath + ps);
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

    [Export]
    public Godot.Collections.Array<BuffEffect> Effects = new();

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
        ForEachEffect(effect => effect.OnEnter(instance));
    }

    public virtual void OnTick(BuffInstance instance, float delta)
    {
        ForEachEffect(effect => effect.OnTick(instance, delta));
    }

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

    public virtual void OnStackChanged(BuffInstance instance)
    {
        // Central cleanup so multiple effects can safely re-apply without wiping each other.
        instance.CleanUpModifiers();
        instance.CleanUpDamageModifiers();
        ForEachEffect(effect => effect.OnStackChanged(instance));
    }

    public virtual void OnExit(BuffInstance instance)
    {
        ForEachEffect(effect => effect.OnExit(instance));
    }

    void ForEachEffect(System.Action<BuffEffect> action)
    {
        if (Effects == null || action == null)
        {
            return;
        }

        for (int i = 0; i < Effects.Count; i++)
        {
            BuffEffect effect = Effects[i];
            if (effect != null)
            {
                action(effect);
            }
        }
    }

    public bool TryGetEffect<T>(out T effect) where T : BuffEffect
    {
        effect = null;
        if (Effects == null)
        {
            return false;
        }

        for (int i = 0; i < Effects.Count; i++)
        {
            if (Effects[i] is T typed)
            {
                effect = typed;
                return true;
            }
        }

        return false;
    }
}
