using System;
using System.Buffers.Binary;

internal static class NetWireFrames
{
    public static bool IsPacket(ReadOnlySpan<byte> packet)
    {
        if (packet.IsEmpty || packet.Length > NetWireLimits.MaxPacketBytes)
            return false;
        while (!packet.IsEmpty)
        {
            if (!TryReadField(ref packet, out var body) || body.IsEmpty)
                return false;
        }
        return true;
    }

    public static bool TryReadField(ref ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value)
    {
        value = default;
        if (source.Length < sizeof(int))
            return false;
        int size = BinaryPrimitives.ReadInt32LittleEndian(source);
        if (size < 0 || size > source.Length - sizeof(int))
            return false;
        value = source.Slice(sizeof(int), size);
        source = source[(sizeof(int) + size)..];
        return true;
    }

    public static bool IsVariantSequence(ReadOnlySpan<byte> source, int count)
    {
        if ((uint)count > (uint)(source.Length / 8))
            return false;
        for (int i = 0; i < count; i++)
        {
            if (!TryReadField(ref source, out var variant) || variant.Length < sizeof(int))
                return false;
        }
        return source.IsEmpty;
    }
}
