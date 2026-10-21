# 房间与大厅

同一套 UI 服务 Steam 与 LAN。进房后由主机生成会话根 `Game`；加入方等待 Spawn，不在本地再创建一个。

## 分层

```text
MainLobbyPanel / MainLobbyCreatePanel     列表、创建参数
        │
        ▼
Main.RoomFlow                             开网络 → Create/JoinRoom → 等 RoomJoined
        │
        ▼
OnlineRoomContextApply                    把 GameOnlineContext 写到 Steam LobbyData 或 LAN 广播
        │
        ▼
Game.AuthorizedNetSpawn                   仅主机。加入方等网络 Spawn
```

| 层 | 路径 |
|---|---|
| 流程 | `Scripts/aFrameworks/Main.RoomFlow.cs` |
| 上下文入口 | `Scripts/aFrameworks/OnlineRoomContextApply.cs` |
| 列表键 | `Scripts/aFrameworks/Net/OnlineRoomListing.cs` |
| 大厅 UI | `Scripts/MainUI/` |
| 会话根 / 玩家 | `Scripts/Game/Game.cs`、`Scripts/Game/Player.cs` |

`Game` 和 `Player` 是示例。新项目换成自己的会话根，但要守这些约定：

- 会话根：`NetSyncScope.Session`，`MidJoinSyncPriority = NetMidJoinSyncPriority.SessionRoot`
- 玩家：`NetSyncScope.Session`。`LocalSlotIndex` 只给离线多人，不要同步
- 进树时 `NetRoomPresenceSource.Read = ...`，出树时若仍是自己的委托则清空
- 有对局对象时再注册 `NetMatchAdmission.CollectRemotePeers`。示例的 `InMatch` / `MatchId` 固定为未开局，列表字段已经留好

## 创建与加入

`StartCreateLobby(context, displayType)` / `StartJoinLobby(roomId, displayType)`：

1. `EnsureNetworkServices` 启动 `NetManager`，切到 Steam 或 LAN
2. Steam 创建前写入人数上限和 `ELobbyType`（来自 `GameOnlineContext.Visibility`）
3. 房主在 `RoomJoined` 里调用 `OnlineRoomContextApply.ApplyAfterRoomCreated`，再 `CreateOnlineGame`
4. 加入方只发 `OnlineRoomJoined`，等主机的 Spawn

房间名空时用 `OnlineRoomListing.ResolveRoomName`。列表用 `CanJoin`：满员不可加；已开局且 `MidJoinable == false` 仍显示，但不可点。

Steam 列表按 `game_id == OnlineRoomListing.GameId` 过滤。LAN 广播带同一套字段；空 `GameId` 的旧包仍接受。

## 壳

`Main` 启动管理器并跑房间流程。界面在 `Scripts/aFrameworks/UI/UIManager`。`UIManager` 不引用 `Game`。

`Main._Ready` 末尾创建 `UIManager`。没有指定 `menuScene` 时，代码主菜单提供 Steam / LAN 两个入口，打开 `MainLobbyPanel`。大厅场景优先用 `Main.lobbyPanelScene`，否则用 `res://Scene/UI/Main/MainLobbyPanel.tscn`。

| 调用 | 作用 |
|------|------|
| `UIManager.OpenLobby` / `Main.OpenLobbyPanel` | 打开大厅 |
| `ShowWaiting` / `HideWaiting` | 等待层。建房、进房时显示，会话根生成后关掉 |
| `ClearTransient` | 关掉菜单和大厅。等待、设置、转场留着 |
| `ShowMenu` | 清掉临时界面并回到主菜单 |
| `Main.ResetAndReturnToMenu` | 关网络、丢掉示例 `Game`、回到主菜单 |

等待层：指定了 `waitingPanelScene` 就用该场景，否则用代码里的 `WaitingOverlay`。转场只在 `transitionScene` 有赋值，或 `res://Scene/UI/Main/Transition.tscn` 存在时启用。设置场景存在时会挂上并先隐藏。

把 `Main` 放进场景树就能从主菜单走房间流程。编辑器当前运行场景仍是 `AATest/LobbyPanelTest`。

`Game.IsGameEnableEnter` 按人数上限判断。超过上限的新连接由主机 `Kick`。
