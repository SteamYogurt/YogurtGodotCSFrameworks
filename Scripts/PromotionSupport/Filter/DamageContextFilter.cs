using Godot;

[GlobalClass]
public partial class DamageContextFilter : ObjectFilter
{
    [Export] public DamageTag requiredTags = DamageTag.None;
    [Export] public DamageTag excludedTags = DamageTag.None;

    public override bool IsMatch(object target)
    {
        if (target is not DamageContext damageContext)
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

    public override string GetDescription() => Tr("伤害上下文");
}
