# Asset Spec: 铜怀表

> **Tier**: Full Production  ⚠️ 2026-09-29 修正:原误标 VS Critical —— 该资产在 entity-inventory.md 位于 Full Production 节(Props #1/#2/#8)
> **Category**: Prop / Timepiece
> **Source**: hud.md §4, time-and-weather.md
> **Art Bible Ref**: §7.2 黄铜侧主导（怀表 = 铜器）; §1 P3「黄 brass 必须有来历」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-009 §五（游戏时间 = 派生态，由事件流确定性重建）

---

## Visual Description

**铜怀表**是世界态常驻 HUD 元素（左上角）。形态为**老式黄铜怀表**——翻盖式，铜壳 + 玻璃面 + 指针。

- **外壳**: 黄铜翻盖，表面有錾刻纹理（花卉纹样，清末民初常见图案）。边缘微磨（使用痕迹）。
- **表盘**: 圆形铜面，刻度为**中文时辰刻度**（子丑寅卯辰巳午未申酉戌亥，非阿拉伯数字）。指针 = 短针（时辰）+ 长针（刻）。
  **时辰文字由 UXML `<Label>` 叠层承载**（动态切换），材质层只出时辰刻度底纹（静态）。
- **玻璃面**: 圆形玻璃（非现代蓝宝石），略有划痕（使用痕迹）。
- **链条**: 黄铜细链，从怀表顶部延伸（固定于 UI 左上角）。
- **动态元素**:
  - 指针实时转动（游戏时间流逝）
  - 时辰切换时，表盘墨迹更新（时辰文字变化）
- **材质归属**: 纯黄铜侧——外壳、表盘、指针、链条均为铜质感。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 256×256 (texture) | 怀表面 + 时辰文字 |
| **Format** | PNG (sRGB) | 黄铜金属感 |
| **Material slots** | 3 (brass_case + glass_face + ink_overlay) | 铜壳 / 玻璃面 / 时辰墨迹层 |
| **Poly count** | ~100 tris (3D prop) | 翻盖 + 表盘 + 指针 |
| **LOD** | LOD0 only | 世界态常驻 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `watch_brass_case.png` | Texture | 黄铜外壳 + 錾刻纹理 + 链条，256×256 | P0 |
| `watch_glass_face.png` | Texture | 玻璃面 + 时辰刻度底纹（静态），不含文字内容 | P0 |
| `watch_hour_labels.png` | Texture | 时辰墨迹材质层（子丑寅卯…），AI 生成含完整版式的材质参考图 → DA 裁切为独立纹理层；动态时辰文字由 UXML `<Label>` 叠层承载 | P0 |
| `watch_wear.png` | Texture | 使用痕迹 / 氧化 / 划痕 | P1a |
| `watch_3d.fbx` | Mesh | 怀表 3D 模型（翻盖 + 表盘 + 指针） | P0 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 指针实时转动（游戏时间流逝） |
| **Hour Change** | 时辰文字切换（0.5s 淡入淡出） |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |
| **Active — Zoom** | 怀表放大至屏幕中央（0.3s ease-out），显示详细时间 |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 黄铜壳 | `#B8863B` (antique brass) | 铜侧主导 |
| 錾刻纹理 | `#8B6914` (darker brass) | 手工锤纹 |
| 玻璃面 | `#F5F5F5` (translucent white) | 散射介质 |
| 时辰文字 | `#1A1714` (ink black) | 墨侧 |
| 指针 | `#D4A84B` (bright brass) | 铜侧 |
| 氧化斑块 | `#4A7C59` (verdigris, 边缘) | 使用痕迹 |

---

## Accessibility

- 时辰文字 ≥ 10px（薄屏可辨识）
- 指针 ≠ 仅颜色区分——有时针位置 + 分针位置差异
- 焦点高亮 = 黄 brass 边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
An antique brass pocket watch (铜怀表), close-up view.
Style: Late Qing / early Republican era, circa 1910s.
Components: Brass flip cover with floral engraving, glass face with static hour tick marks (刻度底纹, 非文字), brass hour and minute hands, brass chain.
Glass face details: Circular glass (非 modern sapphire), slight scratches from use, aged patina, static tick marks at 12 positions — these are visual guide positions only, not legible text.
Lighting: Warm ambient, soft specular highlights on brass surface.
Mood: Scholarly, precise, historical timepiece.
Constraints: NO modern digital watch, NO Arabic numerals, NO Chinese characters in texture. Pure vintage brass pocket watch. Chinese hour names (子丑寅卯…) are rendered via UXML overlay, not baked into texture.
Resolution: 256x256 texture per layer, clock face guide positions clearly marked.
```

### Generation Record(§8.10.2 · 独创性留痕)

| 字段 | 值 |
|---|---|
| `prompt` | 见上方代码块(prompt 本体) |
| `model` | `TBD` — 待补生成时的模型与版本(如 SenseNova U1.5 / flux) |
| `iterations` | `TBD` — 待补迭代轮次与每轮改动要点(i2i 时) |
| `seed` | `TBD` — 待补随机种子(可复现) |
| `human_edits` | `none` — 若有后期人工修改须逐项记录 |

> **§8.10.2 落地**:本表是发行时 Steam AI 申报清单的数据源。
> `TBD` 字段须在该资产**首次生成时回填**;参考图本身为本地产物不入库
> (见 `.gitignore` `/image-gen` 条目),规格真源 = 本 spec。

---

## Open Questions

- [x] ~~时辰系统精度（P0 = 时辰（2 小时）；P1a = 刻（15 分钟））？？~~ → **2026-09-29 裁定：P0 = 时辰(2h)**。
- [x] ~~怀表是否支持闹钟功能（P0 = 无；P1a = 事件提醒）？？~~ → **2026-09-29 裁定：无闹钟**。
- [x] ~~怀表是否支持暂停时间（P0 = 时间持续流逝；P1a = 暂停）？？~~ → **2026-09-29 裁定：时间持续流逝**。
---