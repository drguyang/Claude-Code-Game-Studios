# Asset Spec: 脉案纸页（线装书）

> **Tier**: Vertical Slice Critical
> **Category**: Prop / UI Surface
> **Source**: hud.md §1, casebook-39.md, case-system.md
> **Art Bible Ref**: §7.2 纸面元件库（宣纸纤维 / 墨迹 / 印章）; §1 P3「纸面正文对比承诺 ≥7:1」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-013 §十（ModalId.Casebook 闭集第一员）

---

## Visual Description

**脉案**是诊断态的核心纸面承载。形态为**线装书展开**——左页「四诊摘要」，右页「辨证记录」。

- **纸张**: 宣纸质感，微黄底色，纤维纹理可见。纸面有轻微泛黄（非新纸），边角有磨损痕迹。
- **墨迹**: 手写体病名（毛笔风格），四诊要点以**楷书**书写，辨证记录以**行书**书写。墨色浓淡不一（模拟真实毛笔压力变化）。
- **版式**: 线装书页，竖排从右至左。页边有红色竖线（界行）。书脊处可见线装绳结。
- **状态痕迹**: 空白行 = 未填写（非空格占位，是「空行即答案」的纪律）；已填写行有墨迹；错误/改写处有轻微涂改痕迹（非删除线）。
- **翻页动画**: 书页展开 0.3s ease-out，收起 0.2s ease-in。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 (page spread) | 两页并排，左四诊 + 右辨证 |
| **Format** | PNG (sRGB) | 纸面底 + 墨迹分层 |
| **Material slots** | 2 (paper_base + ink_overlay) | 墨迹独立层，可动态替换内容 |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套纸面 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `casebook_paper_base.png` | Texture | 宣纸底 + 界行 + 线装绳结，2048×2048 | P0 |
| `casebook_ink_font.png` | Texture Atlas | 手写体字库（病名楷书 + 四诊楷书 + 辨证行书），含墨迹浓淡变体 | P0 |
| `casebook_stamp.png` | Texture | 印章样式（已识模式标记 / 鉴别诊断标记） | P0 |
| `casebook_wear.png` | Texture | 边角磨损 / 泛黄 / 折痕细节层 | P1a |
| `casebook_cover.png` | Texture | 线装书封面（深褐漆面，书名烫金） | P1a |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Closed** | 书合拢，仅书脊可见 |
| **Opening** | 书页展开 0.3s ease-out，墨迹渐显 |
| **Open — Default** | 左页四诊摘要（空白行 = 未查），右页辨证记录（空白） |
| **Open — Filling** | 四诊读数落笔，墨迹逐通道填入对应行 |
| **Open — Pattern Found** | 已识模式以印章盖入（红色印泥，非数字） |
| **Open — Confidence Low** | 病名栏用「?」或留空（非文字说明，承 V-8.3） |
| **Closing** | 合书 0.2s ease-in |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 纸面底 | `#F5F0E8` (warm rice paper) | 主底 |
| 墨迹浓 | `#1A1714` (near-black ink) | 已填写内容 |
| 墨迹淡 | `#4A4640` (faded ink) | 辅助文字 / 界行 |
| 界行红 | `#8B3A3A` (seal red) | 竖线界行 |
| 印章红 | `#C13A3A` (vermilion) | 模式标记 |
| 纸面泛黄 | `#E8DFC8` (aged paper) | 边缘 / 旧纸感 |

---

## Accessibility

- 纸面正文对比度 ≥ 7:1（AB-4 已裁定）
- 墨迹浓 ≠ 仅颜色区分——有笔触粗细 / 位置 / 形态差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
A traditional Chinese medical casebook (脉案), open spread view.
Left page: "四诊摘要" (Four Diagnostic Methods summary) with blank ruled lines.
Right page: "辨证记录" (Pattern Differentiation record) with blank lines.
Style: Late Qing / early Republican era (清末民初), circa 1910s.
Material: Rice paper (宣纸) with warm yellowish tone, visible fiber texture.
Ink: Handwritten Chinese characters in calligraphy style (楷书 for diagnostic items, 行书 for differentiation notes), black ink with varying pressure.
Layout: Vertical text from right to left, red vertical guide lines, thread-bound spine visible at center.
Details: Slight wear at edges, minor aging discoloration, red seal stamps (印章) for marked patterns.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Scholarly, meticulous, historical authenticity.
Constraints: NO HP bars, NO numbers, NO modern UI elements. Pure paper and ink aesthetic.
Resolution: 2048x2048, high detail for close-up reading.
```

---

## Open Questions

- [ ] 字体是否引入商用字库（楷体 / 行书）还是手写扫描？
- [ ] 印章图案是否随病种变化（每种疾病有独特印章）？
- [ ] 方笺页（续页）是否复用同一纸面底 + 不同墨迹层？

---

## Related Files

- `design/ux/casebook-39.md` — 脉案页 UX spec（布局 / 交互 / 焦点导航）
- `design/gdd/casebook.md` — 脉案系统 GDD（分册态 / 折叠规则）
- `design/gdd/skeuomorphic-ui.md` — 元件库规则（纸面四件套）
- `design/art/art-bible.md` §7.2 — 纸面元件库规格
