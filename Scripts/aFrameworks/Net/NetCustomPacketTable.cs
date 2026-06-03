using System;
using System.Collections.Generic;
using Godot;

public enum NetCustomPacketSendType : byte
{
    ToAll,
    ToHost,
    ToAllExceptSender
}

public delegate void NetCustomPacketRawHandler(ReadOnlySpan<byte> payload);

public class NetCustomPacketTable
{
    private readonly Dictionary<ushort, Action<Variant[]>> _variantHandlers = new();
    private readonly Dictionary<ushort, NetCustomPacketRawHandler> _rawHandlers = new();

    public void Register(ushort packetId, Action<Variant[]> handler)
    {
        _variantHandlers[packetId] = handler;
    }

    public void RegisterRaw(ushort packetId, NetCustomPacketRawHandler handler)
    {
        _rawHandlers[packetId] = handler;
    }

    public void Dispatch(ushort packetId, Variant[] args)
    {
        if (_variantHandlers.TryGetValue(packetId, out var handler))
        {
            handler.Invoke(args);
        }
    }

    public void DispatchRaw(ushort packetId, ReadOnlySpan<byte> payload)
    {
        if (_rawHandlers.TryGetValue(packetId, out var handler))
        {
            handler.Invoke(payload);
        }
    }

    public bool HasRawHandler(ushort packetId)
    {
        return _rawHandlers.ContainsKey(packetId);
    }

    public bool HasVariantHandler(ushort packetId)
    {
        return _variantHandlers.ContainsKey(packetId);
    }
}