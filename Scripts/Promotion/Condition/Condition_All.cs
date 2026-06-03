using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Condition_All : Condition
{
    [Export] public Array<Condition> conditions = new();

    public override bool IsMatch(ConditionContext context) =>
        ConditionEvaluator.All(conditions, context);
}
