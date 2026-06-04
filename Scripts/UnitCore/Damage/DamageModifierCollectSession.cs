using System;
using System.Collections.Generic;

internal sealed class DamageModifierCollectSession : IDisposable
{
    const int StageBucketCount = 8;

    static readonly List<DamageModifierCollectSession> SessionStack = new();
    static int depth;

    readonly HashSet<object> addedOwners;
    readonly List<DamageModifier>[] byStage;

    DamageModifierCollectSession(HashSet<object> owners, List<DamageModifier>[] stageBuckets)
    {
        addedOwners = owners;
        byStage = stageBuckets;
    }

    public static DamageModifierCollectSession Begin()
    {
        if (depth >= SessionStack.Count)
        {
            var stageBuckets = new List<DamageModifier>[StageBucketCount];
            for (int i = 0; i < StageBucketCount; i++)
            {
                stageBuckets[i] = new List<DamageModifier>();
            }

            SessionStack.Add(new DamageModifierCollectSession(
                new HashSet<object>(),
                stageBuckets));
        }

        DamageModifierCollectSession session = SessionStack[depth];
        session.Clear();
        depth++;
        return session;
    }

    public List<DamageModifier> GetStageList(DamageResolveStage stage)
    {
        int index = (int)stage;
        if (index < 0 || index >= StageBucketCount)
        {
            return null;
        }

        return byStage[index];
    }

    public void AddModifier(DamageModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        List<DamageModifier> bucket = GetStageList(modifier.ApplyStage);
        if (bucket == null)
        {
            return;
        }

        ModifierCollectionUtil.InsertSortedByPriority(
            bucket,
            modifier,
            static m => m.Priority);
    }

    public void AddOwnerModifiers(object owner)
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
            AddModifier(modifiers[i]);
        }
    }

    public void AddBuffControllerModifiers(BuffController buffController)
    {
        if (buffController == null)
        {
            return;
        }

        List<BuffInstance> activeBuffs = buffController.ActiveBuffs;
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            AddOwnerModifiers(activeBuffs[i]);
        }
    }

    void Clear()
    {
        addedOwners.Clear();
        for (int i = 0; i < StageBucketCount; i++)
        {
            byStage[i].Clear();
        }
    }

    public void Dispose()
    {
        if (depth > 0)
        {
            depth--;
        }
    }

    internal static void ResetScratch()
    {
        depth = 0;
        for (int i = 0; i < SessionStack.Count; i++)
        {
            SessionStack[i].Clear();
        }
    }
}
