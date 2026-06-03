using System;
using System.Collections.Generic;
using Godot;

#region Stat Modifier

public enum StatMode
{
    Additive,              // +10
    Multiplicative,        // +10%（叠加后统一乘）
    FinalMultiplicative    // *2（最终乘）
}

public class StatModifier
{
    public bool Active = true;

    public float Value;
    public StatMode Mode;
    public int Priority;
    public object Source;

    public StatModifier(float value, StatMode mode, int priority = 0, object source = null)
    {
        Value = value;
        Mode = mode;
        Priority = priority;
        Source = source;
    }
}

#endregion

#region Stat

public class Stat
{
    private float _baseValue;
    private float _cachedValue;
    private bool _dirty = true;

    private readonly List<StatModifier> _modifiers = new();

    public float BaseValue
    {
        get => _baseValue;
        set
        {
            if (Mathf.IsEqualApprox(_baseValue, value)) return;
            _baseValue = value;
            MarkDirty();
        }
    }

    public float Value
    {
        get
        {
            if (_dirty)
            {
                _cachedValue = CalculateFinalValue();
                _dirty = false;
            }
            return _cachedValue;
        }
    }

    public void MarkDirty() => _dirty = true;

    #region Modifier API

    public void AddModifier(StatModifier mod)
    {
        _modifiers.Add(mod);
        _modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        MarkDirty();
    }

    public void RemoveModifier(StatModifier mod)
    {
        if (_modifiers.Remove(mod))
            MarkDirty();
    }

    public void RemoveAllFromSource(object source)
    {
        if (source == null) return;
        _modifiers.RemoveAll(m => m.Source == source);
        MarkDirty();
    }

    public void ClearModifiers()
    {
        _modifiers.Clear();
        MarkDirty();
    }

    #endregion

    #region Core Calculation
    private float CalculateFinalValue()
    {
        float additiveSum = 0;
        float multiplicativeSum = 0;
        float finalMultiplicativeSum = 0;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];
            if (!mod.Active) continue;

            switch (mod.Mode)
            {
                case StatMode.Additive:
                    additiveSum += mod.Value;
                    break;

                case StatMode.Multiplicative:
                    multiplicativeSum += mod.Value;
                    break;

                case StatMode.FinalMultiplicative:
                    finalMultiplicativeSum += mod.Value;
                    break;
            }
        }

        float finalValue = _baseValue * (1 + multiplicativeSum);
        finalValue += additiveSum;
        finalValue *= (1 + finalMultiplicativeSum);

        return finalValue;
    }

    #endregion
}

#endregion

#region Stat Container

public class StatCollection
{
    private readonly Dictionary<object, Stat> _stats = new();

    public Stat Get<TEnum>(TEnum type)
    {
        if (!_stats.TryGetValue(type, out var stat))
        {
            stat = new Stat();
            _stats[type] = stat;
        }
        return stat;
    }

    public float GetValue<TEnum>(TEnum type, float baseValue)
    {
        var stat = Get(type);
        stat.BaseValue = baseValue;
        return stat.Value;
    }

    public void SetBase<TEnum>(TEnum type, float value)
    {
        Get(type).BaseValue = value;
    }

    public void AddModifier<TEnum>(TEnum type, StatModifier mod)
    {
        Get(type).AddModifier(mod);
    }

    public void RemoveFromSource<TEnum>(TEnum type, object source)
    {
        Get(type).RemoveAllFromSource(source);
    }
}

#endregion