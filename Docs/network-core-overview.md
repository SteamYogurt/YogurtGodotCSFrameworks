# 网络核心原理说明

## 总览
本项目的网络核心由以下几部分组成：
- `INetObject`：网络对象约定。
- `NetManager`：网络对象注册、消息分发、状态同步、生成销毁同步中心。
- `TransportManager` 与具体 Transport：底层传输层封装，负责收发字节流。
- `Game`：当前局内核心状态与玩家管理。
- `Player`：最小化的玩家网络状态对象。

整体设计原则：
- 传输层只负责可靠收发与玩家列表变化。
- `NetManager` 负责协议打包、解析、对象生命周期和状态同步。
- `Game` 只负责房间/局内核心状态与玩家集合管理。
- `Player` 只保留核心身份和本地输入槽位等基础状态。

## INetObject 约定
每个网络对象需要实现以下核心能力：
- `HostInitialize()`：仅主机生成对象时调用，用于初始化权威状态。
- `HasAuthority()`：当前节点是否拥有输入/状态控制权。
- `GetInputStateVars()`：输入同步变量列表。
- `GetFullStateVars()`：完整状态同步变量列表。
- `GetNetRPCTable()`：RPC 分发表。
- `INetSpawn()`：对象完成初始同步后进入游戏逻辑。
- `INetDestroy()`：对象销毁前清理逻辑。
- `IsNetInvalid()`：对象是否已经失效。

规范：
- 内部网络字段使用小写 `NetVar`。
- 对外只暴露大写强类型属性。
- `GetFullStateVars()` 返回顺序必须稳定，不能随意变动。

## 网络对象生命周期
### 主机生成
1. 主机调用 `Game.AuthorizedNetSpawn()`。
2. 对象先执行 `HostInitialize()`。
3. `NetManager.HostSpawnNetObject()` 为对象分配网络 ID。
4. `NetManager` 打包两段消息：
   - `SpawnObj`
   - `InitialPacket`
5. 远端先收到 `SpawnObj`，创建 lazy 占位对象。
6. 远端再收到 `InitialPacket`，把完整状态写入对象。
7. 远端调用 `INetSpawn()`，对象正式进入场景。

### 主机销毁
1. 主机调用 `Game.AuthorizedNetDestroy()`。
2. `NetManager.HostDestroyNetObject()` 广播 `DestroyObj`。
3. 本地对象执行 `INetDestroy()`。
4. 远端收到 `DestroyObj` 后：
   - 若对象已完成初始化，则执行 `INetDestroy()` 并移除注册。
   - 若对象仍是 lazy 占位状态，则直接销毁占位对象，避免脏残留。

## 异步时序健壮性策略
为应对网络乱序与重复包，当前实现加入以下保护：
- 重复 `SpawnObj`：忽略，并打印错误日志。
- 重复 `InitialPacket`：忽略，避免重复初始化。
- 先收到 `DestroyObj`，后收到 `InitialPacket`：通过待销毁 ID 集合忽略后续初始化。
- lazy 对象实例化失败：记录待销毁状态，避免后续继续进入脏流程。
- 主机定向事件转发时重新补齐消息头和长度，避免目标端误解析。

## 状态同步原理
### Full State
- 主机周期性遍历所有网络对象。
- 读取 `GetFullStateVars()`。
- 按脏标记生成 bitmask。
- 只序列化发生变化的字段。
- 客户端收到后按同样顺序回填到对应 `NetVar`。

### Input State
- 客户端只对自己拥有 Authority 的对象发送输入状态。
- 主机读取 `GetInputStateVars()`，再决定如何驱动权威状态。

### RPC 与 Custom Packet
- `RPC`：适合固定编号、固定语义的对象方法调用。
- `Custom Packet`：适合对象级自定义扩展消息。
- 两者都通过 `NetManager` 完成对象 ID 路由。

## Game 的职责
`Game` 只保留核心状态：
- `IsOnline`
- `MidJoinable`
- `MaxPlayerCount`
- `IsAuthorized`
- 本地协作输入上下文
- 玩家集合管理

### 玩家集合管理
当前 `Game` 维护三套核心结构：
- `Players`：当前所有玩家列表。
- `LocalPlayers`：当前所有本地玩家列表。
- 按玩家 ID 的内部字典：保证去重、查找和异步增删稳定。

设计原则：
- 外部读取玩家状态，优先使用强类型属性。
- 在线模式下，本地玩家由传输层 `LocalID` 对应的网络玩家决定。
- 离线模式下，可根据 `LocalPlayerCount` 生成多个本地玩家。
- 本地玩家生成时必须写入 `LocalSlotIndex`，供输入系统分配设备与动作槽位。

## Player 的职责
`Player` 只保留基础核心状态：
- `PlayerId`
- `PlayerName`
- `LocalSlotIndex`
- `IsLocal`
- `IsReady`

不在此层承载复杂表现、战斗、移动或 UI 逻辑。
这些内容后续应由更高层模块按框架扩展。

注意：
- `LocalSlotIndex` 是纯本地运行时属性，不参与网络同步。
- 联机模式不支持多个本地玩家。
- `LocalPlayers` 仅用于离线本地多人。

## 输入系统与本地槽位
`InputManager` 已与 `Game` 解耦。
它只接收外部提供的本地输入上下文：
- 是否本地多人
- 本地玩家数量

输入槽位规则：
- 每个本地玩家有独立 `LocalSlotIndex`。
- 根据槽位决定该玩家使用键鼠还是指定手柄。
- 离线模式下会为每个本地玩家直接分配槽位。
- 联机模式下不会为网络 `Player` 维护本地槽位。

## 当前边界与建议
### 已保证的内容
- 生成/销毁/初始状态同步具备基本乱序防护。
- Game 玩家集合支持按 ID 去重与稳定管理。
- 离线本地多人已具备基础支持。
- 外部网络状态访问已统一为强类型属性。

### 后续建议
- 若未来支持在线 + 单机多本地混合输入，需要在传输层明确“一个连接映射多个本地玩家”的协议。
- 可为 `lazyIdToObject` 增加超时回收，防止极端丢包时长期残留。
- 可进一步把协议写包过程抽象成专用 writer，减少手写长度计算。
- 可增加专门的网络回归测试，覆盖：
  - 重复 spawn
  - destroy 先于 initial
  - 玩家列表频繁抖动
  - 离线多本地玩家生成
