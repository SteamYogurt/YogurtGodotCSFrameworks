using Godot;

[GlobalClass]
public partial class ObjectFilter : Resource
{
    public virtual bool IsMatch(object target) => true;

    public virtual string GetDescription() => Tr("所有对象");
}
