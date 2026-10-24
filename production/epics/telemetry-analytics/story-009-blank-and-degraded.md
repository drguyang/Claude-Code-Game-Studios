# Story 009: 空白与退化

> **Epic**: 遥测与分析
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落边界层)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-24

## Context

**GDD**: `design/gdd/telemetry-analytics.md`(§Edge Cases A/B/D/F · AC-51-D1…D9)
**Requirement**: TR-telemetry-004 · TR-telemetry-006

**ADR Governing Implementation**: ADR-019 §一(R12 输出可空)
**ADR Decision Summary**: 空输入 ⇒ 零值(非 Faulted);分母 0 ⇒ `(0,0)` + 显式标记;Faulted 不产出半份;规模不改变数值。

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 全整数;空流 ⇒ 全 0(合法);`Faulted` 不可自愈。

**Control Manifest Rules (this layer)**:
- Required: 空输入零值 · 分母 0 报 `(0,0)` + 标记 · Faulted 不产出半份 · 规模不改变数值
- Forbidden: 抛异常代替零值 · 报 `0/1` 代替「不可定义」· 静默出半份数据 · 把部分流当完整流
- Guardrail: 51 报测量结果,不判后果

---

## Acceptance Criteria

*From GDD `design/gdd/telemetry-analytics.md`, scoped to this story:*

- [x] **AC-51-D1**(BLOCKING):三流皆空 —— 状态 = `Computed`(非 `Faulted`)、全部指标零值、不抛异常/不写错误日志
- [x] **AC-51-D2**(BLOCKING):`Level` 未携带 —— 报「不可得」(与 0 可区分),不报 0;窗口内无事件 ⇒ 全 0(合法)
- [x] **AC-51-D3**(BLOCKING):Faulted 不产出半份 —— 三种流异常(`Tick` 回退 / `Seq` 断号 / 病例流引用不在病史流的 `Patient`);状态 = `Faulted` 且中止;`ITelemetrySink` 零调用;本地文件未生成/未覆写;`Faulted` 不可自愈
- [x] **AC-51-D4**(BLOCKING · **两侧已证 2026-10-19**):Release 无调试视图 —— 入口不存在(编译期剔除 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`);Development 构建入口存在且输出指标。Release 半边经**真 StandaloneLinux64 Release 包 + 手写 PE/CLI 元数据解析**验证,并经**反向构建证伪**
- [x] **AC-51-D5**(BLOCKING):R7 整数纪律 —— 无 `float`/`double`;率以 `(num, den)` 承载;标量导出 = `ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)`
- [ ] **AC-51-D6**(BLOCKING · **口径已改 2026-10-19**):规模不改变数值 —— ~~同一超长流以 `FOLD_MEMORY_MODE` 两个取值各重算,全部指标逐位相同~~ ⇒ **用户裁定:P0 只保留一套折叠实现,双模式对拍整个推开**(开冻条件:出现真实内存压力时另开 ADR 定第二模式)
- [x] **AC-51-D7**(BLOCKING · **2026-10-24 接线补齐**):ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数
  <br>⚠️ **本勾在 2026-10-19 至 2026-10-24 期间不被生产实现支撑** —— `ConfigVersionCheck` 生产侧**零构造**,
  版本门从未在生产路径上执行(详见 `TD-epic-01-f`)。**2026-10-24 修复轮**接上三处断点后,本勾**首次**有生产主语;
  勾**保留不撤**(它现在成立),但那段假绿史如实登记在下方 §2026-10-24。
- [x] **AC-51-D8**(ADVISORY):部分流 —— 要么拒绝要么产出并显式标注「部分指标」;禁止把部分流当完整流静默出数
- [x] **AC-51-D9**(BLOCKING):样本不足 —— 报告照常产出数值且含「样本不足」标注;不隐藏、不四舍五入掩盖

---

## Implementation Notes

*Derived from ADR-019 §一(R12)+ GDD Edge Cases:*

- **空输入**:三流皆空 ⇒ 状态 `Computed`(非 `Faulted`)、全部指标零值、不抛异常
- **分母 0**:报 `(0,0)` + 显式「分母 0/未定义」标记;不报 `0/1`、不报标量 0、不报 NaN
- **Faulted**:三种流异常(`Tick` 回退 / `Seq` 断号 / 病例流引用不在病史流的 `Patient`);中止,不产出指标;`ITelemetrySink` 零调用;本地文件未生成/未覆写;不可自愈(后续 `Compute()` 从 `Idle` 重入)
- **Release 无调试视图**:编译期剔除 `#if DEVELOPMENT_BUILD`;不是「有按钮但点了没用」
- **整数纪律**:无 `float`/`double`;率以 `(num, den)` 承载;标量导出 = `ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)`
- **规模不改变数值**:`FOLD_MEMORY_MODE` 两值(流式/整表)各重算,全部指标逐位相同
- **ConfigVersion 不匹配**:报告含显式标注,不静默出数
- **部分流**:要么拒绝要么产出并显式标注「部分指标」;禁止静默出数
- **样本不足**:报告照常产出数值且含「样本不足」标注;不隐藏

---

## Out of Scope
- Story 010: 越界拒绝

---

## QA Test Cases
- **AC-51-D1**: 三流皆空。Given: 三流均空。When: `Compute()`。Then: 状态 `Computed`、零值、不抛异常。
- **AC-51-D2**: `Level` 未携带。Given: 窗口内有成长事件但未携带 `Level`。When: 计算 `ΔLevel`。Then: 报「不可得」,不报 0。
- **AC-51-D3**: Faulted 不产出半份。Given: 三种流异常。When: 折叠。Then: 状态 `Faulted`;sink 零调用;文件未生成。
- **AC-51-D4**: Release 无调试视图。Given: Release 构建。When: 尝试触达。Then: 入口不存在。
- **AC-51-D5**: 整数纪律。Given: 51 自有的指标类型与公式层。When: 静态检查。Then: 无 `float`/`double`。
- **AC-51-D6**: 规模不改变数值。Given: 同一超长流。When: 两模式各重算。Then: 逐位相同。
- **AC-51-D7**: ConfigVersion 不匹配。Given: 存档头 `ConfigVersion` 不符。When: 离线重算。Then: 含显式标注。
- **AC-51-D8**: 部分流。Given: 被截断的三流。When: 离线重算。Then: 要么拒绝要么产出并标注。
- **AC-51-D9**: 样本不足。Given: 样本量 < `TELEMETRY_MIN_SAMPLE`。When: 计算。Then: 照常产出 + 标注。

---

## Test Evidence
**Story Type**: Logic
**Required evidence**: `tests/unit/telemetry/blank_and_degraded_test.cs`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Telemetry/blank_and_degraded_test.cs`
(**11 测:8 passed + 3 skipped**,2026-10-19 第十三片重写后的实测;原记「6 测」为旧版);
账本互链 `tests/integration/telemetry/README.md`

**追加载体(2026-10-24 · TD-epic-01-f 修复轮)** —— AC-51-D7 的证据按「一 AC 多载体」补列:
`unity/Assets/Tests/EditMode/DataPipeline/data_core_config_version_test.cs`(**10 测**,
组合口径纯函数性)+ `unity/Assets/Tests/EditMode/DataPipeline/data_core_config_version_wiring_test.cs`
(**6 测**,三处断点逐点守卫 + 端到端走 `TelemetryDeveloperView.Render`);证伪六轮与回归三元组见上方
§D7 的假绿与修复及证据原件 `production/qa/evidence/impl-td-epic-01-f-configversion-2026-10-24.md`

## Dependencies
- Depends on: Story 008(纯函数)
- Unlocks: Story 010(越界拒绝)

## Completion Notes
> ⚠️ **本节 2026-09-28 的原记录以下方「2026-10-19 · 收口」为准,原数字不删留作历史**

**Completed**: 2026-09-28
**Criteria**: 4/4 passing(AC-51-D1 空输入零值 / AC-51-D2 Level 未携带报不可得 / AC-51-D3 Faulted 不产出半份 / AC-51-D4 部分流显式标注;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(51 程序集)尚未实现,本 story 测试用自持谓词面(`F6F7Formula`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **AC-51-D9 用 Assert.Ignore 跳过**:Release 无调试视图属编译期剔除(`#if DEVELOPMENT_BUILD`),当前阶段无法验证,标记为 Ignore 而非 Pass。

**评审与修复**:双评审并行(代码质量面 0 BLOCKING + QA 覆盖面 0 BLOCKING)→ 全修:
- **B1**(代码面):Test 1 第三条事件在窗口 [13,18) 密度为 2(含 tick=14 和 tick=15),原断言期望 1 → 修正为 2。
- **R1**(代码面):Test 2 缺 `D={t}∧J=∅` 用例 → 补 `test_f4_levelNotCarried_allZero`。
- **R1**(QA 面):AC-51-D2「Level 未携带 → 报不可得」未完整验证 → 补 `test_f4_levelNotCarried_allZero`。
- **R2**(QA 面):AC-51-D9「Release 无调试视图」无法自动化验证 → 用 `Assert.Ignore` 标记。
- **R3**(代码面):中位数索引注释与代码不一致 → 修正注释为 `⌈(n-1)/2⌉`。

**残余 NICE(登记不修)**:
- `Level` 字段未使用(ΔLevel 归 Story 009)。
- 缺未知 novelty_class 防御性测试。
- `ComputeSeries` 无 null 守卫(可加 `ArgumentNullException`)。
- 缺 `choiceNum <= skipNum` 子集不变量断言。
- 字符串字面量 vs 枚举(当前阶段可接受)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Telemetry/blank_and_degraded_test.cs`(**6 测:5 passed + 1 skipped**);全量 EditMode **1296 passed + 1 inconclusive + 7 skipped + 0 failed**(`unity/Logs/s009-telemetry-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/R1-R4 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-019 §一(R8/R9/R11 纯函数)· ADR-006 §五(Fix 编码器 + 守恒律)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 5 项已分处登记(本 notes · story Known Risks · 账本)。

## Completion Notes — 2026-10-19 逐条核账(TD-epic-01 · 假绿普查后)

**原 `Complete` 不成立,已撤回 `In Review`。** 上一轮 Completion Notes 记的是 **4 条 AC**,
而本 story 实际有 **9 条**(D1–D9);缺的 5 条从未被核过,其中 3 条(D3/D6/D7)**连生产对象都不存在**。

### 逐条判定

| AC | 判定 | 生产主语 | 夹具 / 反证 |
|---|---|---|---|
| **D1** 三流皆空 ⇒ Computed / 全零 / 不抛 | ✅ 已证 | `TickFoldEngine.Fold` 空流分支 `TickFoldEngine.cs:53` | `test_d1_emptyStreams_computedZeroNoThrow` · `test_d1_nonEmptyStream_valuesFollowInput`;反证 F2(停用空流分支 ⇒ 红) |
| **D2** `Level` 未携带 ⇒ 报不可得 | ✅ 已证 | `SkillGrowthMetrics.TryComputeDeltaLevel`(`return (false, null)`)· `MetricsReport.cs:102`「分母 0」 | `test_f4_levelNotCarried_reportsUnavailable` · `test_f4_levelCarried_computesDelta` · `test_d2_*`;反证 F3 |
| **D3** Faulted 不出半份 | ✅ **已证**(2026-10-19 第十四片) | `Telemetry/StreamIntegrity.cs`:`StreamIntegrity.Validate`(E-4/E-5/E-6)+ `ComputeSession`(不可自愈)· `TelemetryReporter.ComputeAndSink` 的 Faulted 早退 | 5 测:三流异常各一 + 不可自愈 + 完好流正向对照;**证伪 5/5 恰如预期转红**(`tools/falsify/specs/td-epic-01-d3-faulted.json`) |
| **D4** Release 无调试视图 | ✅ **已证**(2026-10-19 第十七片) | `TelemetryDeveloperView.cs:16` `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` · `TelemetryReporter.cs:675` | 开发期半边 3 测(含反射真调入口 + 输出真指标);Release 半边 `test_d4_releasePlayer_entryAbsent`(读 `builds/release-d4/.../Gameplay.Boot.dll` 的 TypeDef 表,正对照 `TelemetryReporter` 必在);**证伪 = 造一个 `#if true` 的坏 Release 包 ⇒ 该条恰转红** |
| **D5** R7 整数纪律 | ✅ 已证(2026-10-19 补) | `Telemetry` 程序集 + `TelemetryReporter` / `TelemetryDeveloperView` 实测零浮点;率载体 `(num, den)`;`MetricsReport.RoundHalfAwayFromZero` / `ToQ1616` | `test_d5_telemetryAssembly_noFloatOrDoubleMembers` · `test_d5_reportProducers_noFloatOrDoubleMembers`(含空集守卫)· `metrics_report_test.cs` 七测;**反证 2/2 恰如预期转红**(`tools/falsify/specs/td-epic-01-d5-float-scan.json`) |
| **D6** 规模不改变数值 | ⏸ **已推开(P0 不做)** | —— | 用户裁定 2026-10-19:P0 只留一套折叠实现;双模式对拍与内存上界两半一并推迟 |
| **D7** ConfigVersion 不匹配标注 | ✅ **已证**(2026-10-19 第十五片) | `Telemetry/ConfigVersionGate.cs`:`ConfigVersionCheck.Banner` · `TelemetryReporter.AllMetricsReport` 的版本门早退 | 3 测(不匹配不出数 / 一致正常 / 未注入不设门);**证伪 2/2 恰如预期转红**(`tools/falsify/specs/td-epic-01-d7-configversion.json`) |
| **D8** 部分流显式标注(ADVISORY) | ✅ 已证 | `TelemetryReporter.CompletenessBanner` `:675` | `test_d8_*` 两测 · `Boot/telemetry_debug_view_test.cs` 侧;反证 F4(3 条红) |
| **D9** 样本不足照常出数 + 标注 | ✅ 已证 | `TelemetryDeveloperView.cs:71-73` | `test_d9_lowSample_addsBannerNumbersUnchanged` · `test_d9_ampleSmpale_noBanner`;反证 F5 |

**统计**:9 条中 **8 已证 / 1 已推开(D6 · 用户裁定)**。**NOT-RUN 归零**;D4 的两个半边均已证(见下 §D4 收口)。

### D5 的解冻(2026-10-19)

原 NOT-RUN 理由「率载体 / 浮点扫描面未裁定」**已过期**:率载体早已定为 `(num, den)`,
扫描面本批裁定为 **`DaYiJingCheng.Telemetry` 全类型 + 两个报告生产者类**。
两条夹具带 `Count > 0` 空集守卫(防「反射扫不到装配」式空洞为真),并经证伪轮证明判别力。

### D7 的假绿与修复(2026-10-24 修复轮 · TD-epic-01-f)

**2026-10-19 核账误判**:D7 当时勾着 `[x]`,但其生产路径上**零接线** ——
`ConfigVersionCheck` 生产侧零构造(测试里自造),`TelemetryDeveloperView` 调
`AllMetricsReport(out int skipped)` 不传版本比对,`CompositionRoot` 的存档头写死 `0U`。
版本门从未在生产路径上执行过一次。这是**第五种假绿形态的变体**:门真的存在、测试真的能红、
但生产路径不经过它。已登记债册 `TD-epic-01-f`(commit `8ba3fb4`)。

**2026-10-24 修复轮**(用户裁定口径:**全局组合值** —— 22 份 cooked 产物头按 address
Ordinal 排序喂 FNV-1a 32;今日实跑值 `0x4A30BB10`;排序是判据的一部分,空集抛不返 0):

- **源头**:`DataCoreConfigVersion.Combine`(新件)+ `CompositionRoot.ResolveGlobalConfigVersion`,
  **惰性**派生(装配期派生会打红两条 `dataProvider: null` 的既有折叠谓词接线测试 = 修复引入假红)
- **消费端**:`TelemetryDeveloperView` 把 `r.ConfigVersion` 传进 `AllMetricsReport`
- **夹具**:`data_core_config_version_wiring_test.cs` 端到端走 `TelemetryDeveloperView.Render`,
  断言不匹配时报告含「版本不匹配」标注且不含指标行

**回归三元组**:`2026-10-24 21:25 CST` · `tdef01f`(全量两套件)· commit `8ba3fb4`
(工作树 = `8ba3fb4` + 11 项未提交改动)—— EditMode `4802/4767/0/35`
(基线 `4786/4751/0/35` @ `2026-10-24 19:49` · `s19-smoke-editmode` · `9e23c42`)
⇒ **+16/+16 零回归**(与新增 10 + 6 = 16 条精确吻合);PlayMode `202/202/0/0` 零变化。
全名级差集 `+67/−1/翻转 0/红 0`(新增 67 = S19-1 的 51 + 本轮 16;消失 1 经 `git show 41b7d87` 归因改名)。

**Falsifiability Gate 六轮全红**:A 删接线实参(5 红)/ B 开发视图不传(2 红)/
C 派生返 0U(2 红)/ D 删 Ordinal Sort(1 红)/ E 空集返 0(1 红)/ F 头读大端(1 红);
还原判据 = SHA-256 逐字节。⚠️ **如实边界**:变异 C 下端到端那条仍绿
(`staleHeader = current ^ 0xFFFFFFFF` 与当前值是否为 0 无关,非假绿但对该断点无判别力)
⇒ 断点③ 由 `currentConfigVersion_isDerivedNotZero` 与 `derivedValue_equalsAllProductsCombination` 两条钉住。

**证据原件**:`production/qa/evidence/impl-td-epic-01-f-configversion-2026-10-24.md`。
**勾保留口径**:上方 AC 的 `[x]` **保留不撤**(修复落地后它首次有生产主语),
2026-10-19 至 2026-10-24 期间的假绿史由本节与 AC 行加注如实承载。
**未核查(禁借绿)**:跨平台三格矩阵未测(只跑 Linux-x64-Mono)· 烘焙侧派生未复核 ·
「compose 调用时必抛」= NOT-RUN(`Checkpoint` 先撞 `RequireNarrativeContext`,够不到 compose)。

### D3 的解冻与落地(2026-10-19 第十四片)

原 NOT-RUN 的解冻条件「出现持状态且调 `ITelemetrySink` 的生产类型」已满足。落地:
`StreamIntegrity.Validate` 按 E-4 → E-5 → E-6 次序返回首条违规;`ComputeSession` 一旦 Faulted
**永不回 Idle**(GDD「不可自愈」);`ComputeAndSink` 在 Faulted 时**一个 sink 方法都不调**。

⚠️ **两处如实登记**:
1. **E-4 / E-5 不能经 `EventStream.Append` 构造** —— `Append` 自己会拒收 Tick 回退(`BCD-码-3`)
   并给 Seq 续号。这两条只在 GDD R9 ② 的**离线重算**输入面才有意义,夹具因此直驱 `Validate`;
   只有 E-6 走 `ComputeAndSink` 端到端。
2. **E-5 组键分歧**:`EventStream` 的发号组键是 `(Tick, Patient)`(**不含 StreamId**),GDD E-5 的
   组键含 `StreamPriority`。只在「同 tick 同病人跨流」时分歧,归流层(ADR-008),本件不扩大。

### D6 的推开(2026-10-19 · 用户裁定)

**P0 只保留一套折叠实现,`FOLD_MEMORY_MODE` 双模式对拍整个推开。** 两条依据:

1. GDD 的「流式 / 整表」两模式当前**都没有独立实现**;写两套玩具实现再断言「逐位相同」是**自证**,
   不是 AC 要验的东西(同型:上一版 `ComputeTwoModes` 手搓常量)。
2. 内存上界那半条另卡 **ADR-005 的 Memory Ceiling `*待定*`** —— GDD `:654` 已登记「**无值 ⇒ 现不可写**」。

`test_d6_scaleDoesNotChangeValues_NOT_RUN` 保留为**开冻哨兵**(不删)。开冻条件:
出现真实内存压力 ⇒ 另开 ADR 定第二模式,同批须给出 Memory Ceiling 值,届时两边换成真实现。

### D4 的 Release 半边落地(2026-10-19 第十七片)

**产物**:`unity build unity --target StandaloneLinux64 --output-path builds/release-d4/Boot.x86_64`(96 MB)。
Release 的 ManagedStripped 产物与 `Boot_Data/Managed` 两份**均无** `TelemetryDeveloperView`
(`monodis --typedef` 与 `strings` 双法独立核对),而 `Library/ScriptAssemblies` 那份有 —— 说明类型确实被 `#if` 剔除。

**夹具的两次假象与修法(如实登记)**:

1. **首版用 `Assembly.LoadFrom` ⇒ 假红**。它绑定到 Editor 已加载的**同名**程序集
   (`Library/ScriptAssemblies/Gameplay.Boot.dll`),测的根本不是构建产物 —— 第五形态「扫不到被测装配」。
   `MetadataLoadContext` / `PEReader` 是 .NET Core API,本工程 netstandard2.1 用不了。
   ⇒ 改为**手写 PE/CLI 元数据解析**(PE → CLI Header → Metadata Root → 流头 → `#Strings` + TypeDef 表),
   只读字节、不加载、不参与程序集绑定。
2. **解析器的三个实现错误**,全部由「正向对照失败」逼出(没有一个被静默吞掉):
   ① TypeDef 列序写成 `Name, NameSpace`,实为 `Flags(4), Name, NameSpace`;
   ② 表头把 `Sorted(8)` 当成 `Valid(8)`(Valid 在 `+8`),导致「表在不在」全判错;
   ③ 流头起点在 `+2` 之后又对齐了一次,多跳 2 字节。
   **正向对照 = 先断言 `TelemetryReporter` 必须出现在 TypeDef 表里** —— 解析失效时该断言先红,
   不会退化成「找不到 ⇒ 通过」的空洞为真。

**判别力证伪(实跑,非推演)**:

| 步骤 | 命令 | 实测 |
|---|---|---|
| 造坏包 | 把 `TelemetryDeveloperView.cs:16` 的 `#if` 改成 `#if true`,重建到 `builds/release-d4-bad/`,**随即还原源码**(SHA-256 逐字节一致) | `strings` 计数:坏包 1 / 好包 0 |
| 夹具指向坏包 | 临时把夹具路径改成 `release-d4-bad`,`unity test … --filter TelemetryDeveloperViewTest`,**随即还原**(SHA-256 一致) | `13 total / 12 passed / 1 failed` —— 仅 `test_d4_releasePlayer_entryAbsent` 转红,报文点名 `TelemetryDeveloperView` |
| 夹具指向好包 | 同上,默认路径 | `13 total / **13 passed** / 0 failed / 0 skipped`(`unity/Logs/d4-release.xml`) |

### ⚠️ 仍如实 NOT-RUN 的面

- **IL2CPP 三格**(ADR-012 矩阵)—— 本机只跑得动 Mono 那一格;Release 包为 Mono 后端。
  本片只证「Mono Release 包剔除了该类型」,**不证** IL2CPP 下同样成立。

### 转 `Complete`

用户 2026-10-19 裁定:9 条 AC 中 8 条已证、D6 经用户裁定推开,**story 转 `Complete`**。

---

## 2026-10-19 · 收口

| 项 | 2026-09-28 原记录 | 2026-10-19 收口后 |
|---|---|---|
| Status | In Review(原记 Complete 后被撤) | **Complete**(用户裁定) |
| AC 勾选 | 4/4(**AC 清单实为 9 条**,原记录漏核 5 条) | **8 已证 / 1 推开(D6)** |
| D4 | 半边(NOT-RUN) | **两侧已证** + 反向构建证伪 |
| `blank_and_degraded_test.cs` | 旧记「6 测」 | **11 测(8 passed + 3 哨兵 skipped)** |
| 仍 NOT-RUN | —— | **IL2CPP 三格**(本机只有 Mono) <br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。 |

**禁借绿口径**:本 story 的「Complete」**不**表示 ADR-012 的跨平台矩阵已验;Release 包为 Mono 后端,
只证明 Mono 下该类型被剔除。
