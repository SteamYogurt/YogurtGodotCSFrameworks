using System.Collections.Generic;
using Godot;

public class BuffInstance
{
    public Buff Data { get; private set; }
    public IUnit Owner { get; private set; }
    public object Caster { get; private set; }

    public float DurationTimer { get; set; }
    public float TickTimer { get; set; }
    public int Stacks { get; set; }
    public bool IsFinished { get; set; } = false;

    public float ResolvedDuration { get; set; }
    public float ResolvedTickInterval { get; set; }
    public int ResolvedMaxStacks { get; set; }

    readonly List<(UnitStatType type, StatModifier mod)> _appliedModifiers = new();
    public List<(UnitStatType type, StatModifier mod)> AppliedModifiers => _appliedModifiers;

    readonly List<DamageModifier> _appliedDamageModifiers = new();
    public List<DamageModifier> AppliedDamageModifiers => _appliedDamageModifiers;

    readonly List<BuffModifier> _appliedBuffModifiers = new();
    public List<BuffModifier> AppliedBuffModifiers => _appliedBuffModifiers;

    readonly List<System.Action> _cleanupActions = new();
    bool _isCleanedUp;

    public BuffInstance(Buff data, IUnit owner, object caster, int stacks = 1)
    {
        Data = data;
        Owner = owner;
        Caster = caster;
        Stacks = stacks;

        RefreshResolvedBuffValues();
        DurationTimer = ResolvedDuration;
    }

    public void UpdateCaster(object caster)
    {
        if (caster == null)
        {
            return;
        }

        Caster = caster;
    }

    public IUnit ResolveCasterUnit() => Caster as IUnit;

    public void RefreshResolvedBuffValues()
    {
        if (Data?.buffInfo == null)
        {
            ResolvedDuration = 0f;
            ResolvedTickInterval = 1f;
            ResolvedMaxStacks = 1;
            return;
        }

        BuffModifierResolver.RefreshRuntimeValues(this);
    }

    public float GetResolvedDuration() => ResolvedDuration;

    public float GetResolvedTickInterval() => Mathf.Max(0.0001f, ResolvedTickInterval);

    public int GetResolvedMaxStacks() => Mathf.Max(1, ResolvedMaxStacks);

    public float ResolveEffectValue(float baseValue)
    {
        return BuffModifierResolver.ResolveEffectValue(this, baseValue);
    }

    public float ResolveTickValue(float baseValue, bool isHealingTick = false)
    {
        return BuffModifierResolver.ResolveTickValue(this, baseValue, isHealingTick);
    }

    public void AddModifier(UnitStatType type, float value, StatMode mode)
    {
        var mod = new StatModifier(value, mode, (int)Data.buffInfo.priority, this);
        Owner.ApplyStatModifier(type, mod);
        _appliedModifiers.Add((type, mod));
    }

    public DamageModifier AddDamageModifier(DamageModifier modifier)
    {
        if (modifier == null)
        {
            return null;
        }

        DamageModifier runtimeModifier = modifier.Duplicate() as DamageModifier ?? modifier;
        runtimeModifier.RuntimeSource = this;
        this.GetDamageModifierController().AddModifier(runtimeModifier);
        _appliedDamageModifiers.Add(runtimeModifier);
        return runtimeModifier;
    }

    public BuffModifier AddBuffModifier(BuffModifier modifier)
    {
        if (modifier == null)
        {
            return null;
        }

        BuffModifier runtimeModifier = modifier.Duplicate() as BuffModifier ?? modifier;
        runtimeModifier.RuntimeSource = this;
        this.GetBuffModifierController().AddModifier(runtimeModifier);
        _appliedBuffModifiers.Add(runtimeModifier);
        return runtimeModifier;
    }

    public void AddCleanup(System.Action cleanupAction)
    {
        if (cleanupAction == null || _isCleanedUp)
        {
            return;
        }

        _cleanupActions.Add(cleanupAction);
    }

    public void CleanUpModifiers()
    {
        foreach (var (type, _) in _appliedModifiers)
        {
            Owner.RemoveStatModifier(type, this);
        }

        _appliedModifiers.Clear();
    }

    public void CleanUpDamageModifiers()
    {
        if (_appliedDamageModifiers.Count <= 0)
        {
            return;
        }

        if (this.TryGetDamageModifierController(out DamageModifierController controller)
            && controller != null)
        {
            for (int i = 0; i < _appliedDamageModifiers.Count; i++)
            {
                controller.RemoveModifier(_appliedDamageModifiers[i]);
            }
        }

        _appliedDamageModifiers.Clear();
    }

    public void CleanUpBuffModifiers()
    {
        if (_appliedBuffModifiers.Count <= 0)
        {
            return;
        }

        if (this.TryGetBuffModifierController(out BuffModifierController controller)
            && controller != null)
        {
            for (int i = 0; i < _appliedBuffModifiers.Count; i++)
            {
                controller.RemoveModifier(_appliedBuffModifiers[i]);
            }
        }

        _appliedBuffModifiers.Clear();
    }

    public void CleanupAll()
    {
        if (_isCleanedUp)
        {
            return;
        }

        _isCleanedUp = true;

        CleanUpModifiers();
        CleanUpDamageModifiers();
        CleanUpBuffModifiers();

        for (int i = _cleanupActions.Count - 1; i >= 0; i--)
        {
            try
            {
                _cleanupActions[i]?.Invoke();
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"Buff cleanup failed: {e}");
            }
        }

        _cleanupActions.Clear();
    }
}
