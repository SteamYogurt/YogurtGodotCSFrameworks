using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class DamageModifier : Resource
{
    [Export] public int Priority;
    [Export] public bool Enabled = true;
    [Export] public DamageResolveStage ApplyStage = DamageResolveStage.Outgoing;

    [ExportGroup("Filters")]
    [Export] public DamageTag RequiredDamageTags = DamageTag.None;
    [Export] public DamageTag ExcludedDamageTags = DamageTag.None;
    [Export] public DamageSourceKind RequiredSourceKind = DamageSourceKind.None;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ApplyChance = 1f;
    [Export] public BuffTag RequiredAttackerBuffTag = BuffTag.None;
    [Export] public BuffTag RequiredTargetBuffTag = BuffTag.None;
    [Export] public string RequiredAttackerBuffId;
    [Export] public string RequiredTargetBuffId;
    [Export] public ObjectFilter attackerFilter;
    [Export] public ObjectFilter targetFilter;
    [Export] public ObjectFilter damageContextFilter;

    [ExportGroup("Deltas")]
    [Export] public float rawPhysicalDamageDelta;
    [Export] public float rawMagicalDamageDelta;
    [Export] public float rawRealDamageDelta;

    [Export] public float outgoingDamageMultiplierDelta;
    [Export] public float incomingDamageMultiplierDelta;
    [Export] public float finalDamageMultiplierDelta;
    [Export] public float shieldDamageMultiplierDelta;

    [Export] public float critChanceDelta;
    [Export] public float critMultiplierDelta;

    [ExportGroup("Flags")]
    [Export] public bool forceCrit;
    [Export] public bool hideDamageText;

    public object RuntimeSource { get; set; }

    public bool IsMatchStage(DamageResolveStage stage)
    {
        return ApplyStage == stage;
    }

    public bool IsMatch(DamageContext damageContext)
    {
        if (!Enabled || damageContext == null)
        {
            return false;
        }

        float applyChance = Mathf.Clamp(ApplyChance, 0f, 1f);
        if (applyChance <= 0f)
        {
            return false;
        }

        if (applyChance < 1f && GD.Randf() > applyChance)
        {
            return false;
        }

        if (RequiredDamageTags != DamageTag.None
            && !damageContext.HasAnyTag(RequiredDamageTags))
        {
            return false;
        }

        if (ExcludedDamageTags != DamageTag.None
            && damageContext.HasAnyTag(ExcludedDamageTags))
        {
            return false;
        }

        if (RequiredSourceKind != DamageSourceKind.None
            && damageContext.Source?.Kind != RequiredSourceKind)
        {
            return false;
        }

        if (attackerFilter != null && !attackerFilter.IsMatch(damageContext.Attacker))
        {
            return false;
        }

        if (targetFilter != null && !targetFilter.IsMatch(damageContext.Target))
        {
            return false;
        }

        if (damageContextFilter != null && !damageContextFilter.IsMatch(damageContext))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(RequiredAttackerBuffId))
        {
            if (damageContext.Attacker?.BuffController == null
                || !damageContext.Attacker.BuffController.HasBuff(RequiredAttackerBuffId))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(RequiredTargetBuffId))
        {
            if (damageContext.Target?.BuffController == null
                || !damageContext.Target.BuffController.HasBuff(RequiredTargetBuffId))
            {
                return false;
            }
        }

        if (RequiredAttackerBuffTag != BuffTag.None)
        {
            if (damageContext.Attacker?.BuffController == null
                || !damageContext.Attacker.BuffController.HasBuffTag(RequiredAttackerBuffTag))
            {
                return false;
            }
        }

        if (RequiredTargetBuffTag != BuffTag.None)
        {
            if (damageContext.Target?.BuffController == null
                || !damageContext.Target.BuffController.HasBuffTag(RequiredTargetBuffTag))
            {
                return false;
            }
        }

        return true;
    }

    public void Apply(DamageContext damageContext)
    {
        if (damageContext == null)
        {
            return;
        }

        if (!Mathf.IsZeroApprox(rawPhysicalDamageDelta))
        {
            damageContext.RawPhysicalDamage = Mathf.Max(
                0f,
                damageContext.RawPhysicalDamage + rawPhysicalDamageDelta);
        }

        if (!Mathf.IsZeroApprox(rawMagicalDamageDelta))
        {
            damageContext.RawMagicalDamage = Mathf.Max(
                0f,
                damageContext.RawMagicalDamage + rawMagicalDamageDelta);
        }

        if (!Mathf.IsZeroApprox(rawRealDamageDelta))
        {
            damageContext.RawRealDamage = Mathf.Max(
                0f,
                damageContext.RawRealDamage + rawRealDamageDelta);
        }

        if (!Mathf.IsZeroApprox(outgoingDamageMultiplierDelta))
        {
            damageContext.OutgoingDamageMultiplier = Mathf.Max(
                0f,
                damageContext.OutgoingDamageMultiplier + outgoingDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(incomingDamageMultiplierDelta))
        {
            damageContext.IncomingDamageMultiplier = Mathf.Max(
                0f,
                damageContext.IncomingDamageMultiplier + incomingDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(finalDamageMultiplierDelta))
        {
            damageContext.FinalDamageMultiplier = Mathf.Max(
                0f,
                damageContext.FinalDamageMultiplier + finalDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(shieldDamageMultiplierDelta))
        {
            damageContext.ShieldDamageMultiplier = Mathf.Max(
                0f,
                damageContext.ShieldDamageMultiplier + shieldDamageMultiplierDelta);
        }

        if (!Mathf.IsZeroApprox(critChanceDelta))
        {
            damageContext.CritChance = Mathf.Clamp(
                damageContext.CritChance + critChanceDelta,
                0f,
                1f);
        }

        if (!Mathf.IsZeroApprox(critMultiplierDelta))
        {
            damageContext.CritMultiplier = Mathf.Max(
                0f,
                damageContext.CritMultiplier + critMultiplierDelta);
        }

        if (forceCrit)
        {
            damageContext.IsCrit = true;
        }

        if (hideDamageText)
        {
            damageContext.ShowText = false;
        }
    }

    public string GetDescriptionLine()
    {
        string effectText = GetEffectText();
        string conditionText = GetConditionText();

        if (string.IsNullOrEmpty(effectText))
        {
            effectText = Tr("伤害修正");
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
        IEnumerable<DamageModifier> modifiers,
        string separator = "\n")
    {
        if (modifiers == null)
        {
            return string.Empty;
        }

        List<string> lines = new();
        foreach (DamageModifier modifier in modifiers)
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

        float applyChance = Mathf.Clamp(ApplyChance, 0f, 1f);
        if (applyChance < 1f)
        {
            parts.Add(string.Format(
                Tr("有{0}概率"),
                FormatPercent(applyChance)));
        }

        if (RequiredDamageTags != DamageTag.None)
        {
            parts.Add(string.Format(
                Tr("伤害具有{0}标签"),
                GetDamageTagsText(RequiredDamageTags)));
        }

        if (ExcludedDamageTags != DamageTag.None)
        {
            parts.Add(string.Format(
                Tr("伤害不具有{0}标签"),
                GetDamageTagsText(ExcludedDamageTags)));
        }

        if (RequiredSourceKind != DamageSourceKind.None)
        {
            parts.Add(string.Format(
                Tr("来源为{0}"),
                GetSourceKindText(RequiredSourceKind)));
        }

        if (RequiredAttackerBuffTag != BuffTag.None)
        {
            parts.Add(string.Format(
                Tr("攻击者带有{0}效果"),
                Tr(RequiredAttackerBuffTag.ToString())));
        }

        if (RequiredTargetBuffTag != BuffTag.None)
        {
            parts.Add(string.Format(
                Tr("目标带有{0}效果"),
                Tr(RequiredTargetBuffTag.ToString())));
        }

        if (!string.IsNullOrWhiteSpace(RequiredAttackerBuffId))
        {
            parts.Add(string.Format(
                Tr("攻击者带有Buff：{0}"),
                RequiredAttackerBuffId));
        }

        if (!string.IsNullOrWhiteSpace(RequiredTargetBuffId))
        {
            parts.Add(string.Format(
                Tr("目标带有Buff：{0}"),
                RequiredTargetBuffId));
        }

        if (attackerFilter != null)
        {
            parts.Add(string.Format(
                Tr("攻击者满足{0}"),
                attackerFilter.GetDescription()));
        }

        if (targetFilter != null)
        {
            parts.Add(string.Format(
                Tr("目标满足{0}"),
                targetFilter.GetDescription()));
        }

        if (damageContextFilter != null)
        {
            parts.Add(string.Format(
                Tr("伤害满足{0}"),
                damageContextFilter.GetDescription()));
        }

        return string.Join(Tr("，且"), parts);
    }

    string GetEffectText()
    {
        List<string> parts = new();

        if (!Mathf.IsZeroApprox(rawPhysicalDamageDelta))
        {
            parts.Add(string.Format(
                Tr("物理基础伤害{0}"),
                FormatSignedNumber(rawPhysicalDamageDelta)));
        }

        if (!Mathf.IsZeroApprox(rawMagicalDamageDelta))
        {
            parts.Add(string.Format(
                Tr("魔法基础伤害{0}"),
                FormatSignedNumber(rawMagicalDamageDelta)));
        }

        if (!Mathf.IsZeroApprox(rawRealDamageDelta))
        {
            parts.Add(string.Format(
                Tr("真实基础伤害{0}"),
                FormatSignedNumber(rawRealDamageDelta)));
        }

        if (!Mathf.IsZeroApprox(outgoingDamageMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("造成伤害{0}"),
                FormatSignedPercent(outgoingDamageMultiplierDelta)));
        }

        if (!Mathf.IsZeroApprox(incomingDamageMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("承受伤害{0}"),
                FormatSignedPercent(incomingDamageMultiplierDelta)));
        }

        if (!Mathf.IsZeroApprox(finalDamageMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("最终伤害{0}"),
                FormatSignedPercent(finalDamageMultiplierDelta)));
        }

        if (!Mathf.IsZeroApprox(shieldDamageMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("对护盾伤害{0}"),
                FormatSignedPercent(shieldDamageMultiplierDelta)));
        }

        if (!Mathf.IsZeroApprox(critChanceDelta))
        {
            parts.Add(string.Format(
                Tr("暴击率{0}"),
                FormatSignedPercent(critChanceDelta)));
        }

        if (!Mathf.IsZeroApprox(critMultiplierDelta))
        {
            parts.Add(string.Format(
                Tr("暴击倍率{0}"),
                FormatSignedPercent(critMultiplierDelta)));
        }

        if (forceCrit)
        {
            parts.Add(Tr("强制暴击"));
        }

        if (hideDamageText)
        {
            parts.Add(Tr("隐藏伤害文字"));
        }

        return string.Join(Tr("，"), parts);
    }

    string GetDamageTagsText(DamageTag tags)
    {
        List<string> names = new();

        DamageTag[] allTags =
        {
            DamageTag.Projectile,
            DamageTag.Dot,
            DamageTag.Explosion,
            DamageTag.BasicAttack,
            DamageTag.Skill,
            DamageTag.Flame,
            DamageTag.Poison,
            DamageTag.Cold,
            DamageTag.Buff,
        };

        for (int i = 0; i < allTags.Length; i++)
        {
            DamageTag tag = allTags[i];
            if ((tags & tag) == 0)
            {
                continue;
            }

            names.Add(GetDamageTagText(tag));
        }

        return string.Join("、", names);
    }

    string GetDamageTagText(DamageTag tag)
    {
        return tag switch
        {
            DamageTag.Projectile => Tr("投射物"),
            DamageTag.Dot => Tr("持续伤害"),
            DamageTag.Explosion => Tr("爆炸"),
            DamageTag.BasicAttack => Tr("普通攻击"),
            DamageTag.Skill => Tr("技能"),
            DamageTag.Flame => Tr("燃烧"),
            DamageTag.Poison => Tr("中毒"),
            DamageTag.Cold => Tr("寒冷"),
            DamageTag.Buff => Tr("Buff"),
            _ => Tr("未知"),
        };
    }

    string GetSourceKindText(DamageSourceKind kind)
    {
        return kind switch
        {
            DamageSourceKind.Attack => Tr("攻击"),
            DamageSourceKind.Projectile => Tr("投射物"),
            DamageSourceKind.BuffTick => Tr("Buff跳伤"),
            DamageSourceKind.Effect => Tr("效果"),
            _ => Tr("任意来源"),
        };
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

    string FormatPercent(float value)
    {
        return (value * 100f).ToString("0.##") + "%";
    }
}