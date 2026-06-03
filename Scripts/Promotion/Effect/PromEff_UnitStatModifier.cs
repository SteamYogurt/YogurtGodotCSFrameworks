using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromEff_UnitStatModifier : PromotionEffect
{
    [Export] public ObjectFilter unitFilter;
    [Export] public Array<StatChangeConfig> statChangeConfigs = new();

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

        StatChangeConfig.ApplyConfigs(unit, statChangeConfigs, this);
    }

    void RemoveFromUnit(IUnit unit)
    {
        if (!PromotionUnitEffectHelper.IsUnitValid(unit) || statChangeConfigs == null)
        {
            return;
        }

        for (int i = 0; i < statChangeConfigs.Count; i++)
        {
            StatChangeConfig config = statChangeConfigs[i];
            if (config == null)
            {
                continue;
            }

            unit.RemoveStatModifier(config.StatType, this);
        }
    }

    public override string GetDescription()
    {
        string unitDescription = GetUnitDescription();
        string statDescription = StatChangeConfig.GetDescription(statChangeConfigs);

        if (string.IsNullOrEmpty(statDescription))
        {
            return string.Format(
                Tr("{0}获得属性修正"),
                unitDescription);
        }

        return string.Format(
            Tr("{0}获得以下属性修正：\n{1}"),
            unitDescription,
            statDescription);
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
