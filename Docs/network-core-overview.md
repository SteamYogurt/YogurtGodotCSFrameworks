# 网络核心

主机权威、星型中继：客机只发主机，主机按包内意图投递/转发。网上不发给自己。

## 分层

| 层 | 职责 |
|---|---|
| `INetTransport` / `TransportManager` | 房间、peer、字节：`Send(peerId)` / `SendToAll(exclude)` |
| `NetManager` | 协议、对象注册、状态/输入同步、Event/RPC/Custom |
| `INetObject` / `Game` / `Player` | 可同步对象与局内状态 |

生命周期：`NetManager.Start()` / `Deactivate()`；入站入口 `ProcessIncoming`。

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

常用写法：

| 意图 | 写法 |
|---|---|
| 全员（含自己立刻跑） | `AllOthers` + `alsoRunLocally: true`（默认） |
| 只同步别人 | `AllOthers` + `alsoRunLocally: false` |
| 只给主机 | `HostOnly` + `alsoRunLocally: false` |
| 客机预测 + 通知主机 | `HostOnly` + `alsoRunLocally: true` |

请求→主机裁决→全员确认：用二次下发（确认 RPC/状态），不要依赖原包回声。

### 点对点（不用 flags）

- `SendEventToPeer` / `SendRPCToPeer` / `SendCustomPacketToPeer` / `SendCustomRawPacketToPeer`
- `INetObject`：`SendNetRPCToPeer`、`SendNetCustomPacketToPeer` 等

包内 flags 字节为 `0x80`，后接 `peerId u64`；客机经主机中转。目标是自己时只走 `alsoRunLocally`。

## 消息通道

| 通道 | 用途 |
|---|---|
| Event（字符串） | 轻量侧信道（如 Kick） |
| RPC（`byte` id） | 对象上固定语义调用 |
| Custom（`ushort` id，Variant/Raw） | 对象级扩展消息 |
| StateUpdate / Input | 脏标记 `NetVar` 同步（每对象最多 64 个 Full/Input 变量） |

## `INetObject`

- `HostInitialize` / `INetSpawn` / `INetDestroy` / `IsNetInvalid`
- `HasAuthority`：本机是否拥有该对象输入权
- `GetFullStateVars` / `GetInputStateVars`：顺序必须稳定
- `GetNetRPCTable` / `GetNetCustomPacketTable`
- 字段用小写 `NetVar`，对外大写属性

便捷发送：`SendNetRPC` / `SendNetCustomPacket` / `SendNetCustomRawPacket`（及对应 `ToPeer`）。

## 对象生命周期

**生成（仅主机）**  
`Game.AuthorizedNetSpawn` → `HostInitialize` → 分配 netId → 广播 `SpawnObj` + `InitialPacket`  
远端：lazy 占位 → 写初始状态 → `INetSpawn`

**销毁（仅主机）**  
`AuthorizedNetDestroy` → 广播 `DestroyObj` → 各方 `INetDestroy` 并注销

**中途加入**  
主机 `SyncAllNetObjectsToPlayer(peerId)` 重放场上对象。

**乱序防护**（`pendingDestroyIds`）  
- Destroy 先于 Spawn/Initial：忽略后续生成/初始化  
- Destroy 落在 lazy：销毁占位并保留 pending，直到 Initial 被消费  
- 重复 Spawn/Initial：忽略  
- lazy 实例化失败：记入 pending

## 状态与输入

- **Full State**：主机周期推送脏 `GetFullStateVars`
- **Input**：有 Authority 的客机推送脏 `GetInputStateVars` 给主机

## Game / Player（边界）

`Game`：在线标记、人数、主机权（`IsAuthorized`）、玩家集合、离线本地多人上下文。  
`Player`：身份与就绪等；不承载战斗/移动/UI。

- 联机：一个连接对应一个本地玩家（不支持同机多本地）
- 离线：可用多个 `LocalPlayers` + `LocalSlotIndex`（纯本地，不同步）
- `HasAuthority`（对象输入权）≠ `Game.IsAuthorized`（是否主机）

## 已知边界

- 无主机迁移；主机退出即会话结束
- 无不可靠信道 / AOI；全可靠 + 全量对象状态扫描
- 线协议 `Action` / `Request` 槽位未实现，勿用
