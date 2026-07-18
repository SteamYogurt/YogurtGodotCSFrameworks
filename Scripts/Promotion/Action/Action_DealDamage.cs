using System.Collections.Generic;
using Godot;

public enum ActionPositionSource
{
    ContextPosition,
    AttackerPosition,
    TargetPosition,
}

[GlobalClass]
public partial class Action_DealDamage : PromotionAction
{
    [Export] public ConditionSubjectKey targetKey = ConditionSubjectKey.Target;
    [Export] public ConditionSubjectKey sourceKey = ConditionSubjectKey.Attacker;
    [Export] public ActionPositionSource impactPositionSource = ActionPositionSource.TargetPosition;
    [Export] public Vector3 impactPositionOffset = Vector3.Zero;
    [Export(PropertyHint.Range, "0,1000,0.1")] public float damageRadius;
    [Export] public bool showDamageText = true;

    [ExportGroup("Fixed Damage")]
    [Export] public float physicalDamage;
    [Export] public float magicalDamage;
    [Export] public float realDamage;

    [ExportGroup("Source Damage Scale")]
    [Export] public float sourcePhysicalDamageScale = 1f;
    [Export] public float sourceMagicalDamageScale = 1f;
    [Export] public float sourceRealDamageScale = 1f;

    [ExportGroup("Visual")]
    [Export] public string tempEffectName;

    protected override void Execute(ConditionContext context)
    {
        if (context == null)
        {
            return;
        }

        Vector3 impactPosition = ResolvePosition(context) + impactPositionOffset;
        IUnit sourceUnit = context.GetObject(sourceKey) as IUnit;
        IUnit targetUnit = context.GetObject(targetKey) as IUnit;

        if (damageRadius > 0f)
        {
            ApplyAreaDamage(context, sourceUnit, targetUnit, impactPosition, damageRadius);
        }
        else
        {
            DealDamageToUnit(sourceUnit, targetUnit);
        }

        SpawnTempEffect(context, impactPosition);
    }

    void ApplyAreaDamage(
        ConditionContext context,
        IUnit sourceUnit,
        IUnit primaryTarget,
        Vector3 impactPosition,
        float radius)
    {
        HashSet<IUnit> damagedUnits = new();
        TryDealAreaTarget(sourceUnit, primaryTarget, impactPosition, radius, damagedUnits);

        MatchContext match = context.Match ?? CombatRuntime.Current;
        match?.ForEachActiveUnit(
            unit => TryDealAreaTarget(sourceUnit, unit, impactPosition, radius, damagedUnits));
    }

    void TryDealAreaTarget(
        IUnit sourceUnit,
        IUnit unit,
        Vector3 impactPosition,
        float radius,
        HashSet<IUnit> damagedUnits)
    {
        if (!CanHitUnit(unit, sourceUnit))
        {
            return;
        }

        if (GetUnitPosition(unit, impactPosition).DistanceTo(impactPosition) > radius)
        {
            return;
        }

        if (!damagedUnits.Add(unit))
        {
            return;
        }

        DealDamageToUnit(sourceUnit, unit);
    }

    void DealDamageToUnit(IUnit sourceUnit, IUnit targetUnit)
    {
        if (!CanHitUnit(targetUnit, sourceUnit))
        {
            return;
        }

        DamageContext damageContext = BuildDamageContext(sourceUnit, targetUnit);
        if (damageContext == null)
        {
            return;
        }

        IUnitExt.ExecuteDamage(damageContext, false);
    }

    DamageContext BuildDamageContext(IUnit sourceUnit, IUnit targetUnit)
    {
        float totalPhysicalDamage = Mathf.Max(0f, physicalDamage);
        float totalMagicalDamage = Mathf.Max(0f, magicalDamage);
        float totalRealDamage = Mathf.Max(0f, realDamage);

        if (sourceUnit != null && sourceUnit.IsAlive)
        {
            totalPhysicalDamage += Mathf.Max(
                0f,
                sourceUnit.PhysicalAttackDamage * sourcePhysicalDamageScale);
            totalMagicalDamage += Mathf.Max(
                0f,
                sourceUnit.MagicalAttackDamage * sourceMagicalDamageScale);
            totalRealDamage += Mathf.Max(
                0f,
                sourceUnit.RealDamage * sourceRealDamageScale);
        }

        if (totalPhysicalDamage <= 0f
            && totalMagicalDamage <= 0f
            && totalRealDamage <= 0f)
        {
            return null;
        }

        DamageContext damageContext = IUnitExt.CreateDamageContext(
            sourceUnit,
            targetUnit,
            totalPhysicalDamage,
            totalMagicalDamage,
            totalRealDamage,
            GD.Randf());

        damageContext.ShowText = showDamageText;
        damageContext.SetSource(DamageSourceKind.Effect, this);

        if (damageRadius > 0f)
        {
            damageContext.AddTags(DamageTag.Explosion);
        }

        return damageContext;
    }

    void SpawnTempEffect(ConditionContext context, Vector3 impactPosition)
    {
        if (string.IsNullOrEmpty(tempEffectName))
        {
            return;
        }

        MatchContext match = context?.Match ?? CombatRuntime.Current;
        match?.SpawnEffectAtPosition?.Invoke(tempEffectName, impactPosition);
    }

    Vector3 ResolvePosition(ConditionContext context)
    {
        Vector3 fallback = context.Get<Vector3>(ConditionSubjectKey.Position);

        return impactPositionSource switch
        {
            ActionPositionSource.AttackerPosition =>
                GetUnitPosition(context.GetObject(ConditionSubjectKey.Attacker) as IUnit, fallback),
            ActionPositionSource.TargetPosition =>
                GetUnitPosition(context.GetObject(ConditionSubjectKey.Target) as IUnit, fallback),
            _ => fallback,
        };
    }

    static Vector3 GetUnitPosition(IUnit unit, Vector3 fallback)
    {
        if (unit is not Node3D unitNode)
        {
            return fallback;
        }

        if (!GodotObject.IsInstanceValid(unitNode) || !unitNode.IsInsideTree())
        {
            return fallback;
        }

        return unitNode.GlobalPosition;
    }

    static bool CanHitUnit(IUnit unit, IUnit sourceUnit)
    {
        if (unit == null || !unit.IsAlive || unit == sourceUnit)
        {
            return false;
        }

        if (unit is not Node3D unitNode)
        {
            return false;
        }

        return GodotObject.IsInstanceValid(unitNode) && unitNode.IsInsideTree();
    }
}
