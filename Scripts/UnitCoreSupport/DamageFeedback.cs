using System;

public static class DamageFeedback
{
    /// <summary>
    /// Hook for floating damage text or other presentation. Assign from game layer when ready.
    /// </summary>
    public static Action<DamageContext> SpawnText { get; set; }
}
