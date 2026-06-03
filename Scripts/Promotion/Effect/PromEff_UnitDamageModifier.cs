using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromEff_UnitDamageModifier : PromotionEffect
{
    [Export] public ObjectFilter unitFilter;
    [Export] public Array<DamageModifier> damageModifiers = new();

    public override PromotionEffectHandle Activate(PromotionEffectContext context)
    {
        PromotionEffectHandle handle = new PromotionEffectHandle();

        ApplyToAllMatchingUnits();
        handle.AddSubscription(PromotionEventBus.Subscribe(
            PromotionEventType.UnitSpawned,
            OnUnitSpawned));
        handle.AddCleanup(RemoveFromAllMatchingUnits);

        return handle;
    }

    void OnUnitSpawned(ConditionContext ctx)
    {
        if (ctx?.GetObject(ConditionSubjectKey.Subject) is not IUnit unit)
        {
            return;
        }

        ApplyToUnit(unit);
    }

    void ApplyToAllMatchingUnits()
    {
        PromotionUnitEffectHelper.ForEachMatchingUnit(unitFilter, ApplyToUnit);
    }

    void RemoveFromAllMatchingUnits()
    {
        PromotionUnitEffectHelper.ForEachMatchingUnit(unitFilter, RemoveFromUnit);
    }

    void ApplyToUnit(IUnit unit)
    {
        if (!PromotionUnitEffectHelper.IsUnitValid(unit))
        {
            return;
        }

        DamageModifierController controller = unit.GetDamageModifierController();
        controller.RemoveAllFromSource(this);

        if (damageModifiers == null)
        {
            return;
        }

        foreach (DamageModifier damageModifier in damageModifiers)
        {
            if (damageModifier == null)
            {
                continue;
            }

            DamageModifier runtimeModifier = damageModifier.Duplicate() as DamageModifier ?? damageModifier;
            runtimeModifier.RuntimeSource = this;
            controller.AddModifier(runtimeModifier);
        }
    }

    void RemoveFromUnit(IUnit unit)
    {
        if (!PromotionUnitEffectHelper.IsUnitValid(unit))
        {
            return;
        }

        unit.GetDamageModifierController().RemoveAllFromSource(this);
    }

    public override string GetDescription()
    {
        string unitDescription = GetUnitDescription();
        string modifierDescription = DamageModifier.GetDescription(damageModifiers);

        if (string.IsNullOrEmpty(modifierDescription))
        {
            return string.Format(
                Tr("{0}获得伤害修正"),
                unitDescription);
        }

        return string.Format(
            Tr("{0}获得以下伤害修正：\n{1}"),
            unitDescription,
            modifierDescription);
    }

    string GetUnitDescription()
    {
        if (unitFilter == null)
        {
            return Tr("所有单位");
        }

        string filterDescription = unitFilter.GetDescription();
        if (string.IsNullOrEmpty(filterDescription) || filterDescription == Tr("所有对象"))
        {
            return Tr("所有单位");
        }

        return string.Format(
            Tr("满足条件的单位（{0}）"),
            filterDescription);
    }
}
