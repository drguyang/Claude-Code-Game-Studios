# integration/telemetry/

按系统分目录的集成测试(命名 `test_[scenario]_[expected]`)。
遥测与分析系统(51)的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点;
`.cs` 真身落 `unity/Assets/Tests/EditMode/Telemetry/`,本 README 记真身与 AC → 测映射。

## Story 001(只读边界与零出厂 —— AC-19-01…08 / AC-51-A1…A8)

故事头登记的证据路径为
`tests/integration/telemetry/readonly_boundary_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Telemetry/readonly_boundary_test.cs`**(类 `ReadonlyBoundaryTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Telemetry/readonly_boundary_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`IEventSink` / `ITelemetrySource` / `ITelemetrySink`) |
| 被测 ADR | `docs/architecture/adr-019-telemetry-and-privacy.md` |
| 运行方式 | `unity test unity --mode EditMode --filter ReadonlyBoundaryTest` |

> 读法纪律:生产代码(51 程序集)尚未实现,测试用契约面验证 + `Assert.Ignore` 明确标记待实现项。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`ReadonlyBoundaryTest` 内) | 性质 |
|---|---|---|
| **AC-51-A1 只读边界** | `test_readonlyBoundary_noEventSinkRefs` | BLOCKING |
| **AC-19-01 零新埋点** | `test_zeroNewInstrumentation_sourceIsReadOnly` | BLOCKING |
| **AC-19-04 无 Append 调用点** | `test_noAppendCallSite_noWriteMethods` | BLOCKING |
| **AC-51-A2 无环(asmdef)** | `test_readonlyBoundary_asmdefNoCycle` | BLOCKING |
| **AC-19-02/AC-51-A4 零出厂** | `test_zeroEgress_noNetworkTypes` | BLOCKING |
| **AC-19-02 ITelemetrySink 无 Upload** | `test_zeroEgress_sinkNoUploadSendPost` | BLOCKING |
| **AC-19-04/AC-51-A8 不订阅 Step** | `test_noStepSubscription_noUpdateCallbacks` | BLOCKING |
| **AC-19-06/AC-51-A5 无呈现面** | `test_noPlayerUi_noPresentationAssets` | BLOCKING |
| **AC-19-07/AC-51-E2 零写回** | `test_noWriteBack_noFileWriteApis` | BLOCKING |
| **AC-51-A5 呈现层不引用** | `test_noPresentationAssemblyRefs_judgmentMetrics` | BLOCKING |
| **AC-51-A7 零第三方** | `test_noThirdParty_noAnalyticsPackages` | BLOCKING |

> **测试数**:`readonly_boundary_test` = **11**(7 passed + 4 skipped)。
> ⚠️ 关键区分:生产代码(51 程序集)尚未实现,测试用契约面验证 + `Assert.Ignore`。
> 实现后应替换为有意义的签名扫描断言。

## Story 002(F1 判断准确率 —— AC-51-B1…B4)

故事头登记的证据路径为
`tests/unit/telemetry/f1_judgment_accuracy_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Telemetry/f1_judgment_accuracy_test.cs`**(类 `F1JudgmentAccuracyTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Telemetry/f1_judgment_accuracy_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`IEventSink` / `ITelemetrySource` / `ITelemetrySink`) |
| 被测 GDD | `design/gdd/telemetry-analytics.md`(AC-51-B1…B4) |
| 运行方式 | `unity test unity --mode EditMode --filter F1JudgmentAccuracyTest` |

> 读法纪律:生产代码(51 程序集)尚未实现,测试用自持谓词面(F1Formula)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`F1JudgmentAccuracyTest` 内) | 性质 |
|---|---|---|
| **AC-51-B1 F1a 四例** | `test_f1a_fourCases_numerator2` | BLOCKING |
| **AC-51-B2 F1a ⊥ F1b** | `test_f1a_f1b_orthogonal` | BLOCKING |
| **AC-51-B3 F1 取法** | `test_f1_takeLastJudgment_wins`(跨病例共病路由) | BLOCKING |
| **AC-51-B4 分母 0** | `test_f1_denominatorZero_reports00` | BLOCKING |
| **AC-51-B4 负向** | `test_f1_denominatorNonZero_not00` | BLOCKING |

> **测试数**:`f1_judgment_accuracy_test` = **5**。
> ⚠️ 关键区分:生产代码(51 程序集)尚未实现,测试用自持谓词面。
> F1 公式实现与 GDD `telemetry-analytics.md:172-181` 逐字对齐。

## Story 003(F2 误诊分布 —— AC-51-B5…B7)

故事头登记的证据路径为
`tests/unit/telemetry/f2_misdiagnosis_matrix_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Telemetry/f2_misdiagnosis_matrix_test.cs`**(类 `F2MisdiagnosisMatrixTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Telemetry/f2_misdiagnosis_matrix_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`IEventSink` / `ITelemetrySource` / `ITelemetrySink`) |
| 被测 GDD | `design/gdd/telemetry-analytics.md`(AC-51-B5…B7) |
| 运行方式 | `unity test unity --mode EditMode --filter F2MisdiagnosisMatrixTest` |

> 读法纪律:生产代码(51 程序集)尚未实现,测试用自持谓词面(F2Formula)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`F2MisdiagnosisMatrixTest` 内) | 性质 |
|---|---|---|
| **AC-51-B5 F2 与 F1 同源** | `test_f2_f1aIdentity_threeTerms`(含 D={t}∧J=∅ 与 D=∅∧J={j} 用例) | BLOCKING |
| **AC-51-B6 三桶显式计数** | `test_f2_threeBucketsExplicitCount`(含 \|J\|≥2 共病路径) | BLOCKING |
| **AC-51-B7 不触发旁路** | `test_f2_unmappableNoBypass` | BLOCKING |

> **测试数**:`f2_misdiagnosis_matrix_test` = **3**。
> ⚠️ 关键区分:生产代码(51 程序集)尚未实现,测试用自持谓词面。
> F2 公式实现与 GDD `telemetry-analytics.md:213-246` 逐字对齐。

## Story 004(F3 跳过率 —— AC-51-B8…B9)

故事头登记的证据路径为
`tests/unit/telemetry/f3_skip_rate_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Telemetry/f3_skip_rate_test.cs`**(类 `F3SkipRateTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Telemetry/f3_skip_rate_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`IEventSink` / `ITelemetrySource` / `ITelemetrySink`) |
| 被测 GDD | `design/gdd/telemetry-analytics.md`(AC-51-B8…B9) |
| 运行方式 | `unity test unity --mode EditMode --filter F3SkipRateTest` |

> 读法纪律:生产代码(51 程序集)尚未实现,测试用自持谓词面(F3Formula)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`F3SkipRateTest` 内) | 性质 |
|---|---|---|
| **AC-51-B8 分开报 + 分母口径** | `test_f3_separateNumerators_notOfferedExcluded` | BLOCKING |
| **AC-51-B8 负向** | `test_f3_notOfferedNotInDenominator` | BLOCKING |
| **AC-51-B4(F3 侧)分母 0** | `test_f3_denominatorZero_reports00` | BLOCKING |
| **AC-51-B4 全 NotOffered** | `test_f3_allNotOffered_denominatorZero` | BLOCKING |
| **AC-51-B9 真实会话可得性** | `test_f3_realSession_blockedByOQ108` | NOT-RUN(BLOCKED-BY-OQ-10-8) |

> **测试数**:`f3_skip_rate_test` = **5**(4 passed + 1 skipped)。
> ⚠️ 关键区分:生产代码(51 程序集)尚未实现,测试用自持谓词面。
> F3 公式实现与 GDD `telemetry-analytics.md:259-280` 逐字对齐。

## Story 007(F6 局内时长 + F7 节律 —— AC-51-B15…B18)

故事头登记的证据路径为
`tests/unit/telemetry/f6_f7_session_rhythm_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Telemetry/f6_f7_session_rhythm_test.cs`**(类 `F6F7SessionRhythmTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Telemetry/f6_f7_session_rhythm_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`IEventSink` / `ITelemetrySource` / `ITelemetrySink`) |
| 被测 GDD | `design/gdd/telemetry-analytics.md`(AC-51-B15…B18) |
| 运行方式 | `unity test unity --mode EditMode --filter F6F7SessionRhythmTest` |

> 读法纪律:生产代码(51 程序集)尚未实现,测试用自持谓词面(F6F7Formula)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`F6F7SessionRhythmTest` 内) | 性质 |
|---|---|---|
| **AC-51-B15 F6 可审计(单事件 span=0)** | `test_f6_auditable_singleEventSpanZero` | BLOCKING |
| **AC-51-B15 F6 空流=0** | `test_f6_emptyStream_returnsZero` | BLOCKING |
| **AC-51-B15 F6 多事件 span>0** | `test_f6_multiEvent_spanPositive` | BLOCKING |
| **AC-51-B16 F7 上中位数(偶数项禁插值)** | `test_f7_upperMedian_evenCount_noInterpolation` | BLOCKING |
| **AC-51-B16 F7 上中位数(奇数项)** | `test_f7_upperMedian_oddCount` | BLOCKING |
| **AC-51-B17 F7 退化(n=1 报不可定义)** | `test_f7_degenerate_singleEvent_reportsUndefined` | BLOCKING |
| **AC-51-B17 F7 n=2 MAD=0 合法** | `test_f7_twoEvents_madZeroIsLegal` | BLOCKING |
| **AC-51-B14(F3 侧)分母 0** | `test_f3_denominatorZero_reports00` | BLOCKING |
| **AC-51-B14(F3 负向)** | `test_f3_denominatorNonZero_not00` | BLOCKING |
| **AC-51-B18 F7 禁浮点统计量** | `test_f7_noFloatStatistics` | BLOCKING |

> **测试数**:`f6_f7_session_rhythm_test` = **10**(9 passed + 1 skipped)。
> ⚠️ 关键区分:生产代码(51 程序集)尚未实现,测试用自持谓词面。
> F6/F7 公式实现与 GDD `telemetry-analytics.md:323-349` 逐字对齐。
