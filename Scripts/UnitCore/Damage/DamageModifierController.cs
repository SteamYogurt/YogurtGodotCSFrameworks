using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class DamageModifierController
{
    readonly List<DamageModifier> modifiers = new();
    public IReadOnlyList<DamageModifier> Modifiers => modifiers;

    public void AddModifier(DamageModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        ModifierCollectionUtil.InsertSortedByPriority(
            modifiers,
            modifier,
            static m => m.Priority);
    }

    public bool RemoveModifier(DamageModifier modifier)
    {
        if (modifier == null)
        {
            return false;
        }

        return modifiers.Remove(modifier);
    }

    public void RemoveAllFromSource(object source)
    {
        if (source == null)
        {
            return;
        }

        modifiers.RemoveAll(m => ReferenceEquals(m.RuntimeSource, source));
    }

    public void Clear()
    {
        modifiers.Clear();
    }

    public bool HasAny()
    {
        return modifiers.Count > 0;
    }
}

public static class DamageModifierOwnerExt
{
    static ConditionalWeakTable<object, DamageModifierController> controllers = new();

    public static DamageModifierController GetDamageModifierController(this object owner)
    {
        if (owner == null)
        {
            return null;
        }

        return controllers.GetValue(owner, _ => new DamageModifierController());
    }

    public static bool TryGetDamageModifierController(
        this object owner,
        out DamageModifierController controller)
    {
        controller = null;
        if (owner == null)
        {
            return false;
        }

        return controllers.TryGetValue(owner, out controller);
    }

    public static void ResetAllDamageModifierControllers()
    {
        controllers = new ConditionalWeakTable<object, DamageModifierController>();
    }
}