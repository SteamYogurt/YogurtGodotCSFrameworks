using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public interface INetObject : IGameObject
{
    public void HostInitialize()
    {

    }
    public bool HasAuthority()
    {
        return false;
    }
    public List<NetVar> GetInputStateVars();
    public List<NetVar> GetFullStateVars();
    public NetRPCTable GetNetRPCTable();
    //public byte[] GetInitialPacket()
    //{
    //    return null;
    //}
    //public void ReadInitialPacket(byte[] data)
    //{

    //}
    /// <summary>
    /// 收到初始包之后立即调用
    /// </summary>
    public void INetSpawn()
    {

    }
    /// <summary>
    /// 销毁或失效前调用
    /// </summary>
    public void INetDestroy()
    {

    }
    public bool IsNetInvalid()
    {
        return false;
    }
}
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

                // 触发本地逻辑响应
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
        _isDirty = true; // 初始状态默认为脏，确保首次同步
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
/// <summary>
/// 封装 RPC 函数的表结构
/// 用于统一管理和分发 RPC 调用
/// </summary>
public class NetRPCTable
{
    // 使用 byte 作为 Key 节省带宽，Value 为处理函数流
    private readonly Dictionary<byte, Action<Variant[]>> _handlers = new();

    /// <summary>
    /// 注册 RPC 处理函数
    /// </summary>
    /// <param name="rpcId">自定义的 RPC 标识符</param>
    /// <param name="handler">处理逻辑，接收一个包含参数的数据流</param>
    public void Register(byte rpcId, Action<Variant[]> handler)
    {
        _handlers[rpcId] = handler;
    }

    /// <summary>
    /// 执行收到的 RPC
    /// </summary>
    public void Dispatch(byte rpcId, Variant[] reader)
    {
        if (_handlers.TryGetValue(rpcId, out var handler))
        {
            handler.Invoke(reader);
        }
    }
}

