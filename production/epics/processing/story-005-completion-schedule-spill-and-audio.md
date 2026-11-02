# Story 005: 完成调度、起货溢出与等待期音频护栏

> **Epic**: 炮制
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-03

## Context

**GDD**: `design/gdd/processing.md`(规则八 R-18-D[甲]② 起货溢出 · 等待期可读性 · 规则十一 音频 BL-10 · UI-18 · AC-18-21/09/22/24/08)
**Requirement**: TR-processing-012(完成时点派生的执行侧)· TR-processing-001(F1 出参原样交 20/落地的下游收口)· TR-audio 族联动(44 消费 18 的 cue 发射;sfx_process_light/complete/work,总线 SFX)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-018(主): 音频架构 —— 44 只触发、无提示音铁律(AC-44-09 白名单),18 = cue 发射方 · ADR-009(次): 溢出落地 = 世界流 `DropSpawned`(身份进流/位置表现)· ADR-013(次): 42 只渲染永不持状态(进度不画、器物状态语义归 18)· ADR-005(次): 完成 tick 到达 = `ITickProvider` 驱动,禁墙钟/帧对齐
**ADR Decision Summary**: 起货溢出 R-18-D[甲]②:完成 tick 当场**本格 `DropSpawned`**,由 20 从 `Craft` 派生折叠(O-18-R4 回 20),18 不写流(落地事件发出者 = 20 结算路径),不延迟不中断;等待期可读性:42 不画进度条,器物状态语义归 18(OQ-18-9 具名渲染形态未裁 ⇒ 只断 18 侧输出:调度落点 + cue 发射,AC-18-21);炉火声床禁 sting / 倒数感(用耳朵数 tick = 失效模式,AC-44-09 白名单**看不见**的听测面独立成 AC-18-09)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 完成调度为纯 tick 逻辑(LOW);cue 发射经 `AudioCueDto` 边界(整数语义,ADR-018),44 侧 AudioMixer 6.3 post-cutoff 行为归 audio-system epic,本故事不触混音实现;[L] 听测/盲测为人工面。

**Control Manifest Rules (this layer)**:
- Required: 完成时刻 = tick 谓词命中(`tick == CompleteTickOf`)当拍调度;`sfx_process_complete` 发射与器物状态变化同源(1 tick 误差内可判起货);溢出路径 = 20 的标准入库失败 ⇒ 本格 `DropSpawned`(成品不消失、不延迟);cue 载荷 ⊆ 整数、无品质维(承 ADR-018 §六)
- Forbidden: 墙钟/`Time.*`/帧对齐作发射条件(Visual/Audio 护栏 1);18 直写世界流落地事件(发出者 = 20,18 只触发结算);进度百分比入 DTO;节奏化加密声床(= 播报剩余时间,违 AC-44-09 精神)
- Guardrail: OQ-18-9(器物状态具名渲染形态)未裁 ⇒ AC-18-21 只断 18 侧两输出,渲染形态半边留给 42;AC-18-08(零数字走查)与 AC-18-24(仅手柄全流程)执行依赖 42 面板/焦点导航落地 ⇒ 走查前保持未勾

---

## Acceptance Criteria

*From GDD `design/gdd/processing.md`, scoped to this story:*

- [x] **AC-18-21** [I]: 在场观察者,时间到达 `CompleteTick` ⇒ 1 tick 误差内器物状态变化可判起货 + `sfx_process_complete` 发射 —— 只断 18 侧输出(调度落点 + cue 发射,OQ-18-9 具名渲染形态不影响本条);禁以墙钟/帧对齐为发射条件
      > **2026-11-03 判定 = PASS(18 侧两输出)**：调度落点 = `FurnaceCompletionSchedule.CollectAt`
      > (等号判据,证伪 M1/M3 命中)；cue 发射 = `ProcessingCompletionDriver`(恰 1 条 / 不合并 /
      > 未解析不发,证伪 M4/M5 命中)；禁墙钟 = 源码面扫描(证伪 M6 命中)。
      > ⚠️ **「起货音真的响」不成立** —— 事件表无该 cue 行(见 Completion Notes ①);
      > 🔁 **溢出落地半边已于同日补齐**(`TD-processing-08` 甲案):`AsOfTick` 切点 +
      > `CraftCompletionSettler` ⇒ 完成 tick 容量不足时整批 `DropSpawned`(id 无第二铸造)。
      > **本 AC 的机器判据现已覆盖两侧**;四条 `[L]` 仍 `NOT-RUN`。
- [ ] **AC-18-09** [L]: 全部炮制音频听测收窄为 AC-44-09 白名单**看不见**者:等待期声床无节奏化加密(= 用耳朵数 tick)、无「还剩 N 秒」式边界音形。证据归档 `production/qa/evidence/processing-09-<date>.md`(镜像 AC-5-10:混入缺陷样本,分不出即过)
- [ ] **AC-18-22** [L]: 去标注盲测:含多次完整炮制的录音,受试者仅凭听觉可指出起货时刻(±1 tick);失败判据 = 报出「倒数感」或把起货声判为播报。与 44 联合验收
- [ ] **AC-18-24** [L, ADVISORY]: 仅手柄(无指针)走查选料→点火→起货全流程,每步有焦点导航路径可达。18 侧义务 = 谓词与调度设备中立(已由 AC-18-20 机器化);本条端到端佐证,主验收在 42/43(TR-concept-008)
- [ ] **AC-18-08** [L]: 一次完整炮制走查逐屏零数字(无转化率/成功率/进度百分比),成色由质地/药签措辞可读。证据归档 `production/qa/evidence/processing-08-<date>.md`(主创签核页)

---

## Implementation Notes

*Derived from ADR-018 §Decision(主)/ ADR-009 §六 扩展:*

1. 完成调度:每 tick(经 `ITickProvider` 步相位,承 tick 裁定 ③)对「在烧」谓词命中 `tick == CompleteTick` 的炉,触发 20 的结算入口(`ApplyOutput`):容量足 ⇒ 成品入 `InventoryOf`;不足 ⇒ 20 走本格 `DropSpawned`(R-18-D[甲]②,不延迟不中断)。18 提供触发与时机,**不持有落地事件写权**(发出者 = 20,同 Story 003 铸造的下游)。
2. `Craft` 载荷的 `output_instance_ids[]` 已在点火铸造 ⇒ 完成落地只引用既有 id(`DropSpawned{instance_id = 已铸 id, spawn_anchor = tool_cell, ...}` 形状对齐,承 ADR-024 载荷)—— 无第二铸造点。
3. cue 发射:`sfx_process_light`(点火)/`sfx_process_work`(等待期声床,无进度语义)/`sfx_process_complete`(起货)三 cue,经 `AudioCueDto` 边界给 44;发射条件 = tick 事件,禁墙钟(AC-18-21 判据);声床素材的「非播报性」归 44 白名单断言(AC-44-09)+ 本故事 [L] 听测。
4. 等待期可读性接缝:器物状态语义(炉盖/音色等)具名形态 = OQ-18-9 未裁 ⇒ 本故事只交「状态变化时刻 + cue 时刻」两个整数时间点给 42,不定义渲染物。
5. 溢出路径的 spy 测试:AC-18-12(点火时点)与本条(完成时点)共享 spy-sink 基建;断「成品永不消失」= 折后 `InventoryOf ∪ 地面 drop` 含全部产出实例。
6. [L] 四条(08/09/22/24)的证据落 `production/qa/evidence/processing-{08,09,22,24}-<date>.md`。
   🔁 **2026-11-03 订正(原文「目录从未建立 ⇒ 登记不擅建」作废)**:实测 `production/qa/evidence/` **早已存在** —— 397 个文件 / 12 个子目录,且已有 processing 证据(`impl-o18r6-processing-screen-2026-11-02.md` / `impl-td-processing-04-busy-count-2026-11-02.md`)。
   原句的出处也引错了:item-database story-003 说的是 `production/qa/` **目录**(为 smoke 文件),不是 `production/qa/evidence/`;且那句本身也已过期(`production/qa/` 下有大量文件)。
   ⇒ **前置阻塞不存在**,走查轮直接按上述路径落证即可。**原文不删。**

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003/004:载荷、铸造、时序谓词(本故事的输入)
- inventory-items epic:20 结算/入库/`DropSpawned` 发出本体(O-18-R4 的回写落点)
- audio-system epic:44 混音总线、白名单断言执行体(AC-44-09)、素材管线
- skeuomorphic-ui(42)epic:炮制面板、进度禁画、焦点导航、OQ-18-9 渲染形态
- ADR-012 / persistence:重放逐位与存档(Story 004 已钉)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).
🔁 **2026-11-03 由 `/qa-plan sprint` 回填**:与 `production/qa/qa-plan-sprint-24-2026-11-03.md` 对齐,
补 AC-18-21 的「恰 1 条 / 不合并 / `duration_ticks` 边界」与溢出路径的「部分容纳 ⇒ 整批落地」。*

- **AC-18-21** [I] — 完成时点调度 + cue 发射。
  - Given: 夹具炉一,`Recipe.duration_ticks = 20`、`start_tick = 100`;tick 由 `ITickProvider` 逐拍推进;`AudioCueDto` spy。
  - When: `tick` 推进到 `CompleteTick = start_tick + duration_ticks = 120`(F-18.3)。
  - Then: ① **当拍**(误差 0 —— 判定是等号 `tick == CompleteTickOf`,不是区间)输出器物状态变化时间点;
    ② `sfx_process_complete` **恰 1 条**(重复发射 ⇒ 红);
    ③ **发射条件无墙钟符号** —— 复用 AC-18-13 的符号扫描件,断言 `Time.*` / 帧计数不参与发射判定。
  - Edge cases: 存档恢复后重放到同一 `start_tick` ⇒ 同 tick 同结果(重放幂等)·
    两玩家同 tick 完成 ⇒ 各发 **1 条** cue,**不得合并**· `duration_ticks` 边界 `= 0`(当拍即完成)与 `= 1`。
- **溢出路径(R-18-D[甲]②)** — 完成时点容量不足。
  - Given: 点火已铸 `output_instance_ids[]`;20 剩余容量 < 产出总体重。
  - When: 完成 tick 触发 20 的结算入口。
  - Then: ① 成品以 `DropSpawned{instance_id = 点火已铸 id, spawn_anchor = tool_cell}` 落地 —— **无第二铸造点**;
    ② fold 后 `InventoryOf ∪ 地面 drop` **含全部产出实例**(不静默吞、不延迟);
    ③ **无「半成品」中间态**。
  - Edge cases: **部分容纳 ⇒ 整批落地**(与 20 规则六「整件拒绝」同纪律,不做拆分入包)·
    落地后玩家可拾(承 inventory 拾取链)。
  - ⚠️ **禁借绿**:夹具必须走**真 20 结算 + 真 fold**,不得用 spy 顶替 fold(F1 型假绿)。
- **AC-18-09 / 22** [L] — 声床听测 + 去标注盲测。
  - Given: 44 素材接入后的录音样本(**含故意混入的加密声床缺陷样例**,镜像 AC-5-10)。
  - When: 人工听测 / 受试者**仅凭听觉**报起货时刻。
  - Then: 无倒数感/播报判读;起货时刻可指出(**±1 tick = 50 ms**);证据 + 与 44 联合签核。
  - Edge cases: 走查未跑 ⇒ **未勾**,**禁借 AC-18-21 的机器绿**。
- **AC-18-24** [L, ADVISORY] — 仅手柄全流程。
  - Given: 手柄独占输入构建(42 焦点导航落地后)。
  - When: 选料→点火→起货走查。
  - Then: 每步**焦点导航可达**、**无指针依赖**;ADVISORY 级(主验收在 42/43)。
  - Edge cases: 药箱模态与炉交互的**焦点单栈门**(ADR-013)交接点无死锁。
- **AC-18-08** [L] — 逐屏零数字走查。
  - Given: 42 面板落地后的完整炮制操作序列。
  - When: 逐屏记录。
  - Then: **零**转化率/成功率/进度百分比;成色由**质地/药签措辞**可读;主创签核页归档。
  - Edge cases: 42 未交付 ⇒ 保持 EXTERNAL 标记未勾(机制侧供数无数字已由 Story 002 保证,**不代走查**)。

> 🔁 **2026-11-03 订正**:原写「归档目录 `production/qa/evidence/` 从未建立 ⇒ 登记不擅建」——
> **实测该目录早已存在**(397 文件 / 12 子目录,且已有 processing 证据两件)⇒ **阻塞不存在**。
> 落点 = `production/qa/evidence/processing-{08,09,22,24}-<date>.md`(顶层,与既有 processing 证据同层)。
> 原文保留以记录该误判。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `unity/Assets/Tests/PlayMode/process_completion_and_spill_test.cs` — must exist and pass
- [L]: `production/qa/evidence/processing-{08,09,22,24}-<date>.md`(走查/听测轮,与 44/42 联合)

**Status**: [x] Integration 件已建并绿（13 测，2026-11-03）；[ ] 四条 `[L]` **未跑**（见下方残余）

**反证记录（Falsifiability Gate · 6 变异全部命中）**：

| 命中形态 | 主要断言 | 反证：停用什么 ⇒ 哪条转红 |
|---|---|---|
| 阈值边界（等号 vs 区间） | `CollectAt` 的 `CompleteTick == tick` | M1 改 `!=` → `>` ⇒ `completionHitsExactlyOnEqualityTick` 等 **3 条红** |
| 一次性 / 幂等 | 每 actor 至多一条 | M2 改成后者覆盖 ⇒ `sameActorTwoFurnaces_keepsEarliestByOrdering` 红 |
| 定义域收窄（前缀口径） | 前缀只认 envelope tick | M3 改读载荷 `StartTick` ⇒ `prefixUsesEnvelopeTick_notPayloadStartTick` 红 |
| 一次性 / 幂等 | tick 严格递增守卫 | M4 改 `<` ⇒ `repeatedTickEdge_throwsInsteadOfDoubleEmitting` 红 |
| 共存 / 互斥 | cue 未解析 ⇒ 绝不发 | M5 去掉 `CueResolved` 短路 ⇒ `unresolvedCue_noEmission` 红 |
| （禁项扫描） | 发射路径零墙钟符号 | M6 注入 `DateTime.UtcNow` ⇒ `emissionPath_hasNoWallClockSymbols` 红 |

还原判据 = **SHA-256 逐字节**（`85112b98…` / `07cc2299…`，改前 = 还原后）。
原件：`production/qa/evidence/impl-processing-005-completion-schedule-2026-11-03.md`。

---

## Dependencies

- Depends on: Story 004(`CompleteTick` 谓词),Story 003(已铸 output ids),inventory-items Story 001/004(20 结算与 `DropSpawned` 发出),audio-system epic(cue 消费端,已 Complete 件直连),42(走查三条的呈现前置,EXTERNAL 半边)
- Unlocks: 炮制 epic 收尾走查轮;OQ-18-9 裁定后的渲染形态故事(另开);O-18-R4 回写完成确认

---

## Completion Notes

**Completed**: 2026-11-03
**Criteria**: **1/5 passing** —— `AC-18-21` [I] PASS(18 侧);`AC-18-09` / `18-22` / `18-24` / `18-08` **NOT-RUN**(四条 `[L]`,人听 / 人走)
**Verdict**: **COMPLETE WITH NOTES**(四条 `[L]` 如实标 NOT-RUN,**未借机器绿**)

### 交付

| 件 | 落点 |
|---|---|
| 完成调度(纯函数) | `unity/Assets/Sim/Processing/FurnaceCompletionSchedule.cs`(`FurnaceCompletion` + `CollectAt`) |
| 发射侧驱动 | `unity/Assets/Gameplay.Boot/ProcessingCompletionDriver.cs` |
| 装配 | `CompositionRoot.cs`(构造)· `CompositionRootServices.cs`(可空属性)· `BootRoot.cs`(tick 边沿) |
| 夹具 | `unity/Assets/Tests/PlayMode/process_completion_and_spill_test.cs`(13 测) |
| 既有扫描面加宽 | `furnace_timing_no_process_state_test.cs` 的 `FurnaceSourceFiles` / `ProcessingTypeNames` |

### 三条判据口径(逐条有反证)

1. **等号,不是区间** —— `CompleteTick == tick`。
2. **前缀与定序只用 envelope tick**(承 `FurnaceState` 的 2026-10-26 W1 订正)。
3. **每 actor 至多一条**(全序键最小者胜;合法流不可达,防御口径)。

### ⚠️ 两处必须如实登记的残余

**① 生产上不会出声。** `assets/data/audio_events.json` 的 19 行 cue 里**没有**
`sfx_process_complete`(`grep -c "sfx_process" assets/data/audio_events.json` ⇒ `0`)
⇒ 装配期 `TryResolveCue` 失败 ⇒ 驱动拿 `NoOrdinal` ⇒ `OnTickEdge` 首行返回,**不扫描、不发**。
本件交付的是**发射时机这一侧**(判据 / 条数 / 无墙钟),**不是音效**。加表行那一轮无需改代码即生效。
**`sfx_process_light`(起炉)/ `sfx_process_work`(声床)本件未接** —— 无 AC 钉它们,
且声床的 `BeginLoop`/`EndLoop` 生命周期要跟素材一起做,登记到 AC-18-09/22 那一轮。

**② 溢出落地的下游没有入口(新债 `TD-processing-08`)。** `AC-18-21` 的溢出半边要求
「完成 tick 容量不足 ⇒ 本格 `DropSpawned`」;实测 **20 侧不存在「完成时点的结算入口」** ——
`InventoryFold.ApplyCraftOutputs`(`:851-889`)在**折叠 `Craft` 时**就把产出建进炮制者名下,
与完成 tick 无关 ⇒ 产出在**点火那刻**已「入包」,溢出路径不可达。
⇒ 本件的溢出交付只到 18 侧:原样携带点火已铸的 `OutputInstanceIds` + `ToolCell`,
并断「调度不可能自铸 id」(结构面)。**落地半边记 NOT-RUN,不得借绿。**

> 🔁 **2026-11-03 同日结清(`TD-processing-08` 实现轮,用户裁定取甲案)**:
> `InventoryFoldContext` 增必填 `AsOfTick`(`Craft` 产出只在 `AsOfTick ≥ CompleteTick` 时材化),
> 新增 20 侧 `CraftCompletionSettler`(完成 tick 判容量,装不下 ⇒ 本批**整批** `DropSpawned`,
> id 原样引用点火已铸 —— 无第二铸造点)。**本条残余已消**,`AC-18-21` 的溢出半边有了机器载体:
> `unity/Assets/Tests/EditMode/Inventory/craft_completion_settler_test.cs`(8 测,证伪 6/6 命中)。
> 原件 = `production/qa/evidence/impl-td-processing-08-craft-completion-settlement-2026-11-03.md`。
> ⚠️ **本件仍维持 `COMPLETE WITH NOTES`** —— 四条 `[L]` 仍 `NOT-RUN`,与溢出落地无关。

> 用 spy 顶替 fold 写绿夹具 = F1 型假绿(断言的是测试自己写的谓词)⇒ **刻意不写**。

### 四条 `[L]` 的状态

| AC | 状态 | 阻塞 |
|---|---|---|
| `AC-18-09` 等待期声床听测 | NOT-RUN | 表里无 cue 行 ⇒ 无素材可听;且需人听 |
| `AC-18-22` 去标注盲测 | NOT-RUN | 同上 + 需 playtest 受试者(项目 advisory,由用户定) |
| `AC-18-24` 仅手柄全流程 | NOT-RUN | 依赖 42 面板与焦点导航落地 |
| `AC-18-08` 逐屏零数字走查 | NOT-RUN | 依赖 42 面板落地 |

### 与 QA 计划的一处措辞冲突(已按代码事实处理)

qa-plan 写「`duration_ticks` 边界 `= 0`(当拍即完成)」,而 `duration_ticks ≥ 1` 是 21a 的硬约束
(`FurnaceState` 同款先验抛 `InvalidOperationException`)⇒ 0 是**坏数据**,不是「立即完成」。
夹具 `test_ac1821_durationZero_throwsInsteadOfCompletingImmediately` 断的是 **fail-closed**;
若将来 21a 允许 0,应先改 21a 与本条判据。
