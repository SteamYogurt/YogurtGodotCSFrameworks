using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 封装 RPC 函数的表结构
/// 用于统一管理和分发 RPC 调用
/// </summary>
public class NetRPCTable
{
    private readonly Dictionary<byte, Action<Variant[]>> _handlers = new();

    public void Register(byte rpcId, Action<Variant[]> handler)
    {
        if (_handlers.ContainsKey(rpcId))
        {
            GD.PrintErr($"NetRPCTable.Register: rpcId={rpcId} 已注册，禁止覆盖");
            throw new InvalidOperationException($"NetRPCTable rpcId {rpcId} already registered");
        }

        _handlers[rpcId] = handler;
    }

    public void Dispatch(byte rpcId, Variant[] reader)
    {
        if (_handlers.TryGetValue(rpcId, out var handler))
        {
            handler.Invoke(reader);
        }
    }
}