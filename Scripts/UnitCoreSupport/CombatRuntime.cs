/// <summary>
/// Single entry to create and tear down a <see cref="MatchContext"/> for UnitCore + Promotion.
/// Call <see cref="BeginMatch"/> when a run starts; <see cref="EndMatch"/> when it ends.
/// </summary>
public static class CombatRuntime
{
	public static MatchContext Current { get; private set; }

	public static bool IsMatchActive => Current is { IsActive: true };

	/// <summary>
	/// Global modifier owner for the active match (promotion-wide damage/buff modifiers).
	/// </summary>
	public static object GlobalModifierOwner => Current?.GlobalModifierOwner;

	/// <summary>
	/// Resets process scratch, then creates and activates a new <see cref="MatchContext"/>.
	/// Resource catalogs are scanned only when the matching root path is set. They outlive the match.
	/// </summary>
	public static MatchContext BeginMatch(CombatRuntimeMatchOptions options = null)
	{
		EndMatch();
		MatchContext.ClearProcessScratch();

		if (!string.IsNullOrEmpty(options?.BuffResourceRootPath))
		{
			BuffExt.Init(options.BuffResourceRootPath);
		}

		if (!string.IsNullOrEmpty(options?.PromotionResourceRootPath))
		{
			PromotionExt.Init(options.PromotionResourceRootPath);
		}

		MatchContext match = new MatchContext(options);
		Current = match;
		match.Activate();
		return match;
	}

	/// <summary>
	/// Same as <see cref="BeginMatch"/> without game-layer hooks (tests or minimal setup).
	/// </summary>
	public static MatchContext BeginMatchMinimal() => BeginMatch(null);

	/// <summary>
	/// Disposes <see cref="Current"/>. Safe to call even if <see cref="BeginMatch"/> was not called.
	/// </summary>
	public static void EndMatch()
	{
		if (Current == null)
		{
			return;
		}

		MatchContext match = Current;
		match.Dispose();
		if (ReferenceEquals(Current, match))
		{
			Current = null;
		}
	}

	/// <summary>
	/// Returns the active match or throws. Prefer passing <see cref="MatchContext"/> explicitly when available.
	/// </summary>
	public static MatchContext RequireCurrent()
	{
		if (!IsMatchActive)
		{
			throw new System.InvalidOperationException(
				"No active MatchContext. Call CombatRuntime.BeginMatch first.");
		}

		return Current;
	}
}
