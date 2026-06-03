using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Action_ApplyBuff : PromotionAction
{
    [Export] public ConditionSubjectKey targetKey = ConditionSubjectKey.Target;
    [Export] public ConditionSubjectKey casterKey = ConditionSubjectKey.Attacker;
    [Export] public Buff buff;
    [Export(PropertyHint.Range, "1,999,1")] public int stacks = 1;

    protected override void Execute(ConditionContext context)
    {
        if (buff == null || context == null)
        {
            return;
        }

        if (context.GetObject(targetKey) is not IUnit unit || unit.BuffController == null)
        {
            return;
        }

        object caster = context.GetObject(casterKey);
        unit.BuffController.AddBuff(buff, caster, Mathf.Max(1, stacks));
    }
}
