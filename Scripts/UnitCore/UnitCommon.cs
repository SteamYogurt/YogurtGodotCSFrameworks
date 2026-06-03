using System.Collections.Generic;
using Godot;
using static Godot.WebSocketPeer;
using static TempLabel;

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
        if(!ResourceLoader.Exists(path))
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

    public static void StdExecuteDamage(DamageContext ctx)
    {
        ExecuteDamage(ctx);
    }

    static void ResolveCritical(DamageContext ctx)
    {
        DamageResolver.ResolveCritical(ctx);
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

    static ObjectPool textPool;
    public static void SpawnDamageText(DamageContext ctx)
    {
        if (ctx.Target is not Node3D targetNode) return;

        if (ctx.FinalPhysicalDamage <= 0 && ctx.FinalMagicalDamage <= 0 && ctx.FinalRealDamage <= 0) return;

        if (textPool == null)
        {
            textPool = ObjectPoolManager.GetObjectPool("TempLabel");
        }

        Vector2 basePosition = Game.instance.camera.UnprojectPosition(targetNode.Position);
        FloatType floatType = ctx.IsCrit ? FloatType.Static : FloatType.RandomUp;
        DecorationType deco = ctx.IsCrit ? DecorationType.Critical : DecorationType.None;

        if (ctx.FinalPhysicalDamage > 0)
        {
            SpawnSingleDamageText(
                Mathf.RoundToInt(ctx.FinalPhysicalDamage).ToString(),
                DamageColorType.Physical,
                deco,
                floatType,
                basePosition + new Vector2(-16, 0)
            );
        }

        if (ctx.FinalMagicalDamage > 0)
        {
            SpawnSingleDamageText(
                Mathf.RoundToInt(ctx.FinalMagicalDamage).ToString(),
                DamageColorType.Magical,
                deco,
                floatType,
                basePosition + new Vector2(16, -8)
            );
        }
        if (ctx.FinalRealDamage > 0)
        {
            SpawnSingleDamageText(
                Mathf.RoundToInt(ctx.FinalRealDamage).ToString(),
                DamageColorType.Real,
                deco,
                floatType,
                basePosition + new Vector2(0, -16)
            );
        }
    }

    private static void SpawnSingleDamageText(
        string damageText,
        DamageColorType colorType,
        DecorationType deco,
        FloatType floatType,
        Vector2 position)
    {
        var label = textPool.GetObject<TempLabel>();
        label.Position = position;
        Game.instance.canvasLayer.AddChild(label);
        label.Setup(damageText, colorType, deco, floatType);
    }
    public static void ApplyStatModifier(this IUnit unit, UnitStatType statType, StatModifier mod)
    {
        // 确保字典已初始化
        unit.UnitStatCollection ??= new StatCollection();
        var iniVal = unit.UnitStatCollection.GetValue(statType, 0);
        unit.UnitStatCollection.AddModifier(statType, mod);
        var afVal = unit.UnitStatCollection.GetValue(statType, 0);
        unit.NotifyStatChanged(statType, afVal - iniVal);
    }

    /// <summary>
    /// 移除指定来源的所有修饰符
    /// </summary>
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
        var dict = unit.UnitStatCollection;
        if (dict == null) return baseVal;
        return unit.UnitStatCollection.GetValue(statType, baseVal);
    }
}