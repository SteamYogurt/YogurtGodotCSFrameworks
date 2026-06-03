# MainUI 联机大厅说明

## 概览
本次新增了 MainUI 联机大厅层，统一兼容 Steam 与 LAN 两种大厅来源。

## 组成
- `Scripts/MainUI/MainLobbyPanel.cs`
  - 主大厅面板。
  - 通过 `DisplayType` 指定显示 Steam 或 LAN。
  - 导出按钮：加入房间、刷新房间、创建房间、返回。
- `Scripts/MainUI/MainLobbyCreatePanel.cs`
  - 创建房间面板。
  - 隐藏上一个大厅面板，取消时恢复并重新处理焦点。
- `Scripts/MainUI/MainLobbyRoomItem.cs`
  - 房间条目按钮。
- `Scripts/MainUI/UIUtils.cs`
  - 目前提供 `FindFirstFocusButton(Control root)`。
- `Scene/UI/Main/MainLobbyPanel.tscn`
- `Scene/UI/Main/MainLobbyCreatePanel.tscn`
- `Scene/UI/Main/MainLobbyRoomItem.tscn`

## 使用方式
大厅面板不要求动态切换模式，入树前写入参数即可：

- `Main.Instance.OpenLobbyPanel(ENetLobbyDisplayType.Steam)`
- `Main.Instance.OpenLobbyPanel(ENetLobbyDisplayType.Lan)`

## 当前流程
### 进入大厅
1. 大厅面板 `_Ready()` 内调用 `Main.Instance.EnsureNetworkServices(DisplayType)`。
2. 该方法会：
   - 启动 `NetManager`
   - 切换 `TransportManager` 到 Steam 或 LAN
   - 停止 `LanDiscoveryService` 的旧状态
   - 监听当前 `INetTransport.RoomStateChanged`

### 刷新大厅
- Steam：调用 `SteamManager.RequestLobbyList()`。
- LAN：调用 `LanDiscoveryService.StartBrowsing()` 并读取本地广播缓存。

### 创建房间
1. 在创建面板填写参数后调用 `Main.Instance.StartCreateLobby(...)`。
2. Main 会清理当前普通 UI，并显示 `WaitingPanel`。
3. LAN：创建房间成功后开始 `LanDiscoveryService.StartHosting(roomName)`。
4. Steam：等待自己真正进入 Lobby 后，写入 Lobby 元数据。
5. 两种模式都会在入房成功后触发 `Main.OnlineRoomCreated`。
6. 这里预留给外部去做真正的 Game 实例生成与初始化。
   - 当前只把 `Game.PendingOnlineContext` 设好。
   - 具体游戏创建逻辑你可以在监听 `Main.OnlineRoomCreated` 时接入。

### 加入房间
1. 点击房间条目后选中。
2. 点击加入房间后调用 `Main.Instance.StartJoinLobby(...)`。
3. Main 会显示 `WaitingPanel`。
4. 成功进入房间后触发 `Main.OnlineRoomJoined`。
5. 按你的要求，目前只保证 `WaitingPanel` 可见，其余逻辑不在这层继续处理。

## Steam 相关补充
- `SteamManager` 新增大厅列表读取能力。
- `SteamManager.ApplyCurrentLobbyMetadata(GameOnlineContext)` 会在创建者进入 Lobby 后写入：
  - 房间名
  - 可加入状态
  - 最大人数
  - 可见性
- `SteamTransport.PendingCreateMaxPlayers` 用于把创建面板的最大人数传给 Steam Lobby 创建接口。

## LAN 相关补充
- `LanDiscoveryService` 生命周期由 MainUI 面板和 Main 统一控制。
- 退出大厅或返回菜单时会调用 `StopAll()`，避免浏览线程或广播线程残留。

## 焦点规则
- 主大厅显示时优先抓取第一个可聚焦按钮。
- 创建面板显示时优先聚焦房间名输入框。
- 创建面板取消后恢复主大厅可见，并重新抓取焦点。

## 预留扩展点
- `Main.OnlineRoomCreated`
- `Main.OnlineRoomJoined`
- `SteamLobbyRoomInfo`

这几个点适合后续继续接入你的 Game 实例创建、房间内状态同步和更复杂的大厅表现。
