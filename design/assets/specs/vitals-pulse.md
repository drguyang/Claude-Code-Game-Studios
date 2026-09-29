# Asset Spec: 体征异常脉冲

> **Tier**: Vertical Slice Critical
> **Category**: HUD Element / Diagnostic Alert
> **Source**: hud.md §Dynamic
> **Art Bible Ref**: §7.2 黄 brass 侧主导（体征异常脉冲 = 铜器边缘泛红）; §1 P3「黄 brass 必须有来历」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-018 §六（镜头效果归属 = 8 语义 + 2 实现，全禁闪烁 / 抖动）

---

## Visual Description

**体征异常脉冲**是诊断态的动态反馈元素。形态为**铜器边缘泛红**——当病人体征异常时，对应诊断工具的铜件边缘泛红，持续 0.5s。

- **触发条件**: 体征读数超出正常范围（承 diagnosis-system.md V-8.0 五通道异常判定）。
- **视觉表现**: 铜器边缘（诊脉台刻度盘 / 戥子秤杆 / 听诊器胸件）泛红，色温 = 印泥红（`#C13A3A`），非亮红。
- **动画**: 泛红 0.5s ease-in-out（非闪烁——承 ADR-018 §六 无提示音铁律视觉侧「全禁闪烁 / 抖动」）。
- **材质归属**: 铜侧（黄 brass 边缘泛红 = 铜器氧化反应视觉隐喻）。
- **非文字**: 零文本提示（承 art-bible §1 P2「非数字」）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | N/A (shader / material effect) | 泛红 = 材质属性变化，非独立纹理 |
| **Format** | Shader Graph / HLSL | 铜面泛红 shader |
| **Material slots** | 1 (brass_base + glow_factor) | 黄 brass 底 + 泛红系数 |
| **Poly count** | N/A | 依附于诊断工具 3D 模型 |
| **LOD** | 不适用 | 材质效果，全 LOD 适用 |
| **Platform variants** | 无 | 全平台同一套 shader |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 铜器正常氧化色（无泛红） |
| **Active — Abnormal** | 铜器边缘泛红 0.5s ease-in-out（非闪烁） |
| **Active — Normal** | 泛红消退 0.3s ease-out |
| **Focus** | 黄 brass 边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 黄 brass 底 | `#B8863B` (antique brass) | 铜侧正常态 |
| 泛红 | `#C13A3A` (vermilion red) | 异常态 |
| 泛红强度 | 30% alpha（非亮红） | 承 art-bible §1 P2 |

---

## Accessibility

- 泛红 ≠ 仅颜色区分——有位置（边缘）+ 动画节奏（0.5s ease-in-out）差异
- 零闪烁 / 零抖动（承 ADR-018 §六 无提示音铁律视觉侧）

---

## AI Generation Prompt (for reference)

```
Close-up of antique brass medical instrument (pulse dial / stethoscope / scale) with vermilion red glow at edges.
Style: Late Qing / early Republican era, circa 1910s.
Material: Antique brass with hand-hammered texture, slight oxidation.
Effect: Subtle vermilion red glow (30% alpha) at brass edges, indicating abnormal vital signs.
Details: Soft glow, no harsh edges, 0.5s ease-in-out animation.
Lighting: Warm ambient, soft specular highlights.
Mood: Scholarly, precise, medical alert.
Constraints: NO flashing, NO harsh colors. Subtle, elegant alert state.
Resolution: N/A (shader effect, not texture).
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

- [x] ~~泛红是否叠加音频层（听诊器噪声底提升）？？~~ → **2026-09-29 裁定：P0 = 不叠音频**。
- [x] ~~多工具同时异常时，泛红是否叠加（诊脉台 + 戥子同时异常）？？~~ → **2026-09-29 裁定：多工具同时异常不叠加(各独立)**。
- [x] ~~泛红是否支持强度分级（轻度异常 / 重度异常 → 不同泛红强度）？？~~ → **2026-09-29 裁定：轻度/重度两档**。
---