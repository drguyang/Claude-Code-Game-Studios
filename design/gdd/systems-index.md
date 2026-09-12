# Systems Index: 《杏林江湖》 (The Apothecary's Journey)

> **Status**: Draft
> **Created**: 2026-09-11
> **Last Updated**: 2026-09-11
> **Source Concept**: `design/gdd/story-bible.md`（叙事真源）· `CLAUDE.md`（项目骨架）

---

## Overview

《杏林江湖》是一款**队伍制叙事 RPG + 回合制策略战斗**。它的机制范围由两条轴线界定：**叙事线**（十卷、一卷一史实锚点、队伍聚散）与**医学认识论线**（还原论 × 整体论的双核，贯穿战斗与成长）。因此本作需要的系统，本质上是为"扮演一个在病床前同时使用两套医学眼睛的医者"服务的：一套承载**辨证施治**的战斗系统，一套承载**图谱连通**的成长系统，以及支撑十卷叙事的对话/论战/关卡系统，和把这一切串起来的队伍、证候、存档等基础系统。本索引登记全部已识别系统，并给出依赖顺序与设计优先级。

---

## Systems Enumeration

| # | 系统名称 | 类别 | 优先级 | 状态 | 设计文档 | 依赖 |
|---|---|---|---|---|---|---|
| 1 | 证候 / 状态系统 Status & Syndrome | Core | MVP | **DRAFT** | `design/gdd/status-syndrome.md` | （无） |
| 2 | 队伍 / 角色系统 Party & Character | Core | MVP | **DRAFT** | `design/gdd/party-system.md` | 证候系统 |
| 3 | 成长系统 Progression（医道图谱） | Progression | MVP | **DRAFT** | `design/gdd/progression-system.md` | 战斗、证候、存档、故事圣经 |
| 4 | 战斗系统 Combat（辨证施治） | Gameplay | MVP | **DRAFT** | `design/gdd/combat-system.md` | 成长、证候、关卡 |
| 5 | 对话系统 Dialogue | Narrative | MVP | **DRAFT** | `design/gdd/dialogue-system.md` | 队伍、故事圣经、成长、证候 |
| 6 | 关卡 / 遭遇系统 Level & Encounter | Gameplay | MVP | **DRAFT** | `design/gdd/level-system.md` | 战斗、队伍 |
| 7 | 任务 / 剧情流程系统 Quest & Story Flow | Narrative | Vertical Slice | **DRAFT** | `design/gdd/quest-system.md` | 对话、关卡 |
| 8 | 论战系统 Debate（说服） | Gameplay | Vertical Slice | PLANNED | — | 成长、对话 |
| 9 | 疫情资源博弈系统 Epidemic Strategy | Gameplay | Vertical Slice | PLANNED | — | 战斗、队伍 |
| 10 | 存档 / 读档系统 Save & Load | Persistence | MVP | PLANNED | — | 成长、战斗、任务 |
| 11 | UI / HUD | UI | MVP | PLANNED | — | 战斗、成长、队伍 |
| 12 | 引导 / 教学系统 Onboarding | Meta | MVP | PLANNED | — | 战斗、对话 |
| 13 | 音频系统 Audio | Audio | Full Vision | PLANNED | — | （无） |
| 14 | 本地化系统 Localization | Meta | Full Vision | PLANNED | — | 对话、UI |
| 15 | 无障碍系统 Accessibility | Meta | Full Vision | PLANNED | — | UI |

> **状态图例**：Not Started / In Design / In Review / Approved / Implemented。本表用 **DRAFT**（= In Design）与 **PLANNED**（= Not Started）两种。带 (inferred) 的为本轮推断补入。

---

## Categories

| 类别 | 说明 | 本作所含系统 |
|---|---|---|
| **Core** | 一切系统的基础 | 证候 / 状态、队伍 / 角色 |
| **Gameplay** | 让游戏"好玩"的系统 | 战斗、关卡 / 遭遇、论战、疫情资源博弈 |
| **Progression** | 玩家随时间成长 | 成长（医道图谱）、队伍成长 |
| **Persistence** | 存档与连续性 | 存档 / 读档 |
| **Narrative** | 故事与对白交付 | 对话、任务 / 剧情流程 |
| **UI** | 面向玩家的信息呈现 | UI / HUD |
| **Audio** | 声音与音乐 | 音频 |
| **Meta** | 核心循环之外的系统 | 引导 / 教学、本地化、无障碍 |

---

## Priority Tiers

| 层级 | 定义 | 目标里程碑 | 设计紧迫度 |
|---|---|---|---|
| **MVP** | 核心循环必需；缺了就无法验证"这是否好玩" | 首个可玩原型 / 竖直切片（卷 1–3） | 优先设计 |
| **Vertical Slice** | 完成一个完整可玩区域所需 | 竖直切片 / Demo | 次优先 |
| **Alpha** | 功能齐全（含粗糙版） | Alpha | 第三 |
| **Full Vision** | 打磨、边界、锦上添花 | Beta / 发布 | 按需 |

> 本作的竖直切片 = **卷 1–3**，仅用 4 名角色（沈砚之 / 陆九针 / 柳希白 / 苏晚棠），因此 MVP 与竖直切片高度重叠。

---

## Dependency Map

### Foundation Layer（无依赖）

1. **证候 / 状态系统** — 证候向量、病机、词条是所有医学机制的公共数据结构；战斗与成长都建立在它的标签体系上。
2. **音频系统** — 技术底座，无逻辑依赖。

### Core Layer（依赖 Foundation）

1. **队伍 / 角色系统** — 依赖：证候系统（队员属性与状态挂载）。
2. **成长系统（医道图谱）** — 依赖：证候系统（节点/证候共享标签）、存档系统。
3. **战斗系统（辨证施治）** — 依赖：成长系统（牌/连线）、证候系统。

### Feature Layer（依赖 Core）

1. **关卡 / 遭遇系统** — 依赖：战斗系统、队伍系统。
2. **论战系统** — 依赖：成长系统、对话系统。
3. **疫情资源博弈系统** — 依赖：战斗系统、队伍系统。
4. **对话系统** — 依赖：队伍系统、故事圣经。
5. **任务 / 剧情流程系统** — 依赖：对话系统、关卡系统。

### Presentation Layer（依赖 Feature）

1. **UI / HUD** — 依赖：战斗、成长、队伍。
2. **引导 / 教学系统** — 依赖：战斗、对话。
3. **本地化系统** — 依赖：对话、UI。

### Polish Layer（依赖一切）

1. **无障碍系统** — 依赖：UI、本地化。
2. **存档 / 读档系统** — 依赖：成长、战斗、任务（贯穿各层，实现上属横切关注点）。

---

## Recommended Design Order

| 顺序 | 系统 | 优先级 | 层 | 建议负责 Agent | 预估工作量 |
|---|---|---|---|---|---|
| 1 | 证候 / 状态系统 | MVP | Foundation | systems-designer | M（**已起草**） |
| 2 | 队伍 / 角色系统 | MVP | Core | game-designer | M（**已起草**） |
| 3 | 成长系统（医道图谱） | MVP | Core | systems-designer | L（**已起草**） |
| 4 | 战斗系统（辨证施治） | MVP | Core | game-designer | L（**已起草**） |
| 5 | 对话系统 | MVP | Feature | narrative-director + writer | M（**已起草**） |
| 6 | 关卡 / 遭遇系统 | MVP | Feature | level-designer | M（**已起草**） |
| 7 | 存档 / 读档系统 | MVP | 横切 | lead-programmer | S |
| 8 | UI / HUD | MVP | Presentation | ui-programmer | M |
| 9 | 引导 / 教学系统 | MVP | Presentation | game-designer | S |
| 10 | 论战系统 | Vertical Slice | Feature | systems-designer | M |
| 11 | 疫情资源博弈系统 | Vertical Slice | Feature | systems-designer | M |
| 12 | 任务 / 剧情流程系统 | Vertical Slice | Feature | narrative-director | M（**已起草**） |
| 13 | 音频系统 | Full Vision | Foundation | audio-director | M |
| 14 | 本地化系统 | Full Vision | Presentation | localization-lead | S |
| 15 | 无障碍系统 | Full Vision | Polish | accessibility-specialist | S |

> 工作量：S = 1 次会话，M = 2–3 次，L = 4+ 次（"会话" = 一次聚焦设计对话产出完整 GDD）。

---

## Circular Dependencies

- **成长系统 ↔ 战斗系统**：成长提供牌与连线加成，战斗产出顿悟点供成长消费。
  **化解**：时序分离——牌在战斗**开始前**由成长决定，IP 在战斗**结束后**结算，二者不在同一刻互取。已确认为非循环（见两份 GDD 第 6 节）。
- 其余：**无发现**。

---

## High-Risk Systems

| 系统 | 风险类型 | 风险描述 | 缓解 |
|---|---|---|---|
| 战斗系统 | Design | "双条 + 双资源 + 证候向量 + 牌组"叠加，认知负荷可能过高，玩家看不懂 | 竖直切片先做**单一病机、单病人**最小验证；分步教学（卷一仅还原） |
| 成长系统 | Design | 双树 + 共享汇通图可能让玩家不知从何下手 | 卷四才开放汇通图；提供"推荐连线"引导；汇通率可视化 |
| 论战系统 | Design | "说服"易做成空谈，缺乏可玩的判定 | 复用战斗的匹配/资源框架；卷八再设计，不阻塞 MVP |
| 疫情资源博弈 | Scope | 大规模多人病患模拟的实现成本高 | 先做"3 病人取舍"小样验证，再决定是否上量 |
| 本地化 | Scope | 中医术语翻译难度极高、易失真 | 术语表先行；中文为主，翻译延后 |
| 证候系统 | Technical | 标签体系若不稳定，将波及战斗与成长两侧 | **最先冻结**标签体系（八纲/八法/病种），再设计上层 |

---

## Progress Tracker

| 指标 | 数量 |
|---|---|
| 已识别系统总数 | 15 |
| 已开始设计（Draft） | 7 |
| 已评审 | 0 |
| 已批准 | 0 |
| MVP 系统已设计 | 6 / 9 |
| 竖直切片系统已设计 | 7 / 12 |

---

## Next Steps

- [ ] 评审并批准本系统枚举（尤其 Foundation 层的证候系统）
- [ ] 优先设计 MVP 系统：**证候 / 状态系统**（冻结标签体系）→ 队伍 / 角色系统 → 对话系统 → 关卡系统
- [ ] 对已起草的 `combat-system.md` 与 `progression-system.md` 运行 `/design-review`
- [ ] 为最高风险系统（战斗认知负荷、成长上手）做 `/vertical-slice` 验证
- [ ] MVP 系统设计完成后运行 `/gate-check pre-production`
