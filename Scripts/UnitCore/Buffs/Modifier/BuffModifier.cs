using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BuffModifier : Resource
{
    [Export] public int Priority;
    [Export] public bool Enabled = true;
    [Export] public BuffModifierStage ApplyStage = BuffModifierStage.Apply;

    [ExportGroup("Filters")]
    [Export] public string RequiredBuffId;
    [Export] public BuffTag RequiredBuffTag = BuffTag.None;
    [Export] public BuffTag ExcludedBuffTag = BuffTag.None;
    [Export] public BuffModifierDebuffFilter DebuffFilter = BuffModifierDebuffFilter.Any;
    [Export] public BuffModifierTickTypeFilter TickTypeFilter = BuffModifierTickTypeFilter.Any;
    [Export] public BuffTag RequiredOwnerBuffTag = BuffTag.None;
    [Export] public BuffTag RequiredCasterBuffTag = BuffTag.None;
    [Export] public ObjectFilter ownerFilter;
    [Export] public ObjectFilter casterFilter;

    [ExportGroup("Apply Deltas")]
    [Export] public float durationDelta;
    [Export] public float durationMultiplierDelta;
    [Export] public float tickIntervalDelta;
    [Export] public float tickIntervalMultiplierDelta;
    [Export] public int maxStacksDelta;

    [ExportGroup("Value Deltas")]
    [Export] public float effectValueDelta;
    [Export] public float effectValueMultiplierDelta;
    [Export] public float tickValueDelta;
    [Export] public float tickValueMultiplierDelta;

    public object RuntimeSource { get; set; }

    public bool IsMatchStage(BuffModifierStage stage)
    {
        return (ApplyStage & stage) != 0;
    }

    public bool IsMatch(BuffModifierContext context)
    {
        if (!Enabled || context == null || context.Buff == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(RequiredBuffId)
            && context.Buff.BuffID.ToString() != RequiredBuffId)
        {
            return false;
        }

        if (RequiredBuffTag != BuffTag.None && !context.BuffTag.HasAll(RequiredBuffTag))
        {
            return false;
        }

        if (ExcludedBuffTag != BuffTag.None && context.BuffTag.HasAny(ExcludedBuffTag))
        {
            return false;
        }

        if (DebuffFilter == BuffModifierDebuffFilter.BuffOnly && context.IsDebuff)
        {
            return false;
        }

        if (DebuffFilter == BuffModifierDebuffFilter.DebuffOnly && !context.IsDebuff)
        {
            return false;
        }

        if (TickTypeFilter != BuffModifierTickTypeFilter.Any)
        {
            if ((context.Stage & BuffModifierStage.TickValue) == 0)
            {
                return false;
            }

            if (TickTypeFilter == BuffModifierTickTypeFilter.DamageOnly && context.IsHealingTick)
            {
                return false;
            }

            if (TickTypeFilter == BuffModifierTickTypeFilter.HealingOnly && !context.IsHealingTick)
            {
                return false;
            }
        }

        if (ownerFilter != null && !ownerFilter.IsMatch(context.Owner))
        {
            return false;
        }

        if (casterFilter != null && !casterFilter.IsMatch(context.Caster))
        {
            return false;
        }

        if (RequiredOwnerBuffTag != BuffTag.None)
        {
            if (context.Owner?.BuffController == null
                || !context.Owner.BuffController.HasBuffTag(RequiredOwnerBuffTag))
            {
                return false;
            }
        }

        if (RequiredCasterBuffTag != BuffTag.None)
        {
            if (context.Caster?.BuffController == null
                || !context.Caster.BuffController.HasBuffTag(RequiredCasterBuffTag))
            {
                return false;
            }
        }

        return true;
    }

    public void Apply(BuffModifierContext context)
    {
        if (context == null)
        {
            return;
        }

        switch (context.Stage)
        {
            case BuffModifierStage.Apply:
                ApplyToRuntime(context);
                break;

            case BuffModifierStage.EffectValue:
                context.EffectValue = context.EffectValue * (1f + effectValueMultiplierDelta)
                    + effectValueDelta;
                break;

            case BuffModifierStage.TickValue:
                context.TickValue = context.TickValue * (1f + tickValueMultiplierDelta)
                    + tickValueDelta;
                break;
        }
    }

    void ApplyToRuntime(BuffModifierContext context)
    {
        if (context.Duration >= 0f)
        {
            context.Duration = context.Duration * (1f + durationMultiplierDelta) + durationDelta;
            context.Duration = Mathf.Max(0f, context.Duration);
        }

        context.TickInterval = context.TickInterval * (1f + tickIntervalMultiplierDelta)
            + tickIntervalDelta;
        context.TickInterval = Mathf.Max(0.0001f, context.TickInterval);

        context.MaxStacks += maxStacksDelta;
        context.MaxStacks = Mathf.Max(1, context.MaxStacks);
    }

    public string GetDescriptionLine()
    {
        string effectText = GetEffectText();
        string conditionText = GetConditionText();

        if (string.IsNullOrEmpty(effectText))
        {
            effectText = Tr("Buff修正");
        }

        if (string.IsNullOrEmpty(conditionText))
        {
            return effectText;
        }

        return string.Format(
            Tr("当{0}时，{1}"),
            conditionText,
            effectText);
    }

    public static string GetDescription(
        IEnumerable<BuffModifier> modifiers,
        string separator = "\n")
    {
        if (modifiers == null)
        {
            return string.Empty;
        }

        List<string> lines = new();
        foreach (BuffModifier modifier in modifiers)
        {
            if (modifier == null)
            {
                continue;
            }

            string line = modifier.GetDescriptionLine();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            lines.Add(line);
        }

        return string.Join(separator, lines);
    }

    string GetConditionText()
    {
        List<string> parts = new();

        if (!string.IsNullOrWhiteSpace(RequiredBuffId))
        {
            parts.Add(string.Format(
                Tr("Buff为{0}"),
                RequiredBuffId));
        }

        if (RequiredBuffTag != BuffTag.None)
        {
            parts.Add(string.Format(
                Tr("Buff具有{0}标签"),
                Tr(RequiredBuffTag.ToString())));
        }

        if (ExcludedBuffTag != BuffTag.None)
        {
            parts.Add(string.Format(
                Tr("Buff不具有{0}标签"),
                Tr(ExcludedBuffTag.ToString())));
        }

        if (DebuffFilter == BuffModifierDebuffFilter.DebuffOnly)
        {
            parts.Add(Tr("Buff为减益"));
        }
        else if (DebuffFilter == BuffModifierDebuffFilter.BuffOnly)
        {
            parts.Add(Tr("Buff为增益"));
        }

        if (TickTypeFilter == BuffModifierTickTypeFilter.DamageOnly)
        {
            parts.Add(Tr("为持续伤害"));
        }
        else if (TickTypeFilter == BuffModifierTickTypeFilter.HealingOnly)
        {
            parts.Add(Tr("为持续治疗"));
        }

        if (RequiredOwnerBuffTag != BuffTag.None)
        {
            parts.Add(string.Format(
                Tr("目标带有{0}效果"),
                Tr(RequiredOwnerBuffTag.ToString())));
        }

        if (RequiredCasterBuffTag != BuffTag.None)
        {
            parts.Add(string.Format(
                Tr("施加者带有{0}效果"),
                Tr(RequiredCasterBuffTag.ToString())));
        }

        if (ownerFilter != null)
        {
            parts.Add(string.Format(
                Tr("目标满足{0}"),
                ownerFilter.GetDescription()));
        }

        if (casterFilter != null)
        {
            parts.Add(string.Format(
                Tr("施加者满足{0}"),
                casterFilter.GetDescription()));
        }

        return string.Join(Tr("，且"), parts);
    }

    string GetEffectText()
    {
        List<string> parts = new();

        if (!Mathf.IsZeroApprox(durationDelta))
        {
            parts.Add(string.Format(
                Tr("持续时间{0}"),
                FormatSignedNumber(durationDelta)));
        }

        if (!Mathf.IsZeroApprox(durationMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("持续时间{0}"),
                FormatSignedPercent(durationMultiplierDelta)));
        }

        if (!Mathf.IsZeroApprox(tickIntervalDelta))
        {
            parts.Add(string.Format(
                Tr("触发间隔{0}"),
                FormatSignedNumber(tickIntervalDelta)));
        }

        if (!Mathf.IsZeroApprox(tickIntervalMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("触发间隔{0}"),
                FormatSignedPercent(tickIntervalMultiplierDelta)));
        }

        if (maxStacksDelta != 0)
        {
            parts.Add(string.Format(
                Tr("最大层数{0}"),
                FormatSignedInt(maxStacksDelta)));
        }

        if (!Mathf.IsZeroApprox(effectValueDelta))
        {
            parts.Add(string.Format(
                Tr("Buff效果值{0}"),
                FormatSignedNumber(effectValueDelta)));
        }

        if (!Mathf.IsZeroApprox(effectValueMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("Buff效果值{0}"),
                FormatSignedPercent(effectValueMultiplierDelta)));
        }

        if (!Mathf.IsZeroApprox(tickValueDelta))
        {
            parts.Add(string.Format(
                Tr("每跳数值{0}"),
                FormatSignedNumber(tickValueDelta)));
        }

        if (!Mathf.IsZeroApprox(tickValueMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("每跳数值{0}"),
                FormatSignedPercent(tickValueMultiplierDelta)));
        }

        return string.Join(Tr("，"), parts);
    }

    string FormatSignedPercent(float value)
    {
        float percent = value * 100f;
        string sign = percent >= 0f ? "+" : "-";
        return sign + Mathf.Abs(percent).ToString("0.##") + "%";
    }

    string FormatSignedNumber(float value)
    {
        string sign = value >= 0f ? "+" : "-";
        return sign + Mathf.Abs(value).ToString("0.##");
    }

    string FormatSignedInt(int value)
    {
        string sign = value >= 0 ? "+" : "-";
        return sign + Mathf.Abs(value).ToString();
    }
}