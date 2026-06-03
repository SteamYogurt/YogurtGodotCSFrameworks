using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class BuffModifierController
{
    readonly List<BuffModifier> modifiers = new();
    public IReadOnlyList<BuffModifier> Modifiers => modifiers;

    public void AddModifier(BuffModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        modifiers.Add(modifier);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public bool RemoveModifier(BuffModifier modifier)
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

public static class BuffModifierOwnerExt
{
    static ConditionalWeakTable<object, BuffModifierController> controllers = new();

    public static BuffModifierController GetBuffModifierController(this object owner)
    {
        if (owner == null)
        {
            return null;
        }

        return controllers.GetValue(owner, _ => new BuffModifierController());
    }

    public static bool TryGetBuffModifierController(
        this object owner,
        out BuffModifierController controller)
    {
        controller = null;
        if (owner == null)
        {
            return false;
        }

        return controllers.TryGetValue(owner, out controller);
    }

    public static void ResetAllBuffModifierControllers()
    {
        controllers = new ConditionalWeakTable<object, BuffModifierController>();
    }
}