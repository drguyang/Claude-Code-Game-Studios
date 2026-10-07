# 评审报告原件 —— patient-ai(13)story-004(补做评审 · 评当下,不追认)

- **对象**: `production/epics/patient-ai/story-004-reconstruction-online-and-write-path-to-10.md`
  (2026-10-05 由 `0b6f948` 收口,当时做过双代理评审并修复,但**报告原件从未落库** ——
  按 `review-workflow.md` BLOCKING 义务,补做一次评审评**当下**代码,不追认原判定)
- **日期**: 2026-10-07 · **轮次**: 单轮双代理(承用户「评审只做一轮」)
- **评审席**: `lead-programmer`(结构侧代码面)+ `qa-lead`(测试面)—— 均只读、评当下 HEAD
- **两侧总裁定**: **CHANGES REQUIRED** → 修复 → 复跑绿(见 §四)→ 本件落库
- **判定 → 修复 → 验证** 三段式逐条登记;**不修项显式登记**(不静默)

---

## 一、结构侧(lead-programmer)—— 12 条

**原判定**:CHANGES REQUIRED。核心依据 = S1/S2/S4(故事核心改动 `ResetForLoad` 并非全重置、
「重新求值」承诺无实现路径、两 director 与 `PresentPatientsView` 的 Clear 口径互不一致)。

| # | 原判定(原文摘要) | 处置 | 修复落点 |
|---|---|---|---|
| S1 | **MAJOR** · `PatientSpatialDirector.cs:128-140` `ResetForLoad` 非真全字段重置:不清 `_states` 键集、`_pathOverride` 残留、`_ecozoneOfCalls` 不归零;与 `PresentPatientsView.ResetForLoad` 的 `Clear()` 口径不一致 | ✅ **已修** | `PatientSpatialDirector.ResetForLoad` 改 `Clear()` 三件(`_states.Clear()` / `_pathOverride.Clear()` / `_ecozoneOfCalls = 0`),XML doc 重写为 fresh construct 语义 |
| S2 | **MAJOR** · 注释宣称 HomeRegion/KnowsClinic「重新求值」,但唯一求值点是 `OnPresentEntered`;Reset 后不重新进场则永久停留 `None`/`false` | ✅ **已修**(注释面) | doc 改为「重新入表兑现求值」;求值点仍唯一在 `OnPresentEntered`;`EcozoneOfCallCount` 随重置归零、随再入表重计 |
| S3 | **MAJOR** · `Step` 的 `tick`/`playerCell` 死参数,`SpatialPerception.Band/ShouldDecideNow` 全库仅测试调用,LOD 整条链死代码 | ⚠️ **登记不修** | story-004 AC 未点名 LOD;死链事实登记于此,归 13 后续 story(结构债,不属本轮授权面) |
| S4 | **MAJOR** · `PatientBehaviorDirector.ResetForLoad` 遍历逐个重置但不清字典,与空间侧/`PresentPatientsView` 三处口径互不一致 | ✅ **已修** | `PatientBehaviorDirector.ResetForLoad() => _states.Clear()`;读档后由 `For(id)` 惰性重建;**逐对象 `PatientBehavior.ResetForLoad` 保留**(其 `Session → None` 契约仍由 behavior_map 反射写入点测试钉住 —— 该测试断言写入者恰 = {ApplyExamSession, ResetForLoad, set_Session},删除会破其恰=断言) |
| S5 | **MINOR** · 「`signs[]` 唯一消费点」双主张矛盾(`PatientBehaviorDirector.Material` vs `MaterialTable.Material`) | ✅ **已修**(注释面) | `PatientBehaviorDirector.Material` 注释改写:不再自称唯一消费点,标注呈现面真源 = `MaterialTable.Material`、本方法零生产调用方(半迁移结构债见 S7) |
| S6 | **MINOR** · `PresentationProjection.cs:142` 恒真死代码 `IsVisible(bool present) => present`,零调用方 | ✅ **已修** | 整体删除,原位留删除说明注释(判据本由 `Rebuild()` 在册循环承载) |
| S7 | **MINOR** · 平行 Material 类型 `PatientMaterial`/`PresentMaterial` 语义重叠(DRY) | ⚠️ **登记不修** | story-003 拆分遗留的类型合并需动 44 侧消费面契约,超本轮授权;登记结构债(与 S5 同根) |
| S8 | **MINOR** · 零写核验:**通过** —— 全目录 `IEventSink\|Append\|Next()` 生产零命中;`PatientCueEmit` 走 44 的 `IAudioCueSink` 属允许发射桥 | ✅ 通过项 | 无需修复 |
| S9 | **MINOR** · 第四来源核验基本通过(纯静态 BehaviorMap / FNV-1a 非 Random / 零 UnityEngine);未核 = AC-2 反射断言强度 | ✅ 由测试面 M3 补齐 | 见 §二 条 3(闭包 + 方法体 IL 四面扫描落地) |
| S10 | **MINOR** · 程序集边界通过(门 A 完好);未核 = 白名单断言是程序集级还是目录级 | ✅ 由测试面 B1 补齐 | 见 §二 条 1(源码文本面正测落地) |
| S11 | **NIT** · 「freshly constructed」措辞 + 硬编码 `WorldPos(0,0,0)` 而非锚点,测试与实现共同硬编码同值掩盖语义缺失 | ✅ **随 S1 消除** | `Clear()` 后不再有「归零到 (0,0,0)」路径;播种由重新入表的真实格承担 |
| S12 | **NIT(通过项)** · `PatientBehavior` 五字段重置完整零漏网 | ✅ 通过项 | 无需修复 |

---

## 二、测试面(qa-lead)—— 15 条

**原判定**:CHANGES REQUIRED —— **1 BLOCKING + 7 MAJOR + 4 MINOR + 3 NIT**。

| # | 原判定(原文摘要) | 处置 | 修复落点 |
|---|---|---|---|
| 1 | **BLOCKING** · 程序集白名单负夹具**整体恒真且断言写倒**:`if (text.Contains(...)) Assert.IsTrue(text.Contains(...))` 进 if 必真,兜底断言读测试文件自身 —— 生产真出现 `using Unity.Entities` 本测依旧绿(反空转规则②完全未兑现) | ✅ **已修** | 拆出共用机器 `ScanSourceTextsForTokens` + `LoadProductionPatientAiSources()`(GetFiles + 非空自卫);**正测** `test_ac13assembly_patientAiSourceFiles_zeroForbiddenRefs` 扫生产 PatientAI 源码面(已核零禁入 token);**负夹具**喂影子源文本 `using Unity.Entities;` 走同一台机器 ⇒ 必红 |
| 2 | **MAJOR** · IL 扫描 token `{"IEventSink.Append","IEventSink"}` 不命中**具体类型** `EventStream.Append`(callvirt token)—— M4 修复覆盖不到其自述失败形态;负夹具只测接口形态 | ✅ **已修** | T2 扫描 token 改 `{"Append"}`(具体类型命中);负夹具同 token,突变形态进面 |
| 3 | **MAJOR** · AC-13-A5 判别面只覆盖两 struct 公有实例字段 + 5 方法签名;方法体读取(`DateTime.Now`)与 static/私有字段盲区,8 条测试无一可红 | ✅ **已修** | T3 A5 补**两面四测**:`test_ac13a5_decisionClosure_noForbiddenSourceTypes`(闭包扫,含 static/私有)+ `..._catchesPrivateStaticField_negativeFixture` + `test_ac13a5_decisionMethodBodies_ilScan_noForbiddenCalls`(方法体 IL)+ `..._ilScan_catchesDateTimeNow_negativeFixture`;根 = 新增 `PatientAiScanSeeds()`(仅 PatientAI 命名空间,A5 决策面用);禁入 token 表已核生产零命中 |
| 4 | **MAJOR** · spasm 测试与 `zeroAppendInTypeGraph` 是同一台机器再跑一遍,`"Append"` 在闭包名字扫里近乎死码;无载荷不可达断言,契约测试全库零命中,AC3 「进流」半边无 NOT-RUN 登记 | ✅ **已修** | 改写为 `test_ac13c4_writePath_spasmAndComaTransport_13ReadsOnlyPresentation`:闭包扫三个处置载荷类型 `{"EmergencyAttemptPayload","EmergencyTreatmentAppliedPayload","DrugTreatmentAppliedPayload"}`(AC3「13 仅从结果表现」);新增负夹具 `..._spasm_catchesPayloadReference_negativeFixture`(ShadowWithTreatmentPayload)必红;**端到端契约测试 BLOCKED-BY-10 NOT-RUN 登记于 story 卡 AC3**(不冒充) |
| 5 | **MAJOR** · 「扫三流并集」退化为单扁平列表,case 流两行装饰性(删掉期望值仍 5);迁移腿未测 | ✅ **已修**(判别力) | T5 改**逐流增量断言**:病史 {0,1,2}→断 3;病例 {5}(独有 id)→断 6;世界 {None,7}→断 8 —— 每流删则红;注释登记:生产 `EventStream` 单 `_events` 列表「三流并集」退化事实、**TC-4 迁移腿归 7a**、None 行不可承重(m10) |
| 6 | **MAJOR** · AC1 文字「存档→读档→重放」,测试实为内存 `ResetForLoad` 重放,无序列化往返,偏差未登记 | ✅ **已修**(登记面) | story 卡 AC1 回填:「P0 测试面 = 内存重置代理存档腿;**二进制序列化腿归 7a**(ADR-010)」—— 代理关系显式登记 |
| 7 | **MAJOR** · V8 桩体 `Assert.IsNotNull(typeof(...))` 恒真(潜在借绿),45 实装摘 Ignore 即空转转绿 | ✅ **已修** | T7 桩体改 `Assert.Fail("AC-13-V8 判据未实现(BLOCKED-BY 45)…本 Fail 是防借绿闸门")` —— 摘 `[Ignore]` 即红,强制先实现判据 |
| 8 | **MAJOR** · [L] 走查件双错:档1「Idle ⇒ 无呼吸层」与生产相反(实为活着即 Begin);五档 ✅ 落点全错指 `test_ac13b3_reconstruction_*`(不含 cue/Breath/姿态断言);无签署行 | ✅ **已修** | 走查件整体重写:档1 判据订正为 Idle ⇒ `BreathAction.Begin`(引 `f1`/`f2`);五档落点全部改指真身测试(`presentation_projection_test` / `spatial_behavior_test`);补 §四签署行 + §〇修订记录;NR-S4-1/2/3 NOT-RUN 保留不记绿 |
| 9 | **MINOR** · 签名负夹具未与正测共用同一台机器,循环内 `if (sig.Contains(f)) Assert.IsTrue(...)` x==x 永真 | ✅ **已修** | T8 拆共用机器 `ScanSignatureForForbidden`;正测改收集 violations 断言空;负夹具走同机断言非空且**点名 Transform** |
| 10 | **MINOR** · `excludesPatientIdNone` 谓词本身不可证伪(哨兵 −1 ≤ max 初值 −1,删排除仍绿) | ✅ **已修**(登记面) | m10 加注:该谓词在本 API 下不可观测,本测只钉可观测契约(None-only ⇒ next=0),不声称守住排除逻辑 |
| 11 | **MINOR** · 字段白名单单向断言(只抓多余、不抓缺失),措辞与判据不符 | ✅ **已修** | T9 `ScanFieldsForToken` 改**双向**:白名单字段缺失也入 violations;已核 `BehaviorInput` 恰 3 字段、`MovingInputs` 恰 5 字段 ⇒ 正测仍绿 |
| 12 | **MINOR** · story 卡 Test Evidence `[ ] Not yet created` 未勾 + 点名假路径 `tests/integration/patient-ai/`(不存在) | ✅ **已修** | Test Evidence 回填:`[x]` + 真身路径 `unity/Assets/Tests/EditMode/PatientAI/…` + 复跑 XML + 原件指针(随本件) |
| 13 | **NIT** · 影子住测试程序集,`AreNotEqual("Gameplay.Presentation")` 结构上恒真 | ⚠️ **登记不修** | 该断言对生产零判别力是结构事实(影子不可能住在生产程序集);生产侧程序集判据由 B1 源码面正测承载 |
| 14 | **NIT** · `ScanTypeForForbiddenCalls` 朴素字节匹配有操作数假阳性/尾部漏检风险 | ⚠️ **登记不修** | 已证形态随 M2 扩到具体类型;深度 IL 解码器超本轮授权,登记为扫描机器结构债 |
| 15 | **NIT** · 种子非空守卫在兄弟文件,跨文件耦合 | ✅ **已修** | 新增 `test_scanMachines_seedsNonEmpty_selfGuard`(种子非空 + 含 `PatientBehavior`,本文件自持) |

---

## 三、AC ↔ 覆盖表(评审时点 → 修复后)

| AC | 评审时点判定 | 修复后 |
|---|---|---|
| ① 端到端重建 | **Partial**(内存代理未登记) | ✅ 代理关系登记于卡(存档腿归 7a) |
| ② 第四来源扫描 | **Partial**(方法体/静态私有盲区) | ✅ 闭包 + IL + 签名 + 源码文本四面,负夹具共用机器 |
| ③ 写路径归 10 | **Partial**(进流半边无登记) | ✅ 13 侧零 Append + 载荷不可达断言;端到端 BLOCKED-BY-10 登记 |
| ④ id 边界 | **Partial**(case 流装饰) | ✅ 逐流增量断言;迁移腿归 7a 登记 |
| ⑤ 联机 V8 | 登记属实但桩体恒真 | ✅ Assert.Fail 防借绿闸门(仍 NOT-RUN,如实) |
| ⑥ 跨平台 | 登记属实 | 不变(EXTERNAL) |
| ⑦ 程序集卫生 | **Covered but defective**(B1 恒真) | ✅ 源码文本面正测 + 影子源文本负夹具 |
| ⑧ [L] 走查 | **Partial**(双错 + 无签署) | ✅ 重写;13 侧结构前提闭,**整体签署仍待 42/44**(不代签) |

---

## 四、验证(可证伪)

**修复后复跑**(2026-10-07,batch 独占锁确认后执行):

```bash
# 1) PatientAI 过滤套件(基线 168/166/0红/2跳)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PatientAI" \
  --output unity/Logs/patientai-fix-2026-10-07.xml
# ⇒ 175 / 173 过 / 0 红 / 2 跳(+7 新测全过;2 跳 = CrossPlatform EXTERNAL + V8 BLOCKED-BY-45)

# 2) 全量 EditMode(基线 2948/2901/0红/1inconclusive/46跳)
unity test unity --mode EditMode --output unity/Logs/editmode-full-2026-10-07.xml
# ⇒ 2955 / 2908 过 / 0 红 / 1 inconclusive / 46 跳(+7 = 本轮新增,inconclusive/skipped 与基线持平)
```

**XML 解析口径**:按根节点 `<test-suite type="TestSuite" name="unity">`(或根 `test-run`)取 total/passed/failed —— per-fixture 节点会误导(已踩两次)。
> 全量 CLI 报 `Unity 进程以代码 2 退出`:溯源 = `SettingsExposureTest.test_monoOption…` 的 1 条 Inconclusive(既有,与本轮无关),基线同形。

**关键修复的可证伪判据**(⚠️ **2026-10-07 边界评估轮就地订正两处失实判据** —— qa-lead 席点名:
原 B1 判据「加真行 `using Unity.Entities;` ⇒ 断言红」会以 **CS0246 编译失败**收场(杀它的是编译器不是断言,
`manifest.json` 零 entities 包 + asmdef 引用集无该程序集);原 M2 判据指向的负夹具走**接口形态**,
旧 token 本就命中它 ⇒ 按原判据突变**不红**(判别力为零)。现文如下,**实跑结果见 §六**):
- B1:注释形态注入 —— 往 `PatientAI/` 任一生产文件加 `// using Unity.Entities;`(子串命中 `ScanSourceTextsForTokens`
  且可编译)⇒ `test_ac13assembly_patientAiSourceFiles_zeroForbiddenRefs` 必红;干净基线 = `grep -rn "Unity.Entities" unity/Assets/Gameplay.Presentation/PatientAI/` ⇒ 零命中。
- M2(订正版):**具体类型形态负夹具已补**(边界评估轮新增 `ShadowWithConcreteAppendCall` + `..._ilScan_catchesConcreteAppendCall_negativeFixture`);
  可证伪 = 该测试内 token 退回 `{"IEventSink.Append","IEventSink"}` ⇒ 本测必红;`{"Append"}` ⇒ 绿。
  (原接口形态夹具 `ShadowWithAppendCall` 对新旧 token 均命中,不构成 M2 守卫 —— 已如实登记。)
- M7:摘 `test_ac13v8…` 的 `[Ignore]` ⇒ 必红(Assert.Fail 闸门)。
- AC6(边界评估轮新增闸门):摘 `test_ac13crossPlatform…` 的 `[Ignore]` ⇒ 必红(原体纯注释零断言 = 与 V8 同型借绿,已补 `Assert.Fail`)。
- 条4:删 `ShadowWithTreatmentPayload.Payload` 字段 ⇒ `..._spasm_catchesPayloadReference_negativeFixture` 必红(IsNotEmpty 失败)。
- m11(改判可达版):正测白名单数组追加不存在的字段名 `"Phase"` ⇒ `..._behaviorInputFields_areExactlyWhitelist` 必红(双向「缺失」分支活性;原案「改回单向 ⇒ 正测红」在现夹具下不可达,已由 qa-lead 席替换)。
- n13(降级覆盖):正测期望值 `"Gameplay.Presentation"` 改 `"Sim"` ⇒ `..._patientAiIsInBoundaryLayer_notInSim` 红(n13 影子恒真的结构事实由正测活性代证)。
- S1/S4:`director.ResetForLoad()` 后 `StateOf(id)` ⇒ null、`EcozoneOfCallCount` ⇒ 0(spatial B4 测试钉住)。

---

## 五、残余与边界(不静默)

- **登记不修(边界评估轮复核后)**:S3(LOD 死链 —— 精确化见 §六)· n13(影子程序集恒真的结构事实,正测活性由突变代证)·
  n14(朴素 IL 匹配,已扩形态,失活性由 IL 负夹具 + 突变看住)· TC-4 迁移腿(归 7a)·
  AC3 端到端契约(BLOCKED-BY 10)· AC5 联机(BLOCKED-BY 45,闸门已在,接缝 = YAGNI)·
  AC6 跨平台(EXTERNAL —— 拆腿登记,夹具腿归 ADR-012 CI 轮)· [L] 整体签署(待 NR-S4-1/2/3,即 42/44)。
- ~~S7(平行 Material 类型,需 44 消费面契约)~~ → **已于边界评估轮删除**(判定与落点见 §六)。
- **本件即** `review-workflow.md` 要求的补做评审原件;**评的是 2026-10-07 修复轮落笔后的当下代码**(测试面/结构面两份原始判定全文见 §一/§二 摘要表,原报告经单轮双代理产出、未改任何文件)。
- 复跑绿后 story-004 转 **Complete**;三层账目 caveat 同批摘除。

---

## 六、边界评估轮(2026-10-07 · 双子代理 · 用户指令「三个如实边界调用子代理评估修复」)

对收口时如实登记的三个边界,双席只读评估(lead-programmer 结构面 + qa-lead 测试面),判定与处置:

### 6.1 边界 1(三条未勾 AC)

- **三本体均合规、不借绿**(⑤⑥⑧);可推进半边只在**措辞/闸门级**,已全部落笔:
  Completion Notes 空壳已填 · AC5 转勾判据上卡 · AC6 拆腿 + CI 现状指针(`unity-tests.yml:91-98` = TODO 空桩)·
  Test Evidence「must exist」与不存在产物的矛盾拆腿 · Note 3「可静态验证」未兑现主张订正 ·
  走查件签署行改机器可检 `[ ] Approved` 形态。
- **AC6 由「空体 [Ignore]」补 `Assert.Fail` 防借绿闸门**(与 M7 同型的借绿,首轮漏项)。
- AC5 维持(接缝 = YAGNI)· AC8 纯外部签署。

### 6.2 边界 2(突变验证)

清单 6+1 条(批次A 6 合 1 跑 + 批次B 具体类型夹具 1 条),含两处原案替换:
- B1 改**注释形态**注入(真行 = 编译炸,非断言红);
- m11 改**「白名单追加不存在字段名」**可达版(原「改回单向 ⇒ 正测红」在现夹具下不可达);
- **M2 前置修复已落**:补 `ShadowWithConcreteAppendCall` + `..._ilScan_catchesConcreteAppendCall_negativeFixture`
  (原接口夹具对新旧 token 均命中 = M2 回归无守卫,lead 席判定「突变存活」)。
**实跑结果见 §七**(执行后回填)。

### 6.3 边界 3(登记不修六项复核)

| 项 | 判定 | 处置 |
|---|---|---|
| S3 LOD 死链 | **维持登记,但原登记三处失实**:① 落点「归 13 后续 story」悬空(EPIC 4/4 无 005);② 范围低估 —— `SpatialPerception` 全类(`SqrDistance`/`Perceives`/`Band`/`DecisionInterval`/`ShouldDecideNow`)+ `_bands` 均测试独占,根因是 **13 零生产构造点(尚无 tick driver)**,本轮无处可接;③ 接线还牵「节流落决策还是落积分」未裁设计问(ADR-016 §九) | 精确化登记:残债 = F-13.3/F-13.4 整链零生产接线,落点**待裁**(新 story-005 或挂表现层 tick 接线 epic),GDD 背离显式登记;(a) 接线风险清单已备(四类单 tick 测试节流下必红等),归后续 story 前置 |
| S7 平行 Material | **可修(删除)** —— 原判理由「需 44 消费面契约」证伪:`PatientMaterial` + `PatientBehaviorDirector.Material` **零生产调用方**(story-003 孤儿半迁移件),测试仅 1 处 | ✅ **已删**:`PatientBehavior.cs` 方法 + struct · `behavior_map_test` 对应测试;删前核两处 `GetMethods` 扫描(返回 VitalsDto 面 / 禁词面)均不受影响;残债收窄为 `PresentMaterial` ↔ `MaterialTable` 命名统一(归 44) |
| n13 影子恒真 | 真不可修(负夹具形态物理上不可能)+ 可降级突变 | 突变 #6(改正测期望值 ⇒ 红)代证正测活性 |
| n14 朴素 IL 匹配 | 真不可修(本轮授权) | 失活面由两个 IL 负夹具看住,负夹具活性由突变 #4 证 |
| TC-4 迁移腿 | 真不可修(载体 = 7a 二进制往返) | 维持登记,双侧口径一致 |
| AC3 端到端 | 真不可修(10 的 Kind 未进 registry) | 维持登记;13 侧可执行半边 = 载荷闭包不可达(突变 #3 看住) |
