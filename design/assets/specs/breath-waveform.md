# Asset Spec: 呼吸波形纸带

> **Tier**: Vertical Slice Critical
> **Category**: HUD Element / Diagnostic Display
> **Source**: hud.md §2, audio-system.md F-44.7, AC-44-01 BLOCKING
> **Art Bible Ref**: §7.2 纸面元件库（呼吸波形 = 纸带墨迹）; §1 P2「纸面元素在应急光下仍可读」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-018 §一（44 只触发 / 只渲染，永不持有游戏状态）

---

## Visual Description

**呼吸波形纸带**是诊断态五通道之一（闻诊通道）的核心视觉承载。形态为**滚动纸带**——两纹（通带 + 噪声底）实时滚动。

- **纸带**: 宣纸质感，微黄底色，纤维纹理可见。纸带从右向左滚动（承心电图经典方向）。
- **波形墨迹**: 两纹分别以**不同墨色**绘制：
  - **通带纹（主纹）**: 浓墨（`#1A1714`），流畅曲线，反映呼吸节律。
  - **噪声底纹（辅纹）**: 淡墨（`#4A4640`），高频微颤，反映噪声底强度。
- **双纹关系**: 两纹并行滚动，间距固定（承 audio-system.md F-44.7「两层不同声像」）。主纹振幅 = 呼吸深度；辅纹振幅 = 噪声底强度。
- **精度档驱动**: 通带 / 噪声底参数由 `stethoscope_material_coeff.png` 材质系数表驱动（承 ADR-014 烘焙管线）。
- **材质归属**: 整体 = 墨侧（纸面 + 墨迹）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×256 (texture, scrollable) | 纸带 + 双波形 |
| **Format** | PNG (sRGB) | 纸面纹理 + 墨迹 |
| **Material slots** | 2 (paper_base + ink_waveform) | 纸面底 / 波形墨迹 |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套 |
| **Update rate** | 20 Hz (TICK_SECONDS = 0.05s) | 承 ADR-005 定点域节奏 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `breath_paper_tape.png` | Texture | 宣纸底 + 界行，1024×256 | P0 |
| `breath_waveform_main.png` | Texture | 通带纹（浓墨），动态滚动 | P0 |
| `breath_waveform_noise.png` | Texture | 噪声底纹（淡墨），动态滚动 | P0 |
| `breath_wear.png` | Texture | 纸带磨损 / 折痕 / 墨迹晕染 | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 纸带静止，双纹平直 |
| **Active — Normal Breath** | 通带纹流畅滚动（呼吸节律 20 Hz），噪声底纹微颤 |
| **Active — Abnormal Breath** | 通带纹振幅异常（呼吸过速 / 过缓），噪声底纹增强 |
| **Active — Different Material** | 通带/噪声底参数随材质系数变化（薄衣 vs 厚衣 vs 皮肤） |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 通带纹（浓墨） | `#1A1714` (near-black ink) | 主纹，呼吸节律 |
| 噪声底纹（淡墨） | `#4A4640` (faded ink) | 辅纹，噪声底 |
| 界行红 | `#8B3A3A` (seal red) | 竖线界行 |
| 纸面泛黄 | `#E8DFC8` (aged paper) | 边缘 |

---

## Accessibility

- 双纹 ≠ 仅颜色区分——有振幅（通带 vs 噪声底）+ 频率（流畅 vs 微颤）差异
- 波形滚动方向 = 从左到右（承心电图经典方向，降低认知负担）
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
A breathing waveform paper tape (呼吸波形纸带), scroll view.
Style: Late Qing / early Republican era, circa 1910s.
Material: Rice paper (宣纸) with warm yellowish tone, visible fiber texture.
Ink: Two parallel waveforms — main waveform (通带) in thick black ink, noise baseline (噪声底) in thin faded ink.
Details: Paper scroll from right to left, red vertical guide lines, slight wear at edges.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Scholarly, precise, medical monitoring.
Constraints: NO digital waveform display, NO modern medical equipment. Pure ink on rice paper aesthetic.
Resolution: 1024x256 texture, scrollable.
```

---

## Open Questions

- [x] ~~波形更新率（P0 = 20 Hz 承 TICK_SECONDS；P1a = 更高精度）？？~~ → **2026-09-29 裁定：P0 = 20Hz**。
- [x] ~~纸带是否支持暂停 / 回放（P0 = 实时滚动；P1a = 暂停查看历史）？？~~ → **2026-09-29 裁定：实时滚动**。
- [x] ~~双纹间距是否固定（P0 = 固定；P1a = 随呼吸深度动态变化）？？~~ → **2026-09-29 裁定：双纹固定间距**。
---