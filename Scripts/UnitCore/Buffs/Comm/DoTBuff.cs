using Godot;

[GlobalClass]
public partial class DoTBuff : Buff
{
    [Export] public bool IsHealing = false;
    [Export] public bool StackDamage = true;

    [Export] public DoTValueMode ValueMode = DoTValueMode.FixedValue;
    [Export] public float ValuePerTick = 10f;
    [Export] public UnitStatType CasterStatType = UnitStatType.MagicAttackDamage;
    [Export] public float CasterStatScale = 1f;

    [Export] public DoTDamageType DamageType = DoTDamageType.Real;

    public override void OnEnter(BuffInstance instance)
    {
    }

    public override void OnTick(BuffInstance instance, float delta)
    {
        float tickInterval = GetTickInterval(instance);
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
        damageContext.AddTags(GetDamageTags());

        IUnitExt.ExecuteDamage(damageContext);
    }

    DamageTag GetDamageTags()
    {
        DamageTag tags = DamageTag.Dot | DamageTag.Buff;
        if (buffInfo != null)
        {
            tags |= buffInfo.tag.ToDamageTag();
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

    float GetTickInterval(BuffInstance instance)
    {
        if (instance == null)
        {
            return 1f;
        }

        return instance.GetResolvedTickInterval();
    }

    public override string GetBuffDes()
    {
        var lines = new System.Collections.Generic.List<string>();
        string stackStr = GetStackStr();

        if (IsHealing)
        {
            if (ValueMode == DoTValueMode.FixedValue)
            {
                string valueStr = (ValuePerTick / Mathf.Max(0.0001f, buffInfo?.tickInterval ?? 1f)).ToString("0.##");
                lines.Add(string.Format("{0}{1}{2}",
                    Tr("每秒治疗"),
                    valueStr,
                    stackStr));
            }
            else
            {
                string scaleStr = FormatPercent(CasterStatScale / Mathf.Max(0.0001f, buffInfo?.tickInterval ?? 1f));
                lines.Add(string.Format("{0}{1}{2}{3}",
                    Tr("每秒治疗施加者"),
                    scaleStr,
                    Tr(CasterStatType.ToString()),
                    stackStr));
            }

            return string.Join("\n", lines);
        }

        if (ValueMode == DoTValueMode.FixedValue)
        {
            string valueStr = (ValuePerTick / Mathf.Max(0.0001f, buffInfo?.tickInterval ?? 1f)).ToString("0.##");
            lines.Add(string.Format("{0}{1}{2}{3}",
                Tr("每秒造成"),
                valueStr,
                GetDamageTypeName(),
                Tr("伤害") + stackStr));
        }
        else
        {
            string scaleStr = FormatPercent(CasterStatScale / Mathf.Max(0.0001f, buffInfo?.tickInterval ?? 1f));
            lines.Add(string.Format("{0}{1}{2}{3}{4}{5}",
                Tr("每秒造成施加者"),
                scaleStr,
                Tr(CasterStatType.ToString()),
                Tr("的"),
                GetDamageTypeName(),
                Tr("伤害") + stackStr));
        }

        return string.Join("\n", lines);
    }

    string GetDamageTypeName()
    {
        return DamageType switch
        {
            DoTDamageType.Physical => Tr("物理"),
            DoTDamageType.Magical => Tr("魔法"),
            _ => Tr("真实"),
        };
    }

    string FormatPercent(float value)
    {
        float percent = value * 100f;
        if (Mathf.Abs(percent) < 10f)
        {
            return percent.ToString("0.#") + "%";
        }

        return percent.ToString("0.##") + "%";
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