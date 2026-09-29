# Asset Spec: 戥子（中药称量器具）

> **Tier**: Vertical Slice Critical
> **Category**: Prop / Diagnostic Tool
> **Source**: hud.md §2, prescription-and-medication.md, 11
> **Art Bible Ref**: §7.2 黄铜侧主导（戥子 = 铜器）; §1 P3「黄 brass 必须有来历」; §3.3 圆/弧 = 脉象连续感
> **ADR Ref**: ADR-013 §三（UI Toolkit 主栈）; ADR-003（模块化网格，戥子读数格 = 建造槽位同格）; ADR-016 §三（感知输入 = 粗粒度整数格，戥子读数 = 连续态）

---

## Visual Description

**戥子**是诊断态 / 处方态的核心称量器具。形态为**老式中药戥子**——黄铜秤杆 + 铜盘 + 铜纽 + 细绳 + 秤砣。

- **秤杆**: 细长黄铜杆，表面有錾刻刻度（整数格 + 半格线）。杆身微弯（承重自然弧度）。两端有铜帽（防磨损）。
- **秤盘**: 圆形黄铜浅盘，直径约 3cm（戥子标准），边缘微卷。盘底有红绒垫（防药材滑落）。
- **铜纽**: 秤杆中段提纽（黄铜环），用于提举。
- **秤砣**: 小黄铜砣，穿在绳上，沿秤杆滑动。绳 = 细麻绳（非尼龙），自然纤维纹理。
- **称量动画**: 秤砣滑动 0.2s ease-out，秤杆微微倾斜（承重反馈）。
- **材质归属**: 纯黄铜侧——秤杆、秤盘、铜纽、秤砣均为铜质感（与纸面四件套的墨侧形成材质对话）。
- **读数方式**: 秤砣位置 = 整数格读数（非数字角标）。精度 = 0.5g（半格线标记）。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 1024×1024 (texture) | 秤杆 + 秤盘 + 秤砣 |
| **Format** | PNG (sRGB) | 黄铜金属感 |
| **Material slots** | 3 (brass_rod + brass_pan + rope_weight) | 秤杆 / 秤盘 / 绳秤砣 |
| **Poly count** | ~200 tris (3D prop) | 秤杆 + 秤盘 + 秤砣 |
| **LOD** | LOD0 only | 诊断态常驻 |
| **Platform variants** | 无 | 全平台同一套 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `scale_brass_rod.png` | Texture | 黄铜秤杆 + 錾刻刻度（整数格 + 半格线），1024×1024 | P0 |
| `scale_brass_pan.png` | Texture | 黄铜秤盘 + 红绒垫，直径 3cm 比例 | P0 |
| `scale_rope_weight.png` | Texture | 麻绳 + 秤砣（黄铜），含滑动动画帧 | P0 |
| `scale_material_coeff.png` | Data Asset | 药材密度表（不同药材 → 重量/体积换算系数） | P0 |
| `scale_wear.png` | Texture | 使用痕迹 / 氧化 / 秤杆磨损 | P1a |
| `scale_3d.fbx` | Mesh | 戥子 3D 模型（秤杆 + 秤盘 + 秤砣 + 铜纽） | P0 |

---

## Interaction States

| State | Visual |
|-------|--------|
| **Idle** | 秤砣位于零位（秤杆水平），秤盘空置 |
| **Active — Weighing** | 药材放入秤盘，秤砣滑动 0.2s ease-out，秤杆微倾 |
| **Active — Balanced** | 秤杆水平（读数 = 精准），秤砣静止 |
| **Active — Overloaded** | 秤杆过度倾斜（超过量程），秤砣滑至极限 |
| **Focus** | 黄铜边框 2px 高亮（承 ADR-013 §六 焦点呈现） |
| **Transition** | 秤砣滑动 0.2s ease-out，秤杆倾斜 0.1s ease-in-out |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 黄铜秤杆 | `#B8863B` (antique brass) | 铜侧主导 |
| 錾刻刻度 | `#8B6914` (darker brass) | 手工锤纹深色 |
| 黄铜秤盘 | `#B8863B` (antique brass) | 铜侧 |
| 红绒垫 | `#8B2323` (deep red) | 墨侧暖色 |
| 麻绳 | `#C4A882` (natural hemp) | 墨侧 |
| 秤砣 | `#D4A84B` (bright brass) | 铜侧，活动层 |
| 氧化斑块 | `#4A7C59` (verdigris, 边缘) | 使用痕迹 |

---

## Accessibility

- 秤杆刻度 ≥ 2px 宽（薄屏可辨识整数格 / 半格线）
- 读数 ≠ 仅颜色区分——有秤砣位置（格数）+ 秤杆倾斜角度差异
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）
- 过载状态 = 秤杆过度倾斜 + 秤砣滑至极限（双通道）

---

## AI Generation Prompt (for reference)

```
An antique Chinese medicinal scale (戥子), close-up view.
Style: Late Qing / early Republican era, circa 1910s.
Components: Thin brass beam with hand-engraved刻度 marks, brass circular pan with red velvet pad, brass sliding weight (秤砣) on hemp string, brass fulcrum ring.
Details: Slight oxidation on brass, natural hemp fiber texture, aged patina.
Lighting: Warm ambient, soft specular highlights on brass surface.
Mood: Scholarly, precise, historical medical instrument.
Constraints: NO digital elements, NO modern scale. Pure vintage brass + hemp.
Resolution: 1024x1024 texture,刻度 marks clearly legible.
```

---

## Open Questions

- [ ] 秤杆刻度是否支持小数（P0 = 整数格 + 半格线；P1a = 更精细刻度）？
- [ ] 过载时是否叠加视觉反馈（秤杆抖动 + 秤砣滑落）？
- [ ] 戥子是否支持多种药材同时称量（P0 = 单药材；P1a = 多药材对比）？

---

## Related Files

- `design/ux/hud.md` §2 — 戥子 HUD spec
- `design/gdd/prescription-and-medication.md` — 处方 / 药材系统
- `design/art/art-bible.md` §7.2 — 黄铜侧材质规格
- `design/gdd/modular-building.md` — 建造槽位（视觉对齐参考：戥子读数格 = 建造槽位同格）
