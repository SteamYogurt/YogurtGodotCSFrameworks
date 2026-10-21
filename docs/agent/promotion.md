# Promotion

词条。资源、效果、条件、动作在 `Scripts/Promotion`。事件总线和 UnitCore 桥在 `Scripts/PromotionSupport`。运行时实例挂在 `MatchContext` 上，见 `docs/agent/unit-core.md`。

开局时如果 `CombatRuntimeMatchOptions.PromotionResourceRootPath` 有值，会 `PromotionExt.Init`。目录留在进程里，`EndMatch` 不清它。

## 生命周期

```text
match.Fetch → Instance.Activate → Effect.Activate → Handle
Revert / EndMatch → Handle.Dispose
```

可逆副作用放进 `PromotionEffectHandle`（含 `AddCleanupOnce`）。

## Effect

- `PromEff_SubscribeEvent`
- `PromEff_UnitModifierGrant`（Stat、Damage、BuffModifier）
- `PromEff_GlobalModifierGrant`

条件在 `Promotion/Condition`，动作在 `Promotion/Action`。

## 事件

```text
DamageResolver
  → UnitCoreEvents
  → PromotionUnitCoreBridge（本局实例，池化 Context）
  → match.EventBus.Raise
  → PromEff_SubscribeEvent → Actions
```

全局 Modifier 从 `CombatRuntime.GlobalModifierOwner` 收集。

## ConditionContext

- `ConditionContext.Rent` / `RentScope` / `Return`
- Bridge、Fetch、`NotifyUnitSpawned` 只在同步 `Raise` 期间租用
- 不要把 Context 存到 Raise 栈之外
- `EndMatch` 调用 `ClearPool`

## 数据

词条资源放游戏项目的 `res://Data/Promotion/`，由目录扫描注册。这个套件不带具体词条内容。

新效果：加一个 `PromEff_*`，需要改数值时走已有 Modifier，不要在 Effect 里直接改单位字段。
