using System;
using System.Collections.Generic;

/// <summary>
/// Event/condition bag. Rent via <see cref="Rent"/> / <see cref="RentScope"/> for sync Raise only —
/// do not store past the Raise callback stack.
/// </summary>
public class ConditionContext
{
	static readonly Stack<ConditionContext> Pool = new();

	readonly Dictionary<ConditionSubjectKey, object> data = new();
	readonly Dictionary<string, object> customData = new();

	/// <summary>Active match that raised or owns this context.</summary>
	public MatchContext Match { get; set; }

	/// <summary>
	/// Effect handle for the promotion effect currently handling this event.
	/// Actions register reversible side effects here.
	/// </summary>
	public PromotionEffectHandle EffectHandle { get; set; }

	public static ConditionContext Rent(MatchContext match = null)
	{
		ConditionContext context = Pool.Count > 0 ? Pool.Pop() : new ConditionContext();
		context.Reset();
		context.Match = match;
		return context;
	}

	public static ConditionContextScope RentScope(MatchContext match = null) =>
		new ConditionContextScope(Rent(match));

	public static void Return(ConditionContext context)
	{
		if (context == null)
		{
			return;
		}

		context.Reset();
		Pool.Push(context);
	}

	public static void ClearPool()
	{
		Pool.Clear();
	}

	public void Reset()
	{
		data.Clear();
		customData.Clear();
		Match = null;
		EffectHandle = null;
	}

	public ConditionContext Set(ConditionSubjectKey key, object value)
	{
		if (key == ConditionSubjectKey.None)
		{
			return this;
		}

		data[key] = value;
		return this;
	}

	public T Get<T>(ConditionSubjectKey key, T fallback = default)
	{
		if (data.TryGetValue(key, out object value) && value is T typed)
		{
			return typed;
		}

		return fallback;
	}

	public object GetObject(ConditionSubjectKey key) =>
		data.TryGetValue(key, out object value) ? value : null;

	public bool Has(ConditionSubjectKey key) => data.ContainsKey(key);

	public ConditionContext SetCustom(string key, object value)
	{
		if (string.IsNullOrEmpty(key))
		{
			return this;
		}

		customData[key] = value;
		return this;
	}

	public T GetCustom<T>(string key, T fallback = default)
	{
		if (customData.TryGetValue(key, out object value) && value is T typed)
		{
			return typed;
		}

		return fallback;
	}

	public bool HasCustom(string key) => !string.IsNullOrEmpty(key) && customData.ContainsKey(key);
}

/// <summary>
/// Returns a rented <see cref="ConditionContext"/> when disposed (after sync Raise).
/// </summary>
public struct ConditionContextScope : IDisposable
{
	ConditionContext context;
	bool returned;

	public ConditionContext Context => context;

	public ConditionContextScope(ConditionContext context)
	{
		this.context = context;
		returned = false;
	}

	public void Dispose()
	{
		if (returned || context == null)
		{
			return;
		}

		returned = true;
		ConditionContext.Return(context);
		context = null;
	}
}
