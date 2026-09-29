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

> **版式归属**：本 spec 只描述**材质与状态**。版式（通道行数 / 行序 / 行高 / 分区）
> 的唯一权威是 `design/ux/casebook-39.md` §5 —— 两者形状一致但**权责分离**，
> 版式改动不触发本 spec 的贴图重出。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 (page spread) | 两页并排，左四诊 + 右辨证 |
| **Format** | PNG (sRGB) | 纸面底 + 界行 + 绳结 + 墨迹分层 |
| **Material slots** | 4 (paper_base 9-slice + ruling repeat + stitch + ink_overlay) | 界行与绳结各自独立，版式改动不需重出纸底（见「为什么要拆成四层」） |
| **Poly count** | N/A (UI element) | UI Toolkit UXML + USS |
| **LOD** | 不适用 | UI 始终全分辨率 |
| **Platform variants** | 无 | 全平台同一套纸面 |

---

## Asset Breakdown

| Asset | Type | Description | Slicing | Priority |
|-------|------|-------------|---------|----------|
| `casebook_paper_base.png` | Texture | **纯纸面**：宣纸纤维 / 泛黄 / 霉斑 / 水渍 / 卷边磨损。**不含界行、不含绳结、不含印章** | 9-slice | P0 |
| `casebook_paper_ruling.png` | Texture | 红色界行（竖线）**单行可 repeat 条**，不含任何行标签文字 | 横向 repeat | P0 |
| `casebook_paper_stitch.png` | Texture | 中缝线装绳结（一条窄图，含线环 + 结） | 9-slice | P0 |
| `casebook_ink_font.png` | Texture Atlas | 手写体字库（病名楷书 + 四诊楷书 + 辨证行书），含墨迹浓淡变体 | — | P0 |
| `casebook_stamp.png` | Texture | 印章样式（已识模式标记 / 鉴别诊断标记） | — | P0 |
| `casebook_wear.png` | Texture | 边角磨损 / 折痕细节层 | 9-slice | P1a |
| `casebook_cover.png` | Texture | 线装书封面（深褐漆面，书名烫金） | — | P1a |

### 为什么要拆成四层

> **2026-09-29 裁定**：材质与版式**解耦**。

原先 `casebook_paper_base.png` 把「宣纸底 + 界行 + 线装绳结」焊在一张图里，导致底图的竖线必须与
UXML 的五行通道行**逐像素对齐**——改一次行高或行序，整张图作废。这是把**代码资产**（版式）
与**美术资产**（材质）绑在一起的典型失败面。

拆开后：

| 改什么 | 动哪里 | 贴图重出？ |
|--------|--------|-----------|
| 通道行数 / 行序 / 行高 | UXML + USS 变量 | **否**（界行 repeat 次数自动跟随） |
| 页面比例 | USS 尺寸 | **否**（底图 9-slice 拉伸） |
| 印章位置 | USS anchor | **否** |
| 焦点高亮粗细 | USS 变量 | **否** |
| 纸的旧度 / 质感 | `casebook_paper_base.png` | **是**（仅此一种） |

**版式权威 = `casebook-39.md` §5**（五行通道区 + 右页判断区 + 网格要点「行高恒定」）。
**贴图只提供材质**，任何 AI 生成的参考图**不承载版式**，版式改动不得触发重出图。

**行高恒定的实现约束**：界行是**均分 repeat**的，不是「按内容自适应」。
未查的空行与已查的行视觉等重（`casebook-39.md` §5.4 网格要点）——空行若缩小/置底
= 变相进度指示，直接违反支柱「绝不①」。

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

> ⚠️ **产出定位 = 材质板，不是版式图**（2026-09-29 裁定）。
> 版式权威是 `casebook-39.md` §5，本节 prompt 只负责**纸的质感 / 旧度 / 光影 / 调性**。
> 生成图中的行数、行高、印章位置**一律不作为规格**；版式一改不需要重出图。

```
A traditional Chinese medical casebook (脉案), open spread view.
Style: Late Qing / early Republican era (清末民初), circa 1910s.
Material focus: Rice paper (宣纸) with warm yellowish tone, visible fiber texture,
slight aging discoloration, foxing spots, a water stain, darkened curled corners,
a gently cockled surface.
Binding: thread-bound spine (线装) at the centre gutter — loose folded paper sewn
with off-white cotton thread, visible thread loops and knots. No hardcover.
Lighting: Warm ambient, no harsh shadows, flat lay view.
Mood: Scholarly, meticulous, historical authenticity.
Constraints: NO HP bars, NO numbers, NO modern UI elements. Pure paper and ink aesthetic.
Resolution: 2048x2048, high detail for close-up reading.
```

**该 prompt 刻意不写的内容**（避免模型回填版式，见 image-gen skill 的「空行即答案」条目）：
行标签 / 通道名 / 病名文字 / 手写正文。模型对中国脉案的强先验是「必落书法」，
v1–v5 五轮实测无法通过提示词消除——**正确做法是让版式根本不进图**，而不是继续和先验搏斗。

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
