using System;
using System.Collections.Generic;

[Flags]
public enum DamageTag
{
    None = 0,
    Projectile = 1 << 0,
    Dot = 1 << 1,
    Explosion = 1 << 2,
    BasicAttack = 1 << 3,
    Skill = 1 << 4,
    Flame = 1 << 5,
    Poison = 1 << 6,
    Cold = 1 << 7,
    Buff = 1 << 8,
    Lightning = 1 << 9,
}

public enum DamageSourceKind
{
    None,
    Attack,
    Projectile,
    BuffTick,
    Effect,
}

public enum DamageResolveStage
{
    None,
    BeforeEvents,
    Outgoing,
    Critical,
    Defense,
    Incoming,
    Final,
    Applied,
}

public class DamageSourceInfo
{
    public DamageSourceKind Kind;
    public object SourceObject;
    public BuffInstance BuffInstance;
    public Buff Buff;

    public void BindSourceObject(object sourceObject)
    {
        SourceObject = sourceObject;
        BuffInstance = sourceObject as BuffInstance;

        if (BuffInstance != null)
        {
            Buff = BuffInstance.Data;
            return;
        }

        Buff = sourceObject as Buff;
    }
}

public class DamageContext
{
    public IUnit Attacker;
    public IUnit Target;

    public float RawPhysicalDamage;
    public float RawMagicalDamage;
    public float RawRealDamage;

    public float OutgoingDamageMultiplier = 1f;
    public float IncomingDamageMultiplier = 1f;
    public float FinalDamageMultiplier = 1f;

    public float ShieldDamageMultiplier = 1f;
    public float ShieldDamageApplied;
    public float HealthDamageApplied;
    public bool TargetHasShield => Target != null && Target.Shield > 0f;

    public float CritChance = 0f;
    public float CritMultiplier = 1.5f;

    public float FinalPhysicalDamage;
    public float FinalMagicalDamage;
    public float FinalRealDamage;

    public bool IsCrit;
    public bool IsRanged;

    public float RandomValue;
    public bool ShowText = true;

    public DamageTag DamageTags = DamageTag.None;
    public DamageResolveStage Stage = DamageResolveStage.None;
    public DamageSourceInfo Source;

    public List<DamageModifier> AppliedModifiers { get; private set; }

    public float DamageMultiplier
    {
        get => FinalDamageMultiplier;
        set => FinalDamageMultiplier = value;
    }

    public float rand1
    {
        get => RandomValue;
        set => RandomValue = value;
    }

    public bool showText
    {
        get => ShowText;
        set => ShowText = value;
    }

    public DamageContext(IUnit attacker, IUnit target, float randomValue = 0f)
    {
        Attacker = attacker;
        Target = target;
        RandomValue = randomValue;
    }

    public bool HasAnyTag(DamageTag tags)
    {
        if (tags == DamageTag.None)
        {
            return true;
        }

        return (DamageTags & tags) != 0;
    }

    public void AddTags(DamageTag tags)
    {
        DamageTags |= tags;
    }

    public void SetSource(DamageSourceKind kind, object sourceObject = null)
    {
        Source ??= new DamageSourceInfo();
        Source.Kind = kind;
        Source.BindSourceObject(sourceObject);
    }

    public void ClearAppliedModifiers()
    {
        AppliedModifiers?.Clear();
    }

    public void RecordAppliedModifier(DamageModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        AppliedModifiers ??= new List<DamageModifier>();
        AppliedModifiers.Add(modifier);
    }

    public float GetTotalFinalDamage()
    {
        return FinalPhysicalDamage + FinalMagicalDamage + FinalRealDamage;
    }
}

public static class DamageTagExt
{
    public static DamageTag ToDamageTag(this BuffTag buffTag)
    {
        DamageTag tags = DamageTag.None;
        if ((buffTag & BuffTag.Flame) != 0)
        {
            tags |= DamageTag.Flame;
        }

        if ((buffTag & BuffTag.Poison) != 0)
        {
            tags |= DamageTag.Poison;
        }

        if ((buffTag & BuffTag.Cold) != 0)
        {
            tags |= DamageTag.Cold;
        }

        return tags;
    }
}
