using Godot;

public static class PromotionUnitCoreBridge
{
    static bool registered;

    public static void Register()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        UnitCoreEvents.DealingDamage += OnDealingDamage;
        UnitCoreEvents.DealtDamage += OnDealtDamage;
        UnitCoreEvents.ReceivingDamage += OnReceivingDamage;
        UnitCoreEvents.ReceivedDamage += OnReceivedDamage;
    }

    public static void Unregister()
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

    static void OnDealingDamage(DamageContext ctx) =>
        RaiseDamage(PromotionEventType.DealingDamage, ctx);

    static void OnDealtDamage(DamageContext ctx) =>
        RaiseDamage(PromotionEventType.DealtDamage, ctx);

    static void OnReceivingDamage(DamageContext ctx) =>
        RaiseDamage(PromotionEventType.ReceivingDamage, ctx);

    static void OnReceivedDamage(DamageContext ctx) =>
        RaiseDamage(PromotionEventType.ReceivedDamage, ctx);

    static void RaiseDamage(PromotionEventType eventType, DamageContext damageContext)
    {
        ConditionContext context = BuildDamageContext(damageContext);
        PromotionEventBus.Raise(eventType, context);
    }

    public static ConditionContext BuildDamageContext(DamageContext damageContext)
    {
        ConditionContext context = new ConditionContext()
            .Set(ConditionSubjectKey.DamageContext, damageContext);

        if (damageContext == null)
        {
            return context;
        }

        context.Set(ConditionSubjectKey.Attacker, damageContext.Attacker);
        context.Set(ConditionSubjectKey.Target, damageContext.Target);

        if (damageContext.Target is Node3D targetNode)
        {
            context.Set(ConditionSubjectKey.Position, targetNode.GlobalPosition);
        }

        return context;
    }
}
