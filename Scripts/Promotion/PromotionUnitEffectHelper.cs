using System;
using System.Collections.Generic;
using Godot;

public static class PromotionUnitEffectHelper
{
    public static void ForEachMatchingUnit(
        ObjectFilter unitFilter,
        Action<IUnit> action,
        bool skipInvalid = true)
    {
        if (action == null)
        {
            return;
        }

        IEnumerable<IUnit> units = PromotionServices.ActiveUnits?.Invoke();
        if (units == null)
        {
            return;
        }

        foreach (IUnit unit in units)
        {
            if (unit == null)
            {
                continue;
            }

            if (skipInvalid && unit is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
            {
                continue;
            }

            if (unitFilter != null && !unitFilter.IsMatch(unit))
            {
                continue;
            }

            action(unit);
        }
    }

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
}
