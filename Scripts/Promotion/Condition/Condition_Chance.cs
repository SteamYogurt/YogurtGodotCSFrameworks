using Godot;

[GlobalClass]
public partial class Condition_Chance : Condition
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float chance = 1f;

    public override bool IsMatch(ConditionContext context)
    {
        float clamped = Mathf.Clamp(chance, 0f, 1f);
        if (clamped <= 0f)
        {
            return false;
        }

        if (clamped >= 1f)
        {
            return true;
        }

        return GD.Randf() <= clamped;
    }
}
