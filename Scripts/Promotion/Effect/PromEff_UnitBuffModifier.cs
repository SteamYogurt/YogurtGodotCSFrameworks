using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromEff_UnitBuffModifier : PromotionEffect
{
    [Export] public ObjectFilter unitFilter;
    [Export] public Array<BuffModifier> buffModifiers = new();

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

        BuffModifierController controller = unit.GetBuffModifierController();
        controller.RemoveAllFromSource(this);

        if (buffModifiers == null)
        {
            return;
        }

        foreach (BuffModifier buffModifier in buffModifiers)
        {
            if (buffModifier == null)
            {
                continue;
            }

            BuffModifier runtimeModifier = buffModifier.Duplicate() as BuffModifier ?? buffModifier;
            runtimeModifier.RuntimeSource = this;
            controller.AddModifier(runtimeModifier);
        }

        unit.BuffController?.RefreshAllBuffModifierValues();
    }

    void RemoveFromUnit(IUnit unit)
    {
        if (!PromotionUnitEffectHelper.IsUnitValid(unit))
        {
            return;
        }

        unit.GetBuffModifierController().RemoveAllFromSource(this);
        unit.BuffController?.RefreshAllBuffModifierValues();
    }

    public override string GetDescription()
    {
        string unitDescription = GetUnitDescription();
        string modifierDescription = BuffModifier.GetDescription(buffModifiers);

        if (string.IsNullOrEmpty(modifierDescription))
        {
            return string.Format(
                Tr("{0}获得Buff修正"),
                unitDescription);
        }

        return string.Format(
            Tr("{0}获得以下Buff修正：\n{1}"),
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
