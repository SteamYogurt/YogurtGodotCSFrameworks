using System;
using System.Collections.Generic;

internal sealed class BuffModifierCollectSession : IDisposable
{
    const int StageBucketCount = 3;

    static readonly List<BuffModifierCollectSession> SessionStack = new();
    static int depth;

    readonly HashSet<object> addedOwners;
    readonly List<BuffModifier>[] byStage;

    BuffModifierCollectSession(HashSet<object> owners, List<BuffModifier>[] stageBuckets)
    {
        addedOwners = owners;
        byStage = stageBuckets;
    }

    public static BuffModifierCollectSession Begin()
    {
        if (depth >= SessionStack.Count)
        {
            var stageBuckets = new List<BuffModifier>[StageBucketCount];
            for (int i = 0; i < StageBucketCount; i++)
            {
                stageBuckets[i] = new List<BuffModifier>();
            }

            SessionStack.Add(new BuffModifierCollectSession(
                new HashSet<object>(),
                stageBuckets));
        }

        BuffModifierCollectSession session = SessionStack[depth];
        session.Clear();
        depth++;
        return session;
    }

    static int StageToBucketIndex(BuffModifierStage stage)
    {
        if (stage == BuffModifierStage.Apply)
        {
            return 0;
        }

        if (stage == BuffModifierStage.EffectValue)
        {
            return 1;
        }

        if (stage == BuffModifierStage.TickValue)
        {
            return 2;
        }

        return -1;
    }

    public List<BuffModifier> GetStageList(BuffModifierStage stage)
    {
        int index = StageToBucketIndex(stage);
        if (index < 0)
        {
            return null;
        }

        return byStage[index];
    }

    public void AddModifier(BuffModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        BuffModifierStage flags = modifier.ApplyStage;
        if ((flags & BuffModifierStage.Apply) != 0)
        {
            InsertIntoBucket(0, modifier);
        }

        if ((flags & BuffModifierStage.EffectValue) != 0)
        {
            InsertIntoBucket(1, modifier);
        }

        if ((flags & BuffModifierStage.TickValue) != 0)
        {
            InsertIntoBucket(2, modifier);
        }
    }

    void InsertIntoBucket(int bucketIndex, BuffModifier modifier)
    {
        ModifierCollectionUtil.InsertSortedByPriority(
            byStage[bucketIndex],
            modifier,
            static m => m.Priority);
    }

    public void AddOwnerModifiers(object owner)
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
