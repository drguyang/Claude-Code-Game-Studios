# 修复证据原件 —— S11-8 / case-system story-003 B-1(`IsClosed` 粒度退化)

**日期**:2026-10-15
**对象**:`unity/Assets/Sim/CaseCloseDecider.cs`(`CaseCloseQuery` / `CaseCloseDecider`)+
`unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs`
**来源判定**:`production/qa/evidence/review-case-system-story-003-retro-2026-10-15.md` §二 **B-1**(BLOCKING)
**排期**:`production/sprints/sprint-11.md` **S11-8**(用户裁定「必须项优先」并入)
**性质**:本件 = **修复证据**,**不是评审**;它不追认也不重做任何判定(承 `.claude/docs/review-workflow.md` §义务 3)。

> ⚠️ **为何单独落件**:本件引用的原始 XML 落在 `unity/Logs/`,而该目录**在 `.gitignore:196`**
> ⇒ 是**易失中间产物**,不得作为交付证据。故把**判据、命令、输出原文**收进本件,使其**自洽可复核**。

---

## 一、原缺陷判定(逐字承补评件 §二 B-1,不重新表述)

`CaseCloseQuery.IsClosed` 的匹配键**过宽**:原实现 `CaseCloseDecider.cs:135`
= `if (e.Patient.Value == caseId.Patient) return true;`
—— 把「该病人**曾有过**任何 `CaseClosed`」当作「**本** `case_id` 已结」,匹配粒度从 `case_id`
**退化为 `Patient`**。作者注释 `:129` 自承「实际生产代码应通过 `PayloadRef` 解码 `case_id` 匹配」
⇒ **未完成态,非等价实现**。

**实害链**:流 `[Opened@100, Closed@200, Opened@300]`(病人 1)上
`IsClosed(复诊新案) == true` ⇒ `Decide` 命中 `RejectAlreadyClosed`
⇒ **复诊案永远无法结案**。冲突落点:GDD `case-system.md:250-256`(规则六-4,幂等定义 = **同一** `case_id`)
· `:818-819`(EC-37-1 复诊「新病例,`case_id` 三元组不同」)。

**缓解事实(不改变判定)**:当时**无生产调用点**(`CaseClosed` 写者未落,`WriterExistenceGate.cs:87` 具名豁免)
⇒ 属**契约层缺陷**,非当时运行期故障。

---

## 二、修复落点

### 2.1 代码(`unity/Assets/Sim/CaseCloseDecider.cs`)

| # | 落点 | 修前 | 修后 |
|---|------|------|------|
| ① | `CaseCloseQuery.IsClosed` | 按 `Patient` 命中即返回 true | **流序扫描关联**:`current` 随该病人 `CaseOpened` 更新;遇该病人 `CaseClosed` 时**仅当 `current == caseId`** 判已结,随后清 `current` |
| ② | `CaseCloseQuery.IsOpen` | 独立 toggle 循环(隐含**不同**输入契约) | `OpenedInStream(c) ∧ ¬IsClosed(c)`,与 `IsClosed` **共用同一契约**(消 ADVISORY **A-6**) |
| ③ | `CaseCloseDecider.Decide` | `!isOpen` 先判 | **两判序对调**:`isClosed` 先于 `isOpen` |

**③ 的理由**:流派生口径下 `IsOpen = 开过 ∧ ¬已结` ⇒ **已结案的 `IsOpen` 恒假** ⇒ 旧序会把已结案报成
`RejectNotOpen`(**错因码**);且 `(isOpen=true ∧ isClosed=true)` 在旧序下**只可能来自硬编码**
(即补评 **A-3** 的不可达状态)。对调后该组合**真正不可达**,且**结果面零变化**
—— 两分支皆为「拒绝且不写事件」(GDD 规则六-4 的唯一要求),**改的只是理由码**。
`(false,false)` = 从未开过;`(false,true)` = 开过且已结(修前此组合是**正常生产路径**)。

### 2.2 ⚠️ 与计划/补评的两处偏离(执行期就地改判 · 已报用户并获批)

**① 判等路径不用 `PayloadRef` 解码 —— 该路径结构上不可行。**
补评 §二 与 `CaseCloseDecider.cs:129` 作者注释**均建议**「经 `PayloadRef` 解码 `CaseClosedPayload.CaseId` 判等」。
实测:`PayloadCodec` 住 **`Sim.Codec`**,而 `CaseCloseDecider` 住 **`Sim`**;`Sim` 引用集
**恰 = {BCL, `Sim.Contracts`}**(ADR-025 §① / b2 门),`PayloadRef` 只有 `BlobId/Offset/Length`
—— **物理上够不着**。⇒ 改走**流序扫描关联**(与既有正确范式 `CaseStreamQuery.OpenCaseOf` 同口径)。

**代价(已登记在实现注释里,不隐藏)**:该口径下 `CaseClosed` 的归属是**推断**而非**读取**;
与载荷解码**在合法流上等价**,二者**仅在违反「同一病人同时至多一个开案」(GDD 规则二)的非法流上分歧**
—— 而该流下**写者行为本身未定义** ⇒ 分歧面**不在契约内**。

**② 契约取「整条全序流」,不取「该案子集」。**
原计划写「统一为『该案子集』」;但两函数签名都收整条 `caseEvents`,而 `Sim` 侧**没有**「按 `case_id` 切片」
的合法手段(同 ① 的引用集约束),调用方也无从构造该子集。⇒ 统一口径改取
**「入参 = 病例流全序前缀」**,由同一扫描派生两值,**结构上不可再分叉**。

> 两处订正均属「**计划写了一个做不到的形状**」,**非范围缩减**。

### 2.3 测试(`case_close_precondition_test.cs`,33 → 35 测)

1. **B-1 回归哨兵** `test_isClosed_revisitNewCase_notClosed` —— 含**反向对照**(首案自身须仍判已结),
   防「一律返回 false」的假修;
2. **端到端组装用例** `test_e2e_revisitClosePath_deriveBothFlagsFromStreams` —— 补补评 **A-3**:
   此前全文件**无一条**把**两条流**(病例流 + 病史流)接进 `Decide`,这正是 B-1 在测试面
   **结构性隐形**的根因;
3. 解除 **4 处硬编码状态标志**(`:133` / `:146` / `:162` / `:510`)改为**由流派生** —— A-3 / A-6 的假绿土壤。

---

## 三、验证命令(可证伪 · 逐字可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
U=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity

# ── ① fixture(红 → 绿)───────────────────────────────────────────────
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter DaYiJingCheng.Tests.CaseSystem.CaseClosePreconditionTest \
  -testResults "$PWD/unity/Logs/s11_s8_after.txt" -logFile unity/Logs/s11_s8_after.log

# ── ② 全量双平台(零回归)─────────────────────────────────────────────
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testResults "$PWD/unity/Logs/s11_s8_editmode.xml" -logFile unity/Logs/s11_s8_editmode.log
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform PlayMode \
  -testResults "$PWD/unity/Logs/s11_s8_playmode.xml" -logFile unity/Logs/s11_s8_playmode.log

# ── ③ 改回旧实现即转红(sprint-11 DoD 明写要求)──────────────────────
cp unity/Assets/Sim/CaseCloseDecider.cs /tmp/s11_s8_fixed.cs
git show HEAD:unity/Assets/Sim/CaseCloseDecider.cs > unity/Assets/Sim/CaseCloseDecider.cs
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter DaYiJingCheng.Tests.CaseSystem.CaseClosePreconditionTest \
  -testResults "$PWD/unity/Logs/s11_s8_revert.xml" -logFile unity/Logs/s11_s8_revert.log
cp /tmp/s11_s8_fixed.cs unity/Assets/Sim/CaseCloseDecider.cs   # 还原;diff -q 须一致
```

判决口径 = **退出码 0 且 XML `failed="0"`** 双信号一致(sprint-11 DoD「退出码口径」条)。

---

## 四、结果

| # | 状态 | 退出码 | 结果 | 信号一致? |
|---|------|--------|------|-----------|
| ① | **修复前**(旧实现 + 新哨兵) | **2** | `total=34 passed=33 **failed=1**` | ✅ 双信号均红 |
| ② | **修复后** | **0** | `total=35 passed=**35** failed=0` | ✅ 双信号均绿 |
| ③ | **改回旧实现** | **2** | `total=35 passed=28 **failed=7**` | ✅ 双信号均红 |

### ① 修复前 —— 唯一红 = 我要证伪的那一条(**先证伪,后修**)

```
[Failed] test_isClosed_revisitNewCase_notClosed
  复诊新案(300,1,2)不得因首案的 CaseClosed(200,1,1) 被判为已结 ……(GDD :250-256 幂等定义 = **同一** case_id;EC-37-1 复诊 = 新病例)
  Expected: False
  But was:  True
```

### ③ 改回旧实现 —— **7 红,恰两类**(每处改动**各自承重、独立可证伪**)

**类 A —— B-1 粒度(4 条)**

```
test_ac3721_edge_revisitWithNewTreatmentInWindow_allows
  复诊窗口内有新处置 ⇒ 可结案(首案的 CaseClosed 不得外溢)
  Expected: CanClose      But was:  RejectAlreadyClosed     ← ★「复诊案结不掉」生产症状的本体
test_ac3721_revisitBackdoor_firstTreatmentNotInSecondWindow_rejects
  复诊案**未结** —— 首案的 CaseClosed 不得外溢到复诊案
  Expected: False         But was:  True
test_isClosed_revisitNewCase_notClosed
  Expected: False         But was:  True
test_e2e_revisitClosePath_deriveBothFlagsFromStreams
  复诊案**未结** —— 修前按 Patient 匹配 ⇒ 首案 CaseClosed 外溢 ⇒ 此处为 true
  Expected: False         But was:  True
```

**类 B —— `Decide` 判序(3 条)**

```
test_ac3703_alreadyClosed_idempotentReject        案件已结须幂等拒收(AC-37-03)   Expected: RejectAlreadyClosed  But was: RejectNotOpen
test_ac3703_edge_sameTick_doubleIntent_idempotent 同 tick 双意图 ⇒ 第二条幂等拒收  Expected: RejectAlreadyClosed  But was: RejectNotOpen
test_fullFlow_open_treat_check_close_idempotent   第二条 CaseClosed 幂等拒收       Expected: RejectAlreadyClosed  But was: RejectNotOpen
```

> ★ 类 A 第一条是**关键**:它把「复诊案**永远结不掉**」这一**生产症状**变成一条**可执行断言**
> —— 修前该测试的期望被静默顶替成 `RejectAlreadyClosed`,即补评 §二 所述
> 「AC-37-21 的 Edge 测试因 `:133` 硬编码 `isClosed:false` 而**假绿**」的**消解**。

### ④ 全量零回归

| 套件 | 结果 | 退出码 | 对比基线 |
|------|------|--------|----------|
| EditMode | `total=3330 passed=3284 failed=0 inconclusive=0 skipped=46` | **0** | 10-14 基线 `3326/3279/0/1/46` ⇒ **+4 测**;逐名 diff **零条消失** |
| PlayMode | `total=186 passed=186 failed=0` | **0** | `story009_review_playmode.xml` = `186/186/0` ⇒ **逐位相同(M2 链零回退)** |

**EditMode +4 的逐名归因**(`comm` 双向比对 `fullname` 集合,零条消失):

- `CaseSystem.CaseClosePreconditionTest.test_isClosed_revisitNewCase_notClosed` —— 本 story 新增(哨兵)
- `CaseSystem.CaseClosePreconditionTest.test_e2e_revisitClosePath_deriveBothFlagsFromStreams` —— 本 story 新增(A-3)
- `Boot.ExamineDriverTest.test_examine_twoPatients_ledgerIsolatedByPatientId` —— **已提交**(`47e0fc4`)
- `Boot.ExamineDriverTest.test_examine_twoPatients_bothExamined_snapshotsIndependent` —— **已提交**(`47e0fc4`)

`inconclusive 1 → 0`:基线中唯一 inconclusive 项在本轮转为 `passed`(逐名 diff 确认无测试消失)。

### ⑤ 静态面

`grep -c "error CS"` = **0**;编辑文件**无** CS1570 / CS1571 / CS1587(XML 注释与 `<param>` 无重复无未闭合)。
`Sim` 引用集**一字未动**(门 A / b2 / ADR-025 §① / ADR-029 全部继续成立)。

---

## 五、未清项(不因本件转绿)

- **story-003 维持 `In Review`** —— 补评 4 类中仅 ① B-1 闭合;② 快照半场(**写者未落 ⇒ 无载体可测**,
  承 §Out of Scope 延后登记)· ③ AC-37-18 恒真化(A-4)· ⑤ `TreatmentKinds` 收 `InjuryOnset`
  (**已排 S11-10**;枚举权威已于 2026-10-15 落 `adr-008` §四)+ ADVISORY A-1
  (`Set` 为可变 `public static readonly HashSet` —— **与 S11-10 同址**,应顺批收为不可写出口)· A-5 未清。
- **AC-37-03 不整条转判** —— 其**流层半边**(幂等判据)已由流派生夹具覆盖;而「**不产生新事件**」半边
  **仍无载体**(写者未落)⇒ 只覆盖到 `Decide` 的返回码,**未断言事件数 = 0**。
- **B-1 的实害面未消失** —— 修好的是**契约层**;复诊案能否**真的**结掉,仍取决于
  写者 story 落地后**消费** `IsClosed`。在写者落地前,§Out of Scope 的延后登记**继续有效**。
