using Godot;

[GlobalClass]
public partial class Action_ModifyDamageContext : PromotionAction
{
    [Export] public float rawPhysicalDamageDelta;
    [Export] public float rawMagicalDamageDelta;
    [Export] public float rawRealDamageDelta;

    [Export] public float outgoingDamageMultiplierDelta;
    [Export] public float incomingDamageMultiplierDelta;
    [Export] public float finalDamageMultiplierDelta;
    [Export] public float damageMultiplierDelta;
    [Export] public float critMultiplierDelta;

    [Export] public bool forceCrit;
    [Export] public bool hideDamageText;

    protected override void Execute(ConditionContext context)
    {
        DamageContext damageContext = context?.Get<DamageContext>(ConditionSubjectKey.DamageContext);
        if (damageContext == null)
        {
            return;
        }

        if (!Mathf.IsZeroApprox(rawPhysicalDamageDelta))
        {
            damageContext.RawPhysicalDamage = Mathf.Max(
                0f,
                damageContext.RawPhysicalDamage + rawPhysicalDamageDelta);
        }

        if (!Mathf.IsZeroApprox(rawMagicalDamageDelta))
        {
            damageContext.RawMagicalDamage = Mathf.Max(
                0f,
                damageContext.RawMagicalDamage + rawMagicalDamageDelta);
        }

        if (!Mathf.IsZeroApprox(rawRealDamageDelta))
        {
            damageContext.RawRealDamage = Mathf.Max(
                0f,
                damageContext.RawRealDamage + rawRealDamageDelta);
        }

        if (!Mathf.IsZeroApprox(outgoingDamageMultiplierDelta))
        {
            damageContext.OutgoingDamageMultiplier = Mathf.Max(
                0f,
                damageContext.OutgoingDamageMultiplier + outgoingDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(incomingDamageMultiplierDelta))
        {
            damageContext.IncomingDamageMultiplier = Mathf.Max(
                0f,
                damageContext.IncomingDamageMultiplier + incomingDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(finalDamageMultiplierDelta))
        {
            damageContext.FinalDamageMultiplier = Mathf.Max(
                0f,
                damageContext.FinalDamageMultiplier + finalDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(damageMultiplierDelta))
        {
            damageContext.DamageMultiplier = Mathf.Max(
                0f,
                damageContext.DamageMultiplier + damageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(critMultiplierDelta))
        {
            damageContext.CritMultiplier = Mathf.Max(
                0f,
                damageContext.CritMultiplier + critMultiplierDelta);
        }

        if (forceCrit)
        {
            damageContext.IsCrit = true;
        }

        if (hideDamageText)
        {
            damageContext.ShowText = false;
        }
    }
}
