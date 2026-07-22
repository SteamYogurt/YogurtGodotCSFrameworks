using Godot;

[GlobalClass]
public partial class ControlBuffEffect : BuffEffect
{
    [Export]
    public UnitControlFlag ControlFlags =
        UnitControlFlag.DisableMove
        | UnitControlFlag.DisableAttack
        | UnitControlFlag.DisableThink;

    public override void OnEnter(BuffInstance instance)
    {
        if (instance.Owner is not IUnitControlHost host)
        {
            instance.IsFinished = true;
            return;
        }

        UnitControlFlag flags = ControlFlags;
        if (flags == UnitControlFlag.None)
        {
            return;
        }

        host.UnitControlController.AddFlags(flags);
        instance.AddCleanup(() => host.UnitControlController.RemoveFlags(flags));
    }
}
