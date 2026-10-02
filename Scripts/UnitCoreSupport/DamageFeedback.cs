using System;
using Godot;

/// <summary>
/// Forwards floating-text presentation to <see cref="MatchContext.SpawnDamageText"/> on the active match.
/// Assign it through <see cref="CombatRuntimeMatchOptions.SpawnDamageText"/> when the match starts.
/// </summary>
public static class DamageFeedback
{
	public static Action<DamageContext> SpawnText
	{
		get => CombatRuntime.Current?.SpawnDamageText;
		set
		{
			MatchContext match = CombatRuntime.Current;
			if (match == null)
			{
				GD.PrintErr("没有进行中的对局，飘字回调没有挂上。请在 CombatRuntimeMatchOptions.SpawnDamageText 里设置。");
				return;
			}

			match.SpawnDamageText = value;
		}
	}
}
