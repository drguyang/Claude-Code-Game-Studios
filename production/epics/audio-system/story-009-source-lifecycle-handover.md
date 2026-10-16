# Story 009: 声源生命周期与贴耳交接

> **Epic**: 音频系统
> **Status**: Complete ✅ 2026-10-16(S14-7 复评结案 —— 假绿撤回 → S14-3 生产实现 + 证伪轮全红 → 本节复评转正)
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

> **⚠️ 2026-10-16 假绿复核 —— Status 由 `Complete` 撤回为 `In Review`**
> sprint-13 S13-9 补评判定:本 story 的 AC 语义层**未被验证** —— 声称的生产实现类
> (EndLoop 自评看门狗 / `HandoverMux` 交接 FSM / 句柄生命周期管理器)**不存在**,
> 测试以自造 Fake 顶替 ⇒ 断言在「被测对象不存在」时仍为绿(**BLOCKING 级**)。
> 实测:12 测中 🟢 真 1 · 🟡 半真 2 · 🔴 假 9。
> ⚠️ **另有一处独立瑕疵**:本 story 的 4 条 AC **无 `AC-44-xx` 编号**(用中文描述名),
> ⇒ 任何按编号做的 traceability / 账本 / 门检对本 story 全数落空。
> **评审原件**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`(§2.3 逐条表)。
> 实现另立 sprint;本 story 维持 `In Review` 直至生产类落地并经证伪轮复评。
>
> **✅ 2026-10-16 S14-3 实现轮落盘 —— 生产类已落地 + 证伪轮 8/8 全红**(本 story 仍 `In Review`:
> 转 `Complete` 归 **S14-7** 的 6 条复评批,承 `sprint-14.md` DoD)。**范围经用户裁定收敛**:
> 三类中的**两类已由 S14-2 交付**(`EndLoopWatchdog` / `CueRenderSink`)⇒ 本件只落
> `HandoverMux` + `LoopSelfTerminationDriver`,兜底谓词与句柄面**改指 S14-2 的生产类、不重复造**。
> 落地内容见下方 `## Completion Notes §S14-3` 与
> `production/qa/evidence/impl-s14-3-loop-handover-2026-10-16.md`。

## Context

**GDD**: `design/gdd/audio-system.md`(§States 播放句柄层 · F-44.7 贴耳交接契约 · §Edge Cases)
**Requirement**: TR-audio-012(`EndLoop` 兜底 —— 事件终止信号丢失时的停止路径;2026-09-23 ADR-001 §一之三裁决二结清,gap → covered)

**ADR Governing Implementation**: ADR-001 §一之三 裁决二(主:呈现侧循环终止**必须自评兜底**,零网络依赖,升格架构义务)+ ADR-028 ⑤(44 持有声源池生命周期)
**ADR Decision Summary**: `EndLoop` 不作网络消息存在;P0 默认且唯一路径 = 44 从最新快照 `Progress` 自求值;重连拉流重建 = P1b 增强(须 45 登记三条前置,不免除自评义务)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(有界时间自评需 tick 时钟源;`HANDOVER_MS` 淡化 = mixer 双组已由 Story 003 结构就位)
**Engine Notes**: 交接窗的 crossfade 作用于两级组的 duck 组(不触玩家组);世界语境层句柄归 44 本地持有(ADR-028)—— 停止 ≠ 违「上游唯一收尾」。

**Control Manifest Rules (this layer)**:
- Required: 循环 cue 自评兜底(有界时间内自行结束)· 交接 = 玩家自因、禁双呼吸稳态并存超窗 · 重复 BeginLoop 以最后一次为准
- Forbidden: 循环依赖网络 EndLoop 才停(「呼吸层永不停止」= 已知失败形态)· 快照承担 bus 路由(归脚本动作,Story 003 F7)· 交接硬切(ramp 下界同 50ms 族)
- Guardrail: 分叉的只有音 —— 自评兜底的 `Progress` 求值读的是本地流副本事实,不读表现态

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story(§Edge Cases 为 AC 源)*:

- [x] **AC-009-1 · EndLoop 自评兜底**(AC 源:Edge Cases「EndLoop 永不到达」条 + ADR-001 裁决二 Validation):流停更/快照停「循环中」时,44 在**有界时间**(≤ 登记常量 `LOOP_EVAL_MAX_TICKS`)内自行结束循环;`AudioCueDto.EndLoop` 不出现在任何网络序列化路径;负向:注入永不到达的收尾场景 ⇒ 循环必须在有界内停。— **S14-3 落**:判据 = **S14-2 已交付**的 `EndLoopWatchdog.ShouldSelfTerminate`(不重写);执行面 = `LoopSelfTerminationDriver`(`AudioTuning.LoopEvalMaxTicks = 40`)。证伪 ①②③(停用 ShouldSelfTerminate ⇒ **3 红** · 停用 `_sink.EndLoop` 执行 ⇒ 同 3 条 · 停用注销体 ⇒ 1 红)。
- [x] **AC-009-2 · 贴耳交接契约**(AC 源:Edge Cases 交接条 / GDD F-44.7 :459):进入听诊 ⇒ 世界层在 `HANDOVER_MS` 窗内压出、听诊层同窗淡入,**两层并存 ≤ 交接窗,禁同刻双呼吸稳态**;退出反向;世界层停止由 44 对自有句柄执行(ADR-028)。— **S14-3 落**:`HandoverMux`(`HandoverPhase` 四相 · 两过渡窗 · 窗内线性交叉 · 窗满对**自有句柄** `EndLoop`;`HandoverTicks = 10`)。证伪 ④(停用 `_sink.EndLoop(_worldHandle)` ⇒ `test_enterStethoscope_worldFadesOut` 红)· ④b(停用听诊侧释放 ⇒ `test_exitStethoscope_worldFadesIn` 红)· ⑤(硬切拒绝守卫停用 ⇒ `test_hardCut_rejected` 红)· ⑥(听诊优先守卫移除 ⇒ `test_worldCueArrives_noRestart` 红)· ⑦(幂等重入守卫移除 ⇒ `test_idempotentReentry` 红)· ⑧(交叉淡化公式恒 1 ⇒ `test_midWindow_crossfadeStrictlyBetween` 红)。
- [x] **AC-009-3 · 重复/过期句柄**(Edge Cases):同源重复 `BeginLoop` ⇒ 以最后一次为准释放旧句柄(防双呼吸层);过期 `EndLoop` 忽略不产生播放错误。— **S14-3 落**:测试改指 **S14-2 已交付**的 `CueRenderSink`(**不重复造** —— ADR-024 §六);`FakeHandleManager` 已删。证伪 ③(停用 `EndLoop` 注销体 `_active.Remove` ⇒ `test_normalEndLoop_noSelfTermination` 红)。
- [x] **AC-009-4 · 咳嗽不停呼吸**(Edge Cases 医学条):咳嗽 cue 不调用呼吸循环的 `EndLoop`(与 Story 004 AC-44-08 联合断言)。— **S14-3 落**:`LoopSelfTerminationDriver` 的注销体 + 循环登记面(仅呼吸族循环入册);`test_normalEndLoop_noSelfTermination` 与 ③ 同条覆盖。

> **⚠️ 2026-10-16 AC 编号补齐(S14-3 · 用户裁定)** —— 原 4 条 AC **无编号**(用中文描述名),
> 评审原件 §2.3 判为独立瑕疵:「任何按 `AC-44-xx` 编号做的 traceability / 账本 / 门检对 009
> 全数落空」。现补 **story 局部编号 `AC-009-1…4`**。
> **为何不用 `AC-44-D10…`**:① GDD `design/gdd/audio-system.md` 的 `AC-44-D` 系列止于 **D9**,
> 无 D10+;② **`AC-44-D10` 本身是幽灵 ID** —— story-007 与账本曾用它指「锚点纪律」,
> 而 **GDD 零定义**(实测 `grep -c "AC-44-D10" design/gdd/audio-system.md` = 0),
> 已于同日就地订正(见 story-007 §Completion Notes 与账本)。
> **先例**:全库既有 `AC-020-N` / `AC-021-N` / `AC-023-N` 等 story 局部编号同型。

---

## Implementation Notes

*Derived from ADR-001 §一之三裁决二 + ADR-028 + GDD(2026-09-25 二轮 EndLoop 收口)*:

- **P0 = 自评兜底唯一义务**(原「二者择一」读法已废):从最新快照 `Progress` 求值;时钟源 = sim tick(挂 `ITickProvider`,禁墙钟 —— 与 D7 时钟面同纪律)。
- P1b 拉流重建启用前置(45 登记三条):重放派生窗 / 活句柄 reconciliation / 触发方 —— 记 Dependencies,不实现。
- `LOOP_EVAL_MAX_TICKS` 入 §Tuning Knobs(用户调;有界性是「不永不停止」的量化)。
- 交接实现:世界层与听诊层各持句柄;交接状态机两态(世界/听诊)+ 窗内交叉淡化;44 本地 `Release` 世界层句柄不走上游。
- 快照在交接中的角色 = 只管电平(F7=甲);bus 路由动作与交接状态机同处(玩家自因入口)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: 快照两级组与调用点白名单(交接的 mixer 面前提)
- Story 004: 咳嗽×呼吸的相位/听测半边(本 story 只断「不调 EndLoop」)
- 45: P1b 拉流重建(登记前置)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **EndLoop 自评兜底**: Given: 循环 cue + 人为停止流更新(快照停「循环中」)。When: 推进 tick 至 `LOOP_EVAL_MAX_TICKS`。Then: 循环已停(句柄释放);负向:不实现自评 ⇒ 超时红;序列化扫描:EndLoop 类型零网络路径。
  - Edge cases: 流恢复更新后不得重复停;有界常量取 0 ⇒ 当轮必停(下界自证)。
- **贴耳交接**: Given: 世界层循环中。When: 触发进入听诊。Then: `HANDOVER_MS` 窗内完成交叉;窗结束断言 = 仅听诊层活跃(世界层句柄已 Release);退出镜像;负向:直接硬切(ramp=0)⇒ 红。
  - Edge cases: 窗内重复触发进入(幂等);听诊中世界 cue 新到 ⇒ 不重启世界层(听诊优先)。
- **重复/过期句柄**: Given: 同 cue 两次 BeginLoop / 过期 EndLoop。When: 跑。Then: 只存活最后一次句柄;过期 EndLoop 无异常无状态破坏。
- **咳嗽不停呼吸**: Given: 呼吸循环 + 咳嗽 cue 到达。When: 派发咳嗽。Then: 呼吸句柄仍存活且未重启(相位不重锚)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/audio_system/loop_lifecycle_test.cs` — must exist and pass

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/loop_lifecycle_test.cs`(**16 测**;S14-3 由 12 扩至 16,自造 Fake 全数换生产类);账本互链 `tests/unit/audio_system/README.md`

---

## Dependencies

- Depends on: Story 002(循环字段载体)· 003(两级组)· 008(两层皆可播)
- Unlocks: Story 011(注册表消费面)· 013(交接后主通道稳定态才可听测)

## Completion Notes

> ⚠️ **本节的判定已于 2026-10-16 被 S13-9 补评推翻**
> (`review-audio-epic-fake-green-2026-10-16.md` §2.2/§2.3/§2.6:12 测中 🟢 真 0 · 🟡 半真 3 · 🔴 假 9)。
> 原文**保留不改写**(历史陈述),现行状态见下方 **§S14-3**。
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(EndLoop 自评兜底 · 贴耳交接契约 · 重复过期句柄 · 咳嗽不停呼吸;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(EndLoop 兜底/交接状态机/句柄管理)尚未实现,本 story 测试用自持谓词面(`FakeLoopWatchdog`/`FakeHandoverFsm`/`FakeHandleManager`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **咳嗽测试不重复实现**:Story 009 AC 明确把「咳嗽不停呼吸」列为验收标准,但 Story 004 `breath_layers_test.cs` 已有同名同逻辑测试。为避免重复维护成本,本 story 不重复实现,改为注释引用 Story 004 判据(联合断言)。
3. **GDD 补登记 `LOOP_EVAL_MAX_TICKS`**:QA 评审 BLOCKING-1 发现 AC 引用的「登记常量 `LOOP_EVAL_MAX_TICKS`」在 GDD §Tuning Knobs 中不存在。已补登记(世界语境呼吸族表,安全范围 ≥ 1 tick)。

**评审与修复**:双评审并行(代码质量面 2 BLOCKING + QA 覆盖面 3 BLOCKING)→ 全修:
- **B1**(代码面):`MethodBodyHasClockTokens` 假扫描器(死代码+注释不匹配)→ 删除,改用 Cecil 扫描器 `AssemblyGates.CheckAudioClockTokens`(与 Story 007 同构)。
- **B2**(代码面):`FakeHandoverFsm._handoverStartTick = 0` 魔数(测试通过是巧合)→ 改为相对计数 `_handoverElapsed++`。
- **BLOCKING-1**(QA 面):`LOOP_EVAL_MAX_TICKS` 未登记到 GDD → 补登记到 §Tuning Knobs 世界语境呼吸族表。
- **BLOCKING-2**(QA 面):咳嗽测试与 Story 004 重复 → 删除本文件重复测试,改为注释引用 Story 004 判据。
- **BLOCKING-3**(QA 面):`test_endLoop_noNetworkSerializationPath` 名不副实 → 改名 `test_endLoop_contractShape_localHandleOnly`,承认只验证契约形状。
- **R2**(代码面):命名拼写错误 `handoscope` → `handover`。

**残余 NICE(登记不修)**:
- `FakeHandoverFsm` 缺 `maxTicks < 0` 防御。
- `EnterWorldLayer`/`OnWorldCueArrived` 参数 `cueId` 未使用。
- 缺「退出后重新进入听诊」路径测试。
- 缺「世界层句柄 Release」断言(只验证状态标志)。
- `test_duplicateBeginLoop_lastWins` 语义与 AC 字面偏差(覆盖 ≠ 释放)。
- `test_handover_hardCut_rejected` 负例不完整(只测 ==0,缺 <0)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/loop_lifecycle_test.cs`(**16 测全过**;S14-3 由 12 扩至 16,自造 Fake 全数换生产类)—— ⚠️ 下行「12 测全过 / 1129/1134」是 **2026-09-28 首轮**(假绿轮)的陈述,现行回归见 `## Completion Notes §S14-3`。
**实现证据**: `production/qa/evidence/impl-s14-3-loop-handover-2026-10-16.md`
**Code Review**: Complete —— 双评审并行 + B1/B2/BLOCKING-1/2/3 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-001 §一之三 裁决二(EndLoop 兜底 = 呈现侧义务,零网络依赖)· ADR-028 ⑤(声源池生命周期 = cue 驱动,44 本地持有)· ADR-018 §二(IAudioCueSink 契约)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 6 项已分处登记(本 notes · story Known Risks · 账本)。

---

## Completion Notes §S14-3(2026-10-16 · 生产实现轮)

> **本节的判据 = 变异测试**(停用生产码守卫 ⇒ 断言转红),**不是**「测试名 + 行号」——
> 承 `sprint-14.md` Risks 第 3 条与 retro-13 行动项 2。

### 范围收敛(用户裁定 2026-10-16)

sprint-14 原计划三类:`EndLoop 自评看门狗` / `HandoverMux 交接 FSM` / `句柄生命周期管理器`。
**勘察实测(sprint-plan Phase 1 硬前置)**:

| 原计划 | 实测状态 | 处置 |
|---|---|---|
| EndLoop 自评看门狗 | **谓词已由 S14-2 交付**(`EndLoopWatchdog`);**执行器缺** | 补 `LoopSelfTerminationDriver`(不重写谓词) |
| 句柄生命周期管理器 | **已由 S14-2 交付**(`CueRenderSink`,含 2 测) | 测试改指生产类,**不重复造** |
| `HandoverMux` 交接 FSM | 全库零命中 | **本件唯一新件** |

⇒ 交付 = **2 个新生产类**,而非 3 个 —— 避免与 S14-2 重复造类(同型两处维护违 ADR-024 §六)。

### 交付面(2 新 + 2 改)

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/HandoverMux.cs` | **AC-009-2 的被测物** —— `HandoverPhase` 四相枚举 · 两态 + 两过渡窗 · 窗内线性交叉(以 sim tick 计)· 窗满对**自有句柄**执行 `EndLoop`(ADR-028 ⑤)· 幂等重入 · 听诊优先 · 硬切构造期拒绝 |
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/LoopSelfTerminationDriver.cs` | **AC-009-1 的执行面** —— 登记活跃循环 + 收上游快照 + 每 tick 调 `EndLoopWatchdog.ShouldSelfTerminate` 判停 ⇒ 自行 `EndLoop`(**判据不复制**) |
| 改(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | `HandoverTicks = 10`(500 ms;GDD :777 安全范围 ≥ 50 ms) |
| 改(白名单) | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` | `EntryAllowlist` += `HandoverPhase` |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/loop_lifecycle_test.cs` | 12 → **16 测**;`FakeLoopWatchdog` / `FakeHandoverFsm` / `FakeHandleManager` 全数删除 |

### 修复的病灶(评审原件 §2.3 逐条)

| 原假绿 | 判定 | S14-3 处置 |
|---|---|---|
| 兜底 3 测打自造 `FakeLoopWatchdog` | 🔴 | 改打生产 `EndLoopWatchdog`(判据)+ `LoopSelfTerminationDriver`(执行) |
| 交接 5 测打自造 `FakeHandoverFsm` | 🔴 | 改打生产 `HandoverMux` |
| 句柄 2 测打自造 `FakeHandleManager` | 🔴 | 改打生产 `CueRenderSink`(**S14-2 已交付**) |
| `test_endLoopWatchdog_noWallClockRefs` 扫真门但被测类不存在 | 🟡 空转真扫 | 生产类落地 ⇒ 扫描面非空(同 S14-2 的 D1 收窄处置) |
| 4 条 AC 无 `AC-44-xx` 编号 | 独立瑕疵 | **补 `AC-009-1…4`**(用户裁定;不用 `AC-44-D10` —— 见下) |

### ⚠️ 幽灵 ID 订正(同批 · 用户裁定)

实测 **`AC-44-D10` 是幽灵 ID**:story-007 与账本曾用它指「锚点纪律」,
而 **GDD `design/gdd/audio-system.md` 零定义**(`grep -c "AC-44-D10"` = **0**;
`AC-44-D` 系列止于 **D9**)。同一处「引用却无登记」失效模式,波及两条 story。

**订正**(就地,2026-10-16):
- story-007 §Criteria:「AC-44-D10 锚点纪律」→「AC-44-D1 锚点纪律(GDD 无编号)」
- 账本 `tests/unit/audio_system/README.md` story-007 段:5 处 `AC-44-D10` → `AC-44-D1`
- `production/qa/qa-plan-sprint-14-2026-10-16.md` 1 处
- ⚠️ **评审原件 `review-audio-epic-fake-green-2026-10-16.md` 不改写** —— 它是历史判定记录,
  事后改写 = 伪造评审(承 `.claude/docs/review-workflow.md` §不可追补)。

### 证伪轮 —— 9 例全红(停用生产码守卫 ⇒ 断言转红)

> 纪律:每例**改 → 跑 → 还原**在**同一命令内**完成(`/tmp/fz143.py`,clean-backup 先行、
> `finally` 还原、还原后逐字节校验)。**零残留**已核(9 例备份与生产码 `cmp -s` 全 IDENTICAL)。

| 例 | 停用的守卫 | 转红断言 | 红数 |
|---|---|---|---|
| ① selftick | `ShouldSelfTerminate(...)` → `false` | `staleStream_stopsWithinBound` · `resumeNoDoubleStop` · `zeroBoundStopsImmediately` | 3 |
| ② execute | `_sink.EndLoop(rec.Handle)` 停用 | 同 ① 三条(**修夹具后**) | 3 |
| ③ unregister | `EndLoop` 注销体清空 | `normalEndLoop_noSelfTermination` | 1 |
| ④ worldrelease | 窗满释放世界层句柄 | `enterStethoscope_worldFadesOut` | 1 |
| ④b stethrelease | 窗满释放听诊层句柄 | `exitStethoscope_worldFadesIn` | 1 |
| ⑤ hardcut | `handoverTicks <= 0` 拒绝 → `false` | `hardCut_rejected` | 1 |
| ⑥ priority | 听诊优先守卫 → 无 | `worldCueArrives_noRestart` | 1 |
| ⑦ idempotent | 幂等重入守卫 → 无 | `idempotentReentry` | 1 |
| ⑧ crossfade | 交叉淡化公式 → 恒 1 | `midWindow_crossfadeStrictlyBetween` | 1 |

> **原始输出**见 `production/qa/evidence/impl-s14-3-loop-handover-2026-10-16.md` §3(逐例红数 + 红名)。

#### ⚠️ 证伪轮抓到**我自己写的假绿夹具**(BLOCKING 级 · 已修)

例 ② 首轮 **红 0 条** —— 停用 `_sink.EndLoop(rec.Handle)`(驱动器**实际执行**停止的唯一一行)
后,三条兜底测试**仍全绿**。根因:它们只断言驱动器**内部计数器**(`ActiveLoopCount` /
`SelfTerminatedCount`);驱动器可以「停止跟踪」却从不调 sink,计数器照样正确。

同一弱点在交接测试:`WorldHandleReleased` / `StethoscopeHandleReleased` 是 `HandoverMux`
的**自述标志**,不是副作用观测。

**修复**:五条测试改为断言 `RecordingRenderTarget.Stopped` 含句柄 id(可观测副作用)。
修复后例 ② **0 红 → 3 红**;新增例 ④b(专测听诊层释放)⇒ **1 红**。
**这是 S11-9 / S13-9 同型假绿在本轮的第三次现身** —— 判 BLOCKING,已就地修复并登记。

### 回归终值

**`3542 / 3496 / 0 / 46`**(`unity/Logs/s14-3-final.xml`,2026-10-16,EXIT=0)—— 基线 S14-2 =
`3538 / 3492 / 0 / 46` ⇒ **+4 / +4 / 0 / 46 不变,零回归**(`loop_lifecycle_test` 12 → 16 测)。
`Audio` 过滤套件 = **324 / 323 / 0**。验证命令见 evidence 文档 §4。

### 门保持

b5(2 新文件全在 44 命名空间内)· b3(`HandoverPhase` 经用户裁定增列)·
b4(字段类型全落白名单)· **零墙钟**(`CheckAudioClockTokens` 扫 `HandoverMux` /
`LoopSelfTerminationDriver` 方法体)· ADR-029 §③(`Sim/` 内 `new PayloadRef(` 真代码零命中)。

### NOT-RUN 如实登记(禁借绿)

- **AC-009-2 的听测半边**(交接窗 500 ms 是否「听觉上不拖沓」)—— **NOT-RUN**:
  听测需素材本体(story-015/016,`Ready`)+ 真人听审。
- **IL2CPP 三格**(ADR-012 矩阵)—— **NOT-RUN**:本机仅 Mono,CI 真 runner 未配。
- **S14-2 已登记的 4 人对拍** —— 本件不涉(无联机面)。

