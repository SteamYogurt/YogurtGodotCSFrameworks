using System.Collections.Generic;
using Godot;

public static class DamageResolver
{
    public static void Execute(DamageContext ctx, bool invokeCallbacks = true)
    {
        if (!CanExecute(ctx))
        {
            return;
        }

        ctx.FinalPhysicalDamage = 0f;
        ctx.FinalMagicalDamage = 0f;
        ctx.FinalRealDamage = 0f;
        ctx.ClearAppliedModifiers();

        ctx.Stage = DamageResolveStage.BeforeEvents;
        if (invokeCallbacks)
        {
            InvokeBeforeCallbacks(ctx);
            if (!CanExecute(ctx))
            {
                return;
            }
        }

        List<DamageModifier> modifiers = CollectModifiers(ctx);

        ctx.Stage = DamageResolveStage.Outgoing;
        ApplyModifiers(ctx, modifiers, DamageResolveStage.Outgoing);
        float outgoingMultiplier = Mathf.Max(0f, ctx.OutgoingDamageMultiplier);
        float outgoingPhysicalDamage = Mathf.Max(0f, ctx.RawPhysicalDamage * outgoingMultiplier);
        float outgoingMagicalDamage = Mathf.Max(0f, ctx.RawMagicalDamage * outgoingMultiplier);
        float outgoingRealDamage = Mathf.Max(0f, ctx.RawRealDamage * outgoingMultiplier);

        ctx.Stage = DamageResolveStage.Critical;
        ApplyModifiers(ctx, modifiers, DamageResolveStage.Critical);
        ResolveCritical(ctx);
        float critMultiplier = ctx.IsCrit ? Mathf.Max(1f, ctx.CritMultiplier) : 1f;

        ctx.Stage = DamageResolveStage.Defense;
        ApplyModifiers(ctx, modifiers, DamageResolveStage.Defense);
        float effectivePhysDefense = ctx.Target.PhysicalDefense;
        float effectiveMagDefense = ctx.Target.MagicalDefense;

        float physDefenseMultiplier = 1f
            - (Mathf.Max(0f, effectivePhysDefense) / (25f + Mathf.Max(0f, effectivePhysDefense)));
        float magDefenseMultiplier = 1f
            - (Mathf.Max(0f, effectiveMagDefense) / (25f + Mathf.Max(0f, effectiveMagDefense)));

        float physicalDamageAfterDefense = outgoingPhysicalDamage * physDefenseMultiplier * critMultiplier;
        float magicalDamageAfterDefense = outgoingMagicalDamage * magDefenseMultiplier * critMultiplier;
        float realDamageAfterDefense = outgoingRealDamage * critMultiplier;

        ctx.Stage = DamageResolveStage.Incoming;
        ApplyModifiers(ctx, modifiers, DamageResolveStage.Incoming);
        float incomingMultiplier = Mathf.Max(0f, ctx.IncomingDamageMultiplier);
        physicalDamageAfterDefense *= incomingMultiplier;
        magicalDamageAfterDefense *= incomingMultiplier;
        realDamageAfterDefense *= incomingMultiplier;

        ctx.Stage = DamageResolveStage.Final;
        ApplyModifiers(ctx, modifiers, DamageResolveStage.Final);
        float finalMultiplier = Mathf.Max(0f, ctx.FinalDamageMultiplier);
        ctx.FinalPhysicalDamage = physicalDamageAfterDefense * finalMultiplier;
        ctx.FinalMagicalDamage = magicalDamageAfterDefense * finalMultiplier;
        ctx.FinalRealDamage = realDamageAfterDefense * finalMultiplier;

        IUnitExt.ApplyDamageImpact(ctx);

        if (invokeCallbacks)
        {
            InvokeAfterCallbacks(ctx);
        }

        if (ctx.ShowText)
        {
            DamageFeedback.SpawnText?.Invoke(ctx);
        }

        ctx.Stage = DamageResolveStage.Applied;
    }

    public static void ResolveCritical(DamageContext ctx)
    {
        if (ctx == null || ctx.IsCrit)
        {
            return;
        }

        float critChance = Mathf.Clamp(ctx.CritChance, 0f, 1f);
        ctx.IsCrit = ctx.RandomValue < critChance;
    }

    static bool CanExecute(DamageContext ctx)
    {
        return ctx != null
            && ctx.Target != null
            && ctx.Target.IsAlive;
    }

    static void InvokeBeforeCallbacks(DamageContext ctx)
    {
        ctx.Attacker?.NotifyDealingDamage(ctx);
        ctx.Target.NotifyReceivingDamage(ctx);
        UnitCoreEvents.RaiseDealingDamage(ctx);
        UnitCoreEvents.RaiseReceivingDamage(ctx);
    }

    static void InvokeAfterCallbacks(DamageContext ctx)
    {
        ctx.Attacker?.NotifyDealtDamage(ctx);
        ctx.Target.NotifyReceivedDamage(ctx);
        UnitCoreEvents.RaiseDealtDamage(ctx);
        UnitCoreEvents.RaiseReceivedDamage(ctx);
    }

    static List<DamageModifier> CollectModifiers(DamageContext ctx)
    {
        List<DamageModifier> result = new();
        HashSet<object> addedOwners = new();

        AddOwnerModifiers(result, addedOwners, UnitCoreModifiers.GlobalOwner);
        AddOwnerModifiers(result, addedOwners, ctx.Attacker);
        AddOwnerModifiers(result, addedOwners, ctx.Target);
        AddOwnerModifiers(result, addedOwners, ctx.Source?.SourceObject);

        AddBuffModifiers(result, addedOwners, ctx.Attacker?.BuffController);
        AddBuffModifiers(result, addedOwners, ctx.Target?.BuffController);

        result.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return result;
    }

    static void AddBuffModifiers(
        List<DamageModifier> result,
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
        List<DamageModifier> result,
        HashSet<object> addedOwners,
        object owner)
    {
        if (owner == null || !addedOwners.Add(owner))
        {
            return;
        }

        if (!owner.TryGetDamageModifierController(out DamageModifierController controller)
            || controller == null
            || !controller.HasAny())
        {
            return;
        }

        IReadOnlyList<DamageModifier> modifiers = controller.Modifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            DamageModifier modifier = modifiers[i];
            if (modifier != null)
            {
                result.Add(modifier);
            }
        }
    }

    static void ApplyModifiers(
        DamageContext ctx,
        List<DamageModifier> modifiers,
        DamageResolveStage stage)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            DamageModifier modifier = modifiers[i];
            if (modifier == null
                || !modifier.IsMatchStage(stage)
                || !modifier.IsMatch(ctx))
            {
                continue;
            }

            modifier.Apply(ctx);
            ctx.RecordAppliedModifier(modifier);
        }
    }
}
