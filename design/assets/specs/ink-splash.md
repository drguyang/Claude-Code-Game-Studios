# Asset Spec: 墨迹落笔

> **Tier**: Vertical Slice Critical
> **Category**: HUD Element / Diagnostic Feedback
> **Source**: hud.md §1, art-bible §8.7
> **Art Bible Ref**: §8.7「墨迹飞溅——诊结论落笔瞬间」; §1 P1「水墨 + 黄 brass 双轨」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-018 §六（镜头效果归属 = 8 语义 + 2 实现，全禁闪烁 / 抖动）

---

## Visual Description

**墨迹落笔**是诊断态的核心反馈 VFX。形态为**墨迹飞溅**——当诊结论（四诊读数 / 辨证记录）落笔时，墨迹从笔尖飞出，落在脉案纸页上。

- **触发时机**: 玩家完成四诊读数（望 / 闻 / 问 / 切四通道之一）后，结论落笔瞬间。
- **视觉表现**:
  - **笔尖墨迹**: 毛笔笔尖接触纸面时，墨迹扩散（0.1s ease-out）。
  - **飞溅墨点**: 墨迹飞溅（3–5 个墨点，大小不一，承 art-bible §8.7「墨迹飞溅」）。
  - **落定**: 墨点落在纸面上，形成墨迹印记（0.2s ease-out）。
- **动画节奏**: 笔尖接触 → 墨迹扩散 0.1s → 飞溅 0.15s → 落定 0.2s（总计 ≈ 0.45s）。
- **材质归属**: 墨侧（纯墨迹，无铜件）。
- **非文字**: 零文本提示（承 art-bible §1 P2「非数字」）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 256×256 (texture atlas) | 墨迹飞溅纹理 |
| **Format** | PNG (sRGB) | 墨迹黑 + 飞溅墨点 |
| **Material slots** | 1 (ink_splash) | 纯墨迹单层 |
| **Poly count** | N/A (VFX particle system) | UI Toolkit + VFX Graph |
| **LOD** | 不适用 | VFX 始终全精度 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 脉案纸页空白（无墨迹） |
| **Active — Writing** | 笔尖墨迹扩散 0.1s ease-out |
| **Active — Splash** | 墨迹飞溅 0.15s（3–5 墨点） |
| **Active — Settled** | 墨点落定 0.2s ease-out，形成墨迹印记 |
| **Focus** | 黄 brass 边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 墨迹浓 | `#1A1714` (near-black ink) | 主墨迹 |
| 墨迹淡 | `#4A4640` (faded ink) | 飞溅墨点 |
| 纸面底 | `#F5F0E8` (warm rice paper) | 落定背景 |

---

## Accessibility

- 墨迹飞溅 ≠ 仅颜色区分——有形态（扩散形状）+ 动画节奏（笔尖接触 → 扩散 → 飞溅 → 落定）
- 零闪烁 / 零抖动（承 ADR-018 §六 无提示音铁律视觉侧）

---

## AI Generation Prompt (for reference)

```
Ink wash splatter VFX (墨迹落笔), close-up on rice paper.
Style: Late Qing / early Republican era, circa 1910s.
Material: Rice paper (宣纸) with warm yellowish tone.
Effect: Brush tip touching paper, ink spreading (0.1s), ink droplets splashing (3-5 droplets, varying sizes), ink marks settling on paper (0.2s).
Details: Natural ink wash diffusion, organic droplet shapes, slight feathering at edges.
Lighting: Warm ambient, flat view.
Mood: Scholarly, precise, calligraphic.
Constraints: NO digital effects, NO harsh edges. Pure ink wash aesthetic.
Resolution: 256x256 texture atlas, ink splash clearly visible.
```

---

## Open Questions

- [ ] 墨迹飞溅是否随四诊通道变化（望 = 淡墨 / 闻 = 浓墨 / 问 = 飞溅多 / 切 = 飞溅少）？
- [ ] 墨迹是否支持「涂改」效果（错误结论 → 墨迹覆盖）？
- [ ] 墨迹落笔是否叠加音频层（纸面物理声）？

---

## Related Files

- `design/ux/hud.md` §1 — 脉案纸页 HUD spec
- `design/art/art-bible.md` §8.7 — 墨迹飞溅规格
- `design/gdd/audio-system.md` — 纸面物理声（翻脉案 / 落笔 / 翻纸）
