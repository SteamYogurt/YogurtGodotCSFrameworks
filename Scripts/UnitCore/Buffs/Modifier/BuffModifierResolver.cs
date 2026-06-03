using System.Collections.Generic;
using Godot;

public static class BuffModifierResolver
{
    public static void RefreshRuntimeValues(BuffInstance instance)
    {
        if (instance == null || instance.Data?.buffInfo == null)
        {
            return;
        }

        BuffInfo buffInfo = instance.Data.buffInfo;
        BuffModifierContext context = new(instance)
        {
            Stage = BuffModifierStage.Apply,
            Duration = buffInfo.duration,
            TickInterval = Mathf.Max(0.0001f, buffInfo.tickInterval),
            MaxStacks = Mathf.Max(1, buffInfo.maxStacks),
        };

        List<BuffModifier> modifiers = CollectModifiers(context);
        ApplyModifiers(context, modifiers, BuffModifierStage.Apply);

        instance.ResolvedDuration = context.Duration;
        instance.ResolvedTickInterval = context.TickInterval;
        instance.ResolvedMaxStacks = context.MaxStacks;
    }

    public static float ResolveEffectValue(BuffInstance instance, float baseValue)
    {
        if (instance == null || instance.Data?.buffInfo == null)
        {
            return baseValue;
        }

        BuffModifierContext context = new(instance)
        {
            Stage = BuffModifierStage.EffectValue,
            EffectValue = baseValue,
        };

        List<BuffModifier> modifiers = CollectModifiers(context);
        ApplyModifiers(context, modifiers, BuffModifierStage.EffectValue);
        return context.EffectValue;
    }

    public static float ResolveTickValue(BuffInstance instance, float baseValue, bool isHealingTick = false)
    {
        if (instance == null || instance.Data?.buffInfo == null)
        {
            return baseValue;
        }

        BuffModifierContext context = new(instance)
        {
            Stage = BuffModifierStage.TickValue,
            TickValue = baseValue,
            IsHealingTick = isHealingTick,
        };

        List<BuffModifier> modifiers = CollectModifiers(context);
        ApplyModifiers(context, modifiers, BuffModifierStage.TickValue);
        return context.TickValue;
    }

    static List<BuffModifier> CollectModifiers(BuffModifierContext context)
    {
        List<BuffModifier> result = new();
        HashSet<object> addedOwners = new();

        AddOwnerModifiers(result, addedOwners, UnitCoreModifiers.GlobalOwner);
        AddOwnerModifiers(result, addedOwners, context.Owner);
        AddOwnerModifiers(result, addedOwners, context.Caster);
        AddOwnerModifiers(result, addedOwners, context.BuffInstance);

        AddBuffModifiers(result, addedOwners, context.Owner?.BuffController);
        AddBuffModifiers(result, addedOwners, context.Caster?.BuffController);

        result.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return result;
    }

    static void AddBuffModifiers(
        List<BuffModifier> result,
        HashSet<object> addedOwners,
        BuffController buffController)
    {
        if (buffController == null)
        {
            return;
        }

        List<BuffInstance> activeBuffs = buffController.ActiveBuffs;
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            AddOwnerModifiers(result, addedOwners, activeBuffs[i]);
        }
    }

    static void AddOwnerModifiers(
        List<BuffModifier> result,
        HashSet<object> addedOwners,
        object owner)
    {
        if (owner == null || !addedOwners.Add(owner))
        {
            return;
        }

        if (!owner.TryGetBuffModifierController(out BuffModifierController controller)
            || controller == null
            || !controller.HasAny())
        {
            return;
        }

        IReadOnlyList<BuffModifier> modifiers = controller.Modifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            BuffModifier modifier = modifiers[i];
            if (modifier != null)
            {
                result.Add(modifier);
            }
        }
    }

    static void ApplyModifiers(
        BuffModifierContext context,
        List<BuffModifier> modifiers,
        BuffModifierStage stage)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            BuffModifier modifier = modifiers[i];
            if (modifier == null
                || !modifier.IsMatchStage(stage)
                || !modifier.IsMatch(context))
            {
                continue;
            }

            modifier.Apply(context);
        }
    }
}
