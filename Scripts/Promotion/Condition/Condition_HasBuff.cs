using Godot;

[GlobalClass]
public partial class Condition_HasBuff : Condition
{
    [Export] public ConditionSubjectKey unitKey = ConditionSubjectKey.Target;
    [Export] public string requiredBuffId;
    [Export] public BuffTag requiredBuffTag = BuffTag.None;

    public override bool IsMatch(ConditionContext context)
    {
        if (context == null || !context.Has(unitKey))
        {
            return false;
        }

        if (context.GetObject(unitKey) is not IUnit unit || unit.BuffController == null)
        {
            return false;
        }

        bool hasIdRequirement = !string.IsNullOrWhiteSpace(requiredBuffId);
        bool hasTagRequirement = requiredBuffTag != BuffTag.None;
        if (!hasIdRequirement && !hasTagRequirement)
        {
            return false;
        }

        if (hasIdRequirement && !unit.BuffController.HasBuff(requiredBuffId))
        {
            return false;
        }

        if (hasTagRequirement && !unit.BuffController.HasBuffTag(requiredBuffTag))
        {
            return false;
        }

        return true;
    }
}
