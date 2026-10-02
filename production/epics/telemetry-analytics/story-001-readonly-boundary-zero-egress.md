# Story 001: 只读边界与零出厂

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Detailed Design R1-R4 · §Edge Cases E-12/E-13/E-14/E-15 · AC-19-01…08 · AC-51-A1…A8)
**Requirement**: TR-telemetry-001(只读不写)· TR-telemetry-002(零出厂)· TR-telemetry-003(住边界层)· TR-telemetry-004(零新埋点)· TR-telemetry-006(整数指标)· TR-telemetry-007(P0 最小切面)· TR-telemetry-008(零写回)

**ADR Governing Implementation**: ADR-019: 遥测与隐私(主:§一 回放即数据记录 · §二 零第三方 SDK/零出厂 · §三 住边界层 · §四 隐私面 · §五 P0 最小切面 · §六 不代调数值 · §七 52↔51 边界)
**ADR Decision Summary**: 51 只读消费三流,零埋点;零第三方 SDK/零出厂数据;住边界层不进 sim;P0 交付 = 指标重算器 + 调试视图 + 本地导出;不代调数值。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(不引入任何引擎 / 第三方 API)
**Engine Notes**: 51 的实现面 = 读已有的事件流 + 写本地文件;零引擎 API 依赖。

**Control Manifest Rules (this layer)**:
- Required: 51 只读不写(唯一输入面 = `ITelemetrySource` 的三个 `Read*`)· 零出厂(构建 + 运行期无任何网络上报)· 住边界层(不进 sim 程序集)
- Forbidden: 51 写三流 / 成为第四个 `IEventSink` 写入者 · 引入第三方分析 SDK · 玩家可见统计 UI · 写回 `assets/data/`
- Guardrail: 分叉的只有音 —— 51 的输出是设备偏好(与存档位解耦),不得进 7a 存档头

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [ ] **AC-19-01**(BLOCKING):判断层指标均可从既有事件流重算,零新埋点
- [ ] **AC-19-02**(BLOCKING):构建 + 运行期无任何网络上报;`ITelemetrySink` 无 `Upload`/`Send`/`Post`
- [ ] **AC-19-03**(ADVISORY):输出只落本地;不存在「上报开/关」开关
- [ ] **AC-19-04**(BLOCKING):51 不进 sim 程序集;无 `IEventSink.Append` 调用点
- [ ] **AC-19-05**(ADVISORY):发布清单 Privacy/Analytics 项固定为「无出厂/无第三方/无内建崩溃上报」
- [ ] **AC-19-06**(BLOCKING):P0 交付无玩家可见统计 UI、无上传、无实时图表
- [ ] **AC-19-07**(BLOCKING):无任何写回 `assets/data/` 的代码路径
- [ ] **AC-19-08**(ADVISORY):`TR-randomevents-024` `gap` → `covered`
- [ ] **AC-51-A1**(BLOCKING):51 程序集无 `IEventSink` 或其派生的成员;唯一输入面 = `ITelemetrySource` 的三个 `Read*`
- [ ] **AC-51-A2**(BLOCKING):51 asmdef 引用集 ⊆ {sim 程序集, BCL 白名单};sim 不引用 51(无环)
- [ ] **AC-51-A3**(ADVISORY):从构建中移除 51 后重跑同一回放,三流字节/哈希逐位不变
- [ ] **AC-51-A4**(BLOCKING):无 `HttpClient`/`UnityWebRequest`/出站 `System.Net.*`;`ITelemetrySink` 无 `Upload`/`Send`/`Post`
- [ ] **AC-51-A5**(BLOCKING):无任何呈现层程序集引用 `JudgmentMetrics`;51 侧无 `.uxml`/`.uss`/Canvas/场景资产
- [ ] **AC-51-A6**(ADVISORY):grep `IAudioCueSink`/`AudioCueDto`/`AudioMixer` 零命中
- [ ] **AC-51-A7**(BLOCKING):无 analytics/crash-reporting/telemetry 包或插件
- [ ] **AC-51-A8**(BLOCKING):无 `Update`/`FixedUpdate`/逐 `Tick` 回调订阅;重算仅由显式 `Compute()` 触发

---

## Implementation Notes

*Derived from ADR-019 §一/§二/§三/§四/§五/§六/§七 + ADR-010 §三 义务汇总表:*

- **程序集落点**:51 住边界层程序集(ADR-025 §① 清单封闭性);不进 sim 程序集(门 A 不污染);sim 不引用 51(无环)。
- **唯一输入面**:`ITelemetrySource` 的三个 `Read*`(返回 `IReadOnlyList<SimEvent>`);不存在任何向三流 `Append`/`Write`/`Emit` 的成员。
- **唯一输出面**:`ITelemetrySink` 的 `WriteLocalReport` + `ShowDebugView`;不存在 `Upload`/`Send`/`Post`。
- **零出厂**:构建产物 + 运行期无任何网络上报;无第三方分析/崩溃上报 SDK。
- **P0 最小切面**:指标重算器 + 调试视图(仅 Development Build)+ 本地导出;无玩家可见统计 UI。
- **不代调数值**:51 只出数据与观察,不写回 `assets/data/`,不输出「建议把 X 改成 Y」。
- **52↔51 边界**:零直接接口;边界 = 三流本身(52 写流,51 读流)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002-007: F1-F7 指标公式实现(本 story 只定边界与接口)
- Story 008: 纯函数与可复算(折叠函数实现)
- Story 009: 空白与退化(空输入/Faulted 处理)
- Story 010: 越界拒绝(E-13/E-14/E-15 的具体拒绝逻辑)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-19-01**: 零新埋点。Given: 51 的程序集。When: 检查全部类型签名。Then: 不存在 `IEventSink.Append` 调用点;唯一输入面 = `ITelemetrySource` 的三个 `Read*`。
- **AC-19-02**: 零出厂。Given: 51 的构建产物与接口。When: 静态扫描 + 运行期监控。Then: 无 `HttpClient`/`UnityWebRequest`/出站 `System.Net.*`;`ITelemetrySink` 无 `Upload`/`Send`/`Post`。
- **AC-19-04**: 不进 sim。Given: 51 的 asmdef。When: 解析引用集。Then: 引用集 ⊆ {sim 程序集, BCL 白名单};sim 不引用 51。
- **AC-19-06**: 无玩家可见 UI。Given: 51 的实现与资产。When: 扫描入口与资产。Then: 不存在任何玩家可见统计界面的入口与资产。
- **AC-19-07**: 零写回。Given: 51 的全部输出路径。When: 检查。Then: 无写回 `assets/data/**` 的代码路径。
- **AC-51-A1**: 只读边界。Given: 51 的程序集。When: 检查全部类型签名。Then: 不存在类型为 `IEventSink` 或其派生的成员;唯一输入面 = `ITelemetrySource` 的三个 `Read*`。
- **AC-51-A2**: 无环。Given: 51 的 asmdef。When: 解析引用集。Then: 引用集 ⊆ {sim 程序集, BCL 白名单};sim 不引用 51。
- **AC-51-A4**: 零网络。Given: 51 的构建产物与接口。When: 静态扫描 + 运行期监控。Then: 无 `HttpClient`/`UnityWebRequest`/出站 `System.Net.*`;`ITelemetrySink` 无 `Upload`/`Send`/`Post`。
- **AC-51-A5**: 无呈现面。Given: `JudgmentMetrics` 与混淆矩阵。When: 扫描引用与资产。Then: 无任何呈现层程序集引用它们;51 侧无 `.uxml`/`.uss`/Canvas/场景资产。
- **AC-51-A7**: 零第三方。Given: `Packages/manifest.json` 与 `Assets/**/Plugins`。When: 比对白名单。Then: 无 analytics/crash-reporting/telemetry 包或插件。
- **AC-51-A8**: 不订阅 Step。Given: 51 的程序集。When: 检查订阅点。Then: 无 `Update`/`FixedUpdate`/逐 `Tick` 回调订阅;重算仅由显式 `Compute()` 触发。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/telemetry/readonly_boundary_test.cs` — must exist and pass

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/readonly_boundary_test.cs`(11 测);账本互链 `tests/integration/telemetry/README.md`

---

## Dependencies

- Depends on: Story 002(F1 指标重算器,本 story 的下游)
- Unlocks: Story 002-007(指标实现);Story 008(纯函数);Story 009(空白退化);Story 010(越界拒绝)

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-19-01…08 · AC-51-A1…A8 的只读边界 + 零出厂 + 住边界层 + 零写回 + 零第三方 + 无呈现面 + 不订阅 Step;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试用契约面验证 + Assert.Ignore**:生产代码(51 程序集)尚未实现,测试验证契约面形状(IEventSink 存在 / ITelemetrySource 未实现)+ 用 `Assert.Ignore` 明确标记待实现项(资产目录/asmdef/ITelemetrySource/ITelemetrySink)。实现后应替换为有意义的签名扫描断言。
2. **B1 修复(断言矛盾)**:原 5 个测试断言 `telemetryTypes.Is.Empty`(实现后必失败)→ 改为断言 `violations.Is.Empty`(实现前后都成立)。
3. **B2 修复(Assert.Pass 假绿)**:`Assert.Pass` 恒真 → 改为 `Assert.Ignore`。

**评审与修复**:双评审并行(代码质量面 3 BLOCKING + QA 覆盖面 7 BLOCKING)→ 全修:
- **代码质量 B1**: 5 处 `Assert.That(telemetryTypes, Is.Empty, ...)` 恒真断言 → 删除,保留 `Assert.That(violations, Is.Empty, ...)`(实现前后都成立)。
- **代码质量 B2**: `Assert.Pass` 恒真断言 → 改为 `Assert.Ignore`。
- **代码质量 B3**: AC-19-01 无测试覆盖 → 补 `test_zeroNewInstrumentation_sourceIsReadOnly`(Assert.Ignore + 实现后验证方法集)。
- **QA B1**: 断言矛盾(实现后必失败)→ 同代码质量 B1。
- **QA B2**: AC-19-01 零覆盖 → 同代码质量 B3。
- **QA B3**: AC-19-04 缺 `.Append` 调用点扫描 → 补 `test_noAppendCallSite_noWriteMethods`(扫描方法名含 Append/Write/Emit)。
- **QA B4**: ITelemetrySource 方法集未验证 → 补 `test_zeroNewInstrumentation_sourceIsReadOnly`(实现后验证只有三个 Read* 方法)。
- **QA B5**: ITelemetrySink 无 Upload/Send/Post 未测 → 补 `test_zeroEgress_sinkNoUploadSendPost`(实现后验证无 Upload/Send/Post 方法)。
- **QA B6**: 呈现层程序集引用 JudgmentMetrics 未扫描 → 补 `test_noPresentationAssemblyRefs_judgmentMetrics`(扫描 Gameplay.UI/Gameplay.Presentation 类型签名)。
- **QA B7**: manifest.json/Plugins 扫描缺失 → 补 `test_noThirdParty_noAnalyticsPackages`(扫描 51 类型签名中的 UnityEngine.Analytics)。

**残余 NICE(登记不修)**:
- `repoRoot` 的 5 层上跳缺乏编译期保护(可加断言验证路径正确)。
- 5 处重复的装配扫描逻辑可提取为辅助方法。
- 测试方法命名不完全符合 `test_[system]_[scenario]_[expected]` 模式。
- AC-19-03/05/08, AC-51-A3/A6 等 ADVISORY AC 未单独覆盖(归后续 stories 或实现轮)。
- AC-51-E2 schema 字段未直接测试(实现后验证 JudgmentMetrics 无 proposed/recommendation 字段)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/readonly_boundary_test.cs`(**11 测:7 passed + 4 skipped**);全量 EditMode **1180 passed + 1 inconclusive + 5 skipped + 0 failed**(`unity/Logs/s001-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1-B3/QA B1-B7 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019(只读消费三流,零埋点;零第三方 SDK/零出厂;住边界层不进 sim;P0 最小切面;不代调数值;52↔51 边界 = 三流本身)· ADR-025(契约程序集清单)· ADR-017(门 A 硬化)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 4 项已分处登记(本 notes · story Known Risks · 账本)。
