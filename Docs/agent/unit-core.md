# UnitCore

战斗核心。游戏特化不要写进这里。对局里会变的状态挂在 `MatchContext` 实例上。

## 依赖方向

```text
UnitCore            单位、属性、伤害、Buff、Filter、Modifier 骨架
UnitCoreSupport     MatchContext / CombatRuntime、事件、飘字、单位控制
游戏层              实现 IUnit，调用 BeginMatch / EndMatch
```

`UnitCore` 只依赖 `IUnit` 和通用类型。

## 对局入口

```csharp
MatchContext match = CombatRuntime.BeginMatch(new CombatRuntimeMatchOptions
{
    ActiveUnits = () => myUnitRegistry.All,
    SpawnDamageText = ctx => { /* 飘字 */ },
    SpawnEffectAtPosition = (name, pos) => { /* 特效 */ },
    BuffResourceRootPath = "res://Data/Buff/",
    PromotionResourceRootPath = "res://Data/Promotion/",
});

match.Fetch(promotion);
match.NotifyUnitSpawned(unit);
CombatRuntime.EndMatch();
```

| 成员 | 职责 |
|---|---|
| `EventBus` | 本局事件 |
| `GlobalModifierOwner` | 全局 Modifier 哨兵 |
| `SpawnDamageText` | 本局飘字。`DamageFeedback.SpawnText` 只转发到这里 |
| `Fetch` / `Revert` / `ActivePromotions` | 词条运行时 |
| `NotifyUnitSpawned` / `ForEachActiveUnit` | 单位与条件上下文 |

`CombatRuntime.GlobalModifierOwner` 指向 `Current?.GlobalModifierOwner`。不要再做一份静态镜子。

`EndMatch` 只销毁 `Current`。`Dispose` 会清掉本局词条，并清进程级暂存：`UnitCoreEvents`、伤害 / Buff 修改器表、`ConditionContext` 租用池。资源目录不跟对局走。`BeginMatch` 在创建下一局前也会清一次暂存。

## 状态挂在哪

| 能力 | 挂载 |
|---|---|
| `UnitStatCollection` | `IUnit` 内置 |
| `BuffController` | `IUnit` 内置 |
| `DamageModifier` / `BuffModifier` | 外挂 WeakTable |

共用骨架在 `UnitCore/Common`：`ModifierController<T>`、`ModifierOwnerStore`、`ModifierCollectSessionStack`。伤害阶段和 Buff 阶段不要合成一个 Modifier。

## Buff

`Buff` 资源持有 `BuffInfo` 和 `Effects[]`。行为由 Effect 组合：`Stat` / `Dot` / `Slow` / `Control` / `Vulnerable` / `ShieldBreak`。

`BuffTag` 是 `[Flags]`。Required 要包含全部位，Excluded 是任意重叠。`BuffModifier` 只改时长、层数、效果值。

## Filter

`ObjectFilter` / `UnitFilter` / `DamageContextFilter` 在 `UnitCore/Common/Filter`。UnitCore 和 Promotion 共用。扩展放这里，或放游戏层子类。

## 目录

```text
Scripts/UnitCore/
  Common/Filter/     ObjectFilter, UnitFilter, DamageContextFilter
  Common/            ModifierController, ModifierOwnerStore, Stat*
  Damage/            DamageResolver, DamageModifier*
  Buffs/             Buff, BuffInstance, BuffController
  Buffs/Effects/     BuffEffect 及各效果
  Buffs/Modifier/    BuffModifier*
Scripts/UnitCoreSupport/
  CombatRuntime, MatchContext, UnitCoreEvents, DamageFeedback, UnitControl
```

## 接入

1. 实现 `IUnit`，并调用 `BuffController.Update(delta)`。
2. 准备 `res://Data/Buff/`。
3. `BeginMatch`，然后 `Fetch` / `NotifyUnitSpawned`。
4. 飘字和特效从 `CombatRuntimeMatchOptions` 注入，挂在返回的 `MatchContext` 上。

对局状态进 `MatchContext`。进程入口是 `CombatRuntime.BeginMatch` / `EndMatch` / `Current`。
