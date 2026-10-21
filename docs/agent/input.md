# 输入

代码在 `Scripts/aFrameworks/Input`。

| 文件 | 职责 |
|---|---|
| `InputManager.cs` | 捕获项目默认绑定，套用 `user://input_config.tres`，跟踪最近使用的设备 |
| `InputConfigResource.cs` | 玩家改键存档 |
| `InputRemapButton.cs` | 改键按钮 |

`LocalInputContext` 只描述离线本地人数。联机时一个连接一个本地玩家，不要把槽位写进 `NetVar`。

`Game` 示例在进树时 `SetLocalInputContext`，出树时 `ClearLocalInputContext`。新项目的会话根做同样的事。

设备变化走 `DeviceTypeChanged`。重置绑定走 `BindingsReset`。默认绑定从项目 InputMap 捕获，不要在代码里再写一份会覆盖备选键的单值表。
