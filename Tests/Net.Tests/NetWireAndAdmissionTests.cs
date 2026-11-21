using System.Buffers.Binary;
using Xunit;

public sealed class NetWireAndAdmissionTests
{
    [Fact]
    public void FramesMustBeCompleteWithinOnePacket()
    {
        byte[] batched = [1, 0, 0, 0, 102, 1, 0, 0, 0, 106];
        Assert.True(NetWireFrames.IsPacket(batched));
        Assert.False(NetWireFrames.IsPacket(batched[..^1]));
        Assert.False(NetWireFrames.IsPacket(batched[3..]));
        Assert.False(NetWireFrames.IsPacket([0, 0, 0, 0]));
        Assert.False(NetWireFrames.IsPacket([255, 255, 255, 255, 1]));

        var oversized = new byte[NetWireLimits.MaxPacketBytes + 1];
        BinaryPrimitives.WriteInt32LittleEndian(oversized, oversized.Length - sizeof(int));
        Assert.False(NetWireFrames.IsPacket(oversized));
    }

    [Fact]
    public void VariantSequenceRejectsIncorrectLengths()
    {
        byte[] sequence = [4, 0, 0, 0, 1, 2, 3, 4, 4, 0, 0, 0, 5, 6, 7, 8];
        Assert.True(NetWireFrames.IsVariantSequence(sequence, 2));
        Assert.False(NetWireFrames.IsVariantSequence(sequence, 1));
        Assert.False(NetWireFrames.IsVariantSequence(sequence[..^1], 2));
    }

    [Fact]
    public void ExplicitMatchAccessOverridesRoomDefaultAndLegacySelection()
    {
        var access = new NetMatchPeerAccess();
        Assert.True(access.CanBroadcast(null!));
        access.RequireAdmission = true;
        Assert.False(access.Allows(2, null!));
        Assert.True(access.Allows(2, [2]));
        Assert.False(access.Allows(3, [2]));
        access.Set(2, false);
        access.Set(3, true);
        Assert.False(access.Allows(2, [2]));
        Assert.True(access.IsExplicitlyAdmitted(3));
        Assert.True(access.Allows(3, [2]));
        Assert.False(access.CanBroadcast(null!));
        access.RemoveDeparted([new INetTransportPlayerInfo { id = 2 }]);
        Assert.False(access.Allows(3, null!));
        access.Clear();
        Assert.True(access.CanBroadcast(null!));
    }
}
