using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Condition_Any : Condition
{
    [Export] public Array<Condition> conditions = new();

    public override bool IsMatch(ConditionContext context) =>
        ConditionEvaluator.Any(conditions, context);
}
