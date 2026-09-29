# Asset Spec: 舌象色卡

> **Tier**: Vertical Slice Critical
> **Category**: HUD Element / Diagnostic Display
> **Source**: hud.md §2, diagnosis-system.md V-8.0
> **Art Bible Ref**: §7.2 纸面元件库（舌象色卡 = 纸面色块比对）; §1 P2「纸面元素在应急光下仍可读」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-016 §三（13 病人 AI = 只读消费者，读 VitalsDto，不引用 sim 状态）

---

## Visual Description

**舌象色卡**是诊断态五通道之一（舌诊通道）的核心视觉承载。形态为**纸面色块比对卡**——非数字，非照片，是色块。

- **纸面**: 宣纸质感，微黄底色，纤维纹理可见。纸面有轻微泛黄（非新纸）。
- **色块**: 9 个圆形色块（承诊断系统舌象九分类），每个色块 = 一种舌象（淡红 / 红 / 绛红 / 淡白 / 白 / 黄 / 黄腻 / 灰 / 黑）。
- **色块特征**:
  - 颜色 = 水墨色（非 RGB 色块——承 art-bible §1 P2「纸面元素在应急光下仍可读」）
  - 形态 = 圆形（非方形——承 art-bible §3.3「圆 / 弧 = 脉象连续感」，舌象同理）
  - 边缘 = 墨迹晕染（非硬边——承真实舌象非几何形状）
- **标记**: 当前病人舌象以**印章盖印**标记（红色印泥，非高亮边框）。已识舌象 = 印章盖入；未识舌象 = 空白圆。
- **材质归属**: 整体 = 墨侧（纸面 + 墨迹色块）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture) | 纸面 + 9 色块 + 印章标记 |
| **Format** | PNG (sRGB) | 纸面纹理 + 墨迹色块 |
| **Material slots** | 2 (paper_base + ink_color_swatches) | 纸面底 / 色块层 |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `tongue_paper_base.png` | Texture | 宣纸底 + 界行，1024×1024 | P0 |
| `tongue_color_swatches.png` | Texture | 9 个圆形墨迹色块（淡红 / 红 / 绛红 / 淡白 / 白 / 黄 / 黄腻 / 灰 / 黑） | P0 |
| `tongue_stamp.png` | Texture | 印章样式（红色印泥，标记当前舌象） | P0 |
| `tongue_wear.png` | Texture | 边角磨损 / 泛黄 / 折痕细节层 | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 9 色块展示，当前舌象以印章盖入 |
| **Active — New Patient** | 印章移至对应色块（0.3s ease-out） |
| **Active — Pattern Found** | 已识舌象模式以金色印章盖入（非红色——承 V-8.3「已识模式 ≠ 当前值」） |
| **Active — Confidence Low** | 色块留空（非文字说明，承 V-8.3） |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 淡红舌 | `#D4A0A0` (light pink-red ink) | 墨迹色块 |
| 红舌 | `#C13A3A` (vermilion red ink) | 墨迹色块 |
| 绛红舌 | `#8B2323` (deep red ink) | 墨迹色块 |
| 淡白舌 | `#D4C4B0` (light pale ink) | 墨迹色块 |
| 白舌 | `#C4B8A8` (pale ink) | 墨迹色块 |
| 黄舌 | `#C4A050` (yellow ink) | 墨迹色块 |
| 黄腻舌 | `#A08030` (yellow-greasy ink) | 墨迹色块 |
| 灰舌 | `#8B8B8B` (gray ink) | 墨迹色块 |
| 黑舌 | `#4A4A4A` (dark ink) | 墨迹色块 |
| 印章红 | `#C13A3A` (vermilion) | 当前舌象标记 |
| 印章金 | `#D4A84B` (brass gold) | 已识模式标记 |

---

## Accessibility

- 9 色块 ≠ 仅颜色区分——有圆形形态 + 墨迹浓淡 + 印章标记差异
- 色块排列 = 3×3 网格（从左到右，从上到下），焦点导航顺序固定
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
A traditional Chinese tongue diagnosis color card (舌象色卡), paper view.
Style: Late Qing / early Republican era, circa 1910s.
Material: Rice paper (宣纸) with warm yellowish tone, visible fiber texture.
Content: 9 circular ink wash color swatches arranged in 3x3 grid (淡红 / 红 / 绛红 / 淡白 / 白 / 黄 / 黄腻 / 灰 / 黑).
Details: Red vermilion seal stamp marking current tongue diagnosis, slight wear at edges, minor aging discoloration.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Scholarly, precise, medical diagnostic tool.
Constraints: NO digital color picker, NO RGB values. Pure ink wash on rice paper aesthetic.
Resolution: 1024x1024 texture, 9 color swatches clearly distinguishable.
```

---

## Open Questions

- [x] ~~舌象色块是否支持动态生成（根据病人实时舌象）—— P0 = 固定 9 色块；P1a = 动态生成？？~~ → **2026-09-29 裁定：P0 = 固定9色块**。
- [x] ~~印章是否随病种变化（每种疾病有独特印章）？？~~ → **2026-09-29 裁定：印章固定**。
- [x] ~~舌象色卡是否支持对比模式（两张舌象并排比对）？？~~ → **2026-09-29 裁定：不支持对比模式**。
---