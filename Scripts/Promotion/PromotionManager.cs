using System.Collections.Generic;

public static class PromotionManager
{
    static readonly List<PromotionInstance> activeInstances = new();
    static bool initialized;

    public static IReadOnlyList<PromotionInstance> ActiveInstances => activeInstances;

    public static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        MarkReady();
    }

    public static PromotionInstance Fetch(Promotion promotion)
    {
        EnsureInitialized();
        if (promotion == null)
        {
            return null;
        }

        PromotionInstance instance = new PromotionInstance(promotion);
        instance.Activate();
        activeInstances.Add(instance);

        PromotionEventBus.Raise(
            PromotionEventType.OnAcquire,
            new ConditionContext().Set(ConditionSubjectKey.Source, instance));

        return instance;
    }

    public static bool Revert(PromotionInstance instance)
    {
        if (instance == null)
        {
            return false;
        }

        instance.Deactivate();
        return activeInstances.Remove(instance);
    }

    public static void ClearAll()
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            activeInstances[i]?.Deactivate();
        }

        activeInstances.Clear();
        PromotionEventBus.ClearAll();
        initialized = false;
    }

    internal static void MarkReady()
    {
        initialized = true;
        UnitCoreModifiers.GlobalOwner = PromotionServices.GlobalModifierOwner;
        PromotionUnitCoreBridge.Register();
    }
}
