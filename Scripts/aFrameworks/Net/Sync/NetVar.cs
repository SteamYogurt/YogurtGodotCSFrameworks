using System;
using Godot;

public class NetVar
{
    private Variant _value;
    private bool _isDirty;

    /// <summary>
    /// 当值发生改变时触发
    /// </summary>
    public Action OnValueChanged;

    /// <summary>
    /// 设置成这个即本地可忽略的量
    /// </summary>
    public bool authorityIgnore = false;

    /// <summary>
    /// 获取或设置值，设置不同值时自动标记为脏并触发回调
    /// </summary>
    public Variant Value
    {
        get => _value;
        set
        {
            if (!_value.Equals(value))
            {
                Variant oldValue = _value;
                _value = value;
                _isDirty = true;

                OnValueChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// 是否有变动未同步
    /// </summary>
    public bool IsDirty => _isDirty;

    public NetVar(Variant defaultValue = default)
    {
        _value = defaultValue;
        _isDirty = true;
    }

    /// <summary>
    /// 清除脏标记
    /// </summary>
    public void ClearDirty() => _isDirty = false;

    /// <summary>
    /// 强制标记为脏
    /// </summary>
    public void MarkDirty() => _isDirty = true;
}
