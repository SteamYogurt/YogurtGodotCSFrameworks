using Godot;

public enum UnitStatType
{
    MaxHealth,
    MaxShield,
    PhysicalAttackDamage,
    MagicAttackDamage,
    RealDamage,
    AttackRange,
    AttackSpeed,
    PhysicalDefense,
    MagicalDefense,
    MoveSpeed,
    CritRate
}

public static class UnitStatTypeExt
{
    public static Texture2D GetTexture(this UnitStatType statType)
    {
        var path = $"res://Assets/Art/Icon/Stat/{statType}.png";
        if (!ResourceLoader.Exists(path))
        {
            return GD.Load<Texture2D>("res://icon.svg");
        }
        return GD.Load<Texture2D>(path);
    }
}

public static class IUnitExt
{
    public static void InitUnit(this IUnit unit)
    {
        if (unit.UnitInfo == null)
        {
            GD.PrintErr("IUnit InitUnit Error: UnitInfo is null");
            return;
        }

        unit.BuffController?.Reset();

        unit.UnitStatCollection = new StatCollection();
        unit.BuffController = new BuffController(unit);

        unit.MaxHealth = unit.UnitInfo.maxHealth;
        unit.Health = unit.MaxHealth;
        unit.MaxShield = unit.UnitInfo.maxShield;
        unit.Shield = unit.MaxShield;
        unit.PhysicalAttackDamage = unit.UnitInfo.physicalAttackDamage;
        unit.MagicalAttackDamage = unit.UnitInfo.magicalAttackDamage;
        unit.RealDamage = unit.UnitInfo.realDamage;
        unit.AttackRange = unit.UnitInfo.attackRange;
        unit.AttackSpeed = unit.UnitInfo.attackSpeed;
        unit.PhysicalDefense = unit.UnitInfo.physicalDefense;
        unit.MagicalDefense = unit.UnitInfo.magicalDefense;
        unit.MoveSpeed = unit.UnitInfo.moveSpeed;
        unit.CritRate = unit.UnitInfo.critRate;

        unit.IsAlive = true;
    }

    public static DamageContext CreateDamageContext(
        IUnit attacker,
        IUnit target,
        float physDamage,
        float magicalDamage,
        float realDamage,
        float rand1 = 0f)
    {
        var ctx = new DamageContext(attacker, target, rand1)
        {
            RawPhysicalDamage = Mathf.Max(0f, physDamage),
            RawMagicalDamage = Mathf.Max(0f, magicalDamage),
            RawRealDamage = Mathf.Max(0f, realDamage),
            CritChance = attacker != null ? Mathf.Clamp(attacker.CritRate, 0f, 1f) : 0f,
            CritMultiplier = 1.5f,
        };

        if (attacker != null)
        {
            ctx.SetSource(DamageSourceKind.Attack, attacker);
        }

        return ctx;
    }

    public static void CalculateDamage(IUnit attacker, IUnit target, float rand1 = 0)
    {
        if (attacker == null)
        {
            return;
        }

        DamageContext ctx = CreateDamageContext(
            attacker,
            target,
            attacker.PhysicalAttackDamage,
            attacker.MagicalAttackDamage,
            attacker.RealDamage,
            rand1);
        ExecuteDamage(ctx);
    }

    public static void CalculateDamage(
        IUnit attacker,
        IUnit target,
        float physDamage,
        float magicalDamage,
        float realDamage,
        float rand1 = 0)
    {
        DamageContext ctx = CreateDamageContext(
            attacker,
            target,
            physDamage,
            magicalDamage,
            realDamage,
            rand1);
        ExecuteDamage(ctx);
    }

    public static void CalculateDamageNoAttacker(
        IUnit target,
        float physDamage,
        float magicalDamage,
        float realDamage,
        float rand1 = 0)
    {
        DamageContext ctx = CreateDamageContext(
            null,
            target,
            physDamage,
            magicalDamage,
            realDamage,
            rand1);
        ExecuteDamage(ctx);
    }

    public static void ExecuteDamage(DamageContext ctx, bool invokeCallbacks = true)
    {
        DamageResolver.Execute(ctx, invokeCallbacks);
    }

    public static void ApplyDamageImpact(DamageContext ctx)
    {
        float damageLeft = ctx.GetTotalFinalDamage();
        ctx.ShieldDamageApplied = 0f;
        ctx.HealthDamageApplied = 0f;
        if (damageLeft <= 0f)
        {
            return;
        }

        if (ctx.Target.Shield > 0f)
        {
            float shieldDamageMultiplier = Mathf.Max(0f, ctx.ShieldDamageMultiplier);
            float effectiveShieldDamage = damageLeft * shieldDamageMultiplier;
            if (effectiveShieldDamage <= 0f)
            {
                return;
            }

            float shieldAbsorb = Mathf.Min(ctx.Target.Shield, effectiveShieldDamage);
            ctx.Target.Shield -= shieldAbsorb;
            ctx.ShieldDamageApplied = shieldAbsorb;
            damageLeft -= shieldAbsorb / shieldDamageMultiplier;
        }

        if (damageLeft > 0f)
        {
            ctx.Target.Health -= damageLeft;
            ctx.HealthDamageApplied = damageLeft;
        }
    }

    public static void ApplyStatModifier(this IUnit unit, UnitStatType statType, StatModifier mod)
    {
        unit.UnitStatCollection ??= new StatCollection();
        var iniVal = unit.UnitStatCollection.GetValue(statType, 0);
        unit.UnitStatCollection.AddModifier(statType, mod);
        var afVal = unit.UnitStatCollection.GetValue(statType, 0);
        unit.NotifyStatChanged(statType, afVal - iniVal);
    }

    public static void RemoveStatModifier(this IUnit unit, UnitStatType statType, object source)
    {
        if (unit.UnitStatCollection != null)
        {
            var iniVal = unit.UnitStatCollection.GetValue(statType, 0);
            unit.UnitStatCollection.RemoveFromSource(statType, source);
            var afVal = unit.UnitStatCollection.GetValue(statType, 0);
            unit.NotifyStatChanged(statType, afVal - iniVal);
        }
    }

    public static void SetOrInit(this IUnit unit, UnitStatType statType, float val)
    {
        unit.UnitStatCollection.SetBase(statType, val);
    }

    public static float GetUnitStat(this IUnit unit, UnitStatType statType, float baseVal)
    {
        if (unit.UnitStatCollection == null)
        {
            return baseVal;
        }
        return unit.UnitStatCollection.GetValue(statType, baseVal);
    }
}
