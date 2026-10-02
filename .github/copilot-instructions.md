# Copilot Instructions

先读根目录 `AGENTS.md` 和 `docs/agent/` 里对应模块的合同。

- 联机不支持多个本地玩家。本地玩家列表只用于离线。
- `Player.LocalSlotIndex` 不是网络字段。
- 套件核心（`Scripts/aFrameworks/Net`、`UnitCore`、`Promotion`）不要引用 `Scripts/Game` 或 `Scripts/Temp`。
- 界面走 `UIManager`。对局状态在 `MatchContext`，由 `CombatRuntime.BeginMatch` / `EndMatch` 管。场景对象用 `ObjectPoolManager.Spawn` / `Release`。
