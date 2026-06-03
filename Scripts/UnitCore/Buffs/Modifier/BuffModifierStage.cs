using System;

[Flags]
public enum BuffModifierStage
{
    None = 0,
    Apply = 1 << 0,
    EffectValue = 1 << 1,
    TickValue = 1 << 2,
}

public enum BuffModifierDebuffFilter
{
    Any,
    BuffOnly,
    DebuffOnly,
}

public enum BuffModifierTickTypeFilter
{
    Any,
    DamageOnly,
    HealingOnly,
}

public class BuffModifierContext
{
    public BuffModifierStage Stage;
    public BuffInstance BuffInstance;
    public Buff Buff;
    public IUnit Owner;
    public IUnit Caster;

    public float Duration;
    public float TickInterval;
    public int MaxStacks;

    public float EffectValue;
    public float TickValue;

    public bool IsDebuff;
    public bool IsHealingTick;

    public BuffTag BuffTag => Buff?.buffInfo != null
        ? Buff.buffInfo.tag
        : BuffTag.None;

    public BuffModifierContext(BuffInstance buffInstance)
    {
        BuffInstance = buffInstance;
        Buff = buffInstance?.Data;
        Owner = buffInstance?.Owner;
        Caster = buffInstance?.ResolveCasterUnit();
        IsDebuff = Buff?.buffInfo != null && Buff.buffInfo.isDebuff;
    }
}