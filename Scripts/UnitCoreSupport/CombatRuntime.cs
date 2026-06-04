using Godot;

/// <summary>
/// Single entry to reset and prepare UnitCore + Promotion runtime for a new match.
/// Call <see cref="BeginMatch"/> when a run starts; <see cref="EndMatch"/> when it ends.
/// </summary>
public static class CombatRuntime
{
    public static bool IsMatchActive { get; private set; }

    /// <summary>
    /// Resets all global combat/promotion state, then registers bridges and applies optional game hooks.
    /// </summary>
    public static void BeginMatch(CombatRuntimeMatchOptions options = null)
    {
        ResetInternal();
        ApplyOptions(options);
        PromotionManager.MarkReady();
        IsMatchActive = true;
    }

    /// <summary>
    /// Same as <see cref="BeginMatch"/> without game-layer hooks (tests or minimal setup).
    /// </summary>
    public static void BeginMatchMinimal() => BeginMatch(null);

    /// <summary>
    /// Tears down match state. Safe to call even if <see cref="BeginMatch"/> was not called.
    /// </summary>
    public static void EndMatch()
    {
        ResetInternal();
        IsMatchActive = false;
    }

    static void ResetInternal()
    {
        PromotionManager.ClearAll();
        PromotionUnitCoreBridge.Unregister();

        UnitCoreEvents.Reset();

        DamageModifierOwnerExt.ResetAllDamageModifierControllers();
        BuffModifierOwnerExt.ResetAllBuffModifierControllers();

        DamageModifierCollectSession.ResetScratch();
        BuffModifierCollectSession.ResetScratch();

        UnitCoreModifiers.GlobalOwner = null;

        DamageFeedback.SpawnText = null;
        PromotionServices.ActiveUnits = null;
        PromotionServices.SpawnEffectAtPosition = null;
    }

    static void ApplyOptions(CombatRuntimeMatchOptions options)
    {
        if (options == null)
        {
            return;
        }

        PromotionServices.ActiveUnits = options.ActiveUnits;
        DamageFeedback.SpawnText = options.SpawnDamageText;
        PromotionServices.SpawnEffectAtPosition = options.SpawnEffectAtPosition;

        if (!string.IsNullOrEmpty(options.BuffResourceRootPath))
        {
            Buff.LoadFrom(options.BuffResourceRootPath);
        }
    }
}
