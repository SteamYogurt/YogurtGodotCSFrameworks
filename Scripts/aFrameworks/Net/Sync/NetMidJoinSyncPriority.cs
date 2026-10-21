/// <summary>
/// <see cref="INetObject.MidJoinSyncPriority"/> 常用取值。
/// </summary>
public static class NetMidJoinSyncPriority
{
    public const int Default = 0;

    /// <summary>对局根，须在会话根之后、场上其它对局对象之前到达。</summary>
    public const int Match = 50;

    /// <summary>会话根（如 Game），须先于场上其它对象到达客机。</summary>
    public const int SessionRoot = 100;
}
