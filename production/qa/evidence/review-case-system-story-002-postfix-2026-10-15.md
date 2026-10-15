# 评审原件(修复后复评)—— case-system story-002(立案两路径·开案唯一·判断记录事件化)

**评审时点**:2026-10-15(评**当下**代码 —— 承 `.claude/docs/review-workflow.md` §义务 3「评审不可事后追补」)
**被评对象**:`unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs`(声明件)
+ `unity/Assets/Sim/CaseOpenDecider.cs`(`CaseStreamQuery.LastJudgmentOf`,生产代码)
+ `story-002` §Test Evidence 的 **AC → 载体映射表**
**本件性质**:对「补评判定缺陷**刚被修复**」后的**新一轮评审**(非追认)
**原判定来源**:`production/qa/evidence/review-case-system-story-002-retro-2026-10-15.md`(含 §七 勘误)
**修复证据来源**:`fix-s11-7-...-blk8-...md`(BLK-8)· `fix-s11-14-...-assertions-...md`(六条断言形态)
**运行基线(只读借用 · 来源 = 主轮)**:`unity/Logs/s11-final-EditMode.xml`(mtime 2026-10-15 12:54 本地)
· `unity/Logs/s11-final-PlayMode.xml`(12:55)。**本件未起 Unity batch**(硬约束)。

---

## 一、原判定(逐条 · 引补评件 §七 勘误后口径)

补评件原判「代码面 4 BLOCKING + 测试面 10 BLOCKING」,经 §七 勘误收窄为:

| ID | 原判定 | 类别 |
|---|---|---|
| **BLK-8** | `CaseStreamQuery.LastJudgmentOf` 的 `caseId` 形参**零引用**;`CompositionRoot.cs:245` 传全局混合流 ⇒ `CasebookVitalsSource.InkAgeTicks` 取到别的病人的判断事件(真实现缺陷 + 生产实害链) | 代码面 · BLOCKING |
| **BLK-1**(恒真半边) | `decide(A) == decide(B)` 因 `Decide` 不读 `sourceKind` 而**构造上恒真** | 断言形态 · BLOCKING |
| **BLK-2** | 同 tick `caseIdA != caseIdB` 由**测试字面量**构造 ⇒ 恒真 | 断言形态 · BLOCKING |
| **BLK-3** | 两处 `.Where().OrderBy().Last()` **自造 `J(c)`**,不调生产 `LastJudgmentOf` | 断言形态 · BLOCKING |
| **BLK-5** | 与 `:158` 重复断言;S-8.3「空判断 = `lexicon_id 0` ∧ `freehand_text ''`」**一字未验** | 断言形态 · BLOCKING |
| **BLK-9** | 药材短缺负断言只喂布尔,**无点名 fixture、无流断言** | 断言形态 · BLOCKING |
| **BLK-10** | 证据面错配 —— AC-37-05 / 23 是 `[I]` 集成,却指 EditMode 单测 | 登记面 |

**已推翻面(§七·三,不在本轮修复范围)**:AC-37-30 入流义务 / AC-37-05 判断半边 / AC-37-05 处置半边 /
AC-37-23 均有**真载体**,只是不在声明件里 ⇒ 真问题是**声明证据与 AC 错配**。
**ADVISORY(§六·三,未列入修复面)**:ADV-1(freehand_text 引用扫描为字段缺席代理)· ADV-2(`Decide` 无生产调用者)·
ADV-4(重复断言)· ADV-5(重排恒等)。

**原门判定**:`Test Evidence 门不通过`(性质 = 声明证据与 AC 错配 + 一条真实现缺陷)。

---

## 二、修复落点(逐条对)

| ID | 落点文件 | 具体改动 |
|---|---|---|
| **BLK-8** | `unity/Assets/Sim/CaseOpenDecider.cs` `:132-165` | `LastJudgmentOf` 重写为**流序关联**:按 `e.Patient.Value == caseId.Patient` 过滤 → 遇 `CaseOpened` 置 `inCase = (e.Tick==caseId.Tick && e.Seq==caseId.Seq)` → 遇 `CaseClosed` 置 `inCase=false` → 仅 `inCase` 时收 `JudgmentRecorded/Revised`。**与 `OpenCaseOf` 同范式**。调用点(`CompositionRoot.cs:245-246`)**一字未改**(传整条流成为正确用法)。 |
| **BLK-8 测试面** | 同声明件 `:595-689` | 三条旧夹具(`test_lastJudgmentOf_{noJudgment,hasJudgment,singleJudgment}_*`)**补入 `CaseOpened`**(旧夹具不含 = 「调用方已切片」形状,正是死参逃过测试面的原因)+ **新增两条跨案判别夹具**:`test_lastJudgmentOf_crossPatientInterleaved_ignoresOtherPatientsJudgments`(`:646`)、`test_lastJudgmentOf_samePatientSecondCase_ignoresFirstCaseJudgments`(`:670`)。 |
| **BLK-1** | 同声明件 `:88-100` | **删除** `decisionA == decide(B)` 断言(被邻两条逻辑蕴含、零独立判别力);docstring 写明删因 + 真载体(`:78-82`)。 |
| **BLK-2** | 同声明件 `:110-130` | 期望值**由流派生**:造两条同 tick `CaseOpened` → 经生产 `CaseStreamQuery.OpenCaseOf(stream, 1/2)` 取 id。 |
| **BLK-3** | 同声明件 `:280`(ac3705 存新值)与 `:481`(fullFlow) | 两处均改调**生产** `CaseStreamQuery.LastJudgmentOf(events, caseId.Value)`;`caseId` 由 `OpenCaseOf` 派生。 |
| **BLK-5** | 同声明件 `:309-350` | 去重(保留覆盖 Recorded+Revised 双支的一条);本测改**真装配 + 真 `JudgmentWriter`**,验「擦除 = 写一条空判断」(`lexiconId: 0` → `Recorded` + 流 `before+1` + 载荷回读 `LexiconId==0`)。 |
| **BLK-9** | 同声明件 `:185`(fixture)+ `:198-221` | 点名 fixture `MedicineShortageFixture = PatientId.None`;真装配(`CompositionRoot.Assemble()`)+ 真写者;断言 `result == NoPatient` + **流总事件数不变** + `CountCaseOpened == caseBefore`。 |
| **BLK-10** | `story-002` §Test Evidence `:176-190` | 新增 **AC → 真实载体映射表(11 行)** + 两条「部分闭合」如实登记(`:192-205`)。 |

**改动量核对**:`git diff --stat` = **207 insertions / 59 deletions**;`[Test]` 计数 **25**(与自述一致)。

---

## 三、验证命令(可复跑 · 可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 断言形态:自造 J(c) 是否清零(期望:仅注释命中,无活代码)
grep -n "OrderBy(EventOrder.KeyOf).Last()" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# ② 是否改调生产函数(期望:LastJudgmentOf 调用 ≥2 —— 实测 11 含注释)
grep -cn "CaseStreamQuery.LastJudgmentOf" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# ③ BLK-1 恒真半边是否已删(期望:仅注释命中)
grep -n "decisionA, Is.EqualTo(decisionB)" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# ④ BLK-9 点名 fixture + 真装配 + 零流断言
grep -n "MedicineShortageFixture\|CompositionRoot.Assemble()\|CountCaseOpened" unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs
# ⑤ BLK-10 映射表点名的载体是否真的存在(逐个)
grep -rn "test_caseOpened_realBag_appendsExactlyOneCaseEvent_roundTrip" unity/Assets/Tests/
grep -rn "test_caseOpenedDriver_opensCaseAtBoot"                unity/Assets/Tests/
grep -rn "test_record_onOpenCase_writesJudgment_caseStream"     unity/Assets/Tests/
grep -rn "test_thirdSubmit_erasesViaEmptyJudgment"              unity/Assets/Tests/
grep -rn "test_inkAge_crossPatient_usesOwnJudgmentNotOtherPatients" unity/Assets/Tests/
grep -rn "test_case_noFold_historicalRowsComplete"              unity/Assets/Tests/
grep -rn "test_ac3721_checkedButNoTreatmentInWindow_rejects"    unity/Assets/Tests/
grep -rn "test_e2e_fullLoop_sevenStages"                        unity/Assets/Tests/
# ⑥ 借绿复核:主轮最终全量日志中本 fixture 的实际结果(只读借用)
grep -o 'name="CaseOpenAndJudgmentTest"[^>]*total="25"[^>]*' unity/Logs/s11-final-EditMode.xml
# ⑦ 时间序复核:日志 run 是否晚于被评文件最后一次改动(防「跑的是旧码」)
stat -c '%y %n' unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs \
                unity/Logs/s11-final-EditMode.xml
```

### 3.1 本件**实跑**输出(命令 ①–⑦)

```
① 命中 2 行 —— 全在 docstring 注释(:249 / :445),无活代码断言  ⇒ 自造 J(c) 已清零
② 11 行(含注释引用);活调用 ≥2(:280 与 :481)                ⇒ 已改调生产函数
③ 命中 1 行 —— 仅 docstring(:79,写明「已删」)                  ⇒ 恒真半边已删
④ MedicineShortageFixture(:185) · CompositionRoot.Assemble()(:202/:313) · CountCaseOpened(:61/:205/:219)  ⇒ 全中
⑤ 8 个点名判据**全部命中**(见 §四·载体抽查)
⑥ <test-suite ... name="CaseOpenAndJudgmentTest" ... testcasecount="25" result="Passed" total="25" passed="25" failed="0">
⑦ 被评文件 mtime = 2026-10-15 11:32:17 本地 <  日志 mtime = 2026-10-15 12:54:28 本地
   ⇒ **日志晚于文件改动**,跑的就是当下工作树(该文件为 uncommitted `M`)
```

### 3.2 主轮全量(⚠️ **来源 = 主轮**,本件未起 batch)

| 套件 | 结果 | 日志 |
|---|---|---|
| EditMode | `total=3387 passed=3341 failed=0 inconclusive=0 skipped=46` | `s11-final-EditMode.xml`(start 2026-10-15 04:53:39Z) |
| PlayMode | `total=186 passed=186 failed=0` | `s11-final-PlayMode.xml` |
| 本 fixture | `CaseOpenAndJudgmentTest` 25/25 passed | 同上 |
| 相关载体 | `InkAgeTest` 5/5 · `JudgmentWriterTest` 4/4 · `CaseOpenWriterTest` 7/7 passed | 同上 |

---

## 四、独立复核结论(逐条)

| ID | 现状态 | 依据(本件亲读代码) |
|---|---|---|
| **BLK-8** | **CLOSED** | `LastJudgmentOf` 现按 `caseId` 的流序关联归属(`CaseOpenDecider.cs:132-165`),`caseId` **不再是死参**。判别夹具二条:跨病人(`:646`,病人 2 判断全流更晚但不得计入病人 1)+ 同病人复诊(`:670`,两案 `Patient` 相同,仅 `CaseOpened` 三元组不同)。端到端实害链夹具 `Boot/ink_age_test.cs:97` 走**全生产装配**(`CompositionRoot`)。**关键**:若把 `inCase` 归属跟踪整段删回旧实现,三条夹具转红(修复件 §四 有反证记录)。 |
| **BLK-1** | **CLOSED** | 恒真断言已**删除**(非重包装)。判别力论证成立:剩两条断言(`decisionA/B == OpenNew`)对任何 `sourceKind` 分支变异都会转红 —— 被删断言由它们**逻辑蕴含**。 |
| **BLK-2** | **CLOSED** | 期望值改由生产 `OpenCaseOf` 从流派生。判别力成立:若 `OpenCaseOf` 丢 `Patient` 维度、或 `CaseId.Equals` 忽略 `Patient`(`PayloadCommon.cs:67` 现含 `Patient`),`Is.Not.EqualTo` 转红。 |
| **BLK-3** | **CLOSED** | 两处活代码均改调生产 `LastJudgmentOf`(`:280`/`:481`),无 `.Where().OrderBy().Last()` 活断言。判别力:改「首条胜」/恒 `null` ⇒ 红。 |
| **BLK-5** | **PARTIALLY-CLOSED**(登记诚实) | 去重 ✅;S-8.3 改真装配实证 ✅(`lexiconId: 0` → `Recorded` + 流 +1 + 载荷 `LexiconId==0`,判别力真实)。**`freehand_text=''` 半边未验** —— 如实登记(`story-002:193-196` / 本测 docstring `:306-308`),理由是该字段住 blob 变长段、超出 `PayloadCodec.TryGetPayload` 接口面。**登记措辞正确**(未写成「已覆盖」)。 |
| **BLK-9** | **CLOSED** | 点名 fixture + 真装配 + 零流断言齐备。判别力成立:删 `TryOpen` 的 `if (patient.IsNone) return NoPatient;` ⇒ 结果码变 `PatientAbsent` ⇒ 断言 ① 红;两前置全删 ⇒ 真流多一条 `CaseOpened` ⇒ 断言 ② 红。 |
| **BLK-10** | **CLOSED(登记面)· 附一处措辞瑕疵** | 映射表 11 行;**点名判据全部真实存在**(见下「载体抽查」);测数勘误(`casebook_command_router_test` 8→**9**、`ink_age_test` 4→**5**)与实测一致。⚠️ **瑕疵**:AC-37-05 处置半边行的「关键点名判据」首列 `test_ac3721_checkedButNoTreatmentInWindow_rejects` 实为**只喂布尔**的决策层测(恰是补评批评的形态);该 AC 的真跨流断言在**同文件的** `test_windowBoundary_multipleTreatmentsInWindow_hasTreatment`(真 `EmergencyTreatmentApplied`/`DrugTreatmentApplied` + `HasTreatmentInWindow`)与 `test_ac3721_revisitBackdoor_*`。且该行写「`test_windowBoundary_*` **四测**」,实测为 **5** 测(其中仅 1 测跨流)。⇒ **属实但点偏**,非伪造。 |

### 4.1 载体抽查(映射表 BLK-10 修复本体的核验)

| 抽查载体 | 点名判据是否存在 | 判据内容是否与映射声明相符 |
|---|---|---|
| `EditMode/CaseSystem/case_open_writer_test.cs`(7) | ✅ `:49` | ✅ 真组合根 + 真流:断言**病例流恰增 1 条** + 载荷池 round-trip + `OpenCaseOf` 派生 `case_id`(= `(Tick,Patient,Seq)`) |
| `EditMode/Boot/judgment_writer_test.cs`(4) | ✅ `:38`/`:65` | ✅ 真装配,断言**病例流 `before+1`** + 尾事件 `Kind==JudgmentRecorded` |
| `EditMode/Boot/casebook_command_router_test.cs`(**9**) | ✅ `:117` | ✅ 断言第三次提交 ⇒ **擦除 = 写空判断**(`JudgmentRevised` + `lexicon_id=0` + `confidence=0`),非删事件 |
| `PlayMode/case_opened_wiring_test.cs`(1) | ✅ `:78` | ✅ 真启动序(BootRoot → 读 `_services.Stream` → 断言含 `CaseOpened`) |
| `EditMode/Boot/ink_age_test.cs`(**5**) | ✅ `:97` | ✅ 全生产路径跨病人墨龄判别 |
| `EditMode/CaseSystem/case_replay_persistence_test.cs`(10) | ✅ `:98`/`:45` | ✅ noFold 两测 + 字节 round-trip |
| `EditMode/CaseSystem/case_close_precondition_test.cs`(39) | ✅ `:77`/`:242…274` | ⚠️ 见 BLK-10 措辞瑕疵(首列测为布尔决策测;真跨流测在同文件另一测) |
| `EditMode/CaseSystem/judgment_lexicon_bake_test.cs`(11) | ✅ `:73` `test_lexiconIdZero_rejected` | ✅ 与「0 是保留空栏哨兵」一致(与写者接受 0=擦除不冲突:一为词表行、一为判断值) |
| `PlayMode/SkeuomorphicUI/e2e_full_loop_test.cs`(1) | ✅ `:390` | — |

**结论**:映射表**无一个判据名是虚构的**(这是最易注水的点,已逐名 grep 复核);**无新借绿**。

### 4.2 两条「部分闭合」自陈的诚实性

- **BLK-5 `freehand_text=''` 半边**:登记为「部分闭合 / 只验可验的两半」—— **诚实**。本件确认:测只断言 `lexicon_id=0` + 擦除事件入流;`freehand_text` 确不在 `JudgmentRecordedPayload`/`JudgmentRevisedPayload`(经 `JudgmentWriter` 以 `freehandText: string.Empty` 承载,住 blob 段)。**未写成「已覆盖」**。✅
- **BLK-10 「无第二副本」半边**:登记为「语义结构性满足,**无点名断言**」—— **诚实**。本件确认:`grep -rln "第二副本" unity/Assets/Tests/` = **0 命中**;语义载体 `CaseCloseQuery.HasTreatmentInWindow`(入参即病史流)真实存在。⚠️ 唯一瑕疵:自陈里的「`grep "第二副本"` 全仓 **0 命中**」现已**不成立**(实测 16 命中)—— 但 16 处**全部是登记/评审文本自引**,测试树仍 0,故**实质结论不受影响**,仅该措辞已过时。✅(措辞级)

---

## 五、Test Evidence 门判定

> **通过**

**理由**:
1. 声明件**存在且通过** —— `CaseOpenAndJudgmentTest` 25/25 passed(主轮最终全量日志 `s11-final-EditMode.xml`,且该 run **晚于**被评文件最后一次改动 ⇒ 非旧码借绿)。
2. 原门失败的**全部净保留 BLOCKING**(BLK-8 + BLK-1/2/3/5/9 + BLK-10)**逐条闭合**(见 §四),无一条 STILL-OPEN。
3. BLK-10 的修复本体(**AC → 载体映射表**)经**逐名 grep** 复核为真,无虚构判据名、无新借绿。
4. 两条「部分闭合」**如实登记**,措辞未越权(未把部分写成全覆盖)。
5. 架构纪律未见回退(纯函数归属判据 · 调用点零改 · 引用集一字未动 —— `Sim` 引用集仍 = {BCL, Sim.Contracts})。

---

## 六、未闭项 / 本件不确定项(如实 · 禁借绿)

### 6.1 未闭(ADVISORY 级 · 非门阻塞 —— 承补评 §六·三 的净保留)

1. **ADV-1(freehand_text「引用扫描」未真做)** —— story AC #6 字面要求「**引用扫描**:freehand_text 零判定消费点」;声明件 `:427-438` 做的是**字段缺席代理**(断言两个 payload struct 无 `FreehandText` 字段),**不是引用扫描**。补评判其为 ADVISORY(非 BLOCKING),故**不阻塞本门**;但 ⚠️ **映射表对此 AC 未登记该缺口** —— 映射行只写「Judgment 形状」,读者可能误以为含引用扫描半边。**建议**:在映射表补一行,把该半边显式登记为「代理级、无引用扫描」。
2. **ADV-2(`Decide` 是孪生体)** —— `CaseOpenDecider.Decide` 仍**无生产调用者**(真实路径 = `CaseOpenWriter.TryOpen`);声明件的 AC-37-30 / AC-37-26 测试打在 `Decide` 上。映射表已把 AC-37-30/26 标为「**决策层**」并把流级真载体指向 `case_open_writer_test` ⇒ 登记口径正确,但 `Decide` 的孪生冗余仍在。
3. **ADV-4 / ADV-5** —— 声明件内多条测反复打 `Decide` 同一分支;`test_ac3723_revisionHistory_allInCaseStream` 的排序断言仍在**已排序输入**上做(重排恒等)。映射表已把该 AC 限定为「**路由面**」,口径诚实;真不折叠载体在 `case_replay_persistence_test`。

### 6.2 措辞级瑕疵(不影响判定 · 建议回刷)

4. 映射表「AC-37-05 处置半边」行的首列点名判据是**布尔决策测**(见 §四 BLK-10 行),且「四测」应为 **5**。
5. 自陈「`grep "第二副本"` 全仓 0 命中」已过时(实测 16,全为登记文本自引)。

### 6.3 本件的取证限度(如实)

- **未起 Unity batch**(硬约束);本 fixture 与全量的「绿」**只读借用主轮** `s11-final-*.xml`,**已标注来源**;本件**亲跑**的仅为 grep / git / stat(§三·3.1)。
- 主轮日志 `s11-final-EditMode.xml` 为 `unity/Logs/` 下产物(该目录 `.gitignore` 覆盖)⇒ **易失**;本件已把关键行**逐字抄入 §三·3.1⑥**,使其可独立复核。
- 未逐一复核映射表**其余** 9 个载体的**每一条**断言(抽查 8 个点名判据;其余按「判据名存在」核验)。若需全量,可对每载体逐一 `Read`。
- 未复核 `CompositionRoot.cs:245-246` 是否**逐字未改**(修复件自陈未改)—— 本件只核了 `CasebookVitalsSource` 侧的调用形态与 `LastJudgmentOf` 新实现自洽。

---

## 附:与原判定对照的一句话

补评件 §七·四 净结论 = 「声明证据与 AC 错配 + 一条真实现缺陷」。**本轮复核:两者均已消除** ——
真实现缺陷(BLK-8)已由流序关联修复并有端到端判别夹具;AC 错配已由**经逐名核验为真**的映射表收口。
**门:不通过 → 通过**。残留仅为补评已定级的 ADVISORY(ADV-1/2/4/5)与两处措辞瑕疵,均**不阻塞本门**,建议在 sprint-12 一并回刷。
