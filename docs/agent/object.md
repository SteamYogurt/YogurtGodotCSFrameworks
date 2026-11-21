# 对象池

场景实例的注册表。代码在 `Scripts/aFrameworks/Object`。资源定义走 `docs/agent/resource.md`，不要和这里混用。

## 目录

| 路径 | 行为 |
|------|------|
| `res://Assets/Objs/aPooledObjs/` | 启动时建池。常量 `ObjectPoolManager.PooledRoot` |
| `res://Assets/Objs/bLazyObjs/` | 只登记路径，第一次取出时再建池。常量 `ObjectPoolManager.LazyRoot` |

名字或文件夹以 `_` 结尾的场景会被跳过。ID 是不带扩展名的文件名。

根节点要带手动新建的 `ObjectInfo`。池在创建时写入名字、目录和所属池。

## 调用

```csharp
Player player = ObjectPoolManager.Spawn<Player>("Player");
ObjectPoolManager.Release(player);
bool exists = ObjectPoolManager.Exists("Player");
```

`Spawn` 先查预热池，没有再查懒加载。`Release` 用对象自己的 `Info.Pool` 归还。

`GetPossibleObject` 和 `ReturnObjectToPool` 是同一套入口的旧名字。只预热、不走懒加载时用 `GetObject`。

`GetObjectReadOnly` 只查看队首，不会把对象取出。

`ObjectPoolManager` 出树时释放池内待复用的实例，包括已脱离场景树的玩家节点。
