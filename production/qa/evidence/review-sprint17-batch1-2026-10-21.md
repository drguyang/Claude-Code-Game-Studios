# 代码评审原件 —— sprint-17 批 1（S17-1 / S17-3 / S17-4）

**日期**:2026-10-21
**评审轮次**:首轮（`/code-review`）
**评审对象**:批 1 三件的实现件与测试件
**四位评审**:unity-specialist ×3（逐件）+ qa-tester ×1（可测性，跨三件）
**评审方式**:静态评审。**四位均未运行 Unity 测试**；「测试全绿」的背书来自
`impl-sprint17-batch1-2026-10-21.md`，不来自本件。

---

## 一、总裁定

| 件 | Verdict | BLOCKING |
|---|---|---|
| **S17-1** clinic-002 | APPROVED WITH SUGGESTIONS | 0 |
| **S17-3** tutorial-001 | APPROVED WITH SUGGESTIONS | 0 |
| **S17-4** casebook-002 | APPROVED WITH SUGGESTIONS | 0 |
| **可测性（跨三件）** | **GAPS** | 0 系统性假绿，但 **2 条 BLOCKING-class 待裁** |

**结论**:代码侧零 BLOCKING。但**不能因此记「三件 AC 全勾」** —— 下面 §三 的两条
是 AC 语义层问题，属 `/story-done` 的判据面。

---

## 二、逐条发现（Landed / Proven 双列结账表）

> 依据 `.claude/skills/code-review/SKILL.md` Phase 8b：`Landed` = 改动在盘上；
> `Proven` = 改对了**且被可复算判据钉住**。**修复轮未开始，故 Proven 列全部为 ✗**（禁借绿）。

### S17-1

| ID | 判定 | Landed | Proven | File | Proven-by（待填） |
|---|---|---|---|---|---|
| A1 门 A | PASS | — | — | `Sim.asmdef:4,13` | 已实测：引用集 = `Sim.Contracts` + `noEngineReferences:true`；`Sim/Clinic/*.cs` 的 `UnityEngine` 命中全为注释 |
| A2 两层世界 | PASS | — | — | `ClinicRoomExtraction.cs:188-194` | `CompareCell` 用 `<` 不用减法；零浮点/零 NavMesh |
| **A3-3 壳格 AC** | **WARNING** | ✗ | ✗ | `clinic_room_extraction_test.cs:111-126` · `:270-283` | ⚠️ 两条测试在当前输入模型下**恒真**（壳格位置无输入格 ⇒ 实现对错都绿）。AC 实际只由 `IPlacedStructureReadView.cs:41` 的**契约声明**承载，**无机械门**强制系统 23 的实现方排除 `BakedInitial` 格 |
| C8 并查集 | PASS | — | — | `:232-251` | 路径减半 + `clusters` 仅根不同时自减；上界守卫必要且正确（须论证，见报告） |
| C9 确定性 | PASS + MINOR | — | ✗ | `:145-146` `:237-238` `:276-277` | 先 `Sort(CompareCell)` 消序 → 根取小索引 → FNV-1a 全程 `unchecked ulong`。MINOR：`ulong→int` 收窄语言层未规定值，**无黄金夹具** |
| C10 只读视图 | PASS | — | — | `IPlacedStructureReadView.cs:42,53` | 接口恰两成员；测试遍历全部 public static 方法强制参数类型（`:384-390`）。**措辞订正**：`IEventSink` 住 `Sim.Contracts`，程序集层面**可见** —— 准确表述是「调用路径层面不可达」 |
| **C11 缺口哨兵** | **MINOR（半合格）** | ✗ | ✗ | `ClinicRoomExtraction.cs:121` | `FurnTypeClosureEnforced => false` 是**编译期常量**，不从任何东西派生 ⇒ 合格于「缺席**可观测**」，不合格于「缺席**可检测**」（有人加了 `FURN_TYPES` 却忘改哨兵 ⇒ **零测试转红**） |

### S17-3

| ID | 判定 | Landed | Proven | File | Proven-by（待填） |
|---|---|---|---|---|---|
| A1 之八 比较器 | PASS | — | — | 生产侧 15 处 | 全部显式带 `StringComparison`；`tutorial_content_validation_test.cs:512` 的 `Contains("click")` 无比较器（测试侧 MINOR，`StringAssert` 2 参重载无可选比较器） |
| A2 结构化计数 | PASS | — | — | `TutorialContentBinder.cs:99,755,793,796,824,826` | 真假由 `HitCount` / `hits.Count>0` 派生；Hits 仅人读 |
| B3 跨侧字段序 | PASS | — | — | 写 `TutorialContentCookedWriter.cs:62-65,148-200` ↔ 读 `TutorialContentTable.cs:258-261,288-336` | 段序 + 四段行字段 + 五个头偏移**逐个对称**。唯一防线 = 往返测（逐字段 + 两次烘焙逐位相同 + 截断/坏魔数负例） |
| B4 解码防御 | PASS | — | — | `TutorialContentTable.cs` | 截断 / 坏 magic / `payloadLength` 不符 / 载荷未恰读完 / 负计数 / 越界串 **全部 return false** |
| **C5 方法长度** | **WARNING（唯一标准违例）** | ✗ | ✗ | `TutorialContentBinder.cs:569-639` `ValidateReferences` = **71 行原始 / 62 行有效** | 30 行文件内**唯一超标**；其余 34 个方法 ≤ 39 行。次高 `Bind:256-310` = 55 原始 / **37 有效**（合规）。**若项目 40 行限是机械硬门，本项应升 BLOCKING** |
| C6/C7 | PASS | — | — | `TutorialContentBake.cs` | 「刻意不碰 UnityEngine」**属实**；`order` 走 `JsonNodeKind.Integer` ⇒ 浮点字面量结构性硬失败 |
| D8/D9 接线 | PASS | — | — | `provider_wiring_gate_test.cs:92-96,220-258` | 三处集合各 **16** 项闭合；门经**反射取 `Ensure*` 名 + 扫描切 `Preload()` 体 + 长度三方比对**，删一个调用会真红；E-13 = `throw new InvalidOperationException(...绝不降级)`（`:510-522`） |
| D8-b | MINOR | ✗ | ✗ | `DataCorePreloader.cs:185-187` | 散文写「共 15 条」，**实际 16** |
| B3-b | MINOR | ✗ | ✗ | `TutorialContentTable.cs:228` | 魔数硬编码字面而非 `CookedFormat.Magic`（漂移是"响的"） |
| E1/E2 | MINOR | ✗ | ✗ | `OrderDemonstrations/OrderPapers`；note 强校验后丢弃 | `steps` 形参未用；note 校验后写成空语句 |
| E3 | 待确认 | — | — | `TutorialContentSchema.cs:184` | `RegistryKindNames = Enum.GetNames(typeof(EventKind))` 与 ADR-024 真源 `entities.yaml` 是否同源**未核**（预算所限，不作判定） |

### S17-4

| ID | 判定 | Landed | Proven | File | Proven-by（待填） |
|---|---|---|---|---|---|
| C1 禁 `a-b` | PASS | — | — | `CaseListOrdering.cs:63-70` | 三分量全 `CompareTo`；测试 `:249-265` 是可证伪反例 |
| C2 真全序 | PASS | — | — | 同上 | 键 = 三元组，`Seq` 在 `(Tick,Patient)` 内唯一 ⇒ 无并列；**不依赖 `Array.Sort` 稳定性** |
| C3 纯函数 | PASS | — | — | `:382-404` | 零静态可变字段/零缓存/零 I/O；反射守卫带非空防空跑 |
| **C4 零展示序** | **WARNING** | ✗ | ✗ | `:354-379`，尤 `:363` `:369` | ⚠️ 反射断言**过滤掉自动属性背衬** ⇒ 属性式 `DisplayOrder` 逃逸（Unity 不序列化属性 ⇒ 非 BLOCKING）；词表有 `sortindex` 却**漏 `sortkey`**；类型限 `int/long`，`byte/short` 逃逸 |
| **C5 前缀性** | **WARNING（规格面）** | ✗ | ✗ | story `:36` · `OrderedList:225` | `OrderedList` 是**全量重排**，本身不保证前缀，依赖「追加键单调」外部前提；该前提**只写在测试文件头**，生产 doc comment 一字未提。story `:36` 的无条件措辞**数学上不可满足** |
| C6 分组 | PASS + MINOR | — | — | `BuildGroups:240-255` | 段区间首尾相接恰覆盖全表，组序不可能重排；MINOR：公有构造不校验 `Count ≥ 1` |
| C7/C8 | PASS | — | — | — | 方法 ≤14 行、复杂度 <10、public 全有 doc；命名避让门只扫 `Skeuomorphic/**`，本件不在扫描面 |
| **C9 装配** | **WARNING** | ✗ | ✗ | `Casebook.Tests.asmdef` | 已进 `AssemblyGates.Manifest:77` 与 `ADR-025:121` ✅；但 `Sim` 与 `Gameplay.Presentation` **零使用**（asmdef 引用不传递）⇒ 非最小；缺 `autoReferenced:false` |
| 记账滞后 | MINOR ×3 | ✗ | ✗ | story | Test Evidence 仍写「Not yet created」实有 16 测 · **Notes ② 伪码写 `a.pid - b.pid`，与同文件「禁 `a-b`」自相矛盾**（实现是对的）· AC-3 字节级对比无载体 |

### 可测性（qa-tester，跨三件）

| ID | 判定 | 内容 |
|---|---|---|
| T0 | **GAPS（无系统性假绿）** | 85 测**都打在生产类型上**，不是自造断言顶替整条 AC |
| T1 | GAPS | S17-1「壳格」两条测 fixture 里**根本没有壳格**，语义空转 |
| T2 | GAPS | S17-1 只读面：静态字段查空，但 **static 属性**上的缓存逃得过反射 |
| T3 | GAPS | S17-3 AC 第 1 条的 `order` / `actor=mentor` / `aspectRatio` / `illustrationKey` **零负例** |
| T4 | GAPS | `body==170` 正例缺失（回填 #2 要求成对）；`test_check2_danglingCueId_isNotCheckedWhenTableMissing` 是**空转夹具**（未注入悬空 id） |
| T5 | 弱断言 | `test_ac4_sevenSteps_throws` 仅 `StringAssert.Contains("7", …)` |
| **T6** | **GAPS（BLOCKING-class）** | **AC-5 的 `IDataProvider` / `EnsureTutorialContent` 装载零测试** —— 接线测只证三个常量互等 |
| T7 | GAPS | S17-4 两条反射 AC 过滤了 `<>` 背衬字段 ⇒ auto-property 可绕过 |
| T8 | **BLOCKING-class（规格）** | **AC-39-03 字面性质被测试自证伪**（追加更小键 ⇒ 非前缀）。测试诚实登记，**须收窄 AC，不能记无条件 covered** |
| T9 | PASS | 四文件零随机/零时间/零写盘（只 `ReadAllText`）；`FsWriteBoundaryGate` 不触发；culture 变更已 `finally` 复原 |

---

## 三、两条 BLOCKING-class（须用户裁定，不由评审自裁）

### B-1 · `AC-39-03` 的措辞不可满足

story `:36` 写的是**无条件**：「同一病例流取任意两个前缀 A ⊂ B，`OrderedList(A)` 是 `OrderedList(B)` 的前缀」。
实现是**全量重排**，只有「追加项三元组 ≥ 既有全部键」时才成立 —— 该前提由
`IIdAuthority` 单调发号 + 新案开案 tick 更大保证，**不在本函数的判据面内**。
测试 `test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix` 已如实登记反例。

**出路**:① 收窄 AC（加前提）或 ② 承认不可满足并降级。**不得改测试迁就。**

### B-2 · `AC-5` 缺装载测试

`AddressablesDataProvider.EnsureTutorialContent` / `IDataProvider` 分支**零测试**；
现有接线测只证「三个 address 常量互等」。按「AC 须有可判载体」的标准，
**AC-5 目前只能记 `PARTIAL`**。

**出路**:补一条注入 byte provider 的装载测。

---

## 四、建议优先修（不阻塞，但成本低）

1. `C5` 拆 `ValidateReferences`（`TutorialContentBinder.cs:569-639`）为「悬垂→反向一致→孤儿」三件
2. `D8-b` `DataCorePreloader.cs:185-187` 散文「15 条」→「16 条」
3. `C4`/`T7` 反射断言纳入 `GetProperties` + 词表补 `sortkey`
4. `C9` 修剪 `Casebook.Tests.asmdef` 的两条未用引用 + 补 `autoReferenced:false` + 同步订正 `ADR-025:121`
5. `T3`/`T4` 补 tutorial 负例（`body==170` 正例 / `actor:"pupil"` / `order` 重复 / 悬空 cueId 真注入）
6. `C11` 把 `O-24-1` 的落地清单绑定「改判据必须同时改哨兵」义务
7. `A3-3`/`T1` 壳格 AC 证据强度**如实降级**，并考虑加实现方守卫
8. `E2` note 强校验后丢弃 → 或存或删

---

## 五、评审未覆盖项（如实）

- 四位评审**均未运行 Unity 测试** —— 本件不为「测试全绿」背书。
- `E3`：`RegistryKindNames` 与 `entities.yaml` 是否同源**未核**。
- `JsonStage1Lexer` 内部（既有件，非本批）。
- `clinic_room_extraction_test.cs` 的 asmdef 引用未核。
- 未逐行比对 GDD 原文（S17-1 只依 story 转述 + ADR-015 节选）。

---

## 六、修复轮结账（Phase 8b 双列 —— 已落 / 已证）

> 用户裁定（2026-10-21）：**B-1 = 收窄 AC 补前提**；**B-2 = 本批补装载测**。
> 判据：`Landed` = 改动在盘上；`Proven` = 被可复算判据钉住。**两列皆 ✅ 才算结清。**

| ID | Landed | Proven | File(s) | Proven-by（可复算判据） |
|---|---|---|---|---|
| **B-1** AC-39-03 收窄 | ✅ | ✅ | `story-002-case-list-sort-pure-function.md:36` 后加注 · `:74-79` QA Test Cases 同步加前提 | 原文不删、就地 🔁 加注（承「原文不删」纪律）。前提写明归上游写者（`IIdAuthority` 单调发号 + 新案 tick 更大），**不在本函数判据面内**；反例测 `test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix` 保留且**不记为失败** |
| **B-2** AC-5 装载半边 | ✅ | ✅ | 新 `Tests/EditMode/Tutorial/tutorial_content_provider_load_test.cs`（9 测）· `AddressablesDataProvider.cs` 加内部可注入构造 + `FetchFromByteSource` | §六 F1/F2 证伪轮见下（M1 恰 8 红 / M2 恰 1 红） |
| D8-b「共 15 条」 | ✅ | ✅ | `DataCorePreloader.cs:186` | 散文改「…共 15 条；2026-10-21 S17-3 AC-5 接线追加 48 教学内容后共 **16** 条」。数字本身由 `test_preloader_list_sources_matchPreloadedProducts` 钉（16 == `Preload()` 的 `Ensure*` 调用数） |
| C5 `ValidateReferences` 71 行 | ✅ | ✅ | `TutorialContentBinder.cs:569-669`（拆为 6 件：dispatcher `:569` · `CollectStepIds:588` · `ValidateStepReferences:597` · `MarkReferenced:621` · `ValidateReverseStepFields:636` · `ValidateNoOrphans:662`） | **2026-10-21 修整轮已拆**（用户指令「修整这些」）。错误串逐字保留。**可复算判据** = 行数实测（`python3` 扫 `Assets/Editor.Tools.Bake/TutorialContentBinder.cs` 的方法块）：本族最大 = dispatcher **17 原始 / 13 有效**，次大 `ValidateReverseStepFields` **24 / 18**，**全部 ≤ 40**；同文件次高 `ValidateExternalMediaKeys` = 38 / 25（既有，未动）。⚠️ **项目 40 行限无机械硬门**，故判据是**实测命令**而非门（与 §六 既有口径一致，不升格为「已门禁」） |
| C4 反射漏 `GetProperties` | ✅ | ✅ | `casebook_sort_test.cs:355-401` · `:403-436` | **已修**：两条反射测均同时扫 `PropertyInfo`；词表补 `sortkey`；整型面扩到 8 种宽度（`byte…ulong`）；两处补 `properties` 非空守卫；可变态测新增 `SetMethod != null` 判据。**反证 M9/M10 见 §六之二** |
| C9 `Casebook.Tests.asmdef` 非最小 | ✅ | ✅ | `Tests/EditMode/Casebook/Casebook.Tests.asmdef` · `ADR-025:121` | **已修**：删 `Sim` 与 `Gameplay.Presentation` 两条未用引用，补 `autoReferenced: false`；引用集收敛为 `Sim.Contracts · Gameplay.UI`。ADR-025 §① 同批就地 🔁 加注。**已证** = 全量复跑 `casebook_sort_test` 16 测全绿（装配收敛后仍能编译 ⇒ 两条引用确为死引用） |
| **S17-3 ④ 病名闭集（生产实现者）** | ✅ | ✅ | 新 `Editor.Tools.Bake/TutorialDiseaseNameSource.cs` · 新 `assets/data/disease_names.json` · `DataBakeMenu.cs:458-505`（注入 + ④ 三态日志）· 新 `Tests/EditMode/Tutorial/tutorial_disease_name_source_test.cs`（10 测） | **已修**（用户指令「修整这些」）：把「词表从哪来」这条路径打通 —— 此前 `ITutorialDiseaseNameSource` **生产路径零实现者**，④ 恒 `TableNotReady`、AC-48-13 只能 NOT-RUN。现 **④ 真跑**：`test_seedWithRealTable_check4IsExecutedAndClean` 断言 `AllSixChecksExecuted == true` 且 `Outcome == Clean`。**判别力反证 M5–M8、M11 见 §六之二**。⚠️ 语义边界如实：**文件不存在 ⇒ false（NOT-RUN，不阻断）**；**损坏 / 空表 ⇒ 硬失败**（空表会让扫描恒零命中 = 假绿）。⚠️ 另修一处**同族静默面**：`BakeFromRepo` 原本 `diseaseNames = null` 默认「未就绪」⇒ **忘传参的调用点（`cooked_drift_all_artifacts_test` 正是其中之一）会让 ④ 静默 NOT-RUN**；现默认注入（`diseaseNames ?? new TutorialDiseaseNameSource(repoRoot)`），「有仓根 ⇒ ④ 必跑」，由 `test_bakeFromRepo_withoutExplicitSource_stillRunsCheck4` 钉住 |
| **S17-3 ② 外部半边（cueId / clipKey）** | ✗ | ✗ | — | **未修 —— 实为内容缺口，非代码缺口**。实测：`assets/data/audio_events.json` 15 行**零 `teach_*`**；`audio_requirements.json` 与 `design/assets/specs/audio-material-manifest.md` 中 `teach`/`mentor` **各 0 命中**；`unity/Assets/Audio/` 37 个 wav **零 `teach_*`/`mentor_*`**；全库无 13 示范片段表（`grep -rln clipKey assets/data/` 唯命中 `48_tutorial_content.json` 自身）。补表须同时产出音频素材与片段本体会撞 D6 素材存在性门 ⇒ **属造内容，不属修整**，拒绝借绿。**具名归属**：`teach_*` cue + wav = 系统 44；示范片段表 = 系统 13。已如实登记为 NOT-RUN |
| **T3 字段级负例**（`order` 浮点 / `actor` 出闭集 / `aspectRatio` 畸形 / 零段 / `illustrationKey` 缺） | ✅ | ✅ | `Tests/EditMode/Tutorial/tutorial_content_validation_test.cs`：`test_check1_orderFloatLiteral_throws` · `test_check1_actorOutsideClosedSet_throws` · `test_check1_aspectRatioMalformed_throws` · `test_check1_aspectRatioZeroSegment_throws` · `test_check1_illustrationKeyMissing_throws` | **已修**（用户指令「其余 MINOR（C11 / T3 / T4 / E1 / E2）……现在修整」）：五条负例均打**真 Binder 守卫**，且每条除「抛 `[①]`」外还断言**报文点名违规值**（`StringAssert.Contains`）。**反证 M12b / M13 / M14 / M15 见 §六之二** —— 逐条停用对应守卫 ⇒ 红集精确等于预期 |
| **T4 `body==170` 正例 + 悬空 cueId 夹具去空转** | ✅ | ✅ | 同上：新 `test_check3_body170_passes` · 改 `test_check2_danglingCueId_isNotCheckedWhenTableMissing` | **已修**：① 补 `body==170` 正例，与既有 `body171_throws` 成对（原只测边界外，测不出「阈值被改小」）；② 原「悬空 cueId」测**根本没注入悬空 id**（`Seed()` 原样送进去）⇒ 语义空转；现真注入 `"cueId": "teach_404"` 并断言**绑定仍成功**、`bound.Narration.Count == 4`、且该 id 原样存活。**反证 M16 / M17 见 §六之二**（M17 红集为超集，已归因） |
| **C11 缺口哨兵「可观测 ≠ 可检测」** | ✅ | ✅ | `Sim/Clinic/ClinicRoomExtraction.cs:120-124`（doc 就地加注边界）· `Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs`：新 `test_furn_type_closure_absent_isDetectable_notJustObservable` | **已修**：`FurnTypeClosureEnforced` 仍是编译期常量，单看只让缺席「可观测」；新测扫 **`Sim` 装配**的全部类型与成员（`PropertyInfo` 已按 `get_`/`set_` 前缀剥名），一旦出现名字含 `FurnType` 的类型或成员（**哨兵自身除外**）而哨兵仍为 false ⇒ 硬失败。**已如实登记已知边界**：闭集落地时类型 / 成员名**须带 `FurnType`**，否则检测不到（命名约定是判据前提）。**反证 M18 见 §六之二** |
| **E1 未使用形参** | ✅ | ✅ | `Editor.Tools.Bake/TutorialContentCookedWriter.cs:101-131` | **已修**：`OrderDemonstrations` / `OrderPapers` 删去从不读取的 `List<TutorialStep> steps` 形参（排序键 = `StepOrdinal(StepId)` 闭集下标，与 `steps` 内容无关）并就地 🔁 加注。**Proven = 编译器强制**（删形参后任何真实依赖都会断编译）+ 全量回归零回归（本件无行为改动，故**无证伪靶**，如实登记） |
| **E2 校验后丢弃的 `note`** | ✅ | ✅ | `Editor.Tools.Bake/TutorialContentBinder.cs` 的 `ParseDemonstrations` / `ParsePapers`（`TryReadString(row, "note", …, out _)`） | **已修**：原写法取到值后只剩一句空语句，把「校验但不使用」伪装成「使用了」；现 `out _` 让意图成为语法，并注释写明 `note` 是**必填且非空**的作者态元数据、**刻意不入模型 / 不入 cooked**（48 零 UI）。**Proven = 编译器强制**（`out _` 无法再被误用）+ 全量回归零回归（无证伪靶，如实登记） |

> 🔁 **2026-10-21 批 2（原文不删 · 就地加注）**：上五行原为**一行**——
> 「`C11/T3/T4/E1/E2` 等 MINOR | ✗ | ✗ | — | **未修**，逐条列在 §四「建议优先修」，留批 2
> （本修整轮只做用户点名的四项：C5 / C9 / C4 / ④ 词表）」。
> 用户当轮追加指令：**「其余 MINOR（C11 / T3 / T4 / E1 / E2）未动 …… 现在修整。」**
> ⇒ 按项上拆为五行、逐行结账；原一行保留于本注，**不删**。
| A3-3 壳格 AC 证据强度 | ✅ | ✅ | `Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs:111-155` | **已修**（用户 2026-10-21 裁定「先修夹具再收口」）。原夹具只是「两簇中间没格」，无判别力；现把壳格表达为契约允许的**空足迹实例**，并加「与无壳格基线逐项相等」三条断言。**反证 M3**：注入 instance-as-cell 混淆 ⇒ 恰 2 条红（本测 + `test_view_reporting_empty_footprints_returns_room_none`）✅。**判据边界如实**：23 若违规上报**非空**外壳格，24 在类型层面无从识别 —— 那一半是提供方契约 |

### 六之一 · 修复轮回归与证伪（B-2 专项）

**绿基线**（截至 `2026-10-21 06:39 CST` · sprint-17 批 1 补测轮 · 工作树基于 `1dde232` + 未提交改动）：

| total | passed | failed | skipped |
|---|---|---|---|
| **4180** | **4144** | **0** | **36** |

与批 1 验收入口 `4171 / 4135 / 0 / 36` 对账：**Δtotal = Δpassed = +9**，
新增按全名枚举 = `tutorial_content_provider_load_test` 的 **9** 条 ⇒ **消失 0**。

**证伪轮（2/2 精确）** —— 预期先写于 `/tmp/fz-out/EXPECT-s17b.md`（时间戳 06:41，早于任何变异运行）：

| 例 | 变异 | 预期转红 | 实际转红 | 判定 |
|---|---|---|---|---|
| **M1** | `Load` 的 `typeof(TDataSet) == typeof(TutorialContentTable)` → `typeof(TutorialPaperRow)` | 8 条（全部正向 + 负向，除 `unknownType`） | 恰 8 条，名单逐一相符 | ✅ `EXPECTED_NOT_RED: []` / `UNEXPECTED_RED: []` |
| **M2** | 删 `EnsureTutorialContent()` 的 `if (_tutorialContent.HasValue) return;` | `test_provider_load_twice_fetchesBytesOnce` | 恰该 1 条 | ✅ `EXPECTED_NOT_RED: []` / `UNEXPECTED_RED: []` |
| **M3** | `CollectFurnitureCells` 注入 instance-as-cell 混淆（空足迹实例亦造格） | `test_two_clusters_with_shell_bridge_still_two_clusters` · `test_view_reporting_empty_footprints_returns_room_none` | 恰该 2 条 | ✅ `EXPECTED_NOT_RED: []` / `UNEXPECTED_RED: []` |
| **M4** | `TutorialContentSchema.MaxTextChars` 170 → 171（阈值边界 off-by-one） | `test_check3_text171_throws` | **2 条**：`text171` + `test_check3_body171_throws` | ⚠️ **预期误算一条，已归因**：`MaxTextChars` 是 `narration.text`(`Binder:726`)与 `papers.body`(`Binder:735`)的**单源常量** ⇒ 一处变异必红两条，**不是夹具过宽**。`EXPECTED_NOT_RED: []`；`UNEXPECTED_RED` 的 1 条经此归因后判为**预期内**(登记为我的预测不完整，非门失败) |

- 三例 `total = 4180` / `skipped = 36`（与绿基线同）⇒ 排除「变异破坏编译致少跑」的假存活。
- 还原 `RESTORE_BYTE_IDENTICAL=YES`：`AddressablesDataProvider.cs` SHA-256 `6667d4b0…34fdbd`；
  `ClinicRoomExtraction.cs` SHA-256 `82522a09…ede5b`（未用 `git checkout` / `git stash`）。
- `test_provider_load_unknownType_stillThrowsUnknownType` 在 M1 下**仍绿** —— 符合预期，它本来就断言未知类型兜底。

### 六之二 · 修整轮（2026-10-21）回归与证伪

> 触发：用户在批 1 收口后下指令「**修整这些**」，点名四项 ADVISORY：
> `ValidateReferences` 71 行 · `Casebook.Tests.asmdef` 两条未用引用 ·
> ④ 病名表 / cueId·clipKey 外部表 NOT-RUN · casebook 反射断言漏 `GetProperties`。

**绿基线（修整轮前）**：`4180 / 4144 / 0 / 36` @ `2026-10-21 06:39 CST` · 批 1 补测轮 · `1dde232` + 未提交改动。

**绿基线（修整轮后）**：`4191 / 4155 / 0 / 36` @ **`2026-10-21 07:31 CST`** · **批 1 修整轮（收尾确认轮）** ·
工作树基于 **`1dde232`** + 未提交改动（`unity/Logs/s17-b1h-editmode.xml` —— **证伪全部还原之后**再跑的一轮；
中间轮 `s17-b1g` @ 07:28 为同值，`s17-b1f` @ 07:26 为 `4190 / 4154 / 0 / 36`（默认注入改动前），
`s17-b1d` @ 07:15 为 `4190 / 4153 / 1 / 36`）。

对账：**Δtotal = Δpassed = +11**，新增按全名枚举 = `tutorial_disease_name_source_test` 的 **11** 条
⇒ **消失 0，零回归**。（`s17-b1d` 那一轮 `4190 / 4153 / 1 / 36` 中的 1 条是我自己写错断言面
（把「④ 命中」当成「Bind 成功后看计数」，而生产行为是**硬失败抛异常**），已改为断言抛出的报文，
**非产品缺陷**；`disease names` 由 10 增至 11 是后补的「默认注入」判别力测。）

**绿基线（批 2 MINOR 收口 · 收尾确认轮）**：**`4198 / 4162 / 0 / 36`** @ **`2026-10-21 08:02 CST`** ·
批 1 批 2-MINOR 修整轮 · 工作树基于 **`1dde232`** + 未提交改动
（`unity/Logs/s17-b1i-editmode.xml` —— **M12–M18 与 M12b 全部还原之后**再跑的一轮）。
对账（按 `fullname` 集合差，非算术）：**Δtotal = Δpassed = +7 · 消失 0**，新增恰为
T3 五条 + `test_check3_body170_passes` + `test_furn_type_closure_absent_isDetectable_notJustObservable`；
**Tutorial 四件现共 73 测**（validation **43** · runtime table 10 · provider load 9 · disease names 11），
ClinicMachine 房间析出 **22 测**。⇒ **零回归**。

**证伪轮（7/7 精确）** —— 三批预期均**先写**于 `/tmp/fz-s17e/EXPECT{,2,3}.md`：

| 例 | 变异（生产守卫停用/改坏） | 预期转红（精确集） | 实际 | 判定 |
|---|---|---|---|---|
| **M5** | `TutorialDiseaseNameSource.ExtractNames` 末尾「零词条 ⇒ errors.Add」整条删除 | `test_emptyTable_throwsRatherThanScanningNothing` | 恰该 1 条（`total=10`） | ✅ |
| **M6** | `TryGetDiseaseNames` 的 JSON 不合法分支改为 `return false`（损坏降级成缺表） | `test_corruptJson_throwsRatherThanDegradesToNotRun` | 恰该 1 条 | ✅ |
| **M7** | `ExtractNames` 去重判据 `if (!names.Contains(...))` 去掉 | `test_realTable_dedupesButKeepsEveryDistinctName` | 恰该 1 条 | ✅ |
| **M8** | `TryGetDiseaseNames` 首行无条件 `names=null; return false;`（真表永不就绪） | 3 条：`realTable_isReady` · `check4IsExecutedAndClean` · `injectedDiseaseName_hits` | 恰该 3 条（`total=10`） | ✅ |
| **M9** | `CaseEntry` 加 `public int DisplayOrder => 0;`（**属性式**展示序） | `test_casebookTypes_haveNoDisplayOrderOrIndexField` | 恰该 1 条（`RAN=16`） | ✅ |
| **M10** | `CaseListOrdering` 加 `public static int Scratch { get; set; }`（带 setter 的属性） | `test_casebookTypes_haveNoMutableState` | 恰该 1 条（`RAN=16`） | ✅ |
| **M11** | `BakeFromRepo` 的 `diseaseNames ?? new TutorialDiseaseNameSource(repoRoot)` **回退**为 `diseaseNames`（默认回到 null = NOT-RUN） | `test_bakeFromRepo_withoutExplicitSource_stillRunsCheck4` | 恰该 1 条（`RAN=11`） | ✅ |

**证伪轮（批 2 MINOR · 7 例 + 1 次重跑）** —— 预期**先写**于 `/tmp/fz-s17e/EXPECT4.md`（M12–M18）
与 `/tmp/fz-s17e/EXPECT5.md`（M12b）；全部还原均 **SHA-256 逐字节一致**
（`TutorialContentBinder.cs` = `af3861…20f` · `TutorialContentSchema.cs` = `120a18…237` ·
`ClinicRoomExtraction.cs` = `90ae32…fd6`）。

| 例 | 变异（生产守卫停用/改坏） | 预期转红（精确集） | 实际 | 判定 |
|---|---|---|---|---|
| **M12** | 锚串 `n.Kind != JsonNodeKind.Integer` 替换为 `false`（`replace(...,1)`） | `test_check1_orderFloatLiteral_throws` | **零红（`RAN=43`）** | ❌ **变异打错靶** —— 该串在文件里出现 **2 次**，`replace(...,1)` 命中的是第一处 `ReadSchemaVersion`（`:323`），不是 `order` 走的 `ReadInt`（`:927`）。**零红不是「无判别力」**。已重跑 M12b |
| **M12b** | 唯一定向 `ReadInt` 整型守卫：`if (!row.TryGet(key, out JsonNode n) \|\| n.Kind != JsonNodeKind.Integer)` 后半改 `false` | `test_check1_orderFloatLiteral_throws`（恰 1） | 恰该 1 条（`RAN=43`） | ✅ |
| **M13** | 停用 `TutorialContentSchema.IsDemonstrationActor(actor)` 闭集校验 | `test_check1_actorOutsideClosedSet_throws` | 恰该 1 条 | ✅ |
| **M14** | 停用 `TutorialContentSchema.TryParseAspectRatio` | `aspectRatioMalformed` + `aspectRatioZeroSegment` | 恰该 2 条 | ✅ |
| **M15** | 不再读 `illustrationKey`（改字面量） | `test_check1_illustrationKeyMissing_throws` | 恰该 1 条 | ✅ |
| **M16** | `TutorialContentSchema.MaxTextChars` 170 → 169 | `test_check3_text170_passes` + `test_check3_body170_passes` | 恰该 2 条 | ✅ |
| **M17** | ② 的「表缺席 ⇒ 未就绪(不抛)」改为 `errors.Add` | `test_check2_danglingCueId_isNotCheckedWhenTableMissing` | **12 条**（命名目标在内） | ⚠️ **红集 = 预期的真超集，已归因**：变异作用于 **`null-source` 公共路径** —— 12 条测试都**不注入**病名表，故一并转红；**不是夹具过宽**（同 M4 的形态）。`EXPECTED_NOT_RED: []` ⇒ 命名目标的判别力成立 |
| **M18** | 向 `Sim` 装配注入含 `FurnType` 的探针类型（`FurnTypeRegistryProbe`） | `test_furn_type_closure_absent_isDetectable_notJustObservable` | 恰该 1 条（`RAN=22`） | ✅（探针已删；**遗留 meta 见下方自查**） |

**本轮两处自查记过（如实登记，禁借绿）**：

1. **M12 首跑的零红是打错靶，不是无判别力。** 根因 = 证伪脚本用 `str.replace(old,new,1)`，
   而 `n.Kind != JsonNodeKind.Integer` 在 `TutorialContentBinder.cs` 里出现 **2 次**（`:323` 的
   `ReadSchemaVersion` 与 `:927` 的 `ReadInt`），脚本静默命中了前者。
   **已加的纪律**：定点变异前先 `count(锚串) == 1` 断言唯一，否则拒跑（`run5.py` 即如此）；
   脚本内的 `assert old in base` **只能防「锚串不存在」，防不住「锚串不唯一」**。
2. **M18 探针的 Unity 自动 `.meta` 未被脚本清理** —— `_FurnTypeProbe.cs` 已由 `finally` 删除，
   但 Unity 在导入期生成的 `_FurnTypeProbe.cs.meta`（59 B，`guid: 85ab71fad54f0d6bbacb6b86cbbf3672`）
   留在盘上（未跟踪、未被 `.gitignore` 覆盖）。已于本轮**显式单文件删除**并复核目录只剩三件源码。
   **下次同类探针须把 `<probe>.cs.meta` 一并纳入清理**。

- **M5–M8 的假存活排除**：四例 XML 均 `total = 10`（全量 10 测全跑），不是「变异破坏编译致少跑」。
- **还原**：七例均 `RESTORE_BYTE_IDENTICAL=YES`（`TutorialDiseaseNameSource.cs` SHA-256
  `9745325a…a05820` · `CaseListOrdering.cs` SHA-256 `1ca44282…f4bad` · `TutorialContentBake.cs`
  SHA-256 `2d9c9273…dcb963`，逐字节比对，未用 `git checkout` / `git stash`）。
- **M9/M10 重跑**：`RAN=16`（16 条 casebook 测试全跑），红集各恰 1 条 ⇒ 反射断言的**属性面**
  与**可变态面**均被钉住，原 C4「属性式 `DisplayOrder` 逃逸」的洞已闭合。
- ⚠️ **M9/M10 首跑作废（如实登记，不掩盖）**：首版脚本 `--filter casebook_sort_test` 用的是**文件名**，
  而测试类名是 `CasebookSortTest` ⇒ Unity 匹配 0 条，XML `total=0`、`red=[]`。
  **那一刻的「零红」是 NOT-RUN，不是「无判别力」**——差点被读成「夹具太弱」。
  已在脚本里加 `RAN=` 计数守卫（`ran==0` 即打印「不得读成无判别力」）后重跑。
  **教训**：证伪轮的「零红」必须先分辨 **0 红 / 0 跑**；`total` 必须与预期测试数对账。

- 夹具改写本身**不新增测数**（替换已有测）⇒ 绿基线仍为 `4180 / 4144 / 0 / 36`。

**B-2 的判据边界（如实）**：本件钉的是**装载路径 + 解码产物**；「Addressables 真 catalog 命中」
仍需真 Group 构建，EditMode 不可得 —— 那一半由 `provider_wiring_gate_test` 的地址表闭合门
+ PlayMode 启动序承担。**两侧合起来才等于 AC-5 全绿，单看任何一侧都不够。**
