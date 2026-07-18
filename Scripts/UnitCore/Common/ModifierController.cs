using System;
using System.Collections.Generic;

/// <summary>
/// Shared sorted-list controller for runtime modifiers attached to an owner.
/// </summary>
public class ModifierController<TModifier> where TModifier : class
{
	readonly List<TModifier> modifiers = new();
	readonly Func<TModifier, int> getPriority;
	readonly Func<TModifier, object> getRuntimeSource;

	public IReadOnlyList<TModifier> Modifiers => modifiers;

	public ModifierController(
		Func<TModifier, int> getPriority,
		Func<TModifier, object> getRuntimeSource)
	{
		this.getPriority = getPriority ?? throw new ArgumentNullException(nameof(getPriority));
		this.getRuntimeSource = getRuntimeSource ?? throw new ArgumentNullException(nameof(getRuntimeSource));
	}

	public void AddModifier(TModifier modifier)
	{
		if (modifier == null)
		{
			return;
		}

		ModifierCollectionUtil.InsertSortedByPriority(modifiers, modifier, getPriority);
	}

	public bool RemoveModifier(TModifier modifier)
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

		modifiers.RemoveAll(m => ReferenceEquals(getRuntimeSource(m), source));
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
