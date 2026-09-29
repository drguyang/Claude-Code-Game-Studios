# Asset Spec: 诊脉台铜器刻度盘

> **Tier**: Vertical Slice Critical
> **Category**: Prop / Diagnostic Tool
> **Source**: hud.md §2, diagnosis-system.md V-8.0
> **Art Bible Ref**: §7.2 黄铜侧主导（诊脉台刻度盘 = 铜器）; §1 P3「黄铜必须有来历」; §3.3 圆/弧 = 脉象连续感
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-016 §三（感知输入 = 粗粒度整数格，诊脉读数 = 连续态）

---

## Visual Description

**诊脉台**是诊断态五通道之一（触诊通道）的核心视觉承载。形态为**黄铜三指寸/关/尺刻度盘**——三枚弧形铜槽，对应中医诊脉的寸、关、尺三部位。

- **盘体**: 圆形黄铜底盘，表面有錾刻纹理（不规则手工锤纹），微微氧化（铜绿斑点在边缘，非全新抛光）。
- **三指槽**: 三个弧形凹槽（寸/关/尺），弧度 = 医师三指自然张开的弧度。凹槽内壁深色（吸光衬底），衬托指尖。
- **指针**: 三枚细铜针，每枚对应一条脉象读数。指针偏转角度 = 浮/中/沉三候（浮 = 靠近盘边，沉 = 靠近盘心）。
- **刻度线**: 盘面刻有 9 条等距弧线（寸 3 + 关 3 + 尺 3），铜丝镶嵌，夜间微反光。
- **材质归属**: 纯黄铜侧——盘体、指针、刻度线均为铜质感（与纸面四件套的墨侧形成材质对话）。
- **状态反馈**: 指针偏转 = 实时体征反馈；异常脉象时，指针抖动（0.1s 微颤，非大摆动）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture) | 铜面 + 刻度 + 指针 |
| **Format** | PNG (sRGB) | 黄铜金属感 |
| **Material slots** | 2 (brass_base + pointer_overlay) | 盘面底 + 指针层 |
| **Poly count** | ~500 tris (3D prop) | 底盘 + 三槽 + 指针 |
| **LOD** | LOD0 only | 诊断态常驻，始终全精度 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `pulse_dial_brass.png` | Texture | 黄铜底盘 + 錾刻纹理 + 氧化细节，1024×1024 | P0 |
| `pulse_dial_needle.png` | Texture | 三枚铜针（寸/关/尺），独立层用于动画 | P0 |
| `pulse_dial_marks.png` | Texture | 9 条刻度线（铜丝镶嵌），含三候位置标记 | P0 |
| `pulse_dial_glow.png` | Texture | 异常脉象时的微光层（铜面泛红） | P1a |
| `pulse_dial_3d.fbx` | Mesh | 诊脉台 3D 模型（底盘 + 三指槽 + 指针） | P0 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 指针静止于默认位置（中候，即关部居中） |
| **Active — Normal** | 指针随体征数据实时偏转（浮/中/沉） |
| **Active — Abnormal** | 指针微颤 0.1s + 铜面泛红（V-8.0 脉象异常反馈） |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |
| **Transition** | 指针偏转 0.05s ease-out（与体征数据更新同步） |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 黄铜底 | `#B8863B` (antique brass) | 铜侧主导 |
| 錾刻纹理 | `#8B6914` (darker brass) | 手工锤纹深色 |
| 铜绿氧化 | `#4A7C59` (verdigris, 边缘斑块) | 使用痕迹 |
| 指针 | `#D4A84B` (bright brass) | 活动层，反光 |
| 刻度线 | `#D4AF37` (gold wire) | 铜丝镶嵌 |
| 异常泛红 | `#8B3A3A` (seal red, P1a) | 脉象异常 |

---

## Accessibility

- 刻度线 ≥ 2px 宽（薄屏可辨识）
- 指针 ≠ 仅颜色区分——有位置（浮/中/沉）+ 微颤幅度差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
An antique Chinese pulse diagnosis dial (诊脉台), close-up view.
Material: Solid brass with visible hand-hammered texture, slight verdigris oxidation at edges.
Design: Three curved slots for fingers (寸/关/尺), each with a thin brass needle pointer.
Marks: 9 equidistant arc marks with thin gold wire inlay.
Style: Late Qing / early Republican era medical instrument, circa 1910s.
Details: Warm brass patina, subtle reflections, aged but well-maintained.
Lighting: Warm ambient, soft specular highlights on brass surface.
Mood: Scholarly, precise, historical medical instrument.
Constraints: NO digital elements, NO modern UI. Pure physical brass instrument.
Resolution: 1024x1024 texture, high detail for close-up reading.
```

---

## Open Questions

- [x] ~~指针动画是否支持触觉反馈（手柄震动）？？~~ → **2026-09-29 裁定：P0 = 三指逐个落**。
- [x] ~~三指同时落 vs 逐个落是否影响指针动画节奏？？~~ → **2026-09-29 裁定：指针动画不叠音频**。
- [x] ~~脉象异常时是否叠加音频层（听诊器层 + 铜面共振音）？？~~ → **2026-09-29 裁定：不触觉**。
---