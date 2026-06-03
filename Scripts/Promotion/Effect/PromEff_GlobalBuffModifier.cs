using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromEff_GlobalBuffModifier : PromotionEffect
{
    [Export] public Array<BuffModifier> buffModifiers = new();

    public override PromotionEffectHandle Activate(PromotionEffectContext context)
    {
        PromotionEffectHandle handle = new PromotionEffectHandle();
        ApplyModifiers();
        handle.AddCleanup(RemoveModifiers);
        handle.AddCleanup(RefreshAllUnits);
        return handle;
    }

    void ApplyModifiers()
    {
        BuffModifierController controller =
            PromotionServices.GlobalModifierOwner.GetBuffModifierController();
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

        RefreshAllUnits();
    }

    void RemoveModifiers()
    {
        PromotionServices.GlobalModifierOwner
            .GetBuffModifierController()
            .RemoveAllFromSource(this);
    }

    void RefreshAllUnits()
    {
        PromotionUnitEffectHelper.ForEachMatchingUnit(
            null,
            unit => unit.BuffController?.RefreshAllBuffModifierValues());
    }

    public override string GetDescription()
    {
        string modifierDescription = BuffModifier.GetDescription(buffModifiers);
        if (string.IsNullOrEmpty(modifierDescription))
        {
            return Tr("获得全局Buff修正");
        }

        return string.Format(
            Tr("获得以下全局Buff修正：\n{0}"),
            modifierDescription);
    }
}
