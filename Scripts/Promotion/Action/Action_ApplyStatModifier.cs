using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Action_ApplyStatModifier : PromotionAction
{
	[Export] public ConditionSubjectKey targetKey = ConditionSubjectKey.Target;
	[Export] public Array<StatChangeConfig> statChangeConfigs = new();

	protected override void Execute(ConditionContext context)
	{
		if (context == null || context.GetObject(targetKey) is not IUnit unit)
		{
			return;
		}

		// Prefer promotion instance as source so Deactivate removes all grants from this promotion.
		object source = context.GetObject(ConditionSubjectKey.Source) ?? this;
		RemoveConfigs(unit, source, statChangeConfigs);
		StatChangeConfig.ApplyConfigs(unit, statChangeConfigs, source);

		if (context.EffectHandle == null || statChangeConfigs == null)
		{
			return;
		}

		Array<StatChangeConfig> configs = statChangeConfigs;
		context.EffectHandle.AddCleanupOnce(
			(unit, source, this),
			() => RemoveConfigs(unit, source, configs));
	}

	static void RemoveConfigs(IUnit unit, object source, Array<StatChangeConfig> configs)
	{
		if (!PromotionUnitEffectHelper.IsUnitValid(unit) || configs == null)
		{
			return;
		}

		for (int i = 0; i < configs.Count; i++)
		{
			StatChangeConfig config = configs[i];
			if (config == null)
			{
				continue;
			}

			unit.RemoveStatModifier(config.StatType, source);
		}
	}
}
