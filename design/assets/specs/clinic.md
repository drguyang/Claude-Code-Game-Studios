# Asset Spec: 医馆（单房间，P0）

> **Tier**: Vertical Slice Critical
> **Category**: Environment / Building
> **Source**: clinic-machine.md, game-concept.md MVP, world-and-ecozones.md
> **Art Bible Ref**: §6「旧木梁 + 纸窗 + 土墙，药柜/诊桌/病床布局」; §8.6.4「深色木梁 + 土墙质感」
> **ADR Ref**: ADR-015（两层世界：确定性整数逻辑层 + 纯视觉层）; ADR-023（三场景制：Boot / MainMenu / World additive）; ADR-022（关卡工具产出烘焙逻辑数据）

---

## Visual Description

**医馆**是 P0 的唯一室内场景，玩家的「家」和诊断 / 治疗的核心场所。形态为**清末民初乡村医馆**——单房间，功能分区明确。

- **墙体**: 土墙（夯土质感），微黄偏灰，墙根有青苔痕迹（承 art-bible §8.6.4）。墙上有纸窗（半透光，承 art-bible §6）。
- **木梁**: 旧木梁横跨天花板，深色（接近黑色），表面有岁月痕迹（裂纹 + 磨损）。梁柱粗大（承重结构）。
- **地板**: 石板地面（青石板），缝隙间有腐殖土（承 art-bible §8.6.4）。局部磨损（诊桌 / 病床区域）。
- **家具布局**:
  - **诊桌**: 木桌（深色木），桌面有墨迹 / 药渍痕迹。桌上放脉案纸页 + 诊脉台。
  - **药柜**: 靠墙多层抽屉柜（墨侧）+ 黄铜拉手（铜侧），抽屉内有药材格。
  - **病床**: 简陋木床（非现代病床），有旧棉褥（深色，非白床单——承清末民初背景）。
  - **手术灯**: 可调节臂，悬挂于病床上方（承 surgical-lamp.md）。
- **照明**: 自然光（纸窗透入）+ 手术灯冷白光 + 油灯暖光（桌面灯）。三种光源混合，承 art-bible §2「3200K 暖漫射」。
- **材质归属**: 墙体 / 地板 / 家具 = 墨侧（土 / 木 / 石）；药柜拉手 / 手术灯关节 = 铜侧。

---

## Technical Specs

| Property | Value | Notes |
|----------|-------|-------|
| **Resolution** | 2048×2048 (texture atlas) | 墙体 / 地板 / 木梁 / 家具 |
| **Format** | PNG (sRGB) | 土墙 / 木梁 / 石板 |
| **Material slots** | 5 (wall_mud / floor_stone / wood_beam / furniture_wood / brass_hardware) | 土墙 / 石板 / 木梁 / 家具木 / 铜件 |
| **Poly count** | ~5000 tris (室内场景) | 墙体 + 家具 + 装饰 |
| **LOD** | LOD0 only (室内 = 固定场景) | 医馆不参与流式加载 |
| **Platform variants** | 无 | 全平台同一套 |
| **Collision** | Simple box colliders (furniture) + mesh collider (walls) | PhysX 静态碰撞 |

---

## Asset Breakdown

| Asset | Type | Description | Priority |
|-------|------|-------------|----------|
| `clinic_wall_mud.png` | Texture | 夯土墙纹理（微黄偏灰 + 青苔痕迹），2048×2048 | P0 |
| `clinic_floor_stone.png` | Texture | 青石板地面（缝隙 + 腐殖土），2048×2048 | P0 |
| `clinic_wood_beam.png` | Texture | 旧木梁（深色 + 裂纹 + 磨损），2048×1024 | P0 |
| `clinic_desk.png` | Texture | 诊桌木面（墨迹 + 药渍痕迹） | P0 |
| `clinic_cabinet.png` | Texture | 药柜木面 + 黄铜拉手 | P0 |
| `clinic_bed.png` | Texture | 病床木架 + 旧棉褥 | P0 |
| `clinic_window.png` | Texture | 纸窗（半透光 + 窗棂） | P0 |
| `clinic_lamp_oil.png` | Texture | 桌面油灯（暖光 3200K） | P0 |
| `clinic_props.png` | Texture Atlas | 小道具（针囊 / 铜筹 / 药罐 / 碗 / 杯） | P0 |
| `clinic_3d.fbx` | Mesh | 医馆 3D 场景（墙体 + 地板 + 家具 + 装饰） | P0 |

---

## Layout Specification

```
┌─────────────────────────────────────────────┐
│  纸窗（北墙）                               │
│  ┌──────┐                     ┌──────┐      │
│  │ 油灯 │                     │ 药柜 │      │
│  └──────┘                     └──────┘      │
│                                             │
│   ┌─────────────┐           ┌────────┐     │
│   │   诊桌      │           │ 病床   │     │
│   │ (脉案+诊脉台)│           │        │     │
│   └─────────────┘           └────────┘     │
│                                             │
│  ═══════════════════════════════════════    │
│  石板地面（青石板 + 腐殖土缝隙）             │
└─────────────────────────────────────────────┘
```

---

## Lighting Setup

| Light Source | Type | Color Temperature | Intensity | Position |
|--------------|------|-------------------|-----------|----------|
| 纸窗自然光 | Directional | 6500K (daylight) | 0.8 | 北墙 |
| 手术灯 | Spot | 5500K (cool white) | 1.2 | 病床上方 |
| 桌面油灯 | Point | 3200K (warm) | 0.5 | 诊桌 |

> **光照原则**（承 art-bible §2）: 三种光源混合，纸面元素（脉案 / 舌象色卡）在应急光下仍可读（AC-42-F1）。

---

## Interaction Zones

| Zone | Interactive Objects | Purpose |
|------|---------------------|---------|
| 诊桌区 | 脉案纸页 + 诊脉台 + 戥子 | 诊断态（五通道） |
| 药柜区 | 药材格 + 铜筹计数 | 库存管理 |
| 病床区 | 病人 + 手术灯 + 听诊器 | 急救操作 |
| 油灯区 | 油灯旋钮 | 照明开关 |

---

## Color Palette (from Art Bible)

| Element | Color | Role |
|---------|-------|------|
| 夯土墙 | `#C4B8A8` (mud beige) | 墨侧 |
| 青苔 | `#6B8E5A` (moss green, 墙根) | 墨侧自然色 |
| 石板地 | `#8B8B8B` (gray stone) | 墨侧 |
| 旧木梁 | `#3D3D3D` (dark wood) | 墨侧深色 |
| 木家具 | `#5C4033` (walnut brown) | 墨侧 |
| 黄铜拉手 | `#B8863B` (antique brass) | 铜侧点缀 |
| 旧棉褥 | `#4A4A4A` (dark gray cloth) | 墨侧 |
| 纸窗透光 | `#FFF8E7` (warm white, 半透明) | 自然光 |

---

## Accessibility

- 纸窗透光 = 自然光引导（视觉障碍玩家可感知光源方向）
- 油灯暖光 = 桌面照明引导（承 AC-42-F1 文本同步）
- 医馆内无血条 / 无小地图（承支柱四）

---

## AI Generation Prompt (for reference)

```
An interior of a traditional Chinese medical clinic (医馆), Late Qing / early Republican era, circa 1910s.
Room: Single room, functional layout with diagnostic desk, medicine cabinet, and patient bed.
Walls: Mud-brick walls with moss at base, dark wooden beams crossing ceiling.
Floor: Gray flagstone with soil in gaps.
Windows: Paper windows (半透光) with wooden frames, soft daylight entering.
Furniture: Dark wood diagnostic desk with ink stains, multi-drawer medicine cabinet with brass handles, simple wooden patient bed with old cotton mattress.
Lighting: Three-source mix — daylight from paper window, cool white surgical lamp, warm 3200K oil lamp on desk.
Details: Aged wood texture, brass hardware with patina, worn cotton bedding, scattered small props (针囊, 铜筹, medicine jars).
Mood: Scholarly, calm, historically authentic.
Constraints: NO modern medical equipment, NO digital elements. Pure historical Chinese medical interior.
Resolution: 2048x2048 texture atlas, full room view.
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

- [x] ~~医馆是否支持扩建（P0 = 单房间；P1a = 多房间 / 扩建）？？~~ → **2026-09-29 裁定：P0 = 单房间**。
- [x] ~~纸窗是否支持开合动画（P0 = 固定；P1a = 开合）？？~~ → **2026-09-29 裁定：纸窗固定**。
- [x] ~~医馆外环境（小镇街道）是否 P0（当前 = 室内单场景）？？~~ → **2026-09-29 裁定：室内单场景**。
---