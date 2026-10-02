---
name: bailian-cli
description: >-
  Invokes Aliyun Bailian CLI (`bl`) for image generate/edit in this repo.
  Use when the user mentions 百炼, DashScope, `bl`, 出图, 生图, 改图, 参考图,
  or a workflow needs Bailian. Do not use for ordinary coding.
---

# 百炼 CLI（本仓库）

本机已装 `bailian-cli`。Agent **用 `bl` 调百炼**，不要自己拼 DashScope HTTP。

命令细节以本机 `bl <command> --help` 和用户全局百炼 skill 为准。不要猜 flag。

## 何时用

- 用户点名百炼 / DashScope / `bl`，或继续既有出图
- 图生模参考图、图标、概念图

不要用 `bl text chat` 做普通问答、编程。那些走宿主模型。

## 鉴权

凭据在 `~/.bailian/config.json`。**不要读取、不要写入仓库、不要在回复里回显 Key。**

1. 先 `bl auth status --output json`。只汇报 `authenticated` 和 masked 字段。
2. 已登录 → 直接调命令。
3. 未登录 → 让用户在本机执行 `bl auth login --api-key <他们自己的 Key>`。Agent 不要代填 Key。

## 本仓库出图约定

省略 flag 时 CLI 默认加水印、同步模式改写提示词。本仓库必须显式：

- `--watermark false`
- `--prompt-extend false`
- `--output json`
- `--timeout 180`
- 未指定模型则不要加 `--model`
- `--out-dir` 用仓库内路径；`--out-prefix` 用小写英文 id

路径：

- 图生模参考图：`Tools/tripo/tmp/ref/<name>.png`
- 其它试图：`Tools/bailian/tmp/`
- 合同：`docs/agent/tools.md`

图生模参考图默认 `--size 1024*1024`。

PowerShell 直接 `bl ...`。不要把整条命令塞进 `cmd`。出图后写清输出路径。
