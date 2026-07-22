using Godot;

[GlobalClass]
public partial class DotBuffEffect : BuffEffect
{
    [Export] public bool IsHealing = false;
    [Export] public bool StackDamage = true;

    [Export] public DoTValueMode ValueMode = DoTValueMode.FixedValue;
    [Export] public float ValuePerTick = 10f;
    [Export] public UnitStatType CasterStatType = UnitStatType.MagicAttackDamage;
    [Export] public float CasterStatScale = 1f;

    [Export] public DoTDamageType DamageType = DoTDamageType.Real;

    public override void OnTick(BuffInstance instance, float delta)
    {
        float tickInterval = instance.GetResolvedTickInterval();
        instance.TickTimer += delta;

        while (instance.TickTimer >= tickInterval)
        {
            instance.TickTimer -= tickInterval;
            ApplyEffect(instance);
        }
    }

    void ApplyEffect(BuffInstance instance)
    {
        float finalValue = ResolveValuePerTick(instance);
        if (StackDamage)
        {
            finalValue *= instance.Stacks;
        }

        finalValue = instance.ResolveTickValue(finalValue, IsHealing);
        if (finalValue <= 0f)
        {
            return;
        }

        if (IsHealing)
        {
            instance.Owner.Health += finalValue;
            if (instance.Owner.Health > instance.Owner.MaxHealth)
            {
                instance.Owner.Health = instance.Owner.MaxHealth;
            }

            return;
        }

        IUnit attacker = instance.ResolveCasterUnit();
        DamageContext damageContext = DamageType switch
        {
            DoTDamageType.Physical => IUnitExt.CreateDamageContext(
                attacker,
                instance.Owner,
                finalValue,
                0f,
                0f),
            DoTDamageType.Magical => IUnitExt.CreateDamageContext(
                attacker,
                instance.Owner,
                0f,
                finalValue,
                0f),
            _ => IUnitExt.CreateDamageContext(
                attacker,
                instance.Owner,
                0f,
                0f,
                finalValue),
        };

        damageContext.SetSource(DamageSourceKind.BuffTick, instance);
        damageContext.AddTags(GetDamageTags(instance));

        IUnitExt.ExecuteDamage(damageContext);
    }

    DamageTag GetDamageTags(BuffInstance instance)
    {
        DamageTag tags = DamageTag.Dot | DamageTag.Buff;
        if (instance?.Data?.buffInfo != null)
        {
            tags |= instance.Data.buffInfo.tag.ToDamageTag();
        }

        return tags;
    }

    float ResolveValuePerTick(BuffInstance instance)
    {
        if (ValueMode == DoTValueMode.FixedValue)
        {
            return Mathf.Max(0f, ValuePerTick);
        }

        IUnit caster = instance.ResolveCasterUnit();
        if (caster == null)
        {
            return 0f;
        }

        float baseValue = CasterStatType switch
        {
            UnitStatType.PhysicalAttackDamage => caster.PhysicalAttackDamage,
            UnitStatType.MagicAttackDamage => caster.MagicalAttackDamage,
            UnitStatType.RealDamage => caster.RealDamage,
            _ => 0f,
        };

        return Mathf.Max(0f, baseValue * CasterStatScale);
    }
}

public enum DoTValueMode
{
    FixedValue,
    CasterStatScale,
}

public enum DoTDamageType
{
    Physical,
    Magical,
    Real,
}
