using System;
using Godot;

/// <summary>
/// Match-scoped adapter: <see cref="UnitCoreEvents"/> → <see cref="PromotionEventBus"/>.
/// Owned and disposed by <see cref="MatchContext"/>.
/// </summary>
public sealed class PromotionUnitCoreBridge : IDisposable
{
	readonly MatchContext match;
	bool registered;
	bool disposed;

	public PromotionUnitCoreBridge(MatchContext match)
	{
		this.match = match ?? throw new ArgumentNullException(nameof(match));
	}

	public void Register()
	{
		if (registered || disposed)
		{
			return;
		}

		registered = true;
		UnitCoreEvents.DealingDamage += OnDealingDamage;
		UnitCoreEvents.DealtDamage += OnDealtDamage;
		UnitCoreEvents.ReceivingDamage += OnReceivingDamage;
		UnitCoreEvents.ReceivedDamage += OnReceivedDamage;
	}

	public void Unregister()
	{
		if (!registered)
		{
			return;
		}

		registered = false;
		UnitCoreEvents.DealingDamage -= OnDealingDamage;
		UnitCoreEvents.DealtDamage -= OnDealtDamage;
		UnitCoreEvents.ReceivingDamage -= OnReceivingDamage;
		UnitCoreEvents.ReceivedDamage -= OnReceivedDamage;
	}

	void OnDealingDamage(DamageContext ctx) =>
		RaiseDamage(PromotionEventType.DealingDamage, ctx);

	void OnDealtDamage(DamageContext ctx) =>
		RaiseDamage(PromotionEventType.DealtDamage, ctx);

	void OnReceivingDamage(DamageContext ctx) =>
		RaiseDamage(PromotionEventType.ReceivingDamage, ctx);

	void OnReceivedDamage(DamageContext ctx) =>
		RaiseDamage(PromotionEventType.ReceivedDamage, ctx);

	void RaiseDamage(PromotionEventType eventType, DamageContext damageContext)
	{
		using ConditionContextScope scope = match.OpenContext();
		FillDamageContext(scope.Context, damageContext);
		match.EventBus.Raise(eventType, scope.Context);
	}

	static void FillDamageContext(ConditionContext context, DamageContext damageContext)
	{
		context.Set(ConditionSubjectKey.DamageContext, damageContext);

		if (damageContext == null)
		{
			return;
		}

		context.Set(ConditionSubjectKey.Attacker, damageContext.Attacker);
		context.Set(ConditionSubjectKey.Target, damageContext.Target);

		if (damageContext.Target is Node3D targetNode)
		{
			context.Set(ConditionSubjectKey.Position, targetNode.GlobalPosition);
		}
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		Unregister();
	}
}
