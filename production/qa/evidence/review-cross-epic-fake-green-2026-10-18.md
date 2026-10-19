# 跨 epic 假绿普查(评审原件)

- **日期**:2026-10-18
- **对象**:除 `audio-system` / `case-system` 外的 **31 个 epic · 158 篇 `Complete` story**
- **性质**:只读勘察 + 编排方逐条自验。**未跑任何 Unity 测试**(见 §五 未核查 1)。
- **判据**:全部结论附**机械 grep 实测输出**;**读散文得出的印象不判假绿**;判不准写「存疑」,不填表。
- **触发**:retro-14 行动项 2(= retro-13 行动项 3+4 合并)——「抽一个已 Complete 的其他 epic 做假绿抽查,
  判断是否系统性」**已欠两个 sprint**;本件即还它。
- **排除范围**:`audio-system`(`TD-audio-100…109` 已逐条对码 + S15-7 刷账)· `case-system`(001–004 已补评)。
- **草稿处置**:本件由子代理草稿 + 编排方订正合并而成;**草稿原判被推翻一处,已就地留档**(§三 形态 3)。

---

## 一、候选池(第 1 步)

`grep -rl "Status\**: Complete" production/epics/*/story-*.md`

| epic | Complete story 数 |
|---|---|
| skeuomorphic-ui | 19 |
| input-system | 13 |
| item-database | 12 |
| telemetry-analytics | 10 |
| skill-system | 8 |
| interaction-system | 7 |
| emergency-procedures | 7 |
| disease-simulation | 7 |
| random-events | 6 |
| player-controller | 6 |
| combat-weapons | 6 |
| camera-viewpoint | 6 |
| world-ecozones | 5 |
| time-weather | 5 |
| prescription-medication | 5 |
| enemy-ai | 5 |
| diagnosis-system | 5 |
| patient-ai | 3 |
| medical-consequences | 3 |
| persistence-service | 2 |
| modular-building | 1 |
| **合计(不含 audio/case)** | **158** |

---

## 二、实测判定表

| # | epic | story | AC | 形态 | 证据(file:line + grep 输出) | 严重度 |
|---|---|---|---|---|---|---|
| 1 | telemetry-analytics | story-002-f1-judgment-accuracy | AC-51-B1…B4 | 2(自持谓词面) | `unity/Assets/Tests/EditMode/Telemetry/f1_judgment_accuracy_test.cs:24` = `// ══ F1 公式(测试自持谓词面,零生产依赖)══`;`grep -rn "JudgmentMetrics\|ITelemetrySource" unity/Assets/ --include=*.cs \| grep -v /Tests/` ⇒ **零命中**(51 程序集全库不存在:`find unity/Assets -ipath "*telemetry*" -not -path "*/Tests/*"` 空) | **High** |
| 2 | telemetry-analytics | story-003-f2-misdiagnosis-matrix | AC-51-B 族 | 2 | `f2_misdiagnosis_matrix_test.cs:25` 同型「测试自持谓词面,零生产依赖」;生产侧零命中(同上 grep) | **High** |
| 3 | telemetry-analytics | story-004-f3-skip-rate | AC-51-B9 等 | 2 | `f3_skip_rate_test.cs:24` 同型;`f3_skip_rate_test.cs:160` `Assert.Ignore("AC-51-B9:BLOCKED-BY-OQ-10-8 …不得记绿")` 而卡面 `Status: Complete` 且 AC 盒未勾 | **High** |
| 4 | telemetry-analytics | story-005-f4-skill-growth | AC-51-B 族 | 2 | `f4_skill_growth_test.cs:25` 同型;生产侧零命中 | **High** |
| 5 | telemetry-analytics | story-006-f5-difficulty-curve | AC-51-B 族 | 2 | `f5_difficulty_curve_test.cs:25` 同型;生产侧零命中 | **High** |
| 6 | telemetry-analytics | story-007-f6-f7-session-rhythm | AC-51-B 族 | 2 | `f6_f7_session_rhythm_test.cs:27` 同型;`f6_f7_session_rhythm_test.cs:328` `Assert.Ignore("51 未实现,当前阶段无法扫描签名")` | **High** |
| 7 | telemetry-analytics | story-008-pure-function-recomputable | AC-51-C 族 | 2 | `pure_function_recomputable_test.cs:27` 同型;生产侧零命中 | **High** |
| 8 | telemetry-analytics | story-009-blank-and-degraded | AC-51-D1…D7 | 2 + 1 | `blank_and_degraded_test.cs:31` 同型;`:166-169` `模拟引擎状态(测试自持)` / `模拟折叠结果(测试自持)`;卡面 8 条 BLOCKING AC **全部未勾**(`grep -c "^- \[ \]\*\? \*\*AC-.*BLOCKING"` = 8) | **High** |
| 9 | telemetry-analytics | story-001-readonly-boundary-zero-egress | AC-19-01/02/04、AC-51-A1/A2/A4…A8 | 1 + 3 | `readonly_boundary_test.cs:87` `Assert.Ignore("AC-19-01:ITelemetrySource 未实现…")`;`:145` `51 asmdef 尚未创建`;`:203` `ITelemetrySink 未实现`;卡面 **11 条 BLOCKING AC 全部未勾** | **High** |
| 10 | telemetry-analytics | story-010-out-of-bounds-rejection | AC-51-E1…E5 | 3(恒真/空跑) | `out_of_bounds_rejection_test.cs:67` `Assert.Pass("51 资产目录不存在 ⇒ 无呈现层资产(AC-51-E1…)")`;`:226` `Assert.Pass("assets/data/ 目录不存在 ⇒ …字节不变(AC-51-E2…)")`;`:312` `Assert.Pass("telemetry_analytics.json 不存在 ⇒ 无 schema ⇒ 无开关(AC-51-E5…)")`;另 14 处 `Assert.Ignore/Inconclusive`;`:20` 自陈 `假绿防护(B1 修复):空命名空间 ⇒ Assert.Ignore` | **High** |
| 11 | enemy-ai | story-004-stepping-astar-and-replanning | AC-27-20(重算保光标) | 4(借绿) | `deterministic_pathing_test.cs:108` `Assert.Pass("重算保光标需集成测试验证")`;卡面 `story-004:39` `- [ ] 重算保光标…(AC-27-20)` **未勾**;`Status: Complete` | **High** |
| 12 | enemy-ai | story-005-encounter-lifecycle-injury-routing-and-output-whitelist | AC-27-34(调试视图隔离) | 4 | `encounter_lifecycle_test.cs:177` `Assert.Pass("调试视图隔离需集成测试验证")`;卡面 `story-005:46` `- [ ] 开发者调试视图三条隔离…(AC-27-34 [A])` **未勾**;`Status: Complete` | **High** |
| 13 | combat-weapons | story-006-onset-stream-dose-seq-balance-gates | AC-25-1-08(Step/CatchUp 一致性) | 4 | `combat_onset_dose_seq_test.cs:98` `Assert.Pass("Step 与 CatchUp 一致性需集成测试验证")`;卡面 `story-006:39` `- [ ] AC-25-1-08(重放半边)…` **未勾**;`Status: Complete` | **High** |
| 14 | disease-simulation | story-004-f1-progression-evaluation-f2-signs-projection | AC-20(`signs[]` 只读原始量)+ AC-13(只改 Signs) | 3(恒真/空跑) | `ProgressionEvaluator.cs:91` `private static readonly int[] NoSigns = new int[0];` `:145` `return new ProgressionResult(Fix.Zero, Fix.Zero, NoSigns);` `:169-170` `// Signs —— tripwire 恒空` `int[] signs = NoSigns;`;唯一生产消费方 `DiseaseVitalsService.cs:167-175` 恒抛/恒返 `signChannelMask: 0, signCount: 0`;`grep -rn "NoSigns\|result.Signs\|\.Signs" Tests/EditMode/DiseaseSimulation/progression_eval_test.cs vitals_chain_test.cs` ⇒ **零命中**(恒空分支无任何断言) | **Medium** |
| 15 | diagnosis-system | story-006-casebook-presentation-routing-anti-fantasy | AC-8-42 / AC-8-44 等 8 条 | 1(零消费方) | 卡面 `:45` `- [ ] **AC-8-42**[V] BLOCKING:V-8.6《他回来了》…若该病种行缺失则记 BLOCKED-BY-disease-story-003,禁借绿`;`:47` `AC-8-44`(手柄半边 NOT-RUN 缓办);卡面 8 条 BLOCKING AC 未勾;`Status: Complete` | **Medium** |
| 16 | ~~modular-building~~ | ~~story-007~~ | ~~6 条 BLOCKING~~ | **非假绿** | 复核实测:`StructureKinds.cs:148` `private readonly IPayloadEncoder _encoder;` `:167` ctor 注入 `IPayloadEncoder encoder` `:220/:242/:273` 三处写入经 encoder;测试 `ModularBuilding/structure_payload_encoder_test.cs:3-5` 逐条对应 AC-23-31…35;`AC-6-29` 同类门判据有载体。⇒ **卡面 AC 盒未勾 = 文档滞后,不是假绿** | **Low(盒态滞后)** |
| 17 | ~~world-ecozones~~ | ~~story-006~~ | ~~5 条 BLOCKING~~ | **非假绿** | 同型:`PoiStateMachine.cs:56` `private readonly IPayloadEncoder _encoder;` `:130-132` 显式记录修复过「把 poiId 当 blobId」的手搓 bug;测试 `WorldEcozones/poi_payload_encoder_test.cs:3-6` 覆盖 AC-6-27/29/30 | **Low(盒态滞后)** |
| 18 | medical-consequences | story-002-deterministic-outcome-judge | AC-53-08(D 输入接线) | 1 + 2 | `IDiseaseProjection` 生产侧**零实现者**(`grep -rn "IDiseaseProjection" unity/Assets/ --include=*.cs \| grep -v /Tests/` ⇒ 仅接口声明 `ConsequenceInputAggregator.cs:42` 与参数 `:203`,无 `: IDiseaseProjection` 实现类);测试 `medcons_outcome_judge_test.cs:91-110` `StubProjection : IDiseaseProjection`(`D 轴判据(痊愈位;本桩恒返回构造值)`);**但卡面 `:222-224` 已如实登记**「AC-53-08 的 D 输入接线仅为类型面…测试中的 stub 命中位是形状夹具,不是已验证的接线」 | **Low(已登记)** |
| 19 | persistence-service | story-001-storage-abstraction | 7 条 BLOCKING | 1 | = 7;但 Completion Notes 自陈「高层方法(Checkpoint/SaveOnExit/Load)归后续 story」;`save_service_test.cs:43-55` 校验和测试**真实**(翻转 `withChecksum[40] ^= 0xFF` 后 `Assert.IsFalse(ValidateChecksum)`) | **Low(偏差已登记)** |
| 20 | persistence-service | story-002-payload-encoder-contract | 8 条 BLOCKING | 1 | = 8;但 Status 行自陈 `✅ 2026-10-02 (8/8 AC 落地;14/14 测试通过 + 突变测试坐实)`;`IPayloadEncoder` 生产侧确有消费方(`Gameplay.Boot/PrescriptionPorts.cs:124` `private readonly IPayloadEncoder _encoder;`) | **Low(状态行与盒态不一致,非假绿)** |
| 21 | player-controller | story-003-locomotion-feel-chain / story-005 | AC-1-21 / host-authority | 3 | `locomotion_chain_test.cs:425,436` `Assert.Ignore("NOT-RUN: AC-1-21 待 OQ-1-12 接地 spike 回填")`;卡面 1 条 BLOCKING 未勾 | **Medium** |
| 22 | camera-viewpoint | story-002-yaw-basis-hard-delivery 等 | 4 条 BLOCKING | 3 | `camera_arm_solver_test.cs:219` `Assert.Ignore("NOT-RUN: 真 Physics.SphereCast 未接线…")`;`:344` `Assert.Ignore("NOT-RUN(部分):序关系已对注入表成立;真档位表(组 5)未在库")`;`camera_presentation_discipline_test.cs:429` `Assert.Ignore("NOT-RUN: 场景资产未建(ADR-023 三场景制未落地)")`;`camera_obligations_reconciliation_test.cs` 6 处 Ignore | **Medium** |
| 23 | skeuomorphic-ui | story-013/015/008/009/010/016/012/014 等 8+ 篇 | 各 3–6 条 | 3 | `Assert.Inconclusive` 集中区:`casebook_rendering_test.cs`(6 skip/11 测)· `focus_visual_and_accessibility_test.cs`(6/11)· `world_billboard_test.cs`(5/7)· `enemy_vitals_bar_test.cs`(5/10)· `tutorial_test.cs`(4/12)· `settings_shell_test.cs`(4/10)· `save_slots_test.cs`(4/9)· `paper_closeup_test.cs`(4/9)· `inventory_container_test.cs`(4/10)· `no_healthbar_feedback_test.cs`(3/7);均自陈「42 命名空间无 X 类型,当前阶段无法判定」 | **Medium** |
| 24 | emergency-procedures | story-003/006 | AC-10-04b / AC-10-08/19/23 | 3 | `judge_test.cs:356` `Assert.Ignore("NOT-RUN: AC-10-04b 跨平台黄金夹具待 ADR-012 矩阵实跑")`;`feel_latency_test.cs:183,190,197` 三处 Ignore | **Medium** |
| 25 | interaction-system | story-004/006 | AC-4-13 / F-4.3 / AC-4-17 | 3 | `discovery_report_test.cs:91` `Assert.Ignore("NOT-RUN(AC-4-13 6 半):BLOCKED-BY 系统 6 latch/幂等实现")`;`:389` `Assert.Ignore("NOT-RUN(F-4.3 收敛半):BLOCKED-BY 系统 6 的幂等实现")`;`radius_single_source_test.cs:82` 同型 | **Medium** |
| 26 | input-system | story-008 | 环境前提失配 | 3(正当) | `device_and_presentation_test.cs:795,1116` `Assert.Ignore("环境已有物理手柄 ⇒ 「移除现源后只剩键鼠」前提失配(qa-008-5:显式跳过,不假红)")` —— 显式登记、理由正当 | **Low(非假绿)** |
| 27 | patient-ai | story-003 | 条件编译剥离 | 4(部分正当) | `presentation_projection_test.cs:908` `Assert.Pass("①(已闭)…②(NOT-RUN,禁借绿)…")`;前半有真实 IL 扫描断言(`CollectionAssert.IsEmpty(hits, …)`),后半显式登记 NOT-RUN | **Low** |
| 28 | patient-ai | story-003 | B3/B4 | 4(已修) | `reconstruction_and_write_path_test.cs:682-683` `// B3 修复(2026-10-05 双代理评审):原实现 Assert.Pass 空操作 ⇒ 借绿。现改 Assert.Ignore 显式登记 NOT-RUN,CI 显示 skipped 而非 passed` | **Low(已修)** |
| 29 | telemetry-analytics | story-001 | B2 | 4(已修) | `out_of_bounds_rejection_test.cs:20` `// ⚠️ 假绿防护(B1 修复):空命名空间 ⇒ Assert.Ignore`;卡面 `:116,120` `B2 修复(Assert.Pass 假绿)` | **Low(已修)** |
| 30 | prescription-medication | story-002/005 | M2 | 3(反向硬化) | `dose_potency_test.cs:119` `// M2:目录缺失 = 环境错误,须硬失败(原 Assert.Ignore 会静默跳过 ⇒ 借绿)` | **Low(已硬化)** |
| 31 | disease-simulation | story-003 | R1-19 | 1 | `RegistrySchema.cs:351-358` `// ⚠️ 2026-10-07 评审 M1:空实现显式标记为未实现 —— 调用方无法区分「校验通过」与「校验未实现」` + `throw new NotImplementedException("R1-19: …待 disease_registry.json 存在后实现(当前结构性不可达)")` | **Low(已显式登记)** |

---

## 三、系统性问题判定(第 3 步)

**结论:是系统性的,但集中度极高,且形态分布不均。**

### 3.1 抽检数 / 命中数

| 判据 | 覆盖面 | 命中 |
|---|---|---|
| C 类(`Assert.Ignore/Inconclusive/Pass`) | **全仓 38 个测试文件**(机械枚举,无抽样) | 38 文件全部命中;其中**至少 9 篇**对应的卡面 BLOCKING AC 未勾且 `Status: Complete` |
| B 类(具名符号零生产消费方) | **全部 158 个 Complete story 的 AC 编号**(机械全扫,非抽样) | 36 个 AC 编号零生产命中,但**逐个复核后 26 个属已登记 NOT-RUN/EXTERNAL/BLOCKED-BY**;真正未登记的只有 telemetry 一族(#1–#10)与 `IDiseaseProjection`(#31,已登记) |
| A 类(自持谓词面) | 全仓 grep「测试自持谓词面,零生产依赖」 | **7/7 命中文件全在 telemetry-analytics** |
| A 类补充(Fake/Stub/Mock 类定义) | 全仓 `grep -rn "class .*Fake\|class .*Stub\|class .*Mock\|class Recording"` | 83 处,分布:PlayerController 10 · PatientAI 9 · Audio 9 · Interaction 7 · SkeuomorphicUI 6 · DiseaseSimulation 6 · MedConsequences 5 · 其余 ≤4。**抽样复核:绝大多数是合法 DI 替身**(`FakeEventSink : IEventSink` / `FakeTickProvider : ITickProvider`),不是自造语义面。唯一语义级桩 = `StubProjection : IDiseaseProjection`(#31,已登记) |
| 未核查 | player-controller(6)· skill-system(8)· item-database(12)· interaction-system(7)· random-events(6)· time-weather(5)· medical-consequences(3)· emergency-procedures 部分 AC | 未逐 AC 跑 B 类 grep |

### 3.2 形态分布(31 条命中)

| 形态 | 条数 | 占比 | 主要聚集 |
|---|---|---|---|
| 1 零消费方 | 5 | 16% | telemetry(51 程序集不存在)· diagnosis-006 · modular-007 · world-006 · medcons(IDiseaseProjection) |
| 2 自持谓词面 | 8 | 26% | **telemetry 7/7 + medcons 1(已登记)** |
| 3 恒真/空跑/跳过 | 11 | 35% | skeuomorphic-ui(10 文件)· camera-viewpoint · emergency · interaction · disease-004 的 `Signs` 恒空 |
| 4 借绿(`Assert.Pass`/未跑出写成事实) | 7 | 23% | enemy-ai(2)· combat(1)· telemetry(1)· patient-ai(2,已修)· telemetry(2,已修) |
| **5 扫不到被测装配**(症状 = 跳过,非绿) | **52 处 / 14 文件** | **单形态面积最大** | **skeuomorphic-ui(13/14 文件缺 `using Gameplay.UI`)** |

### 3.3 判定

- **系统性 = 是**:病灶**不局限于已处理的三个 epic**。四个新 epic 命中:telemetry-analytics(形态 1/2/3/4 四态齐发)· enemy-ai · combat-weapons · camera-viewpoint / skeuomorphic-ui(形态 3 大面积)。
- **但集中度极高**:30 条命中里 **15 条在 telemetry-analytics 一个 epic**(10/10 story 全 Complete,而 51 程序集在 `unity/Assets/` 下**零文件** —— `find unity/Assets -ipath "*telemetry*" -not -path "*/Tests/*"` 空输出)。这是**整个 epic 的生产实现缺失**,不是零散假绿。
- **形态 3 的 skeuomorphic-ui 部分,定性被编排方实测推翻(本报告草稿的原判有误,留档订正)**:
  草稿原判「42 命名空间无 X 类型,当前阶段无法判定」属**诚实登记** ⇒ **错**。
  **实测(编排方,2026-10-18)**:`grep -rn "(?:public|internal).*(?:class|struct|enum|interface)\s+(\w+)" unity/Assets/Gameplay.UI/**/*.cs`
  ⇒ 名含 `Registry`/`Library`/`Component` 的类型**共 8 个在场**
  (`SkeuoComponentRegistry.cs:18` · `SkeuoElementLibrary.cs` · `ISkeuoComponentLibrary.cs` ·
  `ComponentRegistration.cs` · `FallbackFontRegistry.cs` · `MarkRegistry.cs` …),
  `ModalId.cs:9`(enum)· `FocusOrder.cs:18`(class)· `PaperCloseupScreen.cs:20`(class)**全部存在**。
  ⇒ 那些 `Assert.Inconclusive` 的**理由是假的**;真实病因 = **扫描面扫不到被测装配**:
  受影响 14 个文件里 **13 个没有任何 `using DaYiJingCheng.Gameplay.UI`**,
  而 `Gameplay.UI.asmdef:10` 是 `"autoReferenced": false` ⇒ EditMode 下 `AppDomain.CurrentDomain.GetAssemblies()`
  只返回**已加载**装配,无人引用其类型 ⇒ 装配不在 AppDomain ⇒ `SelectMany(a => a.GetTypes())` 扫不到
  ⇒ 误判「无类型」⇒ Inconclusive。
  **对照组(同目录同扫描法)**:`diagnosis/boundary_guard_test.cs` / `audio/assembly_boundary_test.cs`
  用 `FirstOrDefault(a => a.GetName().Name == "Gameplay.Presentation")` **按装配名直取** ⇒ 零跳过。
  **同目录内已有 5 个测试正常 using `Gameplay.UI`**(`dto_boundary_and_modal_test.cs` ·
  `focus_gate_and_bridge_test.cs` · `ComponentLibraryTest.cs` · `sign_channel_binder_test.cs` · `dentch_dose_form_test.cs`)
  ⇒ 类型在测试装配里**可见、可引用**,只是受影响文件自己没用。
  ⇒ **这是新形态(第五种):症状不是「绿」而是「跳过」** —— 比假绿更隐蔽(假绿会因证伪轮转红,
  「跳过」连红的机会都没有),且它**让本可转绿的判据以 ADVISORY 身份存活**:
  `9307ae5`(story-001)先建 Registry → `b581e12`(story-007)后写测试却说「未创建」,
  story-007 `Last Updated: 2026-09-29` 至今未刷新。
- **需要升级处理的四条(按优先级)**:
  ① **#1–#10 telemetry-analytics 整个 epic**(10/10 Complete vs 51 程序集零文件)—— 与前三个已处理 epic
     **不同型**:那些是零散 AC 层,这是**整个 epic 的生产实现缺失**;
  ② **#11–#13 三条 `Assert.Pass` 借绿**(enemy-ai ×2 + combat ×1)—— 卡面 AC 未勾却标 Complete;
  ③ **skeuomorphic-ui 的 52 处跳过(第五形态:扫不到被测装配)** —— 不是状态字段失真,是**判据被扫描面掐死**,
     且**可机械修复**(补 `using` / 改按装配名直取);
  ④ **#14 disease-simulation 的 `Signs` 恒空**(AC-20 / AC-13 子句零断言覆盖)。
- **反例(证明并非全坏)**:item-database / input-system / persistence-service 的测试有真实生产消费方与真实断言(`save_service_test.cs:43-55` 的校验和翻转测试可证伪)。⇒ **不能**得出「所有 Complete story 都假绿」。

---

## 四、未核查范围(硬约束:禁借绿)

**明确未做**:
1. **未跑 Unity 测试** —— 全部判据来自静态 grep,未执行 EditMode/PlayMode,未取得 pass/fail/skip 计数。「测试全绿」这一前提在本报告中**未被独立验证**。
2. **未逐 AC 覆盖以下 8 个 epic 的 B 类 grep**:player-controller(6 Complete)· skill-system(8)· item-database(12)· interaction-system(7)· random-events(6)· time-weather(5)· medical-consequences(3)· prescription-medication(5 中仅 2 篇)· emergency-procedures(7 中仅 3 篇)· patient-ai(3 中仅 2 篇)。
3. **A 类(自造 Fake 承担核心语义)只深挖了 telemetry**;`grep -rn "Fake\|Stub\|Mock" unity/Assets/Tests/` 未做全仓分类,无法排除其他 epic 有同型 Fake。
4. **audio-system / case-system 未纳入**(任务排除)。
5. **#16/#17(modular-007 / world-006)只数了卡面未勾 BLOCKING AC 条数,未逐条 grep 生产消费方** ⇒ 记「Medium(待深挖)」,**不判假绿**。
6. **`tests/unit/` 与 `tests/integration/` 账本树未扫**(仅扫 `unity/Assets/Tests/`)。
7. **回归数字未核** —— 未比对任何「N/N passed」自陈与实跑。

---

## 五、头条发现的判决性验证(telemetry-analytics)

一条命令同时坐实三件事:

```bash
grep -rln "ITelemetrySource\|ITelemetrySink\|JudgmentMetrics\|F1a\|Scorable" \
  unity/Assets/ --include=*.cs | grep -v "/Tests/"
# ⇒ 零输出

grep -c "Status**: Complete" production/epics/telemetry-analytics/story-*.md
# ⇒ 10 个文件各为 1(10/10 Complete)

grep -n "Status" production/epics/telemetry-analytics/EPIC.md
# ⇒ :6  > **Status**: **Complete ✅ 2026-09-28**(10/10 stories)
```

⇒ **51 遥测系统在 `unity/Assets/` 下零生产代码**,而 epic 与 10 篇 story 全标 Complete。
这与「已处理的三个 epic」(零散 AC 层造假)是**不同型**的问题:那是若干 AC 的语义被顶替,
这是**整个 epic 的生产实现缺失**。建议整体降级或补实现,不做零散修补。

---

## 六、建议的下一步(不属本报告裁决面)

1. **telemetry-analytics epic 单独立项**:10/10 Complete vs 51 程序集零文件 —— 这是**状态字段与实现事实的整体背离**,建议整体降级或补实现。形态 1/2/3/4 四态齐发,与「已处理的三个 epic」不同型(那三个是零散 AC 层,这是整个 epic)。
2. **三条 `Assert.Pass` 借绿**(enemy-ai story-004 / story-005 · combat story-006)按 `review-workflow.md` 走「补做一次评审(评当下)并落新原件」。
3. **skeuomorphic-ui / camera-viewpoint / emergency / interaction 的 `Assert.Ignore/Inconclusive`**:判据是**跳过**,应改卡面 AC 盒为未勾 + 状态行标注 NOT-RUN,而非 `Status: Complete`。
4. **disease-simulation story-004 的 `Signs` 恒空**:AC-20 的 `signs[]` 只读原始量子句与 AC-13「只改 Signs」当前**无任何断言覆盖**;建议要么补断言,要么在卡面把该子句降级登记。
