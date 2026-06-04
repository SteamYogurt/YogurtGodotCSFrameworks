using System;

public static class UnitCoreEvents
{
    public static event Action<DamageContext> DealingDamage;
    public static event Action<DamageContext> DealtDamage;
    public static event Action<DamageContext> ReceivingDamage;
    public static event Action<DamageContext> ReceivedDamage;

    internal static void RaiseDealingDamage(DamageContext ctx) => DealingDamage?.Invoke(ctx);

    internal static void RaiseDealtDamage(DamageContext ctx) => DealtDamage?.Invoke(ctx);

    internal static void RaiseReceivingDamage(DamageContext ctx) => ReceivingDamage?.Invoke(ctx);

    internal static void RaiseReceivedDamage(DamageContext ctx) => ReceivedDamage?.Invoke(ctx);

    /// <summary>
    /// Clears all subscribers. Call from <see cref="CombatRuntime"/> when starting or ending a match.
    /// </summary>
    public static void Reset()
    {
        DealingDamage = null;
        DealtDamage = null;
        ReceivingDamage = null;
        ReceivedDamage = null;
    }
}
