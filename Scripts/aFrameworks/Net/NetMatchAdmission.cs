using System;
using System.Collections.Generic;

/// <summary>
/// 对局域发送门闩。由会话宿主注册；未注册时 Match 域仍广播给全部客机。
/// </summary>
public static class NetMatchAdmission
{
    /// <summary>
    /// 参数是本机 peer id。返回已准入对局的远端 peer；返回 null 表示退回全员广播。
    /// </summary>
    public static Func<ulong, List<ulong>> CollectRemotePeers;
}
