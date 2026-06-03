using System;
using System.Collections.Generic;

public static class PromotionServices
{
    public static readonly object GlobalModifierOwner = new();

    public static Func<IEnumerable<IUnit>> ActiveUnits { get; set; }

    public static Action<string, Godot.Vector3> SpawnEffectAtPosition { get; set; }

    public static void NotifyUnitSpawned(IUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        PromotionManager.EnsureInitialized();
        PromotionEventBus.Raise(
            PromotionEventType.UnitSpawned,
            new ConditionContext().Set(ConditionSubjectKey.Subject, unit));
    }
}
