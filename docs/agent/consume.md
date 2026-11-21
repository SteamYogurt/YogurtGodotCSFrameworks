# 沿用

后续 C# 项目独立建仓。按目录抄，不要整仓当模板。合同只给 Agent 执行。

日常 Desc / 音效 / CSV 译表在并列仓 `../gds-framework`。本仓不管那些运行时。

## 原样抄

| 路径 | 合同 |
|------|------|
| `Scripts/aFrameworks/Net` | [net.md](net.md) |
| `Scripts/aFrameworks/Steam` | [net.md](net.md) |
| `Scripts/aFrameworks/Singleton.cs` | [app.md](app.md) |
| `Scripts/aFrameworks/UserInfo` | [app.md](app.md) |
| `Scripts/aFrameworks/Localization` | [app.md](app.md) |
| `Scripts/aFrameworks/Input` | [input.md](input.md) |
| `Scripts/aFrameworks/Resource` | [resource.md](resource.md) |
| `Scripts/aFrameworks/Object` | [object.md](object.md) |
| `Scripts/UnitCore` | [unit-core.md](unit-core.md) |
| `Scripts/UnitCoreSupport` | [unit-core.md](unit-core.md) |
| `Scripts/Promotion` | [promotion.md](promotion.md) |
| `Scripts/PromotionSupport` | [promotion.md](promotion.md) |

连对应的 `docs/agent/<模块>.md` 和 `.cursor/rules/` 一起抄。

## 抄完再改

| 路径 | 合同 |
|------|------|
| `Scripts/aFrameworks/Main*.cs`、`OnlineRoomContextApply.cs` | [lobby.md](lobby.md) |
| `Scripts/aFrameworks/UI` | [lobby.md](lobby.md) |
| `Scripts/MainUI` | [lobby.md](lobby.md) |
| `Scene/UI/Main/` | [lobby.md](lobby.md) |
| `Scene/App/Main.tscn` | [app.md](app.md) |
| `addons_custom/Settings` | [app.md](app.md) |

换成自己的会话根和菜单。快速验证联机时保留 `Scripts/MainUI/`、`Scene/UI/Main/`，用自己的房间数据接 `MainOnlineRoomPanel.SetRoster`、`KickRequested`、`LeaveRequested`。接线见 `Main.UI.cs` 与 [lobby.md](lobby.md)。不要把示例玩法写进套件目录。

## 不要抄

- `Scripts/Game`
- `Scripts/Temp`
- `AATest`

## 必须改

- `OnlineRoomListing.GameId`
- `SteamManager.AppId`、`StoreAppId`（愿望单打开正式版，不要填 480）

## 必须接

- 会话根：`NetSyncScope.Session`，`MidJoinSyncPriority = NetMidJoinSyncPriority.SessionRoot`
- 玩家：`NetSyncScope.Session`。`LocalSlotIndex` 只给离线，不要做成 `NetVar`
- 进树：`NetRoomPresenceSource.Read = ...`；出树时若仍是自己的委托则清空
- 有对局对象再登记 `NetMatchAdmission.CollectRemotePeers`
- 新同步对象守 [net.md](net.md) 的「新 INetObject」
