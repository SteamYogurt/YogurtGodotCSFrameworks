# 工具

Agent 用百炼出图、Tripo 出模，用 Godot 检查 GDScript 解析。运行时不要 `res://Tools/`。玩法只引用 `Assets/`、`Scene/`、`Data/`。

| 路径 | 提交？ | 用途 |
|------|--------|------|
| `Tools/bailian/tmp/` | 否 | 出图草稿 |
| `Tools/tripo/tmp/ref/` | 否 | 图生模参考图，平铺 `<name>.png` |
| `Tools/tripo/resize_glb_textures.py` | 是 | 把 GLB 内嵌贴图压到 1024 |
| `Tools/gdscript-check/` | 是 | `python Tools/gdscript-check/check.py` |
| `addons/gdscript_check/check.gd` | 是 | 上面的脚本用 `--script` 加载 |

成品网格放到 `Assets/Art/Gen/<小写英文 id>/`。图标、提示词、碰撞烘焙由具体项目自己加，不放进这个套件。

Skill：

- 出图：`.cursor/skills/bailian-cli/SKILL.md`
- 出模：`.cursor/skills/tripo-cli/SKILL.md`

凭据留在用户环境。不要读配置文件，不要把 Key 写进仓库。
