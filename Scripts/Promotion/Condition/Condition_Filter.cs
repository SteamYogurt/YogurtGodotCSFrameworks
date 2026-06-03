using Godot;

[GlobalClass]
public partial class Condition_Filter : Condition
{
    [Export] public ObjectFilter filter;
    [Export] public ConditionSubjectKey subjectKey = ConditionSubjectKey.Target;

    public override bool IsMatch(ConditionContext context)
    {
        if (filter == null)
        {
            return true;
        }

        if (context == null || !context.Has(subjectKey))
        {
            return false;
        }

        return filter.IsMatch(context.GetObject(subjectKey));
    }
}
