using Godot;

[GlobalClass]
public partial class Condition : Resource
{
    public virtual bool IsMatch(ConditionContext context) => true;
}
