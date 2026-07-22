using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class SlowBuffEffect : BuffEffect
{
    [Export] public float SlowAmount = 0.2f;
    [Export] public float SlowExtraAmountPerStack = 0.1f;

    public float GetSlowAmount(BuffInstance instance)
    {
        if (instance == null)
        {
            return SlowAmount;
        }

        float value = instance.ResolveStackedValue(SlowAmount, SlowExtraAmountPerStack);
        return instance.ResolveEffectValue(value);
    }

    public override void OnEnter(BuffInstance instance)
    {
        instance.AddModifier(UnitStatType.MoveSpeed, -GetSlowAmount(instance), StatMode.Multiplicative);
        RefreshSlowGroup(instance.Owner);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        instance.AddModifier(UnitStatType.MoveSpeed, -GetSlowAmount(instance), StatMode.Multiplicative);
        RefreshSlowGroup(instance.Owner);
    }

    public override void OnExit(BuffInstance instance)
    {
        RefreshSlowGroup(instance.Owner, excluding: instance);
    }

    public static void RefreshSlowGroup(IUnit owner, BuffInstance excluding = null)
    {
        if (owner?.BuffController == null)
        {
            return;
        }

        List<(BuffInstance instance, SlowBuffEffect effect, float amount)> slows = new();
        List<BuffInstance> activeBuffs = owner.BuffController.ActiveBuffs;
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            BuffInstance buff = activeBuffs[i];
            if (buff == null || ReferenceEquals(buff, excluding))
            {
                continue;
            }

            if (buff.Data == null || !buff.Data.TryGetEffect(out SlowBuffEffect effect))
            {
                continue;
            }

            slows.Add((buff, effect, effect.GetSlowAmount(buff)));
        }

        if (slows.Count == 0)
        {
            owner.NotifyStatChanged(UnitStatType.MoveSpeed, 0);
            return;
        }

        BuffInstance strongest = slows[0].instance;
        float strongestAmount = slows[0].amount;
        for (int i = 1; i < slows.Count; i++)
        {
            if (slows[i].amount > strongestAmount)
            {
                strongest = slows[i].instance;
                strongestAmount = slows[i].amount;
            }
        }

        for (int i = 0; i < slows.Count; i++)
        {
            bool shouldBeActive = ReferenceEquals(slows[i].instance, strongest);
            List<(UnitStatType type, StatModifier mod)> modifiers = slows[i].instance.AppliedModifiers;
            for (int m = 0; m < modifiers.Count; m++)
            {
                if (modifiers[m].type != UnitStatType.MoveSpeed)
                {
                    continue;
                }

                StatModifier modi = modifiers[m].mod;
                if (modi.Active != shouldBeActive)
                {
                    modi.Active = shouldBeActive;
                }
            }
        }

        owner.NotifyStatChanged(UnitStatType.MoveSpeed, 0);
    }
}
