# Asset Spec: 教学纸近景

> **Tier**: Vertical Slice Critical
> **Category**: HUD Element / Tutorial Display
> **Source**: hud.md §6, paper-closeup-48.md, ADR-013 Amendment B
> **Art Bible Ref**: §7.2 纸面元件库（教学纸近景 = 纸面放大）; §1 P2「纸面元素在应急光下仍可读」
> **ADR Ref**: ADR-013 §十（ModalId.PaperCloseup48 闭集第七员）; AC-42-F1 文本同步

---

## Visual Description

**教学纸近景**是教学态的核心纸面承载。形态为**单张纸页放大**——占屏 60%，纸面放大到足以阅读手写批注。

- **纸张**: 宣纸质感，微黄底色，纤维纹理可见。纸面有轻微泛黄（非新纸），边角有磨损痕迹。
- **墨迹**: 手写体教学文本（毛笔风格），楷书为主，行书点缀。墨色浓淡不一（模拟真实毛笔压力变化）。
- **版式**: 单页横排（承清末民初账簿 / 账本风格——承 art-bible §2「卷宗/账本形态」），页边有红色竖线（界行）。
- **状态痕迹**: 纸面有**手写批注**（红笔圈点 + 红笔箭头），承教学场景「师父批注」叙事。
- **动画**: 纸页从屏幕外滑入占屏 60%，0.3s ease-out；关闭时滑出 0.2s ease-in。
- **材质归属**: 整体 = 墨侧（纸面 + 墨迹 + 红笔批注）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 (page close-up) | 纸面放大，可读手写批注 |
| **Format** | PNG (sRGB) | 纸面底 + 墨迹 + 红笔批注 |
| **Material slots** | 3 (paper_base + ink_text + red_annotations) | 纸面底 / 墨迹文本 / 红笔批注 |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `paper_closeup_base.png` | Texture | 宣纸底 + 界行，2048×2048 | P0 |
| `paper_closeup_ink_text.png` | Texture | 手写体教学文本（楷书 + 行书），含墨迹浓淡变体 | P0 |
| `paper_closeup_red_annotations.png` | Texture | 红笔批注（圈点 + 箭头 + 下划线） | P0 |
| `paper_closeup_wear.png` | Texture | 边角磨损 / 泛黄 / 折痕细节层 | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Closed** | 纸页不可见 |
| **Opening** | 纸页从屏幕外滑入占屏 60%，0.3s ease-out |
| **Open — Reading** | 纸面全展开，手写文本 + 红笔批注清晰可读 |
| **Open — Next Page** | 纸页翻页 0.2s ease-in-out（左右滑动） |
| **Open — Dismiss** | 纸页滑出 0.2s ease-in |
| **Focus** | 黄 brass 边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 墨迹浓 | `#1A1714` (near-black ink) | 教学文本 |
| 墨迹淡 | `#4A4640` (faded ink) | 辅助文本 |
| 红笔批注 | `#C13A3A` (vermilion red) | 批注标记 |
| 界行红 | `#8B3A3A` (seal red) | 竖线界行 |
| 纸面泛黄 | `#E8DFC8` (aged paper) | 边缘 |

---

## Accessibility

- 纸面正文对比度 ≥ 7:1（AB-4 已裁定）
- 墨迹浓 ≠ 仅颜色区分——有笔触粗细 / 位置 / 形态差异
- 红笔批注 ≠ 仅颜色区分——有圈点 / 箭头 / 下划线形态差异
- 焦点高亮 = 黄 brass 边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
A close-up of a traditional Chinese instructional paper (教学纸近景), taking 60% of screen.
Style: Late Qing / early Republican era, circa 1910s.
Material: Rice paper (宣纸) with warm yellowish tone, visible fiber texture.
Content: Handwritten Chinese characters in calligraphy style (楷书 for main text, 行书 for annotations), red vermilion pen annotations (圈点 + arrows + underlines).
Layout: Single page, horizontal text (账簿 style), red vertical guide lines.
Details: Slight wear at edges, minor aging discoloration, red pen marks from teacher's annotations.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Educational, scholarly, historical.
Constraints: NO modern UI, NO digital elements. Pure paper and ink aesthetic.
Resolution: 2048x2048, high detail for close-up reading.
```

---

## Open Questions

- [ ] 教学纸是否支持多页（P0 = 单页；P1a = 多页翻页）？
- [ ] 红笔批注是否随教学进度动态显示（逐条出现 vs 全页一次显示）？
- [ ] 教学纸是否支持玩家手写笔记（P0 = 只读；P1a = 手写笔记）？

---

## Related Files

- `design/ux/hud.md` §6 — 教学纸近景 HUD spec
- `design/gdd/tutorial-system.md` — 教学系统
- `design/art/art-bible.md` §7.2 — 纸面元件库规格
- `docs/architecture/adr-013-skeuomorphic-ui-framework.md` Amendment B — ModalId.PaperCloseup48
