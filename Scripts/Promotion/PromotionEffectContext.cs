public class PromotionEffectContext
{
    public PromotionInstance Instance { get; }
    public Promotion Source => Instance?.sourcePromotion;
    public object ModifierOwner => PromotionServices.GlobalModifierOwner;

    public PromotionEffectContext(PromotionInstance instance)
    {
        Instance = instance;
    }
}
