using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ShieldBreakBuff : Buff
{
    [Export] public float ShieldDamageMultiplierDelta = 0.5f;
    [Export] public float ExtraShieldDamageMultiplierDeltaPerStack = 0f;

    public override void OnEnter(BuffInstance instance)
    {
        ApplyShieldDamageModifier(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        instance.CleanUpDamageModifiers();
        ApplyShieldDamageModifier(instance);
    }

    void ApplyShieldDamageModifier(BuffInstance instance)
    {
        float value = GetShieldDamageMultiplierDelta(instance.Stacks);
        value = instance.ResolveEffectValue(value);
        if (Mathf.IsZeroApprox(value))
        {
            return;
        }

        instance.AddDamageModifier(new DamageModifier
        {
            ApplyStage = DamageResolveStage.Final,
            shieldDamageMultiplierDelta = value,
        });
    }

    float GetShieldDamageMultiplierDelta(int stacks)
    {
        int extraStacks = stacks - 1;
        if (extraStacks < 0)
        {
            extraStacks = 0;
        }

        return ShieldDamageMultiplierDelta + ExtraShieldDamageMultiplierDeltaPerStack * extraStacks;
    }

    public override string GetBuffDes()
    {
        if (buffInfo != null && buffInfo.overrideBuffDes && !string.IsNullOrEmpty(buffInfo.buffDes))
        {
            return Tr(buffInfo.buffDes);
        }

        var lines = new List<string>();

        string valueText = FormatPercent(ShieldDamageMultiplierDelta);
        lines.Add(string.Format("{0}+{1}", Tr("对护盾伤害"), valueText));

        if (!Mathf.IsZeroApprox(ExtraShieldDamageMultiplierDeltaPerStack))
        {
            string extraText = FormatPercent(Mathf.Abs(ExtraShieldDamageMultiplierDeltaPerStack));
            string sign = ExtraShieldDamageMultiplierDeltaPerStack >= 0f ? "+" : "-";
            lines.Add(string.Format("{0}{1}{2}", Tr("每层额外"), sign, extraText));
        }

        string stackStr = GetStackStr();
        if (!string.IsNullOrEmpty(stackStr))
        {
            lines.Add(stackStr);
        }

        return string.Join("\n", lines);
    }

    string FormatPercent(float value)
    {
        float percent = value * 100f;
        if (Mathf.Abs(percent) < 10f)
        {
            return percent.ToString("0.#") + "%";
        }

        return percent.ToString("0.##") + "%";
    }
}