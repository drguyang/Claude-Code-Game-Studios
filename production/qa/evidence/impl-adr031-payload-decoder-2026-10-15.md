# 实现轮证据原件 —— ADR-031 载荷解码器(`IPayloadDecoder`)

- **对象**:`docs/architecture/adr-031-payload-decoder-abstraction.md`(Accepted 2026-10-15)
- **日期**:2026-10-15 · **Sprint**:12 · **执行者**:Claude
- **性质**:本件 = **实现轮落盘证据**(契约支 + 接线支 + S12-4 证伪轮),**非评审报告**
- **基线三元组**:截至时刻 **2026-10-15 19:14(+0800)** · 批次 `s12-11-impl-final` · commit **`a5ef7c5`**
- **判定**:ADR-031 §Validation **V-1…V-10 全部勾选**;S12-4 证伪轮 **3 探针全红**(有鉴别力)

---

## 一、结论

| 项 | 结论 | 证据 |
|---|---|---|
| 契约支 | ✅ `IPayloadDecoder`(Sim.Contracts)+ `PayloadDecoder`(Sim.Codec)落盘 | §二 |
| 接线支(b5 门) | ✅ 8 → 10 项,含 `IPayloadEncoder`(ADR-029 漏项)+ `IPayloadDecoder` | §三 |
| 接线支(V-8 逐处登记) | ✅ **实测 11 处**(ADR 原稿只写三处),逐处裁定已落 ADR §Validation V-8 | §四 |
| 计数订正(V-10) | ✅ A 组 25 处改 · B 组 3 改 + 4 注 · C 组历史件 6 注 + 现行件 9 注 | §五 |
| S12-4 证伪轮 | ✅ **3/3 探针成功转红**,还原字节一致 | §六 |
| 回归 | ✅ `3421 total / 3375 passed / 0 failed / 46 skipped`(基线 3401/3355 ⇒ **+20/+20**) | §七 |

**⚠️ 本件不追认任何既往判定** —— 评的是**当下**代码(承 `review-workflow.md` §义务 3)。

---

## 二、契约支 —— 两文件落盘

| 文件 | 内容 |
|---|---|
| `unity/Assets/Sim.Contracts/IPayloadDecoder.cs` | 接口:第八抽象点,住 Contracts,`using` 仅 `System` |
| `unity/Assets/Sim.Codec/PayloadDecoder.cs` | 实现:池寻址 + 委托既有 `PayloadCodec.Decode<T>` |

**签名(实测)**:
```csharp
// Encoder(既有,ADR-029)
PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct;
// Decoder(本件,ADR-031 §②)
T Decode<T>(EventKind kind, in PayloadRef payloadRef) where T : struct;
```

**结构结论(§③ 的收益)**:
- `IBlobPool` **一字不改、不搬家**;池依赖在**实现的构造函数**(逐字承 ADR-029 §② 模式)
- `Sim.Contracts.asmdef` `references` 仍 `[]`;`Sim.asmdef` `references` 仍 `["Sim.Contracts"]`
- ⇒ **零新增程序集边**,门 A / b2 / ADR-025 §① 三重约束全保留

---

## 三、接线支 —— b5 门

`unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` 的 `AudioForbiddenContractTypes`:

```
改前(8 项): IEventSink, IEventAuthority, ITickProvider, IIdAuthority,
            IVitalsQuery, ITeleportCommandSink, SimEvent, VitalsDto
改后(10 项): … + IPayloadEncoder(← ADR-029 的漏项) + IPayloadDecoder(← ADR-031 §①)
```
错误串(第 729 行)`六抽象点` → `八抽象点`。

**⚠️ 归属分开记账**:补 `IPayloadEncoder` 属 **ADR-029 面**(它 2026-10-02 就该补;
实测补前 44 可合法引用编码器),补 `IPayloadDecoder` 属 **ADR-031**。二者同批落盘但**出处不同**。

---

## 四、接线支 —— V-8 逐处登记(ADR 原稿「三处」实为「11 处」)

**实测命令**:
```bash
$ grep -rln "够不着" unity/Assets/Sim/ | wc -l
10
$ grep -rln "够不着\|解码归\|零载荷解码\|PayloadCodec\|diseaseExtractor" unity/Assets/Sim/ | wc -l
13   # 含 StructureKinds.cs / EmergencyAttemptAggregator.cs 两处仅注释引用 ⇒ 真绕行 11 处
```

| 判定 | 处数 | 位置 |
|---|---|---|
| **退**(应改用 `Decode`,归各系统实现轮) | **6** | CaseCloseDecider · ConsequenceInputAggregator · PatternDetector · CaseOpenDecider · PatternRecognitionWriter · ResourceHarvestWriter |
| **暂不退**(正当边界分工) | **5** | CaseOpenWriter · CaseCloseWriter · PoiStateMachine · DiseaseCourse · DiseaseCourseBook |

逐处理由见 ADR-031 §Validation V-8 表。**⚠️ 无一处在本轮接线** —— 本件只保证退路存在。

---

## 五、计数订正(V-10)

**全库实测**(排除 `unity/Library`、`.git`、`unity/Logs`、审计日志 `session-logs`):
```
$ grep -rnE "(五个|六个|七个|八个|…)[^。]{0,14}抽象点" --include=*.md --include=*.cs --include=*.yaml . | wc -l
110   # 含 adr-029/adr-031 自身的历史叙述与 session-state
```

| 组 | 处置 | 处数 |
|---|---|---|
| **A 组**(现行态断言,「七个」→「八个」) | **改** | 25 |
| **B 组**(更老残留,现行规范性文档) | **改 3 + 加注 4** | 7 |
| **C 组**(历史陈述 / 评审记录 / 报告) | **加注不改写** | 历史件 6 + 现行件 9 |
| **序数陈述**(「第六个」「第七个」)| **不动**(至今准确) | 余 |

**⚠️ 原清单两处错**(「本清单不声称已复核」之兑现):
1. `AssemblyGates.cs:720` 被记为「错误串」—— 实为**普通 doc 注释**;真错误串在 `:729`(已改)。
2. 清单**漏列** `production/session-logs/session-log.md`(自动审计日志,数百次重复 ADR-005 文本)
   —— **不属订正面**,已排除。

**A 组关键落点**:`adr-005:25/478/489` · `control-manifest:106` · `architecture.md:258/439/561/816/1222` ·
`adr-020:39` · `adr-015:341` · `adr-023:78` · `adr-017:269` · `adr-001:448/452` ·
`adr-025:63/112` · `player-controller-and-movement.md:125/386/1087/1692` · `enemy-ai.md:976` ·
`u0-assembly-checklist.md:320` · `DiagnosisGrowthExit.cs:7` · `motor_lease_test.cs:252`。

---

## 六、S12-4 证伪轮 —— 3 探针(改 → 跑 → 还原)

**纪律**:编辑与 Unity 运行**必须在同一次 Bash 调用内**(`trap ... EXIT` 才守得住)。
⚠️ **首次探针 1 曾假绿** —— 编辑与跑 Unity 分属两次调用,`trap` 在编辑那次退出时即还原,
Unity 跑的是原文件。**此为探针执行缺陷,非测试缺陷**;重做后正确转红(见下)。

| 探针 | 形态 | 编辑 | Unity 判定 | 还原 |
|---|---|---|---|---|
| **1**(重做) | 定义域收窄:从 b5 删 `IPayloadDecoder` | `MATCH_COUNT=1` | **`EXIT=2` / `failed=1`**,正是 `test_adr031_v4_b5GateForbidsEncoderAndDecoder` | `RESTORE=OK` |
| **2** | 共存互斥:接口加 `TryGetFreehandText` | 方法数 1→2 | **`EXIT=2` / `failed=1`**,正是 `test_adr031_v7_noFreehandPath_interfaceHasExactlyOneMethod` | `RESTORE=OK` |
| **3** | 抛形退化:`throw` → 空 span | throw 3→0 | **`EXIT=2` / `failed=3`**,正是三条 `…_throwsInvalidOperation` | `RESTORE=OK` |

**日志**:`unity/Logs/probe1b.{xml,log}` · `probe2.*` · `probe3.*`。
**重编确证**:`Editor.Tools.Gates.dll` mtime 由 `19:01:01` → `19:09:59`(探针 1 重做时)。

**终检(字节一致)**:
```
gates = 2afec3a4d899811d4204b5ff12d9bbad38b96be866fca3e1b682ae46a37d76a6
dec   = efa786e93a1de2ee00889e00a7b6d1a0055eca38fea0ce8b8bec7045639fb400
idec  = 817ca5011474d2be0df5d0e21968c81ce99bdc1293c6b8c7cb228e900b8bbdcd
残留探针痕迹: TryGetFreehandText=0 · ReadOnlySpan<byte>.Empty=0
```

---

## 七、回归基线

```
$ Unity -batchmode -runTests -testPlatform EditMode … -testResults s12-11-impl-final.xml
EXIT=0
total=3421  passed=3375  failed=0  skipped=46
HEAD=a5ef7c5
```
**对照**:实现前基线 `3401/3355`(`s12-6-final.xml`,18:05)⇒ **+20 / +20**(新增 20 条 `payload_decoder_test`),**零回归**。

**计数订正后复跑**(`s12-12-final.xml`,V-10 改毕 + 两处 `.cs` 注释改动后):
```
EXIT=0   total=3421  passed=3375  failed=0  skipped=46
```
⇒ V-10 的改动(`DiagnosisGrowthExit.cs` / `motor_lease_test.cs` 两处**注释**)未破坏任何测试。

---

## 八、未结(不在本件裁决面)

- **接线支的「退」执行** —— 5 处归各归属系统实现轮(37 / 17 / 53)
- **S12-9**(53 story-002 Judge 实现)—— `blocked`,本件已解其结构前置
- **ADR-010 §三 义务 16**(读侧解码实现)—— 本件只登记落点,不落表
- **V-1…V-10 已勾,但证伪轮只覆盖 3 条判据的鉴别力** —— 其余(V-2/3/5/6/9)为**静态实测**,非「回退即转红」类

---

## 九、接线轮:V-8 六处「退」的执行(2026-10-15 · 同日第二支)

**用户裁定**(本日,口头指令):「接接线支,把 6 处「退」做掉」;
对 ② 的归属(原表裁「归 S12-9 重开」)进一步裁定:「**6 处全做,含改 Inputs53**」。

### 产品码(7 文件)

| # | 文件 | 改动 |
|---|------|------|
| ① | `Sim/CaseCloseDecider.cs` | `IsClosed` / `IsOpen` 加 `IPayloadDecoder`;按 `CaseClosedPayload.CaseId` 精确匹配(流序关联退役) |
| ② | `Sim/Consequences/ConsequenceInputAggregator.cs` | `Aggregate` 加 `decoder` 参;`Inputs53` 的 P/C 两路改**解码行**(`JudgmentRow53` / `CareRow53` —— 字段名与数量不变,只换元素类型;三种异构处置载荷在 `CareRowOf` 归一:`ActionId` = ActionId\|TreatmentId,`Polarity` = 极性,CareApplied 恒 0;`Fix DrugPotency` 刻意不承载) |
| ③ | `Sim/PatternDetector.cs` | `Evaluate` ×2 / `NextEmissions` / `IsFired` 加 `decoder`;`DiseaseOrdinalsOf` 解码 `DiseaseSet.Bits` 位展开(委托面消失;`SortBySaltedKey(List,Func)` 公开 API 保留 —— tie-break 判别测试依赖) |
| ④ | `Sim/CaseOpenDecider.cs` | `LastJudgmentOf` 加 `decoder`;逐条解码 `JudgmentRecorded/RevisedPayload.CaseId` 精确匹配 |
| ⑥ | `Sim/CaseOpenDecider.cs`(同文件) | (含于 ④) |
| ⑦ | `Sim/PatternRecognitionWriter.cs` | ctor 10 参 → 8 参(双委托透传 → 单 `decoder`) |
| ⑨ | `Sim/World/ResourceHarvestWriter.cs` | ctor 4 参;`Record` 去 `gatherSeq` 参、返回 int;`CountHarvestsOfNode` 自解码数流 |

### 连带(装配 + 生产调用点,6 文件)

- `Gameplay.Boot/CompositionRoot.cs` / `CompositionRootServices.cs`:decoder 构造(共用同一池)+ `Decoder` 属性 + fail-loud;`CasebookVitalsSource` 构造传 decoder
- `Sim/CaseCloseWriter.cs`:ctor 加 decoder(内部 `IsClosed` / `IsOpen` 调用)
- `Gameplay.Boot/CasebookVitalsSource.cs`:`InkAgeTicks` → `LastJudgmentOf(..., _decoder)`

### 测试夹具(8 文件)

- `case_close_precondition_test.cs`:12 处 CaseClosed 全改真载荷(`MakeClosed`,CaseId 三元组与归属语义一致);41 处 `IsClosed`/`IsOpen` 加 `Decoder`
- `case_open_and_judgment_test.cs`:10 处 Judgment 事件真载荷(**具名重载** `EncodeJudgmentRecorded/Revised` —— 通用 `Encode<T>` 对 Judgment 拒收,freehandText 变长段);8 处 `LastJudgmentOf` 加 decoder;3 处路由用例保留 header-only(不解码)
- `case_pattern_detection_test.cs`:104 处 CaseClosed 改 `MakeClosed(tick, patient, seq, params int[] ordinals)`(判别力迁移:原 8 个提取器的「输入序 ≠ 期望序」设计全部迁入 ordinal 实参);8 个静态/局部提取器删除;`Emit` 委托面退役;ac3711 接口面允许形参集 `diseaseExtractor` → `decoder`;ac3713 判别面注记订正(位掩码无序,集合迭代序判别随委托消失)
- `resource_harvest_writer_test.cs`:Record 新签名;gather_seq 递增断言改为**返回值断言**(写者自算行为);gather_seq 非法入参用例删除(已非入参);null 依赖测试扩 4 依赖
- `pattern_recognition_writer_test.cs` / `case_close_writer_test.cs`:ctor 形态(此前支已改,本轮回归确认)
- `medcons_aggregation_test.cs`:② 的新签名 + P/C 真载荷(`EvJ`/`EvC`/`EvE`/`EvD` 四 helper);指纹升级为**内容级**(载荷置换也红);P 路 Kind 断言改内容级(`LexiconId`)

### 门保持

- **门 A / b2**:`Sim` asmdef 引用集仍恰 = `["Sim.Contracts"]`,`noEngineReferences: true` 不变
- **b6**:`Sim/` 内无任何真实 `new PayloadRef(`(全库 grep:仅注释)
- **AC-53-05**:五路闭集反射断言**原样绿**(字段名与数量未动)

### 回归

```
2026-10-15 20:39  s12-wiring-final.xml  EXIT=0
total=3421  passed=3375  failed=0  skipped=46
```
**与基线(`3421/3375/0/46`,`s12-12-final.xml`,commit a5ef7c5)逐位一致 —— 零回归。**

**迭代记录**(如实):第一跑 `3360/15`(15 败同一根因 —— Judgment 两 Kind 须走具名重载,
测试误用通用 `Encode<T>`;产品码零改,夹具改具名重载后全绿)。此前一次编译红
(`ResourceHarvestWriter` 缺 `using System.Collections.Generic`),单行补齐。

### V-8 表终态

| # | 状态 |
|---|------|
| ①④⑥⑦⑨ | **已退(执行完毕)** |
| ② | **已退(执行完毕;Inputs53 形状经用户裁定「含改 Inputs53」已改)** |
| ③ | **已退(执行完毕)** |
| ⑤⑧⑩⑪ | 暂不退(正当边界,不变) |

### 未结(承接线轮)

- **S12-9**(53 story-002 Judge 实现)—— ② 的 P/C 解码行已就位,`Judge` 可直接消费 `JudgmentRow53.LexiconId/Confidence` 与 `CareRow53.Kind/ActionId/Polarity`;**重开与否仍待用户裁**
- **53 的 GDD 轮张力**(处置 Kind 集闭集 / 窗口下界)—— 不因本批消解,归 53 GDD 轮
