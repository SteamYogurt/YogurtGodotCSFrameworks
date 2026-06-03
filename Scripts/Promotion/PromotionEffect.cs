using Godot;

[GlobalClass]
public partial class PromotionEffect : Resource
{
    public virtual PromotionEffectHandle Activate(PromotionEffectContext context) =>
        PromotionEffectHandle.Empty;

    public virtual string GetDescription() => null;
}
