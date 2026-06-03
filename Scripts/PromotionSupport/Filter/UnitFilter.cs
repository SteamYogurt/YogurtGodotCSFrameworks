using Godot;

[GlobalClass]
public partial class UnitFilter : ObjectFilter
{
    [Export] public bool requireAlive = true;

    public override bool IsMatch(object target)
    {
        if (target is not IUnit unit)
        {
            return false;
        }

        return !requireAlive || unit.IsAlive;
    }

    public override string GetDescription() => Tr("单位");
}
