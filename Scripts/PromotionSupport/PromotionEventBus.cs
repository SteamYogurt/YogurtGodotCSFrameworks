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

public static class PromotionEventBus
{
    static readonly Dictionary<PromotionEventType, List<Action<ConditionContext>>> handlers = new();

    public static PromotionEventSubscription Subscribe(
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
        return new PromotionEventSubscription(eventType, handler);
    }

    public static void Raise(PromotionEventType eventType, ConditionContext context)
    {
        if (!handlers.TryGetValue(eventType, out List<Action<ConditionContext>> list))
        {
            return;
        }

        context ??= new ConditionContext();
        for (int i = 0; i < list.Count; i++)
        {
            list[i]?.Invoke(context);
        }
    }

    internal static void Unsubscribe(PromotionEventType eventType, Action<ConditionContext> handler)
    {
        if (handler == null || !handlers.TryGetValue(eventType, out List<Action<ConditionContext>> list))
        {
            return;
        }

        list.Remove(handler);
    }

    internal static void ClearAll()
    {
        handlers.Clear();
    }
}

public sealed class PromotionEventSubscription : IDisposable
{
    public static PromotionEventSubscription Empty { get; } = new(PromotionEventType.OnAcquire, null);

    readonly PromotionEventType eventType;
    readonly Action<ConditionContext> handler;
    bool disposed;

    internal PromotionEventSubscription(PromotionEventType eventType, Action<ConditionContext> handler)
    {
        this.eventType = eventType;
        this.handler = handler;
    }

    public void Dispose()
    {
        if (disposed || handler == null)
        {
            return;
        }

        disposed = true;
        PromotionEventBus.Unsubscribe(eventType, handler);
    }
}
