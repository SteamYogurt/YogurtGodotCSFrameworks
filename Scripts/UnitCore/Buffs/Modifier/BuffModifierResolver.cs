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

        using (BuffModifierCollectSession collectSession = BuffModifierCollectSession.Begin())
        {
            CollectModifiers(context, collectSession);
            ApplyModifiers(context, collectSession, BuffModifierStage.Apply);
        }

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

        using (BuffModifierCollectSession collectSession = BuffModifierCollectSession.Begin())
        {
            CollectModifiers(context, collectSession);
            ApplyModifiers(context, collectSession, BuffModifierStage.EffectValue);
        }

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

        using (BuffModifierCollectSession collectSession = BuffModifierCollectSession.Begin())
        {
            CollectModifiers(context, collectSession);
            ApplyModifiers(context, collectSession, BuffModifierStage.TickValue);
        }

        return context.TickValue;
    }

    static void CollectModifiers(BuffModifierContext context, BuffModifierCollectSession session)
    {
        session.AddOwnerModifiers(UnitCoreModifiers.GlobalOwner);
        session.AddOwnerModifiers(context.Owner);
        session.AddOwnerModifiers(context.Caster);
        session.AddOwnerModifiers(context.BuffInstance);
        session.AddBuffControllerModifiers(context.Owner?.BuffController);
        session.AddBuffControllerModifiers(context.Caster?.BuffController);
    }

    static void ApplyModifiers(
        BuffModifierContext context,
        BuffModifierCollectSession session,
        BuffModifierStage stage)
    {
        List<BuffModifier> modifiers = session.GetStageList(stage);
        if (modifiers == null || modifiers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            BuffModifier modifier = modifiers[i];
            if (modifier == null || !modifier.IsMatch(context))
            {
                continue;
            }

            modifier.Apply(context);
        }
    }
}
