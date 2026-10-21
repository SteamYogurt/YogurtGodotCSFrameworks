/// <summary>
/// 网络对象同步域：会话层（进房即可见）与对局层（准入后才同步）。
/// </summary>
public enum NetSyncScope : byte
{
    /// <summary>会话根与玩家等：中途加入、尚未进局也可同步。</summary>
    Session = 0,

    /// <summary>对局对象：仅已准入 peer 接收。未注册准入名单时仍广播给全部客机。</summary>
    Match = 1,
}
