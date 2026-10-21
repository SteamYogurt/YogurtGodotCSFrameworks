/// <summary>
/// 中途加入 / 准入快照的排序与域过滤。无 Godot 依赖，供 NetManager 与单元测试共用。
/// </summary>
public static class NetSyncSelectUtil
{
    /// <summary>
    /// 优先级降序，同优先级按 netId 升序。
    /// </summary>
    public static int CompareMidJoinOrder(int priorityA, uint netIdA, int priorityB, uint netIdB)
    {
        int byPriority = priorityB.CompareTo(priorityA);
        return byPriority != 0 ? byPriority : netIdA.CompareTo(netIdB);
    }

    /// <summary>
    /// <paramref name="filter"/> 为 null 时包含全部域；否则仅匹配指定域。
    /// </summary>
    public static bool IncludeInSync(NetSyncScope objectScope, NetSyncScope? filter)
        => filter == null || objectScope == filter.Value;
}
