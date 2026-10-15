# 修复后评审原件 —— case-system story-004(图样检测 · PatternRecognized)

**评审时点**:2026-10-15(**评当下的代码** —— 承 `.claude/docs/review-workflow.md` §义务 3 不可追补;
本件**不追认**任何旧判定,含 `8a693fd` 与补评件 `review-case-system-story-004-retro-2026-10-15.md`)
**被评对象**:`production/epics/case-system/story-004-pattern-detection-recognized.md`(Status: In Review)
**实现**:`unity/Assets/Sim/PatternDetector.cs`(产品代码)+ `unity/Assets/Sim/PatternRecognitionWriter.cs`(新增写者)
**测试**:`unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs`(**34 测**)
· `unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs`(9 测)
**权威件**:`design/gdd/case-system.md` F-37.1 / F-37.1b / AC-37-27(:1142)· `design/registry/entities.yaml:1944-1955`(ADR-024 §① 单一真源)· ADR-008 §三/§五
**性质**:补评件的**修复后复核** —— 补评判 **Test Evidence 门不通过**(代码面 3 + 测试面 5 BLOCKING,去重后核心 4 类)
**硬约束遵守**:未跑 Unity / 未起 batch / 未改任何既有文件(仅新建本件)

---

## 一、原判定(逐条引补评原件)

> 出处:`production/qa/evidence/review-case-system-story-004-retro-2026-10-15.md`

| # | 原判定 | 面 |
|---|---|---|
| **CB-1 / TB-4** | **发射语义无生产载体(借绿)** —— `PatternRecognized` 写者未落(`WriterExistenceGate.cs:88` 具名豁免)· `IsFired` 零生产调用者;「第 4 例不重发」由一份**未写、未测**的调用方契约保证 | 代码+测试 |
| **CB-2 / TB-5** | **`ChainEligible` / `ScriptedChain` 全库 `.cs` 零命中** + **AC-37-29 零覆盖**;且 W-1 已于 2026-10-15 会签结案 ⇒ 免责前提当日失效 | 代码+测试 |
| **CB-3** | **`memberSet` 按 `Dictionary` 迭代序拼装**(`PatternDetector.cs:104`,违 AC-37-27)+ **扁平结构**无逐 D 归属 | 代码 |
| **TB-1** | **AC-37-27 定序判别力为零** —— 夹具**期望序 ≡ 插入序** ⇒ 删掉 `:121-126` 的 Sort,断言仍绿(可捕获性 = **不可捕获**) | 测试 |
| **TB-2** | **AC-37-09/10 成员身份与冻结内容不可证伪** —— 只断 `MemberSet.Count` ⇒ 取「后 3 例」的变异同样绿 | 测试 |
| **TB-3** | **AC-37-12 真空夹具** —— 只有 2 例(< 阈值)⇒ `Fired==false` 由**数据不足**蕴含 | 测试 |
| **TA-6** | **元规则** —— 全文件「输入序 == 期望序」⇒ 定序类断言**系统性**丧失判别力 | 测试 |

**去重后核心 4 类**:① AC-37-27 定序判别力(CB-3 + TB-1)· ② AC-37-09/10 成员冻结(TB-2)·
③ AC-37-29 零覆盖(CB-2 ≡ TB-5)· ④ 发射语义无生产载体(CB-1/TB-4)。
**原门判定:不通过。**

**另附四条**(本批声称已处理):`A12 = CB-3`(**产品代码**改动)· `TB-3`(AC-37-12 真空)· `TA-6`(元规则)·
**⑥** GDD F-37.1b「先触发歧义」注块由「归 37 实现轮」改为「已闭合」。
**另有一处越权改动**:曾给 `SortBySaltedKey` 加 **ordinal 次级键**(引据失实),按用户裁定**已回退** —— 须核实回退干净。

---

## 二、修复落点(逐条对 · 我独立读文件确认,非采信自述)

| 原判定 | 修复落点 | 我的确认 |
|---|---|---|
| **TB-1** | 夹具改**插入序 ≠ 期望序**:`test_ac3727_..._saltedKeyOrderNotInsertionOrder`(病人 1–3→D2 **先**入、4–6→D1 **后**入,期望 `[1,2]`)+ `..._permutedInsertionOrder_identicalSequence`(两流插入序相反,断**逐位相同**)+ 新增 `..._saltedOrderIsNotOrdinalOrder`(D∈{0,1,5} ⇒ 期望 `[5,0,1]` ≠ ordinal `[0,1,5]`)+ `test_ac3713` 提取器改返 `[2,1]` | ✅ 见 §四-A |
| **CB-3** | **产品代码**:`PatternDetectionResult.MemberSet` 字段**删除**;`Evaluate` 里拼 `memberSet` 的 `foreach (var kvp in candidateSeq)` 循环**删除**;结构收窄为 `{ Fired, FiredDiseases }` | ✅ 见 §四-B |
| **TB-2** | 断言改**逐位内容 + 次序**:AC-37-09 用逐 tick 递增 4 例(前 3 ≠ 后 3);AC-37-10 用**同 tick**(流内序 ≠ 全序) | ✅ 见 §四-C |
| **TB-3** | AC-37-12 改**恰达阈值数**(3 例纯伤情)+ 新增混合夹具(2 真 + 3 伤情) | ✅ 见 §四-D |
| **TA-6** | 测试文件头(`:21-47`)写入**元规则** + 逐用例清单(含可捕获变异) | ✅ 见 §四-E |
| **CB-1 / TB-4** | 用户裁定「解冻写者」⇒ 新增 `unity/Assets/Sim/PatternRecognitionWriter.cs`(199 行)+ `pattern_recognition_writer_test.cs`(9 测);`WriterExistenceGate.cs` **撤两条豁免** | ✅ 见 §四-F |
| **CB-2 / TB-5** | S11-9 交付 `ScriptedChain` / `ChainEligible` + 7 条夹具 | ✅ 见 §四-G |
| **⑥** | `design/gdd/case-system.md:649` 注块改「~~归 37 实现轮~~ → ✅ 2026-10-15 已闭合」;裁法实现 = `ComputeCandidateSeq` 第 **3b** 步 | ✅ 见 §四-H |
| **越权回退** | `SortBySaltedKey` 回退为仅 `saltedA.CompareTo(saltedB)` | ✅ 见 §四-I |

---

## 三、验证命令(可复跑 · 可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ── A. TB-1 判别力:期望序 ≠ 插入序(独立复刻 SplitMix64,golden 自校验随行)──
python3 -I - <<'PY'
M=(1<<64)-1
def av(z):
    z=((z^(z>>30))*0xBF58476D1CE4E5B9)&M; z=((z^(z>>27))*0x94D049BB133111EB)&M; return (z^(z>>31))&M
def fold(s,w): return av((s+w)&M)
def ftag(s,t):
    b=t.encode(); s=fold(s,len(b))
    for i in range(0,len(b),8):
        n=min(8,len(b)-i); c=0
        for j in range(8): c=((c<<8)|(b[i+j] if j<n else 0))&M
        s=fold(s,c)
    return s
def h(a,t,b): return av(fold(ftag(a&M,t),b&M))
assert h(0xABCD,"case-salt",3)==0x9104567A4D5A819C, "golden mismatch"
W=12345
print("salted(1)=%#x  salted(2)=%#x"%(h(W,'case-salt',1),h(W,'case-salt',2)))
print("salted(5)=%#x  salted(0)=%#x  salted(1)=%#x"%(h(W,'case-salt',5),h(W,'case-salt',0),h(W,'case-salt',1)))
PY
#    期望:salted(1) < salted(2)(⇒ 夹具 1 期望序 [1,2] ≠ 插入序 [2,1] ⇒ 删 Sort 必红)
#          salted(5) < salted(0) < salted(1)(⇒ 夹具 2 期望序 [5,0,1] ≠ ordinal 序 [0,1,5])

# ── B. CB-3:扁平字段确已删,且无生产消费者 ──
grep -rn "\.MemberSet" unity/Assets --include=*.cs
#    期望:仅 PatternEmission.MemberSet(定义在 PatternDetector.cs:152)+ 写者 PatternRecognitionWriter.cs:175
#          + 测试件;PatternDetectionResult 无 MemberSet 字段
grep -rn "class PatternDetectionResult" -A8 unity/Assets/Sim/PatternDetector.cs

# ── C. 写者存在性 + 豁免已撤 ──
ls -la unity/Assets/Sim/PatternRecognitionWriter.cs
grep -n '("PatternRecognized"\|("CaseClosed"' unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs   # → 空

# ── D. SortBySaltedKey 回退干净(与 HEAD 逐字一致)──
git diff HEAD -- unity/Assets/Sim/PatternDetector.cs | grep -n "saltedA\|saltedB\|SortBySaltedKey"
#    期望:只命中注释行,函数体无 hunk ⇒ 未改动
grep -rn "ordinal 次级\|次级键\|次级定序\|全序,非偏序" unity/Assets --include=*.cs   # → 空(exit 1)

# ── E. ⑥ GDD 注块 ──
grep -n "先触发歧义" design/gdd/case-system.md
#    期望:649: ... ~~归 37 实现轮~~ → ✅ 2026-10-15 已闭合

# ── F. 测数 ──
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs          # → 34
grep -c '\[Test\]' unity/Assets/Tests/EditMode/CaseSystem/pattern_recognition_writer_test.cs      # → 9

# ── G. 全量门(⚠️ 主轮已跑;本件只解析其落盘 XML,不重跑)──
python3 -I -c "
import re
for f in ['unity/Logs/s11-final-EditMode.xml','unity/Logs/s11-final-PlayMode.xml']:
    x=open(f,encoding='utf-8',errors='replace').read()
    print(f, re.search(r'total=\"\d+\" passed=\"\d+\" failed=\"\d+\" inconclusive=\"\d+\" skipped=\"\d+\"',x).group(0))
"
```

### 本件实跑输出(我亲自执行的)

```text
# A. SplitMix64 独立复刻(含 golden 自校验 assert 通过)
salted(1)=0x8eb85fea2ccdbf7c  salted(2)=0xd3f9ecb60053aff4
salted(5)=0x405cc4fe863fce06  salted(0)=0x74180702d186d1d6  salted(1)=0x8eb85fea2ccdbf7c
⇒ 夹具1: 插入序=[2,1] ≠ 期望序=[1,2]  ⇒ 判别力成立
⇒ 夹具2: salted(5)<salted(0)<salted(1) ⇒ 期望序 [5,0,1] ≠ ordinal [0,1,5] ⇒ 判别力成立

# B. .MemberSet 引用(全仓) —— PatternDetectionResult 已无该字段
unity/Assets/Sim/PatternRecognitionWriter.cs:175:  var members = emission.MemberSet;      ← PatternEmission(逐 D)
unity/Assets/Tests/.../case_pattern_detection_test.cs:{160,205,230,319,382,605,840,846,847,873,921,923,959}
(无 PatternDetectionResult.MemberSet)

# D. 回退残留扫描 → 空(exit 1);SortBySaltedKey 函数体不在 diff 中

# E. GDD:649: > - **⚠️ 先触发歧义(登记为澄清项 ~~归 37 实现轮~~ → ✅ 2026-10-15 已闭合)**:

# F. [Test] 计数:34 / 9

# G. 主轮落盘 XML(我解析,非我执行)
unity/Logs/s11-final-EditMode.xml  total="3387" passed="3341" failed="0" inconclusive="0" skipped="46"
unity/Logs/s11-final-PlayMode.xml  total="186"  passed="186"  failed="0" inconclusive="0" skipped="0"
PatternDetectionTest 用例数: 34,非 Passed: 无(全 Passed)
PatternRecognitionWriterTest 用例数: 9,非 Passed: 无(全 Passed)
```

> ⚠️ **来源标注**:全量 `3387/3341/0/46` · `186/186/0` 为**主轮执行**(落盘 `unity/Logs/s11-final-*.xml`,
> 2026-10-15 12:54/12:55),与任务简报给的数字一致 —— **本件未重跑 Unity**(硬约束:主轮正跑最终全量)。
> 本件**亲自执行**的是:python 复刻(A)、全部 grep 静态核验(B/D/E/F)、以及**解析主轮 XML**(G)。

---

## 四、独立复核结论(逐条)

### A. TB-1 —— AC-37-27 定序判别力 **CLOSED**

**任务问:删掉 `SortBySaltedKey` 后哪条断言会红?** 答案(逐条给出,非泛泛):
1. `test_ac3727_sameTickMultipleDiseases_saltedKeyOrderNotInsertionOrder` `:428` `Is.EqualTo(new[]{1,2})` —— 删排序后得插入序 `[2,1]` ⇒ **红**;
2. `test_ac3727_permutedInsertionOrder_identicalSequence` `:472`(流 B)`Is.EqualTo(new[]{1,2})` ⇒ **红**;
3. `test_ac3713_comorbidCase_countedInBothGroups` `:339` `Is.EqualTo(new[]{1,2})`(提取器迭代序 `[2,1]`)⇒ **红**;
4. `test_ac3727_saltedOrderIsNotOrdinalOrder` `:520` `Is.EqualTo(new[]{5,0,1})` ⇒ **红**;
5. 写者侧 `pattern_recognition_writer_test.cs:190/192`(`SaltedKeyOf(fired[0]) < ...` + ordinal 期望)⇒ **红**。

**关键**:期望序是**字面量**(`[1,2]`),**不是**用实现自己的函数在断言时刻推导 —— 这正是补评件指出的原病灶
(「断言用的是实现自己的函数」)。且**判别力已由独立复刻坐实**:我用 python 逐位复刻 `SplitMix64.HashTagged`
(带 `golden_hash_v1_test.cs` 的 `"case-salt"` 黄金向量自校验 `h(0xABCD,"case-salt",3)==0x9104567A4D5A819C` 通过),
算出 `salted(1)=0x8eb8… < salted(2)=0xd3f9…`,与夹具注释、夹具前提自检(`:409-411`)**逐位一致**。
⇒ 「插入序 `[2,1]` ≠ 期望序 `[1,2]`」**成立**,删排序**必红**。**判别力真建立,不是只有一条自检断言。**
另:`test_ac3727_saltedOrderIsNotOrdinalOrder` 补上了原修法**证不到**的那半边(定序**键**是 `salted_key` 而非 `ordinal`)——
我实测 `salted(5)<salted(0)<salted(1)` 使 `[5,0,1] ≠ [0,1,5]`,该补强有效。

### B. CB-3 —— 删除是否安全 **CLOSED**

**全仓 grep `.MemberSet`**:命中仅三类 ——
① `PatternEmission.MemberSet`(定义 `PatternDetector.cs:152`,**仍在且正确**,逐 D);
② 写者 `PatternRecognitionWriter.cs:175`(读 `emission.MemberSet`,**是 `PatternEmission` 的**);
③ 测试件。
**`PatternDetectionResult.MemberSet` 字段确已删**(`PatternDetector.cs:47-59` 结构体只剩 `Fired` + `FiredDiseases`),
`Evaluate` 的 `foreach (var kvp in candidateSeq)` 拼装循环**已删**(diff 确证:`- var memberSet = new List<CaseId>();` … `- return new PatternDetectionResult(..., memberSet);` 全为删除行)。
⇒ **全仓确无其他消费者**;`PatternEmission.MemberSet` 与 `PatternDetectionResult` 是**两个**结构,前者未受影响。**安全。**

> 旁证:registry `entities.yaml:1954-1955` 要求 `member_set` **逐事件**携带冻结三元组 ⇒ 删除扁平字段**符合**真源要求。

### C. TB-2 —— 是否真断内容 **CLOSED**

**真断逐位内容 + 次序**,不是换个方式断 Count:
- AC-37-09 `:160-163`:`Is.EqualTo(new[]{ CaseId(100,1,0), CaseId(200,2,0), CaseId(300,3,0) })` —— 4 例逐 tick 递增 ⇒ 「前 3」≠「后 3」`{200,2,0 / 300,3,0 / 400,4,0}`,「取后 3」变异**必红**;
- AC-37-10 `:205-208`:`Is.EqualTo(new[]{ CaseId(100,1,0), CaseId(100,2,0), CaseId(100,3,0) })` —— 同 tick,流内序 `(3,1,2)` ≠ 全序 `(1,2,3)`;
- `CaseId` 是 `IEquatable<CaseId>` 值类型(`PayloadCommon.cs:59-79`,`Equals` 逐字段 + `==` 运算符)⇒ `Is.EqualTo` 做**元素级值比较、次序敏感**。**断言有效。**

### D. TB-3 —— 是否还空跑 **CLOSED**

**任务问:AC-37-12 夹具的内层 foreach 现在迭代几次?**
答:`ComputeCandidateSeq` 第 2 步的 `foreach (int diseaseOrdinal in diseaseExtractor(e))`(`PatternDetector.cs:314`)
对 `ExtractEmptyDisease`(恒返 `new int[0]`)**仍迭代 0 次** —— 但这是**正确行为**(空集 = 不进任何病种组),
且**不再构成真空**:
- 原夹具 2 例(< 阈值)⇒ 即便伤情案被计入,也因不足阈值而不触发 ⇒ `Fired==false` 由**数据不足**蕴含(真空);
- 新夹具 **3 例(= 阈值)** `:270-282` ⇒ 若伤情案被计入任一病种组,**计数即达阈值 ⇒ 转红**;
- 新增混合夹具 `:291-323`(3 伤情 + 2 真 ⇒ 不触发;补第 3 真 ⇒ 触发且成员只含真病例)。
⇒ 对变异「空 `disease_set` 也计入某组」(M4)有判别力。**真空性已消除。**

### E. TA-6 —— 元规则是否真落地 **CLOSED**

测试文件头 `:21-47` 写入**元规则**(「定序类 AC 的夹具必须至少一处**输入序 ≠ 期望序**」;判别力判据固定为
「**把对应实现删掉/改错 ⇒ 该测试转红**」)+ **逐用例清单**(7 条,逐条给出可捕获的变异)。我逐条核了清单里
的 5 条定序用例,断言形态与所述变异**相符**(见 A)。**元规则真落地。**

### F. CB-1 / TB-4 —— 发射语义生产载体 **CLOSED**

- **写者已落**:`unity/Assets/Sim/PatternRecognitionWriter.cs`(199 行,`TryRecognize` 复用 `PatternDetector.NextEmissions`,
  **不重写检测、不自行排序**;载荷 `AnchorCase = MemberSet[末位]` / `SaltedKey = emission.SaltedKey` /
  `MemberDiseaseSet = {D}` / `PatientId = -1` 哨兵;`Append` 落病例流);
- **9 测为真装配**:`CompositionRoot.Assemble()`(**真** `EventStream` + **真** `PayloadCodec` + **真** blob 池),
  经 `PayloadCodec.TryGetPayload<PatternRecognizedPayload>` **回读** `SaltedKey` / `AnchorCase` / `MemberDiseaseSet` / `Version`
  ⇒ 这正是补评件 **TA-2** 指出的「真载体应是载荷经 codec 解码」的形态;
- **fires-once 有真生产载体**:`test_patternRecognized_secondCall_firesOnce_noResend` —— 二次调用返 `AlreadyRecognized`、流内仍只 1 条;
- **豁免已撤**:`WriterExistenceGate.cs` 两条具名豁免确已删(`:87-90` 留痕注释)。

### G. CB-2 / TB-5 —— AC-37-29 **CLOSED**

`ScriptedChain`(`PatternDetector.cs:76-89`)/ `ChainEligible.IsEligible` / `TryChainOf`(`:94-143`)已落;
7 条夹具(`:768-963`)含名额不被消耗(带反向对照)、链成员专属成员集、一次性(带两层边界对照)、声明序不消费、逐 D 二选一。
**S11-9 交付已在主轮全量中 34/34 全绿(我解析 XML 确认)。**

### H. ⑥ GDD 订正 **CLOSED**

`case-system.md:649` 已由「~~归 37 实现轮~~」改为「**✅ 2026-10-15 已闭合**」,并写明落地载体。
**该裁法确已实现**:`ComputeCandidateSeq` 第 **3b** 步(`PatternDetector.cs:351-367`)—— 命中链的病种
`candidates = narrowed`(只留链成员),未命中链的维持原算式(`ChainEligible.TryChainOf` / `IsEligible`)。**与 GDD 注块所述一致。**

### I. 越权改动回退是否干净 **CLOSED(干净)**

- `SortBySaltedKey`(`PatternDetector.cs:378-386`)现在**只有** `return saltedA.CompareTo(saltedB);` —— **无**次级键;
- `git diff HEAD` 中该函数体**无任何 hunk**(只命中注释行)⇒ 与 HEAD **逐字一致**;
- 全仓 grep `ordinal 次级|次级键|次级定序|全序,非偏序` ⇒ **空(exit 1)**,**无失实引据残留**。

### J. 是否有新的借绿 **未发现新的 BLOCKING 级借绿**(两处残余见 §六)

逐项排查:34 条夹具无 `Assert.Pass()` / 无硬编码 `true` 桩;AC-37-34 / AC-37-29 的去重与收窄判别力真实;
写者 9 测为真装配(非 Fake)。**两处值得登记的残余**(非 BLOCKING,见 §六 ①②)。

---

## 五、Test Evidence 门判定

# **通过**

**理由**(逐条对补评件的核心 4 类 + 4 条附加):
1. **AC-37-27 定序判别力** —— 夹具期望序 ≠ 插入序(我用独立复刻坐实),删排序**必红**(能说出具体哪条断言);
   产品代码 CB-3 的 Dictionary 迭代序违规点**已删**,消费者调查确认安全。**CLOSED**
2. **AC-37-09/10 成员身份与冻结** —— 断言改逐位内容 + 次序,`CaseId` 值语义支持元素级比较。**CLOSED**
3. **AC-37-29 零覆盖** —— S11-9 交付实现 + 7 夹具,全绿。**CLOSED**
4. **发射语义无生产载体** —— 写者已落 + 9 测真装配 + 豁免已撤。**CLOSED**
5. **TB-3 / TA-6 / ⑥ / 越权回退** —— 四条附加全部 **CLOSED**。

**Story Type = Logic** ⇒ 必需证据 = 自动化单元测试通过。`case_pattern_detection_test.cs` **34/34 绿**
(主轮 XML,我解析确认),且**每条 BLOCKING AC 现均有可证伪的判别力**(非「绿即通过」)。
⇒ **门通过**,story-004 的 Test Evidence 面**不再有 BLOCKING 缺口**。

---

## 六、未闭项 / 不确定(如实 · 禁借绿)

### ① 未处理的 ADVISORY(补评件登记 5 项,fix-s11-15 §六.2 自陈「不在本任务清单」)

| ID | 内容 | 现状(我核实) |
|---|---|---|
| **TA-1** | AC-37-11 夹具与 AC-37-08 **逐字重复**(`:240-257` 仍用 `ExtractDisease` 3 例) | **未动**,仍弱 |
| **TA-2** | `IsFired` 三用例把 `salted_key` **塞进 `SimEvent.Seq`** 自造载体(`ExtractSaltedKeyFromSeq`);真载体应是 `PatternRecognizedPayload.SaltedKey` 经 codec 解码 | **未动**。⚠️ 但**新增的写者测试已用真载体**(`PayloadCodec.TryGetPayload`)⇒ 该风险**已由另一文件部分覆盖**,非无覆盖 |
| **TA-5** | AC-37-14 负断言按**字段名**判(`GetField("DiseaseId")`),防不住「换名携带」 | **未动** |
| **TA-7** | AC-37-34「第 4 例**同 tick**」边界未覆盖 | **已部分缓解** —— 新增 `test_ac3710_sameTickFourCases_memberFreezeDropsFourth` 覆盖了同 tick 4 例边界 |
| **TA-8** | Impl Note 5 要求阈值「2/3 两档」;`PATTERN_THRESHOLD ≥ 2 构建拒` 仅注释 | **未动**。我实测:全文件只用 `Threshold = 3`;写者 ctor 只校验 `patternThreshold < 1` **抛**(`PatternRecognitionWriter.cs:123`)⇒ Guardrail 的「**≥2**」**未强制** |

### ② 我**新发现**的问题

1. **story §Test Evidence Status 行陈旧**(`story:116`):仍写「**30/30 绿**」,而测试件现为 **34 测**;
   同一 story 的 `:195` 却写「30 → 34」。**同一文件内自相矛盾**(文档陈旧,非门缺口)。
2. **测试件注释陈旧**(`case_pattern_detection_test.cs:165-166`):`test_ac3709` 步骤 ② 注释写
   「**写者未落**,此处由夹具模拟同一契约」—— 而写者**已落**。该注释**误导读者的当下判断**。
3. **元规则清单的措辞略松**:文件头把 `test_ac3709` 列在「**满足该纪律**的用例」下,但其夹具**输入序 == 期望序**
   (严格说它满足的是**另一条轴** ——「前 3 vs 后 3」)。文件头后半句已披露「逐 tick 递增夹具结构上不可能满足该纪律」,
   **故非隐瞒**,但清单首句措辞与所列项不完全对齐。
4. **`SortBySaltedKey` 用 `List.Sort`(不稳定排序)**:64 位哈希碰撞时次序**未定义**,与 AC-37-27 后半句
   「换一个插入序重跑,事件流逐位相同」有**理论张力**。**是否需次级键是设计决策**(fix-s11-15 §五已登记为待裁,
   本轮**未自行加** —— 引据失实的越权改动已回退)。**我确认当前无次级键**(见 §四-I)。归**待裁项**。
5. **写者 `anchor_case = MemberSet[末位]` 在「同 tick 批量跨阈值」下的语义**:ADR-008 §三 定义 `anchor_case` =
   「触发识别的那一例」。在流的全序键 `(Tick, StreamPriority, Patient, Seq)` 下,同 tick 内按 `Patient` 升序
   ⇒ 全序末位 ≡ 流内末位 ≡ 跨阈值那一例 ⇒ **当前实现与定义一致**。**但**写者测试**无同 tick 批量跨阈值**的夹具
   (9 测均逐 tick 递增),该语义**未被直接证伪过**。低置信度、非阻塞,**登记待补夹具**。

### ③ 我不确定的(不冒充已决)

- **全量数字的独立性**:`3387/3341/0/46` · `186/186/0` 出自**主轮**落盘 XML;我**解析**确认而非**重跑**。
  若主轮 XML 与当前工作树不同步(理论上可能),则数字需重跑核验 —— 但 `s11-final-*.xml` 时间戳(12:54/12:55)
  晚于全部修复件,且 `PatternDetectionTest` 34 条逐条 `Passed` 与该树一致,**可信度高**。
- **变异探针 M1–M6** 是**独立复刻 + 直接调生产**,**不是 UTF 执行**(fix-s11-15 §三自陈)。我**未**独立重跑探针
  (禁跑 Unity);但 §四-A 我用**独立复刻 + 全仓静态核验**独立证了 TB-1 的判别力,**不依赖**探针自述。

---

## 七、合取判定

| 原判定 | 现状态 |
|---|---|
| ① AC-37-27 定序判别力为零(CB-3 + TB-1) | **CLOSED** |
| ② AC-37-09/10 成员冻结不可证伪(TB-2) | **CLOSED** |
| ③ AC-37-29 零覆盖(CB-2 ≡ TB-5) | **CLOSED** |
| ④ 发射语义无生产载体(CB-1 / TB-4) | **CLOSED** |
| A12 = CB-3(产品代码) | **CLOSED** |
| TB-3(AC-37-12 真空) | **CLOSED** |
| TA-6(元规则) | **CLOSED** |
| ⑥(GDD 注块) | **CLOSED** |
| 越权改动回退 | **CLOSED(干净)** |
| 4–5 条 ADVISORY(TA-1/2/5/8 + TA-7 部分) | **STILL-OPEN(非阻塞)** |

# **Test Evidence 门:通过。**

**残留(不阻塞门)**:5 条 ADVISORY · story `:116` 与测试件 `:165` 两处**陈旧注释** ·
`List.Sort` 不稳定排序待裁 · 写者同 tick 批量 anchor 语义未补夹具。

---

**签署**:本件由评审轮落盘,评 2026-10-15 当下的代码与测试;
全量数字**引自主轮**落盘 XML(已标注来源),静态与复刻核验**由本件独立执行**。
