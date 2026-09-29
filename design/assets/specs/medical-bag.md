# Asset Spec: 出诊箱（药箱）

> **Tier**: Vertical Slice Critical
> **Category**: Prop / Inventory Container
> **Source**: hud.md §3, inventory-container-20.md, 20
> **Art Bible Ref**: §7.2 纸面元件库（出诊箱 = 墨侧主导）; §1 P3「黄铜必须有来历」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-003（模块化网格，出诊箱格子 = 建造槽位同格）

---

## Visual Description

**出诊箱**是玩家的 inventory 容器。形态为**老式中药铺出诊箱**——木框 + 铜扣 + 抽屉式格子。

- **箱体**: 深色木框（胡桃木色），四角有黄铜包角。箱盖可翻开，内衬深红绒布。
- **格子**: 抽屉式分隔，每格放一种药材/工具。格子大小统一（承 modular-building 建造槽位同格逻辑，视觉上对齐）。
- **铜筹计数**: 每种药材数量以**铜柱堆叠**表示（非数字角标）。铜筹 = 小黄铜柱，高度 = 数量。
- **图标**: 每种药材/工具有**实物线描图标**（非抽象符号）。针具 = 细长银针图标；绷带 = 卷状布图标；药材 = 植物形态图标。
- **材质归属**: 箱体 = 墨侧（木框 + 纸签标签）；铜扣/铜筹 = 铜侧（黄铜錾刻 + 机械位移感）。
- **展开动画**: 抽屉滑出 0.25s ease-out。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture atlas) | 含格子底 + 铜筹 + 图标 |
| **Format** | PNG (sRGB) | 纸面纹理 + 墨迹标签 |
| **Material slots** | 3 (wood_base + brass_hardware + icon_overlay) | 木框 / 铜扣 / 图标层 |
| **Poly count** | ~200 tris (3D prop) | 简单 box + 抽屉分隔 |
| **LOD** | LOD0 only (always visible when open) | 出诊箱仅在诊断态展开时可见 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `medical_bag_wood.png` | Texture | 木框底 + 内衬绒布，1024×1024 | P0 |
| `medical_bag_brass.png` | Texture | 铜扣 / 包角 / 铜筹，含錾刻细节 | P0 |
| `medical_bag_icons.png` | Texture Atlas | 药材/工具线描图标（12 格 × 64px），含铜筹堆叠图 | P0 |
| `medical_bag_labels.png` | Texture | 纸签标签（药材名，AI 生成含完整版式的材质层 → DA 裁切为独立纹理；动态文字内容由 UXML `<Label>` 叠层承载） | P0 |
| `medical_bag_3d.fbx` | Mesh | 出诊箱 3D 模型（木框 + 铜扣 + 抽屉分隔） | P0 |
| `medical_bag_wear.png` | Texture | 磨损 / 划痕 / 使用痕迹 | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Closed** | 箱体闭合，铜扣扣紧，平放于地面/桌面 |
| **Opening** | 箱盖翻开 0.25s，抽屉依次滑出 |
| **Open — Empty** | 格子空置，木底可见 |
| **Open — Filled** | 格子内有药材图标 + 铜筹计数 |
| **Open — Selected** | 当前选中格子边框高亮（黄铜色 2px） |
| **Open — Depleted** | 格子空置，铜筹为 0（不显示数字，铜筹柱消失 = 空） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 木框 | `#5C4033` (walnut brown) | 墨侧主导 |
| 内衬绒布 | `#8B2323` (deep red) | 墨侧暖色 |
| 黄铜扣 | `#B8863B` (brass) | 铜侧，有錾刻纹理 |
| 铜筹 | `#D4A84B` (bright brass) | 铜侧，堆叠数量感 |
| 纸签 | `#F5F0E8` (rice paper) | 墨侧，AI 材质层（文字内容由 UXML `<Label>` 承载） |
| 墨迹 | `#1A1714` (ink) | 药材名墨迹质感 |

---

## Accessibility

- 格子可聚焦（44×44px 焦点目标，承 AB-3）
- 选中状态 = 黄铜边框 2px + 纸面压痕（非颜色区分）
-  depleted 状态 = 铜筹消失 + 格子墨色加深（双通道）

---

## AI Generation Prompt (for reference)

```
An antique Chinese medical bag (出诊箱), open view showing divided compartments.
Style: Late Qing / early Republican era, circa 1910s.
Materials: Dark walnut wood frame with brass corners and clasps. Red velvet lining inside. Brass coin-shaped counters stacked to indicate quantity.
Compartments: Grid of small drawers, each containing different herbal medicine icons (hand-drawn botanical illustrations in ink style).
Details: Slight wear on wood surface, brass hardware with subtle engraving. Paper labels with handwritten Chinese characters.
Lighting: Warm ambient, soft shadows, flat lay or slight perspective view.
Mood: Scholarly, meticulous, historical medical instrument.
Constraints: NO modern UI, NO digital elements. Pure physical object aesthetic.
Resolution: 1024x1024 texture atlas, individual icons 64x64px each.
```

---

## Open Questions

- [x] ~~格子数量上限（P0 = 12 格？P1a 扩展？）—— 待 20 GDD 裁定？~~ → **2026-09-29 裁定：P0 = 12格**。
- [x] ~~铜筹堆叠上限（最多几枚铜柱？超限如何处理？）？~~ → **2026-09-29 裁定：铜筹堆叠上限待20GDD裁定**。
- [x] ~~药材图标是否随品质等级变色（Q1–Q10 色阶）？？~~ → **2026-09-29 裁定：不随品质变色**。
---