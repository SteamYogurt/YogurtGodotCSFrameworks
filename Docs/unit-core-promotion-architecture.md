# UnitCore / Promotion 架构说明

本文档概括 **战斗核心（UnitCore）**、**对局上下文（MatchContext）**、**支撑层** 与 **Promotion（提升 / 升级效果）系统** 的现行设计。

---

## 1. 总体分层

```
UnitCore          — 单位、属性、伤害、Buff、通用 Filter、Modifier 骨架
UnitCoreSupport   — MatchContext / CombatRuntime、UnitCoreEvents、飘字、单位控制
PromotionSupport  — Promotion 事件总线（实例）、UnitCore 桥接（实例）
Promotion         — Promotion 资源、效果、条件、动作、运行时实例
```

**原则：**

- 核心只依赖 `IUnit` 与通用类型；游戏特化不进入核心。
- **对局可变状态全部挂在 `MatchContext` 实例上**。
- 游戏层只通过 `CombatRuntime.BeginMatch` / `EndMatch` 与 `MatchContext` API 接入。

---

## 2. 对局入口：`CombatRuntime` + `MatchContext`

```csharp
MatchContext match = CombatRuntime.BeginMatch(new CombatRuntimeMatchOptions
{
    ActiveUnits = () => myUnitRegistry.All,
    SpawnDamageText = ctx => { /* TempLabel */ },
    SpawnEffectAtPosition = (name, pos) => { /* VFX */ },
    BuffResourceRootPath = "res://Data/Buff/",
});

match.Fetch(promotion);
match.NotifyUnitSpawned(unit);

CombatRuntime.EndMatch();
```

### `MatchContext` 拥有

| 成员 | 职责 |
|------|------|
| `EventBus` | Promotion 侧事件（实例） |
| `GlobalModifierOwner` | 全局 Modifier 哨兵（对象级真源） |
| `ActiveUnits` / `SpawnEffectAtPosition` | 对局注入 |
| `Fetch` / `Revert` / `ActivePromotions` | Promotion 运行时 |
| `NotifyUnitSpawned` / `ForEachActiveUnit` / `OpenContext` | 单位与条件上下文 |

`CombatRuntime.GlobalModifierOwner` → `Current?.GlobalModifierOwner`（解析器读这里，无静态镜子）。

---

## 3. UnitCore

### 3.1 状态挂载约定

| 能力 | 挂载 | 原因 |
|------|------|------|
| `UnitStatCollection` | `IUnit` 内置 | 单位自身数值底座 |
| `BuffController` | `IUnit` 内置 | 单位自身状态 |
| `DamageModifier` / `BuffModifier` | 外挂 WeakTable | 可选附加能力 |

### 3.2 Modifier 共用骨架（`UnitCore/Common`）

| 类型 | 作用 |
|------|------|
| `ModifierController<T>` | 按 Priority 插入、按 RuntimeSource 卸 |
| `ModifierOwnerStore<TController>` | ConditionalWeakTable |
| `ModifierCollectSessionStack<T>` | 嵌套安全的 Collect Session 栈 |

`DamageModifierController` / `BuffModifierController` 与各自 CollectSession 基于上述骨架；**语义仍分离**（伤害阶段 vs Buff 多阶段 flags）。

### 3.3 Filter（`UnitCore/Common/Filter`）

`ObjectFilter` / `UnitFilter` / `DamageContextFilter` 为 UnitCore 与 Promotion **共用**，不再放在 PromotionSupport。

---

## 4. Promotion

### 4.1 生命周期

```
match.Fetch → Instance.Activate → Effect.Activate → Handle
Revert / EndMatch → Handle.Dispose
```

可逆副作用必须进 `PromotionEffectHandle`（含 `AddCleanupOnce`）。

### 4.2 Effect

- `PromEff_SubscribeEvent`
- `PromEff_UnitModifierGrant`（Stat + Damage + BuffModifier）
- `PromEff_GlobalModifierGrant`

### 4.3 ConditionContext 池化

- `ConditionContext.Rent` / `RentScope` / `Return`
- Bridge / Fetch / NotifyUnitSpawned 仅在 **同步 Raise** 期间租用
- **禁止**把 Context 存到 Raise 栈之外
- `EndMatch` 调用 `ClearPool`

---

## 5. 事件流

```
DamageResolver
  → UnitCoreEvents
  → PromotionUnitCoreBridge（Match 实例，池化 Context）
  → match.EventBus.Raise
  → PromEff_SubscribeEvent → Actions
```

全局 Modifier 收集：`CombatRuntime.GlobalModifierOwner`。

---

## 6. 目录速查

```
Scripts/UnitCore/
  Common/
    Filter/          ObjectFilter, UnitFilter, DamageContextFilter
    ModifierController, ModifierOwnerStore, ModifierCollectSessionStack
    Stat*, StatChangeConfig, ModifierCollectionUtil
  Damage/            DamageResolver, DamageModifier*, CollectSession
  Buffs/             Buff*, Modifier/*

Scripts/UnitCoreSupport/
  CombatRuntime, MatchContext, CombatRuntimeMatchOptions
  UnitCoreEvents, DamageFeedback, UnitControl

Scripts/PromotionSupport/
  PromotionEventBus, PromotionUnitCoreBridge

Scripts/Promotion/
  Effect/  PromEff_SubscribeEvent, UnitModifierGrant, GlobalModifierGrant
  Action/, Condition/
```

---

## 7. 游戏层接入清单

| 优先级 | 内容 |
|--------|------|
| P0 | 实现 `IUnit`；`BuffController.Update(delta)` |
| P0 | `res://Data/Buff/`、`res://Data/Promotion/` |
| P1 | `BeginMatch` + `match.Fetch` / `NotifyUnitSpawned` |
| P1 | 飘字 / VFX 注入 |
| P2 | 自定义 Filter；TD 事件 → `match.EventBus.Raise` |

---

## 8. 维护约定

1. 对局状态只进 `MatchContext`；唯一静态入口 `CombatRuntime.Current`。
2. Filter 扩展放在 `UnitCore/Common/Filter`（或游戏层子类）。
3. 新「外挂 Modifier 栈」优先复用 `ModifierController` / `ModifierOwnerStore` / SessionStack。
4. ConditionContext 仅同步 Raise 使用；异步勿持有。
5. Stat / Buff 留在 `IUnit`；Damage/Buff Modifier 外挂。

---

*文档版本：Filter 下沉、Modifier 骨架共用、GlobalOwner 对象级、ConditionContext 池化之后。*
