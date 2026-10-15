# 当下状态新评审 —— case-system story-003(修复后复核)

**评审时点**:2026-10-15(**新评审 · 评当下代码**,非追认 —— 承 `.claude/docs/review-workflow.md` §义务 3)
**被评对象(当下工作树)**:
- `unity/Assets/Sim/CaseCloseDecider.cs`(`CaseCloseDecider` / `TreatmentKinds` / `CaseCloseQuery`)
- `unity/Assets/Sim/CaseCloseWriter.cs`(新增 · 结案写者)
- `unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs`(39 测)
- `unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs`(12 测)
- `design/gdd/case-system.md` AC-37-18 行(`:1171`)
**前次判定**:`production/qa/evidence/review-case-system-story-003-retro-2026-10-15.md`(含 §七 勘误)
**本轮约束**:⛔ 未跑 Unity(主轮正跑全量)· ⛔ 未改任何既有文件(仅新建本报告)
**性质**:本件 = **评审**(评当下),不是修复件、不追认任何历史判定。

---

## 一、原判定(逐条,承补评件 §七·三「修正后净结论」)

去重后 **4 类 BLOCKING**(原 5 类,§七·一 已推翻 B4):

| 编号 | 命题 | 原判据 |
|---|---|---|
| **① B-1** | `CaseCloseQuery.IsClosed` 按 `Patient` 而非 `case_id` 匹配 ⇒ 复诊新案被判「已结」⇒ **复诊案永远无法结案** | `CaseCloseDecider.cs:135`(旧)`if (e.Patient.Value == caseId.Patient) return true;` |
| **② B2** | 快照半场零行为覆盖(5 处反射字段存在性检查 = 借绿);**生产侧无快照计算** | `case_close_precondition_test.cs` 全部 `CaseClosedPayload` 证据为反射;`WriterExistenceGate.cs:87` 具名豁免自陈「写者未落」 |
| **③ B3** | AC-37-18 三条用例的夹具均不含该 AC 的输入(**恒真断言**) | `:310-325`(无死亡输入)/ `:329-336`(无空名)/ `:340-350`(无空集) |
| **⑤ B5** | `TreatmentKinds` 收录 `InjuryOnset` **无任何权威件授权**,语义相反 | `CaseCloseDecider.cs:72-77`(旧)`{Emergency, Drug, InjuryOnset}` |
| ~~④ B4~~ | ~~副作用序列两件零测试~~ | **§七·一 已推翻** —— 判据在 8 自己的件里 |

**A-5**(ADVISORY,同命题另一半):「37 自身不产生后果事件」+ 副作用序列在 37 侧无判据。

**前次门结论**:不通过。

---

## 二、修复落点(逐条对)

### ① B-1 → S11-8(证据:`fix-s11-8-case-system-story-003-b1-2026-10-15.md`)

| # | 文件 | 具体改动(当下代码实读) |
|---|---|---|
| ① | `Sim/CaseCloseDecider.cs:192-216` | `IsClosed` 改**流序扫描关联**:`current` 随该病人 `CaseOpened` 更新(`:202-206`),遇该病人 `CaseClosed` 时**仅当 `current.Equals(caseId)`** 判已结(`:207-213`),随后清 `hasCurrent` |
| ② | `Sim/CaseCloseDecider.cs:229-232` | `IsOpen := OpenedInStream(c) ∧ ¬IsClosed(c)` —— 与 `IsClosed` **共用同一输入契约**(消 A-6) |
| ③ | `Sim/CaseCloseDecider.cs:43-75` | `Decide` **两判序对调**(`isClosed` 先于 `isOpen`,旧序 `!isOpen` 在前会把已结案报成错因码 `RejectNotOpen`) |

**偏离(已如实登记)**:不用 `PayloadRef` 解码 `CaseClosedPayload.CaseId` —— `PayloadCodec` 住 `Sim.Codec`,`Sim` 引用集恰 = {BCL, `Sim.Contracts`}(ADR-025 §① / b2 门),**结构上够不着**。改走流序关联(与 `CaseStreamQuery.OpenCaseOf` `CaseOpenDecider.cs:74-93` **同口径**,已核实两处 `new CaseId(e.Tick, e.Patient.Value, e.Seq)` 构造逐字一致)。代价(归属是**推断**非**读取**;仅非法流分歧)已登记于 `CaseCloseDecider.cs:178-181`。

### ② B2 → 写者落地(证据:`fix-s11-13-…-writers-2026-10-15.md` + `fix-s11-15-…-assertions-2026-10-15.md`)

| # | 文件 | 具体改动 |
|---|---|---|
| ① | `Sim/CaseCloseWriter.cs`(新 · 181 行) | 结案写者;`TryClose` 于 `:143-146` 由**真流**求 `isClosed`/`isOpen`/`hasTreatmentInWindow`,交 `CaseCloseDecider.Decide` **同一判序**裁决(`:148`),过则经 `IPayloadEncoder.Encode` 写一条 `CaseClosed` |
| ② | `Sim/CaseCloseWriter.cs:169-171` | `treated = hasTreatmentInWindow`(**本类自算**,非调用方转述 —— 调用方只给 `isChecked`,不给处置证据) |
| ③ | `Sim/CaseCloseWriter.cs:118-121` | `disease_set` 由**调用方**给出 —— 承 `CaseOpenWriter.disease_snapshot` 的「Sim 侧够不着者由调用方给出」**已登记先例**(`CaseOpenWriter.cs:28-31`,实读确认) |
| ④ | `Editor.Tools.Gates/WriterExistenceGate.cs:87-95` | 撤 `CaseClosed` / `PatternRecognized` 两条具名豁免(实读确认表内**已无**该两条) |
| ⑤ | `Tests/EditMode/CaseSystem/case_close_writer_test.cs`(新 · 12 测) | 真装配袋(真 `EventStream` + 真 `PayloadEncoder`)端到端 |

### ③ B3 → 判据收窄 + 夹具判别化(证据:`fix-s11-15-…`)

| # | 文件 | 具体改动 |
|---|---|---|
| ① | `design/gdd/case-system.md:1171` | AC-37-18 行内**加注**(原文一字保留 + `<br>` 后追加收窄说明「病人死亡**不进事件流** ⇒ 37 结构性不可见 ⇒ 可测内容收窄为**37 无任何自动结案路径**」) |
| ② | `case_close_precondition_test.cs:466-490` | `test_ac3718_caseOpen_canClose` 夹具由「只造一条 `CaseOpened`」改为**塞满非 `CaseClosed` 事件**(`DrugTreatmentApplied`/`EmergencyTreatmentApplied`/`CareApplied`/`InjuryOnset`/`JudgmentRecorded`)+ 断言案件仍「开」 |

### ⑤ B5 → S11-10(证据:`fix-s11-10-…-b5-2026-10-15.md`)

| # | 文件 | 具体改动(实读确认) |
|---|---|---|
| ① | `Sim/CaseCloseDecider.cs:103-108` | 处置集 = `{EmergencyTreatmentApplied, DrugTreatmentApplied, CareApplied}`(撤 `InjuryOnset`,补 `CareApplied`) |
| ② | `Sim/CaseCloseDecider.cs:100-132` | `Set` 由 `public static readonly HashSet` 改 `private Members` + `Contains` 出口 + `MembersSnapshot()` 返独立副本(顺批 A-1) |
| ③ | `case_close_precondition_test.cs:299-393` | 撤反向断言缺陷的旧测;新增 `containsCareApplied` / `excludesInjuryOnset` / `excludesEmergencyAttempt` / `equalsAuthorityExactly`(逐字引 ADR-008 §四)/ `snapshotIsIsolatedFromSource` |

---

## 三、验证命令(可复跑 · 可证伪)+ 实跑输出

> ⚠️ 本评审**未跑 Unity**(硬约束:主轮正跑全量)。以下「实跑」= 只读静态核验(读文件 + grep)。
> Unity 全量测数**引自主轮**(已注明来源),未由本件复跑。

### 3.1 本件实跑(只读,可逐字复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# V-1 处置集成员(期望 = {Emergency, Drug, CareApplied},无 InjuryOnset)
sed -n '103,108p' unity/Assets/Sim/CaseCloseDecider.cs
# 实跑输出:
#   private static readonly HashSet<EventKind> Members = new HashSet<EventKind>
#   {
#       EventKind.EmergencyTreatmentApplied,  // 10 急救**已施加**
#       EventKind.DrugTreatmentApplied,       // 11 用药**已施加**
#       EventKind.CareApplied,                // 8  护理动作(病史流)
#   };
#   ⇒ ① B5 CLOSED

# V-2 IsClosed 流序关联(期望:current.Equals(caseId) 精确三元组)
sed -n '192,216p' unity/Assets/Sim/CaseCloseDecider.cs
# 实跑输出:current = new CaseId(e.Tick, e.Patient.Value, e.Seq); ... if (hasCurrent && current.Equals(caseId)) return true;
#   ⇒ ① B-1 CLOSED

# V-3 treated 本类自算
sed -n '143,172p' unity/Assets/Sim/CaseCloseWriter.cs
# 实跑输出:bool hasTreatmentInWindow = CaseCloseQuery.HasTreatmentInWindow(_streamEvents, caseId.Patient, caseId.Tick, tick);
#          ... var payload = new CaseClosedPayload(caseId.Patient, caseId, diseaseSet, hasTreatmentInWindow);
#   ⇒ ② B2 生产侧确有快照计算,treated 非调用方转述

# V-4 disease_set 先例(CaseOpenWriter)
sed -n '28,31p' unity/Assets/Sim/CaseOpenWriter.cs
# 实跑输出://   - `disease_snapshot` 由**调用方**给出(9 的病种状态在解码侧,Sim 侧够不着;与
#          //     `PoiStateMachine` 「读侧由调用方喂已解码字段」的甲案同构)。
#   ⇒ ② B2 的 disease_set 口径 = **已登记先例**,非新开例外

# V-5 GDD AC-37-18 加注(原文保留?)
grep -n "AC-37-18" design/gdd/case-system.md
# 实跑输出(:1171):
#   | **AC-37-18** | **GIVEN** 一个病人死亡但病例未结,**WHEN** 玩家回到脉案,**THEN** 该病例仍为「开」,无自动结案。
#   <br>⚠️ **2026-10-15 判据收窄(用户裁定「裁『不进流』+ 改判据」)**:病人死亡**不进事件流** ⇒ 37 对它
#   **结构性不可见** ... ⇒ 本 AC 的可测内容 = **37 无任何自动结案路径** ... | `[L]` 单测 · **BLOCKING** |
#   ⇒ ③ 的 GDD 加注**如实**(原文逐字保留 + 收窄说明)

# V-6 AC-37-18 判别化夹具
sed -n '466,490p' unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs
# 实跑输出:夹具含 CaseOpened + Drug/Emergency/CareApplied/InjuryOnset/JudgmentRecorded,断言 IsOpen == True
#   ⇒ ③ 死亡半边判别化已落

# V-7 测试计数
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs  # → 39
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/case_close_writer_test.cs          # → 12
#   ⇒ 与 story 自述 39 测 / writer 12 测一致

# V-8 WriterExistenceGate 撤表
grep -n 'CaseClosed\|PatternRecognized' unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs
# 实跑输出:仅 :87-90 的撤表**注释**;豁免元组表内**已无**该两条
#   ⇒ ② B2 的「写者未落」豁免已撤

# V-9 生产消费者 / 接线
grep -rn "new CaseCloseWriter" unity/Assets --include=*.cs
# 实跑输出:仅 case_close_writer_test.cs(:46/268/270/272) —— **生产零实例化**
#   ⇒ ⚠️ 写者**未接线**(已由 fix-s11-13 §2.5 如实登记)

# V-10 ADR-029 门(Sim/ 内 new PayloadRef()
grep -rn "new PayloadRef(" unity/Assets/Sim --include=*.cs | grep -v "/Tests/"
# 实跑输出:全部命中皆为**注释**(CaseCloseWriter.cs:13/93 等);**零真实调用点**
#   ⇒ 写者符合 ADR-029 §③

# V-11 A-5 三处点名判据 grep 核实
grep -n "OnCaseClosed\|FrozenJudgment.Frozen\|HandoffTo53" unity/Assets/Tests/EditMode/DiagnosisSystem/reading_state_test.cs
# 实跑输出:
#   :396  Assert.IsTrue(r.FrozenJudgment.Frozen, "② 判断冻结");
#   :399  Assert.AreEqual(JudgmentState.Suspected, r.HandoffTo53, "③ 交 #53 = 冻结时刻的判断状态");
#   :410  Assert.AreEqual(SignReadState.Blank, DiagnosisReadingFsm.OnCaseClosed(s), $"任意读数状态关病例 ⇒ 清除({s})");
#          (包裹在 foreach (SignReadState s in Enum.GetValues(typeof(SignReadState))) 内 —— 全状态遍历)
grep -n "test_case_ac37_20_noConsequenceOwnership" unity/Assets/Tests/EditMode/CaseSystem/case_secrecy_discipline_test.cs
# 实跑输出::241  public void test_case_ac37_20_noConsequenceOwnership()  —— 对 5 个 37 出站载荷逐个反射扫越权语义 + 空集绿守卫
#   ⇒ A-5 点名的三处判据**均在**(见 §五 详表)
```

### 3.2 Unity 全量(⚠️ 引自主轮,非本件复跑)

| 平台 | 结果 | 来源 |
|---|---|---|
| EditMode | `3387 / 3341 / 0 / 46`(退出码 0) | **主轮**(本件未跑) |
| PlayMode | `186 / 186 / 0`(退出码 0) | **主轮**(本件未跑) |

story-003 自述门验证行(`story-003:254`)引用的即是该组数 ⇒ 与本件引据同源。

---

## 四、独立复核结论(逐条)

| 编号 | 现状态 | 依据 |
|---|---|---|
| **① B-1** | ✅ **CLOSED** | 代码实读:`IsClosed:192-216` 精确三元组关联;哨兵 `test_isClosed_revisitNewCase_notClosed:613-632`(含**反向对照**);端到端 `test_e2e_revisitClosePath_…:744-823`;`fix-s11-8 §四③` 报「改回旧实现 ⇒ 7 红」。**口径代价如实登记**(`:178-181` + 件 §2.2)。**新问题:未发现** |
| **② B2** | ✅ **CLOSED**(带登记残余) | 生产侧确有快照计算(`CaseCloseWriter.cs:169-171`);`treated` 本类自算;`disease_set` 用 `CaseOpenWriter` **已登记先例**;豁免表已撤;12 条真装配行为测。残余:写者**未接线**(`fix-s11-13 §2.5` 已如实登记) |
| **③ B3** | ⚠️ **PARTIALLY-CLOSED** | 死亡半边 CLOSED(GDD 加注如实 + 夹具判别化 + 变异探针可信,见 §五·二);**空名 / 纯伤情两条仍恒真**(见 §六 新发现) |
| **⑤ B5** | ✅ **CLOSED** | 处置集 = 权威枚举(实读);`equalsAuthorityExactly:360-374` 逐字引 ADR-008 §四(**非**引生产默认值);`fix-s11-10 §四③` 报「回退成员表 ⇒ 4 红」 |
| **A-5** | ✅ **CLOSED(判据在别处属实)** | 三处点名判据 grep 全数核实存在(见 §五·一) |

---

## 五、独立核验的深水区(任务点名的三问)

### 5.1 A-5「判据在别处」是否属实 —— ✅ 属实(grep 逐处核实)

| A-5 子命题 | 点名处 | grep 实核 | 结论 |
|---|---|---|---|
| **→8 读数清除** | `reading_state_test.cs:409-411` | **:410** `Assert.AreEqual(SignReadState.Blank, DiagnosisReadingFsm.OnCaseClosed(s), …)`,**确在** `foreach (SignReadState s in Enum.GetValues(typeof(SignReadState)))` 全状态遍历内 | ✅ 在 |
| **→53 判断冻结** | 同文件 `:396` / `:399` | **:396** `r.FrozenJudgment.Frozen` · **:399** `r.HandoffTo53` · **:404-406** 竞态断言「冻结后落笔必拒」 | ✅ 在 |
| **「37 自身不产生后果事件」** | `case_secrecy_discipline_test.cs:240-273` | **:241** `test_case_ac37_20_noConsequenceOwnership` —— 对 5 个 37 出站载荷(`CaseOpened`/`CaseClosed`/`PatternRecognized`/`JudgmentRecorded`/`JudgmentRevised`)逐个反射扫越权 token + **空集绿守卫** | ✅ 在 |

⇒ **A-5 的「判据在别处」不构成新借绿** —— 三处判据**真的存在**。残余(已登记):该三处是**纯函数层**判据,不是「结案真的触发了清除」的**端到端**判据(37 结案 → 8 `CloseCase` 的接线 P0 无载体)。

### 5.2 ③ 的变异探针说法是否可信 —— ✅ 可信(按代码推演)

探针:`CaseCloseDecider.IsClosed` 注入 `|| e.Kind == EventKind.CareApplied`(「治疗即自动结案」)。
**按当下代码推演**:`test_ac3718_caseOpen_canClose:466-490` 的流 = `[CaseOpened@100, Drug@110, Emergency@120, **CareApplied@130**, InjuryOnset@140, Judgment@150]`,`caseId=(100,1,0)`。
注入后扫描至 `CareApplied@130` 时 `hasCurrent=true ∧ current=(100,1,0)==caseId` ⇒ `IsClosed` 返回 **true**
⇒ `IsOpen = OpenedInStream(true) ∧ ¬IsClosed(true) = **false**`
⇒ 断言 `:481 Assert.That(isOpen, Is.True)` **转红**。
⇒ 自陈 `total=1 passed=0 failed=1`(单测 filter)**与代码推演一致,可信**。
补充:夹具含 `InjuryOnset`/`JudgmentRecorded` ⇒ 该断言对「伤情即结案 / 判断即结案」同类变异**同样转红**,判别面不止 `CareApplied` 一支。

### 5.3 ① 修复是否引入新问题 —— 未发现(代价如实登记)

- 两 `CaseId` 构造点逐字一致(`CaseCloseDecider.cs:204` vs `CaseOpenDecider.cs:82`)⇒ `IsClosed` 与 `OpenCaseOf` **同口径**,不引入口径分裂。
- 流序关联的**唯一分歧面** = 违反「同一病人同时至多一个开案」(GDD 规则二)的**非法流**;而 `EventStream` 的 tick 非降不变量(`EventStream.cs:109`)保证合法流的全序 ⇒ 归属推断在契约内**恒正确**。代价已登记,非隐藏。
- `Decide` 判序对调**结果面零变化**(两分支皆拒绝且零写入),只改理由码 —— 已由 `fix-s11-8 §四③` 的「判序 3 红」佐证(改回即 `RejectAlreadyClosed → RejectNotOpen`)。

---

## 六、⚠️ 新发现 / 未真正验证的东西

### 6.1 【新发现 · ADVISORY】AC-37-18 的「病名为空」「纯伤情空集」两条测试**仍恒真**,且**未登记为残余**

`fix-s11-15 §2.2 ③` 只判别化了**一条**(`:459`,即死亡半边)。另两条**原样留存**:

| 测试 | 行 | 实况 | 问题 |
|---|---|---|---|
| `test_ac3718_emptyDiseaseName_canClose` | `:493-501` | 仅 `Decide(isOpen:true, isClosed:false, hasTreatmentInWindow:true, isChecked:true) == CanClose` | **「病名为空」根本不在夹具里** —— 它与 happy path(`:484-489`)逐字重复,零独立信息 |
| `test_ac3718_pureInjuryCase_emptyDiseaseSet_canClose` | `:504-515` | `new DiseaseIdSet(0)` 断言 `Bits==0`(构造器恒真)+ 同一 `Decide` 字面量 == `CanClose` | `emptyDiseaseSet` **从未进入 `Decide`**(`Decide` 无 `disease_set` 形参)⇒ 与「纯伤情案」正交 |

**性质**:这正是补评 §六·一 表 #15 / #16(评 ❌极低)与 **B3 明列的三条**中的两条。B3 的 BLOCKING 头条(死亡输入)已闭,但**这两条属 B3 的显式列举面**,现状 = **PARTIALLY-CLOSED**。
**正解**(与死亡半边同构):「病名」是 39/8 的 **UI 概念**,`37` 的 Sim 层**结构性不可表达**(同「死亡不进流」)—— 应**记 NOT-RUN 并说明**,而非断言一个与命题无关的常量。本轮**未做**,且 story 登记块 `:212-254` 亦**未把它列为残余**(仅登记了「处置集补 AC 归用户裁」)。
**门影响**:低(补评自身评其 ❌极低);但 story 的「③ AC-37-18 全部已清」表述**略过宽** —— 准确说是「死亡半边已清」。

### 6.2 【新发现 · ADVISORY】B2 闭合的 `treated` 断言**结构性恒真**,不构成「自算」的判别证据

`case_close_writer_test.cs:110` 断言 `decoded.Treated is True`。但 `treated` 只在 `Decide==CanClose` 分支被写入,而 `CanClose ⟹ hasTreatmentInWindow==true` ⇒ **任何被写入的 `CaseClosed` 其 `treated` 恒为 true** ⇒ 该断言**无法区分**「本类自算」与「硬编码 true」。
**B2 仍成立**,但闭合的**载体是生产代码**(`CaseCloseWriter.cs:171` 确传 `hasTreatmentInWindow`),**不是**该断言 —— 该断言的判别力对「self-computed」这一命题**为零**。若要真判别,需一条「调用方转述 false 但流内有处置 ⇒ 写者仍算 true」的对照夹具(当前接口下**不可构造**,因调用方无从注入 treated)。
**门影响**:低(B2 由代码闭合)。如实登记,不冒充已判别。

### 6.3 【旧残余 · 未清】`IsInWindow` 生产零调用点(补评 A1)仍在

`grep` 全仓:`IsInWindow` 唯一出现 = `CaseCloseDecider.cs:255` 定义处;**生产与测试调用点皆零**(补评称测试有 `T:176/184/192/200`,现文件已重构)。真实谓词 `HasTreatmentInWindow:150-164` **仍自行内联**比较。⇒ 「立案 tick 端 / 结案 tick 端」的闭区间夹具(`:242-270`)测的是**未被任何生产路径使用的方法**。本 story 的处置窗口真判据在 `HasTreatmentInWindow`(有 `test_fullFlow_edge_*` / writer 端夹具覆盖),故**非 BLOCKING**;但 `IsInWindow` 是**悬垂方法**(补评 A1),本轮未处置。

### 6.4 【旧残余 · 未清】`EmergencyAttempt` 排除无显式 AC 守卫(B5 的登记缺口)

story 登记块 `:206-210` 自陈:B5 是 BLOCKING 级却**不对应任何 `AC-37-xx`**(GDD 36 条 AC 无一条枚举处置集)。守卫**只在测试件 + ADR 闭环纪律**里。**归用户裁**,本轮未自行加 AC。现状如实。

---

## 七、Test Evidence 门判定

> ## ✅ **通过**
> (BLOCKING 4 类核心命题已闭;**附一处 ADVISORY 残余** —— §6.1)

**理由**:
1. **① B-1 / ⑤ B5** —— 两条**最重**的代码/规格缺陷**已闭**,且各有**双向证伪链**(修复前红 · 修复后绿 · 改回即转红),本件实读代码**独立确认**当下状态即修复态。
2. **② B2** —— 「生产侧无快照计算」的核心命题**已闭**(`CaseCloseWriter` 自算 `treated`;`disease_set` 承**已登记先例**);残余 = 写者**未接线**,已由修复件 §2.5 **如实登记**,非隐藏。
3. **③ B3** —— **BLOCKING 头条**(死亡输入不可表达 ⇒ 恒真)已闭:GDD 加注**如实**(原文保留)、夹具**判别化**、变异探针**经代码推演证实可信**。残余(空名 / 纯伤情两条仍恒真)评 **ADVISORY**(补评自身评 ❌极低),**不阻断门**。
4. **A-5** —— 「判据在别处」**经 grep 逐处核实属实**,不构成借绿。
5. 无**新增 BLOCKING 级**借绿(§六 三项均为 ADVISORY / 旧残余)。

**⚠️ 附条件(须与门结论同批登记,否则本判定不得单独引用)**:
- **§6.1** —— AC-37-18 的「病名为空 / 纯伤情空集」两条测试**应转 NOT-RUN 或补判别力**;story `:212-254` 的「③ 全部已清」应订正为「③ 死亡半边已清」。
- **§6.2** —— `treated is True` 断言的判别力为零,B2 的闭合载体是**代码**非该断言。

---

## 八、未闭项 / 不确定(如实)

1. **§6.1 ③ 的残余未登记** —— 我不确定项目是否把「空名 / 纯伤情」两条视作 B3 的必需闭合面。若视作必需 ⇒ ③ 降为 PARTIALLY-CLOSED 且门应「不通过(待补 NOT-RUN)」。本件按「B3 头条已闭 + 补评评其 ❌极低」判 ADVISORY。
2. **未跑 Unity** —— 本件全部结论来自**只读核验**(读文件 + grep);全量测数引自主轮(`3387/3341/0/46` · `186/186/0`)。若主轮测数与 story 自述不符,请以主轮为准。
3. **变异探针未亲跑** —— §5.2 是**代码推演**,非实跑(硬约束禁起 batch)。推演与自陈一致,但**非**第一手执行证据。
4. **写者未接线的实害面** —— `CaseCloseWriter` 生产零实例化(§3.1 V-9)。故「快照真的被算出并写入」在**运行期**仍**无端到端载体**;B2 的闭合是**类级**(生产代码存在 + 行为测),非**运行期端到端**。承 `fix-s11-13 §2.5` 与 story §Out of Scope 的延后登记,**不因本件转绿**。
5. **`CaseCloseDecider.cs` 未在 `git status` 的 modified 列表** ⇒ 其 S11-8/S11-10 改动**已提交**;`case_close_precondition_test.cs`(M)与 `CaseCloseWriter.cs`(??)为**工作树未提交态**。本件评的是**当下工作树**。

---

**签署**:本件为**新评审**(评当下),不追认 `31e3f9b`、亦不追认补评件;落盘于
`production/qa/evidence/review-case-system-story-003-postfix-2026-10-15.md`。
