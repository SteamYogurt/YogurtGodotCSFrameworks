using Godot;

[GlobalClass]
public partial class Action_RemoveShield : PromotionAction
{
    [Export] public ConditionSubjectKey targetKey = ConditionSubjectKey.Target;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ratio = 0.2f;

    protected override void Execute(ConditionContext context)
    {
        if (context?.GetObject(targetKey) is not IUnit unit)
        {
            return;
        }

        if (unit.MaxShield <= 0f || unit.Shield <= 0f)
        {
            return;
        }

        float removeValue = unit.MaxShield * Mathf.Clamp(ratio, 0f, 1f);
        if (removeValue <= 0f)
        {
            return;
        }

        unit.Shield -= removeValue;
    }
}
