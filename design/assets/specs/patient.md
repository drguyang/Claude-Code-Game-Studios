# Asset Spec: 普通病人（T0–T3）

> **Tier**: Vertical Slice Critical
> **Category**: Character / NPC
> **Source**: diagnosis-system.md, patient-ai.md, game-concept.md
> **Art Bible Ref**: §5「面色五色阶梯（art-bible §8.6.1）」; §8.6.1「面色五色阶梯 — 病情 severity 的可视化映射」
> **ADR Ref**: ADR-016 §三（13 病人 AI = 只读消费者，读 VitalsDto / IPresentPatients，不引用 sim 状态）; ADR-009 §五（病人位置 = 表现态，只读 AudioCueDto.Cell）

---

## Visual Description

**普通病人**是诊断态的核心 NPC，四档病情 severity（T0–T3）。面色五色阶梯 = 病情可视化映射。

- **T0（健康 / 无症状）**: 面色正常（微黄，非红润——清末民初平民营养不良底色），眼神清明，姿态自然。
- **T1（轻症）**: 面色微青（气滞）/ 微白（气虚），眼神略散，姿态微倦（倚靠 / 坐姿）。
- **T2（重症）**: 面色苍白（血虚）/ 青紫（瘀血），眼神涣散，姿态虚弱（躺卧 / 需搀扶）。
- **T3（危重）**: 面色灰暗（精绝）/ 通红（热毒），眼神无光，姿态昏迷（平躺，需 CPR）。
- **体型**: 平民体型（不魁梧，承清末民初营养不良背景），男女各半（P0 各 2 个变体）。
- **服饰**: 粗布短褐（平民装束），无装饰，袖口 / 裤脚有磨损。
- **材质归属**: 整体 = 墨侧（粗布 / 棉布）；皮肤 = PBR non-metal + high roughness (0.65–0.85) + SSS only on ears/nose（承 art-bible §1）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 (texture) | 全身 + 面部细节 |
| **Format** | PNG (sRGB) | 布料纹理 + 皮肤 PBR |
| **Material slots** | 3 (skin / cloth_outer / cloth_inner) | 皮肤 / 外衣 / 内衣 |
| **Poly count** | ~1500 tris (LOD0) / ~600 tris (LOD1) | 人体 |
| **LOD** | LOD0 (近景) / LOD1 (中景) | 开放世界摄像机距离 |
| **Platform variants** | 无 | 全平台同一套 |
| **Animations** | P0: idle / walk / run / sit / lie_down / groan / seizure | 7 套基础动画 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `patient_T0_body.png` | Texture | T0 健康面色，粗布短褐，2048×2048 | P0 |
| `patient_T1_body.png` | Texture | T1 轻症面色（微青 / 微白），粗布短褐 | P0 |
| `patient_T2_body.png` | Texture | T2 重症面色（苍白 / 青紫），粗布短褐 | P0 |
| `patient_T3_body.png` | Texture | T3 危重面色（灰暗 / 通红），粗布短褐 | P0 |
| `patient_face_shared.png` | Texture | 面部贴图共享（4 档 severity 共享同一面部，仅面色层不同） | P0 |
| `patient_skin_normals.png` | Texture | 皮肤法线贴图（SSS only on ears/nose） | P0 |
| `patient_rig.fbx` | Mesh + Rig | 人体骨骼（Humanoid，支持 7 套动画） | P0 |
| `patient_idle.anim` | Animation | 待机动画（T0 自然 / T1 微倦 / T2 虚弱 / T3 昏迷） | P0 |
| `patient_walk.anim` | Animation | 行走动画（T0 正常 / T1 缓慢 / T2 需搀扶 / T3 无法行走） | P0 |
| `patient_groan.anim` | Animation | 呻吟动画（T1–T3 触发） | P0 |
| `patient_seizure.anim` | Animation | 痉挛动画（TR-patient-021 触发） | P0 |

---

## Severity Color Mapping (Five Colors)

| Severity | 面色 | 中医辨证 | 视觉映射 |
|----------|------|----------|----------|
| **T0（健康）** | `#D4B896` (warm asian skin) | 正常 | 正常肤色 |
| **T1（轻症）** | `#B8C4C4` (slight cyan) / `#C4B8B8` (slight pale) | 气滞 / 气虚 | 微青 / 微白 |
| **T2（重症）** | `#A8B8C8` (pale blue-white) / `#8B7B8B` (purple-cyan) | 血虚 / 瘀血 | 苍白 / 青紫 |
| **T3（危重）** | `#6B6B6B` (gray-dark) / `#C44D4D` (bright red) | 精绝 / 热毒 | 灰暗 / 通红 |

> ⚠️ **AC-37-15 递归扫描**：`disease_id` 不进呈现层（落地 = `PresentationDtoGuard` 递归反射扫描）。面色 = `VitalsDto` 的 float 输出（唯一浮点出口），**不是** `disease_id` 直接映射。

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle — T0** | 自然站立，面色正常 |
| **Idle — T1** | 微倦姿态，面色微青/白 |
| **Idle — T2** | 虚弱躺卧，面色苍白/青紫 |
| **Idle — T3** | 昏迷平躺，面色灰暗/通红 |
| **Active — groan** | 呻吟动画（T1–T3 触发），面部表情微变 |
| **Active — seizure** | 痉挛动画（TR-patient-021 触发），全身微颤 |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Accessibility

- 面色 ≠ 仅颜色区分——有姿态（站/坐/躺/昏迷）+ 表情（清明/涣散/无光）差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）
- 呻吟 / 痉挛 = 音频 + 视觉双通道（承 audio-system.md AC-44-05 语声变体）

---

## AI Generation Prompt (for reference)

```
A Chinese commoner patient, full body view, four severity levels.
Style: Late Qing / early Republican era, circa 1910s.
Appearance: Male and female, 20-60 years old, lean to emaciated build (historical malnutrition context), plain facial features.
Clothing: Rough cotton short robe (短褐), worn at cuffs and hems, no ornaments.
Severity levels:
- T0 (healthy): Natural skin tone, alert eyes, standing posture.
- T1 (mild): Slight cyanosis/pallor, tired eyes, leaning posture.
- T2 (severe): Pale/purple-cyan complexion, unfocused eyes, weak reclining posture.
- T3 (critical): Gray/dark or flushed-red complexion, no eye focus, unconscious flat posture.
Details: Clean but worn clothing, natural fiber texture, aged but functional.
Lighting: Warm ambient, soft shadows, three-quarter view.
Mood: Human, vulnerable, historically accurate.
Constraints: NO fantasy elements, NO modern clothing. Pure historical Chinese civilian.
Resolution: 2048x2048 texture, four severity variants.
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

- [x] ~~病人性别比例~~ → **2026-09-29 裁定：P0 = 男女各半**（P1a = 更多变体，含性别比例调参旋钮）。
- [x] ~~病人年龄范围~~ → **2026-09-29 裁定：P0 = 20–60 岁**；儿童（<12）/ 老人（>65）各多一套体型和面色变体推到 P1a。
- [x] ~~面色是否支持中间态~~ → **2026-09-29 裁定：严格四档 T0–T3**（与 art-bible §8.6.1 面色五色阶梯一致）；T1.5/T2.5 中间态 P1a。诊断读数分辨四档，美术四套面色。

---

## Related Files

- `design/gdd/diagnosis-system.md` — 诊脉 / 舌诊 / 面色五色阶梯
- `design/gdd/patient-ai.md` — 病人 AI（姿态 / 呻吟 / 痉挛）
- `design/art/art-bible.md` §5 / §8.6.1 — 面色五色阶梯
- `design/gdd/emergency-procedures.md` — 急救动画（CPR / 止血包扎）
