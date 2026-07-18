using System;
using Godot;

public static class PromotionUnitEffectHelper
{
	public static bool IsUnitValid(IUnit unit)
	{
		if (unit == null)
		{
			return false;
		}

		if (unit is GodotObject godotObject)
		{
			return GodotObject.IsInstanceValid(godotObject);
		}

		return true;
	}

	public static string GetUnitFilterDescription(ObjectFilter unitFilter)
	{
		if (unitFilter == null)
		{
			return Tr("所有单位");
		}

		string filterDescription = unitFilter.GetDescription();
		if (string.IsNullOrEmpty(filterDescription) || filterDescription == Tr("所有对象"))
		{
			return Tr("所有单位");
		}

		return string.Format(
			Tr("满足条件的单位（{0}）"),
			filterDescription);
	}

	[Obsolete("Use MatchContext.ForEachActiveUnit instead.")]
	public static void ForEachMatchingUnit(
		ObjectFilter unitFilter,
		Action<IUnit> action,
		bool skipInvalid = true)
	{
		CombatRuntime.Current?.ForEachActiveUnit(action, unitFilter, skipInvalid);
	}
}
