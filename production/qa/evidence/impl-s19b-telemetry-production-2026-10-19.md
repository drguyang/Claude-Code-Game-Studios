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

## 三·六、第六片:F2–F7 接线 + 全量判据(2026-10-19)

**接线前状态(如实)**:第五片只接了 **F1**;`AllMetricsReport` 自陈「F3–F7 未接线」,
F2 的 `MisdiagnosisReport` 生产实现虽在,但**没有任何测试调用过它**。本片补齐 F2–F7。

| 片 | 生产方法 | 还原口径(出处) |
|---|---|---|
| F2 | `MisdiagnosisReport` | truth = `CaseClosed.DiseaseSet` 位掩码 bit+1;judged = `LexiconId`(已有实现,本片补判据) |
| F3 | `SkipRateReport` | **只由 `EmergencyAttempt` 还原**(10 GDD 规则十二:`Played ⇔ Manual` · `Skipped ⇔ Skip`;`NotOffered` = 无意图事件) |
| F4 | `SkillGrowthReport(skill, window?)` | `SkillGrown`;`Level = LevelNotGrown(-1)` ⇒ **不可得**(AC-51-D2) |
| F5 | `DifficultyCurveReport(t0, S, W)` | 三流合流,流别派生自 `StreamRouting.Of`(ADR-024 单一真源,**不另抄白名单**) |
| F6/F7 | `SessionRhythmReport(GAP_THRESHOLD)` | 三流合流 tick 序列 |

**参数纪律(禁替用户调参)**:`W`/`S`/`t_0`/`GAP_THRESHOLD`/`GROWTH_WINDOW` 全部 `*待定*`(GDD Tuning Knobs)
⇒ 生产方法**无默认值**,一律由调用方传;`AllMetricsReport` 只汇总**不需传参**的三项(F1/F2/F3),
并显式自陈 F4–F7 须调用方传参(`test_wired_allMetrics_admitsUnwired` 钉住)。

**如实登记的欠账(F3)**:`cause`(玩家选择 / 降级)住**结算**事件 `EmergencyTreatmentApplied`,
与意图事件的关联须 (Tick, Actor) 对齐 —— **枚举名归 10 的 GDD,关联逻辑归 10 实现轮**。
本片:F3 只由意图事件判 `Played/Skipped`,`cause` 一律 null ⇒ `SkipByChoice` 恒 0,
`test_wired_f3_causeNotInvented` 钉住「**不猜**」(若有人硬塞结算事件条数进 SkipByChoice 即转红)。

**F5 序列长度的实测订正(两轮)**:序列长度 = `f(t_0, S, Tick_max)`、**与 W 无关**
(首版夹具按「三窗」写 ⇒ 红;改为按实际窗数断言,并把判别力锚在**半开窗边界**(tick=W 不进 `[t_k,t_k+W)`)。

**F7 上中位数偏差(~~登记为遗留项,本批不擅改~~ **✅ 2026-10-19 第十片已结清** —— 见 §三·十)**:
生产式(原 `F6F7Formula` 逐字上移)`sorted[(n-1+1)/2-1]` ≡ `⌈m/2⌉-1`(`m` = Δ 项数),
n=5(m=4)时取 20。当时把这段判为「与 GDD 不符」,依据是 GDD `:338` 的**字面** `Δ_(⌈n/2⌉)`
(按事件数读 ⇒ 第 3 项 ⇒ 30)与夹具注释「idx = ⌈m/2⌉−1 ⇒ idx=2」。
**第十片实测:生产式符合 `AC-51-B16` 的 `⟨a₁a₂a₃a₄⟩ ⇒ a₂`;错的是 GDD 公式符号 `n` 与那条注释(漏减 1)。**
原文保留作闭环记录。

**证伪轮(改生产码 → 跑 → 同命令还原 + SHA-256 校验)4/4 转红**:

| 变异 | 改坏什么 | 转红的用例 |
|---|---|---|
| M-fz-2 | F4 不再把 `LevelNotGrown` 映射为「不可得」 | `test_wired_f4_levelSentinel_isUnavailableNotEmptyZero` |
| M-fz-3 | F5 三流一律标 History(流派) | `test_wired_f5_threeStreamsSeparate` |
| M-fz-4 | F7 `GapCount` 恒 0 | `test_wired_f7_upperMedianAndMad` |
| M-fz-5 | F3 Method 判据反判 | `test_wired_f3_skipOverOpportunity` |

⚠️ **两处过程失误(留档)**:
① 只读不变量门(名级含 `Append`/`Emit`)命中 `DecodeJudgment`,**与「未接线」自陈断言同一次运行里互相抵消**
⇒ 红 2 条、绿自欺。处理:**改生产命名**(`Decode*Record`)而非放松判据;判据本身注释其过宽面。
② 全量回归：`unity test` 只产出 `unity/test-results.xml`,第一次脚本读错了相对路径 ⇒ 「无结果」误报;
改读正确路径后得数。

**回归**:`3839 / 3802 / 1 / 36`(`unity/Logs/s19d-f3f7-wiring-full.xml`)vs 本批前 `3819 / 3782 / 1 / 36`
⇒ total **+20** = 新增测数(F2 6 + F3–F7 14);唯一红 = `TD-player-01` 已知间歇(`MovementFeedTest`)。**零回归**。

---

## 三·七、第七片:调试视图(ADR-019 §五 第三项交付 · 2026-10-19)

**接线前状态(如实)**:ADR-019 §五 的三项 P0 交付里,指标重算器 ✓、导出 ✓,**调试视图零字零代码**
—— 这正是 `OQ-51-1`(2026-10-06 已裁:两处分立 · 51 侧纯文本)已裁但**未落**的那一项。

| 件 | 落点 |
|---|---|
| 视图本体 | `unity/Assets/Gameplay.Boot/TelemetryDeveloperView.cs`(新)—— **整个类型包在 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 内**(承 `Skeuomorphic/DevDebugView.cs` 先例) |
| 预览口径 | `TelemetryPreviewRequest`(7 个可空旋钮;**不代填** —— 未给即「未给(未算 —— 旋钮待用户调)」) |
| 显式入口 | `TelemetryReporter.RenderDeveloperPreview(request, sink)` —— 同样在条件编译内 |
| 默认入口 | `TelemetryReporter.RenderDeveloperSummary(sink)` —— 只收 sink、旋钮全 null;供 DevBuild 键位 / 控制台命令调用 |

**为什么入口住 `Gameplay.Boot` 而不住 `Telemetry` 程序集**:`Telemetry` 契约程序集**所有构建都编译**
(`noEngineReferences: true`),把视图放进去 = Release 玩家构建里也有入口。放进 Boot 并整体条件编译
⇒ Release 的剔除是**编译期**的,不是「有按钮点了没用」(AC-51-D4)。

**口径标注(AC-51-D1/D8/D9)**:
- 空流 ⇒ 头部 `# ⚠️ 空流:… 「空输入零值」(Computed,非 Faulted)`;非空但 `ScorableCount == 0` ⇒
  `# ⚠️ 部分指标:…`(`CompletenessBanner`,由 `AllMetricsReport` 自带,**视图不前置第二份**)。
- 样本不足(`ScorableCount < MinSample`)⇒ 只**加一行标注**,正文数值字段逐字不变(判据 `test_d9_lowSample_addsBannerNumbersUnchanged`
  断言带/不带门槛两版 F1 行**字符串相等**)。

**⚠️ 如实登记(禁借绿)**:
1. **入口在生产装配里尚无调用点** —— DevBuild 里怎么唤(键位 / 控制台命令)未裁 ⇒ `RenderDeveloperSummary`
   今天**只由夹具驱动**。视图本体 (代码 / 判据 / 参数纪律) 已落,「玩家开发者可达」这一半**未落**。
2. **AC-51-D4 的 Release 半边 NOT-RUN** —— 须 Release player 构建后反射扫描;本机仅 EditMode
   (宏含 `UNITY_EDITOR`)⇒ 以 `test_d4_releasePlayer_entryAbsent_NOT_RUN` 哨兵钉住,**不记绿**。
   本片可自动化的半边 = 源文件形态(整类型在条件编译内 + 入口在同一块内)。
3. `CompletenessBanner(f1Faulted)` 的 `f1Faulted` 恒 false(`Folded` 的 Faulted 检出归 story-009 轮)。

**判据** `Tests/EditMode/Boot/telemetry_debug_view_test.cs` **12 条**(11 绿 + 1 NOT-RUN),全部打真链路
(真 `EventStream` + 真 Encoder/Decoder + 真 blob 池;记录型 `RecordingSink` 只替 IO 面):
D4 源形态 · D4 反射面(`Telemetry` 程序集零 Preview/DebugView 成员)· D4 NOT-RUN 哨兵 · 入口交 sink 且各段齐全 ·
参数纪律(三项「未给」)· 默认入口同口径 · D8 空流 · D8 部分指标 · D9 样本不足数值不变 · D9 达标不加标 ·
A5 零呈现引用 · E1 零呈现资产。

**证伪轮(改生产码 → 跑 → 同命令还原 + `sha256sum -c` 基线校验)8/8 转红**:

| 变异 | 改坏什么 | 转红的用例 |
|---|---|---|
| M-dv-1 | 视图文件 `#if` 改为恒真 | `test_d4_viewSource_entirelyInsideConditionalCompilation` |
| M-dv-2 | 入口 `#if` 改为恒真 | 同上 |
| M-dv-3 | `empty` 恒 false | `test_d8_emptyStream_bannerSaysZeroIsLegit` |
| M-dv-4 | `partial` 恒 false | `test_d8_nonEmptyWithoutScorable_flagsPartial` |
| M-dv-5 | 样本不足标注不打 | `test_d9_lowSample_addsBannerNumbersUnchanged` |
| M-dv-6 | 入口不交 sink | `test_entry_writesToSink_withAllSections` + `test_summaryEntry_defaultParamsAreAllNotGiven` |
| M-dv-7 | 视图代填 `GAP_THRESHOLD ?? 100`(条件与取值一并改) | `test_notGiven_params_areStatedNotDefaulted` + `test_summaryEntry_…` |
| M-dv-8 | 默认入口代填 GAP_THRESHOLD(第二形态) | `test_summaryEntry_defaultParamsAreAllNotGiven` |

⚠️ **证伪轮自身抓到三处夹具/变异缺陷(逐条留档,均是「绿但什么都没测」的形态)**:
① **判据被注释满足** —— 头注里逐字写着 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 作说明,
首版判据扫全文 ⇒ **删掉真 `#if` 也命中**(M-dv-1 首轮不红)。修法 = `CodeOnly()` 去注释行后判。
② **条件表达式未钉** —— `#if` 改成 `#if true` 后,「块尾在 EOF」「类型在 #if 之后」全部仍成立 ⇒ 判据仍绿。
修法 = 补「条件行须含 `UNITY_EDITOR` 与 `DEVELOPMENT_BUILD`」+ 块尾去 `}` 后为空。改后 M-dv-1 转红。
③ **M-dv-7 是假变异** —— 只把 `.Value` 改成 `?? 100` 而不改 `HasValue` 判定,`GapThreshold == null` 时
**根本走不到那一行** ⇒ 行为不变、必然不红。修法 = 条件与取值一并改(`if (true)` + `?? 100`)。
**同族纪律(承 S19b 的「变异没转红先分清夹具弱还是变异假」):两者都要修,但不能混为一谈。**

**本片抓到的两个真缺陷(都出在自己刚写的代码上)**:
① **完整性头重复两份** —— 视图自己前置了一次 `CompletenessBanner`,而 `AllMetricsReport` 已自带 ⇒ 正文里
「流事件 N 条」出现两次,第二份还与「未给」口径脱节。修法 = 视图**不前置**;补 `test_entry_writesToSink_…`
内「`# 流事件` 行只许出现一次」的判据。
② `if (sink == null)` 位于 `Render` **之后**(空 sink 时先渲染再抛)⇒ 前置。
③ ⚠️ **过程失误**:证伪批被中断后,首条变异(`#if` → `#if true`)留在盘上 —— 第二次基线跑出 1 红才发现。
处理 = 从变异表恢复并**以 `sha256sum -c` 基线文件**复核后才续跑;脚本随后加了「开跑前基线自检 +
`finally` 内 `sha256sum -c`」两道。**教训:证伪批被中断 ⇒ 先核 SHA 基线,再跑任何基线。**

**回归**:`3851 / 3813 / 1 / 37`(`unity/Logs/s19e-debugview-full.xml`)vs 本片前 `3839 / 3802 / 1 / 36`
⇒ **+12 total = +11 passed + 1 skipped**,逐项等于新增测数(12 条:11 绿 + 1 NOT-RUN);唯一红 = `TD-player-01` 已知间歇(`MovementFeedTest`)。**零回归**。

---

## 三·八、第八片:F3 `cause` 关联(2026-10-19)

**接线前状态(如实)**:`SkipRateReport` 的「玩家选择」分子恒 0 —— `ReduceActions` 建动作实例时
`Cause = null`,且**没有任何代码去填**。`cause` 住结算事件 `EmergencyTreatmentApplied.cause`,
而 F3 的每个分子按**意图事件**(`EmergencyAttempt`)计 ⇒ 两者必须关联。
**同批发现并修复一处生产 bug**:`EmergencyManualDriver.TryAttempt` 原先**硬编码 `cause: 0`**
⇒ 即便读侧关联做对了,AC-10-21(BLOCKING)的「纯键鼠 ⇒ `cause = 降级`」半边在生产里**恒假**。

GDD 出处:`emergency-procedures.md` 规则十二 `:464-469` / Edge Cases `:728` / AC-10-21。

### 写侧(10 → 流)

| 件 | 落点 |
|---|---|
| 判据接口 + 实现 | `unity/Assets/Gameplay.Input/EmergencyDeviceReadiness.cs`(新,含 `.meta`)—— `IEmergencyDeviceReadiness.IsAnalogChannelAbsent`。**住 3**(判据归通道拥有者),Boot 只转发 bool ⇒ 零新增引用边 |
| 写侧解析 | `EmergencyManualDriver.ResolveCause()` + `BindDeviceReadiness()`(单次注入,重复抛);`TryAttempt` 改传 `ResolveCause()` |
| 生产挂接 | `BootRoot.cs` 启动序 **2.9**:构造 `DeviceStateManager`(**首个生产实例**)+ `Attach()` → 注入 driver;`OnDestroy` 卸挂 |
| 旋钮种子 | `CompositionRoot` 六个 `EmergencyDeviceSwitchSeed*` 常量(值与 `assets/data/input_device_switch.json` 同) |

**判据口径(三条,均已写进文件头注)**:
1. **按能力而非最近输入** —— `PadOnly` / `Mixed` / `XrActive` ⇒ 有模拟量通道。Mixed 下玩家用手柄时通道是通的,
   记降级会把「有通道却没用」误报成「无通道」。
2. **设备态未知(未 Attach / `NoDevice`)⇒ 判「无模拟量」** —— 降级如实可见,不静默记成「玩家选择」把读数抹掉。
3. **未注入 ⇒ 夹具默认「玩家选择」** —— 显式记为默认(由 `test_cause_deviceReadinessNotBound_defaultsToPlayerSelection`
   守卫),生产路径 BootRoot 必注入。

### 读侧(流 → 指标)

`TelemetryReporter.ReduceActions` 改为**结算全序关联**:意图入 `pending` 队列;每笔结算解码后贴给
**同 (病人, 施予者) 的「最早一笔尚未结算」**意图;贴不上 ⇒ 保持 `null` 并计「未关联」
(可观测,不猜成「玩家选择」)。`SkipRateReport(out skipped, out unattributed)` 输出四行:
`F3 跳过率` / `跳过·玩家选择` / `跳过·降级` / `跳过·未关联`(AC-51-B8 两个独立分子)。

**刻意不拿 tick 作判据**:GDD 未定义该比较,且主机写路径(`HostEmergencyProcessor.Process`)
两事件恒同 tick 写 ⇒ 加它是自造规则(起草期曾加,自查后删除)。

辅助:`SkipRateMetrics.CauseNameOf(int)`(闭集 {0→玩家选择, 1→降级},**闭集外返回 null 不猜**)·
`CountDegradedSkips(...)`。

### 判据

| fixture | 条数 | 断言面 |
|---|---|---|
| `Tests/EditMode/Boot/emergency_manual_driver_test.cs` | 10(本片 +4)· 全绿 | 未注入默认 · 键鼠 ⇒ 降级(且与 `FeelLatencyEvaluator.GetKeyboardFallbackCause()` **对值**)· 有通道 ⇒ 玩家选择 · 重复注入抛 |
| `Tests/EditMode/InputSystem/emergency_device_readiness_test.cs`(新) | 4 · 全绿 | 仅键鼠 ⇒ absent · 键鼠+手柄(Mixed)⇒ available · 未注入 ⇒ absent · 契约由生产读数实现 |
| `Tests/EditMode/Telemetry/f3_f7_wiring_test.cs` | 本片净 +4 · 全绿 | 两分子分开(1/2, 1/2)· 结算定序 · cause 越界不猜 · 多余结算不覆盖 · 结算不顶替意图 |

### 证伪轮(改生产码 → 跑 → **同命令**还原 + `sha256sum -c` 基线校验)10/10 转红

| 变异 | 改坏什么 | 转红的用例 |
|---|---|---|
| M-c-1 | 写侧退回 `cause: 0` | `test_cause_keyboardOnlyPath_writesDegraded` |
| M-c-2 | `ResolveCause` 恒降级 | `test_cause_analogChannelPresent_writesPlayerSelection` |
| M-c-3 | 未注入默认成降级 | `test_cause_deviceReadinessNotBound_defaultsToPlayerSelection` |
| M-c-4 | 3 侧判据反判(default ⇒ 有通道) | `test_keyboardMouseOnly_analogChannelAbsent` |
| M-c-5 | 读侧不关联(cause 恒 null) | `associationIsSettlementOrder` + `twoMoleculesSeparate` + `extraSettlement_doesNotOverwrite` |
| M-c-6 | 关联取「最近一笔」而非「最早未结算」 | `test_wired_f3_associationIsSettlementOrder`(第 **2** 轮才红,见下) |
| M-c-7 | 删「已结算不得再贴」 | 同 M-c-5 三条 |
| M-c-8 | 越界 cause 猜成「降级」 | `test_wired_f3_causeEnumOutOfRange_notGuessed` |
| M-c-9 | 未关联计数恒 0 | `causeEnumOutOfRange_notGuessed` + `causeNotInvented` |
| M-c-10 | 降级分子数所有跳过 | 同 M-c-5 三条 |

⚠️ **证伪轮抓到一处假绿夹具(= BLOCKING,已修)**:`test_wired_f3_associationIsSettlementOrder` 首版
**两笔意图同为 Skip、两笔结算各给一个 cause** ⇒ 「贴第一笔」与「贴第二笔」在报告上**读数完全相同**
(两个分子各 1)⇒ 对定序毫无判别力,**M-c-6 首轮 GREEN**。
修法 = 两笔意图改为**不同结算路径**(t1 Manual / t2 Skip):cause 落到 Manual 笔还是 Skip 笔会改变读数
⇒ 正解 玩家选择 1/2 · 降级 0/2,错解 0/2 · 1/2。改后 M-c-6 转红。
**同轮另抓到**:`ReduceActions` 类注写「贴给最近一笔尚未结算的意图」,与实现(最早一笔)**自相矛盾**
⇒ 已就地订正为「最早一笔」。

### 回归

`3864 / 3827 / 0 / 37`(`unity/Logs/s19f-f3cause-full.xml`)vs 本片前 `3851 / 3813 / 1 / 37`
⇒ **total +13 = +14 passed − 1 failed**;逐名比对 **无消失用例**,新增 **13** 条
(driver +4 · readiness +4 · F3ToF7 +4 = 12 条新测,**+1** = 上批唯一红 `MovementFeedTest.test_move_afterFreeFall_…`
(`TD-player-01` 已知间歇)本轮转绿)。**零回归**。
⚠️ 口径说明:上表 `total` = Passed + Failed + NotRunnable(**含 Skipped 的 3864 在括号里**),
与前几片同一口径;本批汇总脚本首版把 Skipped 漏出 total,已按同口径修正后重新数。

### ⚠️ 未落半边(禁借绿)

1. **纯键鼠时 `magnitude ≡ MAG_MAX` 的通道读数回填未落** —— 那是 `FeelLatencyEvaluator.CreateKeyboardFallbackReading()`
   的形状,今天仍只有夹具调用。AC-10-21 的**幅度门恒过**半边在生产里**尚未成立**;本片只落 `cause` 半边。
2. **设备切换六旋钮是临时种子常量**(`CompositionRoot`),**未进 `data-core`**(ADR-014:含 `Fix` 的常量表须两阶段烘焙)
   ⇒ 数值轮定稿时须改走 cooked 并删本组常量。
3. `EmergencyDeviceReadiness` 读的是 `DeviceStateManager.State`(设备态五值)—— **不读 InputSystem 设备列表第二份副本**;
   若将来设备态判据变化,须同改 3 侧。

---

## 三·九、第九片:纯键鼠 `MAG_MAX` 回填(AC-10-21 的幅度半边 · 2026-10-19)

**接线前状态(如实)**:第八片只落了 `cause` 半边。AC-10-21 的另一半 ——「纯键鼠 ⇒ **幅度门恒过**、
节奏 + 稳度双门照评」—— 在生产里**不成立**:`FeelLatencyEvaluator.CreateKeyboardFallbackReading()`
自 GDD 起草时就存在,但**只有夹具调用**,生产求值链(`HostEmergencyProcessor`)对键鼠与手柄一视同仁地
按真实 `MagPeak` 判 ⇒ 键鼠上 `Axis` 恒 0 ⇒ 幅度门**恒挂** ⇒ 每一次键鼠动作都被判 `Missed`。

**用户裁定(2026-10-19,两问)**:
① **回填落点 = 读侧门豁免**(不写回载荷);
② **覆盖面 = 只包急救通道**(`TryAggregated` 那条链;`TryManual` 跳过入口不受影响)。

### 落点

| 件 | 改动 |
|---|---|
| `Sim/EmergencyProcedures/FeelLatencyEvaluator.cs` | 新增 `CreateKeyboardFallbackReading(action, holdTicks, edges, edgeTicks)` 重载 —— **节奏 / 稳度输入原样透传,只把 `magnitude` 置 `MAG_MAX`**。原无参重载保留(夹具时代的固定样本形状示意,计时是写死的假数) |
| `Sim/EmergencyProcedures/HostEmergencyProcessor.cs` | 新增带 `analogChannelAbsent` 判据的 `Process` 重载;判据为真 ⇒ 走上面的回填读数,**载荷里的 `MagPeak` 不变**(仍原样进流) |
| `Gameplay.Boot/EmergencyManualDriver.cs` | 新增 `IsAnalogChannelAbsent` 属性 = 判据真源;`ResolveCause()` 改为从它派生;`TryAttempt` 把判据传给处理器 |

### 三条口径(已写进文件头注)

1. **回填只落求值侧,不写回载荷** —— F-10.4 `:679-692` 规定 `mag_peak` 是「存在量词取峰值」的
   **判定量**且**进流**;而载荷是**判定输入**(ADR-011 Amendment B C 路:客户端上行 → 主机 Judge)——
   输入写的是玩家**真给了什么**,不是它被怎么判。回填若写进载荷,读流方就再也分不出
   「玩家按满了」与「设备按不出」。
2. **回填只改幅度,节奏 / 稳度照评** —— GDD Edge Cases `:728`「只走节奏 + 稳度双门」。
   回填读数的 `holdTicks` / `edges` / `edgeTicks` **逐字透传**本次动作的真实值。
3. **不知道就不回填** —— `IsAnalogChannelAbsent` 在「设备读数未注入」时回答 `false`(宁严勿猜),
   而 `ResolveCause()` 在同样情形落到「玩家选择」的夹具默认。两者**刻意分开传**:判据是输入,
   `cause` 是它的投影,不能让默认值反向决定判据。

### 判据

`Tests/EditMode/Boot/emergency_manual_driver_test.cs` 本片 **+8** 条(合计 18 条,全绿):

| 用例 | 断言面 |
|---|---|
| `test_ac1021_zeroMag_keyboardFallback_passesMagGate` | 键鼠 + 零幅度 ⇒ Applied(1.0),不是 Missed |
| `test_ac1021_zeroMag_analogChannel_stillMissed` | **反向对照**:有模拟量通道时同一份零幅度读数仍 Missed(0.25) |
| `test_ac1021_rhythmGate_stillEvaluatedUnderKeyboard` | 键鼠下 `edges = 0` 仍 Missed ⇒ 节奏门照评 |
| `test_ac1021_fallbackReading_keepsRealTimingOnlyMagIsFilled` | 回填读数的计时原样透传,只有幅度被置 `MAG_MAX` |
| `test_ac1021_payloadKeepsRealMagPeak_notFilled` | **流上 `MagPeak` 仍是 0**(回填不进载荷) |
| `test_ac1021_productionChain_tryAggregated_fillsOnKeyboard` | 端到端走 `TryAggregated`(通道交出物入口)⇒ 键鼠下也豁免 + `cause = 降级` |
| `test_ac1021_legacyOverload_doesNotInferFromCause` | 6 参兼容入口**不**从 `cause` 反推判据 |
| `test_ac1021_unboundDeviceReadiness_doesNotFill` | 未注入 ⇒ 判据 `false`(不回填),`cause` 仍是夹具默认 |

### 证伪轮(改 → 编译 → 跑 → **同命令**还原 + `sha256sum -c` 基线)10/10 转红

| 变异 | 改坏什么 | 转红的用例 |
|---|---|---|
| M-m-1 | 驱动不传判据(退回 6 参入口) | `productionChain_tryAggregated_fillsOnKeyboard` |
| M-m-2 | 驱动把「未注入」也当「无通道」 | `unboundDeviceReadiness_doesNotFill` + 2 条 cause 用例 |
| M-m-3 | 门豁免恒不生效(等于本片没做) | `productionChain_…` + `zeroMag_keyboardFallback_passesMagGate` |
| M-m-4 | 门豁免恒生效(手柄压平也被救活) | `zeroMag_analogChannel_stillMissed` |
| M-m-5 | 回填顺手把节奏门也放过(直接判 Applied) | `rhythmGate_stillEvaluatedUnderKeyboard` |
| M-m-6 | 回填改用无参重载(计时写死 100/3/{10,20,30}) | `rhythmGate_stillEvaluatedUnderKeyboard` |
| M-m-7 | 回填写进载荷 | `payloadKeepsRealMagPeak_notFilled` |
| M-m-8 | 兼容入口重新从 `cause` 反推判据 | `legacyOverload_doesNotInferFromCause`(第 **2** 轮才红) |
| M-m-9 | 回填不置 `MAG_MAX` | `fallbackReading_…` + `productionChain_…` + `zeroMag_keyboardFallback_…` |
| M-m-10 | 回填覆盖持时(节奏门被喂假数) | `fallbackReading_…` + `rhythmGate_…` |

⚠️ **证伪轮抓到的两处(性质不同,分别留档)**:

① **M-m-1 首轮 GREEN = 变异等价,不是夹具弱** —— 当时 6 参兼容入口**从 `cause` 反推判据**
(`cause == 降级 ⇒ 豁免`),而注入了设备读数时 `cause` 与判据**恒同源** ⇒ 行为确实不变。
复查后判定该设计本身**是错的**:用**结算输出**反推**判定输入的输入**,正是本项目反复剿的第二真源;
且「未注入 ⇒ `cause` 记玩家选择」的默认会让它进一步失真。**修的是生产码**(兼容入口改为
`analogChannelAbsent: false`,不承载判据),不是夹具。
② **M-m-8 首轮 GREEN = 夹具缺判据** —— 改完兼容入口后,没有任何测试钉住「它不豁免」。
补 `test_ac1021_legacyOverload_doesNotInferFromCause` 后转红。

⚠️ **另修两处夹具自身缺陷(都在本片新写的测试里)**:
- `MinHoldTicks = 0` ⇒ 节奏门 `edges ≥ MinEdges || hold ≥ MinHoldTicks` 在 `hold = 0` 时**恒过**
  ⇒ 节奏门永不可失败(`rhythmGate_stillEvaluatedUnderKeyboard` 首轮因此得 Applied)。改 `MinHoldTicks = 5`。
- `ResultMul[Missed] = 0` ⇒ Missed 的 potency 也是 0,与「没执行」不可分。另建 `MagGateRowGddMuls`
  用 GDD F-10.4 的三档(0.25 / 0.5 / 1.0),让 Missed 可观测。

### 回归

`3872 / 3835 / 0 / 37`(`unity/Logs/s19g-magfill-full.xml`)vs 本片前 `3864 / 3827 / 0 / 37`
⇒ **total +8 = passed +8**;逐名比对 **NEW 8 / GONE 0**,与新增测数**逐一相等**。**零回归**。

### ⚠️ 仍未落 / 边界(禁借绿)

1. **只覆盖急救直读通道链**(用户裁定 ②)。`TryManual` 的跳过入口不走判据 —— 跳过路径本就不评幅度门
   (规则六:跳过恒 `AppliedWeak`),故无缺口;但若将来新增其它手动入口,须显式决定是否带判据。
2. **设备读数未注入 ⇒ 不回填**。生产 `BootRoot` 启动序 2.9 必注入;若挂接失败(该步已有 `catch` + 警告),
   键鼠玩家会退回到「幅度门照评」= 每次 Missed —— 这是**已知的降级行为**,不是静默错误,但**无提示**
   (承无提示音铁律 AC-44-09)。是否需要在挂接失败时改为「宁可回填」,属产品判断,**本片未裁**。
3. **VR 半边不在本片** —— 回填判据按「有无模拟量通道」,`XrActive` 判为有通道;VR 急救动作本身归 P1b。

---

## 三·十、第十片:F7 上中位数口径订正(2026-10-19)

### 原判定(§三 留下的遗留项)

「生产式得 **20**,GDD 口径得 **30** ⇒ 现行生产行为与 GDD「上中位数」不符」,归 story-007 轮修。

### 实测根因 —— 判定本身错了,生产代码是对的

生产式 `SessionRhythmMetrics.cs` 原 `sorted[(n - 1 + 1) / 2 - 1]`,代入 `m = n − 1`(Δ 项数):

```
(n - 1 + 1) / 2 - 1  ≡  ⌈m/2⌉ - 1
```

`m = 1…10` 逐项复算,**全部**等于 `⌈m/2⌉` 的 1-indexed 项号 —— 即**恰好**是 `AC-51-B16`
的 `⟨a₁<a₂<a₃<a₄⟩ ⇒ a₂`。原判定的「口径得 30」来自两处**符号/下标错误**,不是生产式:

| 落点 | 原文 | 错在哪 |
|---|---|---|
| `telemetry-analytics.md:338/:344/:345` | `Δ_(⌈n/2⌉)` | `n` 在该节**从未定义为 Δ 项数**;按事件数读 ⇒ 取第 `⌈(m+1)/2⌉` 项(m=4 时 30) |
| `telemetry-analytics.md:587`(AC-51-B16) | `⌈n/2⌉` | 同款;而同一句的举例 `⟨a₁a₂a₃a₄⟩⇒a₂` 是 m 口径 —— **同一句里 `n` 有两个含义** |
| `f3_f7_wiring_test.cs:436` | 「idx = ⌈m/2⌉−1 ⇒ idx = 2 ⇒ 30」 | 把 **1-indexed 项号** `⌈m/2⌉ = 2` 直接当 **0-indexed 下标**用(漏减 1);实际 idx = 1 ⇒ 20 |

**三值互异**(m = 4,Δ = `{10,20,30,50}`):上中位数 **20** / 插值 **25** / 事件数口径 **30**。

### 用户裁定(2026-10-19)

**口径 1 —— 上中位数 = `⌈m/2⌉`,`m` = Δ 项数 = `n − 1`。** 与 AC-51-B16 的举例逐字一致。

### 修复落点

| # | 文件 | 改动 |
|---|---|---|
| 1 | `design/gdd/telemetry-analytics.md` | F7 块补 `n := \|E\|` / `m := n − 1` 两行定义;`Δ_i` 取值域 `1…n−1 → 1…m`;`:338/:344/:345` `n → m`;新增一段符号订正注 + 一条「夹具须选两值互异输入」的判别力提醒;AC-51-B16 行 `n → m` 并写明 `m = 4 个 Δ、5 个事件` |
| 2 | `production/epics/telemetry-analytics/story-007-*.md` | `:34/:45/:47/:59` 四处 `n → m`;追加 `## 追加记录 · F7 符号订正`(原判定 → 根因 → 落点 → 验证命令) |
| 3 | `unity/Assets/Telemetry/SessionRhythmMetrics.cs` | 头注 `:4` `n → m`;**等价重构**:引入 `int m = n - 1; int medianIdx = (m - 1) / 2;`,消掉诱人算错的 `(n-1+1)/2-1` 噪音 |
| 4 | `unity/Assets/Tests/EditMode/Telemetry/f6_f7_session_rhythm_test.cs` | 头注与 `:101` `n → m`;**新增判别力夹具**(下) |
| 5 | `unity/Assets/Tests/EditMode/Telemetry/f3_f7_wiring_test.cs` | `:436-440` 错误下标注释订正为三口径对账(20 / 25 / 30) |
| 6 | 本文档 | §三 遗留项段就地结清;§四 残留表第 2 项 ② 划掉 |

**生产行为零变更**:`(m - 1) / 2` 与 `(n - 1 + 1) / 2 - 1` 在整数域恒等,`ComputeRhythm` 输出逐位不变。

### 判据

- `F6F7SessionRhythmTest` **11/11 全绿**(原 10 + 新增 1)。
- 新增 `test_f7_upperMedian_differsFromInterpolationAndEventCount`:Δ = `{10,20,30,50}` ⇒ 断言
  `deltaMedian == 20` 且**显式** `!= 25`(插值)、`!= 30`(事件数口径)—— 三值互异,任一变异必红。

> ⚠️ **旧用例的判别力缺口(如实登记)**:`test_f7_upperMedian_evenCount_noInterpolation` 用
> Δ = `{2,5,1,3}`,而插值 `(2+3)/2` 在整数域截断**也是 2** ⇒ 该用例**对「改成插值」的变异零判别力**。
> 它保留下来只承担「结果 ∈ 输入集」这一条断言;判别力由新用例承担。这是假绿夹具的又一实例
> (与 S11-9 / 第九片 M-m-8 同型),**未被发现时它以绿的形式留下了却没测插值**。

### 证伪轮(改 → 编译 → 跑 → **同命令**还原 + `sha256sum -c` 基线)4/4 转红

驱动:`/tmp/fz/fz_f7_median.py`;基线 `/tmp/fz/baseline_f7median.sha`。

| 变异 | 改坏什么 | 转红的用例 |
|---|---|---|
| M-f7-1 | 取项改事件数口径 `(n + 1) / 2 - 1`(= 事故里「GDD 得 30」那一支) | `test_f7_upperMedian_differsFromInterpolationAndEventCount` · `test_f7_upperMedian_evenCount_noInterpolation` · `test_f7_nonZeroMad` |
| M-f7-2 | 取项改下中位数 `m / 2 - 1`(对偶数 m 等价于插值向下取整) | `test_f7_upperMedian_oddCount` · `test_f7_twoEvents_madZeroIsLegal` |
| M-f7-3 | 只改 MAD 的取项(中位数不动)⇒ 上中位数/MAD 取法不一致 | `test_f7_twoEvents_madZeroIsLegal` |
| M-f7-4 | 退化时 `defined` 报 `true`(报 0 而非「不可定义」) | `test_f7_degenerate_singleEvent_reportsUndefined` |

四次还原后 `sha256sum -c` 均 OK(`3c24d1cab81e58bb…`)。

### 回归

`3873 / 3836 / 0 / 37`(`unity/test-results.xml`,2026-10-19)vs 本片前 `3872 / 3835 / 0 / 37`
⇒ **total +1 = passed +1**,与新增测数**逐一相等**。`TD-player-01` 间歇本轮未复发。**零回归**。

### ⚠️ 未落半边(禁借绿)

- **GDD 符号订正无夹具**:`m` / `n` 的定义行是文档,没有机械门钉它(不属可执行判据)。
  防漂移靠 review + 本片新增的**行为夹具**(行为一旦回到事件数口径,夹具即红)。
- **`Δ_MAD` 的「中位数」口径**:GDD 写 `median(|Δ_i − Δ_median|)`,未明写「上中位数」。
  实现与 `Δ_median` 同取 `⌈m/2⌉`(m 相同),本片未改;若将来要把 MAD 定为下中位数,**须另裁**。

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

## 三·十一、第十一片:设备切换六旋钮临时种子(2026-10-19)

### 原判定(§四 残留 2① 的「下一半」)

「设备切换六旋钮仍为临时种子未进 data-core」。**实测**(还债前):

| 面 | 实测 |
|---|---|
| 源 | `assets/data/input_device_switch.json` 存在,`InputDeviceSwitchBinder` 构建期校验存在 |
| 运行期真源 | **无** —— `CompositionRoot.EmergencyDeviceSwitchSeed*` 六个**手抄常量**直接进 `DeviceStateManager` |
| cooked 制品 | **零**(`unity/Assets/DataCooked/` 无 `input_device_switch.*`) |
| 装载路径 | **无**(`AddressablesDataProvider` 无该分支,`DataCoreAddresses` 7 条) |
| JSON ↔ 常量对账 | **无任何机械约束** —— 同值纯属手抄巧合;数值轮改一边 ⇒ 静默分叉 |

### 用户裁定(2026-10-19)

**「种子常量降为兜底 + 对账断言」** —— 预载失败(未预载 / catalog 未重建)时仍取常量兜底
(设计内降级面,与 `ApplyAudioSettingsProvider` 同款),并加构建期对账断言钉住「常量 == 烘焙表」。

### 落点

| # | 文件 | 改动 |
|---|---|---|
| 1 | `Gameplay.Presentation/Input/DeviceSwitchTuningDataSet.cs`(**新**) | 运行期载体(两列 + schemaVersion + ConfigVersion);`CookedFileName` 单一出处 |
| 2 | `Gameplay.Presentation/Input/DeviceSwitchTuningCookedCodec.cs`(**新**) | 读方;与写方**严格镜像**;缺列 / 头损坏 / 截断 / 版本不符 ⇒ `[E-13]` 硬失败 |
| 3 | `Editor.Tools.Bake/InputDeviceSwitchCookedWriter.cs`(**新**) | deterministic 写方(列序固定 pointer → axis;`Fix` 写 i64 raw;schema 只住头槽) |
| 4 | `Editor.Tools.Bake/InputDeviceSwitchBake.cs`(**新**) | 仓根装载器 + `Materialize`(**真往返** = 写读两侧布局的唯一自动兜现) |
| 5 | `Editor.Tools.Bake/DataBakeMenu.cs` | 菜单「烘焙 input_device_switch(3 设备切换旋钮)」 |
| 6 | `Gameplay.Presentation/AddressablesDataProvider.cs` | `DeviceSwitchTuningAddress` + `Load<>` 分支 + `Preload()` 追加 |
| 7 | `Gameplay.Presentation/DataCorePreloader.cs` | `DataCoreAddresses` 7 → **8** |
| 8 | `Gameplay.Boot/BootRoot.cs` | 取值缝 `ResolveDeviceSwitchKnobs(IDataProvider)`(provider 非空 ⇒ 烘焙表;null ⇒ 兜底);`checked` 窄化 long → int |
| 9 | `Gameplay.Boot/CompositionRoot.cs` | 六常量注释「临时种子」→「**烘焙表的兜底值**」 |
| 10 | `Assets/DataCooked/input_device_switch.cooked.bytes`(+`.meta`) | 烘焙产物 66 B(`ConfigVersion = 0x34F3BBCE`) |
| 11 | `AddressableAssetsData/AssetGroups/data-core.asset` | 第 8 条目(GUID `4780fed9…`、address = 文件名、label `data-core`) |
| 12 | `Tests/EditMode/InputSystem/device_switch_wiring_test.cs`(**新**) | 12 条夹具(下) |
| 13 | `Tests/PlayMode/precheck_allKeys_test.cs` | 注释「7 条真地址」→「第十一片后 8 条」 |

### 判据:新夹具 `DeviceSwitchWiringTest` **12/12 全绿**

对账断言 / 真往返(+**绝对偏移**位置断言:载荷首列 = pointer)· 头槽 schema 非 0 ·
同源两次逐位一致 · 两列对调载荷逐位一致 · 取值缝两分支(provider ⇒ 烘焙表,**非**兜底;
null ⇒ 兜底)· E-13 负例 5 条(头槽 0 / 载荷-头槽 schema 分叉 / 缺列标志 / 截断三截法 / 魔数)。

### 证伪轮(`tools/falsify/unity_falsify.py` · 改 → 跑 → **同进程**还原 + SHA-256)7/7 转红

spec:`tools/falsify/specs/td-epic-01-device-switch-cooked.json`(`exactRed: true`)。
⚠️ **spec 内的 `guards` 字段驱动不读** —— `unity_falsify.py:269` 只认 `--guard` 与 `DEFAULT_GUARDS`。
故本轮**显式追加** `--guard unity/Assets/DataCooked`(否则 cooked 产物不在受保护面内),命令:

```bash
python3 tools/falsify/unity_falsify.py \
  --spec tools/falsify/specs/td-epic-01-device-switch-cooked.json \
  --guard unity/Assets/DataCooked
```

| 变异 | 改坏什么 | 转红的用例 |
|---|---|---|
| F1 | 停用头槽 schemaVersion 守卫 | `test_codec_headerSchemaZero_e13` |
| F2 | 停用载荷/头槽 schema 交叉断言 | `test_codec_payloadSchemaMismatch_e13` |
| F3 | 停用缺列守卫 | `test_codec_missingColumnFlag_e13` |
| F4 | 停用 payloadLength 一致性守卫 | `test_codec_truncatedPayload_e13` |
| F5 | 停用魔数守卫 | `test_codec_badMagic_e13` |
| F6 | 写方列序改 axis 先 | `test_bake_materialize_roundTripExact` |
| F7 | 兜底常量漂移 **1 个 raw 单位** | `test_reconciliation_emergencyConstantsEqualBakedTable` |

### ⚠️ 过程中实测到的两件事(如实登记)

1. **`DeviceSwitchTuning` / `DeviceSwitchColumn` 的命名空间是 `Gameplay.Presentation`,不是
   `.Input`**(`DeviceSwitchTuning.cs:16`;落点注记:Gameplay.Input 不引 Sim.Contracts)。
   写侧(`Editor.Tools.Bake`)与夹具起初只 `using …Presentation.Input` ⇒ CS0246。**已订正**。
2. **真往返测试对「写方列序」零判别力** —— 读方按同一新序回读,往返仍等值。首版只有往返等值
   ⇒ 静默假绿。**已补绝对偏移位置断言**(`HeaderSize + 4 + 2` 处读 i64 须 = pointer 阈值),
   F6 的转红即该断言的判别力证明。
3. 夹具首版载荷长度算式漏了 `u32 schema_version` 的 4 B(期望 62,实际 66)—— 已订正。

### 回归

`3885 / 3847 / 1 / 37`(`unity/Logs/s19h-dsswitch-full.xml`,2026-10-19)vs 本片前 `3872 / 3835 / 0 / 37`。
- **total +13 = passed +12(+1 failed)**:新增用例 13 条 = `DeviceSwitchWiringTest` **12** + `F6F7SessionRhythmTest.test_f7_upperMedian_…` **1**
  (`s19g-magfill-full.xml` 是第十片**之前**的基线,故第十片那条也在差集里);逐名比对 **GONE = 0**。
- 唯一红 = `MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig` ——
  **`TD-player-01` 的已知间歇**(`docs/tech-debt-register.md:207`:Unity 默认 `minMoveDistance = 0.001`
  吞掉亚阈值位移,`dx` 恰为 `0.0`,与本片无任何文件交集),不抵扣、**不记为已修**。
  **间歇性已实测定性**:单独重跑 `MovementFeedTest` **5/5 全绿**(`unity/Logs/ds_movfeed_rerun.xml`,
  该条 `Passed`)—— 与本次改动同批红、单跑转绿 ⇒ 间歇,不是回归。

### ⚠️ 未落半边(禁借绿)

- **catalog 重建 = 机器本地产物**:`AddressableAssetsData/*.bin` 被 `.gitignore` 忽略 ⇒
  `data-core.asset` 第 8 条目已入库,但**本机 catalog** 是否已含该地址由
  `precheck_allKeys_test::test_precheck_liveCatalog_allRealAddressesLocatable`(PlayMode)判定,
  **本轮未跑 PlayMode** ⇒ 该半边 **NOT-RUN**。跑 PlayMode 前须先跑「构建 Addressables 内容」菜单。
- **「产物 ↔ 组条目」一致无门**:`DataCooked/` 里有 cooked 产物、`data-core.asset` 里没登记
  ⇒ 预检能抓到(地址不可解析),但**反向**(组里多一条无产物)**无门**。本片靠「确保 data-core 组」菜单实测写入(8 条)。
- **`ResolveDeviceSwitchKnobs` 的 `checked` 窄化分支无夹具**:装载期 binder 已断区间,
  越界值到不了 ⇒ 该分支行为面不可达(靠 binder 的既有负例 `test_deviceSwitchBinder_violations_hardFail`)。
- **六个兜底常量与烘焙表对账**只钉「相等」,不钉「谁是真源」—— 删掉兜底须另裁。

---

## 三·十二、第十二片:51 遥测 F4–F7 六旋钮参数(2026-10-19)

### 原判定(本片还的债)

`design/gdd/telemetry-analytics.md:541` 声明「**旋钮的落地位置** = `assets/data/telemetry_analytics.json`」,
而该文件**从未存在**(`ls assets/data/ | grep telemetry` 空输出)。后果两半:

1. **`AC-51-B14` 的「配置加载」半边没有对象** —— `W < 1` / `S < 1` 没有可加载的配置,
   加载期硬失败无处施加(只有 `DifficultyCurveMetrics` 的运行期 `ArgumentException`)。
2. **`AC-51-E5` 是恒真分支** —— `test_e5_noSwitch_noSwitchFields` 在文件缺失时 `Assert.Pass`,
   而文件永远缺失 ⇒ 对变异零判别力(已登记 `review-cross-epic-fake-green-2026-10-18.md:58`)。

### 用户裁定(2026-10-19,AskUserQuestion)

| 问 | 裁定 |
|---|---|
| 六个数值旋钮谁定 | **「我给建议值,你批准后写死」** |
| 建不建 `assets/data/telemetry_analytics.json` | **「建文件 + 烘焙 + 装载 + 加载期门」** |
| E5 那条恒真测试改成什么 | **「文件必须存在 + 扫描禁止字段」** |

### 数值(用户 2026-10-19 批准;仍标临时、待数值轮)

| 旋钮 | 值 | 换算(20 Hz) | 推荐理由 |
|---|---|---|---|
| `W` | 600 tick | 30 s | 窗内单流事件约 5–20 条 |
| `S` | 300 tick | 15 s | `= W/2` ⇒ 50% 重叠、`CoveredTicks == SpanTicks` |
| `t_0` | 0 | — | 窗锚定世界起点 |
| `GAP_THRESHOLD` | 200 tick | 10 s | 中位 Δ 的 4–10× |
| `GROWTH_WINDOW` | 12000 tick | 10 min | 窗内成长事件约 30–60 条 |
| `TELEMETRY_MIN_SAMPLE` | 30 | 计数 | 比例型指标稳定下限;低于 GDD 工作例 `|Scorable| = 40` |

### 落地(原判定 → 修复落点)

| # | 原判定 | 修复落点 | 判据(可证伪) |
|---|---|---|---|
| 1 | 配置文件不存在 | `assets/data/telemetry_analytics.json`(四组结构;白名单 = 无开关) | `test_e5_noSwitch_noSwitchFields` 断言文件**存在**且 binder 无错 |
| 2 | 构建期门无对象 | `TelemetryAnalyticsBinder`(词法 → 四组白名单 → schema → `TelemetryAnalyticsTuning.Validate`) | `test_binder_windowZero_buildTimeReject` / `…_stepZero_…` |
| 3 | 加载期门无对象 | `TelemetryAnalyticsCookedCodec.Read` 解码后调 `Validate`,越界 ⇒ E-13 | `test_codec_windowZero_loadTimeReject` / `test_codec_otherOutOfRange_loadTimeReject` |
| 4 | 无 cooked 制品/装载路径 | `TelemetryAnalyticsBake` + `…CookedWriter` + 菜单 + `AddressablesDataProvider` 第 9 支路 + `DataCorePreloader` 9 地址 | `test_bake_materialize_roundTripExact` / `…_payloadColumnOrder_byAbsoluteOffset` |
| 5 | 生产方法「无默认值」= 调试视图永远「未给」 | `BootRoot.ResolveTelemetryKnobs` → `CompositionRoot.Assemble(telemetryKnobs:)` → `TelemetryReporter.Knobs` → `RenderDeveloperSummary` 用烘焙值 | `test_developerSummary_withKnobs_computesF5AndF7`(vs `…_withoutKnobs_keepsNotGiven`) |
| 6 | E5 恒真 | 测试重写(见 #1),并加白名单负例 | `test_binder_switchField_rejectedByWhitelist` / `…_nestedUnknownKey_…` / `…_missingGroup_…` |

### 本片实测到的两件事(如实登记)

1. **`unity build --execute-method` 首跑报 CS0246 却以退出码 0 返回** —— 该 CLI 的 build 子命令
   **不把编译失败映射为退出码**。判别只能读日志里的 `Scripts have compiler errors`。首跑未读日志
   会导致「以为烘焙成功、实际产物是旧的」。已改口径:**跑完必须 grep 日志**。
2. **生产消费缝的真实形态比「传参」复杂**:`RenderDeveloperSummary` 原设计「不替用户填旋钮」,
   与本片「装入烘焙值」正面冲突。解法 = 注入**可空**旋钮:注入了才填,未注入仍是「未给」——
   既落了真源又没造默认值。**`GROWTH_WINDOW` 是窗宽而非区间**,生产映射
   `(Tick_max − GROWTH_WINDOW, Tick_max)`;`Skill` 仍恒 null(F4 需要指名技能,预览不猜)。

### 证伪(7 变异,`--guard unity/Assets/DataCooked`)

| # | 变异 | 期望转红 |
|---|---|---|
| F1 | 停用 `W ≥ 1` 守卫 | `test_binder_windowZero_buildTimeReject` |
| F2 | 停用加载期范围门 | `test_codec_windowZero_loadTimeReject` + `test_codec_otherOutOfRange_loadTimeReject` |
| F3 | 停用顶层白名单 | `test_binder_switchField_rejectedByWhitelist` |
| F4 | 写方列序改 S 先 | `test_bake_payloadColumnOrder_byAbsoluteOffset` |
| F5 | 头槽 schemaVersion 硬写 0 | `test_bake_headerSchemaSlot_notZero_equalsContract` + `…_roundTripExact` |
| F6 | 取值缝断线 | `test_resolveKnobs_withProvider_takesBakedTable` |
| F7 | 停用 `MIN_SAMPLE ≥ 1` 守卫 | `test_codec_otherOutOfRange_loadTimeReject` |

> 结果见下方「判据」段(证伪轮完成后回填;未完成前本节**不得**记绿)。

#### 首轮实测(2026-10-19):**未通过(退出码 1)** —— 三处「红得比预期多」

首版期望清单按「理想情况」只列了**一条**期望转红的测;实测 F1 / F4 / F5 三个变异各红了更多。
逐条复核 —— **不是夹具弱,是我把期望写窄了**:

| 变异 | 首版期望 | 实测 | 归因 |
|---|---|---|---|
| F1 停用 `W≥1` | 1 条(构建期) | **2 条**(+加载期) | 守卫住在**共享**的 `TelemetryAnalyticsTuning.Validate` ⇒ 构建期与加载期**同时**失效,两条红是必然 |
| F4 写方列序 | 1 条(绝对偏移) | **3 条**(+往返 + 三方同值) | W=600 / S=300 **异值** ⇒ 写反后往返确实丢值;三条分别是「位置 / 单值 / 六值」三种粒度,互补 |
| F5 头槽 schemaVersion=0 | 2 条 | **5 条** | 读方版本门拒收 ⇒ 任何走整条管线的测都红 |

**处置**:期望清单**按实测**列全(已改 `…-cooked.json` 并标「首轮实测订正」),
然后**重跑**。原则是「清单必须与实测一致」,但**不得**用缩短清单的办法掩盖判别面不清 ——
三处差异均有**机制解释**,不是耦合噪声。还原与受保护指纹判据首轮**已通过**(SHA-256 + 零 diff)。

#### 判据(最终):**证伪轮通过(退出码 0)** —— 7/7 恰如预期

| 变异 | 实测转红 |
|---|---|
| F1 `W≥1` 守卫 | `test_binder_windowZero_buildTimeReject` + `test_codec_windowZero_loadTimeReject` |
| F2 加载期范围门 | `test_codec_otherOutOfRange_loadTimeReject` + `test_codec_windowZero_loadTimeReject` |
| F3 顶层白名单 | `test_binder_switchField_rejectedByWhitelist` |
| F4 写方列序 | `…_boundEqualsReloaded_sixKnobs` + `…_materialize_roundTripExact` + `…_payloadColumnOrder_byAbsoluteOffset` |
| F5 头槽 schemaVersion | 5 条(见上表) |
| F6 取值缝断线 | `test_resolveKnobs_withProvider_takesBakedTable` |
| F7 `MIN_SAMPLE≥1` 守卫 | `test_codec_otherOutOfRange_loadTimeReject` |

还原 SHA-256 逐字节通过 · 受保护路径 `unity/Assets/DataCooked` 指纹未变。
命令:`python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/td-epic-01-telemetry-analytics-cooked.json --guard unity/Assets/DataCooked`
(`--guard` 不可省 —— 驱动不读 spec 的 `guards` 字段,同第十一片口径)。

### data-core 组登记(走真菜单,非手改)

`DataBakeMenu.EnsureDataCoreGroup` via `unity build --execute-method`:

- 组条目 **8 → 9**,`data-core.asset` diff **仅 +6 行**(1 条):
  `m_Address: telemetry_analytics.cooked.bytes` · `m_SerializedLabels: [data-core]`;
- 条目 `m_GUID` = `89dcadb188a11e789a0ea310199b1d5b`,与
  `unity/Assets/DataCooked/telemetry_analytics.cooked.bytes.meta:2` **逐字一致**;
- 同一次命令编译错误 grep = **0** 命中。

### 回归

`3902 / 3865 / 0 / 37`(`unity/Logs/s19i-ta-knobs-full.xml`,2026-10-19)vs 本片前 `3885 / 3847 / 1 / 37`。

| 量 | 前 | 后 | Δ | 对账 |
|---|---|---|---|---|
| total | 3885 | 3902 | **+17** | = 新夹具 `telemetry_analytics_wiring_test.cs` 的 `[Test]` 数 **17**(等式成立) |
| passed | 3847 | 3865 | **+18** | = +17 新增 + 1(上批 `TD-player-01` 间歇 `MovementFeedTest` 本轮转绿) |
| failed | 1 | 0 | −1 | 同上 |
| skipped | 37 | 37 | 0 | 无新跳过;37 条前批已有 NOT-RUN 逐名不变 |

**GONE = 0** —— 前批 3885 条一名未丢(总数只增不减且增量恰等于新夹具数)。**零回归**。

### ⚠️ 未落半边(禁借绿)

- **`FOLD_MEMORY_MODE` 刻意不在本文件** —— 它是折叠实现策略而非测量参数;`AC-51-D6` 要求两值对拍,
  配置文件只能承载一个值 ⇒ 落进去会让对拍失去第二值来源。
- **`ComputeSeries` 覆盖记账既有偏差**(末窗越过 `Tick_max` ⇒ `CoveredTicks` 可能 &gt; `SpanTicks`)
  —— 从测试上移时带入,**不混在旋钮片里**改(改它 = 改 F5 行为)。
- **PlayMode catalog 半边 NOT-RUN** —— 同 §三·十一;跑 PlayMode 前须先跑「构建 Addressables 内容」菜单。
- **GDD `:519-529` 的 `*待定*` 标注**仍是旧的(值已批准但 GDD 未回写) —— 回写归 GDD 轮,本片不动 GDD。

---

## 四、仍未还(epic 级 · 禁借绿)

| # | 缺口 | 说明 |
|---|---|---|
| 1 | ~~**调试视图**~~ **✅ 第七片已落(留两半未落)** | 视图本体 + 入口 + 判据 12 条 + 证伪 8/8 全落。**残余**:① 生产装配**无调用点**(DevBuild 怎么唤未裁 ⇒ 只由夹具驱动)② AC-51-D4 的 Release 半边 **NOT-RUN**(须 Release player 构建) |
| 2 | ~~**装配层未接线**~~ **✅ 第五/六/八/九片已接** | F1–F7 均已有生产还原器 + 各自的真链路判据。**残余**:① ~~F3 的 `cause` 关联~~ **✅ 第八片已落**;~~纯键鼠 `MAG_MAX` 回填~~ **✅ 第九片已落**(AC-10-21 两半边齐);~~设备切换六旋钮仍为临时种子未进 data-core~~ **✅ 第十一片已落**(§三·十一 —— cooked 制品 + 装载路径 + 取值缝 + 8 条地址 + 对账断言;PlayMode catalog 半边 NOT-RUN);**该残余的下一半** = 兜底常量与烘焙表并存(删兜底须另裁)② ~~F7 上中位数与 GDD 不符~~ **✅ 第十片已结清**(§三·十 —— 生产式本就对,订正的是 GDD 符号 `n→m` 与一处下标注释)③ ~~F4–F7 的参数全 `*待定*`,生产方法**无默认值**,须调用方传~~ **✅ 第十二片已落**(§三·十二 —— 文件 + 烘焙 + 加载期门 + 可空注入;数值经用户批准,**仍是临时值待数值轮**,GDD 表内 `*待定*` 未回写) |
| 3 | 3 条 Ignore 未清 | `AC-51-B9`(集成目录 `tests/integration/51/` 未建)· `AC-51-E3`(非主机侧拒绝,依赖 `OQ-51-5` 未裁)· `blank_and_degraded` 的谓词面仍无测试驱动(见 §二②) |
| 4 | **10 条 story 的 `Complete` 一条未动** | 账面与实现的背离仍在 |

**⇒ `TD-epic-01` 状态 = 偿还中(十一片),不是已修好。**

---

## 五、命令(第三方可重跑)

```bash
# 本批夹具
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.JudgmentStreamReducerTest"
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.MetricsReportTest"
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.ReadonlyBoundaryTest"
# 期望:7/7 · 8/8 · 11/11(ReadonlyBoundary 的 skipped 已由 2 → 0)

# 调试视图(第七片)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Boot.TelemetryDeveloperViewTest"
# 期望:total 12 / passed 11 / failed 0 / skipped 1(唯一 skip = AC-51-D4 Release 半边 NOT-RUN)

# 证伪(改 → 跑 → 还原,同命令;开跑前先核基线)
sha256sum -c /tmp/fz/baseline.sha && python3 /tmp/fz/fz_debug_view.py M-dv-1   # 其余 M-dv-2…M-dv-8 同

# F3 cause 关联(第八片)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Boot.EmergencyManualDriverTest;DaYiJingCheng.Tests.Unit.Telemetry.F3ToF7WiringTest;DaYiJingCheng.Tests.Unit.InputSystem.EmergencyDeviceReadinessTest"
# 期望:41 例全绿(driver 18 · F3ToF7 19 · readiness 4)
sha256sum -c /tmp/fz/baseline_f3cause.sha && python3 /tmp/fz/fz_f3_cause.py M-c-1   # 其余 M-c-2…M-c-10 同;每例须 RED + 还原后基线 OK

# 纯键鼠 MAG_MAX 回填(第九片)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Boot.EmergencyManualDriverTest"
# 期望:18/18 全绿
sha256sum -c /tmp/fz/baseline_magfill.sha && python3 /tmp/fz/fz_mag_fill.py M-m-1   # 其余 M-m-2…M-m-10 同;每例须 RED + 还原后基线 OK

# F7 中位数口径(第十片)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.F6F7SessionRhythmTest"
# 期望:11/11 全绿(原 10 + 新增 1)
sha256sum -c /tmp/fz/baseline_f7median.sha && python3 /tmp/fz/fz_f7_median.py M-f7-1   # 其余 M-f7-2…M-f7-4 同;每例须 RED + 还原后基线 OK

# 设备切换六旋钮(第十一片)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.InputSystem.DeviceSwitchWiringTest"
# 期望:12/12 全绿
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/td-epic-01-device-switch-cooked.json --guard unity/Assets/DataCooked
# 期望:退出码 0(7 个变异全部恰如预期转红,受保护路径指纹未变)
# ⚠️ --guard 不可省:驱动不读 spec 里的 guards 字段,缺它则 cooked 产物不在受保护面内

# 遥测 F4–F7 六旋钮(第十二片)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.TelemetryAnalyticsWiringTest"
# 期望:17/17 全绿
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Telemetry.OutOfBoundsRejectionTest.test_e5_noSwitch_noSwitchFields"
# 期望:1/1 绿(文件必须存在 + 扫禁止字段 + binder 白名单无错)
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/td-epic-01-telemetry-analytics-cooked.json --guard unity/Assets/DataCooked
# 期望:退出码 0(7 个变异全部恰如预期转红);--guard 不可省(同第十一片)
# 重新烘焙(改了 assets/data/telemetry_analytics.json 之后):
#   unity build unity --target StandaloneLinux64 --execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeTelemetryAnalytics
#   ⚠️ 该 CLI 的 build 子命令**编译失败也返回 0** ⇒ 必须 grep 日志里的「烘焙完成」/「Scripts have compiler errors」

# 全量
rm -f test-results.xml && unity test unity --mode EditMode -- --log-file -
# 期望:total 3902 / passed 3865 / failed 0 / skipped 37(第十二片后,`unity/Logs/s19i-ta-knobs-full.xml`;
#       若 TD-player-01 间歇复发则 failed 1 —— 该条与遥测无关,单跑可复核)
```
