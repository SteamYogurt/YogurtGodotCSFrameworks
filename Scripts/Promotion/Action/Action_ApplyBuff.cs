using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Action_ApplyBuff : PromotionAction
{
	[Export] public ConditionSubjectKey targetKey = ConditionSubjectKey.Target;
	[Export] public ConditionSubjectKey casterKey = ConditionSubjectKey.Attacker;
	[Export] public Buff buff;
	[Export(PropertyHint.Range, "1,999,1")] public int stacks = 1;

	/// <summary>
	/// When true, removing the promotion also removes this buff from the target (if still present).
	/// </summary>
	[Export] public bool bindToPromotionLifetime;

	protected override void Execute(ConditionContext context)
	{
		if (buff == null || context == null)
		{
			return;
		}

		if (context.GetObject(targetKey) is not IUnit unit || unit.BuffController == null)
		{
			return;
		}

		object caster = context.GetObject(casterKey);
		unit.BuffController.AddBuff(buff, caster, Mathf.Max(1, stacks));

		if (!bindToPromotionLifetime || context.EffectHandle == null || buff.BuffID == null)
		{
			return;
		}

		StringName buffId = buff.BuffID;
		BuffController controller = unit.BuffController;
		context.EffectHandle.AddCleanupOnce(
			(unit, buffId, this),
			() =>
			{
				if (controller == null)
				{
					return;
				}

				if (unit is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
				{
					return;
				}

				controller.RemoveBuffByID(buffId);
			});
	}
}
