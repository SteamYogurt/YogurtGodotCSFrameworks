using Xunit;

namespace Net.Tests;

public class NetVarMaskUtilTests
{
    [Fact]
    public void BuildMask_AndEnumerate_StayAligned()
    {
        bool[] dirty = { false, true, false, true, true };
        ulong mask = NetVarMaskUtil.BuildMask(dirty.Length, i => dirty[i]);

        Assert.Equal(0b11010UL, mask); // bits 1,3,4

        var indices = NetVarMaskUtil.EnumerateDirtyIndices(mask, dirty.Length);
        Assert.Equal(new[] { 1, 3, 4 }, indices);
    }

    [Fact]
    public void BuildMask_IgnoresDirtyBeyond64()
    {
        ulong mask = NetVarMaskUtil.BuildMask(70, i => i == 10 || i == 65);
        Assert.Equal(1UL << 10, mask);
        Assert.DoesNotContain(65, NetVarMaskUtil.EnumerateDirtyIndices(mask, 70));
    }

    [Fact]
    public void Enumerate_DoesNotEmitBitsPastVarCount()
    {
        // mask 误带高位时，仍按 varCount 截断
        ulong mask = ~0UL;
        var indices = NetVarMaskUtil.EnumerateDirtyIndices(mask, varCount: 3);
        Assert.Equal(new[] { 0, 1, 2 }, indices);
    }

    [Fact]
    public void EmptyMask_YieldsNoIndices()
    {
        Assert.Empty(NetVarMaskUtil.EnumerateDirtyIndices(0, 10));
        Assert.Equal(0UL, NetVarMaskUtil.BuildMask(10, _ => false));
    }
}
