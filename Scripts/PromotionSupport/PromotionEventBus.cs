using System;
using System.Collections.Generic;

public enum PromotionEventType
{
	OnAcquire,
	DealingDamage,
	DealtDamage,
	ReceivingDamage,
	ReceivedDamage,
	UnitSpawned,
}

/// <summary>
/// Instance event bus owned by <see cref="MatchContext"/>. Not a process-wide singleton.
/// </summary>
public sealed class PromotionEventBus
{
	readonly Dictionary<PromotionEventType, List<Action<ConditionContext>>> handlers = new();
	readonly List<Action<ConditionContext>> raiseScratch = new();

	public PromotionEventSubscription Subscribe(
		PromotionEventType eventType,
		Action<ConditionContext> handler)
	{
		if (handler == null)
		{
			return PromotionEventSubscription.Empty;
		}

		if (!handlers.TryGetValue(eventType, out List<Action<ConditionContext>> list))
		{
			list = new List<Action<ConditionContext>>();
			handlers[eventType] = list;
		}

		list.Add(handler);
		return new PromotionEventSubscription(this, eventType, handler);
	}

	public void Raise(PromotionEventType eventType, ConditionContext context)
	{
		if (context == null
			|| !handlers.TryGetValue(eventType, out List<Action<ConditionContext>> list)
			|| list.Count == 0)
		{
			return;
		}

		// Snapshot so Subscribe/Unsubscribe/Deactivate during invoke cannot skip handlers.
		raiseScratch.Clear();
		raiseScratch.AddRange(list);
		for (int i = 0; i < raiseScratch.Count; i++)
		{
			raiseScratch[i]?.Invoke(context);
		}
		raiseScratch.Clear();
	}

	internal void Unsubscribe(PromotionEventType eventType, Action<ConditionContext> handler)
	{
		if (handler == null || !handlers.TryGetValue(eventType, out List<Action<ConditionContext>> list))
		{
			return;
		}

		list.Remove(handler);
	}

	internal void ClearAll()
	{
		handlers.Clear();
		raiseScratch.Clear();
	}
}

public sealed class PromotionEventSubscription : IDisposable
{
	public static PromotionEventSubscription Empty { get; } = new(null, PromotionEventType.OnAcquire, null);

	readonly PromotionEventBus bus;
	readonly PromotionEventType eventType;
	readonly Action<ConditionContext> handler;
	bool disposed;

	internal PromotionEventSubscription(
		PromotionEventBus bus,
		PromotionEventType eventType,
		Action<ConditionContext> handler)
	{
		this.bus = bus;
		this.eventType = eventType;
		this.handler = handler;
	}

	public void Dispose()
	{
		if (disposed || handler == null || bus == null)
		{
			return;
		}

		disposed = true;
		bus.Unsubscribe(eventType, handler);
	}
}
