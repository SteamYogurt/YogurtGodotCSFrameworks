using System.Collections.Generic;
using Godot;
using Godot.Collections;

/// <summary>
/// Grants per-unit Stat / DamageModifier / BuffModifier while the promotion is active.
/// Empty arrays are ignored. Replaces the old UnitStat / UnitDamage / UnitBuff effect copies.
/// </summary>
[GlobalClass]
public partial class PromEff_UnitModifierGrant : PromotionEffect
{
	[Export] public ObjectFilter unitFilter;
	[Export] public Array<StatChangeConfig> statChangeConfigs = new();
	[Export] public Array<DamageModifier> damageModifiers = new();
	[Export] public Array<BuffModifier> buffModifiers = new();

	MatchContext match;
	readonly List<IUnit> appliedUnits = new();

	public override PromotionEffectHandle Activate(PromotionEffectContext context)
	{
		match = context.Match;
		PromotionEffectHandle handle = new PromotionEffectHandle();

		ApplyToAllMatchingUnits();
		handle.AddSubscription(match.EventBus.Subscribe(
			PromotionEventType.UnitSpawned,
			OnUnitSpawned));
		handle.AddCleanup(RemoveFromAllAppliedUnits);

		return handle;
	}

	void OnUnitSpawned(ConditionContext ctx)
	{
		if (ctx?.GetObject(ConditionSubjectKey.Subject) is not IUnit unit)
		{
			return;
		}

		if (unitFilter != null && !unitFilter.IsMatch(unit))
		{
			return;
		}

		ApplyToUnit(unit);
	}

	void ApplyToAllMatchingUnits()
	{
		match.ForEachActiveUnit(ApplyToUnit, unitFilter);
	}

	void RemoveFromAllAppliedUnits()
	{
		for (int i = appliedUnits.Count - 1; i >= 0; i--)
		{
			RemoveFromUnit(appliedUnits[i]);
		}

		appliedUnits.Clear();
	}

	void TrackApplied(IUnit unit)
	{
		if (!appliedUnits.Contains(unit))
		{
			appliedUnits.Add(unit);
		}
	}

	void ApplyToUnit(IUnit unit)
	{
		if (!PromotionUnitEffectHelper.IsUnitValid(unit))
		{
			return;
		}

		RemoveStatModifiers(unit);
		StatChangeConfig.ApplyConfigs(unit, statChangeConfigs, this);
		ApplyDamageModifiers(unit);
		ApplyBuffModifiers(unit);
		TrackApplied(unit);
	}

	void RemoveFromUnit(IUnit unit)
	{
		if (!PromotionUnitEffectHelper.IsUnitValid(unit))
		{
			return;
		}

		RemoveStatModifiers(unit);
		unit.GetDamageModifierController().RemoveAllFromSource(this);
		unit.GetBuffModifierController().RemoveAllFromSource(this);
		unit.BuffController?.RefreshAllBuffModifierValues();
	}

	void ApplyDamageModifiers(IUnit unit)
	{
		DamageModifierController controller = unit.GetDamageModifierController();
		controller.RemoveAllFromSource(this);

		if (damageModifiers == null)
		{
			return;
		}

		foreach (DamageModifier damageModifier in damageModifiers)
		{
			if (damageModifier == null)
			{
				continue;
			}

			DamageModifier runtimeModifier = damageModifier.Duplicate() as DamageModifier ?? damageModifier;
			runtimeModifier.RuntimeSource = this;
			controller.AddModifier(runtimeModifier);
		}
	}

	void ApplyBuffModifiers(IUnit unit)
	{
		BuffModifierController controller = unit.GetBuffModifierController();
		controller.RemoveAllFromSource(this);

		if (buffModifiers == null)
		{
			return;
		}

		foreach (BuffModifier buffModifier in buffModifiers)
		{
			if (buffModifier == null)
			{
				continue;
			}

			BuffModifier runtimeModifier = buffModifier.Duplicate() as BuffModifier ?? buffModifier;
			runtimeModifier.RuntimeSource = this;
			controller.AddModifier(runtimeModifier);
		}

		unit.BuffController?.RefreshAllBuffModifierValues();
	}

	void RemoveStatModifiers(IUnit unit)
	{
		if (statChangeConfigs == null)
		{
			return;
		}

		for (int i = 0; i < statChangeConfigs.Count; i++)
		{
			StatChangeConfig config = statChangeConfigs[i];
			if (config == null)
			{
				continue;
			}

			unit.RemoveStatModifier(config.StatType, this);
		}
	}

	public override string GetDescription()
	{
		string unitDescription = PromotionUnitEffectHelper.GetUnitFilterDescription(unitFilter);
		System.Collections.Generic.List<string> parts = new();

		string statDescription = StatChangeConfig.GetDescription(statChangeConfigs);
		if (!string.IsNullOrEmpty(statDescription))
		{
			parts.Add(statDescription);
		}

		string damageDescription = DamageModifier.GetDescription(damageModifiers);
		if (!string.IsNullOrEmpty(damageDescription))
		{
			parts.Add(damageDescription);
		}

		string buffDescription = BuffModifier.GetDescription(buffModifiers);
		if (!string.IsNullOrEmpty(buffDescription))
		{
			parts.Add(buffDescription);
		}

		if (parts.Count == 0)
		{
			return string.Format(Tr("{0}获得单位修正"), unitDescription);
		}

		return string.Format(
			Tr("{0}获得以下修正：\n{1}"),
			unitDescription,
			string.Join("\n", parts));
	}
}
