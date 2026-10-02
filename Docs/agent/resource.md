# 资源目录

代码在 `Scripts/aFrameworks/Resource`。

`ResourceCatalog<T>` 先扫描路径，用到再加载。ID 是不带扩展名的文件名。名字或文件夹以 `_` 结尾的会被跳过。

`ResourceCatalogRegistry` 收非泛型的 `IResourceCatalog`，方便跨类型查询。

游戏层为每种资源建一个 catalog，`Init` 时传入根路径（例如 `res://Data/Buff/`、`res://Data/Promotion/`）。Buff 和词条带这两个默认根路径，并在类型初始化时注册。对局选项里的根路径会在 `CombatRuntime.BeginMatch` 再 `Init` 一次。对局结束不清目录。

查询：`Has(id)`、`GetResource(id)`、`GetIds()`。加载失败让调用方看到空结果，不要在 catalog 里塞游戏逻辑。
