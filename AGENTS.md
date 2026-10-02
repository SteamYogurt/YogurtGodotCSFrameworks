# 框架项目 — Agent 入口

C# / Godot 4.7 的可复用套件。后续项目的 Agent 按模块取用：先读本文件和 `.cursor/rules/`，再只打开任务对应的 `docs/agent/<模块>.md`。

不要凭记忆改顶层目录，也不要把示例玩法写进套件核心。

## 语言

- 运行时逻辑写 **C#**。GDScript 只留在 `addons/`、`addons_custom/` 插件里。
- 物理引擎是 **Jolt**。渲染器是 **Forward Plus**。
- 联机是主机权威、星型中继。一个连接对应一个本地玩家；同机多本地玩家只用于离线。

## 套件与示例

| 路径 | 角色 | 合同 |
|------|------|------|
| `Scripts/aFrameworks/Net` | 套件：传输、协议、同步域 | `docs/agent/net.md` |
| `Scripts/aFrameworks` 里的 `Main*.cs`、`OnlineRoomContextApply.cs` | 示例壳：房间编排 | `docs/agent/lobby.md` |
| `Scripts/aFrameworks/UI` | 示例壳：`Main` 的界面层 | `docs/agent/lobby.md` |
| `Scripts/aFrameworks/Object` | 套件：场景对象池 | `docs/agent/object.md` |
| `Scripts/MainUI` | 示例：Steam / LAN 大厅 UI | `docs/agent/lobby.md` |
| `Scripts/Game` | 示例：`Game` / `Player`。不是套件 | `docs/agent/lobby.md` |
| `Scripts/UnitCore` | 套件：单位、属性、伤害、Buff | `docs/agent/unit-core.md` |
| `Scripts/UnitCoreSupport` | 套件：对局上下文 | `docs/agent/unit-core.md` |
| `Scripts/Promotion`、`Scripts/PromotionSupport` | 套件：词条 | `docs/agent/promotion.md` |
| `Scripts/aFrameworks/Input` | 套件：输入 | `docs/agent/input.md` |
| `Scripts/aFrameworks/Resource` | 套件：资源目录 | `docs/agent/resource.md` |
| `Tools/`、`.cursor/skills/` | 套件：检查与出图出模 | `docs/agent/tools.md` |
| `Scripts/Temp`、`AATest` | 草稿。不要当套件抄 | — |

套件核心不得引用 `Scripts/Game` 或 `Scripts/Temp`。示例壳（`Main`、`MainUI`）可以引用 `Game`。

## 工具路由

| 意图 | 打开 |
|------|------|
| 联机协议 / 同步 | `docs/agent/net.md` |
| 建房、进房、大厅列表 | `docs/agent/lobby.md` |
| 单位、伤害、Buff | `docs/agent/unit-core.md` |
| 词条 | `docs/agent/promotion.md` |
| 输入 | `docs/agent/input.md` |
| 资源目录 | `docs/agent/resource.md` |
| 场景对象池 | `docs/agent/object.md` |
| 出图 | `.cursor/skills/bailian-cli/SKILL.md` |
| 出模 | `.cursor/skills/tripo-cli/SKILL.md` |
| 检查 `.gd` 解析 | `python Tools/gdscript-check/check.py` |
| 纯逻辑网络测试 | `dotnet test Tests/Net.Tests/Net.Tests.csproj` |

凭据在用户环境里。不要读、不要写入仓库、不要在回复里回显 Key。

## 停检点

- 新代码按模块分夹。一个目录超过约 10 个同级文件时先拆夹。
- 改套件边界、同步域或房间列表键时，同步对应的 `docs/agent/*.md`。
- 草稿只进 `Tools/<工具>/tmp/`。运行时不引用 `res://Tools/`。
- 出图、出模先看 skill。网格落到 `Assets/Art/Gen/<id>/`。
