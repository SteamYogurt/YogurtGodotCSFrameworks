using System;
using System.Collections.Generic;

/// <summary>
/// NetVar 脏标记 bitmask（最多 64），保证收发按同一索引集合序列化。
/// </summary>
public static class NetVarMaskUtil
{
    public const int MaxTrackedVars = 64;

    public static ulong BuildMask(int varCount, Func<int, bool> isDirty)
    {
        ulong mask = 0;
        int n = Math.Min(varCount, MaxTrackedVars);
        for (int i = 0; i < n; i++)
        {
            if (isDirty(i))
                mask |= 1UL << i;
        }
        return mask;
    }

    /// <summary>按 mask 位从低到高列出应序列化/反序列化的变量下标。</summary>
    public static List<int> EnumerateDirtyIndices(ulong mask, int varCount)
    {
        var list = new List<int>();
        int n = Math.Min(varCount, MaxTrackedVars);
        for (int i = 0; i < n; i++)
        {
            if ((mask & (1UL << i)) != 0)
                list.Add(i);
        }
        return list;
    }
}
