using Godot;

[GlobalClass]
public partial class VulnerableBuffEffect : BuffEffect
{
    [Export] public float DamageMultiplierDelta = 0.2f;
    [Export] public float ExtraDamageMultiplierDeltaPerStack = 0f;

    public override void OnEnter(BuffInstance instance)
    {
        ApplyIncomingDamageModifier(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        ApplyIncomingDamageModifier(instance);
    }

    void ApplyIncomingDamageModifier(BuffInstance instance)
    {
        float value = instance.ResolveStackedValue(
            DamageMultiplierDelta,
            ExtraDamageMultiplierDeltaPerStack);
        value = instance.ResolveEffectValue(value);
        if (Mathf.IsZeroApprox(value))
        {
            return;
        }

        instance.AddDamageModifier(new DamageModifier
        {
            ApplyStage = DamageResolveStage.Incoming,
            incomingDamageMultiplierDelta = value,
        });
    }
}
