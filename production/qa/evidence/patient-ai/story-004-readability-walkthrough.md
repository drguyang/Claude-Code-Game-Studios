# SIGN-OFF —— patient-ai(13)story-004 `[L]` 五档行为可读性走查

> **对象**: `design/gdd/patient-ai.md` **§Visual/Audio 一/二** + **UI Requirements 一**(「看姿态·听呼吸/咳呻,无 UI 读出」)——
> 「Idle/Seeking/Bedridden/InTreatment/Terminal 五档玩家可从姿态+音**无 UI** 读出」
> **story**: `production/epics/patient-ai/story-004-reconstruction-online-and-write-path-to-10.md`(Test Evidence 第 3 项)
> **真身夹具**: `presentation_projection_test.cs`(cue / 呼吸 / 姿态档)+ `spatial_behavior_test.cs`(走格 / 站定)+ `behavior_map_test.cs`(分档节律)
> **日期**: 2026-10-05 · **修订**: 2026-10-07(补做评审修复轮,见 §〇)· **轮次**: 单轮(承用户「评审只做一轮」)

---

## 〇、本文件的性质与 2026-10-07 修订

**story-004 AC 原文**:
> Idle/Seeking/Bedridden/InTreatment/Terminal 五档玩家可从姿态+音**无 UI** 读出。

本文件**不是**「五档已达成」的宣告。它是**分层落点登记**:
- 13 侧(本 story 交付面)**能判且已判**的部分 = 数据的**结构前提**;
- 依赖 44 素材 / 42 冗余通道的部分 = **BLOCKED-BY 外部项,NOT-RUN,不记绿**。

⚠️ **判据面与被测面必须错位声明**:13 生产 `CueDispatch`(整数语义 cue + 呼吸层动作 + 姿态档),
**不生产**「可见对应物」—— 那是 42 的呈现职责(ADR-013 §9 C3:呈现层只渲染)。
故凡 AC 原文要求「对玩家**可见/可闻**」者,其**达成**在 13 之外。

⚠️ **修订记录(2026-10-07 补做评审 · QA M8)**:原稿三处失实,本轮逐处订正 ——
① **档 1 判据写反** —— 原写「Idle ⇒ 无呼吸层 cue」,生产恰恰相反(活着即 `BreathAction.Begin`,Idle 在默认起循环内);
② **五档 ✅ 落点错引** —— 原全指 `test_ac13b3_reconstruction_*`(只断言 run1==run2 决策相等,
无 cue / Breath / 姿态断言),真实承载在 `presentation_projection_test` / `spatial_behavior_test`;
③ **无签署行** —— 本轮补 §四。

---

## 一、五档逐档判定

### 档 1:Idle(在家休养)

| 半边 | 判据 | 落点(真身) | 状态 |
|---|---|---|---|
| 「Idle 活着即有呼吸层」 | `Idle` ⇒ `BreathAction.Begin`(**默认起,非静默** —— 原稿「无呼吸层 cue」为误,已订正) | `presentation_projection_test::test_ac13f1_structuralHalf_breathLayerIsDefaultOn` · `test_ac13f2_structuralHalf_breathIsIndependentOfMovement` | ✅ 结构前提绿(13 职责内) |
| 「咳嗽/呻吟按病程节律发放」 | 一次性 cue 节律 = `SymptomTier` 阶梯(非连续、非随机) | `presentation_projection_test::test_ac13d6_piecewiseConstant_observableThroughDecide` · `test_ac13d6_tierOrdering_isHardConstraint` | ✅ 结构前提绿 |
| 「Idle 与 Seeking 可辨」 | 步态 / 巡游姿态差 | **42 呈现接线** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 2:Seeking(求医途中)

| 半边 | 判据 | 落点(真身) | 状态 |
|---|---|---|---|
| 「Seeking 真在走」 | 五合取全真 ⇒ `Moving` 真,且逐 tick 真走格 | `spatial_behavior_test::test_ac13b2_allNonMovingInputs_freezeAcc`(全真 ⇒ Moving 真)· `test_tc1_threeQuarterSpeed_walksThreeCellsInFourTicks` | ✅ 结构前提绿 |
| 「到医馆即停」 | `AtClinic` = **格成员判定**(非路径游标) | `spatial_behavior_test::test_f13_7_atClinic_isCellMembership_notPathCursor`(+ 单格路径 / 终点馆外两变体) | ✅ 结构前提绿 |
| 「Seeking 有呼吸层」 | `Seeking` ⇒ `BreathAction.Begin` | `test_ac13f1_structuralHalf_breathLayerIsDefaultOn`(循环含 Seeking) | ✅ 结构前提绿 |
| 「Seeking 姿态可辨」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 3:Bedridden(倒地)

| 半边 | 判据 | 落点(真身) | 状态 |
|---|---|---|---|
| 「Bedridden 不动」 | `Moving` 假 ⇒ 零位移、acc 冻结 | `spatial_behavior_test::test_ac13b2_bedridden_doesNotMove_andAccUnchanged` · `test_ac13b2_allNonMovingInputs_freezeAcc` | ✅ 结构前提绿 |
| 「Bedridden 活着仍有呼吸层」 | `Bedridden` ⇒ `BreathAction.Begin`(倒地 ≠ 无呼吸) | `test_ac13f1_structuralHalf_breathLayerIsDefaultOn`(循环含 Bedridden) | ✅ 结构前提绿 |
| 「昏迷 vs 死亡可辨」 | 姿态 + 呼吸**组合**:昏迷持续呼吸,终局呼吸收 —— 唯一判据是呼吸层 | `presentation_projection_test::test_ac13d3_comaKeepsBreathing_terminalDoesNot` · `test_ac13d3_comaAndDeceased_differOnlyInBreathLayer` | ✅ 结构前提绿(GDD §Visual/Audio 一 末行的组合语义) |
| 「Bedridden 姿态可辨(蜷缩/半卧)」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 4:InTreatment(会诊中)

| 半边 | 判据 | 落点(真身) | 状态 |
|---|---|---|---|
| 「会诊中站定」 | `Session = InTreatment` ⇒ `Moving` 假 | `spatial_behavior_test::test_ac13b2_allNonMovingInputs_freezeAcc` | ✅ 结构前提绿 |
| 「会诊态优先呈现」 | `ViewState` 优先级:InTreatment 压过 Collapsed/AwaitingCare | `presentation_projection_test::test_ac13c5_priority_inTreatmentBeatsCollapsedAndAwaitingCare` · `test_ac13c5_viewState_truthTable_overAllCombinations` | ✅ 结构前提绿 |
| 「InTreatment 姿态可辨」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 5:Terminal(终态)

| 半边 | 判据 | 落点(真身) | 状态 |
|---|---|---|---|
| 「终局呼吸收」 | `Terminal.Latched` ⇒ `BreathAction.End` + 零一次性 cue + `PostureTier = 0` | `presentation_projection_test::test_ac13d3_terminal_endsBreathAndEmitsNoOneShot` | ✅ 结构前提绿 |
| 「终局不被播报」 | 终局调度静默:无 End sting、强度 0(承 ADR-018 无提示音铁律) | `test_ac13d3_terminalDispatch_isSilent_noEndSting` | ✅ 结构前提绿 |
| 「终局闩锁单调」 | 置位后行为恒定,position 回升不复活 | `test_ac13d3_terminal_latchesRegardlessOfBehaviorOrPosition` | ✅ 结构前提绿 |
| 「Terminal 姿态落最静止档」 | 姿态映射 = 42 呈现(13 侧只给 `PostureTier = 0` 档位) | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

---

## 二、NOT-RUN 汇总(禁借绿)

| # | 对象 | 归属 | 解除条件 |
|---|---|---|---|
| NR-S4-1 | 五档「姿态可辨」(可见对应物) | **42 冗余通道** | 42 呈现接线 + 非色相设计就位 |
| NR-S4-2 | 五档「音可闻」(素材/空间化) | **44 空间化** | 44 素材/混音 + 距离衰减就位 |
| NR-S4-3 | 「音色按病程档可辨」的**素材映射** | **44 + ADR-014 烘焙**(AC-13-D1 表内容) | `ai_patient.json` 表内容落盘 + 44 签署(GDD §四:13 只保证分档正确,音色正确归 44) |

> **本文件不记 NR-S4-1/2/3 为绿。** story-004 的 `[L]` 项**整体状态 = 13 侧结构前提已闭 + 三项 BLOCKED-BY 外部**。

---

## 三、声明

- 13 侧交付面**结构前提**已闭(五档决策值 + 呼吸层动作 + 姿态档位 + cue 节律)—— 由上表**真身夹具**断言,绿。
- **AC 原文的达成**(玩家真能看见/听见)依赖 42 / 44 未就位的外部件 ⇒ **NOT-RUN**。
- 「引用字符串就位」**不构成**任何记绿理由(承「引用 ≠ 验收」)。
- 档 1 判据与五档落点的错引已于 2026-10-07 评审修复轮订正(本文件修订记录见 §〇)。

---

## 四、签署

| 角色 | 对象 | 状态 |
|---|---|---|
| 13 侧结构前提(本表 ✅ 行) | 夹具绿(复跑见证据 `patientai-fix-2026-10-07.xml`) | ✅ 已验 |
| [L] 走查整体(玩家可读性) | 依赖 NR-S4-1/2/3 解除 | ⏳ **待 42/44 就位后的走查轮签署 —— 本文件不代签** |
| 补做评审修复轮核验 | `production/qa/evidence/review-patient-ai-story-004-2026-10-07.md`(QA M8) | ✅ 本轮 |
