# F5 多病聚合接线阻塞面勘察(2026-10-22 · 只读)

> 交付物 = 本表 + 文末路径对比。**未改任何仓库文件,未 commit。**

## 一、阻塞面逐条

| # | 问题 | 结论 | 证据(file:line) |
|---|---|---|---|
| 1 | `ApplyOnset` 二次 onset 是否 fail-loud | **是**。同 `patient_id` 第二次 `DiseaseOnset` 抛 `InvalidOperationException`,理由串写「病人出现是单次事件(ADR-030 ① 甲案)」 | `unity/Assets/Sim/DiseaseSimulation/DiseaseCourseBook.cs:55-57` |
| 2 | 档案簿索引结构 | `Dictionary<int, DiseaseCourse> _byPatient` —— **每病人一条**;`DiseaseCourse` 单 `DiseaseId`。结构上装不下多 course | `unity/Assets/Sim/DiseaseSimulation/DiseaseCourseBook.cs:32-34`、`:59-60` |
| 3 | 求值侧 `D` 恒单元素 | 注释自陈:`D` 恒单元素集,`max_d` 退化恒等,`dominant d` 恒 = `course.DiseaseId`;刻意不调 `DiseaseAggregator.Aggregate`(避免「看起来已接线」) | `unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs:311-319` |
| 4 | `DiseaseAggregator` 本身 | **已完整实现且正确** —— 两个 max 独立求、argmax 并列取 `DiseaseId` 最小者、空集 `HasAny=false`。**唯一缺的是没人喂 >1 元素** | `unity/Assets/Sim/DiseaseAggregator.cs:121-175`(实现)、`:139-172`(argmax/并列) |
| 5 | 两个处置载荷不带 `disease_id` | **确认**。`EmergencyTreatmentAppliedPayload` 9 字段、`DrugTreatmentAppliedPayload` 7 字段,**均无 `disease_id`**;病人来源只有事件头 `SimEvent.Patient` | `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs:170-186`、`:193-208` |
| 6 | 处置归档按病人寻址 | `ApplyTreatment(PatientId headerPatient, ...)` 用 `Require(headerPatient)` 取**唯一** course;多 course 时无法选靶 | `unity/Assets/Sim/DiseaseSimulation/DiseaseCourseBook.cs:66-76` |
| 7 | 注册表 `payload_schema`(当前值) | `EmergencyTreatmentApplied`:`tick: i64; treatment_id: i32; actor_id: i32; polarity: 枚举; drug_potency: i64(Fix raw); half_life: i64; method: 枚举; cause: 枚举; seq: i64`。`DrugTreatmentApplied`:`tick; treatment_id; actor_id; polarity; drug_potency; half_life; seq`。**两者均无 disease_id** | `design/registry/entities.yaml:2420`、`:2436` |
| 8 | codec tag 表 | `EmergencyTreatmentApplied`:tag 1–9 已满 `(1u<<9)-1`;`DrugTreatmentApplied`:tag 1–7 已满 `(1u<<7)-1`。**加字段 = 新 tag 10 / 8,须同步 `RequireCompleteMask` 掩码** | `unity/Assets/Sim.Codec/PayloadCodec.History.cs:307-352`、`:354-395` |
| 9 | `PayloadEncoder` 分派 | 按 `EventKind` 转 `PayloadCodec.Encode((TPayload)payload)` —— **加字段不需改分派**,只需改 struct + codec | `unity/Assets/Sim.Codec/PayloadEncoder.cs:89-90` |
| 10 | `PayloadDecoder` 分派 | 无直接引用(按 `EventKind` 泛型分派在 `PayloadCodec` 内)。**结构上同样不需改** | `unity/Assets/Sim.Codec/PayloadDecoder.cs`(grep 零命中该三 Kind) |
| 11 | 构造点(生产) | `unity/Assets/Sim/Prescription/PrescribeFlow.cs:327`(Drug)· `unity/Assets/Sim/EmergencyProcedures/HostEmergencyProcessor.cs:216`(Emergency) | 同左 |
| 12 | 构造点(测试) | Emergency:`Tests/EditMode/EmergencyProcedures/aggregate_stream_test.cs:79,106` · `Telemetry/f3_f7_wiring_test.cs:100,109` · `MedConsequences/medcons_aggregation_test.cs:83` · `Sim/sim_codec_roundtrip_test.cs:85` · `DiseaseSimulation/vitals_chain_test.cs:97` · `PrescriptionMedication/drug_event_test.cs:626`。Drug:`MedConsequences/medcons_aggregation_test.cs:93` · `Sim/sim_codec_roundtrip_test.cs:99` · `DiseaseSimulation/f4_wiring_test.cs:95,235` · `DiseaseSimulation/vitals_chain_test.cs:86` · `MedConsequences/medcons_outcome_judge_test.cs:75` · `PrescriptionMedication/drug_event_test.cs:586,618,671` | `grep -rn` 全量输出见本节 |
| 13 | 是否有「同病人第二种病」的 P0 路径 | **代码侧无**。`PatientSpawner.SpawnNext` 每次都 `_idAuthority.NextPatientId()` 发**新** id,无「给已有病人加病种」的入口 | `unity/Assets/Sim/DiseaseSimulation/PatientSpawner.cs:59-77` |
| 14 | 7a 折叠是否已支持多病 | **已支持** —— `HistoryFolder.Collect` 的 `onsets` 字典键是 **`DiseaseId`** 不是 `patient_id`;`PatientOutcomeChanged` 亦按 `DiseaseId` 索引并「每病人每病种至多一条终态」(ADR-032 ⑤) | `unity/Assets/Sim/HistoryFolder.cs:94-131` |
| 15 | ⚠️ 去重键口径冲突 | `entities.yaml` 的 `DiseaseOnset` 去重键写 **`(onset_tick, patient_id)`**(「同一病人在同一 tick 只出现一次」);而 `HistoryFolder` 去重按 **`DiseaseId`** 并引用该键。**同一 tick 同时 onset 两种病会被前者拒、后者接受** | `design/registry/entities.yaml:2460` vs `unity/Assets/Sim/HistoryFolder.cs:110-112` |
| 16 | `compounds` 的 C# 落地 | **桩实现**。`ComputeCompounds` 从 `ActorCellEntered` 事件硬编码推 `patient_1`,忽略 `when`/`bonus`,无 `compoundMap` 真实数据源。**生产侧 `CompoundTriggered` 零写入者**(仅 payload struct + 路由) | `unity/Assets/Sim/CatchUp.cs:90-127`(桩)· `unity/Assets/Sim/StreamRouting.g.cs:26-27`(仅路由) |
| 17 | `compounds` schema 落地 | **`DiseaseRegistryEntry` 无 `compounds` 字段**;`disease_registry.json` 自陈「旗舰共病线当前不可求值,归 9 的 compounds 实现轮」 | `unity/Assets/Sim/RegistrySchema.cs:172-235`(无该成员)· `assets/data/disease_registry.json:9` · `design/gdd/disease-simulation.md:1677-1679` |

## 二、`compounds` vs 多病聚合:关系结论

**两者是不同层次的东西,不是同一条。** GDD F5 把二者的分工写死了:

| 维度 | 多病聚合(`position_agg = max`) | `compounds` 显式加成 |
|---|---|---|
| 作用面 | **出口合成量**(9 的阈值判定 / 8 的读数) | **目标病种自己的 `Progress` 曲线** |
| 数学形态 | `max_{d∈D} position_d(t)` —— 取大,幂等 | 向 `Progress_{target} += bonus.Δ_progress` —— **加性阶跃** |
| 是否产生新病种 | 否。`D` 是**已存在**的病程集合 | **是**。触发后可让 `target` 从"无"变"有"(`DIS_RHEUMATIC_FEVER → DIS_HEART_FAILURE`) |
| 事件 | 无(纯求值,不进流) | `CompoundTriggered` / `CompoundExpired`(进病史流) |

GDD 原文两处钉死「加成不在聚合量上加」:

> 「**`compounds` 的加成是「向目标病种的 Progress 加一项」**,不是在聚合量上加 —— 这样共病恶化对**目标病种自己的曲线**可见,也保持了「取大」的语义。」 —— `design/gdd/disease-simulation.md:1342-1345`

> 「**为什么合成只在出口**:各 `d` 的病程**必须独立**(否则「治好了关节、心脏也跟着好」—— 恰是 R3.4 明令禁止的医学错误)。**合成只发生在 ①8 的读数 ②9 的阈值判定两处。**」 —— `design/gdd/disease-simulation.md:1340-1342`

**⇒ 结论**:`compounds` **不能替代**多病聚合,也**不隐含**它。`compounds` 解决「病 A 恶化了病 B 的曲线」;多病聚合解决「病人身上同时有 B 和 C 时,阈值判定读谁」。**即便 `compounds` 全部实现,只要档案簿一病人一 course,`D` 仍是单元素,`max` 仍退化**。

**但两者有一个耦合点**:`compounds` 触发后 `target` 病种需要**进入 `D`** —— 即需要一条"给已有病人新增病种"的路径。这条路径**同时**是多病聚合的前置。⇒ **两者的共同阻塞是同一个:档案簿的单 course 假设。**

## 三、ADR-030 ① 甲案是权威裁决还是实现选择

| 问题 | 结论 | 证据 |
|---|---|---|
| ADR-030 ① 原文说的是什么 | 「**病人出现**」= **病程 onset**(同一个事件)。**不立第二个 Kind**。理由是两条事件「同一时刻、同一主体、同一因果」且「零独立信息」 | `docs/architecture/adr-030-disease-onset-kind.md:112-122` |
| 它是否裁了「每病人至多一次 onset」 | **没有**。ADR-030 全文 grep `多病` / `第二幕` / `重复` / `再发` / `多次` —— **零命中**。它裁的是 **Kind 归属**(不立 `PatientAppeared`),不是**基数** | `grep` 输出为空(ADR-030 全文) |
| 那「单次事件」这句从哪来 | **`DiseaseCourseBook.cs:56` 的实现注释**自造。ADR-030 的「同一个事件」被实现读成「同一病人只发生一次」—— **语义漂移** | `unity/Assets/Sim/DiseaseSimulation/DiseaseCourseBook.cs:56` |
| GDD 对「同病人多病」怎么表达 | **明确要求多病,且是 P0 常态**。三处:① F5 整节「多人多病合成」;② Edge Cases「同一病人多种伤情 / 多种病」;③ 复杂度 K3 注「**P0 病人多病 / 多次 onset 是常态**(共病线 / 第二幕)」 | `design/gdd/disease-simulation.md:1294` · `:1367` · `:1021` |
| 有没有可判的 AC | **有,BLOCKING · Gate(C2)**:`AC-31`「病人同时有 `position = 3/10` 与 `4/5` 两个病,`position_agg = 4/5`」 | `design/gdd/disease-simulation.md:2041` |
| `CompoundTriggered` 是不是那条路径 | **部分**。它能让 `target` 病种获得 `bonus`,但 GDD 未说它**创建** target 的 `DiseaseOnset`;且它当前是桩 + 零写入者 | 见 §一 #16/#17 |
| 所以「第二种病」在 GDD 里走哪条 | **GDD 未指定**。F5 假设 `D` 已存在,却没写 `D` 如何增长。**这是 GDD 侧的缺口**,不是 ADR 侧的裁决 | 见 §四 未查清项 |

**⇒ 结论**:「单次事件」**不是权威裁决**,是**实现选择**;且该实现选择**与 GDD 的 AC-31(BLOCKING)直接冲突**。ADR-030 不需要被推翻 —— 它只裁了 Kind 归属,没裁基数。**需要的是 9 的 GDD 补一条「`D` 如何增长」的规则 + `DiseaseCourseBook` 的结构改动。**

## 四、路径对比(不替用户选)

### 路径 A:档案簿改多 course(最小可证)

**改动面**
- `unity/Assets/Sim/DiseaseSimulation/DiseaseCourseBook.cs`:`_byPatient` → `Dictionary<int, List<DiseaseCourse>>`;`ApplyOnset` 去重键改 `(patientId, diseaseId)`;`ApplyTreatment` 增加靶病种参数
- `unity/Assets/Sim/DiseaseSimulation/DiseaseCourse.cs`:无需改(已单病种)
- `unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs:311-319`:改为对 `course` 列表调 `DiseaseAggregator.Aggregate`
- `unity/Assets/Sim/HistoryFolder.cs:110-112`:已按 `DiseaseId` 去重,**无需改**(反而是它的口径更正确)
- **`DiseaseOnset` 载荷 / codec / entities.yaml:零改动** —— `DiseaseOnsetPayload` 已带 `disease_id`,同病人再发一条即可

**权威件动作**
- `design/gdd/disease-simulation.md`:补「`D` 如何增长」规则(谁在何时为已有病人发第二条 `DiseaseOnset`)+ 修正 `:174` 边界表口径
- `design/registry/entities.yaml:2460`:**订正去重键** `(onset_tick, patient_id)` → `(onset_tick, patient_id, disease_id)`,否则同 tick 双病被拒
- ADR-030:**加注不改写**(「单次事件」非本 ADR 裁决);若用户认为需明确,可走窄修订
- 无需新 ADR(不触任何抽象点 / 程序集边)

**风险**:低。不触 `Sim` 引用集、不触门 A、不触 codec 字节格式、不破既有存档(只是允许更多事件)。
**代价**:处置事件仍**无法路由到具体 course** —— 多病时 `ApplyTreatment` 只能把剂量加到「某一支」,需先裁靶。⇒ **路径 A 只解 `position_agg`,不解处置路由。**

### 路径 B:路径 A + 两个处置载荷加 `disease_id`

**改动面**(在 A 之上)
- `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs:170-208`:两 struct 各加 `int DiseaseId` + 构造参数
- `unity/Assets/Sim.Codec/PayloadCodec.History.cs:307-352`:`Emergency` 新 tag **10**,掩码 `(1u<<10)-1`;`:354-395`:`Drug` 新 tag **8**,掩码 `(1u<<8)-1`
- `unity/Assets/Sim.Codec/PayloadEncoder.cs:89-90`:**无需改**
- 构造点 2 生产 + ~20 测试(§一 #11/#12 全量清单):全部补实参
- `unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs:37,40,93`:门里引用了两个类型名,须核字段断言是否需同步
- `unity/Assets/Gameplay.Boot/DiseaseVitalsService.cs:238,247`、`Sim/Consequences/ConsequenceInputAggregator.cs:303,309`、`Gameplay.Boot/TelemetryReporter.cs:370-374`:解码后按 `DiseaseId` 路由

**权威件动作**
- `design/registry/entities.yaml:2420` / `:2436`:`payload_schema` 各加 `disease_id: i32`
- **ADR 窄修订**:`ADR-009 Amendment I` 定了这两个 Kind 的载荷形状(七项 / 形状与前者一致);加字段须窄修订或在 10 / 11 的 GDD 轮走「系统 ADR 追加」通道
- `design/gdd/emergency-procedures.md`(10)· `design/gdd/prescription-and-medication.md`(11):载荷定义方
- **ADR-024 A2 不受影响**:`disease_id: i32` 属整数域白名单,断言仍过(`docs/architecture/adr-024-kind-single-source.md:161`)

**风险**:**高,且有一处硬阻塞**。

> ⚠️ **硬阻塞(本轮新发现)**:`unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs:68-82` 的
> `DiseaseNameForbidden` 词表**逐字禁止 `"DiseaseId"` 符号**出现在 `Sim.Prescription` 命名空间
> (扫描面 = `PrescriptionNamespacePrefix`,**实现面,间接引用 / 只读一处也算**)。
> 出处 = `TR-prescription-001`「**零 diagnosis / disease_id / tier_named 引用**」+ 11 规则一。
> ⇒ **给 `DrugTreatmentAppliedPayload` 加 `DiseaseId` 字段,11 侧 `PrescribeFlow` 构造它时即触红**。
> 这不是可以顺手绕的门 —— 它是 11 的**设计原则**:11 按**症状**开方,不按病种。

> ⚠️ **第二处**:`PrescriptionWriterGates.cs:96` 的 `TenSideOnlyFields = { "Method", "Cause" }` ——
> 该门要求 11 / 10 两侧载荷**除这两项外字段名 / 类型 / 序数完全一致**。
> ⇒ 只给 10 侧(`EmergencyTreatmentAppliedPayload`)加 `DiseaseId` 也会触红。

**⇒ 路径 B 须拆为 B1 / B2**:
- **B1(只解 10 侧)**:`EmergencyTreatmentAppliedPayload` 加 `DiseaseId`;`DrugTreatmentAppliedPayload` **不加**。
  须同步改 `TenSideOnlyFields` 为 `{ "Method", "Cause", "DiseaseId" }` 并写明理由(10 是「对某个病人做某个动作」,
  11 是「按症状开方」)。**这本身是一次对 11/10 载荷对称性的裁决,须 ADR 窄修订**。
- **B2(11 侧的替代路由)**:11 不加 `DiseaseId`,处置路由改用**事件头 + 时间就近**规则
  (药作用于 onset 最早 / 当前 `position` 最高的 course),或由 9 侧在消费时按 `polarity` + `treatable_by` 匹配。
  **代价**:路由是**推断**而非**事实**,多病时可能加错 course —— 与 AC-16「Append 顺序即权威」的确定性取向相悖。

**其余风险**:字节格式变更 ⇒ 所有既有存档 / 黄金夹具失效(ADR-012 集成级字节夹具须重签 `golden-vN`);~20 处测试构造点全改。
**收益**:处置路由闭合,`ApplyTreatment` 可精确到 course。

### 路径 C:仅解除 fail-loud,`D` 仍单元素(最小改动,但**不解决问题**)

**改动面**:只改 `DiseaseCourseBook.cs:55-57` 去掉抛,改为「覆盖 / 忽略」。
**权威件动作**:无。
**风险**:**高** —— 静默丢病种(第二条 onset 被忽略 = 病人少一个病),且 `position_agg` 仍退化。**这是假绿路径**(AC-31 仍红,但看起来"不抛了")。**不建议**,列出仅为说明「最小改动 ≠ 最小可证」。

### 「最小可证」判定

**路径 A 是最小可证**:它用**一条 AC-31 单测**(两病种 → `position_agg = 4/5`)即可证伪/证实,且**不动字节格式、不动抽象点、不动程序集边**。路径 B 是 A 的必要后继(处置路由),但可拆为独立 story。

## 五、明确没查清的事

1. **GDD 未定义 `D` 如何增长** —— 谁在什么条件下为已有病人发第二条 `DiseaseOnset`?52 的疫情事件?`compounds` 触发时自动建?还是 `PatientSpawner` 之外的另一个 spawner?**全库未找到答案**。这是**最大的缺口**,路径 A / B 都依赖它。
2. **`compoundMap` 的真实数据源** —— `ComputeCompounds` 的 `Dictionary<string, List<string>>` 参数无任何生产调用点,`disease_registry.json` 也无 `compounds` 块。
3. **ADR-032 已预设逐病种终态**(`:178`「终态是**逐病种**的,每病人每病种至多一次终态」· `:184`「每病人每病种至多写一次」)—— **这是多病为设计预期的强旁证**,但未通读全文确认它是否也预设了多 `DiseaseOnset`。
4. **测试构造点 ~20 处的具体实参语义** —— 未逐个确认补 `DiseaseId` 时该填什么(部分测试可能本就不该有靶病种)。
5. **`ApplyTreatment` 之外的剂量消费路径** —— `DiseaseVitalsService.cs:238-248` 解码后如何把剂量交给 course,未逐行读;多 course 时该处也要改。
6. **`DiseaseCourse` 是否可承载「同病种二次 onset」**(复发 vs 新发)—— GDD 有 `relapse.*` 复发块,若「同病种再发」走 relapse 而非新 onset,则 `(patientId, diseaseId)` 去重键仍成立;未核。

## 五之二、⚠️ AC-31 / AC-32 现有测试是假绿(本轮实测)

| AC | 声称 | 实际测试体 | 判定 |
|---|---|---|---|
| **AC-31**(多病取大 · BLOCKING · Gate C2) | 「病人同时有 `3/10` 与 `4/5` 两个病,`position_agg = 4/5`」 | `catchup_replay_test.cs:66-79` —— **本地造 `List<Fix>{100,300,200}` 手写 for 循环取大**。**零调用** `DiseaseAggregator.Aggregate`,**零构造** `DiseaseCourseBook`,**零 `DiseaseOnset`**。它测的是 `for` 循环会不会取最大值。 | **假绿**。AC 的语义层(多病 → 聚合)完全未被触碰。 |
| **AC-32**(compounds 触发 · BLOCKING · Gate C2) | 风湿热持续 → `DIS_HEART_FAILURE` 的 `Progress` 出现 `bonus` 增量 + 落 `CompoundTriggered` | `catchup_replay_test.cs:81-99` —— 断言 `compounds.Count >= 0`(**恒真**),`Assert.IsNotNull(compounds)`(**恒真**,桩函数必返回非 null List) | **假绿**。恒真断言。 |

**⇒ 这是本项目已抓过的第四形态「自造断言顶替 AC 语义层」在 F5 上的实例。**

**⚠️ 必须精确区分两个层次,否则会误判「聚合器没测」**:
- **单元层 = 真的、且测得好**:`DiseaseAggregator.Aggregate` 有 10 条真单测
  (`care_lever_and_aggregator_test.cs:130,144,156,157,168,180,191,199,209,218`),
  覆盖取大、argmax 并列取小 id、两个 max 独立、空集 / null。**这一层是绿的、可信的。**
- **集成层 = 假绿**:AC-31 的测试(`catchup_replay_test.cs:66-79`)根本没走
  `CourseBook → Aggregator` 这条路,而是本地重写了取大逻辑。
  ⇒ **未被验证的是「档案簿能产出 >1 元素」这件事**,而这恰是 F5 唯一的真缺口。

**含义**:「F5 接不上」比任务描述更严重 —— 不是聚合器写错了(它是对的),
而是**「喂给它的集合永远是单元素」这件事没有任何 AC 在守**。
路径 A 若要「最小可证」,**须新增一条真集成测试**(`DiseaseCourseBook` + 两条 `DiseaseOnset` + `Aggregate`),
并**就地订正 AC-31 的假绿测试**(承「原文不删,加注订正」纪律)。

**证据命令(可复跑)**:
```
sed -n '65,100p' unity/Assets/Tests/EditMode/DiseaseSimulation/catchup_replay_test.cs
grep -rn "DiseaseAggregator.Aggregate" --include=*.cs unity/Assets/Tests
```

## 六、一句话结论

> 「`DiseaseOnset` 单次」是 `DiseaseCourseBook.cs:56` 的**实现自造**,不是 ADR-030 的裁决
> (ADR-030 只裁 Kind 归属,全文零「多病 / 多次」字样)。它与 GDD **AC-31(BLOCKING·Gate C2)**
> 直接冲突。`compounds` **不是**多病聚合的替代 —— GDD `:1342` 明写加成「不是在聚合量上加」。
> **两者共用一个阻塞:档案簿的单 course 假设。** 最小可证路径 = 路径 A(改档案簿,零字节格式改动),
> 用 AC-31 一条单测即可证伪;处置路由(路径 B)是独立后继,且 11 侧有一条**禁 `DiseaseId` 的 IL 门**挡着。
