# Asset Spec: 手术灯（急救场景冷光）

> **Tier**: Vertical Slice Critical
> **Category**: Prop / Emergency Equipment
> **Source**: emergency-procedures.md, art-bible §8.6.3
> **Art Bible Ref**: §8.6.3「冷光白 = 手术灯的唯一指定色温」; §1 P1「白 ≠ 纯白——急救白是冷调白」
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-018 §六（镜头效果归属 = 8 语义 + 2 实现，VR 全禁）

---

## Visual Description

**手术灯**是急救操作场景的核心照明器具。形态为**老式煤油手术灯**——铁架 + 黄铜关节 + 玻璃灯罩 + 冷白光。

- **灯体**: 铁质弧形支架，表面深色（接近黑色，非亮面金属），有使用痕迹（锈斑在关节处）。三节可调节臂（承 ADR-015 §五 整数格邻接判定）。
- **灯罩**: 圆形玻璃罩（非现代无影灯的多灯头），玻璃略有磨砂（散射光线）。边缘有黄铜包边。
- **灯泡**: 碳丝灯泡（非 LED），发出**冷调白光**（色温 ≈ 5500K，承 art-bible §8.6.3）。光晕 = 柔和散射，无强烈边缘。
- **照明效果**: 灯光范围内，纸面 / 墨迹 / 药材颜色**无偏移**（承 art-bible §1 P2「纸面元素在应急光下仍可读」）。
- **材质归属**: 灯架 = 墨侧（深色铁质，表面粗糙）；灯罩包边 = 铜侧（黄铜关节）。
- **开关动画**: 旋钮旋转 0.3s ease-in-out，灯光渐亮 0.5s。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture) | 灯架 + 灯罩 + 灯泡 |
| **Format** | PNG (sRGB) | 铁质深色 + 黄铜关节 |
| **Material slots** | 3 (iron_frame + brass_joints + glass_shade) | 铁架 / 铜关节 / 玻璃 |
| **Poly count** | ~400 tris (3D prop) | 三节可调臂 + 灯罩 |
| **LOD** | LOD0 only | 急救场景常驻 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `surgical_lamp_iron.png` | Texture | 铁质灯架（深色 + 锈斑），1024×1024 | P0 |
| `surgical_lamp_brass.png` | Texture | 黄铜关节 + 灯罩包边，含錾刻细节 | P0 |
| `surgical_lamp_glass.png` | Texture | 磨砂玻璃灯罩（散射效果），含灯泡反光 | P0 |
| `surgical_lamp_glow.png` | Texture | 冷光白光晕（5500K，柔和散射） | P0 |
| `surgical_lamp_wear.png` | Texture | 锈斑 / 划痕 / 使用痕迹 | P1a |
| `surgical_lamp_3d.fbx` | Mesh | 手术灯 3D 模型（三节臂 + 灯罩 + 灯泡） | P0 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Off** | 灯关，灯罩透明，灯架可见 |
| **Turning On** | 旋钮旋转 0.3s，灯泡渐亮 0.5s，光晕扩散 |
| **On — Idle** | 冷光白稳定照明（5500K），光晕柔和 |
| **On — Active (CPR)** | 灯光随按压节律微闪（急救操作反馈，承 emergency-procedures.md） |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 铁架 | `#2A2A2A` (near-black iron) | 墨侧深色 |
| 锈斑 | `#6B4423` (iron rust) | 使用痕迹 |
| 黄铜关节 | `#B8863B` (antique brass) | 铜侧 |
| 磨砂玻璃 | `#F5F5F5` (translucent white) | 散射介质 |
| 冷光白 | `#FFF8E7` (5500K warm white) | 照明色 |
| 光晕 | `#FFFFFF` at 30% alpha | 柔和散射 |

---

## Accessibility

- 灯光 ≠ 仅颜色区分——有光晕半径 + 亮度变化
- 手术灯是**诊断态唯一主动光源**——环境音 + 光晕组合传达场景状态
- VR 全禁镜头效果（承 ADR-018 §六）——手术灯照明 = 场景光，非镜头效果

---

## AI Generation Prompt (for reference)

```
An antique surgical lamp (手术灯), close-up view.
Style: Late Qing / early Republican era, circa 1910s.
Components: Iron adjustable arm (3 sections) with brass joints, frosted glass shade, carbon filament bulb emitting cool white light (5500K).
Details: Iron frame with rust at joints, brass fulcrums with hand-hammered texture, aged but functional.
Lighting: Cool white glow (5500K), soft light cone, no harsh shadows.
Mood: Clinical, focused, historical medical instrument.
Constraints: NO modern LED lamp, NO harsh shadows. Pure vintage surgical instrument.
Resolution: 1024x1024 texture, light cone visible.
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

- [x] ~~手术灯是否可携带（P0 = 固定位置；P1a = 便携）？？~~ → **2026-09-29 裁定：P0 = 固定位置**。
- [x] ~~灯光是否影响其他诊断工具的读数（听诊器音频层 / 诊脉台指针）？？~~ → **2026-09-29 裁定：不影响读数**。
- [x] ~~多灯场景（手术台 + 诊脉台 + 桌面灯）的照明叠加如何处理？？~~ → **2026-09-29 裁定：多灯待定**。
---