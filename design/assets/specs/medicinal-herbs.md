# Asset Spec: 药材（P0 六种）

> **Tier**: Vertical Slice Critical
> **Category**: Item / Medicinal Herb
> **Source**: foraging.md, item-database.md, prescription-and-medication.md, emergency-procedures.md
> **Art Bible Ref**: §7.2 墨侧主导（药材 = 植物形态图标）; §1 P2「纸面元素在应急光下仍可读」
> **ADR Ref**: ADR-014（数据管线：药材属性 JSON → 构建期烘焙 → `*.cooked`）; ADR-003（模块化网格，药材格 = 建造槽位同格）

---

## Visual Description

**药材**是库存系统的核心 consumable 物品，P0 六种基础药材。每种药材有**实物线描图标**（非抽象符号）+ **3D 模型**（世界态可拾取）。

### 药材列表（P0）

| # | 药材名 | 来源 | 外观 | 用途 |
|---|--------|------|------|------|
| 1 | 柳树皮 | 野外采集（河边） | 灰褐色树皮碎片，卷曲 | 退热 / 止痛 |
| 2 | 毛地黄 | 野外采集（林下） | 紫色钟形花 + 绿色叶片 | 强心（急救） |
| 3 | 金鸡纳树皮 | 野外采集（深山） | 深褐色树皮，有白色纹理 | 退热（疟疾） |
| 4 | 止血草 | 野外采集（草地） | 绿色细叶，折断处有红汁 | 止血（急救） |
| 5 | 针具（针灸） | 初始装备 | 细长银针，针尖微弯 | 针灸 / 急救 |
| 6 | 绷带 / 止血包 | 初始装备 | 卷状白布，有血渍（使用后） | 止血包扎 |

- **图标风格**: 实物线描（承 art-bible §7.2），墨迹浓淡不一，留白 = 药材形态轮廓。
- **3D 模型**: 简单几何体（树皮 = 弯曲片状；花 = 低多边形钟形；针 = 细长圆柱；绷带 = 卷状），无需高精度。
- **材质归属**: 整体 = 墨侧（植物形态 / 布纹）；针具 = 铜侧（银针反光）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 64×64 (icon) / 256×256 (world 3D texture) | 图标 + 世界态纹理 |
| **Format** | PNG (sRGB) | 墨迹风格 |
| **Material slots** | 1 (ink_illustration) | 墨迹单层 |
| **Poly count** | ~50 tris (world 3D) | 简单几何体 |
| **LOD** | LOD0 only | 世界态可拾取，距离近 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown（六种药材合并）

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `item_herb_willow_bark.png` | Icon + Texture | 柳树皮（灰褐色卷曲碎片） | P0 |
| `item_herb_foxglove.png` | Icon + Texture | 毛地黄（紫色钟形花 + 绿叶） | P0 |
| `item_herb_chinchona.png` | Icon + Texture | 金鸡纳树皮（深褐 + 白纹） | P0 |
| `item_herb_hemostatic.png` | Icon + Texture | 止血草（绿色细叶 + 红汁） | P0 |
| `item_tool_acupuncture.png` | Icon + Texture | 针具（银针，铜侧反光） | P0 |
| `item_tool_bandage.png` | Icon + Texture | 绷带（卷状白布 + 血渍） | P0 |
| `herb_world_3d.fbx` | Mesh | 药材 3D 模型（6 种合并，简单几何体） | P0 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **World — Idle** | 药材散落于地面（采集后），轻微摇曳（风） |
| **World — Highlighted** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |
| **Inventory — Empty Slot** | 格子空置，木底可见（出诊箱） |
| **Inventory — Filled** | 格子内有药材图标 + 铜筹计数 |
| **Inventory — Selected** | 当前选中格子边框高亮（黄铜色 2px） |
| **Inventory — Depleted** | 格子空置，铜筹为 0（铜筹柱消失 = 空） |

---

## Color Palette (from Art Bible)

| 药材 | 主色 | 辅色 | 角色 |
|-------|------|------|------|
| 柳树皮 | `#8B7355` (灰褐) | `#6B5B45` (深褐) | 墨侧 |
| 毛地黄 | `#7B3F8D` (紫色) | `#4A7C59` (绿叶) | 墨侧 + 点缀色 |
| 金鸡纳树皮 | `#4A3728` (深褐) | `#D4C4B0` (白纹) | 墨侧 |
| 止血草 | `#4A7C59` (绿色) | `#C13A3A` (红汁) | 墨侧 + 点缀色 |
| 针具 | `#C0C0C0` (silver) | `#D4A84B` (铜侧反光) | 铜侧 |
| 绷带 | `#F5F0E8` (米白) | `#8B3A3A` (血渍) | 墨侧 |

---

## Accessibility

- 药材图标 ≠ 仅颜色区分——有形态（树皮卷曲 / 花形 / 针形 / 布卷）+ 纹理差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）
- 血渍（绷带使用后）= 双通道（颜色 + 形态变化）

---

## AI Generation Prompt (for reference)

```
Traditional Chinese medicinal herbs and tools, ink wash illustration style.
Style: Late Qing / early Republican era, circa 1910s.
Items: Willow bark (gray-brown curled bark), foxglove (purple bell-shaped flowers), quinine bark (dark brown with white striations), hemostatic herb (green slender leaves with red sap), acupuncture needles (thin silver), bandage (rolled white cloth).
Style: Ink wash (水墨) with varying pressure, rice paper texture background.
Details: Hand-drawn botanical illustrations, NO captions, NO Chinese characters — item names rendered via UXML Label on the casebook labels layer.
Lighting: Flat, no shadows, ink on paper aesthetic.
Mood: Scholarly, precise, historical medical documentation.
Constraints: NO modern medical packaging, NO digital elements, NO text labels in texture. Pure ink illustration on rice paper.
Resolution: 64x64 icons, 256x256 world textures.
```

---

## Open Questions

- [x] ~~药材是否支持品质等级（Q1–Q10 色阶）—— P0 = 无等级；P1a = 引入？？~~ → **2026-09-29 裁定：P0 = 无品质等级**。
- [x] ~~药材是否支持加工（干燥 / 提取物 / 酊剂）—— P0 = 原形态；P1a = 加工形态？？~~ → **2026-09-29 裁定：原形态**。
- [x] ~~药材图标是否随库存数量变化（铜筹堆叠 = 数量；P0 = 单一图标）？？~~ → **2026-09-29 裁定：单一图标**。
---