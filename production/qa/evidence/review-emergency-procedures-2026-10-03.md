# 独立代码评审 —— emergency-procedures(系统 10 急救动作)

> **评审对象**: `production/epics/emergency-procedures/`(7 story:001–007)
> **评审日期**: 2026-10-03
> **评审基线**: HEAD `7285264`(工作树 clean)
> **评审员**: 独立评审轮(unity-specialist)

## 🔍 独立复核批注(2026-10-03 · 由主会话复核,非 agent 自述)

复核人**逐条实测**了本报告的关键结论:

| 报告条目 | 复核结果 |
|---|---|
| **N-1** AC-10-02 断言恒真 | ✅ **成立** —— `reading_contract_test.cs:174` 取 `Sim.Contracts` 程序集,断言其名 `Contains("Input")` **恒 false** ⇒ **零扫描、恒过** |
| **N-2** header `Seq` 未发号 | ❌ **不成立(agent 误报)** —— 传 `0` 是**全库既定占位约定**:`EventStream.cs:89-92` 明写「如果事件没有 Seq,则发号」并给 `Seq == 0` 赋值;`PoiStateMachine.cs:129` 同样传 0。**header Seq 由流发号,非缺陷** |
| story-007 手搓点确已真修 | ✅ 成立(九字段逐项对 · 豁免表空 · 重复 struct 已删) |
| A8 勘误验算 | ✅ 成立(`32768×16385=536903680`,rem 恰为半) |

⚠️ **复核纠正了 agent 的一处误报(N-2)** —— 该条**不得**作为「缺陷」计入前置。

## ⚠️ 声明(必读)

**本报告评的是「补做时点的代码」,不是「追认原判定」。**

6 个 story(001–006)各有一次「双代理评审修复」提交(`2c95835` / `bea472a` / `2d74834` /
`eac7e12` / `dcc6676` / `1959167`),**但评审报告原件从未落盘、已不可得**。
story-007(`6563d54`)的 Completion Notes 自陈「Code Review: 尚无独立评审件(归后续轮)」。

因此本报告**不是补录、不是背书**,是**在 2026-10-03 对当前代码做的一次全新评审**。
凡本报告写「已交付」,指的是**我在本次评审中亲自复现的当下事实**,与任何原 commit message 自述无关。

**验证方法**:逐条 `grep`/`sed`/读源码 + 算术独立验算。**未实跑 Unity 测试套件**(本环境无 batch 权限确认);
凡「测试通过」类结论均来自**读测试源码**而非**执行结果** —— 相关条目已逐条标注证据强度。

---

## 结论摘要表

| Story | 主题 | 本报告判决 | 依据强度 |
|---|---|---|---|
| 001 | EmergencyReading 读数与直读通道契约 | ⚠️ **部分交付** —— 类型域断言真;**AC-10-02/03(BLOCKING)判据空转** | 源码已读 |
| 002 | 动作表/熟练度表与 result_mul 烘焙 | ⚠️ **部分** —— DC-1…DC-4/DC-6 机制真;**DC-5 未实现校验(仅返回字符串数组)** | 源码已读(未读 bake 测试全文) |
| 003 | Judge 三扇门定点纯函数 | ⚠️ **部分** —— 三门映射真;**`SkillMul` 死代码 + JITTER 用 C# 裸 `/`(违 Forbidden)** | 源码已读 |
| 004 | Aggregate / 可靠上行 / 主机落流 | ⚠️ **部分** —— 载荷填充真;**`Seq` 两处均未发号**;可靠通道仅类型断言 | 源码已读(未读 host 测试全文) |
| 005 | 模态期:跳过/中止/档位/压制 | ⚠️ **部分** —— 静态求值器真;**非状态机;`holdMode` 参数被忽略(AC-10-24 空转)** | 源码已读(未读 modal 测试全文) |
| 006 | 手感、预表现与键鼠回退 | ✅ **自动化半边已交付** · ⚠️ 3 项 NOT-RUN **真身 = `Assert.Ignore`(确已 skip)** | 源码已读 |
| 007 | applied 载荷九字段 + 结构性收口 | ✅ **已交付**(唯一「真修好」的一条)—— 但 `Seq` 仍占位 | 源码已读 + 算术验算 |

**新发现 8 条**(详见 §4),其中 **2 条严重**(空转判据 · `Seq` 未发号)。

---

## 逐条详节

### Story 001 —— EmergencyReading 读数与直读通道契约

**原判定**: `Complete ✅ 2026-10-02 (测试 5/5 通过)`(story 头)/ `11/11 passed`(Test Evidence)
**修复落点**: `unity/Assets/Tests/EditMode/EmergencyProcedures/reading_contract_test.cs`

| 项 | 实测证据 | 判决 |
|---|---|---|
| AC-10-01 类型反射(零 float/double) | `reading_contract_test.cs:20-38` 逐字段断言 ∈ {int,long,int[]};`EmergencyReading` 定义在 `Sim.Contracts`(测试 `:147-152` 断言程序集名 = `Sim.Contracts`) | ✅ 真 |
| F-10.1 死区 / edges / 单调 / 域 | `:84-143` 四组边界断言,含负例 | ✅ 真 |
| **AC-10-02(BLOCKING) 3 侧零 Judge/JudgeResult/SimEvent 引用** | `:170-177` 仅断言 `inputAssembly.GetName().Name.Contains("Input") == false`,而 `inputAssembly = typeof(EmergencyReading).Assembly` = **`Sim.Contracts`** —— 该名**永不含 "Input"** ⇒ **恒真,零扫描**。**无 IL/Cecil 扫描** | 🔴 **判据空转** |
| **AC-10-03(BLOCKING) 零浮点字面量(操作码级 `ldc.r4/r8`)** | `:154-166` `test_noFloatLiterals_inContract` 与 `:20-38` **逐字重复**(仅重查字段类型),**无任何操作码/IL 检查** | 🔴 **判据空转** |

**复现命令**:
```
sed -n '168,177p' unity/Assets/Tests/EditMode/EmergencyProcedures/reading_contract_test.cs
```
**文件内部不一致**:story-001 头写「5/5」,Test Evidence 写「11/11」(实际测试数 = 11)。

---

### Story 002 —— 动作表 / 熟练度表 / result_mul 烘焙

**原判定**: `Complete ✅ 2026-10-02 (21/21)`
**修复落点**: `unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs`

| 项 | 实测证据 | 判决 |
|---|---|---|
| DC-1 `half_life_ticks ≥ 1` | `EmergencyAction.cs:71-74` `ValidateHalfLifeTicks` 真实现 | ✅ 真 |
| DC-2 `1 ≤ mag_threshold ≤ MAG_MAX` | `:77-80` 真实现 | ✅ 真 |
| DC-3 `jitter_relax_mul ≥ MUL_ONE` | `:83-86` 真实现 | ✅ 真 |
| DC-4 `action_id` 闭集 | `:89-92` `Enum.IsDefined(typeof(EmergencyAction), id)` —— OQ-10-6 已裁归 10,枚举 `:19-23` 已存在 ⇒ **不再悬置** | ✅ 真 |
| **DC-5 Kind 白名单** | `:95-98` `GetRequiredKindWhitelist()` 仅**返回字符串数组**,**无任何构建期断言**;差集断言自陈「归 disease-simulation story 002/003」 | ⚠️ **本 story 无校验体(仅常量载体)** |
| **DC-6 `result_mul` 恰三档** | `:112-115` `ValidateResultMul` 真实现(`!= null && Length == 3`) | ✅ 真 |
| `GetResultMulTiers()` | `:118-123` 返回 `{16384, 32768, 65536}`(序数 `Missed/Weak/Applied`) | ✅ 与 GDD F-10.4 一致 |
| `MAG_CAP(L)` 形状 | `:126-131` 返回全档相同的 5 元数组(P0 效应关闭) | ✅ 形状留 |

**DC-6 编号冲突核实(任务点名)**:
```
grep -rn "DC-6" --include=*.md .   # 命中:emergency 与 prescription 各一套,互不冲突
grep -n "DC-1\|DC-5\|DC-6" design/gdd/emergency-procedures.md
```
GDD 10-DC 表 `:828-833` 中 DC-1…DC-4 连续、**DC-5 = Kind 白名单(`:833`)**、**DC-6 = result_mul(`:832`)**。
**DC-6 与 DC-5 无编号冲突** —— 二者内容与编号均唯一。✅ 任务点名的「DC-6 未与 DC-5 冲突」**成立**。
(注:`prescription-and-medication.md:735` 另有独立的 11-DC 的 DC-6,属另一系统,非冲突。)

**未验证**:`action_tables_bake_test.cs` 全文(仅读头部注释行)。「21/21」为**未验证**。

---

### Story 003 —— Judge 三扇门定点纯函数

**原判定**: `Complete ✅ 2026-10-02 (16/16)`
**修复落点**: `unity/Assets/Sim/EmergencyProcedures/JudgeEvaluator.cs`

| 项 | 实测证据 | 判决 |
|---|---|---|
| AC-10-04a `.5` 中间积 = 8193 | `JudgeEvaluator.cs:37-47` `ScaleFixed` = `product/MUL_ONE` + 余数 `≥ MUL_ONE/2` 进位 ⇒ half-away。测试 `judge_test.cs:24-30` 用 `32769×32768`(rem=32768)⇒ 16385 | ✅ 真 |
| AC-10-15 枚举非分值 | `EmergencyAction.cs:28-33` `JudgeResult` 三值枚举 | ✅ 真 |
| AC-10-20 `edges ≤ 1` 不评不抛 | `JudgeEvaluator.cs:108` `if (agg.Edges <= 1) return Applied;` | ✅ 真 |
| F-10.3 三门映射 | `:100-117` 节奏不过→Missed · 幅度不过→Missed · 稳度不过→AppliedWeak | ✅ 与 GDD F-10.3 一致 |
| **F-10.2 稳度门公式** | `:113` `long skillMul = ComputeSkillMul(ctx.Level);` **其后 `:116` `stabilityPass = jitter <= jitterMax;` 未使用 `skillMul`** ⇒ **死变量**。GDD F-10.2 要求 `JITTER × MUL_ONE ≤ JITTER_MAX × SkillMul(L)` | 🔴 **SkillMul 死代码** |
| **JITTER 舍入** | `:82` `return (sumAbsDev * MUL_ONE) / denominator;` —— **C# 裸 `/` 向零截断**,无 half-away。story-003 Control Manifest 明列 Forbidden:「C# `/` 裸用(向零截断)」 | 🔴 **违 Forbidden** |
| `test_f102_magnitudeNotScaledBySkill` | `judge_test.cs:200-216` 断言 L=0 与 L=10 同判 —— 因 `ComputeSkillMul` **恒返回 MUL_ONE 且根本未被使用**,该测**无论实现如何都会绿** | 🔴 **判据空转** |

**复现命令**:
```
sed -n '86,118p' unity/Assets/Sim/EmergencyProcedures/JudgeEvaluator.cs
```

**AC-10-04b 真身**: `judge_test.cs:255-260` = **`Assert.Ignore`**(确为 skip,非静默通过)。✅ 真 NOT-RUN。

---

### Story 004 —— Aggregate / 可靠上行 / 主机落流

**原判定**: `Complete ✅ 2026-10-02 (EditMode 10/10 + PlayMode 4/4)`
**修复落点**: `unity/Assets/Sim/EmergencyProcedures/EmergencyAttemptAggregator.cs` · `HostEmergencyProcessor.cs`

| 项 | 实测证据 | 判决 |
|---|---|---|
| F-10.5 聚合唯一入口 | `EmergencyAttemptAggregator.cs:56-71` `Aggregate` 真实现;`holdTicks = 末沿−首沿` · `edges = 长度` | ✅ 真 |
| F-10.5 平局取较小 tick | `:84-102` `MaxWithEarlierTickTieBreak` 真实现(严格大于替换 + 平局取更小 tick) | ✅ 真 |
| AC-10-06 七项齐备 | `HostEmergencyProcessor.cs:100-109` 七项逐字段填充 | ✅ 真 |
| **AC-10-06 的 `Seq` 由主机填充** | `:109` `seq: 0`(载荷)+ `:87`/`:114` 两个 `SimEvent` 构造的 **header Seq 亦硬编码 0** | 🔴 **两处 Seq 均未发号** |
| AC-10-07 不检查 treatable_by | `HostEmergencyProcessor.Process` 全文无 `treatable_by` 读取 | ✅ 真(结构性) |
| AC-10-22 零即时生效路径 | `Sim` asmdef `noEngineReferences: true` + 无 `IVitalsQuery` 写面(源码未见) | ✅ 真(未做 IL 扫描) |
| 可靠上行(AC) | 无通道选择代码,仅 Kind 存在性;自陈「丢包模拟归 45」 | ⚠️ **仅类型断言** |
| 本地=预表现 | `FeelLatencyEvaluator.cs:70-75` `PrePresentationJudge` 仅调 `Judge`,不写流 | ✅ 真 |

**`Seq` 占位影响面(任务点名,独立核实)**:
- **载荷 `Seq`** = 0(`:109`),由 `test_ac1039_seqIsExplicitlyPlaceholder`(`applied_payload_settlement_test.cs:131-140`)显式钉死。
- **`SimEvent` header `Seq`** = 0(`:87` 与 `:114`)—— **任务未点名,但更严重**:ADR-006 的「主机唯一 Append + Seq 发号」在本处理器**完全未实现**。
- 影响面:① 任何按载荷 `Seq` 或 header `Seq` 去重/排序的下游(9 的 AC-15 五元组去重、45 的 `ReorderBuffer`)在 P0 单机路径拿到**恒 0**;② 事件全序键 `(Tick, StreamPriority, Patient, Seq)`(ADR-008)中 `Seq` 分量恒 0 ⇒ 同 tick 同 Patient 的两次动作**不可全序区分**。
- **诚实性**:story-007 Completion Notes 已如实登记载荷 `Seq` 占位(裁定 A=丙),**不构成借绿**;但 **header Seq 占位未被任何文档登记** —— 见 §4 新发现 N-2。

**未验证**:`host_authority_test.cs` 全文(仅确认含 2 `[UnityTest]` + 2 `[Test]` = 4 例)。

---

### Story 005 —— 模态期:跳过 / 中止 / 档位意图 / 输入压制

**原判定**: `Complete ✅ 2026-10-02 (20/20)`
**修复落点**: `unity/Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs`

| 项 | 实测证据 | 判决 |
|---|---|---|
| AC-10-10 跳过 = AppliedWeak / 开关 ON = Applied | `ModalPhaseEvaluator.cs:58-76` `Skip` 真实现 | ✅ 真 |
| AC-10-10b 三档偏序 | `:62` Skip potency = MUL_ONE/2(0.5);`:111` Missed = MUL_ONE/4(0.25) | ✅ 真 |
| AC-10-18 中止零事件零 Attempt | `:126-160` `Abort` 达阈值 ⇒ `eventCount:0, attemptCount:0` | ✅ 真 |
| AC-10-13 MotorSuppressed 三出口配对 | `:73,98,110,155` 各出口 `motorSuppressed`/`motorSuppressedSet` 为**返回结构体字段**;无真实置/清生命周期 | ⚠️ **仅数据形状,非生命周期** |
| **AC-10-24 toggle 等价** | `:82` `Complete(bool accessibilityOn, int holdMode = 0)` —— **函数体完全不读 `holdMode` 也不读 `accessibilityOn`**(`:85-99` 硬编码)⇒ 两模式**必然**返回相同值 | 🔴 **判据空转** |
| 状态机 `Idle→Armed→Complete/Abort/Skip` | 无状态机实现,仅 4 个**静态纯函数返回硬编码 struct** | ⚠️ **形状交付,机制未实现** |

**复现命令**:
```
sed -n '82,100p' unity/Assets/Sim/EmergencyProcedures/ModalPhaseEvaluator.cs
```

**未验证**:`modal_phase_test.cs` 全文。AC-10-12(手柄走查)按 story 自陈 NOT-RUN,本轮**未验证其真身**。

---

### Story 006 —— 手感 / 预表现 / 键鼠回退(含 3 项 NOT-RUN)

**原判定**: `Complete ✅ 2026-10-02 (10/10 通过 + 3 NOT-RUN)`
**修复落点**: `unity/Assets/Sim/EmergencyProcedures/FeelLatencyEvaluator.cs` · `Tests/EditMode/EmergencyProcedures/feel_latency_test.cs`

| 项 | 实测证据 | 判决 |
|---|---|---|
| AC-10-21 键鼠回退 | `FeelLatencyEvaluator.cs:30-46` `CreateKeyboardFallbackReading`(magnitude=MAG_MAX)+ `GetKeyboardFallbackCause()=1(降级)` | ✅ 真 |
| AC-10-09 DTO 洁净 | 呈现 DTO 未在本 story 文件内;自陈「PresentationDtoGuard 递归」 | 未验证(跨 epic) |
| AC-10-17 零档位指示 | `:60-64` `GetAudioCueForResult(result)` **忽略 `result` 恒返回 `"action_feedback"`** | ⚠️ 真(但属恒真式实现,非白名单断言) |
| 预表现不写流 | `:70-75` 仅调 `Judge` | ✅ 真 |

**3 项 NOT-RUN 真身核实(任务点名)**:
```
grep -n "Assert.Ignore" unity/Assets/Tests/EditMode/EmergencyProcedures/feel_latency_test.cs
```
- `feel_latency_test.cs:183` `Assert.Ignore("NOT-RUN: AC-10-08 L_input 实测待 OQ-10-12 原型门")`
- `feel_latency_test.cs:190` `Assert.Ignore("NOT-RUN: AC-10-19 联机 L_input 实测待 45 网络层")`
- `feel_latency_test.cs:197` `Assert.Ignore("NOT-RUN: AC-10-23 录屏走查待桌面调试轮")`

⇒ **真身 = `Assert.Ignore`(NUnit 会记为 skipped,不记为 passed)**。✅ **非「已跑」,NOT-RUN 诚实。**
**但注意**:这 3 项位于 **EditMode**(story-006 的 Test Evidence 却指向 **PlayMode** 的 `feel_latency_test.cs`)——
**文件位置与 story 声明不符**,见 §4 新发现 N-4。

---

### Story 007 —— applied 载荷九字段 + 结构性收口(2026-10-03 新增)

**原判定**: `Complete ✅ 2026-10-03 (17/17)` —— 但 story 文件 Test Evidence 仍写 `[ ] Pending`(未同步)
**修复落点**: `HostEmergencyProcessor.cs:100-117`(九字段)· `EmergencyAction.cs:112-115`(DC-6)·
`applied_payload_settlement_test.cs`(17 例)

#### 🔴 核心问题:story-007 前的「手搓点」现在是否真的修好了?

**结论:是。九字段真的齐备了(载荷 `Seq` 除外)。** 逐字段实测:

| # | 字段 | 修复前(手搓) | 修复后(`HostEmergencyProcessor.cs`) | 判决 |
|---|---|---|---|---|
| 1 | `Tick` | ❌ 未载 | `:101` `tick: tick` | ✅ |
| 2 | `TreatmentId` | `attempt.Action` | `:102` `action.ActionId` | ✅ 改为查表 |
| 3 | `ActorId` | `(int)result` 🔴 | `:103` `attempt.ActorId` | ✅ **真修** |
| 4 | `Polarity` | `0` 恒零 🔴 | `:104` `action.Polarity` | ✅ **真修** |
| 5 | `DrugPotency` | ❌ 未载 | `:105` `new Fix(drugPotency)`,经 `ScaleFixed` | ✅ **真修** |
| 6 | `HalfLife` | ❌ 未载(恒 0)🔴 | `:106` `action.HalfLifeTicks` | ✅ **真修** |
| 7 | `Method` | ❌ 未载 | `:107` `attempt.Method` | ✅ **真修** |
| 8 | `Cause` | ❌ 未载 | `:108` `cause`(调用方传参) | ⚠️ 有值,但真源归 45(见下) |
| 9 | `Seq` | ❌ 未载 | `:109` `seq: 0` | 🔴 **仍占位** |

**结构性收口(AC-10-42/44)**:`grep -n "new PayloadRef(" HostEmergencyProcessor.cs` ⇒ **0 命中**;
`AssemblyGates.cs:256` `PayloadRefWaivers = Array.Empty<...>()` ⇒ **豁免表确为空**。✅ 真。

**权威 struct**:`Sim.Contracts/Payloads/HistoryPayloads.cs:95-114` 九字段齐备;
`Sim.EmergencyProcedures` 内**已无重复 struct 副本**(源码目录仅 6 个 .cs,无 payload 定义)。✅ 真。

**AC-10-41 单一舍入 —— 独立验算**:
```
32768 × 16385 = 536903680 ÷ 65536 = 8192.5   (rem = 32768 = MUL_ONE/2)
32769 × 16384 = 536887296 ÷ 65536 = 8192.25  (rem = 16384 ≠ 半)
```
⇒ 测试 `applied_payload_settlement_test.cs:182-196` 用 `32768×16385` 得 8193,**正确**。✅

#### GDD A8 勘误 —— 独立验算(任务点名)

`design/gdd/emergency-procedures.md:617-630` 勘误块声称:原文 `(32769 × 16384) ÷ 65536 = 8192.5` 数字颠倒,
正确应为 `(32768 × 16385) ÷ 65536 = 8192.5`。

**独立验算**:`32769×16384 = 536887296`,`536887296 / 65536 = 8192.25`(非 8192.5),余数 16384 ≠ 32768
⇒ **原数字确实不产生 `.5` 分道,勘误结论正确**;`32768×16385 = 536903680 / 65536 = 8192.5`,余数 32768 = `MUL_ONE/2`
⇒ **订正后的数字正确**。✅ **勘误成立。**

🔴 **但勘误未覆盖 AC 表**:`design/gdd/emergency-procedures.md:1014`(AC-10-04a)仍写
「如 `(32769×16384)`」—— **同一个错误数字仍留在 AC 表**。见 §4 新发现 N-3。

---

## §4 新发现(含严重度)

| # | 严重度 | 发现 | 落点 | 说明 |
|---|---|---|---|---|
| **N-1** | 🔴 高 | **两条 BLOCKING AC(10-02/10-03)判据空转** | `reading_contract_test.cs:154-177` | AC-10-02 断言的是「程序集名不含 'Input'」这一恒真命题;AC-10-03 与字段类型测重复。二者均**无 IL/操作码扫描**,却标 `[x]`。GDD 明文要求「asmdef 白名单 + IL 扫描」。 |
| ~~**N-2**~~ | ~~🔴 高~~ → **❌ 撤回** | ~~`SimEvent` header `Seq` 硬编码 0~~ | `HostEmergencyProcessor.cs:87,114` | ❌ **经主会话复核,本判不成立** —— 传 `0` 是**全库既定占位约定**:`EventStream.cs:89-92` 明写「如果事件没有 Seq,则发号」并给 `Seq == 0` 赋值;`PoiStateMachine.cs:129` 同样传 0。**header `Seq` 由流发号,非缺陷。** 原文保留以留痕。 |
| **N-3** | 🟠 中 | **A8 勘误未同步 AC 表** | `design/gdd/emergency-procedures.md:1014` | F-10.4 正文 `:617-630` 已订正为 `32768×16385`,但 AC-10-04a 单元格仍写 `32769×16384`(错误数字)。勘误不完整。 |
| **N-4** | 🟠 中 | **story-006 测试文件位置与声明不符** | story-006 Test Evidence vs `Tests/EditMode/.../feel_latency_test.cs` | story 声明证据在 `Tests/PlayMode/.../feel_latency_test.cs`,实际文件在 **EditMode**。3 项 NOT-RUN 也全在 EditMode。 |
| **N-5** | 🟠 中 | **`SkillMul` 死代码 + JITTER 违 Forbidden** | `JudgeEvaluator.cs:113,116,82` | `skillMul` 算出即弃;稳度门未按 F-10.2 公式用 `SkillMul(L)`;JITTER 用 C# 裸 `/` 截断(Control Manifest 明列 Forbidden)。P0 数值无害但公式与文档不符。 |
| **N-6** | 🟡 低 | **`holdMode` 参数被忽略 ⇒ AC-10-24 空转** | `ModalPhaseEvaluator.cs:82-99` | `Complete` 不读 `holdMode`/`accessibilityOn`,两模式必然同值,测试恒绿。 |
| **N-7** | 🟡 低 | **story/EPIC 文档状态自相矛盾** | story-007 `:207` · EPIC `:70-76` · story-001 头 | story-007 头「Complete」但 Test Evidence「`[ ] Pending`」;EPIC 表 002–006 标 `Ready` 而各 story 文件标 `Complete`;story-001 头「5/5」vs 证据「11/11」。 |
| **N-8** | 🟡 低 | **`Cause` 真源不在本处理器** | `HostEmergencyProcessor.cs:61-68` | `cause` 由调用方传入,自陈「真实调用方 = 45/P1b;当前仅测试调用」⇒ AC-10-39 的 `Cause ∈ {玩家选择,降级}` 在 P0 **无真实生产者**。 |

---

## §5 为什么前两轮评审会漏掉 story-007 的手搓点?(对评审方法本身的检验)

手搓点(`new PayloadRef(attempt.Action, (int)result, 0)`)的特征:**不抛异常、不越界、编译通过**。
既有测试只断言 `Kind` 与**事件条数**,**从不读载荷字段** ⇒ 静默通过。漏检的**方法性根因**有三:

1. **「事件存在」≠「载荷正确」**:前两轮把「Append 被调用」当成载荷验收。判据停在**事件层**,未下沉到**字段层**。
2. **无「构造路径唯一性」门**:手搓 `PayloadRef` 与走 `IPayloadEncoder` 在**类型系统里等价**(都产出 `SimEvent`),
   唯有**源码级/IL 级扫描**(b6 门)才能区分。前两轮无此门,直到 ADR-029 才补。
3. **重复 struct 副本掩盖了类型错配**:`Sim.EmergencyProcedures` 曾**重复定义** payload struct 且**字段集合不同**,
   ⇒ 处理器用的根本不是权威 struct,「九字段齐备」在编译期就**类型对不上**,却因副本存在而静默。

**方法论教训**:BLOCKING AC 的判据**必须读到被断言对象的字段/操作码层**,不能停在「对象存在」。
本报告的 N-1(空转判据)正是**同型缺陷的现役实例** —— 说明该失效模式**未被根除**。

---

## §6 转 Complete 的前置

**EPIC 转 Complete 的硬前置**:
1. **本报告已落盘**(2026-10-03)—— 兑现「评审报告原件落盘」义务。
2. **N-1 修复**:AC-10-02/10-03 补真 IL/操作码扫描,或**降级为 NOT-RUN**(禁以空转判据记绿)。
3. ~~**N-2 登记**:header `Seq` 占位须显式登记~~ —— ❌ **已撤回(复核判定非缺陷)**。
4. **N-3 修复**:AC-10-04a 单元格数字同步 A8 勘误。
5. **N-7 修复**:story-007 Test Evidence / EPIC 表 / story-001 计数三处状态对齐。

**各 story 单独转 Complete**:
- 001 / 003 / 005:**不得**在当前状态下转 Complete(N-1 / N-5 / N-6 判据空转未清)。
- 002:可转,但须注明 DC-5 校验体在 disease-simulation epic。
- 004:可转,**但须**同步登记 header `Seq`(N-2)。
- 006:可转(3 项 NOT-RUN 诚实)。
- 007:可转,**但** `Seq` 占位须保持显式(现有测试已钉死,合规)。

**本报告未覆盖(评审盲区,须后续轮)**:
- 未实跑 Unity 测试套件(结论均基于源码阅读)。
- `action_tables_bake_test.cs` / `modal_phase_test.cs` / `aggregate_stream_test.cs` /
  `host_authority_test.cs` / `feel_latency_test.cs` **全文未读**。
- `ADR-029` / `adr-009 Amendment I` / `adr-006` / `entities.yaml` 的 `EmergencyAttempt` 载荷 schema 未逐字核对。
- `Polarity` 语义(GDD 规则五要求「处置词表查得」vs 实现取 `action.Polarity`)未裁定 —— 标 **未验证**。
