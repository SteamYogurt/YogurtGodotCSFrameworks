using System;
using System.Collections.Generic;
using Godot;

public sealed class PromotionEffectHandle : IDisposable
{
	public static PromotionEffectHandle Empty { get; } = new(isEmpty: true);

	readonly List<IDisposable> disposables = new();
	readonly List<Action> cleanups = new();
	readonly HashSet<object> onceKeys = new();
	readonly bool isEmpty;
	bool disposed;

	public PromotionEffectHandle()
		: this(isEmpty: false)
	{
	}

	PromotionEffectHandle(bool isEmpty)
	{
		this.isEmpty = isEmpty;
	}

	public void AddSubscription(IDisposable subscription)
	{
		if (isEmpty || subscription == null || disposed)
		{
			return;
		}

		disposables.Add(subscription);
	}

	public void AddCleanup(Action cleanup)
	{
		if (isEmpty || cleanup == null || disposed)
		{
			return;
		}

		cleanups.Add(cleanup);
	}

	/// <summary>
	/// Registers a cleanup at most once for the given key (e.g. unit + source pair).
	/// </summary>
	public void AddCleanupOnce(object key, Action cleanup)
	{
		if (isEmpty || cleanup == null || disposed || key == null)
		{
			return;
		}

		if (!onceKeys.Add(key))
		{
			return;
		}

		cleanups.Add(cleanup);
	}

	public void Dispose()
	{
		if (isEmpty || disposed)
		{
			return;
		}

		disposed = true;

		for (int i = disposables.Count - 1; i >= 0; i--)
		{
			disposables[i]?.Dispose();
		}
		disposables.Clear();

		for (int i = cleanups.Count - 1; i >= 0; i--)
		{
			try
			{
				cleanups[i]?.Invoke();
			}
			catch (Exception e)
			{
				GD.PrintErr($"Promotion effect cleanup failed: {e}");
			}
		}
		cleanups.Clear();
		onceKeys.Clear();
	}
}
