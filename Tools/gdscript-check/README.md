# gdscript-check

用 Godot `--headless` 扫 `.gd` 解析错误（引擎解析器，不是 linter）。

```text
python Tools/gdscript-check/check.py
```

默认扫 `addons/` 与 `addons_custom/`。`check.gd` 里的 `_VENDOR` 列出要跳过的第三方目录名。

| 参数 | 作用 |
|------|------|
| 路径… | 只扫这些目录或 `.gd` |
| `--all` | 两个插件根下的全部脚本 |
| `--vendor` | 连 `_VENDOR` 里的目录一起扫 |
| `--json` | 机器可读 |
| `--godot` / `GODOT_BIN` | 指定 Godot 可执行文件 |
