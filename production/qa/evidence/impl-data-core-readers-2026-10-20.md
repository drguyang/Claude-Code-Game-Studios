# 实现轮 —— 6 份数据集的「读者面」:接 2 份 · 登记 4 份

**日期**: 2026-10-20
**用户裁定**:「把 6 个数据集的读者接上」→ 范围为 **甲档 2 份 + 其余 4 份落实测登记**
**前置**:
- `bdadfb3`(路 A 接 2 份装载面 + 接线门 7 用例)
- `1a55c81`(第二轮接 4 份装载面 + 读方镜像门,`6/6` 装载面闭合)

**本件补的是哪条线**:前两轮把 6 份产物接进了**装载面**,并在
`impl-data-core-wiring-b-2026-10-20.md` §7.1 如实登记了边界:

> 「**6 个新数据集在运行期仍然零读者** …… ⇒ 两轮做的是「**可装载**」,不是「**已被使用**」。」

本件是那条边界的**收口**。

---

## §0 为什么只接 2 份(6 份不是同一类东西)

按「生产侧有没有真读点」实测分三档。**分档不是估计,是 grep 出来的**:

| # | 数据集 | 生产侧现状(实测) | 档 | 本件 |
|---|---|---|---|---|
| 1 | `interaction_kinds` | `KindPriorityTable.Priorities` **硬编码 10 项**;`InteractionSelector:146` 与校验器 `:252` **都在读它** | **甲** | ✅ 接 |
| 2 | `prescription_actions` | `PrescribeRequestDriver._entries` **硬编码单条**;`doseBase: 65536` 写字面量 | **甲** | ✅ 接 |
| 3 | `disease_action_axis` | `ValidateActionIdFromAxis` 存在但**非 Tests 面零调用**;`ValidateActionId` 仍是 `Enum.IsDefined`(自陈影子)。且 `RegistrySchema.cs:349-361` 的 R1-19 是**显式 `throw NotImplementedException`**(自我标记未实现,不是「差一次调用」) | **乙** | 登记 |
| 4 | `diagnosis_negative_confidence` | `DiagnosisNegativeConfidenceEvaluator` 的**非 Tests 面零调用**;8 的运行期求值出口 `ExamineDriver.OnExamine` 走 `DiagnosisExamineEvaluator`(阳性族),**阴性族在运行期无求值点** | **乙** | 登记 |
| 5 | `53_outcome_table` | `Outcome53.Judge.Evaluate` / `new JudgeInputs` **非 Tests 面零构造**(唯一命中是 codec 的 doc 注释) | **丙** | 登记 |
| 6 | `53_delay_table` | `new ConsequenceEmitter(` **非 Tests 面零构造** | **丙** | 登记 |

**分档的含义**:
- **甲档 = 真换数据源**:读点在跑、消费者在跑,只是数据来自硬编码常量。接上 = 换源 + 留回退缝。
- **乙档 = 缺的不是接线而是调用方**:函数写好了,没有生产代码调用它。**往缝里塞 provider 而没人读,
  产出的是「已装载但无人用」—— 与前两轮登记的边界同一形态,属借绿家族**,故不做。
- **丙档 = 消费链本身不存在**:53 的 `JudgeInputs` 需要 P/C/D 三键(病例流落笔/处置/康复)+ U(worldSeed 派生),
  `Emit` 需要 tick 驱动 + `eligible` 现算。这是 `story-003 发射路径接线` 的量。

---

## §1 改动落点

| # | 文件 | 改动 |
|---|---|---|
| P-1 | `Gameplay.Presentation/Interaction/InteractionKindTable.cs` | `KindPriorityTable` 加 `public static Func<IReadOnlyList<KindContractRow>> Provider`;表改**惰性建**(Provider 非空 ⇒ 从产物行建,否则回退);原硬编码字典升为 `FallbackPriorities` + 公开只读 `FallbackRows`;**缓存按 Provider 委托身份判定**(换 Provider / 置 null 即失效,无需额外 Reset API);重复 kind ⇒ `BuildFromRows` 抛 |
| P-2 | `Gameplay.Boot/PrescribeRequestDriver.cs` | 构造加可选参 `IReadOnlyList<PrescriptionActionCookedRow> prescriptionRows = null`;**`null` ⇒ 原硬编码单条(零行为变化)**;非 null ⇒ `BuildFromRows` 逐行建;极性串未知 ⇒ 抛;重复 `item_key` ⇒ 抛;原单条升为 **`public static FallbackEntries`**(对照 oracle;⚠️ 首版写成 `internal` 而**零消费者** —— 「独立 oracle」当时只是注释里的一句话,改成 public 并配上 `artifactRows_matchFallbackLiteral` 才成为可红的机器) |
| P-3 | `Gameplay.Boot/CompositionRoot.cs` | `dataProvider != null` 时:`Load<PrescriptionActionsDataSet>()` 一次 ⇒ `prescribePorts(doseBase: ds.DoseBase)`(**第二个真读点**,替掉写死的 `65536`)+ `PrescribeRequestDriver(..., prescriptionRows: ds.Rows)`;并设 `KindPriorityTable.Provider`。`== null` ⇒ 现状 |
| P-4 | `Gameplay.Presentation/DataCorePreloader.cs` | `ResetForPlayModeTests()` 增清 `KindPriorityTable.Provider`(跨 Play 轮残留,照 `AudioSettingsRegistry.Provider` 先例) |
| T-1 | `Tests/EditMode/DataPipeline/reader_wiring_gate_test.cs`(**新**) | **10 用例**(见 §3) |

---

## §2 ⚠️ 必须如实登记的边界(禁借绿)

### 2.1 本轮**不改变任何行为** —— 判据是「源真的换了」

两份产物的数据**逐值等于**既有硬编码字面量(实测):

| 项 | 产物 | 原硬编码 |
|---|---|---|
| `interaction_kinds` 的 (kind, priority) 10 项 | GDD §F-4.1b 演示序(Patient→0 … Drop→9) | 同一序 |
| `prescription_actions` 唯一行 | `salicylic_acid` / `10` / `symptomatic` | 同 |
| `DOSE_BASE` | `65536` | `65536` |

⇒ **接线前后游戏行为逐位相同**。本件证的是「**源换成了产物**」,**不是**「行为变了」。
把前者写成后者 = 借绿。因此「换源」这半边**只能靠合成夹具证**(真产物测恒绿)。

### 2.2 `unlock_level` 与 `ProcessingState` 是**派生映射**,产物不携带

- 产物只带 `base_id` ⇒ `ProcessingState` 取 `Extracted`(与 21a `MinimalDrugCatalog.DrugKey` 同口径);
- 产物无 `unlock_level` 字段 ⇒ 恒取 `0`(F-11.5 的门槛值仍归数值轮)。

产物一旦带上这两者,`BuildFromRows` 中对应两行即成**第二真源**,须同批删。

### 2.3 `InteractionSelector` 生产侧**仍零构造点**

实测(`grep "new InteractionSelector"` 非 Tests 面):**唯一命中是 `ModalGate.cs:116` 的形参**。
⇒ 本轮接的是**契约层读点**(选择器的第二键查表 + 4-DC-3 校验器),
**不是**「运行期已有实例在查」。选择器的生产装配归 4 的装配轮 ——
本件只保证「到那时读的是产物,不是字面量」。

### 2.4 乙/丙 4 份**未接**,原因逐份可复算(见 §0 表 + §5 grep)

不是「以后再接」这类散文,而是:乙档缺调用方(塞 provider = 假绿);丙档缺整条消费链。

### 2.5 IL2CPP 三格(ADR-012 矩阵)**NOT-RUN** —— 本机仅 Mono。<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。

---

## §3 门真身(`ReaderWiringGateTest`,10 用例)

### 3.1 `interaction_kinds` → `KindPriorityTable`

| 用例 | 判据 | 性质 |
|---|---|---|
| `artifactRows_matchFallbackLiteral` | 真产物行 ⟷ `FallbackRows` **逐项相等** | **对照**(保留字面量的用处) |
| `artifactRows_totalOrderAndCoverEnum` | 产物行两两互异 + 覆盖 `InteractableKind` 全集 | 数据完整性 |
| `providerInjection_swapsTable` | 注入**反转优先级**的合成行集 ⇒ 查表随之改变;置 null ⇒ 回到回退 | **换源(承重)** |
| `assemblyInjectsProvider` | `CompositionRoot.Assemble(dataProvider:…)` 后查表 = provider 行集 | **接线(承重)** |
| `duplicateKindInRows_throws` | 同 kind 两次 ⇒ 抛 | 负向 |

### 3.2 `prescription_actions` → `PrescribeRequestDriver`

| 用例 | 判据 | 性质 |
|---|---|---|
| `realArtifact_rowIsPrescribable` | 真产物 ⇒ 端到端开方成功,载荷 `treatment_id` = 10 | 正向 |
| `artifactRows_matchFallbackLiteral` | 真产物行 ⟷ `FallbackEntries` **逐项相等** | **对照**(保留字面量的用处) |
| `syntheticRow_drivesEntriesAndDoseBase` | 合成产物(`action_id=11` · `DOSE_BASE=1<<24`)⇒ 载荷 = 11、`DoseBase` = `1<<24` | **换源(承重)** |
| `duplicateItemKey_throws` | 同 `item_key` 两次 ⇒ 抛 | 负向 |
| `unknownPolarity_throws` | 极性串 ∉ {causal, symptomatic} ⇒ 抛 | 负向 |

> **为什么「换源」用合成夹具**:真产物的两处恰好等于字面量 ⇒ 用真产物测恒绿(假门)。
> 这是 §2.1 的直接后果。

---

## §4 证伪轮(改 → 跑 → 还原,同一命令内;`sha256` 逐字节校验)

脚本原件 `/tmp/fz-readers.py`(临时件,不入库)。基线 `ReaderWiringGateTest 10/10`。

| # | 变异 | 期望红 | 实测 | 还原 |
|---|---|---|---|---|
| **F1** | `CompositionRoot` 不设 `KindPriorityTable.Provider`(`if (dataProvider != null && false)`) | `assemblyInjectsProvider` | ✅ `9/10 red=1` —— 正该测 | sha256 一致 |
| **F2** | 驱动不吃产物行(`_entries = false && …`) | 处方侧三条负向/换源 | ✅ `7/10 red=3` —— `duplicateItemKey` / `syntheticRow` / `unknownPolarity` | sha256 一致 |
| **F3** | `doseBase` 退回字面量 `65536` | `syntheticRow` | ✅ `9/10 red=1` —— 正该测 | sha256 一致 |
| **F4** | 极性校验失效(未知串默认 Symptomatic) | `unknownPolarity` | ✅ `9/10 red=1` —— 正该测 | sha256 一致 |
| **F5** | 交互表重复 kind 不再抛 | `duplicateKindInRows` | ✅ `9/10 red=1` —— 正该测 | sha256 一致 |
| **F6** | 处方表重复 item_key 不再抛 | `duplicateItemKey` | ✅ `9/10 red=1` —— 正该测 | sha256 一致 |
| **F7** | `Active()` 恒回退(Provider 分支整段短路) | 交互表三条 | ✅ `7/10 red=3` —— `assemblyInjectsProvider` / `duplicateKindInRows` / `providerInjection` | sha256 一致 |
| **F8** | 缓存不随 Provider 身份失效(`if (_providerTable == null)`) | `providerInjection`(换第二个 Provider 半边) | ✅ `8/10 red=2` —— `duplicateKindInRows` / `providerInjection` | sha256 一致 |

**8/8 全部转红**;每例 `finally` 还原后 **sha256 逐字节一致**,盘上零变异残留(实测 grep 为空)。

### 4.1 ⚠️ 本轮证伪抓出的**三处真缺口**(不是脚本噪声)

| 缺口 | 怎么暴露的 | 处置 |
|---|---|---|
| **`BuildFromRows` 的重复 `item_key` 检查无判据** | F6 首轮**仍绿**(8/8) | 补 `duplicateItemKey_throws` ⇒ 现 F6 转红 |
| **`Provider = null` 时缓存未清** | F8 红点错位到 `duplicateKindInRows`(而非预期的 `providerInjection`) | `Active()` 在 Provider 为 null / 返回空时**连同 `_cachedFor`/`_providerTable` 一起失效** |
| **`ReferenceEquals` 缓存无专属判据** | 同上 —— 无断言区分「换了 Provider」与「没换」 | `providerInjection_swapsTable` 增「换第二个 Provider 必须跟着换」半步 |

### 4.2 ⚠️ 一次**变异脚本缺陷**(不是门的问题,留作记录)

F1 首轮报 **`NO-XML` / exit=6**,我一度误读成「门没判别力」。实为:
被测那行是 `if (dataProvider != null)` 的**唯一语句体**,脚本把它替换成注释后,
`if` 吞掉了下一条声明(`var emergencyCandidates = …`)⇒ **CS1023 + CS0103,编译失败**。

> **教训与 `tests/integration/processing/README.md` §首轮证伪的失败教训同型**:
> 「**没出结果**」与「**结果是绿的**」是两件不同的事。
> 本轮判据已加硬:脚本先删旧 XML,再区分 `NO-XML` 与「跑出来但仍绿」。

---

## §5 乙/丙 4 份:未接的可复算证据

```
$ grep -rn "ValidateActionIdFromAxis" --include=*.cs unity/Assets | grep -v "^unity/Assets/Tests/"
unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs:100:  /// 真源接线须由 `Editor.Tools.Bake` 侧调用 …
unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs:124:  public static bool ValidateActionIdFromAxis(…)
  ⇒ 2 行,全是声明与注释;**零生产调用方**

$ grep -rn "DiagnosisNegativeConfidenceEvaluator\." --include=*.cs unity/Assets | grep -v "^unity/Assets/Tests/"
  ⇒ 0 行;**零生产读者**

$ grep -rn "Outcome53\.Judge\.Evaluate\|new Outcome53\.JudgeInputs" --include=*.cs unity/Assets | grep -v "^unity/Assets/Tests/"
unity/Assets/Gameplay.Presentation/OutcomeTableCookedCodec.cs:111:  /// 判定表(可直接交给 …)
  ⇒ 1 行,且是 doc 注释;**零生产调用点**

$ grep -rn "new ConsequenceEmitter(" --include=*.cs unity/Assets | grep -v "^unity/Assets/Tests/"
  ⇒ 0 行;**零生产构造点**
```

**⚠️ 一条自我订正**:本轮勘察**首版**用的是 `grep -v "/Tests/"` —— 而仓库内测试路径是
`Tests/EditMode/…`(**无前导斜杠**),该过滤**完全失效**,一度得出「B-1 也零命中」的错误结论。
改为 `grep -v "^unity/Assets/Tests/"` 后 B-1 才显出那 2 行。**登记此事,因为它正是
「grep 写错 ⇒ 结论反转」的样本**(与 `cooked_codec_roundtrip_test` 的 7-bit 违例同族)。

---

## §6 完成度(双列 —— Landed / Proven)

| # | 内容 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| P-1 | `KindPriorityTable` 产物优先 + 回退 + 按 Provider 身份缓存 | ✅ | ✅ | F1(接线)/ F7(换源)/ F8(缓存语义);`providerInjection_swapsTable` · `assemblyInjectsProvider` |
| P-2 | `PrescribeRequestDriver` 吃产物行(缺省回退) | ✅ | ✅ | F2 / F3 / F6;`syntheticRow_drivesEntriesAndDoseBase` · `duplicateItemKey_throws` |
| P-2b | 极性串未知即抛 | ✅ | ✅ | F4;`unknownPolarity_throws` |
| P-2c | `FallbackEntries` 升 public(作判据宾语) | ✅ | ✅ | `artifactRows_matchFallbackLiteral`(处方侧) |
| P-3 | `CompositionRoot` 两行装配 + `doseBase` 读产物 | ✅ | ✅ | F1 / F3 |
| P-4 | `ResetForPlayModeTests` 增清 Provider | ✅ | ✗ | **无专属判据**(见下) |
| T-1 | 门 10 用例 | ✅ | ✅ | 基线 `10/10` + F1–F8 |

> ⚠️ **P-4 是 `Landed ✅ / Proven ✗`** —— 如实登记。`ResetForPlayModeTests` 属 PlayMode
> 跨轮卫生,其判据在 PlayMode 侧;本机本轮**未跑 PlayMode**(见 §7),故不标已证。
> 不把它写成 ✅ 的理由:该行是「跨轮残留」的**唯一**处置点,若无判据,删除它不会有任何测转红。

---

## §7 回归

**基线溯源三元组(强制)**:
**截至时刻** `2026-10-20 12:19 CST` · **批次** `sprint-15 数据管线读者面轮(全量 EditMode,单批)` · **commit** `1a55c81`(工作树另有 **9** 个未提交改动 —— 本表评的就是这棵树,不是 `1a55c81` 的纯态)

| 套件 | 退出码 | XML | 对照基线 |
|---|---|---|---|
| EditMode(全量) | `0` | `total=3998 passed=3962 failed=0 inconclusive=0 skipped=36` | 前次 `3988 / 3952 / 0 / 36`(同 commit `1a55c81`) |
| PlayMode | **NOT-RUN** | — | 本轮未跑(见下) |

**对账(等式判据 `total − 前次 = 新增数`)**:

| 量 | 前次 | 本次 | Δ | 预期 Δ | 判 |
|---|---|---|---|---|---|
| total | 3988 | **3998** | **+10** | +10(T-1 十用例) | ✅ |
| passed | 3952 | **3962** | **+10** | +10 | ✅ |
| failed | 0 | **0** | 0 | 0 | ✅ 零回归 |
| skipped | 36 | **36** | 0 | 0 | ✅ |

**门内自证** —— `ReaderWiringGateTest` 在本次全量里 **10/10 Passed**(逐名核对,见下):

```
test_readers_interactionKinds_artifactRows_matchFallbackLiteral      Passed
test_readers_interactionKinds_artifactRows_totalOrderAndCoverEnum    Passed
test_readers_interactionKinds_assemblyInjectsProvider                Passed
test_readers_interactionKinds_duplicateKindInRows_throws             Passed
test_readers_interactionKinds_providerInjection_swapsTable           Passed
test_readers_prescriptionActions_artifactRows_matchFallbackLiteral   Passed
test_readers_prescriptionActions_duplicateItemKey_throws             Passed
test_readers_prescriptionActions_realArtifact_rowIsPrescribable      Passed
test_readers_prescriptionActions_syntheticRow_drivesEntriesAndDoseBase Passed
test_readers_prescriptionActions_unknownPolarity_throws              Passed
```

> ⚠️ **`skipped=36` 不变**,不是「36 条被跳过所以 3998 虚高」—— 该值与基线逐位相同,
> 说明本轮**没有新增任何 skipped**;36 条是本轮之前既有的 Ignored 存量。

### §7.1 ⚠️ 本轮抓出的第二个**真实缺陷**(不是门的问题,也不是脚本的问题)

**全量回归首次尝试根本没有跑起来。** 首次启动用的等待式前置是:

```bash
until ! pgrep -f "Unity.*batchmode" >/dev/null && ! pgrep -f "fz-readers.py" >/dev/null; do sleep 15; done
```

`pgrep -f` 匹配的是**整条命令行**,而这条等待脚本**自身的命令行里就含 `Unity.*batchmode`
与 `fz-readers.py` 两个字面量** ⇒ 它匹配到自己 ⇒ 条件恒假 ⇒ **`until` 死循环,`unity test` 那一行
从未被执行**。四个后台等待任务全部卡在这里,`unity/Logs/readers-full.xml` 一直不存在。

**这正是「没出结果 ≠ 结果是绿的」的第三个变体**:前两个是 XML 陈旧(README 记过)与
变异脚本编译失败(`§4.2` 记过),这次是**启动器自己空转**。

**处置**:停掉四个自匹配任务,改为**同步直接执行**(先 `ls` 确认无 lockfile、`ps -eo comm | grep -x Unity`
确认无 Unity 进程,再 `rm -f` 旧 XML,然后前台跑)。代价是阻塞 5 分钟,收益是**不可能静默空转**。

**判据(可复算)**:`ls -l unity/Logs/readers-full.xml` 的 mtime 晚于本次启动时刻
⇒ 文件是本次新生成的,不是旧件。

### §7.2 PlayMode —— **NOT-RUN**(如实登记)

本轮**未跑 PlayMode**。直接后果是 P-4(`ResetForPlayModeTests` 清 `KindPriorityTable.Provider`)
的 `Proven` 列保持 **✗**(见 §6)。不跑的理由:本轮的改动面是**装配层两行 + 两条可注入缝**,
PlayMode 侧唯一相关行为就是跨轮回退卫生;而在 EditMode 侧已用 `[SetUp]`/`[TearDown]`
双向清 `Provider` 覆盖了同一语义。**但覆盖不等价** —— 跨轮残留的判据只能在 PlayMode 会话间观察,
故 P-4 仍标 `Landed ✅ / Proven ✗`,不借 EditMode 的绿说它已证。
