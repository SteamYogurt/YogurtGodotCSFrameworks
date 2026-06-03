using Godot;
using System.Linq;

[GlobalClass]
public partial class SlowBuff : Buff
{
    [Export] public float SlowAmount = 0.2f;
    [Export] public float SlowExtraAmountPerStack = 0.1f;

    public float GetSlowAmount(BuffInstance instance)
    {
        if (instance == null)
        {
            return SlowAmount;
        }

        float value = SlowAmount + SlowExtraAmountPerStack * (instance.Stacks - 1);
        return instance.ResolveEffectValue(value);
    }

    public override void OnEnter(BuffInstance instance)
    {
        instance.AddModifier(UnitStatType.MoveSpeed, -GetSlowAmount(instance), StatMode.Multiplicative);
        RefreshTagGroup(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        instance.CleanUpModifiers();
        instance.AddModifier(UnitStatType.MoveSpeed, -GetSlowAmount(instance), StatMode.Multiplicative);
        RefreshTagGroup(instance);
    }

    public override void OnExit(BuffInstance instance)
    {
        RefreshTagGroup(instance, true);
    }

    private void RefreshTagGroup(BuffInstance instance, bool isExiting = false)
    {
        IUnit owner = instance.Owner;
        if (owner?.BuffController == null) return;

        var sameTagInstances = owner.BuffController.ActiveBuffs
            .Where(b => b.Data.buffInfo.tag == this.buffInfo.tag)
            .ToList();

        if (isExiting)
        {
            sameTagInstances.Remove(instance);
        }

        if (sameTagInstances.Count == 0)
        {
            owner.NotifyStatChanged(UnitStatType.MoveSpeed, 0);
            return;
        }

        var strongest = sameTagInstances
            .OrderByDescending(b => (b.Data as SlowBuff)?.GetSlowAmount(b) ?? 0f)
            .First();

        foreach (var ins in sameTagInstances)
        {
            bool shouldBeActive = (ins == strongest);

            foreach (var mod in ins.AppliedModifiers)
            {
                var modi = mod.mod;
                if (modi.Active != shouldBeActive)
                {
                    modi.Active = shouldBeActive;
                }
            }
        }

        owner.NotifyStatChanged(UnitStatType.MoveSpeed, 0);
    }

    public override string GetBuffDes()
    {
        if (buffInfo != null && buffInfo.overrideBuffDes && !string.IsNullOrEmpty(buffInfo.buffDes))
        {
            return Tr(buffInfo.buffDes);
        }

        var lines = new System.Collections.Generic.List<string>();

        string FormatValue(float val)
        {
            if (val < 0.1f)
                return (val * 100).ToString("F1") + "%";
            return (val * 100).ToString("F0") + "%";
        }

        string baseStr = FormatValue(SlowAmount);
        string extraStr = string.Empty;

        if (Mathf.Abs(SlowExtraAmountPerStack) > 0)
        {
            string sign = SlowExtraAmountPerStack > 0 ? "+" : "-";
            extraStr = string.Format(" ({0}{1}{2})",
                Tr("每层额外"),
                sign,
                FormatValue(SlowExtraAmountPerStack));
        }

        string addSign = SlowAmount > 0 ? "-" : "+";

        lines.Add(string.Format("{0}{1}{2}{3}",
            Tr("MoveSpeed"),
            addSign,
            baseStr,
            extraStr));

        var stackStr = GetStackStr();

        lines.Add(stackStr);
        return string.Join("\n", lines);
    }
}