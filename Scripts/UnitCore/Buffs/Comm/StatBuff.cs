using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class StatBuff : Buff
{
    [Export]
    public Godot.Collections.Array<StatChangeConfig> StatChanges;

    public override void OnEnter(BuffInstance instance)
    {
        ApplyStats(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        instance.CleanUpModifiers();
        ApplyStats(instance);
    }

    private void ApplyStats(BuffInstance instance)
    {
        if (StatChanges == null) return;

        foreach (var config in StatChanges)
        {
            float finalValue;
            if (!buffInfo.infiniteStacks)
                finalValue = config.Value + config.ExtraValueEachStack * instance.Stacks;
            else
            {
                finalValue = config.Value * instance.Stacks;
            }

            finalValue = instance.ResolveEffectValue(finalValue);
            instance.AddModifier(config.StatType, finalValue, config.Mode);
        }
    }

    public override string GetBuffDes()
    {
        if (buffInfo != null && buffInfo.overrideBuffDes && !string.IsNullOrEmpty(buffInfo.buffDes))
        {
            return Tr(buffInfo.buffDes);
        }

        if (StatChanges == null || StatChanges.Count == 0)
        {
            return Tr(buffInfo?.buffDes ?? string.Empty);
        }

        var lines = new List<string>();

        foreach (StatChangeConfig config in StatChanges)
        {
            bool isPercent = config.ShowMode != StatMode.Additive;
            float baseVal = config.Value;
            float extraPerStack = config.ExtraValueEachStack;

            string FormatValue(float val)
            {
                if (isPercent)
                {
                    if (val < 0.1)
                        return (val * 100).ToString("F1") + "%";
                    return (val * 100).ToString("F0") + "%";
                }
                return val.ToString("0.##");
            }

            string propName = Tr(config.StatType.ToString());
            string baseStr = FormatValue(baseVal);
            string extraStr = string.Empty;

            if (Mathf.Abs(extraPerStack) > 0)
            {
                string sign = extraPerStack > 0 ? "+" : "-";
                extraStr = string.Format(" ({0}{1}{2})", Tr("每层"), sign, FormatValue(extraPerStack));
            }
            string addsign = config.Value > 0 ? "+" : "-";
            lines.Add(string.Format("{0}:{1}{2}{3}", propName, addsign, baseStr, extraStr));
        }

        var stackStr = GetStackStr();
        if (!string.IsNullOrEmpty(stackStr))
            lines.Add(stackStr);

        return string.Join("\n", lines);
    }
}