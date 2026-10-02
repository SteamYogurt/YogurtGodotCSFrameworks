---
name: tripo-cli
description: >-
  Generates 3D meshes with Tripo CLI (`tripo make`) using v3.1 smart
  low-poly (`smart_low_poly`), 1000–3000 faces, 1024 textures. Use when the
  user mentions Tripo, tripo, 智能低模, 图生模, 文生模, GLB, 生成模型, 3D模型.
  Do not use for 2D icons or ordinary coding.
---

# Tripo CLI（本仓库）

本机已装 `tripo-cli`。Agent **用 `tripo make` 出模**，不要自己拼 OpenAPI。

flag 以 `tripo docs --topic commands/make` 为准。不要猜模型版本。**不要**为了压贴图去 `convert:texture_size`。贴图用本地脚本压到 1024。

## 何时用

- 用户点名 Tripo / 智能低模 / 图生模 / 文生模，或继续既有出模
- 需要一件单体网格

2D 图走 [bailian-cli](../bailian-cli/SKILL.md)。路径约定见 `docs/agent/tools.md`。

## 默认：智能低模，1000–3000 面，贴图 1024

走 **v3.1 + `smart_low_poly`**。`smart_low_poly` 要求 `face_limit` ≥ 500。

| 档 | `face_limit` |
|----|----------------|
| 更碎的小件 | **1000** |
| 默认 | **2000** |
| 稍复杂的单体 | **3000** |

不要超过 3000，除非用户点名。不要关 `smart_low_poly`、不要 P1、不要 `--for game-pc` / `film` / `anim`，除非用户点名。

生成默认是 2K 贴图。下载后本地压：

```powershell
tripo make "Tools/tripo/tmp/ref/prop.png" --model tripo-v3.1 -p smart_low_poly=true -p face_limit=2000 --json --yes --no-open -o "Assets/Art/Gen/prop"
python Tools/tripo/resize_glb_textures.py "Assets/Art/Gen/prop/prop.glb"
```

把 `model.glb` 改名为 `<name>.glb` 放到 `Assets/Art/Gen/<name>/`。

| 要做 | 不要做（除非用户点名） |
|------|------------------------|
| `--model tripo-v3.1 -p smart_low_poly=true` | P1 / P2 / film / anim |
| `-p face_limit=2000`（范围 1000–3000） | `face_limit` > 3000 |
| 本地 `resize_glb_textures.py` | `--then convert` 只为改贴图尺寸 |
| `--json --yes --no-open` | 一次对话自动重出超过 1 次 |

## 默认：图生模

**先百炼出参考图，再图生模。** 文生模只在用户点名、或参考图已在手上时用。

1. `bl image generate`，参考图 `--size 1024*1024`，落到 `Tools/tripo/tmp/ref/<name>.png`。
2. **停，等用户确认参考图。**
3. `tripo make` 该图。一次只跑 1 个任务。先 `tripo balance --json`。`-o` 用 `Assets/Art/Gen/<name>`。
4. 改名后跑 `python Tools/tripo/resize_glb_textures.py`。

绑骨、动画、高模、LOD：先问用户。

## 鉴权与网络

凭据在用户环境变量 / `%USERPROFILE%\.tripo`。**不要读取、不要写入仓库、不要在回复里回显 Key。**

本仓库固定走 **国内区**。`openapi.tripo3d.com` 直连，不要走代理。

```powershell
$env:TRIPO_API_KEY = [System.Environment]::GetEnvironmentVariable("TRIPO_API_KEY", "User")
$env:TRIPO_REGION = "cn"
Remove-Item Env:HTTP_PROXY, Env:HTTPS_PROXY, Env:ALL_PROXY -ErrorAction SilentlyContinue
tripo doctor
```

只汇报 region、balance、是否通过。未登录让用户自己设 `TRIPO_API_KEY` 和 `TRIPO_REGION=cn`。余额不够就停。

- `-o` 必须落到 `Assets/Art/Gen/<小写英文 id>/`，不要用仓库根 `tripo-out/`。
- 参考图只放 `Tools/tripo/tmp/ref/<name>.png`。
- `tripo make` 会阻塞到结束。不要把超时收到 15 分钟以内。
- 成功后回复写清 `output_dir` / `model_file` / `credits_consumed`。
