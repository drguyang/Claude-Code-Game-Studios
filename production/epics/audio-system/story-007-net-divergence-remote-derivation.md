# Story 007: 联机分叉与远端派生(静态半)

> **Epic**: 音频系统
> **Status**: Complete ✅ 2026-10-16(S14-7 复评结案 —— 假绿撤回 → S14-2 生产实现 + 证伪轮全红 → 本节复评转正)
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 5h(运行面 P1b 另计)
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-16

> **⚠️ 2026-10-16 假绿复核 —— Status 由 `Complete` 撤回为 `In Review`**
> sprint-13 S13-9 补评判定:本 story 的 AC 语义层**未被验证** —— 声称的生产实现类
> (cue 发射派生器 / `IAudioCueSink` 生产实现 / 锚点门 / EndLoop 看门狗)**不存在**,
> 测试以自造 Fake / 自写谓词顶替 ⇒ 断言在「被测对象不存在」时仍为绿(**BLOCKING 级**)。
> 实测:19 测中 🟢 真 4 · 🟡 半真 5 · 🔴 假 10;AC-44-D7 的**核心判据**(重排差分哈希)
> 被测物是测试自己写的折叠函数。
> **评审原件**:`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`(§2.1 逐条表)。
> 实现另立 sprint;本 story 维持 `In Review` 直至生产类落地并经证伪轮复评。
>
> **✅ 2026-10-16 S14-2 实现轮落盘 —— 生产类已落地 + 证伪轮 7/7 全红**(本 story 仍 `In Review`:
> 转 `Complete` 归 **S14-7** 的 6 条复评批,承 `sprint-14.md` DoD「6 条 story 由 In Review 转
> Complete(经证伪轮复评)」)。落地内容见下方 `## Completion Notes` §S14-2 与
> `production/qa/evidence/impl-s14-2-audio-cue-deriver-2026-10-16.md`。

## Context

**GDD**: `design/gdd/audio-system.md`(规则七 · Edge Cases 联机与空间化段 · AC-44-07/D1/D7)
**Requirement**: TR-audio-007(听感精度档 = 各设备按本机技能档,分叉的只有音)· TR-audio-008 ⚠partial(远端空间化经 `IPositionalChannel` 复用 —— 发布者实现随 45 走 P1b,**禁借绿**)· TR-audio-009(cue 载荷不复制)

**ADR Governing Implementation**: ADR-001: 联机 pipe 抽象(主:§一之二 `IPositionalChannel` + 消费纪律 + §一之三裁决二)+ ADR-018 §五(联机音频)
**ADR Decision Summary**: cue 不复制 —— 各客户端从本地流副本 + 表现映射自行派生;远端位置只读 per-`ActorId` latest-value 表,不新增通道;`SetTier(TierSource.Local)` 为默认且联机亦然(裁定 D-A)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(ADR-001 NGO/Fusion 库面 post-cutoff,`OQ-3-x` 库 swap P1b)
**Engine Notes**: 本 story 为**静态半**:三腿中的 IL 扫描与重排差分在 P0 可跑;**4 人双实例对拍 = BLOCKED-BY-45**(P1b);`IPositionalChannel` 运行面依赖 45 未写代码(契约已定)。

**Control Manifest Rules (this layer)**:
- Required: 派生 = 流事件/sim tick 驱动(禁帧/墙钟)· `Intensity` 归一整数域 · 远端只读锚点禁判定(`ServerTick` 陈旧检查条件,ADR-001 消费纪律 2)
- Forbidden: 本地 RNG / 读表现态位置触发 cue(违 D7)· 新增 QoS 通道 · 语音通道 · 把「分叉只有音」读成可分叉病情事实
- Guardrail: P0 只交静态半;运行面对拍未跑前 D7/D1 的运行面**禁记绿**

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **AC-44-07**(静态半):`SetTier(TierSource.Local)` 为默认语义(单机 = 本地;联机亦然)—— 单元断言本地档取本机技能值;**4 人运行面对拍 = BLOCKED-BY-45**(P1b);脉案字体量分叉文档一致性核对(回归注)。— **S14-2 落**:`CueRenderSink` + `ICueRenderTarget`(生产 `IAudioCueSink` 实现);`test_setTier_defaultLocalRecorded`(证伪例 ⑦:停用 `SetTierCallCount++` ⇒ 该测红)。
- [x] **AC-44-D1**:空间化位置来源检查 —— 复用 `IPositionalChannel`(按 `ActorId` 索引);44 不自建位置通道/不新增 QoS/不做语音;**只读取锚点,禁读其做判定**(增益/声像/低通/距离排序 = 渲染路由可读,`ServerTick` 陈旧检查 + 允许缺省锚点;「发不发 cue」= 判定,禁)。— **S14-2 落**:`AnchorGate`(生产 `GateDecision` 闭集,`Dispatch` 只做渲染路由;`IsStale` 用 `candidateTick < latestTick`);`test_anchorStale_refused`(证伪例 ⑤)。
- [x] **AC-44-D7**(BLOCKING,静态三腿):cue 发射派生 —— ① 输入集 ∈ {流事件, 烘焙数据} 纯函数;② **时钟面** = 流/tick 驱动,禁帧与墙钟;③ **载荷面** = DTO 全字段(除注入的 `Cell`)同纯度,`Intensity` 归一 ∈ 整数域。**三腿**:发射模块 IL 扫描(禁 `Random`/`Time`/`DateTime`/`IPositionalChannel`)· 同流重排差分哈希(含 Intensity)· 负向夹具(注入读表现态 ⇒ 红);**P1b 双实例对拍 = BLOCKED-BY-45**。— **S14-2 落**:`CueEmitDeriver`(`Derive` + `CanonicalHash` + `DomainSeparator`)+ `AssemblyGates.CheckAudioDerivationSurfaceRefs`(具名面 + **反空转守卫** `test_derivationSurfaceScan_absentNamedType_isRed`);重排差分哈希三例证伪 ①②③(`sort` / `intensity` / `loopfold`);负向夹具 = 锚点/陈旧两例 ④⑤。

---

## Implementation Notes

*Derived from ADR-001 §一之二/§一之三 + GDD Edge Cases(2026-09-25 二轮 N1 两面扩)*:

- **两段式装配**:派生 = 是否发出 + `CueId/Intensity/Looped`;`Cell` 于空间化时注入(远端 ← `IPositionalChannel`,本机 ← 13 的 `p.Cell`)—— 「派生禁读表现态」与「DTO 携带表注入 Cell」不冲突(GDD Edge 注,回引 ADR-001 消费纪律 3)。
- 锚点时序竞争:**一次性 cue 锚点缺席 ⇒ 丢弃不补播**(与断线静默过期同语义);循环 cue 挂起至锚点到达(Edge Cases 新条)。
- 重排差分:同流 fixture 打乱到达序过 `ReorderBuffer` → cue 序列(含 Intensity)哈希一致 —— 与确定性黄金夹具(ADR-012)同族的可重构判据。
- `ServerTick` 条件是 ADR-001 已写、GDD 缺的承接(二轮 REC 落点):渲染路由读表须陈旧检查 + 缺省锚点兜底。
- 联机裁决 D-A 的反向激励论证在 GDD 规则七 —— 实现不得回退到主机档。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008: 空间化渲染实现与 cell_jitter(本 story 只管位置来源与派生纪律)
- 45 网络层:`IPositionalChannel` 代码实现与发布者(P1b,契约已定)
- 4 人双实例对拍运行面(BLOCKED-BY-45)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-07**(静态): Given: 本机技能档值。When: `SetTier(TierSource.Local)` 初始化。Then: 档值 == 本机技能(非主机/非默认);文档核对无「取主机技能」残句。
- **AC-44-D1**: 来源纪律。Given: 44 空间化代码。When: IL 扫描 + 接口枚举。Then: 仅读 `IPositionalChannel`;无写入/无新通道方法;负向:加一个按位置做 if 分支的 cue 触发 ⇒ 注入夹具红;`ServerTick` 陈旧检查存在于读表路径。
- **AC-44-D7**: 三腿。Given: 发射模块(13/9/52/18/37/8)。When: ① IL 扫描引用白名单;② 同流 fixture 重排 → cue 序列哈希;③ 负向注入。Then: ① 零 `Random/Time/DateTime/IPositionalChannel` 引用;② 打乱前后哈希一致(含 Intensity);③ 注入读表现态触发 ⇒ 红;`Intensity` 归一输出断言 ∈ 整数域(与 `D-8-4` 已还清口径一致)。
  - Edge cases: 到达序不同但同流 ⇒ 哈希同;tick 驱动 vs 帧驱动的触发时序差异被重排腿抓住。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/unit/audio_system/net_derivation_test.cs` — 静态三腿 must pass
- 运行面(4 人对拍):**BLOCKED-BY-45**,证据待 P1b

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs`(**34 测**;S14-2 由 19 扩至 34,自造 Fake 全数换生产类);账本互链 `tests/unit/audio_system/README.md`

---

## Dependencies

- Depends on: Story 001(DTO 护栏)· 002(事件表字段)
- Unlocks: Story 008(D1 的位置面被空间化消费);运行面解锁靠 45

## Completion Notes

> ⚠️ **本节的判定已于 2026-10-16 被 S13-9 补评推翻**
> (`review-audio-epic-fake-green-2026-10-16.md` §2.2/§2.3/§2.6:19 测中 🟢 真 0 · 🟡 半真 5 · 🔴 假 14)。
> 原文**保留不改写**(历史陈述),现行状态见下方 **§S14-2**。
**Completed**: 2026-09-28
**Criteria**: 5/5 passing(AC-44-07 静态半 · AC-44-D1 来源纪律 · AC-44-D7 静态三腿 · AC-44-D1 锚点纪律(GDD 无编号 —— 原写 AC-44-D10 系幽灵 ID,2026-10-16 订正) · EndLoop 自评兜底;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **IL 扫描器路线变更**:初版用手写 IL 解析器(`MethodBodyReferences` + `OperandSize`),代码评审发现三类 BLOCKING 缺陷(B1: `ret` 误判 / B2: 属性访问 token 失效 / B3: 操作数表错误)。改用 **Mono.Cecil**(与 b5 `CheckAudioAssemblyIl` 同构),在 `AssemblyGates` 内实现 `CheckAudioClockTokens` + `CheckAudioPositionalChannelRefs` 两个 Cecil 扫描器,测试侧只调谓词。**手写解析器已完全删除**。
2. **D1 实现数断言调整**:原断言「实现数 = 1」,但 P0 内当前 = 0(实现归 Story 008)。改为「实现数 ≤ 1」+ 方法集断言在 0 实现时跳过。**空过防护**:扫描面限制为生产装配,不含测试装配的 fake。
3. **重排差分哈希覆盖全部载荷字段**:QA 评审 REC-1/REC-2 指出 `DeriveCueHash` 注释写「全字段」但实现只折叠 `CueId`+`Intensity`。已补齐 `Source`+`Looped`,注释与实现一致(AC-44-D7 ③ 要求「DTO 全字段(除 Cell)的派生同纯度」)。

**评审与修复**:双评审并行(代码质量面 3 BLOCKING + 5 REC;QA 覆盖面 0 BLOCKING + 3 REC)→ 全修:
- **B1/B2/B3**(代码面):手写 IL 解析器三类缺陷 → Cecil 扫描器替代,手写解析器完全删除。
- **R1**(代码面):`test_positionalChannel_singleConsumerImplementation` 扫描面含测试装配 → 限制为生产装配。
- **R2**(代码面):`FakePositionalChannel` 死代码 → 删除。
- **R3**(代码面):`test_setTier_defaultLocalRecorded` 恒真断言 → 改为枚举闭集断言(`Enum.GetNames` 不含 `Host`)。
- **REC-1/REC-2**(QA 面):`DeriveCueHash` 注释与实现不一致 → 补齐 `Source`+`Looped`。
- **REC-3**(QA 面):「按位置做 if 分支的 cue 触发」负向夹具 → **登记为已知缺口,归 Story 008 实现时补上**(需解析 IL 条件跳转逻辑,比 token 引用扫描复杂)。

**残余 NICE(登记不修)**:
- `test_gdd_netSection_noHostTierWording` 依赖文件系统(文档漂移守卫,非单元测试;项目已有先例,可接受)。
- `DeriveCueHash` 的魔数 `0xA44DE077B1E5C196UL` 未具名(建议提取为 `DomainSeparator` 常量)。
- `test_audioCueDto_allFieldsIntegerDomain` 硬编码字段数 5(漂移监控,增删字段须同步更新)。
- `test_anchorStale_refused` 缺边界值测试(`ServerTick = 0` / `uint.MaxValue` 回绕)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs`(**34 测全过**;S14-2 由 19 扩至 34,自造 Fake 全数换生产类)—— ⚠️ 下行「19 测全过 / 1102/1103」是 **2026-09-28 首轮**(假绿轮)的陈述,现行回归见 `## Completion Notes §S14-2`。
**实现证据**: `production/qa/evidence/impl-s14-2-audio-cue-deriver-2026-10-16.md`
**Code Review**: Complete —— 双评审并行 + B1/B2/B3 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-18 §五(联机音频:分叉的只有音)· ADR-001 §一之二(IPositionalChannel + 消费纪律)+ §一之三 裁决二(EndLoop 兜底)· ADR-005(客户端持流副本)· ADR-008(EventOrderKey)· ADR-012(SplitMix64)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 4 项 + REC-3 已分处登记(本 notes · story Known Risks · 账本)。

---

## Completion Notes §S14-2(2026-10-16 · 生产实现轮)

> **本节的判据 = 变异测试**(停用生产码守卫 ⇒ 断言转红),**不是**「测试名 + 行号」——
> 承 `sprint-14.md` Risks 第 3 条与 retro-13 行动项 2。

### 交付面(4 新 + 4 改)

| 类别 | 文件 | 内容 |
|---|---|---|
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/CueEmitDeriver.cs` | **AC-44-D7 核心判据的被测物** —— `CueEmitInput`(全序键四元组 + cue 载荷三件)· `Derive`(`EventOrderKey` 全序排序)· `CanonicalHash`(载荷面全字段折叠)· `DomainSeparator` 具名常量(清掉原 NICE 的魔数) |
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/AnchorGate.cs` | AC-44-D1 / D10 —— `GateDecision` 闭集 · `Dispatch`(**只读锚点存在性**)· `Update`(挂起补发)· `IsStale`(**独立纯谓词**,归渲染路由面) |
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/EndLoopWatchdog.cs` | EndLoop 自评兜底(ADR-001 §一之三 裁决二)· `LoopProgress` 快照形状 · `ShouldSelfTerminate` · `IsStagnant` |
| 新(生产) | `unity/Assets/Gameplay.Presentation/Audio/CueRenderSink.cs` | **`IAudioCueSink` 的生产实现**(补评前该位置是测试自造的 `TierRecordingSink`)· `ICueRenderTarget` 注入 seam · 句柄唯一性 / 过期忽略 / 档位记录 |
| 改(生产) | `unity/Assets/Gameplay.Presentation/Audio/AudioTuning.cs` | `LoopEvalMaxTicks = 40`(2 s;ADR-001 裁决二「有界值不得留空」的 P0 半边) |
| 改(门) | `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | **D1 扫描面收窄为具名派生面 + 反空转守卫**(见下) |
| 改(白名单) | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` | `EntryAllowlist` += `CueEmitInput` / `GateDecision` / `LoopProgress`;`EntryInterfaceAllowlist` += `ICueRenderTarget` |
| 改(测试) | `unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs` | 19 → **34 测**;`DeriveCueHash` / `NetSimEvent` / `FakeAnchorGate` / `FakeEndLoopWatchdog` / `TierRecordingSink` 全数删除 |

### 修复的病灶(评审原件 §2.1 逐条)

| 原假绿 | 补评判定 | S14-2 处置 |
|---|---|---|
| 重排差分哈希的被测物 = 测试自写 `DeriveCueHash` | 🔴 | 被测物改为生产 `CueEmitDeriver.Derive` / `.CanonicalHash` |
| `IAudioCueSink` 生产实现不存在(测试自造 `TierRecordingSink`) | 🔴 | 落 `CueRenderSink` + `ICueRenderTarget` |
| 锚点门 / EndLoop 看门狗无生产实现(测试自造 Fake) | 🔴 | 落 `AnchorGate` / `EndLoopWatchdog` |
| **「空转真扫」**(真门 + 非空面 + 被断言类不存在 ⇒ 零违例对空集平凡成立) | 🔴 | 扫描面收窄为**具名** `{CueEmitDeriver, CueEmitInput}` + **反空转守卫**(具名类型缺席 ⇒ 直接 RED) |

### D1 门收窄(用户裁定 · 三选一取「收窄 + 反空转守卫」)

`CheckAudioPositionalChannelRefs(dllPath, nsPrefix)` 的**公开签名保留**(委派重载),
新增 `CheckAudioDerivationSurfaceRefs(dllPath, nsPrefix, surfaceTypeNames)`。
私有实现增 `surfaceTypeNames` 过滤 + **反空转守卫**:具名面任一类型在扫描面内缺席 ⇒
直接报错,不再让「零违例」对空集平凡成立。**自证测试**:`test_derivationSurfaceScan_absentNamedType_isRed`。

### 证伪轮 —— 7 例全红(停用生产码守卫 ⇒ 断言转红)

> 纪律:每例**改 → 跑 → 还原**在**同一命令内**完成(`/tmp/fz_one.py`,clean-backup 先行、
> `finally` 还原、还原后逐字节校验)。**零残留**已核(`grep` + 与 `/tmp/fzsrc-*.cs` diff)。

| 例 | 停用的守卫 | 转红断言 | 红数 |
|---|---|---|---|
| ① sort | `keyed.Sort(...)` 全序排序 | `test_reorderInvariance_arrivalOrderPreservesCueHash` | 1 |
| ② intensity | `Fold(state, c.Intensity)` | `test_reorderInvariance_intensityFeedsHash` | 1 |
| ③ loopfold | `Fold(state, c.Looped ? 1UL : 0UL)` | `test_reorderInvariance_allPayloadFieldsFeedHash` | 1 |
| ④ pending | `if (cue.Looped)` → `if (false)` | `test_anchorAbsent_duplicatePending_lastWins` · `test_anchorAbsent_oneShotDropped_loopCuePending` · `test_anchorArrived_pendingLoopReleased` | 3 |
| ⑤ stale | `IsStale` → `false` | `test_anchorStale_refused` | 1 |
| ⑥ threshold | `return stagnant > maxStagnantTicks;` → `false` | `test_endLoop_selfAssessPredicate_pureAndBounded` · `test_endLoop_thresholdBoundary_zeroAndEqual` | 2 |
| ⑦ settier | `SetTierCallCount++;` | `test_setTier_defaultLocalRecorded` | 1 |

**两处我的预测与实际不符(如实登记,不修饰)**:
- 例 ② 预测 2 红、实际 **1 红** —— `allPayloadFieldsFeedHash` 只断言 `Source` / `Looped`
  两个字段,并**不断言** `Intensity` ⇒ 不该红(我的预测过宽,非夹具缺陷)。
- 例 ④ 预测 2 红、实际 **3 红** —— 多出 `test_anchorArrived_pendingLoopReleased`:
  `Pending` 分支停用后该路径不可达,断言随之失败(合理)。

### 回归终值

**`3538 / 3492 / 0 / 46`**(`unity/Logs/s14-2-final.xml`,2026-10-16,EXIT=0)——
基线 S14-1 = `3523 / 3477 / 0 / 46`(commit `a2c6bd6`)⇒ **+15 / +15 / 0 / 46 不变,零回归**。
`NetDerivationTest` = **34/34 Passed** · `AssemblyBoundaryTest` = **24/24 Passed**(白名单增列生效)。
**验证命令**:`grep -o 'total="3538" passed="3492" failed="0" skipped="46"' unity/Logs/s14-2-final.xml`

### 门保持

b5(4 新文件全在 `DaYiJingCheng.Gameplay.Presentation.Audio` 命名空间内)· b3(白名单 4 项经用户裁定批准增列)·
b4(字段类型全落白名单)· ADR-029 §③(`Sim/` 内 `new PayloadRef(` 真代码零命中)·
**派生面零 `IPositionalChannel`**(`CueEmitDeriver` 真代码零引用;位置面归 `AnchorGate`,两段式装配的结构表达)。

### NOT-RUN 如实登记(禁借绿)

- **AC-44-D7 / D1 的 4 人双实例运行面对拍 = BLOCKED-BY-45**(P1b,`IPositionalChannel` 发布者未写)。
  本 story 只声称**静态半**已证。
- **IL2CPP 三格**(ADR-012 矩阵):**NOT-RUN** —— 本机仅 Mono;CI 真 runner 未配(`UNITY_LICENSE`)。

