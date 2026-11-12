# Asset Spec: 教堂断墙（P0 场景 A · 城市废墟街区）

> **Tier**: Vertical Slice Critical
> **Category**: Environment / Building Module（近景 hero）
> **Source**: campaign-arc.md:22 / :28（炮弹轰塌东墙）· he-returns.md:28（序章起点）
> **Art Bible Ref**: §8.10.6（3D 生成管线）· §8.5 建筑模块 · 近景 hero 20,000
> **ADR Ref**: ADR-015（两层世界：逻辑层 + 纯视觉层）· ADR-023（World 场景 additive）
> **Scene**: 城市废墟街区（P0 场景 A）· **hero 名额 1/3**

```budget
asset: pending
family: building-module
tier: hero
scene: city-ruins
```

---

## Visual Description

晚清教堂**东墙残件**，被炮弹轰塌后的形态：左上角**整块缺失**，断口**向右下斜切**；
右侧保留较完整的墙垛与一扇**尖拱窗**（边缘破损、缺角）；灰砖 + 剥落白灰（露出下层旧砖）；
断口处可见**砖层剖面**；墙脚散落碎砖；墙体有厚度。

叙事用途：序章开场——神父走的那一晚，这面墙已经塌了一半（`campaign-arc.md:22`）。

> **2026-11-12 实测判读**：砖缝、灰缝、剥落白灰、尖拱窗框与窗台**都在**，纹理映射正确；
> 但**断口边缘呈方块台阶**（不是参差碎砖），且**整体厚度 ≈ 高度**（见 §Technical Specs）。
> ⚠️ 512 px 缩略图上的「横向层纹」是**砖缝**，不是重建伪影 —— 判读须放大后再下结论。

## Technical Specs

| Property | Value | Notes |
|---|---|---|
| 面数预算 | **20,000**（hero 档） | 实测 medium **499,936** = **25×**；`low` 99,962 = 5× |
| 贴图 | **2 × 4096²**（albedo + MetallicRoughness） | **无 normal**（建筑模块允许，§8.9.2 走顶点法线） |
| 目标尺寸 | **暂定 6.054 × 6.037 × 6.000 m**（按高 6 m 归一） | ⚠️ **深度 ≈ 高度** ⇒ 这是**带厚基座的墙体碎块**,不是薄墙;真实比例须裁基座或非均匀压缩 |
| 材质槽 | 1 | |
| LOD | **未生成**（要求 3 级） | |
| 碰撞 | **未建**（须与渲染网格分离） | 好消息:清理后为**闭合实体**,碰撞可由渲染网格派生 |
| 清理状态 | ✅ **已清理**（2026-11-12） | 非流形边 **0** · 边界边 **0**(闭合) · 删碎面 0 · 顶点 335,961 → 249,852 |

## Asset Breakdown

| 文件 | 面数 | 状态 |
|---|---|---|
| `church-wall-medium.glb` | 499,936 | ✅ 已生成（zip 内 `pbr/mesh_textured_pbr.glb`） |
| `church-wall-low.glb` | 99,962 | ✅ 已生成（同参数 `subdivisionlevel low`） |
| `church-wall.glb`（清理版） | 499,936 | ✅ 2026-11-12 · 顶点 249,852 · 0 非流形 · **0 边界边（闭合实体）** · 38 MB · sha256 `bca4c72d212ba55c…` |

## AI Generation Prompt

**概念图（t2i，`sensenova-u1.5-fast`）**

```
一面被炮弹炸塌的晚清教堂残墙，灰砖与剥落白灰，左上角整块炸飞、断口向右下斜切，
右侧墙垛较高并残留一扇尖拱窗，墙体有厚度，断口处能看到砖层剖面，墙脚散落碎砖，
四分之三侧视（不是正面），能看出墙的厚度与纵深，孤立单体，浅灰纯色背景，
无底座无草地，低饱和，游戏资产参考图，无文字，无水印
```

**图生 3D（火山方舟 Seed3D）**

```
--subdivisionlevel medium --fileformat glb
```

### Generation Record（§8.10.2 + §8.10.6.3）

| 字段 | 值 |
|---|---|
| `prompt` | 见上方两个代码块（概念图 + 服务参数） |
| `model` | `sensenova-u1.5-fast`（概念图）· `doubao-seed3d-2-0-260328`（3D） |
| `iterations` | 概念图 5 轮（v1 → v2 不对称断口 → v3 左上方炸飞 → v4 落砖 → v5 四分之三侧视）；3D 1 轮 |
| `seed` | **`none`** — Seed3D 无 seed 参数；概念图 seed 未记录（`none`） |
| `human_edits` | **`none`**（仅脚本：RMBG-1.4 抠图 → 512² 白底） |
| `budget_tier` | **`hero`** — 场景 A，本场景 hero 第 1/3 件 |
| `source_images` | `assets/design-references/3d-pilot/input-sn-v3/church-wall-3q.png`（sha256 `655b11fed2f6bc0e…`） |
| `service_task` | `cgt-20261112094856-gk4zm`（medium，398 s）· `cgt-20261112095904-fnbhk`（low，460 s）· 调用日 2026-11-12 |
| `reconstruction` | 图生 3D，单图，`subdivisionlevel=medium` / `low`，radius / threshold 由服务端定（不可调） |
| `dcc_cleanup` | **Blender 4.5.16 LTS** · 合并重复顶点（1e-5）→ 去退化 → 重算法线 → 归一 6.000 m → 轴心移到包围盒中心；**未减面** |
| `license_review` | **⏸ 待核** — 火山方舟 ToS 原文未取得（见 §8.10.6.11-A）；输出不可复现、服务端版本不可钉 |

## Open Questions

- [ ] **ToS 与输出权属**：`⏸` 未结前，本件仅可作试点评估，**不得进 Addressables / 构建 / 发行包**。
- [ ] **厚度与比例**：按高 6 m 归一后**深度同为 6.04 m** —— 这件是**带厚基座的墙体碎块**，
  不是薄墙。须二选一：(a) DCC 里裁掉基座再归一；(b) 非均匀压缩深度（会把砖缝比例拉扁）。**未定**。
- [ ] **断口台阶**：左上缺口边缘呈**方块台阶**，不是概念要求的参差碎砖断口。DCC 手工可修，非自动过程。
- [x] ~~**清理**~~ ✅ **2026-11-12 已完成**：非流形边 0、边界边 0（闭合实体）、顶点 249,852。
  §8.10.6.9 的「清理后」判据自此对本件生效。
