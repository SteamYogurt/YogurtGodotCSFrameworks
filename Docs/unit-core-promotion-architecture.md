# UnitCore / Promotion 架构说明

本文档概括本对话中对 **战斗核心（UnitCore）**、**支撑层（UnitCoreSupport / PromotionSupport）** 与 **遗物系统（Promotion）** 的设计与改造，便于查阅。未改动的游戏层（`Game`、塔防实体等）不在此展开。

---

## 1. 总体分层

```
UnitCore          — 单位、属性、伤害、Buff（与具体游戏解耦）
UnitCoreSupport   — 本地化、全局事件、飘字接口、单位控制等
PromotionSupport  — 遗物事件总线、服务注入、Filter、UnitCore 桥接
Promotion         — 遗物资源、效果、条件、动作、运行时管理
```

**原则：** 核心只依赖 `IUnit` 与通用类型；游戏特化（Monster、Building、Projectile、Turn 等）不进入核心，由游戏层通过 `PromotionServices` / `PromotionEventBus` 扩展。

---

## 2. UnitCore：战斗内核

### 2.1 模块结构

| 区域 | 职责 |
|------|------|
| `IUnit` / `UnitInfo` / `UnitCommon` | 单位接口、初始数值、伤害入口（`CalculateDamage` / `ExecuteDamage`） |
| `Stat` / `StatCollection` | 属性修饰符（加算 / 乘算 / 最终乘算），与 `IUnit` 字段可分离维护 |
| `DamageContext` / `DamageResolver` | 分阶段伤害：Outgoing → Critical → Defense → Incoming → Final |
| `DamageModifier` + Controller | 可配置的伤害修正（标签、Filter、Buff 条件等） |
| `Buff` / `BuffInstance` / `BuffController` | Buff 生命周期、叠层、视觉染色 |
| `BuffModifier` + Resolver | Buff 数值（持续时间、跳伤等）的全局修正 |

### 2.2 属性读写（刻意设计）

- `CalculateDamage` 等仍读取 **`IUnit.PhysicalAttackDamage` 等字段**，不强制每次从 `StatCollection` 回写。
- Buff 通过 `StatCollection` 挂修饰符；具体单位实现负责在合适时机把集合结果同步到字段（「聪明读取」）。

### 2.3 与游戏层解耦

| 旧耦合 | 现方案 |
|--------|--------|
| `Game.instance` 伤害回调 | `UnitCoreEvents`（静态事件） |
| 全局修正器挂 Game | `UnitCoreModifiers.GlobalOwner` |
| 飘字 / TempLabel | `DamageFeedback.SpawnText` 委托（默认 null） |
| `Projectile` / `PromotionEffect` 在 `DamageContext` | 仅保留 `SourceObject`，攻击者用 `IUnit` |

### 2.4 P0 性能优化（已实现）

1. **收集池化：** `DamageModifierCollectSession` / `BuffModifierCollectSession` 复用列表与 `HashSet`，支持嵌套伤害（depth 栈）。
2. **按阶段分桶：** 收集时按 `ApplyStage` 入桶；应用时只扫当前阶段，不再五遍全表 + `IsMatchStage`。
3. **有序插入：** Controller `AddModifier` 按 Priority 插入，收集阶段不再 `Sort`。
4. **Buff 查询：** `BuffController` 用 `_buffsById` + for 循环，替代 LINQ `Any`。

---

## 3. UnitCoreSupport

| 文件 | 作用 |
|------|------|
| `UnitCoreLocalization` + `GlobalUsings` | 全局 `Tr()` |
| `UnitCoreEvents` | 四种伤害阶段事件 |
| `UnitCoreModifiers` | `GlobalOwner` 占位对象 |
| `DamageFeedback` | 飘字钩子 |
| `UnitControl` | `UnitControlFlag` / `IUnitControlHost`（眩晕等） |

Filter 基类已迁至 **PromotionSupport**（见下），与 Promotion、DamageModifier 共用。

---

## 4. PromotionSupport

| 文件 | 作用 |
|------|------|
| `ObjectFilter` / `UnitFilter` / `DamageContextFilter` | 最简 Filter，可继承扩展 |
| `PromotionEventBus` | 遗物侧唯一事件订阅点；`Subscribe` 返回 `IDisposable` |
| `PromotionUnitCoreBridge` | `UnitCoreEvents` → `ConditionContext` → Bus |
| `PromotionServices` | `ActiveUnits`、`SpawnEffectAtPosition`、`NotifyUnitSpawned` 等注入点 |
| `GlobalModifierOwner` | 与 `UnitCoreModifiers.GlobalOwner` 对齐 |

---

## 5. Promotion：遗物系统

### 5.1 生命周期（核心改进）

```
PromotionManager.Fetch(promotion)
  → PromotionInstance（DeepCopy 各 PromotionEffect）
  → 每个 effect.Activate(context) → PromotionEffectHandle
  → Handle 登记：事件订阅、cleanup、modifier 等
  → Raise(OnAcquire)

Revert / Deactivate → Handle.Dispose() → 对称退订与清理
```

- 废弃分散的 `OnFetch` / `OnRevert` 直接挂 `Game`。
- 所有副作用必须通过 **Handle** 登记，避免泄漏。

### 5.2 统一 Condition

- 原：`filter` + `chance` + `OnEventCondition[]` 三套并行。
- 现：**仅** `PromotionAction.conditions[]`，包含：
  - `Condition_Filter`（ObjectFilter + subjectKey）
  - `Condition_Chance`
  - `Condition_All` / `Condition_Any`
  - `Condition_DamageTag` / `Condition_HasBuff`
- **不含** `Condition_Turn` 及一切回合/GameStatus 特化。

执行：`ConditionEvaluator.All` → `Execute`。

### 5.3 事件总线解耦（三层）

```
DamageResolver
  → UnitCoreEvents
  → PromotionUnitCoreBridge（DamageContext → ConditionContext）
  → PromotionEventBus.Raise
  → PromEff_SubscribeEvent → PromotionAction[]
```

| 事件类型 | 谁 Raise |
|----------|----------|
| 四种伤害 | Bridge ← UnitCore |
| `OnAcquire` | `PromotionManager.Fetch` |
| `UnitSpawned` | `PromotionServices.NotifyUnitSpawned`（游戏层可选） |

上下文统一为 **`ConditionContext`**（`Attacker` / `Target` / `DamageContext` / `Position` 等），不再使用 `Monster` / `Tower` / `Projectile` 键。

### 5.4 保留的 Effect / Action

**Effect：**

- `PromEff_SubscribeEvent`
- `PromEff_GlobalDamageModifier` / `PromEff_GlobalBuffModifier`
- `PromEff_UnitStatModifier` / `UnitBuffModifier` / `UnitDamageModifier`（合并原 Monster/Tower 特化）

**Action：**

- `Action_ApplyBuff` / `ApplyStatModifier` / `ModifyDamageContext`
- `Action_DealDamage` / `RemoveShield`

### 5.5 已删除的游戏特化

建筑邻接、卡牌费用、商店刷新、Fortress、资源收入、投射物生成、Turn/GameStatus 条件，以及全部 `Promotion/Common/` 旧实现。

---

## 6. 目录速查

```
Scripts/UnitCore/
  IUnit.cs, UnitCommon.cs, UnitInfo.cs
  Common/          Stat, StatChangeConfig, ModifierCollectionUtil
  Damage/          DamageContext, DamageResolver, DamageModifier*, CollectSession
  Buffs/           Buff*, BuffController, Comm/*, Modifier/*

Scripts/UnitCoreSupport/
  UnitCoreEvents, DamageFeedback, UnitControl, GlobalUsings

Scripts/PromotionSupport/
  PromotionEventBus, PromotionServices, PromotionUnitCoreBridge
  Filter/

Scripts/Promotion/
  Promotion.cs, PromotionManager, PromotionInstance, PromotionEffect*
  Condition/, Action/, Effect/
```

---

## 7. 对局收束入口：`CombatRuntime`

一局开始时调用一次，避免全局订阅/遗物/修正器残留：

```csharp
CombatRuntime.BeginMatch(new CombatRuntimeMatchOptions
{
    ActiveUnits = () => myUnitRegistry.All,
    SpawnDamageText = ctx => { /* TempLabel */ },
    BuffResourceRootPath = "res://Data/Buff/",
});
// 对局中：PromotionManager.Fetch(promotion);
// 结束：CombatRuntime.EndMatch();
```

`BeginMatch` 顺序：清空遗物实例与 EventBus → 注销 Bridge → 清空 UnitCoreEvents → 重置 Modifier 弱表与 CollectSession 池 → 再注册 Bridge、设置 `GlobalOwner`、注入 Options。

---

## 8. 游戏层接入清单（待实现）

| 优先级 | 内容 |
|--------|------|
| P0 | 实现 `IUnit`；驱动 `BuffController.Update(delta)` |
| P0 | Buff 资源 `res://Data/Buff/` |
| P1 | `PromotionManager.Fetch` 调用点（商店/获得遗物） |
| P1 | `PromotionServices.ActiveUnits`、`NotifyUnitSpawned` |
| P1 | `DamageFeedback.SpawnText`（飘字） |
| P2 | 自定义 Filter 子类、从原项目迁移的 TD 事件 → `PromotionEventBus.Raise` |

---

## 9. 相关性能备注（P1 及以后，未做）

- `ConditionContext` 池化、Bridge 按订阅类型懒转发
- Stat `AddModifier` 二分插入；AOE 空间查询
- 遗物 `DuplicateDeep` 仅在 Fetch 时，规模极大时可再优化

---

*文档版本：对应对话内 UnitCore 移植解耦、Promotion 重构与 P0 性能优化完成后的状态。*
