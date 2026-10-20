# 实现轮 —— 乙档第一份的读者接线(`disease_action_axis` → DC-4)+ 第二份的四层降级登记

**日期**: 2026-10-20
**用户裁定**(AskUserQuestion 两问,均取推荐):
- **B(`diagnosis_negative_confidence`)**: 「**只接 A,B 降级登记(推荐)**」——
  只接 `disease_action_axis`;B 如实登记为「缺整条消费链:求值 / 传输 / 呈现 / 下游 四层」,
  逐层列出缺口与归属系统,**不硬接**。
- **A 的接线点**: 「**Processor 构造注入 + Process 入口校验(推荐)**」——
  把处置轴闭集注入 `HostEmergencyProcessor` 构造,在 `Process` 入口设门;
  不用静态 `Provider` 缝,也不只在装配期校验 `BandageRow`。

**前置**:
- `370345c`(6 份数据集的读者面 —— 甲档 2 份换源 + 乙丙 4 份实测登记)
- 本件是 `370345c` 的「乙档第一份」收口;乙档第二份按裁定**降级登记**。

---

## §0 为什么 A 能接、B 不能接

分档不是估计,是 grep 出来的(逐条可复算,见 §5)。关键区别:

| | `disease_action_axis`(A) | `diagnosis_negative_confidence`(B) |
|---|---|---|
| 求值器实现 | **齐备**(`EmergencyActionSchema` / `ValidateActionIdFromAxis`) | **齐备**(`DiagnosisNegativeConfidenceEvaluator`,纯静态) |
| 生产读点 | 接线前**零**(DC-4 长期是 `Enum.IsDefined` 影子) | **零** |
| 缺的是 | **一次调用**(读点本身在跑,只差把产物接上去) | **整条链**(见 §2) |
| 本轮 | ✅ **接** | 📋 **降级登记** |

**判定口径**:A 的缺是「接线」,B 的缺是「消费链」。
往 B 的缝里塞 provider 而没人读,产出的是「已装载但无人用」——
与前两轮登记的边界**同一形态**,属借绿家族,故不做(§2 逐层给出证据)。

---

## §1 A 侧改动落点

| # | 文件 | 改动 |
|---|---|---|
| A-1 | `Sim/EmergencyProcedures/HostEmergencyProcessor.cs` | 构造增可选参 `ISet<int> actionAxis = null`;`Process`(带 `analogChannelAbsent` 的重载)**入口**设 DC-4 门 |
| A-2 | `Gameplay.Boot/CompositionRoot.cs` | `dataProvider != null` ⇒ `Load<DiseaseActionAxisDataSet>()` → 按 `owner=="10"` 过滤 → 注入 |
| A-3 | `Sim/EmergencyProcedures/EmergencyAction.cs` | **doc-only**:`ValidateActionId` 的自陈从「唯一判据」改为「兜底影子」 |
| T-1 | `Tests/EditMode/DataPipeline/action_axis_reader_gate_test.cs`(新) | **9 用例**(见 §3) |
| T-2 | `Tests/EditMode/DataPipeline/reader_wiring_gate_test.cs` | 装配面补 `DiseaseActionAxisDataSet`(组合根即时 Load) |

### 1.1 门的位置:`Process` 入口,**先于** `patientId` 门

理由(已写进代码注释):
- 载荷里的 action 非法 ⇒ 后面算什么都没意义;
- 该门**只读入参**,不产生任何副作用(不 `Append` / 不发号)⇒ 放在最前不改变任何既有语义。

### 1.2 门查**两个** action id,不是只查一个

| 字段 | 去处 | 只查另一个的后果 |
|---|---|---|
| `attempt.Action` | `EmergencyAttempt` 载荷(**判定输入,进流**) | 轴外 id 照样进流 ⇒ 由 9 的构建期白名单**静默拒收** |
| `action.ActionId` | `EmergencyTreatmentApplied.treatmentId`(九字段之二) | 同上,且报文无法定位是哪个字段错 |

**两个都查**是唯一正确的形态 —— 这条由 `test_axis_gatesPayloadActionNotJustRowActionId` 单独记分
(证伪 F2 证明它有判别力)。

### 1.3 `null` ≠ 空集(刻意区分)

| 值 | 语义 | 行为 |
|---|---|---|
| `null` | 装配层**未装**(EditMode 15+ 既有调用方全部不传该参) | 走 `Enum.IsDefined` 影子,**零行为变化** |
| 空集 | 装了,但产物里没有 `owner=="10"` 条目 ⇒ **装配错误** | 入口直接 `throw`(`InvalidOperationException`) |

混为一谈的话,产物损坏会静默退化成「全部动作非法」或「全部合法」。
判据 = `test_axis_emptyAxis_throwsDistinctFromNotInjected`(证伪 F3)。

### 1.4 为什么在装配层过滤、而不是在 Processor 里

`Sim` 住门 A(`noEngineReferences: true`),**够不着** `Gameplay.Presentation.DiseaseActionAxisCookedCodec`。
⇒ Processor 只收一个 `ISet<int>`,与 `IPayloadEncoder` 同构 —— **零新增程序集边**。
门 A / b2 / ADR-025 §① 全部继续成立。

---

## §2 B 侧:四层缺口(逐层可复算,不硬接)

**裁定要求**:如实登记为「缺整条消费链:求值/传输/呈现/下游 四层」,逐层列出缺口与归属系统。

| 层 | 现状(实测) | 缺口 | 归属 |
|---|---|---|---|
| **① 求值** | `DiagnosisNegativeConfidenceEvaluator` 实现**齐备**(纯静态:`WeightOf` / `Confidence` / `ConstitutesExclusion` / `ExclusionLevel`);`DiagnosisNegativeConfidenceTable`(含 `CurveAt` 硬失败)齐备 | **实现不缺** —— 缺的是调用方 | 8(已交付) |
| **② 装载** | `AddressablesDataProvider` 有 `DiagnosisNegativeConfidenceAddress` 分支 + `EnsureDiagnosisNegativeConfidence()`;`Preload()` 名单含它 | **`Load<DiagnosisNegativeConfidenceDataSet>` 全库零调用**(含测试)⇒ 产品会被预载,但**没有任何代码索取它** | 8 的装配轮 |
| **③ 传输** | `ExamineDriver.OnExamine` 是 8 的运行期求值出口,它调 `DiagnosisExamineEvaluator.Evaluate` 得 `ExaminedSet` | `ExaminedSet` **只有 4 字段**(`Rows` / `SignChannelMask` / `SignCount` / `PrecisionSlot`),**无把握度字段**;`ExamineDriver` **无** `SetNegativeConfidence` 类注入点 ⇒ 表进不了求值路径 | 8(形状扩展) |
| **④ 呈现** | 42 侧 `ConfidenceColumn` **无任何数值字段**(只有 `CapacityLimit = 6` 布局参数);`ICasebookDtoSource.PendingConfidence` 是 **u8 判断档位**(`JudgmentState` 0/1/2),**不是** F-8.3 的把握度 | 把握度的呈现载体在设计上是**墨色/笔迹**(`SkeuoPaper.uss .channel-reading-negative`),而「把握不足 vs 真阴性不可分」的**逐档墨色映射**未落 | 42 + 8(承 V-8.3 / 阻断 #8) |
| **⑤ 下游** | `ConstitutesExclusion` / `ExclusionLevel` / `CurveAt` / `ExcludeMin` / `NeverExcludes` | **零生产调用方**(仅 `confidence_leak_test` 测试面)⇒ 阴性排除证据从未进入 37 结案 / 53 结算 | 37 / 53 |

**关键判断**:即便把 ② 接上(塞 provider),①③④⑤ 四层全缺 ⇒ 结果是
「表装载了、可读了,但**没有任何玩法效果**」。这正是 `370345c` §0 早已判定的
「乙档 = 缺的不是接线而是调用方」—— **本轮不改判,只是把它的四层细化到可排期**。

**不接的直接后果(如实登记)**:`AC-8-13`(Lv15 不构成排除 / Lv20 构成排除的端到端)
**仍未可执行**。它是 `[I] BLOCKING` 级,归 8 的 story-004/story-006 与 9 的投影落地。

---

## §3 门真身(`ActionAxisReaderGateTest`,9 用例)

| 组 | 用例 | 判据 | 性质 |
|---|---|---|---|
| A | `artifactOwner10Set_matchesEmergencyActionEnum` | 产物 `owner=="10"` ⟷ `EmergencyAction` 枚举**逐值相等** | **对照** |
| A | `artifactIdsUniqueAndOwnerClosed` | id 两两互异;`owner ∈ {"10","11"}` | 数据完整性 |
| B | `syntheticAxis_rejectsActionOutsideAxis` | 轴 `{7,8}` ⇒ `action=0` 必拒,且**流里零事件** | **换源(承重 · 拒)** |
| B | `syntheticAxis_acceptsActionInsideAxis` | 轴 `{0}` ⇒ 放行,双事件入流 | **换源(承重 · 放)** |
| B | `gatesPayloadActionNotJustRowActionId` | 轴 `{0}`、行 `actionId=0` 合法但 `attempt.Action=99` ⇒ 必拒 | **门覆盖载荷侧** |
| C | `emptyAxis_throwsDistinctFromNotInjected` | 空集 ⇒ `InvalidOperationException`(报文含「空集」) | 负向 |
| C | `notInjected_fallsBackToEnumShadow` | `null` ⇒ 照常放行(零行为变化) | 回退 |
| D | `assemblyWithArtifact_acceptsActionInAxis` | 真产物经组合根 ⇒ `action=0` 放行 | 接线(对照半边) |
| D | `assemblyInjectsSyntheticAxis_overridesShadow` | **合成轴 `{99}`** 经组合根 ⇒ `action=0` **必拒** | **接线(承重半边)** |

### 3.1 ⚠️ 「换源」只能靠合成夹具证 —— 理由是实测的

产物实测(`disease_action_axis.cooked.bytes` 直接解字节,不经 codec):

```
action_id=0  disease_or_key=止血包扎    owner=10
action_id=1  disease_or_key=节奏型通气  owner=10
action_id=10 disease_or_key=柳树皮      owner=11
action_id=11 disease_or_key=毛地黄      owner=11
action_id=12 disease_or_key=金鸡纳皮    owner=11
```

⇒ `owner=="10"` 段 = `{0, 1}` **恰等于** `EmergencyAction` 枚举 `{0, 1}`。
**接线前后游戏行为逐位相同**。因此:
- 用真产物测「源换了」= **恒绿假门**;
- 换源半边**只能**用合成轴(如 `{7,8}` / `{99}`)证。

这条不是修辞 —— 证伪 F4 实测证明了它:删掉组合根那一行注入,用真产物时
**8 条测全绿**(见 §4 F4)。

### 3.2 本轮自查发现并修掉的一个**判别力缺口**(F4 首轮仍绿)

首版 9 条测里,`assemblyWithArtifact_acceptsActionInAxis` 单独看**没有判别力**:
组合根不注入(回退影子)时它也绿 —— 因为轴的 `{0,1}` 与枚举 `{0,1}` 行为相同。

**证伪轮 F4 首轮实测:删掉组合根那行注入,9 条测全绿** —— 这正是假绿夹具的形态。

**修法**:补 `assemblyInjectsSyntheticAxis_overridesShadow`,用**合成轴 `{99}`**
经组合根注入 ⇒ `action=0` 必拒。合成轴把「注入了」与「没注入」分开。
补后 F4 转红(§4)。

---

## §4 证伪轮(改 → 跑 → 还原,同一命令内;`sha256` 逐字节校验)

脚本原件 `/tmp/fz-axis2.py`(**临时件,不入库**)。基线 = 本轮最终树。

| # | 变异 | 期望红 | 实测 | 还原 |
|---|---|---|---|---|
| **F1** | `HostEmergencyProcessor` 停用整段 DC-4 门 | 拒 / 空集 / 载荷三条 | ✅ `5/9 failed=4` —— `rejectsActionOutsideAxis` · `emptyAxis` · `gatesPayloadAction` · `overridesShadow` | sha256 一致 |
| **F2** | 只查表行不查载荷(`attempt.Action` 那行短路) | `gatesPayloadAction` | ✅ `8/9 failed=1` —— 正该测 | sha256 一致 |
| **F3** | 空集不再抛(当未注入处理) | `emptyAxis` | ✅ `8/9 failed=1` —— 正该测 | sha256 一致 |
| **F4** | 组合根不注入轴(`actionAxis:` 参数删掉) | `overridesShadow` | ✅ `8/9 failed=1` —— 正该测 | sha256 一致 |
| **F5** | 组合根过滤写错(`owner=="11"`) | `overridesShadow` + `assemblyWithArtifact` | ✅ `7/9 failed=2` —— 正该测 | sha256 一致 |
| **F6** | **去掉本夹具的静态缝复位**(`[SetUp][TearDown]` 删掉) | **全量**里的 `cooked_drift[interaction_kinds]` | ✅ `3970/4007 failed=1` —— 红点**精确**落在该 case | sha256 一致 |
| **F7** | `reader_wiring_gate_test` 的 provider 不喂处置轴 | `ReaderWiringGateTest` 5 条 | ✅ `5/10 failed=5` —— 与首轮实测的 5 条红**逐名一致** | sha256 一致 |

**7/7 全部转红**;每例 `finally` 还原后 **sha256 逐字节一致**;盘上零变异残留
(`grep -E "if \(false &&|\[MUTATED\]"` 为空)。

### 4.1 F6 的解释:为什么它是**必须**的(本轮 6 条回归的真因)

`CompositionRoot:348` 会设**进程级静态** `KindPriorityTable.Provider`。
`ActionAxisReaderGateTest` 的 D 组经 `Assemble(dataProvider: …)` 把它设到自己那份
**刻意不含 `InteractionKindDataSet`** 的 provider 上;不复位则残留到后续测试,
凡走到 `InteractionKindTableValidator.ValidatePriorityTotalOrder`(经 `PriorityOf` → Provider)
的路径就抛。**实测**:这就是 `cooked_drift_all_artifacts_test.drift[interaction_kinds]` 转红的真因。

⇒ 补 `[SetUp][TearDown] ResetInjectionSeam()`(与 `ReaderWiringGateTest` 的**既有先例**同款),
并由 F6 证明:**删掉它,红点精确复现**。

### 4.2 ⚠️ F6 复跑(纪律,不是补绿)

上一轮 F6 执行期间我改了 `EmergencyAction.cs` 的 XML doc 注释(doc-only),
那段时间窗口内的全量编译是**混合态**。虽然红点命中了预期、判定不因此改变,
但为免「结果产生于一棵不稳定的树」,在树冻结后**重跑一次**:

- 复跑脚本 `/tmp/fz-f6-rerun.py`(临时件)
- 复跑输出 `unity/Logs/fz2-F6-rerun.xml`
- 结果见 §4.3

### 4.3 F6 复跑结果(树冻结后)

| 量 | 首轮 F6 | **复跑 F6** | 判 |
|---|---|---|---|
| total | 4007 | **4007** | 同 |
| passed | 3970 | **3970** | 同 |
| failed | 1 | **1** | 同 |
| skipped | 36 | **36** | 同 |
| exit | 8 | **8** | 同 |
| 红点 | `cooked_drift[interaction_kinds.cooked.bytes]` | **同一条,逐字一致** | ✅ |
| 还原 | sha256 一致 | **sha256 一致**(`2fee53c4efc67bff`) | ✅ |

复跑输出 `unity/Logs/fz2-F6-rerun.xml`;脚本 `/tmp/fz-f6-rerun.py`(临时件,不入库)。
提供给 `unity test` 的变异是**去掉本夹具的 `[SetUp][TearDown]`**(与首轮同一处、同一段文本)。

**判定**:复跑在冻结的树上**逐位重现**首轮结果 ⇒ 首轮的红**不是**混合态编译的产物,
判据成立。§4.2 所述「树不稳定」的疑点就此关闭。

**⚠️ 这条复跑是纪律,不是补绿** —— 首轮的红本就命中预期,判定从未因此改变;
复跑只为排除「结果产生于一棵不稳定的树」这一**独立**的证伪路径。
两次跑都是红,所以它既没有把任何东西变绿,也没有把任何东西变红。

**⚠️ 须知的实际代价**:F6 是本轮唯一一条必须在**全量**下跑的变异(其余 F1–F5、F7 都是
过滤跑,秒级)。原因是它的红点**落在另一个夹具**里(`cooked_drift_all_artifacts_test`),
过滤跑扫不到 —— **过滤跑对这条变异是无效的**(会假绿)。这条也写进了 `/tmp/fz-axis2.py`
的注释,供后续同类变异参考。

---

## §5 可复算证据(grep 原文)

### 5.1 A 侧接线后

```
$ grep -rn "dataProvider.Load<DiseaseActionAxisDataSet>" unity/Assets --include=*.cs
unity/Assets/Gameplay.Boot/CompositionRoot.cs:206:  DiseaseActionAxisDataSet axis = dataProvider.Load<DiseaseActionAxisDataSet>();

$ grep -rn 'Owner == "10"' unity/Assets --include=*.cs
unity/Assets/Gameplay.Boot/CompositionRoot.cs:209:  if (axis.Actions[i].Owner == "10")
unity/Assets/Tests/EditMode/EmergencyProcedures/action_tables_bake_test.cs:298:  (测试面)
unity/Assets/Tests/EditMode/DataPipeline/action_axis_reader_gate_test.cs:83:    (测试面)

$ grep -rn "new HostEmergencyProcessor(" unity/Assets --include=*.cs | grep -v "^unity/Assets/Tests/"
unity/Assets/Gameplay.Boot/CompositionRoot.cs:218:  var emergencyProcessor = new HostEmergencyProcessor(stream, idAuthority, encoder,
```

### 5.2 B 侧四层零读者

```
[层② Load]
$ grep -rn "Load<DiagnosisNegativeConfidenceDataSet>" unity/Assets --include=*.cs
  ⇒ 0 行(全库零调用,含测试)

[层③ 传输]
$ grep -n "SetNegativeConfidence\|NegativeConfidence" unity/Assets/Gameplay.Boot/ExamineDriver.cs
  ⇒ 0 行
$ grep -n "public readonly" unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisExamineEvaluator.cs
  34: IReadOnlyList<SignLexemeRow> Rows
  37: int SignChannelMask
  40: int SignCount
  43: int PrecisionSlot
  ⇒ 4 字段,无把握度

[层④ 呈现]
$ grep -nE "public " unity/Assets/Gameplay.UI/Skeuomorphic/ConfidenceColumn.cs
  16: public ConfidenceColumn()
  22: public int CapacityLimit { get; set; } = 6;
  ⇒ 无数值字段

[层⑤ 下游]
$ grep -rn "ConstitutesExclusion\|ExclusionLevel\|DiagnosisNegativeConfidenceEvaluator" unity/Assets --include=*.cs \
    | grep -v "^unity/Assets/Tests/" | grep -v "DiagnosisNegativeConfidenceTable.cs"
  ⇒ 0 行
```

---

## §6 完成度(双列 —— Landed / Proven)

| # | 内容 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| A-1 | `HostEmergencyProcessor` 构造注入 + `Process` 入口 DC-4 门 | ✅ | ✅ | F1 / F2 / F3;`rejectsActionOutsideAxis` · `gatesPayloadAction` · `emptyAxis` |
| A-1b | `null` 与空集分离 | ✅ | ✅ | F3;`emptyAxis_throwsDistinctFromNotInjected` |
| A-2 | 组合根读产物 → `owner=="10"` 过滤 → 注入 | ✅ | ✅ | F4 / F5;`assemblyInjectsSyntheticAxis_overridesShadow` |
| A-3 | `EmergencyAction` doc 改口径(非唯一判据) | ✅ | ✗ | **doc-only,无判据** —— 如实登记 |
| T-1 | 门 9 用例 | ✅ | ✅ | 基线 9/9 + F1–F5 |
| T-2 | `reader_wiring_gate_test` 装配面补轴 | ✅ | ✅ | F7 |
| T-2b | `action_axis_reader_gate_test` 静态缝复位 | ✅ | ✅ | F6(全量,红点精确) |
| R-1 | B 侧四层降级登记(§2) | ✅ | ✅ | §5.2 四条 grep 逐条可复算 |
| R-2 | **B 侧接线** | ✗ **按裁定不做** | — | 用户裁定「B 降级登记」;理由见 §2 |

> ⚠️ **A-3 是 `Landed ✅ / Proven ✗`** —— 它只改注释措辞,没有任何机械判据能钉住
> 「注释说的是对的口径」。如实登记,不标已证。

---

## §7 回归

**基线溯源三元组(强制)**:
**截至时刻** `2026-10-20 12:51 CST` · **批次** `sprint-15 数据管线乙档接线轮(全量 EditMode,单批)` ·
**commit** `370345c`(工作树另有 **6** 个未提交改动 —— 本表评的就是这棵树)

| 套件 | 退出码 | XML | 对照基线 |
|---|---|---|---|
| EditMode(全量) | `0` | `total=4007 passed=3971 failed=0 inconclusive=0 skipped=36` | 前次 `3998 / 3962 / 0 / 36` |
| PlayMode | **NOT-RUN** | — | 本轮未跑 |

**对账(等式判据 `total − 前次 = 新增数`)**:

| 量 | 前次 | 本次 | Δ | 预期 Δ | 判 |
|---|---|---|---|---|---|
| total | 3998 | **4007** | **+9** | +9(T-1 九用例) | ✅ |
| passed | 3962 | **3971** | **+9** | +9 | ✅ |
| failed | 0 | **0** | 0 | 0 | ✅ 零回归 |
| skipped | 36 | **36** | 0 | 0 | ✅ |

> ⚠️ 首次全量跑出 **6 条红**(`total=4007 passed=3965 failed=6`),见 §7.1。
> 修复后重跑为零回归。**6 条红是本轮真回归,不是测试污染**。

### §7.1 首次全量的 6 条红 —— 真因与修复(承重记录)

| 红 | 真因 | 修 |
|---|---|---|
| `ReaderWiringGateTest` ×5 | 组合根现在**即时** `Load<DiseaseActionAxisDataSet>()`,该夹具的 `MultiTableProvider` 没有这份表 | 补 `CommittedDiseaseActionAxis()`(读盘上**真产物**,非合成值)+ `BuildProvider()` 增一行 |
| `cooked_drift_all_artifacts_test.drift[interaction_kinds]` | **我新夹具引入的静态污染**(§4.1) | 补 `[SetUp][TearDown] ResetInjectionSeam()` |

**⚠️ 这一条值得记住**:6 条红里 **5 条**是「装配面扩大」的正常后果(夹具要跟上),
但第 6 条**不是** —— 它是一条**跨夹具、跨命名空间**的静态状态泄漏,
症状出现在一个**与我改动无关的测试文件**里。区分方法:
删掉我新增的整个测试文件后,那 5 条仍红(**产品侧接线仍在**),第 6 条转绿。

**判据(可复算)**:F6 正是这条因果的反证 —— 重新去掉复位,该 case **精确转红**。

### §7.2 PlayMode —— **NOT-RUN**(如实登记)

本轮改动面是 `Sim` 侧一个入口门 + 装配层一段过滤,**PlayMode 侧唯一相关行为**
是「真实启动序里 DC-4 门是否随 provider 生效」。该路径的判据需要 Addressables 组,
而组数据被 `.gitignore` 忽略(机器本地产物)⇒ 本机不可复现。
不跑就写 NOT-RUN,不借 EditMode 的绿说它已证。

### §7.3 IL2CPP 三格(ADR-012 矩阵)—— **NOT-RUN**,本机仅 Mono。

---

## §8 未结事项(如实登记)

1. **`treatable_by` 段仍未接** —— 其消费者是 F-11.5 的 `GateHit`,属 11 二轮评审 BL-3 的阻断项,
   不在本件面内。
2. **B 侧四层全缺**(§2)—— 按用户裁定降级登记。**`AC-8-13` 仍未可执行**。
3. **`AC-10-08` 的 `L_input < 50 ms`** 与本门的 DC-4 门无关 —— 门的开销是
   `ISet<int>.Contains` 两次,不涉及定格点 / 求值;不构成手感预算的负担。
4. **`EmergencyManualDriver.BandageRow` 仍是硬编码单行**(`ActionId = 0`),其真实来源
   (10 的动作表烘焙)归 21a/10 的烘焙轮。本轮只保证**注入的轴**能拦住它 ——
   即:若将来 `BandageRow.ActionId` 改成轴外值,门会拒。

---

## §9 落账

| 件 | 状态 |
|---|---|
| 本证据原件 | ✅ |
| `tests/integration/item_database/README.md`(账本) | ✅ |
| `production/session-state/active.md`(Session Extract) | ✅ |
| commit | **未做**(不经用户指令不 commit) |
