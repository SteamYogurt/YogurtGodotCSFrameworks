using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Per-match ownership root for promotion bus, services, bridge, and active promotions.
/// Created by <see cref="CombatRuntime.BeginMatch"/>; dispose via <see cref="CombatRuntime.EndMatch"/>.
/// </summary>
public sealed class MatchContext : IDisposable
{
	readonly List<PromotionInstance> activeInstances = new();
	PromotionUnitCoreBridge bridge;
	bool activated;
	bool disposed;

	public object GlobalModifierOwner { get; } = new();

	public PromotionEventBus EventBus { get; } = new();

	public Func<IEnumerable<IUnit>> ActiveUnits { get; set; }

	public Action<string, Vector3> SpawnEffectAtPosition { get; set; }

	public IReadOnlyList<PromotionInstance> ActivePromotions => activeInstances;

	public bool IsActive => activated && !disposed;

	internal MatchContext(CombatRuntimeMatchOptions options)
	{
		if (options == null)
		{
			return;
		}

		ActiveUnits = options.ActiveUnits;
		SpawnEffectAtPosition = options.SpawnEffectAtPosition;
	}

	internal void Activate()
	{
		if (activated || disposed)
		{
			return;
		}

		bridge = new PromotionUnitCoreBridge(this);
		bridge.Register();
		activated = true;
	}

	public PromotionInstance Fetch(Promotion promotion)
	{
		EnsureActive();
		if (promotion == null)
		{
			return null;
		}

		PromotionInstance instance = new PromotionInstance(promotion);
		instance.Activate(this);
		activeInstances.Add(instance);

		using (ConditionContextScope scope = ConditionContext.RentScope(this))
		{
			scope.Context.Set(ConditionSubjectKey.Source, instance);
			EventBus.Raise(PromotionEventType.OnAcquire, scope.Context);
		}

		return instance;
	}

	public bool Revert(PromotionInstance instance)
	{
		if (instance == null)
		{
			return false;
		}

		instance.Deactivate();
		return activeInstances.Remove(instance);
	}

	public void NotifyUnitSpawned(IUnit unit)
	{
		if (unit == null || !IsActive)
		{
			return;
		}

		using (ConditionContextScope scope = ConditionContext.RentScope(this))
		{
			scope.Context.Set(ConditionSubjectKey.Subject, unit);
			EventBus.Raise(PromotionEventType.UnitSpawned, scope.Context);
		}
	}

	public ConditionContextScope OpenContext() => ConditionContext.RentScope(this);

	public void ForEachActiveUnit(Action<IUnit> action, ObjectFilter unitFilter = null, bool skipInvalid = true)
	{
		if (action == null)
		{
			return;
		}

		IEnumerable<IUnit> units = ActiveUnits?.Invoke();
		if (units == null)
		{
			return;
		}

		foreach (IUnit unit in units)
		{
			if (unit == null)
			{
				continue;
			}

			if (skipInvalid && unit is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
			{
				continue;
			}

			if (unitFilter != null && !unitFilter.IsMatch(unit))
			{
				continue;
			}

			action(unit);
		}
	}

	void EnsureActive()
	{
		if (!IsActive)
		{
			throw new InvalidOperationException(
				"MatchContext is not active. Call CombatRuntime.BeginMatch before using promotions.");
		}
	}

	void ClearPromotions()
	{
		for (int i = activeInstances.Count - 1; i >= 0; i--)
		{
			activeInstances[i]?.Deactivate();
		}

		activeInstances.Clear();
		EventBus.ClearAll();
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		ClearPromotions();
		bridge?.Dispose();
		bridge = null;
		activated = false;

		ActiveUnits = null;
		SpawnEffectAtPosition = null;
	}
}
