using Godot;

/// <summary>
/// Composable buff behavior. Attach multiple effects on a <see cref="Buff"/>.
/// </summary>
[GlobalClass]
public partial class BuffEffect : Resource
{
    public virtual void OnEnter(BuffInstance instance)
    {
    }

    public virtual void OnTick(BuffInstance instance, float delta)
    {
    }

    public virtual void OnStackChanged(BuffInstance instance)
    {
    }

    public virtual void OnExit(BuffInstance instance)
    {
    }
}
