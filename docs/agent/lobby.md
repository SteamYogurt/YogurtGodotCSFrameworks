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
| 列表键 | `Scripts/aFrameworks/Net/Room/OnlineRoomListing.cs` |
| 大厅、建房、房间面板 | `Scripts/MainUI/`、`Scene/UI/Main/` |
| 建房参数 | `Scripts/aFrameworks/Net/Room/GameOnlineContext.cs` |
| 会话根 / 玩家 | `Scripts/Game/Game.cs`、`Scripts/Game/Player.cs` |

`Game` 和 `Player` 是示例。新项目换成自己的会话根，但要守这些约定：

- 会话根：`NetSyncScope.Session`，`MidJoinSyncPriority = NetMidJoinSyncPriority.SessionRoot`
- 玩家：`NetSyncScope.Session`。`LocalSlotIndex` 只给离线多人，不要同步
- 进树时 `NetRoomPresenceSource.Read = ...`，出树时若仍是自己的委托则清空
- 示例的 `InMatch` / `MatchId` 固定为未开局。接入对局时提供真实会话状态，生成对局对象前配置 `RequireMatchAdmission`，由游戏层掌握准入时机

## 创建与加入

`StartCreateLobby(context, displayType)` / `StartJoinLobby(roomId, displayType)`：

1. `EnsureNetworkServices` 启动 `NetManager`，切到 Steam 或 LAN
2. 两种传输创建前设置人数上限。Steam 同时设置 `ELobbyType`（来自 `GameOnlineContext.Visibility`）
3. 房主在 `RoomJoined` 里调用 `OnlineRoomContextApply.ApplyAfterRoomCreated`，再 `CreateOnlineGame`
4. 加入方完成传输握手后发 `OnlineRoomJoined`，等待主机会话根。主机对新连接调用 `SyncForJoiningPeer`，再生成该 peer 的房间玩家

房间名空时用 `OnlineRoomListing.ResolveRoomName`。列表用 `CanJoin`：满员不可加；已开局且 `MidJoinable == false` 仍显示，但不可点。

Steam 和 LAN 都按 `OnlineRoomListing.GameId` 与协议版本过滤。人数、是否开局及能否进房由主机的 `NetRoomPresence` 更新。

LAN 大厅可以输入房主地址直连默认游戏端口。Steam 房主可打开游戏内界面邀请好友。取消等待、退房、被踢、房主断连或进房失败回到原类型大厅。被踢及重入受限分别显示原因。

## 壳

`Main` 启动管理器并跑房间流程。界面在 `Scripts/aFrameworks/UI/UIManager`。`UIManager` 不引用 `Game`。

`Main._Ready` 末尾创建 `UIManager`。没有指定 `menuScene` 时，代码主菜单提供 Steam / LAN 两个入口，打开 `MainLobbyPanel`。`SteamManager.OfferWishlist` 时再给「加入愿望单」。大厅场景优先用 `Main.lobbyPanelScene`，否则用 `res://Scene/UI/Main/MainLobbyPanel.tscn`。

| 调用 | 作用 |
|------|------|
| `UIManager.OpenLobby` / `Main.OpenLobbyPanel` | 打开大厅 |
| `UIManager.ShowOnlineRoom` | 已同步会话根时挂房间面板。场景可由 `Main.onlineRoomPanelScene` 替换 |
| `ShowWaiting` / `HideWaiting` | 等待层。建房、进房时显示，会话根生成后关掉 |
| `ClearTransient` | 关掉菜单和大厅。等待、设置、转场留着 |
| `ShowMenu` | 清掉临时界面并回到主菜单 |
| `Main.ResetAndReturnToMenu` | 关网络、丢掉示例 `Game`、回到主菜单 |

等待层：指定了 `waitingPanelScene` 就用该场景，否则用代码里的 `WaitingOverlay`。转场只在 `transitionScene` 有赋值，或 `res://Scene/UI/Main/Transition.tscn` 存在时启用。设置场景存在时会挂上并先隐藏。打开走 `UIManager.ShowSettings`，`Esc` 只关已打开的设置。语言与套用见 `docs/agent/app.md`。

运行入口 `res://Scene/App/Main.tscn` 把 `Main` 放进场景树，从代码主菜单走房间流程。`AATest` 是草稿，不要抄。

`Game.IsGameEnableEnter` 按人数上限和中途加入策略判断，传输握手执行同一准入判断。

房间面板由 `MainOnlineRoomPanel.SetRoster` 接收房间名、人数上限、房主 ID、本地 ID 和已同步成员。示例接线在 `Main.UI.cs`。新项目换成自己的会话根事件，订阅 `LeaveRequested` 与 `KickRequested`，踢人走 `TransportManager.HostKick`。同目录的 `OnlineRoomTheme.tres` 由大厅、建房和房间共用。
