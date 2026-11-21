internal static class NetWireLimits
{
    public const int ProtocolVersion = 3;
    // Steam Networking Messages permits at most 512 KiB per message.
    public const int MaxPacketBytes = 512 * 1024;
    public const int MaxQueuedBytes = 8 * 1024 * 1024;
    public const int MaxQueuedPackets = 2048;
}
