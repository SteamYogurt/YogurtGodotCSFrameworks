public class PromotionEffectContext
{
	public MatchContext Match { get; }
	public PromotionInstance Instance { get; }
	public Promotion Source => Instance?.sourcePromotion;
	public object ModifierOwner => Match.GlobalModifierOwner;

	public PromotionEffectContext(PromotionInstance instance, MatchContext match)
	{
		Instance = instance;
		Match = match ?? throw new System.ArgumentNullException(nameof(match));
	}
}
