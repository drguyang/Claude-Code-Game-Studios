# MCP for Blender —— 本地文档与实测笔记

> **抓取时间**:2026-11-13 · **服务端** v1.30.0(`uv tool` 安装)· **addon** 1.8 · **protocol** 15
> **上游**:https://github.com/ahujasid/blender-mcp(MIT)

## 目录里有什么

| 文件 | 内容 | 来源 |
|---|---|---|
| `README.md` | 上游 README 全文(927 行) | 包内 `dist-info/METADATA` 的 Description 段 |
| `tools.md` | **9 个工具的完整 JSON Schema** | 实连服务端 `tools/list` 抓取 |
| `guides/*.md` | addon 内置的 7 篇操作指南:`bpy` / `materials` / `scene` / `animation` / `rigging` / `retopology` / `level-design` | 包内 `blender_mcp/guides/` |

> `guides/` 是**服务端自己的 `get_guide()` 返回的正文** —— 比 README 更接近实际写代码时要用的知识。

## 九件工具(实测可用)

`execute_blender_code`(跑 Python)· `look`(从 viewport/camera/angles/frames 截图,可指定 shading)·
`get_scene_info`(逐对象摘要)· `get_addon_status` · `search_assets` / `import_asset`(Poly Haven /
Sketchfab / Poly Pizza)· `generate_3d`(Tripo / Hunyuan3D / Hyper3D / Meshy,**本项目不用**)·
`disable_telemetry` / `record_trajectory_feedback`

## 本项目的两条关键结论

1. **分辨率问题有解,而且不用 3D 资产**:Poly Haven 的 CC0 PBR 贴图自带真实世界尺寸,
   `import_asset(..., resolution="4k")` 一步建好材质。按 `guides/materials.md` 的公式平铺:
   `Mapping.Scale = 面尺寸(m) / 贴图覆盖尺寸(m)`。
2. **图案问题无解**:Poly Haven 是实拍库,**没有「白灰剥落露砖」这种合成形态**
   (实测检索 `peeling plaster revealing brick`,返回的全是纯灰泥或纯砖)。
   ⇒ 剥落图案必须**自己合成**:灰泥底 + 砖层 + 一张 mask。

## 实测数字(2026-11-13)

| 方案 | 有效密度 | 1080p/60° 打平距离 | 1 m 处观感 |
|---|---|---|---|
| 概念图投影(14 m 墙 → 2048) | **66 px/m** | 25 m | 完全糊 |
| `brick_wall_11` 4K / 4 m 覆盖 | **1024 px/m** | 1.6 m | 清晰 |
| `plastered_wall_04` 4K / 3.2 m 覆盖 | **1280 px/m** | 1.3 m | 清晰(能看见抹刀痕) |

公式:`屏幕密度 = 1920 / (2·d·tan30°) = 1662 / d px/m`;与贴图密度相等时的 `d` = 打平距离。

## 一个坑(实测)

`import_asset` 第一次调用时先后报:

```
Created material 'plastered_wall_04' ...
Error applying texture: Communication error with Blender: Unknown command type: set_texture
```

随后整个场景**被重置回启动文件**(只剩 Camera/Cube/Light),`scene.blendermcp_use_polyhaven`
也随之回落到 `False`。**根因未定**(addon 的 `set_texture` 处理器确实存在,且第二次调用成功),
但已确认**不是 Blender 重启**(pid 未变)。

> 后果很实际:**用它之前先把工作存盘**。第二次调用(先重建对象、再重设 polyhaven 开关)一次通过。

## 遥测

默认会上报一条最小匿名用量记录(工具名 / 耗时 / 版本 / 系统 / 时间戳),**不含**提示词、
代码、截图与场景数据。本项目**用户明确指示不关**(2026-11-13)。

关闭方式(留档,未采用):环境变量 `DISABLE_TELEMETRY=true`,或 `disable_telemetry` 工具。
