# 网络核心

主机权威、星型中继：客机只发主机，主机按包内意图投递或转发。网上不发给自己。

代码在 `Scripts/aFrameworks/Net`。这一层不引用 `Scripts/Game`。

## 分层

| 层 | 职责 |
|---|---|
| `INetTransport` / `TransportManager` | 房间、peer、字节：`Send(peerId)` / `SendToAll(exclude)` |
| `SteamTransport` + `SteamTransport.P2P` | Steam 大厅与可靠 P2P。建房前等 Relay；发送失败会短重试 |
| `LanTransport` / `LanDiscoveryService` | LAN 字节与 UDP 房间广播 |
| `NetManager` | 协议、对象注册、状态/输入同步、Event/RPC/Custom |
| `INetObject` | 可同步对象。会话根与玩家由游戏层实现 |

生命周期：`NetManager.Start()` / `Deactivate()`。入站入口 `ProcessIncoming`。

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
| Event（字符串） | 轻量侧信道（如 Kick） |
| RPC（`byte` id） | 对象上固定语义调用。同一 `NetRPCTable` 内 id 不可覆盖 |
| Custom（`ushort` id，Variant/Raw） | 对象级扩展消息 |
| StateUpdate / Input | 脏标记 `NetVar`。每对象最多 64 个 Full/Input 变量 |

## 同步域

`INetObject.NetSyncScope`：

| 值 | 谁该用 | 发送 |
|---|---|---|
| `Session` | 会话根、玩家 | 全部客机 |
| `Match`（默认） | 对局对象 | 仅 `NetMatchAdmission.CollectRemotePeers` 返回的 peer |

未注册 `CollectRemotePeers`，或它返回 null 时，Match 域仍广播给全部客机。有对局流的项目在开局后注册这个委托；没有对局流就不要注册。

中途加入快照按 `MidJoinSyncPriority` 降序，同优先级按 netId 升序。常用值在 `NetMidJoinSyncPriority`：`SessionRoot = 100`，`Match = 50`，`Default = 0`。会话根必须先到。

`SyncAllNetObjectsToPlayer` 推两域。`SyncObjectsToPlayer(peerId, scope)` 只推指定域。

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

## 房间发现快照

`LanDiscoveryService` 与 Steam LobbyData 读 `NetRoomPresenceSource.Read`。会话宿主在进树时注册，出树时清空。字段：人数、上限、可加入、允许中途加入、是否已开局、对局 id。

列表键见 `OnlineRoomListing`。新项目只改 `GameId`。

## 已知边界

- 无主机迁移。Steam 传输记下进房时的原始房主，Owner 转移不改变主机；原始房主离开即 `HostQuit`
- 无不可靠信道 / AOI
- 线协议 `Action` / `Request` 槽位未实现，不要用
- `TransportManager.HostKick` 向目标发 `Kick`。收到后由示例壳 `Main.OnKicked` 退房
