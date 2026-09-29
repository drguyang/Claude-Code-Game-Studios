# Asset Spec: 听诊器

> **Tier**: Vertical Slice Critical
> **Category**: Prop / Diagnostic Tool
> **Source**: hud.md §2, audio-system.md F-44.7, emergency-procedures.md
> **Art Bible Ref**: §7.2 黄铜侧主导（听诊器 = 铜器）; §1 P3「黄铜必须有来历」; §3.3 圆/弧 = 脉象连续感
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-016 §三（感知输入 = 粗粒度整数格，听诊读数 = 连续态）; ADR-028（世界语境声源归属 = 44 声源池）

---

## Visual Description

**听诊器**是诊断态五通道之一（闻诊通道 + 触诊通道）的核心工具。形态为**老式黄铜听诊器**——胸件 + 导管 + 耳件三部分。

- **胸件（钟形）**: 黄铜钟形结构，表面有錾刻纹理。底部边缘微磨（使用痕迹）。材质系数影响音频层（F-44.7：不同材质系数 = 不同通带/噪声底）。
- **导管**: 黑色橡胶管（非现代 PVC），双管分叉到耳件。导管有自然弯曲（非直线），模拟真实佩戴状态。
- **耳件**: 黄铜弹簧臂 + 耳塞（软橡胶）。弹簧臂有轻微氧化。
- **贴合动画**: 胸件贴合身体时，导管自然下垂摆动 0.15s ease-out（承 ADR-013 动画哲学）。
- **材质归属**: 黄铜侧主导——胸件和耳件为铜质感；导管 = 墨侧（橡胶深色）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture atlas) | 胸件 + 导管 + 耳件 |
| **Format** | PNG (sRGB) | 黄铜金属感 + 橡胶深色 |
| **Material slots** | 3 (brass_chestpiece + rubber_tube + brass_earpieces) | 三件独立材质 |
| **Poly count** | ~300 tris (3D prop) | 钟形 + 导管 + 耳件 |
| **LOD** | LOD0 only | 诊断态常驻 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `stethoscope_brass.png` | Texture | 黄铜胸件 + 耳件，含錾刻纹理 + 氧化，1024×1024 | P0 |
| `stethoscope_rubber.png` | Texture | 黑色橡胶导管 + 耳塞，含自然弯曲 | P0 |
| `stethoscope_material_coeff.png` | Data Asset | 材质系数表（薄衣/厚衣/皮肤/湿衣 → 通带/噪声底参数） | P0 |
| `stethoscope_wear.png` | Texture | 使用痕迹 / 划痕 / 氧化细节 | P1a |
| `stethoscope_3d.fbx` | Mesh | 听诊器 3D 模型（钟形 + 导管 + 耳件） | P0 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 听诊器悬挂于玩家身侧（导管自然下垂） |
| **Equipped** | 耳件插入玩家耳朵（第一人称视角可见导管 + 胸件） |
| **Active — Listening** | 胸件贴合身体，导管微颤 0.15s（承 F-44.7 材质系数） |
| **Active — Abnormal** | 胸件边缘泛红（异常体征） + 音频层噪声提升 |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |
| **Transition** | 胸件贴合 0.15s ease-out，收起 0.1s ease-in |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 黄铜胸件 | `#B8863B` (antique brass) | 铜侧主导 |
| 錾刻纹理 | `#8B6914` (darker brass) | 手工锤纹 |
| 橡胶导管 | `#2A2A2A` (near-black rubber) | 墨侧深色 |
| 耳塞 | `#3D3D3D` (soft rubber) | 墨侧 |
| 氧化斑块 | `#4A7C59` (verdigris, 边缘) | 使用痕迹 |
| 异常泛红 | `#8B3A3A` (seal red, P1a) | 体征异常 |

---

## Audio Layer Integration (F-44.7)

听诊器是**世界语境声源**的载体（ADR-028）。材质系数决定呼吸两层的通带/噪声底：

| 材质 | 通带特征 | 噪声底特征 |
|------|----------|------------|
| 薄衣（单层） | 高保真，细节丰富 | 低噪声 |
| 厚衣（棉袄） | 中频衰减 | 中等噪声 |
| 皮肤直接接触 | 最清晰，全频段 | 最低噪声 |
| 湿衣（雨/汗） | 低频增强 | 高噪声（水滴干扰） |

材质系数 = `stethoscope_material_coeff.png` 承载的 Data Asset（JSON 烘焙，承 ADR-014）。

---

## Accessibility

- 胸件贴合状态 ≠ 仅颜色区分——有导管微颤幅度 + 音频层特征差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）
- 听诊器是**唯一**诊断态依赖音频的工具 —— 视觉障碍玩家可依赖音频反馈（AC-44-05 语声变体同理）

---

## AI Generation Prompt (for reference)

```
An antique Chinese brass stethoscope, close-up view.
Style: Late Qing / early Republican era medical instrument, circa 1910s.
Components: Brass bell-shaped chestpiece with hand-hammered texture, black rubber tubing, brass spring earpieces with soft rubber tips.
Details: Slight oxidation (verdigris) at edges, subtle wear marks, aged brass patina.
Lighting: Warm ambient, soft specular highlights on brass surface.
Mood: Scholarly, precise, historical medical instrument.
Constraints: NO modern medical equipment, NO digital elements. Pure vintage brass + rubber.
Resolution: 1024x1024 texture atlas, individual components clearly separated.
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

- [x] ~~导管物理摆动是否引入布料模拟？（P0 = 预制动画；P1a = 简易布料模拟）？~~ → **2026-09-29 裁定：P0 = 预制动画,无布料模拟**。
- [x] ~~听诊器是否支持多种材质系数（皮肤/薄衣/厚衣/湿衣）—— P0 是否只做皮肤 + 薄衣两种？？~~ → **2026-09-29 裁定：皮肤+薄衣两种**。
- [x] ~~耳塞是否可见（第一人称视角）？还是仅导管 + 胸件可见？？~~ → **2026-09-29 裁定：导管+胸件可见,耳塞不可见**。
---