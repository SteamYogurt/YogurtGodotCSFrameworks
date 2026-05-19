using System;
using System.Collections.Generic;
using Godot;

public enum NetCustomPacketSendType : byte
{
    ToAll,
    ToHost,
    ToAllExceptSender
}

public class NetCustomPacketTable
{
    private readonly Dictionary<ushort, Action<Variant[]>> _handlers = new();

    public void Register(ushort packetId, Action<Variant[]> handler)
    {
        _handlers[packetId] = handler;
    }

    public void Dispatch(ushort packetId, Variant[] args)
    {
        if (_handlers.TryGetValue(packetId, out var handler))
        {
            handler.Invoke(args);
        }
    }
}