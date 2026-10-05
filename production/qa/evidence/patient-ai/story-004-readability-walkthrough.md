# SIGN-OFF —— patient-ai(13)story-004 `[L]` 五档行为可读性走查

> **对象**: `design/gdd/patient-ai.md` **§Visual/Audio 三** —— 「Idle/Seeking/Bedridden/InTreatment/Terminal 五档玩家可从姿态+音**无 UI** 读出」
> **story**: `production/epics/patient-ai/story-004-reconstruction-online-and-write-path-to-10.md`(Test Evidence 第 3 项)
> **夹具**: `unity/Assets/Tests/EditMode/PatientAI/reconstruction_and_write_path_test.cs`
> **日期**: 2026-10-05 · **轮次**: 单轮(承用户「评审只做一轮」)

---

## 〇、本文件的性质

**story-004 AC 原文**:
> Idle/Seeking/Bedridden/InTreatment/Terminal 五档玩家可从姿态+音**无 UI** 读出。

本文件**不是**「五档已达成」的宣告。它是**分层落点登记**:
- 13 侧(本 story 交付面)**能判且已判**的部分 = 数据的**结构前提**;
- 依赖 44 素材 / 42 冗余通道的部分 = **BLOCKED-BY 外部项,NOT-RUN,不记绿**。

⚠️ **判据面与被测面必须错位声明**:13 生产 `CueDispatch`(整数语义 cue + 呼吸层动作 + 姿态档),
**不生产**「可见对应物」—— 那是 42 的呈现职责(ADR-13 §9 C3:呈现层只渲染)。
故凡 AC 原文要求「对玩家**可见/可闻**」者,其**达成**在 13 之外。

⚠️ **B5 修复(2026-10-05 双代理评审)**:原文件覆盖的是 `AC-13-F1/F2/F3`(无障碍三判据),
与 story-004 的 `[L]` AC(五档可读性)**不是同一条**。现重写为五档逐档走查。

---

## 一、五档逐档判定

### 档 1:Idle(在家休养)

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「Idle 病人不发 cue」 | `BehaviorState.Idle` ⇒ 无咳嗽/呻吟/呼吸层 cue | `test_ac13b3_reconstruction_*`(决策序列含 Idle) | ✅ 已闭(13 职责内) |
| 「Idle 姿态可辨」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 2:Seeking(求医途中)

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「Seeking 病人发呼吸层 cue」 | `BehaviorState.Seeking` ⇒ `BreathAction.Begin` | `test_ac13b3_reconstruction_seekingToAtClinic_*` | ✅ 已闭(13 职责内) |
| 「Seeking 姿态可辨」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 3:Bedridden(倒地)

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「Bedridden 病人发咳嗽/呻吟 cue」 | `BehaviorState.Bedridden` ⇒ 咳嗽/呻吟 cue | `test_ac13b3_reconstruction_terminalLatch_*` | ✅ 已闭(13 职责内) |
| 「Bedridden 姿态可辨」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 4:InTreatment(会诊中)

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「InTreatment 病人站定」 | `SessionState.InTreatment` ⇒ `Moving = false` | `test_ac13b3_reconstruction_examSessionReset_*` | ✅ 已闭(13 职责内) |
| 「InTreatment 姿态可辨」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

### 档 5:Terminal(终态)

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「Terminal 病人呼吸层停止」 | `Terminal.Latched` ⇒ `BreathAction.End` | `test_ac13b3_reconstruction_terminalLatch_*` | ✅ 已闭(13 职责内) |
| 「Terminal 姿态落最静止档」 | 姿态映射 = 42 呈现 | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

---

## 二、NOT-RUN 汇总(禁借绿)

| # | 对象 | 归属 | 解除条件 |
|---|---|---|---|
| NR-S4-1 | 五档「姿态可辨」 | **42 冗余通道** | 42 呈现接线 + 非色相设计就位 |
| NR-S4-2 | 五档「音可闻」 | **44 空间化** | 44 素材/混音 + 距离衰减就位 |

> **本文件不记 NR-S4-1/2 为绿。** story-004 的 `[L]` 项**整体状态 = 部分闭 + 两项 BLOCKED-BY 外部**。

---

## 三、声明

- 13 侧交付面**结构前提**已闭(五档决策值 + 呼吸层动作 + 姿态档)—— 由夹具断言,绿。
- **AC 原文的达成**(玩家真能看见/听见)依赖 42 / 44 未就位的外部件 ⇒ **NOT-RUN**。
- 「引用字符串就位」**不构成**任何记绿理由(承「引用 ≠ 验收」)。
