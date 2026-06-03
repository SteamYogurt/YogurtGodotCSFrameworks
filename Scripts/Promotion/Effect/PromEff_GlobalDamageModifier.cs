using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromEff_GlobalDamageModifier : PromotionEffect
{
    [Export] public Array<DamageModifier> damageModifiers = new();

    public override PromotionEffectHandle Activate(PromotionEffectContext context)
    {
        PromotionEffectHandle handle = new PromotionEffectHandle();
        ApplyModifiers();
        handle.AddCleanup(RemoveModifiers);
        return handle;
    }

    void ApplyModifiers()
    {
        DamageModifierController controller =
            PromotionServices.GlobalModifierOwner.GetDamageModifierController();
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

    void RemoveModifiers()
    {
        PromotionServices.GlobalModifierOwner
            .GetDamageModifierController()
            .RemoveAllFromSource(this);
    }

    public override string GetDescription()
    {
        string modifierDescription = DamageModifier.GetDescription(damageModifiers);
        if (string.IsNullOrEmpty(modifierDescription))
        {
            return Tr("获得全局伤害修正");
        }

        return string.Format(
            Tr("获得以下全局伤害修正：\n{0}"),
            modifierDescription);
    }
}
