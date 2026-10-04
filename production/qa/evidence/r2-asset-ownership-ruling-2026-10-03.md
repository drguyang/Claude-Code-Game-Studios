# R-2 裁定 · 42 项 VS Critical 资产归属(按类型分别归属)

> **Date**: 2026-10-03
> **裁定**: 承 `art-asset-pipeline-gap-rulings-2026-10-03.md` R-2 —— **按类型分别归属**
> **依据**: `art-bible §8.11.2 D1`「资产须在该系统 epic 的 story 件内显式登记」
> **一句话**: 不选单一归属方;角色/环境/道具/界面类挂**消费系统**,
> SFX/Ambient 挂 **audio-system**(急救专用音除外),VFX 挂**触发系统**。

---

## 归属规则(逐类)

| 类 | 数 | 规则 |
|---|---|---|
| Characters | 2 | 消费系统 |
| Environment / Buildings | 1 | 消费系统 |
| Props | 6 | 消费系统 |
| Items | 6 | 消费系统 |
| UI Screens | 1 | 消费系统 |
| HUD Elements | 8 | 消费系统 |
| SFX | 10 | **通用 8 → audio-system · 急救专用 2 → emergency-procedures** |
| Ambient | 1 | **→ audio-system** |
| VFX Events | 7 | **触发系统** |

> ⚠️ **急救专用 SFX 的例外**(止血包扎接触音 / CPR 节律音)—— 由 `emergency-procedures`
> 触发,归该 epic 而非集中 audio-system。**用户 2026-10-03 裁定**。

---

## 逐 epic 归属表(42 / 42,无遗漏无重叠)

### `audio-system`(9 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [SFX #1] 诊脉接触音 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 2 | [SFX #2] 听诊器接触音 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 3 | [SFX #3] 病人呼吸两层 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 4 | [SFX #6] 病人语声 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 5 | [SFX #7] 纸面物理声 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 6 | [SFX #8] 脚步声 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 7 | [SFX #9] 药柜/器物声 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 8 | [SFX #10] 世界声 | 新立 story-015(已在 R-5 立)+ 素材批次 |
| 9 | [Ambient #1] 医馆室内环境音 | 新立 story-015(已在 R-5 立)+ 素材批次 |

### `diagnosis-system`(7 项)

> ✅ **归属口径统一(2026-10-04 用户裁定 G · 消双写)**:**#4 / #5 / #6 三项(`HUD Elements #2/#3/#4`)
> 同时被 `skeuomorphic-ui` 的 42 元件库面覆盖** —— 那是**「呈现元件」与「消费系统」两种读法**,
> **不是两个所有者**。现定:
> - **归属(谁定义资产需求 / 谁承接出图)** = **`diagnosis-system`**(下方三行,依「按消费系统」判据);
> - **`skeuomorphic-ui` 侧只保留「呈现元件引用」**(即这 3 项走 42 的元件库渲染,与 §8.11.6 引用边同源),
>   **不另立资产登记**。
>
> ⚠️ **与 `教学纸近景` 先例方向相反,须显式区分**:那一次(`entity-inventory.md` Props#8 vs HUD#4)
> 是**同一资产的重复计数**,去重后归**呈现侧(HUD)**;本三条是**两个不同问题**:
> 「谁拥有资产需求」(→ 消费系统)与「谁负责渲染」(→ 42)。
> **判据不同,故结论相反,不构成矛盾。**
> ⚠️ **立 story 时必须二选一登记**,禁两处都写(违「不另立第二真源」)。

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Characters #2] 普通病人（T0–T3） | story-00X 资产登记(挂消费 story) |
| 2 | [Props #3] 诊脉台读数（铜器刻度盘） | story-00X 资产登记(挂消费 story) |
| 3 | [Props #4] 听诊器 | story-00X 资产登记(挂消费 story) |
| 4 | [HUD Elements #2] 诊脉台刻度盘 | **归属 = 本系统**(2026-10-04 G 裁);42 侧只留呈现元件引用 |
| 5 | [HUD Elements #3] 呼吸波形纸带 | **归属 = 本系统**(2026-10-04 G 裁);42 侧只留呈现元件引用 |
| 6 | [HUD Elements #4] 舌象色卡 | **归属 = 本系统**(2026-10-04 G 裁);42 侧只留呈现元件引用 |
| 7 | [VFX Events #3] 呼吸波形滚动 | story-00X 资产登记(挂消费 story) |

### `emergency-procedures`(7 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Props #6] 手术灯（急救场景，冷光白） | story-00X 资产登记(挂消费 story) |
| 2 | [Items #5] 针具（针灸） | story-00X 资产登记(挂消费 story) |
| 3 | [Items #6] 绷带 / 止血包 | story-00X 资产登记(挂消费 story) |
| 4 | [SFX #4] 止血包扎接触音 | story-00X 资产登记(挂消费 story) |
| 5 | [SFX #5] CPR 节律音 | story-00X 资产登记(挂消费 story) |
| 6 | [VFX Events #6] 急救止血包扎 VFX | story-00X 资产登记(挂消费 story) |
| 7 | [VFX Events #7] CPR 节律视觉反馈 | story-00X 资产登记(挂消费 story) |

### `casebook`(5 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Props #1] 脉案纸页（线装书） | story-00X 资产登记(挂消费 story) |
| 2 | [UI Screens #1] 脉案页（诊断态纸面） | story-00X 资产登记(挂消费 story) |
| 3 | [HUD Elements #1] 脉案纸页展开 | story-00X 资产登记(挂消费 story) |
| 4 | [HUD Elements #7] 墨迹落笔 | story-00X 资产登记(挂消费 story) |
| 5 | [VFX Events #4] 病历落笔墨迹 | story-00X 资产登记(挂消费 story) |

### `foraging`(4 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Items #1] 柳树皮 | story-00X 资产登记(挂消费 story) |
| 2 | [Items #2] 毛地黄 | story-00X 资产登记(挂消费 story) |
| 3 | [Items #3] 金鸡纳树皮 | story-00X 资产登记(挂消费 story) |
| 4 | [Items #4] 止血草 | story-00X 资产登记(挂消费 story) |

### `skeuomorphic-ui`(3 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [HUD Elements #8] 焦点高亮（黄铜边框 2px） | story-019 已承接(HUD#8 焦点高亮 · VFX 水墨/黄铜) |
| 2 | [VFX Events #1] 水墨晕染 | story-019 已承接(HUD#8 焦点高亮 · VFX 水墨/黄铜) |
| 3 | [VFX Events #2] 黄铜反光 | story-019 已承接(HUD#8 焦点高亮 · VFX 水墨/黄铜) |

### `inventory-and-items`(2 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Props #2] 出诊箱（药箱） | story-00X 资产登记(挂消费 story) |
| 2 | [HUD Elements #5] 出诊箱抽屉展开 | story-00X 资产登记(挂消费 story) |

### `patient-ai`(2 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [HUD Elements #6] 体征异常脉冲 | story-00X 资产登记(挂消费 story) |
| 2 | [VFX Events #5] 病人状态变化 | story-00X 资产登记(挂消费 story) |

### `player-controller-and-movement`(1 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Characters #1] 主角（医者） | story-00X 资产登记(挂消费 story) |

### `clinic-machine`(1 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Environment / Buildings #1] 医馆（单房间，P0） | story-00X 资产登记(挂消费 story) |

### `prescription-and-medication`(1 项)

| # | 资产 | 承接形态 |
|---|---|---|
| 1 | [Props #5] 戥子（称药器具，黄铜） | story-00X 资产登记(挂消费 story) |

**合计 = 42 / 42**

---

## 与 R-1 / R-3 / R-5 的关系

- **R-1**(引用边):已落 31/31 GDD。R-2 定的是**归属**,R-1 记的是**涉及** —— 两者不冲突:
  引用边写明「本系统涉及哪些资产」,本件写明「谁负责做出来」。
- **R-3**(spec 认领):19 份 spec 已随消费系统具名认领;其归属与 R-2 一致。
- **R-5**(音频接入):`audio-system` 分到 9 项(R-2),其落地由 `story-015` 承接。
- **skeuomorphic-ui/story-019**:已承接 HUD#8(焦点高亮)+ VFX#1/#2(水墨晕染/黄铜反光)。

---

## 承接形态(待 producer 排期)

> ⚠️ `art-bible §8.11.3` 明写**排期归 producer**,不写死在本件。本件只定**归属**,
> 不定何时做 —— 承接 story 的**编号与排期**须由 producer 在 M4 启动前裁定。

本件为 11 个 epic 各列了建议承接形态,但**编号一律留 `story-00X`**,
不预设具体号,避免与 producer 排期冲突。
