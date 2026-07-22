using System;
using Godot;

[GlobalClass]
public partial class BuffInfo : Resource
{
    [Export]
    public float duration = 5f; // -1 代表永久
    [Export]
    public float tickInterval = 1f;
    [Export]
    public bool infiniteStacks = false;
    [Export]
    public int maxStacks = 1;
    [Export]
    public int priority = 0; // 影响 StatModifier 的优先级
    [Export]
    public bool isDebuff = false;
    [Export]
    public bool changeColor = false;
    [Export]
    public Color color = new Color(1, 1, 1, 1);
    [Export]
    public int visualPriority = 0;
    [Export]
    public BuffTag tag;
}

[Flags]
public enum BuffTag
{
    None = 0,
    Cold = 1 << 0,
    Poison = 1 << 1,
    Flame = 1 << 2,
    Stun = 1 << 3,
    Vulnerable = 1 << 4,
}

public static class BuffTagExt
{
    /// <summary>tags 是否包含 required 的全部位。</summary>
    public static bool HasAll(this BuffTag tags, BuffTag required)
    {
        return required == BuffTag.None || (tags & required) == required;
    }

    /// <summary>tags 是否与 query 有任意重叠位。</summary>
    public static bool HasAny(this BuffTag tags, BuffTag query)
    {
        return query != BuffTag.None && (tags & query) != 0;
    }
}
