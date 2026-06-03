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

        if (!string.IsNullOrWhiteSpace(requiredBuffId)
            && !unit.BuffController.HasBuff(requiredBuffId))
        {
            return false;
        }

        if (requiredBuffTag != BuffTag.None
            && !unit.BuffController.HasBuffTag(requiredBuffTag))
        {
            return false;
        }

        return true;
    }
}
