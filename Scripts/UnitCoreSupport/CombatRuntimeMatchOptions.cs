using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Optional hooks wired when <see cref="CombatRuntime.BeginMatch"/> runs.
/// All match wiring lives on the returned <see cref="MatchContext"/>.
/// </summary>
public sealed class CombatRuntimeMatchOptions
{
	/// <summary>All units that promotion effects may target (stat/damage modifiers, etc.).</summary>
	public Func<IEnumerable<IUnit>> ActiveUnits { get; set; }

	/// <summary>Floating damage text or other presentation.</summary>
	public Action<DamageContext> SpawnDamageText { get; set; }

	/// <summary>World VFX at a position (e.g. promotion AOE sparkle).</summary>
	public Action<string, Vector3> SpawnEffectAtPosition { get; set; }

	/// <summary>If set, calls <see cref="Buff.LoadFrom"/> on match start.</summary>
	public string BuffResourceRootPath { get; set; }
}
