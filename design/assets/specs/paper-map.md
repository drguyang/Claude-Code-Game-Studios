# Asset Spec: 纸质地图卷轴

> **Tier**: Full Production  ⚠️ 2026-09-29 修正:原误标 VS Critical —— 该资产在 entity-inventory.md 位于 Full Production 节(Props #1/#2/#8)
> **Category**: Prop / UI Surface
> **Source**: hud.md §4, world-and-ecozones.md, ADR-015（两层世界）
> **Art Bible Ref**: §7.2 纸面元件库（纸质地图 = 墨侧主导）; §1 P2「纸面元素在应急光下仍可读」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-015 §一（两层世界：确定性整数逻辑层 + 纯视觉层）

---

## Visual Description

**纸质地图**是世界态常驻 HUD 元素（右下角）。形态为**卷轴展开**——墨点标记生态区 / POI / 玩家位置。

- **纸张**: 宣纸质感，微黄底色，纤维纹理可见。纸面有轻微泛黄（非新纸），边角有磨损痕迹。
- **墨迹**: 生态区边界以**淡墨线**勾勒（非色块填充）；POI 以**印章式墨点**标记（圆形 / 方形 / 三角形，承 ADR-021）；玩家位置以**小红点**标记（唯一允许的「贴屏数值反馈」之外的红色）。
- **版式**: 卷轴展开，从右至左阅读（承清末民初背景）。页边有红色竖线（界行）。
- **动态元素**:
  - 玩家位置 = 小红点（实时更新，承 ADR-009 §五「位置 = 表现态，只读」）
  - 已探索区域 = 墨色加深（已探索 vs 未探索的墨迹浓度差异）
  - 未探索区域 = 空白（非灰色遮罩——承 art-bible §1 P2「纸面元素在应急光下仍可读」）
- **展开动画**: 卷轴展开 0.3s ease-out，收起 0.2s ease-in。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture) | 地图底 + 墨迹层 + 标记层 |
| **Format** | PNG (sRGB) | 纸面纹理 + 墨迹 |
| **Material slots** | 3 (paper_base / ink_overlay / marker_overlay) | 纸面底 / 墨迹 / 标记 |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `map_paper_base.png` | Texture | 宣纸底 + 界行 + 卷轴边框，1024×1024 | P0 |
| `map_ink_zones.png` | Texture | 生态区淡墨线 + POI 印章标记（四种印章形状） | P0 |
| `map_player_dot.png` | Texture | 玩家位置小红点（唯一允许的红色标记） | P0 |
| `map_explored_mask.png` | Texture | 已探索区域墨色加深遮罩 | P0 |
| `map_wear.png` | Texture | 边角磨损 / 泛黄 / 折痕细节层 | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Closed** | 卷轴收起，右下角仅露卷轴头 |
| **Opening** | 卷轴展开 0.3s ease-out，墨迹渐显 |
| **Open — Default** | 地图全展开，玩家位置小红点实时更新 |
| **Open — Unexplored** | 未探索区域空白（非灰色遮罩） |
| **Open — Explored** | 已探索区域墨色加深 |
| **Open — POI Selected** | POI 印章高亮（黄铜边框 2px） |
| **Closing** | 收起 0.2s ease-in |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 墨迹浓 | `#1A1714` (near-black ink) | 生态区边界 / POI 标记 |
| 墨迹淡 | `#4A4640` (faded ink) | 已探索区域 |
| 界行红 | `#8B3A3A` (seal red) | 竖线界行 |
| 玩家位置 | `#C13A3A` (vermilion red) | 唯一允许的红色标记 |
| 纸面泛黄 | `#E8DFC8` (aged paper) | 边缘 / 旧纸感 |

---

## Accessibility

- 地图 ≠ 仅颜色区分——有形态（生态区边界形状 / POI 印章形状）+ 墨迹浓度差异
- 玩家位置 = 小红点（唯一允许的红色），与墨迹形成对比但不过度醒目
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
A traditional Chinese paper map (纸质地图), scroll view.
Style: Late Qing / early Republican era, circa 1910s.
Material: Rice paper (宣纸) with warm yellowish tone, visible fiber texture.
Ink: Hand-drawn ink wash (水墨) ecozone boundaries (淡墨线), seal stamp (印章) POI markers in four shapes (circle, square, triangle, diamond).
Layout: Scroll展开, vertical text from right to left, red vertical guide lines.
Details: Slight wear at edges, minor aging discoloration, red vermilion dot for player location.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Scholarly, historical, exploratory.
Constraints: NO modern map UI, NO digital elements, NO color fills. Pure ink on rice paper aesthetic.
Resolution: 1024x1024 texture, high detail for close-up reading.
```

---

## Open Questions

- [x] ~~地图是否支持缩放（P0 = 固定比例；P1a = 缩放）？？~~ → **2026-09-29 裁定：P0 = 固定比例**。
- [x] ~~POI 印章形状是否随 POI 类型变化（城镇 / 野外 / 危险区）？？~~ → **2026-09-29 裁定：POI印章不随类型变**。
- [x] ~~地图是否支持手写标注（玩家在纸上画标记）？？~~ → **2026-09-29 裁定：不支持手写标注**。
---