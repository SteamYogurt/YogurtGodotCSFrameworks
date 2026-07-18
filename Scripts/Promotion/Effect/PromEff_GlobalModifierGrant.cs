using Godot;
using Godot.Collections;

/// <summary>
/// Grants global DamageModifier / BuffModifier on the match GlobalModifierOwner while active.
/// </summary>
[GlobalClass]
public partial class PromEff_GlobalModifierGrant : PromotionEffect
{
	[Export] public Array<DamageModifier> damageModifiers = new();
	[Export] public Array<BuffModifier> buffModifiers = new();

	MatchContext match;
	object modifierOwner;

	public override PromotionEffectHandle Activate(PromotionEffectContext context)
	{
		match = context.Match;
		modifierOwner = context.ModifierOwner;
		PromotionEffectHandle handle = new PromotionEffectHandle();

		ApplyModifiers();
		handle.AddCleanup(RemoveModifiers);
		if (HasBuffModifiers())
		{
			handle.AddCleanup(RefreshAllUnits);
		}

		return handle;
	}

	bool HasBuffModifiers() => buffModifiers != null && buffModifiers.Count > 0;

	void ApplyModifiers()
	{
		ApplyDamageModifiers();
		ApplyBuffModifiers();
		if (HasBuffModifiers())
		{
			RefreshAllUnits();
		}
	}

	void ApplyDamageModifiers()
	{
		DamageModifierController controller = modifierOwner.GetDamageModifierController();
		controller.RemoveAllFromSource(this);

		if (damageModifiers == null)
		{
			return;
		}

		foreach (DamageModifier damageModifier in damageModifiers)
		{
			if (damageModifier == null)
			{
				continue;
			}

			DamageModifier runtimeModifier = damageModifier.Duplicate() as DamageModifier ?? damageModifier;
			runtimeModifier.RuntimeSource = this;
			controller.AddModifier(runtimeModifier);
		}
	}

	void ApplyBuffModifiers()
	{
		BuffModifierController controller = modifierOwner.GetBuffModifierController();
		controller.RemoveAllFromSource(this);

		if (buffModifiers == null)
		{
			return;
		}

		foreach (BuffModifier buffModifier in buffModifiers)
		{
			if (buffModifier == null)
			{
				continue;
			}

			BuffModifier runtimeModifier = buffModifier.Duplicate() as BuffModifier ?? buffModifier;
			runtimeModifier.RuntimeSource = this;
			controller.AddModifier(runtimeModifier);
		}
	}

	void RemoveModifiers()
	{
		if (modifierOwner == null)
		{
			return;
		}

		modifierOwner.GetDamageModifierController().RemoveAllFromSource(this);
		modifierOwner.GetBuffModifierController().RemoveAllFromSource(this);
	}

	void RefreshAllUnits()
	{
		match?.ForEachActiveUnit(unit => unit.BuffController?.RefreshAllBuffModifierValues());
	}

	public override string GetDescription()
	{
		System.Collections.Generic.List<string> parts = new();

		string damageDescription = DamageModifier.GetDescription(damageModifiers);
		if (!string.IsNullOrEmpty(damageDescription))
		{
			parts.Add(damageDescription);
		}

		string buffDescription = BuffModifier.GetDescription(buffModifiers);
		if (!string.IsNullOrEmpty(buffDescription))
		{
			parts.Add(buffDescription);
		}

		if (parts.Count == 0)
		{
			return Tr("获得全局修正");
		}

		return string.Format(
			Tr("获得以下全局修正：\n{0}"),
			string.Join("\n", parts));
	}
}
