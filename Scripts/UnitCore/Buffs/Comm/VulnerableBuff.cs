using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class VulnerableBuff : Buff
{
    [Export] public float DamageMultiplierDelta = 0.2f;
    [Export] public float ExtraDamageMultiplierDeltaPerStack = 0f;

    public override void OnEnter(BuffInstance instance)
    {
        ApplyIncomingDamageModifier(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        instance.CleanUpDamageModifiers();
        ApplyIncomingDamageModifier(instance);
    }

    void ApplyIncomingDamageModifier(BuffInstance instance)
    {
        float value = GetDamageMultiplierDelta(instance.Stacks);
        value = instance.ResolveEffectValue(value);
        if (Mathf.IsZeroApprox(value))
        {
            return;
        }

        instance.AddDamageModifier(new DamageModifier
        {
            ApplyStage = DamageResolveStage.Incoming,
            incomingDamageMultiplierDelta = value,
        });
    }

    float GetDamageMultiplierDelta(int stacks)
    {
        int extraStacks = stacks - 1;
        if (extraStacks < 0)
        {
            extraStacks = 0;
        }

        return DamageMultiplierDelta + ExtraDamageMultiplierDeltaPerStack * extraStacks;
    }

    public override string GetBuffDes()
    {
        if (buffInfo != null && buffInfo.overrideBuffDes && !string.IsNullOrEmpty(buffInfo.buffDes))
        {
            return Tr(buffInfo.buffDes);
        }

        var lines = new List<string>();

        string valueText = FormatPercent(DamageMultiplierDelta);
        lines.Add(string.Format("{0}+{1}", Tr("承受伤害"), valueText));

        if (!Mathf.IsZeroApprox(ExtraDamageMultiplierDeltaPerStack))
        {
            string extraText = FormatPercent(Mathf.Abs(ExtraDamageMultiplierDeltaPerStack));
            string sign = ExtraDamageMultiplierDeltaPerStack >= 0f ? "+" : "-";
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