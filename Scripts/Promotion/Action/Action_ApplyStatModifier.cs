using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Action_ApplyStatModifier : PromotionAction
{
    [Export] public ConditionSubjectKey targetKey = ConditionSubjectKey.Target;
    [Export] public Array<StatChangeConfig> statChangeConfigs = new();

    protected override void Execute(ConditionContext context)
    {
        if (context == null || context.GetObject(targetKey) is not IUnit unit)
        {
            return;
        }

        StatChangeConfig.ApplyConfigs(unit, statChangeConfigs, this);
    }
}
