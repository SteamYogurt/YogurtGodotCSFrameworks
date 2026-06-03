using System.Collections.Generic;

public class ConditionContext
{
    readonly Dictionary<ConditionSubjectKey, object> data = new();
    readonly Dictionary<string, object> customData = new();

    public ConditionContext Set(ConditionSubjectKey key, object value)
    {
        if (key == ConditionSubjectKey.None)
        {
            return this;
        }

        data[key] = value;
        return this;
    }

    public T Get<T>(ConditionSubjectKey key, T fallback = default)
    {
        if (data.TryGetValue(key, out object value) && value is T typed)
        {
            return typed;
        }

        return fallback;
    }

    public object GetObject(ConditionSubjectKey key) =>
        data.TryGetValue(key, out object value) ? value : null;

    public bool Has(ConditionSubjectKey key) => data.ContainsKey(key);

    public ConditionContext SetCustom(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return this;
        }

        customData[key] = value;
        return this;
    }

    public T GetCustom<T>(string key, T fallback = default)
    {
        if (customData.TryGetValue(key, out object value) && value is T typed)
        {
            return typed;
        }

        return fallback;
    }

    public bool HasCustom(string key) => !string.IsNullOrEmpty(key) && customData.ContainsKey(key);
}
