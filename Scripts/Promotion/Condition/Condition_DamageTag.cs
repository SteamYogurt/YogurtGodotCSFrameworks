using Godot;

[GlobalClass]
public partial class Condition_DamageTag : Condition
{
    [Export] public DamageTag requiredTags = DamageTag.None;
    [Export] public DamageTag excludedTags = DamageTag.None;
    [Export] public ConditionSubjectKey damageContextKey = ConditionSubjectKey.DamageContext;

    public override bool IsMatch(ConditionContext context)
    {
        DamageContext damageContext = context?.Get<DamageContext>(damageContextKey);
        if (damageContext == null)
        {
            return false;
        }

        if (requiredTags != DamageTag.None && !damageContext.HasAnyTag(requiredTags))
        {
            return false;
        }

        if (excludedTags != DamageTag.None && damageContext.HasAnyTag(excludedTags))
        {
            return false;
        }

        return true;
    }
}
