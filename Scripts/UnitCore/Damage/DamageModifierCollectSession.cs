using System;
using System.Collections.Generic;

internal sealed class DamageModifierCollectSession : IDisposable
{
	const int StageBucketCount = 8;

	static readonly ModifierCollectSessionStack<DamageModifierCollectSession> Stack = new(
		static () => new DamageModifierCollectSession(),
		static session => session.Clear());

	readonly HashSet<object> addedOwners = new();
	readonly List<DamageModifier>[] byStage;

	DamageModifierCollectSession()
	{
		byStage = new List<DamageModifier>[StageBucketCount];
		for (int i = 0; i < StageBucketCount; i++)
		{
			byStage[i] = new List<DamageModifier>();
		}
	}

	public static DamageModifierCollectSession Begin() => Stack.Begin();

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

	public void Dispose() => Stack.End();

	internal static void ResetScratch() => Stack.ResetScratch();
}
