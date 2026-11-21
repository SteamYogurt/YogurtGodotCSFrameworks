# 入口与设置

运行入口是 `res://Scene/App/Main.tscn`。根节点脚本 `Scripts/aFrameworks/Main.cs`。不要把 `AATest` 当入口。

`Main._Ready` 按这个顺序创建并 `AddChild`：

- `UserInfo`
- `ObjectPoolManager`
- `NetManager`
- `TransportManager`
- `InputManager`
- `SteamManager`（`Main.tscn` 上已有子节点则复用，否则 `new`）
- `LanDiscoveryService`
- `UIManager`

常驻服务用 `Singleton<T>`（`Scripts/aFrameworks/Singleton.cs`）。`Global.cs` 是杂项工具，不是入口。

## Steam Demo 与愿望单

`SteamManager` 挂在 `Main.tscn`，检视器勾选。不要 `new` 之后再改导出项。

| 导出项 | 含义 |
|---|---|
| `IsDemo` | 勾选后菜单和社交条显示「加入愿望单」。导出特性 `demo` 也会打开 |
| `AppId` | 本构建。大于 0 时写 `SteamAppId` / `SteamGameId`。0 则交给工作目录 `steam_appid.txt` |
| `StoreAppId` | 正式版商店页。愿望单打开这个 id。0 时退回 `AppId`。不要填编辑器用的 480 |

界面只问 `SteamManager.OfferWishlist` / `IsDemoBuild`。已连上时：商店目标就是本构建则视为已有；`StoreAppId` 与 `AppId` 不同则用 `SteamApps.BIsSubscribedApp`。已有正式版，或商店 id 是 480，都不再推愿望单。Demo 不解锁成就。

打开愿望单走 `OpenWishlist`：Overlay 商店页（`EOverlayToStoreFlag_None`，不要加购物车）→ `steam://store/<id>` → 浏览器。`OpenUrl` 同样先 Overlay 网页。Steam 没有静默加愿望单 API。不要创建或改写 `steam_appid.txt`。编辑器不跑 `RestartAppIfNecessary`。

Windows 构建保留 `steam_api64.dll`。Steam 初始化首先解析程序集输出目录，再尝试项目根目录。原生库不可用时 Steam 功能停用，局域网和离线入口继续初始化。

社交条 `addons_custom/SocialEntryButton/entry.tscn` 的 `WishlistEntryButton` 自己按 `OfferWishlist` 显隐，点了直接开商店页、不经确认。Steam / Discord 确认后再走 `OpenUrl`。

## 设置

`UserInfo` 在 `Scripts/aFrameworks/UserInfo`。语言、窗口、垂直同步、像素对齐、帧率、音量。存 `user://user_info.cfg`。

`UserInfo.Apply()` 启动时套用语言、画面和音频。设置改一项只走对应的 `ApplyLocale` / `ApplyDisplay` / `ApplyAudio`，再 `SaveToDisk`。`ApplyLocale` 语言没变就跳过。音乐和音效总线不存在时会建 `Music`、`SFX`，都送到 `Master`。不要把默认的 `Master` 总线改名。

设置面板是一个场景：`addons_custom/Settings/Settings.tscn`。`UIManager` 挂上后先隐藏，节点名 `Settings`。主菜单打开 `ShowSettings`。`Esc`（`ui_cancel`）只关闭已打开的设置。改键监听中的 Esc 由 `InputRemapButton` 先吃掉，不关面板。

## 语言

十三种语言与 `gds-framework` 一致：`en, zh_CN, zh_TW, ja, ko, es, fr, pt_BR, de, it, ru, tr, pl`。列表和归一在 `Scripts/aFrameworks/Localization/Localization.cs`。

当前语言存在 `UserInfo.Locale`。系统语言和存档都经 `Localization.ResolveLocale` 收成其中一种，对不上时用 `en`。运行时只 `TranslationServer.SetLocale`。不要在运行时读 CSV，也不要 `TranslationServer.AddTranslation`。

CSV 译表、Desc、字体切换在并列仓 `../gds-framework`。本仓不装那套运行时。后续项目自己把译表写进 Internationalization。
