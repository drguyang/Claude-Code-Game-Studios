# Asset Spec: 主角（医者）

> **Tier**: Vertical Slice Critical
> **Category**: Character / Player Avatar
> **Source**: game-concept.md, systems-index #1, hud.md §Dynamic
> **Art Bible Ref**: §5「清末民初医师装束，布衣 + 布鞋 + 药囊」; §1 P1「水墨 + 黄铜双轨」
> **ADR Ref**: ADR-020（玩家控制器 CharacterController + 自建机位）; ADR-016 §三（玩家位移 = 连续态，跨格写世界流事件）

---

## Visual Description

**主角**是玩家的化身，清末民初医师形象。装束朴素但有专业辨识度。

- **面部**: 30–40 岁男性，面容清瘦（承继「医者」身份），胡须短须（非长须——便于表情辨识）。肤色偏黄（长期室内工作）。
- **发型**: 传统发髻（非辫子——清末民初过渡期特征），黑纱帽（无装饰）。
- **上衣**: 深色粗布长衫（藏蓝或墨黑），右襟盘扣，袖口宽松（便于号脉手势）。肩头有药囊带（斜挎）。
- **下装**: 同色系布裤，裤脚扎紧（防泥污）。
- **鞋**: 布鞋（千层底），深色，鞋尖微磨损。
- **药囊**: 斜挎小布囊（墨侧），黄铜扣（铜侧），内有针囊 / 药材格（P1a 可见内部）。
- **手部**: 双手干净（医者特征），手指修长（便于号脉 / 针灸手势）。
- **体型**: 中等身材，不魁梧（承「医术为主，格斗为辅」的 19 项技能定位）。
- **材质归属**: 整体 = 墨侧主导（粗布 / 棉布）；药囊铜扣 = 铜侧点缀。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 (texture) | 全身 + 面部细节 |
| **Format** | PNG (sRGB) | 布料纹理 + 皮肤 PBR |
| **Material slots** | 4 (skin / cloth_outer / cloth_inner / brass_accessories) | 皮肤 / 外衣 / 内衣 / 铜扣 |
| **Poly count** | ~2000 tris (LOD0) / ~800 tris (LOD1) | 人体 + 药囊 |
| **LOD** | LOD0 (近景) / LOD1 (中景) | 开放世界摄像机距离 |
| **Platform variants** | 无 | 全平台同一套 |
| **Animations** | P0: idle / walk / run / crouch / interact / diagnose / emergency (CPR) | 7 套基础动画 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `protagonist_body.png` | Texture | 全身布料纹理（粗布褶皱 + 盘扣细节），2048×2048 | P0 |
| `protagonist_face.png` | Texture | 面部贴图（面容 + 胡须 + 表情 UV），1024×1024 | P0 |
| `protagonist_hands.png` | Texture | 手部贴图（号脉手势 / 针灸手势 UV） | P0 |
| `protagonist_medicine_pouch.png` | Texture | 药囊纹理（布面 + 黄铜扣） | P0 |
| `protagonist_rig.fbx` | Mesh + Rig | 人体骨骼（Humanoid，支持 7 套动画） | P0 |
| `protagonist_idle.anim` | Animation | 待机呼吸动画（0.5s 周期，承 ADR-016 §三 连续态） | P0 |
| `protagonist_walk.anim` | Animation | 行走动画（布鞋步态，慢节奏） | P0 |
| `protagonist_run.anim` | Animation | 奔跑动画（承 TICK_SECONDS = 0.05s 连续移动） | P0 |
| `protagonist_crouch.anim` | Animation | 蹲伏动画（检查病人 / 听诊姿态） | P0 |
| `protagonist_interact.anim` | Animation | 交互手势（拾取 / 放置 / 开箱） | P0 |
| `protagonist_diagnose.anim` | Animation | 诊断手势（号脉 / 舌诊 / 闻诊姿态） | P0 |
| `protagonist_emergency_cpr.anim` | Animation | CPR 急救动画（按压节律 550ms，承 O-10-3 量化下限） | P0 |

---

## Animation States

| State | Animation | Duration | Notes |
|-------|-----------|----------|-------|
| **Idle** | Breathing idle | Loop | 0.5s 呼吸周期 |
| **Walk** | Slow walk | Loop | 布鞋步态 |
| **Run** | Run | Loop | TICK_SECONDS 连续移动 |
| **Crouch** | Crouch | Hold | 检查病人 / 听诊 |
| **Interact** | Hand gesture | 0.3s | 拾取 / 放置 |
| **Diagnose** | Diagnostic gesture | 0.5s | 号脉 / 舌诊 / 闻诊 |
| **Emergency (CPR)** | CPR compression | 550ms cycle | 承 O-10-3 |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 粗布长衫 | `#2A2A2A` (near-black cloth) | 墨侧主导 |
| 盘扣 | `#1A1714` (ink black) | 墨侧 |
| 皮肤 | `#D4B896` (warm asian skin) | PBR non-metal + high roughness (0.65–0.85) |
| 药囊布 | `#5C4033` (walnut brown) | 墨侧 |
| 黄铜扣 | `#B8863B` (antique brass) | 铜侧点缀 |
| 布鞋 | `#3D3D3D` (dark cloth) | 墨侧 |

---

## Accessibility

- 主角无血条 / 无小地图（承支柱四「拟物 UI 无血条 / 无小地图」）
- 药囊 = 唯一「快捷栏」视觉暗示（非数字角标）
- 焦点高亮 = 黄铜边框 2px（承 art-bible §3.3 / §7.4 + ADR-013）

---

## AI Generation Prompt (for reference)

```
A Chinese physician protagonist, full body view.
Style: Late Qing / early Republican era, circa 1910s.
Appearance: Male, 30-40 years old, lean scholarly face, short beard, traditional topknot with black gauze cap.
Clothing: Dark coarse cotton robe (藏蓝 or ink black), right-side closure with fabric knots, loose sleeves, dark cloth pants, cloth shoes with worn tips.
Accessories: Small medicine pouch (药囊) slung across shoulder, with brass clasp.
Details: Clean hands (physician trait), long fingers suitable for pulse diagnosis.
Material: Rough cotton texture, natural fiber, aged but well-maintained.
Lighting: Warm ambient, soft shadows, three-quarter view.
Mood: Scholarly, calm, professional healer.
Constraints: NO modern clothing, NO fantasy elements. Pure historical Chinese physician.
Resolution: 2048x2048 texture, full body + face details.
```

---

## Open Questions

- [x] ~~主角性别是否固定~~ → **2026-09-29 裁定：P0 固定男性**（P1a 可选女性为扩展字段，不阻塞主体 spec）。立绘 / 模型 / pronoun 全按单一性别；药囊装束、服饰、可选命名集各一份。
- [x] ~~药囊是否可见内部~~ → **2026-09-29 裁定：P0 = 外部纹理**（P1a = 开囊动画 + 内部格子）。
- [x] ~~主角是否支持换装~~ → **2026-09-29 裁定：P0 = 固定装束**（P1a = 季节 / 剧情换装）。

---

## Related Files

- `design/gdd/game-concept.md` — 主角设定
- `design/art/art-bible.md` §5 — 主角视觉方向
- `design/gdd/player-character.md` — 主角技能 / 成长
- `design/gdd/emergency-procedures.md` — 急救动画（CPR）
