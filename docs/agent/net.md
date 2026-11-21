# 网络核心

主机权威、星型中继：客机只发主机，主机按包内意图投递或转发。网上不发给自己。

代码在 `Scripts/aFrameworks/Net`。Steam 初始化在 `Scripts/aFrameworks/Steam`。这一层不引用 `Scripts/Game`。

```text
Net/              NetManager、INetObject、TransportManager
Net/Transport/    Steam / LAN 字节
Net/Sync/         域、NetVar
Net/Wire/         flags、路由、表、Id
Net/Room/         列表键、准入、发现
Steam/            SteamManager、SteamStore（Demo / 商店页规则）
```

## 分层

| 层 | 职责 |
|---|---|
| `INetTransport` / `TransportManager` | 房间、peer、字节：`Send(peerId)` / `SendToAll(exclude)` |
| `SteamManager` | Steam 初始化、Persona、Relay、商店 / 愿望单。Demo 合同在 [app.md](app.md) |
| `SteamTransport` + `SteamTransport.P2P` | Steam 大厅、Relay、成员握手、逐 peer 可靠发送队列 |
| `LanTransport` / `LanDiscoveryService` | TCP 游戏数据与 UDP 房间发现。握手、读写在异步 I/O 路径执行，游戏回调留在主线程 |
| `NetManager` | 协议、对象注册、状态/输入同步、Event/RPC/Custom |
| `INetObject` | 可同步对象。会话根与玩家由游戏层实现 |

生命周期：`NetManager.Start()` / `Deactivate()`。入站入口 `ProcessIncoming`。

- 两端须使用同一 `NetWireLimits.ProtocolVersion`。Steam `protocol` 和 LAN `ProtocolVersion` 用于发现过滤，握手也校验版本。版本 3 增加踢出与拒绝重入的传输控制，同版本游戏同步字段须一致。
- Steam 只与该大厅成员建立游戏会话。客机仅接收原始房主的包。Hello / Welcome 和游戏消息使用同一可靠通道，房主在 Welcome 入队后才通知游戏层同步。发送缓冲满时按 peer FIFO 补发；队列超限、超时或会话失败会移除对应连接。
- LAN TCP 按长度分帧，来源由连接确定。握手完成后才触发 `RoomJoined`。每连接收发队列有字节及包数上限，连接操作及整帧读写有超时。传输层队列持续饱和时关闭该连接。
- 传输消息上限为 512 KiB。一次传输消息可含多条完整的内层消息，长度头和消息体不得跨传输包。

## 发送路由

### `NetSendFlags`（网上谁收，写入包内）

| Flags | 含义 |
|---|---|
| `Host` / `HostOnly` | 仅主机 |
| `Clients` | 各客机（不含发送端自己） |
| `AllOthers` | `Host \| Clients` |

### `alsoRunLocally`（与 flags 正交）

- `true`（默认）：调用方本地执行一次
- `false`：调用方不执行，只出站

```text
客机出站 → 永远只发主机（flags 在包内）
主机收到 → 若含 Host 则本地执行；若含 Clients 则转发给其他客机（排除原发送者，无回声）
主机出站 → 若含 Clients 则 SendToAll
```

| 意图 | 写法 |
|---|---|
| 全员（含自己立刻跑） | `AllOthers` + `alsoRunLocally: true`（默认） |
| 只同步别人 | `AllOthers` + `alsoRunLocally: false` |
| 只给主机 | `HostOnly` + `alsoRunLocally: false` |
| 客机预测 + 通知主机 | `HostOnly` + `alsoRunLocally: true` |

请求经主机裁决后再全员确认：用第二次下发，不要依赖原包回声。

点对点不走 flags：`SendEventToPeer` / `SendRPCToPeer` / `SendCustomPacketToPeer`。包内 flags 字节为 `0x80`，后接 `peerId u64`；客机经主机中转。

## 消息通道

| 通道 | 用途 |
|---|---|
| Event（字符串） | 应用级轻量侧信道 |
| RPC（`byte` id） | 对象上固定语义调用。同一 `NetRPCTable` 内 id 不可覆盖 |
| Custom（`ushort` id，Variant/Raw） | 对象级扩展消息 |
| StateUpdate / Input | 脏标记 `NetVar`。每对象最多 64 个 Full/Input 变量 |
| MatchReset | 主机通知指定客机卸载已同步的对局对象 |

主机专用消息包括生成、初始状态、Full State、销毁和 MatchReset。客机的输入由主机使用传输来源调用对象的 `CanReceiveInputFrom` 校验。客机对象 RPC / Custom 受同步域准入和 `CanReceiveRemoteCallFrom` 约束。玩法级参数和权限仍由对象处理器验证。

## 同步域

`INetObject.NetSyncScope`：

| 值 | 谁该用 | 发送 |
|---|---|---|
| `Session` | 会话根、房间玩家和 UI 状态 | 全部已完成握手的客机 |
| `Match`（默认） | 对局对象 | 仅已准入客机，或使用默认的全员模式 |

- 有对局流的项目，在生成对局对象前设 `RequireMatchAdmission = true`，并由主机对参战 peer 调用 `AdmitPeerToMatch`。房间能否接收新连接由 `NetRoomPresence` 管理。
- 新客机由房间壳调用 `SyncForJoiningPeer`。它先推会话域，已获对局权限时再推对局域。客机加载完成后，主机可用 `AdmitPeerToMatch` 补发当前对局快照，之后才把该 peer 纳入对局实时发送。显式准入随断线或退房清除。
- `RevokePeerFromMatch` 停止该 peer 的对局收发，并让客机按快照的逆序卸载对局对象。会话对象持续同步，可再次准入。
- 旧项目的 `NetMatchAdmission.CollectRemotePeers` 仍可提供默认对局选人。显式准入和撤销优先；没有注册选人委托且未启用准入时，Match 继续全员同步。

快照先发会话域，每个域内按 `MidJoinSyncPriority` 降序，同优先级按 netId 升序。常用值在 `NetMidJoinSyncPriority`。会话根优先级为 `SessionRoot`。快照分批发送，每批内对象的 Spawn 和 Initial 相邻。Full State 和 Input 在同步 tick 内批量发送，Full State 按域分别组包。

`SyncAllNetObjectsToPlayer` 遵循当前对局准入。`SyncObjectsToPlayer(peerId, scope)` 可只推指定域。Spawn、Destroy、Full State、Input、对象 RPC 和 Custom 都受同一对局域准入约束。

排序与过滤的纯逻辑在 `NetSyncSelectUtil`，测试：

```text
dotnet test Tests/Net.Tests/Net.Tests.csproj
```

## 对象生命周期

生成只在主机：`HostInitialize` → 分配 netId → `SpawnObj` + `InitialPacket`。远端 lazy 占位，写初始状态，再 `INetSpawn`。

销毁只在主机：广播 `DestroyObj`，各方 `INetDestroy` 并注销。

乱序防护（`pendingDestroyIds`）：

- Destroy 先于 Spawn/Initial：忽略后续生成
- Destroy 落在 lazy：销毁占位并保留 pending，直到 Initial 被消费
- 重复 Spawn/Initial：忽略
- lazy 实例化失败：记入 pending

Full State 由主机推脏 `GetFullStateVars`。Input 由有 Authority 的客机推脏 `GetInputStateVars` 给主机。

`HasAuthority` 是对象输入权，不是「是不是主机」。

## 新 INetObject

游戏层实现。只主机调用生成。

必须：

- `HostInitialize`：仅主机，分配 netId 之前
- `HasAuthority`：输入权，不是「是不是主机」
- 客机主动发送 Input 的对象实现 `CanReceiveInputFrom`，由主机按连接 ID 校验。默认拒绝
- `NetSyncScope`：会话根 / 玩家 = `Session`；对局对象保持默认 `Match`
- `MidJoinSyncPriority`：会话根 = `NetMidJoinSyncPriority.SessionRoot`
- `GetFullStateVars` / `GetInputStateVars`
- `GetNetRPCTable`
- `INetSpawn` / `INetDestroy`

不要：

- 把 `LocalSlotIndex` 做成 `NetVar`
- 在客机本地再创建一个会话根
- 用未实现的线协议槽位 `Action` / `Request`

## 房间发现快照

`LanDiscoveryService` 与 Steam LobbyData 读 `NetRoomPresenceSource.Read`。会话宿主在进树时注册，出树时清空。字段：人数、上限、可加入、允许中途加入、是否已开局、对局 id。

列表键见 `OnlineRoomListing`。新项目只改 `GameId`。

## 房主踢人

- `TransportManager.HostKick(peerId)` 移除指定已连接客机并拒绝其重入当前房间。返回 `false` 时成员名单未改变。临时拒绝槽位可用 `blockRejoin: false`。
- Steam 按账号 ID 拒绝 Hello，由原始房主发出带目标 ID 的大厅通知，客机收到后退出 Steam 大厅。LAN 关闭 TCP 连接，在握手阶段按房主观察到的来源 IP 拒绝重入。共享该 IP 的新连接也会被拒绝。
- 客机被踢时传输会退出房间并触发 `TransportManager.Kicked`。重入被拦截时触发 `RoomJoinFailed`，通过 `INetTransport.LastJoinWasBlocked` 区分原因。
- 拒绝名单随房主关房或重新建房清空。Steam 平台大厅成员资格仍由 Steam 管理，游戏能否加入由房主握手决定。

## 已知边界

- 原始房主离开即触发 `HostQuit`。Steam Owner 转移不迁移游戏主机
- 无不可靠信道 / AOI
- 线协议 `Action` / `Request` 槽位未实现，不要用
