using Godot;

[GlobalClass]
public partial class ShieldBreakBuffEffect : BuffEffect
{
    [Export] public float ShieldDamageMultiplierDelta = 0.5f;
    [Export] public float ExtraShieldDamageMultiplierDeltaPerStack = 0f;

    public override void OnEnter(BuffInstance instance)
    {
        ApplyShieldDamageModifier(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        ApplyShieldDamageModifier(instance);
    }

    void ApplyShieldDamageModifier(BuffInstance instance)
    {
        float value = instance.ResolveStackedValue(
            ShieldDamageMultiplierDelta,
            ExtraShieldDamageMultiplierDeltaPerStack);
        value = instance.ResolveEffectValue(value);
        if (Mathf.IsZeroApprox(value))
        {
            return;
        }

        instance.AddDamageModifier(new DamageModifier
        {
            ApplyStage = DamageResolveStage.Final,
            shieldDamageMultiplierDelta = value,
        });
    }
}
