using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PromEff_SubscribeEvent : PromotionEffect
{
	[Export] public PromotionEventType eventType = PromotionEventType.DealtDamage;
	[Export] public Array<PromotionAction> actions = new();

	PromotionEffectContext effectContext;
	PromotionEffectHandle handle;

	public override PromotionEffectHandle Activate(PromotionEffectContext context)
	{
		effectContext = context;
		handle = new PromotionEffectHandle();
		handle.AddSubscription(context.Match.EventBus.Subscribe(eventType, InvokeAll));
		return handle;
	}

	void InvokeAll(ConditionContext ctx)
	{
		if (actions == null || ctx == null)
		{
			return;
		}

		ctx.Match ??= effectContext.Match;
		ctx.EffectHandle = handle;
		ctx.Set(ConditionSubjectKey.Source, effectContext.Instance);

		for (int i = 0; i < actions.Count; i++)
		{
			actions[i]?.Invoke(ctx);
		}
	}

	public override string GetDescription()
	{
		if (actions == null || actions.Count == 0)
		{
			return Tr("订阅事件时触发效果");
		}

		return Tr("订阅事件时触发配置的动作");
	}
}
