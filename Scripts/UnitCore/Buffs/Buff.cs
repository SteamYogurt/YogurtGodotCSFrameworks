using Godot;

[GlobalClass]
public partial class Buff : Resource
{
	[Export]
	public BuffInfo buffInfo;

	[Export]
	public Godot.Collections.Array<BuffEffect> Effects = new();

	public static ulong numBId;

	public StringName BuffID
	{
		get
		{
			if (buffId == null)
			{
				buffId = (++numBId).ToString();
			}
			return buffId;
		}
	}

	StringName buffId;

	/// <summary>Called by <see cref="BuffExt"/> when loading a catalog asset.</summary>
	public void BindCatalogId(StringName id)
	{
		buffId = id;
	}

	public Buff GetCopy()
	{
		var copy = Duplicate() as Buff;
		copy.buffId = BuffID;
		return copy;
	}

	public virtual void OnEnter(BuffInstance instance)
	{
		ForEachEffect(effect => effect.OnEnter(instance));
	}

	public virtual void OnTick(BuffInstance instance, float delta)
	{
		ForEachEffect(effect => effect.OnTick(instance, delta));
	}

	public virtual void OnRefresh(BuffInstance instance, int stacks = 1)
	{
		instance.RefreshResolvedBuffValues();
		instance.DurationTimer = instance.GetResolvedDuration();

		int maxStacks = instance.GetResolvedMaxStacks();
		if (buffInfo.infiniteStacks || instance.Stacks < maxStacks)
		{
			instance.Stacks += stacks;
			if (!buffInfo.infiniteStacks && instance.Stacks > maxStacks)
			{
				instance.Stacks = maxStacks;
			}

			OnStackChanged(instance);
		}
	}

	public virtual void OnStackChanged(BuffInstance instance)
	{
		// Central cleanup so multiple effects can safely re-apply without wiping each other.
		instance.CleanUpModifiers();
		instance.CleanUpDamageModifiers();
		ForEachEffect(effect => effect.OnStackChanged(instance));
	}

	public virtual void OnExit(BuffInstance instance)
	{
		ForEachEffect(effect => effect.OnExit(instance));
	}

	void ForEachEffect(System.Action<BuffEffect> action)
	{
		if (Effects == null || action == null)
		{
			return;
		}

		for (int i = 0; i < Effects.Count; i++)
		{
			BuffEffect effect = Effects[i];
			if (effect != null)
			{
				action(effect);
			}
		}
	}

	public bool TryGetEffect<T>(out T effect) where T : BuffEffect
	{
		effect = null;
		if (Effects == null)
		{
			return false;
		}

		for (int i = 0; i < Effects.Count; i++)
		{
			if (Effects[i] is T typed)
			{
				effect = typed;
				return true;
			}
		}

		return false;
	}
}
