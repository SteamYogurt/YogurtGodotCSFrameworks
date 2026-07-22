using Godot;

[GlobalClass]
public partial class StatBuffEffect : BuffEffect
{
    [Export]
    public Godot.Collections.Array<StatChangeConfig> StatChanges;

    public override void OnEnter(BuffInstance instance)
    {
        ApplyStats(instance);
    }

    public override void OnStackChanged(BuffInstance instance)
    {
        ApplyStats(instance);
    }

    void ApplyStats(BuffInstance instance)
    {
        if (StatChanges == null || instance?.Data?.buffInfo == null)
        {
            return;
        }

        foreach (StatChangeConfig config in StatChanges)
        {
            if (config == null)
            {
                continue;
            }

            float finalValue;
            if (instance.Data.buffInfo.infiniteStacks)
            {
                finalValue = config.Value * instance.Stacks;
            }
            else
            {
                finalValue = instance.ResolveStackedValue(config.Value, config.ExtraValueEachStack);
            }

            finalValue = instance.ResolveEffectValue(finalValue);
            instance.AddModifier(config.StatType, finalValue, config.Mode);
        }
    }
}
