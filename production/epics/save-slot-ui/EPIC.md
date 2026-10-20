# Epic: 7b 存档位 UI

> **Layer**: **Presentation**(`systems-index.md:43` 类别) —— ⚠️ `README.md:5` 与 `story-001:6` 写 `Foundation`,见 §Layer 订正
> **GDD**: `design/gdd/save-slot-ui.md`(✅ Approved 2026-09-19 · **13 条 AC**)
> **Architecture Module**: `Gameplay.UI`(42 屏幕空间模态 `ModalId.SaveSlots`)—— 只读 7a 的槽位投影,**零写入**
> **Status**: **`Ready`(卡面)· 实测 `BLOCKED-BY-7a`** —— ⚠️ 卡面零 `BLOCKED-BY` 标记,但上游两条契约在今日代码里**零落点**(§Epic Status)。已登记 **`TD-persist-02`**(不立 story)
> **Stories**: 1 story(全案 31 个 epic 中**唯一的单件 epic**,见 §S-6)
> **本 EPIC 建于**:2026-10-20(`production/p0-remaining-backlog-2026-10-20.md:271` **S-6** —— 31 个 epic 目录里唯一缺 `EPIC.md` 的一个)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 存档位 UI 基础框架（槽位呈现 + 焦点导航 + 翻页交互） | UI | **`Ready`** | ADR-013(主)· ADR-010(次) |

## Overview

7b 是**呈现层的第五件套** —— 42 底座 · 39 是纸 · 44 是耳朵 · 48 是那个教你的人 · **7b 是那本册子**。
它与 42 / 39 / 44 / 48 同构：**只触发 / 只渲染，永不持有游戏状态**(承 ADR-013 §9 C3)。

7b 是 42 的**模态闭集成员**(`ModalId.SaveSlots`，枚举权威 = `ADR-013 §十 / §十-B`)，
形态 = **UI Toolkit 屏幕空间模态**：开册即 42 模态，世界交互经 4 冻结，**移动不抑制**(对齐 39，
`AC-1-23` 白名单不加 7b)，**不发档位意图**(承 39 的 `O-12`)。

**三方分离是本 epic 的全部内容**：**7a 逻辑 · 7b 语义 · 42 渲染**。
7b 若自己判断「能不能存」，就是越权 —— 这是 `AC-7b-02` / `AC-7b-10` 的全部判据。

| 面 | 归 7b | 不归 7b |
| --- | --- | --- |
| 槽位列表的**呈现组织**(册页 / 翻页 / 槽序) | ✅ | — |
| 约束的**呈现**(只进不退的灰置 / 文案) | ✅ | — |
| 禁载 / 拒写的**语义** | ❌ 7a | — |
| 槽位**写入**(`Checkpoint`) | ❌ 7a | — |
| 槽位**头信息**(tick / 时间戳 / 摘要) | ❌ 7a 给语义(投影 DTO 定义归 7b) | — |
| 渲染 / 焦点引擎 | ❌ 42 | — |
| 存档策略(事件为主 + 定时兜底) | ❌ 7a | — |
| **游戏状态** | ❌ **绝不** | — |

## Epic Status

**结论：`Ready` 不等于可开工。** 以下全部为 2026-10-20 本 EPIC 补建轮的**逐代码实测**(非卡面转抄)。

### 实测五项

| # | 事实 | 实测值 | 后果 |
|---|---|---|---|
| **1** | `ISaveService.Checkpoint(SaveSlot)` / `Load(SaveSlot)` —— AC-7b-01 点名的**唯一写入 API**(签名出处 `adr-010:324-327`) | **零落点**。`unity/Assets/Sim.Codec/ISaveService.cs:20-28` 只有 `SaveBytes` / `LoadBytes`，其 doc 自陈「高层方法（Checkpoint/SaveOnExit/Load）归后续 story」 | AC-7b-01 的**宾语不存在**;7b 拿不到「写」这个动作 |
| **2** | `Writable` / `Loadable` —— AC-7b-02 / AC-7b-10 要求「全部来自 7a」 | **零落点**。生产侧 `unity/Assets/` 带这些名字的只有 `EnemyEncounter.ValidateWritableSet`(同名不同物) | 灰置 / 页角小字**没有语义源**;F-7b.2 两投影**无输入** |
| **3** | 读档即锁 / `Gating`(`AC-7a-19` 的读侧半边) | **零落点** | 7a 规则十四的读侧半边未实现 |
| **4** | 槽位头信息(tick / 时间戳 / 摘要) | **零落点**。`struct SaveSlot { public uint SlotSeq; }`(`ISaveService.cs:13-17`)**只有排序键** | 册页每格最多画得出一个序号 |
| **5** | 42 侧**已有外壳** | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/SaveSlotsScreen.cs` = **44 行**(由 42 的 `story-012-save-slots`,已 `Complete`)—— 一个空 `slot-list` + 一张纸,**零槽行、零焦点、零投影调用** | story-001 的**实际剩余面 = 槽行 + 排序 + 投影 + 焦点**，**不是**外壳 |

> **✅ 第 1–3 项已入册**:`docs/tech-debt-register.md` **`TD-persist-02`**
> (2026-10-20 登记 · 用户裁定「G-4 也登记 TD,不立 story」)。
> 路线图侧同一条 = `p0-remaining-roadmap-2026-10-20.md` §三 **G-4**。
> ⚠️ **入册 ≠ 已还** —— 本 epic 的三条 BLOCKING AC 判据**仍然一条都不可达**。

### 由此得到的取件判据

> 本 epic 与 `production/milestones/p0-remaining-roadmap-2026-10-20.md` §四 **A-1** 的登记**不一致**。
> A-1 的入选依据是「卡面零 `BLOCKED-BY` 标记」—— 而那是**卡面属性**，不是**代码测量**。
> 逐代码实测后，story-001 的两条上游链（第 1 / 2 项）在今日树里**均不存在**。
> 按该文件 §六·3 的 **R-2**(每个 `BLOCKED-BY-X` 的 X 是否仍成立)与 **R-3**(前提是「实现」还是「补登记」)判据，
> **`save-slot-ui/001` 应移出 A-1**。
> ⚠️ 本 EPIC **只登记实测，不改路线图** —— 修法待用户裁定(见 §Next Step 之二)。

### AC 载体覆盖(13 条 GDD AC vs 1 张卡)

| AC | 载体 | 状态 |
|---|---|---|
| AC-7b-01 · 02 · 04 · 05 · 06 · 07 · 10 | `story-001-slot-presentation.md:33-39` | ✅ 有卡 |
| **AC-7b-03** | — | ⚠️ **无卡**。GDD 载体欠账表(`save-slot-ui.md:418`)点名「ADR-017 门 A 白名单」 |
| **AC-7b-08** | — | ⚠️ **无卡**(`[L]` 物理感走查) |
| **AC-7b-09** | — | ⚠️ **无卡**。载体欠账表(`:419`)点名「44 的音频事件名闭集」 |
| **AC-7b-11** | — | ⚠️ **无卡**(零 `IEventSink.Append`)—— story-001 只在 Control Manifest 里写了同名要求，**未列为 AC** |
| **AC-7b-12** | — | ⚠️ **无卡**(灰置不单独依赖颜色) |
| **AC-7b-13** | — | ⚠️ **无卡**。载体欠账表(`:420`)点名「7a 侧损坏注入夹具」 |

**7 条有卡 / 6 条无卡。** GDD 评审日志已把 `AC-7b-03 / 09 / 13` 登记为**重开触发条件第 4 条**
(`design/gdd/reviews/save-slot-ui-review-log.md:22`)—— 即「载体到期未出现 ⇒ 判据失效，须重启评审」。

## Layer 订正

- **权威** = `systems-index.md:43`「类别 = **Presentation**」。
- **不一致** = `README.md:5` 与 `story-001:6` 均写 `Foundation`。
- 本 EPIC 取 Presentation;**两处待回刷，归 7b 实现轮**(本件不改卡面，避免与 sprint 取件口径打架)。
- ⚠️ 同类不一致**不止这一处**(如 7a:`systems-index:42` 记 Core / `persistence-service/EPIC.md:3` 记 Foundation)。
  本行只登记 7b 自身，**不做全库对账**。

## S-6 结案：单件 epic 是有意的吗？M3 分母怎么算？

**问**(`production/p0-remaining-backlog-2026-10-20.md:271`)：「若有意(单件 epic)，在 M3 分母口径里注明。」

**答 —— 两条**：

1. **有意，且分母仍是 31。** 依据 `systems-index.md` 注③ 定 7b 为 **S 级**
   (零机制 · 零公式除排序投影 · 1 上游 7a + 1 渲染 42) ⇒ 它**结构上不该拆成多件**。
   单件不是欠账。
2. **但「单件」不等于「一件就够过门」。** 13 条 AC 里 **6 条无卡**(上表)。
   M3 的 `31 个 P0 epic 全部 Complete`(`production/milestones/README.md:371`)若按 **story 状态**判，
   本 epic 会在一张卡 `Complete` 时转绿，而 **6/13 AC 无人认领** —— 与 `audio-system`
   (`21/21` 全 `Complete` 但自陈 ⛔ 不得收口)是同一类假绿。

⇒ **本 epic 的收口判据** = 路线图 §五 的修正口径：

```
epic Complete ⇔ story 全 Complete ∧ 名下 AC 全有载体 ∧ 名下 TD 的 P0 面 = 0
```

本 epic 当前：`story 1/1 全非 Complete` · `AC 有卡 7/13` · `TD P0 面 0` ⇒ **未收口**。

## Dependencies

| 上游 | 要什么 | GDD 声明状态 | **代码实测(2026-10-20)** |
| --- | --- | --- | --- |
| **7a 持久化服务** | `SaveSlot` 契约 + 槽位列表语义 + 规则十四 + 载入安全上下文门 | ✅ `persistence-service.md` | ⚠️ `SaveSlot` ✅ 有(`ISaveService.cs:13`)· **槽位语义 / `Writable` / `Loadable` / `Gating` 全零落点** |
| **42 拟物 UI** | 册页渲染 + 焦点导航 + 纸纹元件 + `ModalId.SaveSlots` | ✅ `skeuomorphic-ui.md` | ✅ 枚举有(`ModalId.cs:18`)· 外壳有(`SaveSlotsScreen.cs`, 44 行) |
| **3 输入与设备** | `iconKey`(仅此) | ✅ `input-system.md:762` | — |
| **ADR-010 §Key Interfaces** | `struct SaveSlot` 定义(2026-09-19 已补) | ✅ | ✅ 类型在;**同文件的高层方法零落点**(见 §Epic Status 1) |

**下游**：3(焦点导航消费者，经 42 焦点头)· 48(存档册教学第⑤步)· 44(翻页 / 合册音)。
**1 玩家控制器 / 2 相机 = 无依赖**(不抑制移动 · 不发档位意图)。

## Next Step

**三条，按依赖序**：

1. **先落 7a 的槽位契约**(`Checkpoint` / `Load` / `Writable` / `Loadable` / `Gating`)。
   ✅ **归属已裁**(2026-10-20):登记为 **`TD-persist-02`**,**不立 story** ——
   与 `TD-sim-01` / `TD-persist-01` 同一口径(`persistence-service` 已标 `Complete 2/2`,
   补立会扰动 M3 分母)。
   ⇒ **本 epic 可先交「槽行 + 排序 + 灰置呈现骨架」**(`AC-7b-04` / `06` / `07` / `12`),
   **`AC-7b-01 / 02 / 10` 三条 BLOCKING 必须挂 `BLOCKED-BY-7a`**,不得勾、不得记 `Complete`。
2. ~~**修正路线图 §四 A-1**~~ ✅ **已完成(2026-10-20)** —— `save-slot-ui/001` 已移出 A-1 → A-2,
   §七 增设 **SP-6**(A-1 判据的洞),§六 6.3 增设 **R-4**(上游契约逐代码实测)。
3. **6 条无卡 AC 的归属**：`AC-7b-11 / 12` 可并入 story-001(同一装配面);
   `AC-7b-03 / 09 / 13` 的载体按 GDD 欠账表在**别的 epic**(ADR-017 门 A 白名单 / 44 事件表 / 7a 损坏夹具)，
   **本 epic 只登记依赖，不自造载体**。

> ⚠️ **禁借绿**：本 epic 的任何一条 AC 在 2026-10-20 均**未验证**。`Ready` 是卡面状态，
> 不是「已就绪可勾」。本 EPIC 与 story-001 的 `Status` 字段**本轮均未改动**。
