# ADR-045: 第四条逻辑流「任务流」(`Quest`)与奇遇事件的两个 Kind

## Status

**Accepted**(2026-11-14)

> **2026-11-14 用户裁定「优先级按 World 之后,落实现」** —— 即 D1 的优先级序
> (`History 0 < Case 1 < World 2 < Quest 3`)**照起草落**,且 M1–M8 同日开工。
> **同日实现已落盘**(M1–M7 全落;M8 的**数据表**按 §D9′ 住代码 `AdventureSteps.PrologueFive`,
> 其**内容可达性**未通 —— 见 §Validation 末注与 `prologue-beat-script.md` §1.1 `N5` 行)。
> 其余细项(D2 载荷 / D5 唯一性 / D6 掉落 / D8 折叠 / D10 订正清单)由起草轮拟定、
> 用户未逐条过问 —— 实现轮若发现不可行,须回改本件而不是在代码里私改。
>
> **用户 2026-11-14 已裁的三项**(本件的成因):
> ① 奇遇事件**单独作为任务流** —— 不塞进病史流,也不塞进世界流;
> ② 新增**两个** Kind(`AdventureAccepted` / `AdventureCompleted`),不是一个;
> ③ 奇遇完成时组件**掉在玩家脚下**(玩家自己捡),不直接进背包。
>
> ⚠️ **本件不是「任务系统 GDD」。** 它只回答一件事:**奇遇的进度状态存在哪**。
> 通用任务系统(任务链 / 日志 UI / 状态机)仍未立项 —— 用户 2026-11-14 裁定
> 「选 1 过于庞大,先改序章的设计件」。

## Date

2026-11-14

## Last Verified

2026-11-14

## Decision Makers

dr_guyang(用户 · 2026-11-14 三项裁定)· technical-director(起草)
· 52 随机事件导演 / 37 病例系统(奇遇内容的相邻系统)
· 7a 持久化(新流的折叠 / 重建义务承接方)· 45 网络层(新流的跨端语义,**本件不裁**)

## Summary

序章的五条奇遇(`PA-01…PA-05`,`design/levels/prologue-beat-script.md` §3.1)**今天没有任何载体** ——
「采了 6 次」「击败了 1 个兵痞」这些进度只活在内存里,读档即丢,违反 ADR-005 唯一真源。
用户裁定**为它单开一条逻辑流**(任务流),而不是塞进既有的三条。本 ADR 定义:
`StreamId.Quest = 3`(追加,**优先级排在 World 之后**,不折叠)、两个 Kind
(`AdventureAccepted` / `AdventureCompleted`)、唯一写者 `AdventureWriter`、以及与既有三流契约的订正面。
**关键成本事实:存档布局零变更** —— 事件段本来就是三流合一,新流只多一个**分类值**。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS(6000.3.24f1) |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | **LOW** —— 纯 C# 枚举与数据契约,零引擎 API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-009**(三态 / 三问判据 / 三条逻辑流 —— 本件要改判它的 §二)· **ADR-005**(唯一真源 / 主机唯一 Append)· **ADR-024**(`entities.yaml` 是 Kind 单一真源,构建期生成路由) |
| **Enables** | 序章 `N5` 奇遇链的实现;将来「通用任务系统」若立项,可直接获得第四流(不必再改底层流契约) |
| **Blocks** | `N5`(五件组件**没有任何授予点**)· `PA-04` / `PA-05`(另需 27 的遭遇实体接通) |
| **Ordering Note** | 本件 §Decision D1(`StreamId.Quest`)必须**先于** `entities.yaml` 的 `stream: quest` 落盘 —— 否则 kindgen A1(`stream ∈ {history, case, world}`)会把新条目判成非法 |

## 1. Context(为什么必须裁)

### 1.1 奇遇的进度今天没有载体

五条奇遇的完成判据全是**可观测事实的累计**:在废墟 A 采集 6 次 / 击败兵痞 1 名 / 击败野狗 2 只。
「累计到第几次了」这件事:

- 若存在**内存字段**里 ⇒ 读档即丢(ADR-005:状态必须是事件流的纯函数);
- 若每次从 `ResourceHarvested` 全量重算 ⇒ 每次进入都要扫全流(ADR-036 实测过这个坑:
  2000 条时逐例全扫 = 1135 ms/tick);
- 「已完成」本身是**不可逆状态**(完成后组件已经给出去),必须有一条**事件**把它钉住 ——
  否则「给过一次」与「没给过」在流层面不可区分。

⇒ 必须有事件。问题只剩:**落在哪条流。**

### 1.2 为什么不是病史流

病史流里已经住着两个「玩家级、`Patient = None`」的先例(`TechniqueUnlocked` / `ChapterAdvanced`),
`ChapterAdvanced` 的 entries 甚至写死了理由:「章推进是玩家/世界级成长事实,与 `TechniqueUnlocked` /
`SkillGrown` 同族」。**照此推下去,奇遇也该进病史流。**

但这条类比在**语义**上不成立:

- `TechniqueUnlocked` 是**能力**的永久变化(「这个大夫会不会这一招」);
- `ChapterAdvanced` 是**章节号**的单调推进(一个标量);
- 奇遇是**一次性的进程节点** —— 它有「接取 / 完成」两态,同一玩家可同时有多条在途,
  未来还会长出「放弃 / 失败」。

把它们塞进病史流,后果是病史流要为一类**既非病、也非成长**的事件持续开例外:
`Folded(p)` 的适用面、有界性论证、51 的遥测分母都要一条条写「本条不算」。
病史流的边界会从「病人的病程」滑成「除了世界之外的一切」。

### 1.3 为什么不是世界流

世界流装的是**世界状态**:`PoiStateChanged` / `StructurePlaced` / `DropSpawned` / `ActorCellEntered`。
奇遇是**玩家侧进程**(「我做到哪一步了」),不是世界的一部分 —— 联机时它按玩家分叉,
而世界流按世界收敛。硬塞进去会让「同一条世界流」在两台机器上出现**按玩家分叉**的语义。

### 1.4 成本核实(这条决定「行不行」)

| 面 | 结论 | 依据 |
|---|---|---|
| **存档布局** | **零变更** | `SaveComposer.cs:34` 原文:「事件段 = **三流合一**(病史 + 病例 + 世界,按 `EventStream` 追加序)」⇒ 流只是 Kind 的分类值,不是物理分段 |
| **存档版本 / 迁移 / 金标准** | **零变更** | 同上;不涉及 `SaveVersion`、不涉及 `ISaveMigration` |
| **`StreamId` 枚举** | 追加 `Quest = 3`,既有值**零位移** | `Sim.Contracts/StreamId.cs` |
| **跨流全序键** | 自动成立 | `EventOrderKey.StreamPriority = (int)StreamId`(`:40`)⇒ Quest 优先级 = 3,排在 World 之后 |
| **kindgen** | **需改三处常量** | `STREAM_MAP` / `order` / A1 的合法集合(`tools/kindgen/kindgen.py:49,270,10`) |
| **按流枚举的代码** | 60 处引用,其中**只有三处**需要按新流复核 | `max(patient_id)` 扫集 · `StreamIntegrity` · 遥测分母 |

⇒ **不是「重写架构」,是「给既有分类面加一个等价值」。** 成本主要落在**文档口径**上,不在代码上。

## 2. Decision

### D1 — 第四条逻辑流:`StreamId.Quest = 3`

- **追加在枚举末尾**,既有 `History = 0 / Case = 1 / World = 2` **零位移**。
- **优先级 = 成员值 = 3** ⇒ 同一 tick 的跨流全序里,任务流排在**最后**
  (`History < Case < World < Quest`)。理由:奇遇的完成是**对世界动作的解读**
  (读 `ResourceHarvested` / 击败事件),它必须排在产生它的那些事件之后 —— 排在前面
  会让「读方看到完成、写方还没写触发条件」在同一 tick 内出现。
- **不折叠**(与 `Case` / `World` 同):任务进度一旦折叠就无法重建「做到哪一步」。

### D2 — 两个 Kind,落任务流

| Kind | 何时写 | 载荷 |
|---|---|---|
| `AdventureAccepted` | 首次观测到「玩家进入了某条奇遇的计数范围」(区域 + 首个计数) | `adventure_id: i32` · `actor_id: i32` |
| `AdventureCompleted` | 完成判据满足的那一 tick | `adventure_id: i32` · `actor_id: i32` · `item_key: i32` · `qty: i32` |

- 全整数域(`ADR-024` A2);**无 `tick` / 无 `seq`**(它们是事件头 `SimEvent.Tick` / `Seq` 的量,
  入载荷即第二真源,同 `ChapterAdvancedPayload` 口径);**无 `patient_id`**(`Patient = PatientId.None`)。
- `AdventureAccepted` 与 `AdventureCompleted` 是**两个独立事实**:完成不蕴含「曾经接取」
  (读档后可能先见到 Completed),判「在途」须两者都在。

### D3 — 写者:`AdventureWriter`(唯一 Append 点)

- 住 `Gameplay.Boot`,与 `TechniqueUnlockWriter` / `ChapterAdvanceWriter` 同构:
  持 `IEventSink` + `IPayloadEncoder` + 事件流只读视图(查重)。
- **只有 `AdventureDriver` 调它**;任何第二处 `Append(Adventure*)` 都算违规(单点口径)。
- 主机权威:联机下只有主机写(ADR-005),**客户端上行归 45 的轮次,本件不裁**。

### D4 — 驱动:`AdventureDriver`(每 tick 一次,纯读)

- 排在 tick 边沿,读**已有事件**判条件:
  - 采集型 `GatherCount(区域, n)` ⇒ 读病史流 `ResourceHarvested` + `ActorCellEntered` 定区域;
  - 战斗型 `DefeatCount(类, n)` ⇒ 读世界流的击败事实(**今日无生产者,见 §Risks R2**)。
- 判定所需的一切都在流里 ⇒ 读档后重放同一 tick 得到同一结论(ADR-016 §一 三源不变量)。

### D5 — 唯一性:同一 `(actor, adventure)` 至多一条 Completed

- 重复写 = **no-op**(不写、不抛、不增流长);`Accepted` 同理。
- 依据:组件只给一次(`PrologueBeatScript` §3.1 规则 3「不可重复获得」);
  去重若靠调用方纪律,读档重放就会二次发放。

### D6 — 组件入包:完成那一刻在玩家脚下发 `DropSpawned`

- 写 `AdventureCompleted` 之后,**同 tick** 在玩家当前格写一条 `DropSpawned`
  (`item_key` / `qty` 取自 Completed 载荷),玩家自行 `DropClaimed`。
- **零 fold 改动**:物品进背包仍只有「掉落 → 拾取」这一条已被 `DropSpawned` / `DropClaimed`
  闭合的路径。不新增库存谓词,不动 R18 的品级来源桶。
- ⚠️ 玩家若不捡,组件躺在地上 —— 这是**正确行为**(世界里的东西就是这样),
  且 `DropSpawned` 已是世界流事实,读档后仍在。

### D7 — 有界性

`≤ |adventures| × |actors| × 2`(Accepted + Completed 各至多一条),**单调只增**,
与 tick 频率 / 帧率无关(扩展 ADR-008 §六,不重写)。P0:`5 × 1 × 2 = 10` 条上界。

### D8 — 不折叠 · 不独立快照 · 从流重建

任务进度**不进** `Folded(p)`(病人级谓词,本流 `Patient = None` 天然不适用),
**不设**独立快照字段 —— 与 `TechniqueUnlocked` 同格。7a 侧只需:
`Adventure*` 行**不进折叠视图**(与 `Case` / `World` 同)。

### D9 — 与既有分发的关系:只读、不反写

`AdventureDriver` **只读**病史流 / 世界流,**不向它们写任何东西**。
「奇遇完成导致世界变化」(将来的剧情分支)若需要,须经 52 / 37 各自的写者 ——
本件不提供「奇遇驱动直接改世界」的通道。

### D9′ — P0 的步骤表住**代码**,不住 JSON(实现轮就地裁定,2026-11-14)

`AdventureSteps.PrologueFive` 是代码常量表,不是 `assets/data/adventure_steps.json`。

- **理由**:① 判据闭集只有两种;② 六个数值是用户 2026-11-14 当场给定的**固定值**;
  ③ 表内**零 `Fix`**(ADR-014 的「必须外部化」收窄到承载 `Fix` 的字段)。
  走 JSON 要连带一套 binder + cooked + 烘焙菜单,而它换来的是「作者可调的五个整数」—— 今天不成立。
- **触发外部化的三个条件(任一)**:条数 > 10;出现需要反复调参的数值;通用任务系统立项。
- ⚠️ **在此之前,改这张表 = 改 `AdventureSteps.cs`**,而它不在 `assets/data/` 的
  作者态管辖内 —— 这是本件**唯一**偏离「玩法数值必须数据驱动」的地方,显式登记。

### D10 — 订正清单(三流 → 四流)

| 文件 | 订正 |
|---|---|
| `docs/architecture/adr-009-world-state-event-boundary.md` §二 | 就地加注:三流 → **四流**;写明「第四流由 ADR-045 追加,优先级 3,不折叠」 |
| `docs/architecture/adr-010-persistence-save-format.md` §三 | 追加义务:`Adventure*` 不折叠 / 不快照;事件段**仍是一段**(流是分类不是分段) |
| `docs/architecture/adr-001-networking-pipe-abstraction.md` | 「三条逻辑流」措辞加注:现为四条(联机语义**本件不裁**,归 45) |
| `unity/Assets/Sim/EventStream.cs` / `EventOrder.cs` 附近 | `max(patient_id)` 的扫集加入 Quest(**值不变** —— 本流 `Patient` 恒 `None`,但扫集要写全,否则将来加类型时静默漏) |
| `unity/Assets/Telemetry/StreamIntegrity.cs` | 新流纳入完整性检查(E-5 组键含 `StreamPriority`,自动覆盖,需夹具确认) |
| `tools/kindgen/kindgen.py` | `STREAM_MAP` / `order` / A1 合法集合加 `quest` |

## 3. Alternatives Considered

| 方案 | 为什么被否 |
|---|---|
| **A · 落病史流**(照 `ChapterAdvanced` 的先例) | 语义不成立(§1.2):奇遇是进程节点,不是病程也不是能力;塞进去会让病史流为一类「非病事件」持续开例外 |
| **B · 落世界流** | 按玩家分叉 vs 按世界收敛(§1.3);联机下同一条世界流会出现玩家级分歧 |
| **C · 不落流,内存标记** | 读档即丢,违反 ADR-005 唯一真源 |
| **D · 复用 `Craft` 事件产出** | 语义不对(不是合成);且 Craft 带配方 / 时长 / 技能门,三者都不是奇遇有的 |
| **E · 只加 Kind 不加流**(落病史流但标 `quest` 语义) | `StreamId` 就是那条分类轴 —— 「标了但不落」等于没有分类,查重 / 遥测 / 折叠都要各写一次特例 |
| **F · 等通用任务系统立项一起做** | 用户已裁「先做序章、GDD 迭代推进」;而 N5 卡着五件组件的**唯一授予点**,不能等 |

## 4. Consequences

### 正面

- 奇遇进度**可重放、可存档、可查询**(读档后重建结果逐位相同)。
- 病史流 / 世界流的边界**不被稀释** —— 将来审计「病史流里为什么有 X」时不用再解释奇遇。
- 未来通用任务系统若立项,**底层流已就位**(不必再改流契约与存档)。

### 代价(如实登记)

- **60 处 `StreamId.*` 引用**要过一遍;实测真正需要动的只有三处(D10),但**复核本身**是成本。
- ADR-009 / 010 / 001 三份 Accepted 裁决的措辞要订正 —— 这是「改判既有裁决」,
  不是「补一个新件」,必须显式留痕(§D10)。
- `StreamPriority` 从三值变四值:**任何写死 `<= 2` 或 `max(StreamPriority) == 2` 的地方要改**
  (实现轮须 grep 复核,不得假设没有)。

## Risks

| # | 风险 | 缓解 |
|---|---|---|
| **R1** | **「有了任务流」被误读成「任务系统已存在」** | 本件 §Status 与 §Summary 已明写;实现轮的 commit message 与证据件须重复这一句 |
| **R2** | `PA-04` / `PA-05` 的**战斗型判据没有生产者**(`EnemySpawn` 生产零命中) | 用户 2026-11-14 裁定「先接 27 的遭遇实体」;在那之前 `DefeatCount` 恒 0 —— **五条里只有三条可完成**,不得声称「奇遇链能跑完」。🔁 **2026-11-14 同日**:采集三型的内容缺口已消解(关卡补 6/4/5 个止血草点)· **R2 本行不变**(它说的是战斗型) |
| **R2 结案(2026-11-14 同日)** | ✅ **战斗型生产者已接**(用户裁定「接 27 的遭遇实体」):`PrologueEncounterDriver.PrologueTwo`(废墟 B 1 兵痞 / 七里路 2 野狗)+ `CombatDriver`(出手 → `EnemyInjuryOnset` → `COMA_THRESHOLD` → `InjuryStateChanged(Coma)` → `EncounterEnded(AllDowned)`),`AdventureDriver` 按 `EncounterStarted.ProtoId` 把倒地归到「类」⇒ `DefeatCount` 不再恒 0 | ⚠️ **仍不得声称「五条都能在真关卡跑完」**:敌人**不可见 / 不还手**(27 的 tick 循环未接),真启动序走查 `NOT-RUN`;夹具只证机制(`enemy_encounter_test` 19 条) |
| **R3** | 新流被塞进越来越多的东西(奇遇 → 任务 → 剧情分支 → …) | 本件只定义**奇遇的两个 Kind**;任何第三个 Kind 须另裁(扩流 ≠ 扩 Kind 的自动许可) |
| **R4** | 联机下任务流的权威归属未裁 | 本件**不裁**;45 的轮次。P0 = 单机,不阻塞 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|---|---|---|---|
| CPU(帧时间) | — | 驱动每 tick 扫**新增**事件(增量游标,同 `PrologueFactCounter`) | 16.6 ms |
| Memory | — | P0 上界 10 条事件 × 数十字节 | — |
| Load Time | — | 事件段**不分段**,零额外步骤 | — |
| Network | — | 未裁(归 45) | — |

> ⚠️ **不得**在驱动里做「每 tick 全流扫描」:ADR-036 实测过 2000 条时 1135 ms/tick。
> 必须用 `PrologueFactCounter` 同款增量游标(只吃 `_scanned` 之后的那一段)。

## 5. Migration Plan

| 步 | 内容 | 判据 |
|---|---|---|
| **M1** | `Sim.Contracts/StreamId.cs` 追加 `Quest = 3` | 既有三个成员值零位移;`EventOrderKey` 的优先级自动 = 3 |
| **M2** | `tools/kindgen/kindgen.py`:`STREAM_MAP` / `order` / A1 合法集合加 `quest` | kindgen A1–A5 全过 |
| **M3** | `entities.yaml` 追加两个 Kind 条目(`stream: quest` / `author` / `payload_schema`) | 三字段齐;A4 兜底 |
| **M4** | `EventKind` 追加区末尾两个成员 + 重跑 kindgen | 既有黄金字节不变;`StreamRouting.g.cs` 出现两分支,生成参数 `18 history / 6 case / 17 world / 2 quest = 43 支`(⚠️ 起草时写的 `17/5/17/2 = 41` 是照抄 ADR-044 之前的分布,实测已因 `CaseReferred` / `ChapterAdvanced` 多两支 —— 以产物文件头的行为准) |
| **M5** | 载荷 struct ×2 + `PayloadCodec` 重载 + `PayloadEncoder` 两支 dispatch | kindgen A2 过 |
| **M6** | `AdventureWriter`(查重 + 编码 + Append)+ `AdventureDriver`(增量游标 + 判据闭集) | 夹具:重复完成 ⇒ 流长不变;读档重放 ⇒ 同结论 |
| **M7** | 组件掉落接线(完成时 `DropSpawned` 于玩家格) | 夹具:完成 ⇒ 玩家格多一条掉落,`item_key`/`qty` 与载荷一致 |
| **M8** | 五条奇遇的数据表(`adventure_steps.json`)+ 装配 | 三条采集型端到端可完成;两条战斗型**如实标 BLOCKED-BY-27** |

> ⚠️ **M4 的「支数字面量」与 `TD-persist-11` 同型**:全仓有一批「40 支」字面量(生产码 + 测试探针键),
> **必须一次改全**,只改一处会让白名单完整性夹具**静默退化为恒绿**。实现轮须先 `grep` 出全部命中点
> 再动手,并把命中数写进证据件。

## 6. 回写义务(本件 Accepted 后必须同步)

| 文件 | 内容 |
|---|---|
| `docs/architecture/adr-009-*.md` §二 | 三流 → 四流加注(§D10) |
| `docs/architecture/adr-010-*.md` §三 | 不折叠 / 不快照义务 |
| `docs/architecture/adr-001-*.md` | 「三条逻辑流」措辞加注(联机语义不裁) |
| `docs/architecture/adr-024-kind-single-source.md` | V-2 条目数 → **43**(`18/6/17/2`;起草时写的 `40 → 42` 是旧分布) |
| `design/levels/prologue-beat-script.md` §1.1 N5 | 「Kind 归属须另开 ADR」→「已由 ADR-045 裁定」 |
| `docs/tech-debt-register.md` `TD-building-07` | 解除条件里的「N5」指向本件 |

## 7. Validation Criteria

| # | 判据 | 状态 |
|---|---|---|
| V-1 | `StreamId.Quest = 3` 追加后**既有值零位移**;`EventOrderKey.StreamPriority` 对 Quest = 3 | ✅ **PASS** —— `adventure_chain_test.test_questKindRoutesToQuestStream_atPriorityAfterWorld`(断言 `History/Case/World = 0/1/2`、`Quest = 3`、两个 Kind 都路由到 `Quest`) |
| V-2 | kindgen A1–A5 全过,`StreamRouting` 出现 quest 两分支,总数 **43** | ✅ **PASS** —— 产物文件头逐字 `18 history / 6 case / 17 world / 2 quest = 43 支`;`drug_event_test` 的闭集哨兵 41 → 43 同批改。⚠️ **本表的 42 / 41 两个旧数是错的**(照抄 ADR-044 之前的分布),以产物为准 |
| V-3 | 既有黄金字节(`SimCodecRoundtripTest` / `DeterminismGoldenFixturesTest`)不变 | ✅ **PASS** —— 全量 EditMode `7667/7630/0/0/37` @ `2026-11-14T05:12:34Z` · `da334a4c`+工作树,两件均在其中且绿(追加区末尾 ⇒ 既有 enum 值零位移) |
| V-4 | 同一 `(actor, adventure)` 重复 `Completed` ⇒ no-op(流长不变) | ✅ **PASS** —— `test_complete_isIdempotent_andDropsOnce`(第二发 `false`;`Completed` 恰 1、`DropSpawned` 恰 1) |
| V-5 | 读档后重放同一 tick ⇒ 驱动给出同一结论(增量游标可重建) | ✅ **PASS** —— `test_driver_tickRollback_rescansFromScratch`(流变短 + tick 回退 ⇒ 按**当前流**重算,不抛也不残留在旧计数) |
| V-6 | 完成 ⇒ 玩家格多一条 `DropSpawned`,且 `item_key` / `qty` 与载荷一致 | ✅ **PASS** —— 同上,`ItemKey == step.ItemKey` / `Qty == 1` / `SpawnAnchor == 完成时玩家格` |
| V-7 | 任务流事件**不进**折叠视图(读档折叠后仍在) | ✅ **PASS(结构 + 夹具)** —— 两 Kind 的 `Patient` 恒 `None`;夹具断言完成一条奇遇后 `FoldedRows.Count == 0`。⚠️ 「读档折叠后仍在」未单独跑(折叠按病人 id 选行,`None` 不匹配任何病人 ⇒ 结构上不可达) |
| V-8 | `max(patient_id)` 在含 Quest 行的流上值不变(本流 `Patient` 恒 `None`) | ✅ **PASS** —— `test_questEvents_doNotMovePatientIdHighWatermark` |

**证伪轮(2026-11-14,三发各自转红,备份 + `cp` 还原 + SHA-256 逐字节)**:|
  M1 `RecordSpawned(step.ItemKey + 1)` ⇒ `test_complete_isIdempotent_andDropsOnce` 红;
  M2 完成事件改用 `new PatientId(actorId)` ⇒ 该条 + `test_questEvents_doNotMovePatientIdHighWatermark` 双红;
  M3 `StreamId.Quest = 2`(与 World 撞值)⇒ `test_questKindRoutesToQuestStream_atPriorityAfterWorld` 红。
  还原后两文件 `36a3d28c…` / `fc7ccd0e…` 与备份额逐字节一致,无 `MUTATION-` 残留。

> ⚠️ **本节只证「机制」**。五条奇遇**在真关卡里能不能做完**,本 ADR 不证也不该证 ——
> 现况见 §Risks R2 与 `prologue-beat-script.md` §1.1 `N5` 行:
> **PA-01/02/03 已达标**(2026-11-14 关卡补 6 / 4 / 5 个止血草点,资源点 3 → 18 行);
> **PA-04/05 仍零目标**(`EnemySpawn` 生产零命中)。**五条里可完成三条**。

## GDD Requirements Addressed

| 需求 | 出处 | 本件如何兑现 |
|---|---|---|
| 奇遇进度必须是可重建的真源状态 | ADR-005 §一(唯一真源) | D1 + D8:落流、不折叠、不独立快照 |
| 新 Kind 必须由 `entities.yaml` 单一真源登记 | ADR-024 ① | M3 + kindgen(M2/M4) |
| 奇遇是章节骨架、不是任务链 | `design/gdd/content/campaign-arc.md:685`(「你的主线不是任务链,是生活流」)· `prologue-beat-script.md` §3.1 | D2 只定义「接取 / 完成」两态,不引入任务列表 / 日志 UI |
| 五件组件的唯一授予点 | `prologue-beat-script.md` §3.1(PA-01…PA-05) | D6:完成时 `DropSpawned` 落脚下 |
| 扩展而非重写既有有界性论证 | ADR-008 §六 | D7 |
