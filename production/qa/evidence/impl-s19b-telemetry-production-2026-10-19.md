# S19b 实现原件 —— TD-epic-01:51 遥测生产主语(四片)

**日期**:2026-10-19
**对象**:`unity/Assets/Telemetry/`(装配 + 11 个生产类)· `unity/Assets/Tests/EditMode/Telemetry/`(8 个夹具)
· `docs/architecture/adr-025-contract-assembly-manifest.md` · `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs`
**类型**:Foundation(边界层;不进 sim、不进三流、零引擎)
**判定口径**:每条 [A] 须附**对生产码变异**的反证;禁借绿(没跑的写没跑)。

---

## 一、原判定(普查原文)

`TD-epic-01`:telemetry-analytics **整个 epic** 的生产实现缺失 ——
10/10 story 标 `Complete`,而 **51 程序集在 `unity/Assets/` 下零文件**
(`find unity/Assets -ipath "*telemetry*" -not -path "*/Tests/*"` 空输出);
7 个测试文件自陈「测试自持谓词面,零生产依赖」。
⇒ 与已处理的三个 epic **不同型**:那些是零散 AC 层,这是整个 epic。

---

## 二、四片偿还(逐片附判据)

### ① 装配 + F1 谓词面(承接 S19 第一批)

- 新建 `unity/Assets/Telemetry/Telemetry.asmdef`:`noEngineReferences: true` + 引用集 ≤ {`Sim.Contracts`}
  ⇒ ADR-019 §三「51 住边界层、不进 sim」的**编译期**表达(GDD R3 同款)。
- `JudgmentMetrics.cs`:F1a/F1b 谓词面上移(整数 (num,den) 对 / 分母 0 报 (0,0)+标记不抛 / ∅==∅ 判对 / F1a⊥F1b)。
- 登记两处:ADR-025 §① 表 + `AssemblyGates.cs:59` b3 manifest。
  ⚠️ **门当场拦下未登记装配**(b3 红 2 条:「[b3] 未登记 asmdef「Telemetry」—— 清单封闭性 = 构建失败」)
  —— 门在履行职责;只改文档门仍红,**文档与门代码必须同改**。

**反证**(改生产码):M1 分母 0 改抛 ⇒ `test_f1_denominatorZero_reports00` 红 ·
M2 空集判错 ⇒ `test_f1a_fourCases_numerator2` 红 · M3 改写计入准确率 ⇒ `test_f1a_f1b_orthogonal` 红。

### ② 七个谓词面上移(本批主体)

| 测试文件 | 新生产类 | 上移方法数 |
|---|---|---|
| `f2_misdiagnosis_matrix_test.cs` | `MisdiagnosisMatrixMetrics` | 2 + 1 嵌套 |
| `f3_skip_rate_test.cs` | `SkipRateMetrics` | 1 + 1 嵌套 |
| `f4_skill_growth_test.cs` | `SkillGrowthMetrics` | 3 + 1 嵌套 |
| `f5_difficulty_curve_test.cs` | `DifficultyCurveMetrics` | 2 + 1 嵌套 |
| `f6_f7_session_rhythm_test.cs` | `SessionRhythmMetrics` | 2 + 1 嵌套 |
| `pure_function_recomputable_test.cs` | `FoldEngine` | 4 + 1 嵌套 |
| `blank_and_degraded_test.cs` | `TickFoldEngine` | 1 |

**纪律**:① **零公式变更** —— 用 `git show HEAD:` 取原子典,brace-match 后逐字比对:
`ComputeSessionSpan` / `ComputeRhythm` **归一缩进后逐字一致**(编排方独立复核);其余同类。
② 嵌套类型(`CaseRecord`/`ActionInstance`/`GrowthEvent`/`SimEvent`/`Event`)**类名与可写属性一字未改**。
③ 生产类**零 `UnityEngine`**(原 F6/F7 用的是 `System.Math`,BCL)。
④ 未手写 `.meta`(Unity 自动生成)。

**两处命名/去重处理(非改判据)**:
- `F6F7Formula` → `SessionRhythmMetrics`(语义化);
- `blank_and_degraded` 的 `FoldEngine` → **`TickFoldEngine`** —— 不改会与 story-008 上移的
  `DaYiJingCheng.Telemetry.FoldEngine` **同名冲突(CS0101)**,两支形状不同。
- `f5` 的自持 `StreamId` 删除,复用既有 `DaYiJingCheng.Sim.Contracts.StreamId`(去重复)。

⚠️ **一处如实登记的无效功**:`blank_and_degraded_test.cs` 对原私有类**零调用点**
(D1–D9 断言走本地替身 `FoldOutcome`/`ComputeEmpty`)⇒ `TickFoldEngine` 上移后**测试仍未驱动它**。
本批只做上移(不自行扩大为「改测试驱动生产类」);**该类的判据仍无生产主语**这一事实登记在案。

### ③ 契约面(`TelemetryContracts.cs`)

- `ITelemetrySource`:**恰好三个 `Read*`**(`ReadHistoryStream` / `ReadCaseStream` / `ReadWorldStream`),
  一一对应 ADR-009 三流;返回 `IReadOnlyList<SimEvent>`(GDD AC-51-A1 逐字)。
- `ITelemetrySink`:`WriteLocalReport` / `LogDeveloperSummary` —— **零** `Upload`/`Send`/`Post`。
- ⇒ 「51 只读」与「51 零出厂」从**纪律**变成**编译期不可表达**。

**同时收窄一条过宽判据**:`readonly_boundary_test.cs::test_noAppendCallSite_noWriteMethods`
原扫「方法名含 Write/Append/Emit」⇒ **过宽**(ADR-019 §五 明确允许本地导出,`WriteLocalReport` 被误杀)。
AC-19-04 的宾语逐字是「**无 `IEventSink.Append` 调用点**」⇒ 收窄为:
① 不得有 `Append`/`Emit` 命名的方法;② 不得有 `IEventSink` 类型形参。

**反证**(改生产码):M1 Source 加第四个方法 ⇒ `sourceIsReadOnly` 红 ·
M2 Sink 加 `Upload` ⇒ `sinkNoUploadSendPost` 红 · M3 Source 加 `Append` ⇒ **2 条红** ·
M4 asmdef 引 `Sim` ⇒ `asmdefNoCycle` 红。
另:`asmdefNoCycle` 由两个 Ignore(「asmdef 尚未创建」/「引用集验证未实现」)**改为真判据**
(解析 references 数组 ⊆ {`Sim.Contracts`} + `noEngineReferences == true`)。

### ④ 还原器 + 导出(`JudgmentStreamReducer.cs` / `MetricsReport.cs`)

- `JudgmentStreamReducer`:**事件流 → `JudgmentRecord`**(ADR-019 §一「回放即数据记录」的落地)。
  判据:全序取**末条**作 J(c) / Scorable = 有真值**且**有判断 / 有真值无判断**不进分母** /
  落笔空集**进分母且 J=∅**(判错,与未落笔是两件事)/ 解码失败**计数且跳过**(不静默)。
  ⚠️ **为何用解码委托**:`PayloadRef` 只寻址,内容要 `IPayloadDecoder`(ADR-031,实现在 `Sim.Codec`),
  而本装配引用集被 `AC-51-A2` 钉死 ⊆ {`Sim.Contracts`} ⇒ **物理上引不到** ⇒ 委托注入(与 ADR-031 同型)。
- `MetricsReport`:整数 (num,den) 对渲染 / Q16.16 显式 `ROUND_HALF_AWAY_FROM_ZERO` /
  分母 0 渲染「分母 0」/ 空列表显式「无指标」。**零 IO**(只拼文本,落盘归 `ITelemetrySink` 实现)。

**反证**(改生产码):M1 取首条 ⇒ `takesLastInOrder` 红 ·
M2 解码失败静默吞 ⇒ `decodeFailure_countedAndSkipped` 红 ·
M3 无判断也进分母 ⇒ `noJudgment_notScorable` 红 · M4 改写不标记 ⇒ `takesLastInOrder` 红。
导出侧:M1 舍入退 ties-to-even · M2 分母 0 除零抛 · M3 率折小数 · M4 分母 0 渲染成 0 ⇒ **四条全红**。

⚠️ **M1 首轮未转红,根因是夹具弱(不是变异式写错)**:原夹具两条判断**解出同一集合**
⇒「取首条」与「取末条」结果相同 ⇒ 变异不可观测。**修夹具**(两条给不同集合 {7}/{9})后转红。
⚠️ 同型第二例在导出侧:M1(舍入)首轮不红是**变异式写法等价**于正确实现(`(n+d/2)/d` ≡ 半远离零),
换真 ties-to-even 实现后转红。⇒ **「变异没转红」要先分清是夹具弱还是变异假**,两者本批各逢一次。

---

## 三、回归对账

| 项 | 值 | 出处 |
|---|---|---|
| 全量 EditMode(S19b 前) | `3805 / 3769 / 0 / 36` | `unity/Logs/s19-epic01-full.xml` |
| 全量 EditMode(S19b 后) | **`3812 / 3775 / 1 / 36`** | `unity/Logs/s19b-epic01-full.xml` |

**等式**:`3812 − 3805 = 7` = 本批新增测数(`judgment_stream_reducer_test` 7 条);`passed +6`。
`failed 0 → 1` = **`TD-player-01` 已知间歇**(`MovementFeedTest`,`dt` 由 0.0099 变 0.0098,同因:引擎吞亚阈值位移)
—— **不用于抵扣计数**,归系统 1 实现轮。

---

## 三·五、第五片:接线到 CompositionRoot(2026-10-19)

**接线前状态(如实,也是本片的存在理由)**:四片加起来只被「**编译通过 + 全量零回归**」证明
—— 那**不是**接线的证据:没有任何一条测试调用过 `TelemetryReporter`。这正是普查批判的形态。

| 件 | 落点 |
|---|---|
| 生产入口 | `unity/Assets/Gameplay.Boot/TelemetryReporter.cs` —— 解码委托的**生产实现**(位掩码展开 / Kind 分派 / ADR-031 抛形转「跳过并计数」) |
| 服务袋 | `CompositionRootServices.Telemetry` 属性;ctor 尾 `new TelemetryReporter(stream, decoder)` |
| 装配边 | `Gameplay.Boot.asmdef` 加 `Telemetry` 引用 |

**为何不改 `Assemble` 签名**:`stream` 与 `decoder` **本就是** `CompositionRootServices` ctor 的形参
⇒ 接线零新装配边、零签名变动。

⚠️ **过程失误(留档)**:首轮给 `Gameplay.Boot.asmdef` 手写 JSON 注释(`// TD-epic-01 …`)
⇒ asmdef **不支持注释**,Unity 报 `JSON parse error: Invalid value` + 编译失败。
已删注释恢复合法 JSON。(前一批给 `EditMode.asmdef` 加引用时没犯,这次加了 —— 教训:**asmdef 只写合法 JSON**。)

**判据** `Tests/EditMode/Boot/telemetry_wiring_test.cs` **7 条**(全部打真链路):
`realStream_f1aComputed`(真编码入流 → 真解码还原 → F1a=1/1)· `wrongJudgment_f1aZeroOverOne`(判错 ≠ 分母 0)·
`bitmaskExpanded_multiDiseaseTruth`(位掩码必须展开)· `emptyStream_denominatorZero` ·
`corruptPayload_countedNotZeroed`(载荷与 Kind 不符 ⇒ 计数跳过,**不**当 0)·
`reporter_holdsNoWriteFace`(只读不变量)· `allMetrics_admitsUnwired`(自陈 F2–F7 未接)

**证伪**(改生产码)M1 位掩码当单值 · M2 取错字段域 · M3 解码失败当 0 ⇒ **全转红**。
⚠️ **M3 首轮不红 = 夹具缺损坏场景**(「跳过」与「当 0」不可观测)⇒ 补 `corruptPayload_countedNotZeroed` 后转红。

**只读不变量的判据收窄(首版过宽被自己的断言打红)**:首版扫「字段类型实现了 `IEventSink`」
⇒ 命中 `_stream: EventStream`(具体类**恰好实现** `IEventSink`,但本类只用其 `Events` 只读快照)
⇒ 收窄为:① 字段**声明类型就是** `IEventSink`;② 有 `Append`/`Emit` 命名的方法。

**回归**:`3819/3782/1/36`(`unity/Logs/s19c-wiring-full.xml`)vs 接线前 `3812/3775/1/36`
⇒ total **+7** = 新增测数;唯一红 = `TD-player-01` 已知间歇(不抵扣)。

---

## 四、仍未还(epic 级 · 禁借绿)

| # | 缺口 | 说明 |
|---|---|---|
| 1 | **调试视图** | ADR-019 §五 三项交付里唯一整项未落的(指标重算器 ✓ · 导出 ✓ · 调试视图 ✗) |
| 2 | **装配层未接线** | 生产侧**没有**把真 `IPayloadDecoder` 包成委托注入 reducer 的构造点 ⇒ 还原器今天只有 EditMode 夹具驱动,**生产零调用**。⇒ F1 指标在生产里仍算不出来 |
| 3 | 3 条 Ignore 未清 | `AC-51-B9`(集成目录 `tests/integration/51/` 未建)· `AC-51-E3`(非主机侧拒绝,依赖 `OQ-51-5` 未裁)· `blank_and_degraded` 的谓词面仍无测试驱动(见 §二②) |
| 4 | **10 条 story 的 `Complete` 一条未动** | 账面与实现的背离仍在 |

**⇒ `TD-epic-01` 状态 = 偿还中(四片),不是已修好。**

---

## 五、命令(第三方可重跑)

```bash
# 本批夹具
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.JudgmentStreamReducerTest"
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.MetricsReportTest"
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.ReadonlyBoundaryTest"
# 期望:7/7 · 8/8 · 11/11(ReadonlyBoundary 的 skipped 已由 2 → 0)

# 全量
rm -f test-results.xml && unity test unity --mode EditMode -- --log-file -
# 期望:total 3812 / passed 3775 / failed 1(唯一红 = TD-player-01 已知间歇)/ skipped 36
```
