using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromotionAction : Resource
{
    [Export] public Array<Condition> conditions = new();

    public void Invoke(ConditionContext context)
    {
        if (!ConditionEvaluator.All(conditions, context))
        {
            return;
        }

        Execute(context);
    }

    protected virtual void Execute(ConditionContext context)
    {
    }
}
