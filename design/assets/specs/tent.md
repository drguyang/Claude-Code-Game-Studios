# Asset Spec: 帐篷（P0 场景 C · 城郊营地）

> **Tier**: Vertical Slice Critical
> **Category**: Environment / Building Module（近景 hero）· P0 唯一新建
> **Source**: campaign-arc.md:32（门板 + 两根房梁 + 一块布）· he-returns.md:29
> **Art Bible Ref**: §8.10.6 · §8.5 建筑模块 · 近景 hero 20,000 · §1 P1 水墨双轨
> **ADR Ref**: ADR-015（模块化建造 · 同一整数格）· ADR-003（模块化网格）
> **Scene**: 城郊营地（P0 场景 C）· **hero 名额 1/3**

---

## Visual Description

一个**随手搭起来**的临时棚子：一块捡来的**旧门板**斜立作一面墙，两根**没烧透的房梁**交叉支撑，
上面蒙一块**打补丁的粗麻布**，布边用麻绳系在梁上；布面下垂起皱、有泥污与破洞，
另一侧敞开露出内部**草席**。整体**歪斜不规整**——不是帐篷，是「能挡风的东西」（`campaign-arc.md:32`）。

## Technical Specs

| Property | Value | Notes |
|---|---|---|
| 面数预算 | **20,000**（hero 档） | 实测 **499,990** = **25×**；减面实测 8k 会压碎布边须条（`TD-art-01`） |
| 贴图 | **2 × 4096²**（albedo + MetallicRoughness） | **无 normal**（建筑模块允许） |
| 尺寸 | **2.051 × 1.706 × 1.900 m** | 已归一，轴心 = 底面中心 |
| 材质槽 | 1 | |
| LOD | **未生成**（要求 3 级） | 已有 8k / 20k / 50k 减面参考件，非正式 LOD |
| 碰撞 | **未建** | |
| 清理状态 | ✅ **已清理**（2026-11-12） | 非流形边 0 · 边界边 4 · 删碎面 0 |

## Asset Breakdown

| 文件 | 面数 | 状态 |
|---|---|---|
| `tent.glb`（清理后） | 499,990 | ✅ 顶点 249,932 · 0 非流形边 |
| 减面参考 8k / 20k / 50k | 7,999 / 20,000 / 50,000 | ⚠️ 仅实验件，**未入库、未登记 LOD** |

## AI Generation Prompt

**概念图（t2i，`sensenova-u1.5-fast`）**

```
清末逃难者搭的临时窝棚：一块捡来的旧门板斜靠作一面墙，两根粗糙房梁支起，
上面蒙一块打补丁的粗麻布，布边用麻绳随意系在梁上，布面下垂、起皱、有泥污与破洞，
另一侧敞开露出内部草席，整体歪斜不规整，像随手搭起来能挡风的东西，
不要竹竿、不要帐篷杆、不要规整三角形、不要现代露营帐，孤立单体，浅灰纯色背景，
微侧视，完整入画，低饱和水墨淡彩与旧木铜色，暖白纸底，柔和漫射光，
无文字，无水印，无印章，无边框
```

**图生 3D（火山方舟 Seed3D）**

```
--subdivisionlevel medium --fileformat glb
```

### Generation Record（§8.10.2 + §8.10.6.3）

| 字段 | 值 |
|---|---|
| `prompt` | 见上方两个代码块 |
| `model` | `sensenova-u1.5-fast`（概念图）· `doubao-seed3d-2-0-260328`（3D） |
| `iterations` | 概念图 2 轮（v1 竹竿三角帐 → v2 门板 + 房梁 + 布）；3D 1 轮 |
| `seed` | **`none`** — Seed3D 无 seed 参数 |
| `human_edits` | **`none`**（脚本：RMBG-1.4 抠图 → 512² 白底；`clean_glb.py` 合并顶点 / 重算法线 / 归一尺度） |
| `budget_tier` | **`hero`** — 场景 C，本场景 hero 第 1/3 件 |
| `source_images` | `assets/design-references/3d-pilot/input-sn-v3/tent.png`（sha256 `31f03e3e35131841…`） |
| `service_task` | `cgt-20261112101940-nm7vb`（medium，361 s）· 调用日 2026-11-12 |
| `reconstruction` | 图生 3D，单图，`subdivisionlevel=medium`，radius / threshold 由服务端定（不可调） |
| `dcc_cleanup` | **Blender 4.5.16 LTS** · 合并重复顶点（1e-5）→ 去退化 → 重算法线 → 归一 1.900 m → 轴心底面中心；**未减面** |
| `license_review` | **⏸ 待核** — 火山方舟 ToS 原文未取得（见 §8.10.6.11-A） |

## Open Questions

- [ ] **ToS 与输出权属**：`⏸` 未结前不得进 Addressables / 构建 / 发行包。
- [ ] **LOD**：三级未生成；8k 档会压碎布边须条，正式 LOD 须等 `TD-art-01`（alpha 卡片）或其替代方案。
- [ ] **交互**：若帐篷可被玩家改造（P1a 自由建造），须决定是拆网格还是整体换件。
