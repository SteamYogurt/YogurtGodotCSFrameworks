using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class StatChangeConfig : Resource
{
    [Export] public UnitStatType StatType;
    [Export] public float Value;
    [Export] public float ExtraValueEachStack;
    [Export] public StatMode Mode = StatMode.Additive;
    [Export] public StatMode ShowMode = StatMode.Multiplicative;

    public float GetFinalValue(int stacks = 0)
    {
        return Value + ExtraValueEachStack * stacks;
    }

    public StatModifier CreateModifier(object source = null, int priority = 0, int stacks = 0)
    {
        return new StatModifier(GetFinalValue(stacks), Mode, priority, source);
    }

    public void ApplyTo(IUnit unit, object source = null, int priority = 0, int stacks = 0)
    {
        if (unit == null)
        {
            return;
        }
        unit.ApplyStatModifier(StatType, CreateModifier(source, priority, stacks));
    }

    public string GetDescriptionLine(int stacks = 0)
    {
        return GetDescriptionLine(this, stacks);
    }

    public static void ApplyConfigs(
        IUnit unit,
        Array<StatChangeConfig> statChangeConfigs,
        object source = null,
        int priority = 0,
        int stacks = 0)
    {
        if (unit == null || statChangeConfigs == null)
        {
            return;
        }

        foreach (StatChangeConfig config in statChangeConfigs)
        {
            if (config == null)
            {
                continue;
            }
            config.ApplyTo(unit, source, priority, stacks);
        }
    }

    public static string GetDescription(
        Array<StatChangeConfig> statChangeConfigs,
        string separator = "\n",
        int stacks = 0)
    {
        if (statChangeConfigs == null || statChangeConfigs.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = new List<string>();
        foreach (StatChangeConfig config in statChangeConfigs)
        {
            if (config == null)
            {
                continue;
            }

            string line = GetDescriptionLine(config, stacks);
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            lines.Add(line);
        }
        return string.Join(separator, lines);
    }

    public static string GetDescriptionLine(StatChangeConfig config, int stacks = 0)
    {
        if (config == null)
        {
            return string.Empty;
        }

        float finalValue = config.GetFinalValue(stacks);
        string statName = TranslationServer.Translate(config.StatType.ToString());
        string sign = finalValue >= 0f ? "+" : "-";
        string valueText = FormatValue(config.ShowMode, Mathf.Abs(finalValue));

        if (Mathf.IsZeroApprox(config.ExtraValueEachStack))
        {
            return string.Format(
                TranslationServer.Translate("{0}：{1}{2}"),
                statName,
                sign,
                valueText);
        }

        string stackSign = config.ExtraValueEachStack >= 0f ? "+" : "-";
        string stackValueText = FormatValue(config.ShowMode, Mathf.Abs(config.ExtraValueEachStack));

        return string.Format(
            TranslationServer.Translate("{0}：{1}{2}（{3}{4}{5}）"),
            statName,
            sign,
            valueText,
            TranslationServer.Translate("每层"),
            stackSign,
            stackValueText);
    }

    public static string FormatValue(StatMode showMode, float value)
    {
        if (showMode != StatMode.Additive)
        {
            return (value * 100f).ToString("0.##") + "%";
        }
        return value.ToString("0.##");
    }
}