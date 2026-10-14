# 补做评审原件 —— case-system story-004(图样检测 · PatternRecognized)

**评审时点**:2026-10-15(**补做评审** —— 评当下代码,承 `.claude/docs/review-workflow.md` §义务 3 不可追补)
**被评对象**:`production/epics/case-system/story-004-pattern-detection-recognized.md`(Status: Complete @ `8a693fd`,2026-10-06)
**实现**:`unity/Assets/Sim/PatternDetector.cs`
**测试**:`unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs`(23 测)
**权威件**:`design/gdd/case-system.md` F-37.1 / F-37.1b / F-37.3 / EC-37-1(`:1062-1073` AC 表)· `entities.yaml:1944-1957`(ADR-024 §① 单一真源)· ADR-008 §五
**轮次**:双代理(unity-specialist 代码面 + qa-lead 测试面)
**原件缺口确证**:`review-case-system-001-004-gap-audit-2026-10-15.md` §二(004 判定分级未能确证)
**commit 自述(`8a693fd`)的处置**:一律作**待验证主张**,本件独立判定

> **两面判定**:代码面 **3 BLOCKING** + 测试面 **5 BLOCKING** ⇒ **去重后核心 4 类**(见 §三 合取判定)。
> **共同病灶**:`PatternDetector` 的**读出侧**(`Fired` / `MemberSet` / `IsFired`)在生产中零消费者,而 AC 的语义层(发射一次 / 冻结内容 / 不重发)**不由实现保证**,由一份**未写、未测**的调用方契约保证。

---

## 一、逐条 AC 判定(两面并列)

| AC | 代码面 | 测试面 | 综合 |
|---|---|---|---|
| **AC-37-07** 长度 < 阈值不触发 | ✅ | ✅(`:62-73`,与 `:75` 成对夹住 `>=` 边界) | ✅ |
| **AC-37-08** 恰达标发**一次**(哨兵 −1) | ⚠️ | ✅ 弱·半条(`:75-89`)—— 「世界级聚合事件 + 哨兵 −1」**未断言**:`PatternDetectionResult` 不含 patient 字段,该层不可测 | ⚠️ |
| **AC-37-09** 第 4 例不重发 + `MemberSet` 冻结于前 3 | ❌ 借绿(无真判据) | ❌(`:91-106`)—— `:104` 断的是 `Fired==true`(**注释自写「第 4 例仍触发」,与 AC 相反**);`Fired` 是**前缀谓词**(`PatternDetector.cs:128`),不表达「本 tick 新发射几条」;「冻结于前 3」退化为只断 `Count==3` ⇒ 取「后 3 例」的变异同样计数 3 | ❌ |
| **AC-37-10** 同 tick 三例 ⇒ 一次 + 成员取全序前 3 | ⚠️ | ❌(`:112-127`)—— 两次断言均不检查成员身份;夹具输入序 `p1,p2,p3` 恰等于期望全序 ⇒「全序键含 `Patient`」**无判别力** | ❌ |
| **AC-37-11** 三互异 `patient_id` ⇒ 照样触发 | ✅ | ✅ 弱(`:133-150`)—— 与 AC-37-08 夹具**逐字重复**;病名轴在 `Func<SimEvent,int[]>` 接口面**结构性不可读** ⇒ 该子句由签名保证,不由本测试证明 | ✅(弱) |
| **AC-37-12** 纯伤情案(`disease_set` 空)不进任何计数 | ✅ | ❌ **真空真**(`:156-168`)—— 夹具只有 2 例且 `ExtractEmptyDisease` 恒返空集 ⇒ 内层 `foreach`(`PatternDetector.cs:62`)**零迭代**;`Fired==false` 由夹具**蕴含** | ❌ |
| **AC-37-13** 共病一案进两组各计一次 | ✅ | ✅(`:170-185`) | ✅ |
| **AC-37-14** 载荷含 `salted_key`、不含裸 `disease_id` | ✅ | ✅ 结构面(`:273-292`);⚠️ **值面不可测**(无写者 ⇒ 无「写进去的键 = 派生键」证据) | ✅ |
| **AC-37-22** 同 `patient_id` 三例 ⇒ 长度 1 不触发 | ✅ | ✅(`:191-204` A,B,A + `:355-366`)—— AC 原文夹具(A,A,A)未出现,但 **A,B,A 是更强夹具**(GDD `:452-465` 明指其正是新旧算式分叉点) | ✅ |
| **AC-37-27** 同 tick 多 D 按 `salted_key` 升序(禁集合迭代序)+ 换插入序逐位同 | ❌ `memberSet` 按 Dictionary 迭代序 | ❌ **不可捕获变异**(`:234-267`,见 B-1) | ❌ |
| **AC-37-29** 脚本链名额不被随机病例消耗 + 一次性 | ❌ `ChainEligible`/`ScriptedChain` **全库 .cs 零命中** | ❌ **NOT-RUN 零覆盖**(见 B-5) | ❌ |
| **AC-37-34** A、B、A ⇒ 长度 2 不触发;补 C 后触发且 `MemberSet={A₁,B₁,C₁}` | ✅ | ✅ **全文件最强**(`:206-228`,`Is.EquivalentTo` 断**成员内容**) | ✅ |
| 性质 2「无时间窗口下界」 | — | ✅(`:368-381`) | ✅ |
| Impl Note 5「阈值符号化小值 2/3 **两档**」 | — | ❌ NOT-RUN(全文件只用 `Threshold = 3`) | ❌ |

---

## 二、判定清单

### 代码面 BLOCKING(3)

**[CB-1 · 借绿] 一次性门 / 哨兵发事件无真判据**
- `Evaluate` **从不读** `PatternRecognized`(无「已发过不再发」判据);`IsFired`(`:162-175`)是 fires-once 的**唯一**守卫,而**零生产调用者**;
- 测试断言 `Fired==true`(前缀属性)与 AC 语义(「恰发一次」)**相反**;
- ⇒ AC-37-08 / AC-37-09 的发射语义在本 story 内无载体。

**[CB-2 · 必交缺失] `ChainEligible` / `ScriptedChain` 全库 `.cs` 零命中**
- AC-37-29 在 story `:40` 登记「非 ScriptedChain 半边……**谓词实现先落**(`ChainEligible` 判定域),夹具待 9+37 会签后补」;
- 实测 `grep -rn "ChainEligible\|ScriptedChain" unity/Assets --include=*.cs` ⇒ **零命中**(唯二命中:`case-system.md:588` 定义、story `:40`);
- ⇒ **连「先落」的那半边也没落**,而这半边**从未被 W-1 阻塞**。

**[CB-3 · 结构性] `memberSet` 按 Dictionary 迭代序拼装 + 扁平结构**
- `PatternDetector.cs:104` `foreach (var kvp in candidateSeq)` —— 顺序取决于 `Dictionary` 枚举序 ⇒ 违 AC-37-27 的「禁集合迭代序」;
- `PatternDetectionResult.MemberSet` 是**扁平** `IReadOnlyList<CaseId>`(`:23`),多 D 触发时(AC-37-13/27)6 项**无病种归属**,调用方无法按 D 切出各 `PatternRecognized` 的 `member_set` —— 而 registry `:1954` 要求**逐事件**携带。

### 测试面 BLOCKING(5)

**[TB-1 · 假绿 · 可捕获性判定 = 不可捕获] AC-37-27 的断言对其唯一守护的变异恒真**
- 实现 `PatternDetector.cs:120-126` 是唯一定序步骤(`firedDiseases.Sort` by `SplitMix64.HashTagged(worldSeed,"case-salt",D)`);
- 测试 `:251-255` 病种映射:patient ≤3 → `{1}`(**先**入 `Dictionary`),patient ≥4 → `{2}`(后入)⇒ 迭代序 = `[1,2]`;
- 测试 `:261-266` 断言 `ComputeSaltedKey(D[i]) < ComputeSaltedKey(D[i+1])`,**用的是实现自己的函数**;
- **代理按 `SplitMix64.cs:31-100` 逐位复刻**(黄金自校验:`HashTagged(0xABCD,"case-salt",3) == 0x9104567A4D5A819C` 与 `golden_hash_v1_test.cs:103` 一致):
  `salted(W=12345,D=1) = 0x8EB85FEA2CCDBF7C` / `salted(W=12345,D=2) = 0xD3F9ECB60053AFF4` ⇒ **1 < 2**,期望序 `[1,2]`;
- **期望序 ≡ 插入序** ⇒ 变异「删掉 `:121-126` 的 Sort」产出 `[1,2]`,**断言仍绿**;该用例的绿**与排序机制无因果关系**(能否恰红取决于 `Dictionary` 枚举序是否等于插入序这一**实现细节**);
- 同时 AC 后半句「换一个插入序重跑、事件流逐位相同」**零覆盖**(测试只跑一次,从未置换 `disease_set` 构造序);
- **最小修法**:把夹具改成**期望序 ≠ 插入序**(先入 D₂、后入 D₁)⇒ 删排序的变异**必红**;再补一条「同一病例集用两种 `disease_set` 输出序各评一次 ⇒ `FiredDiseases` 序列全等」的置换用例。

**[TB-2 · 只断 `Count`] AC-37-09/10 的成员身份与冻结内容不可证伪**
- `:105` / `:126` 均为 `MemberSet.Count == Threshold`;`MemberSet` 由 `PatternDetector.cs:112-116` 按 `candidates[0..threshold)` 填 ⇒ 变异「取 `candidates[count-threshold..]`(后 3 例)」在 4 例夹具下**仍是 3**,两处全绿;
- 文件自己在 `:222-227` 用 `Is.EquivalentTo` + `Is.EqualTo(new CaseId(...))` 证明了**「断内容」是可做的**;
- ⇒ GDD `:1063/:1065` 的 AC 内容(「触发时刻的前 N 例」「全序前 3」)**无有效证据**。

**[TB-3 · 真空夹具] AC-37-12**(同 §一 表)。
缺锐利夹具(≥3 纯伤情案;或 2 真病例 + 3 伤情案混合 ⇒ 仍不触发)。且 `PatternDetectionResult` **不暴露计数**,「不进计数」只能经阈值间接观察 ⇒ 必须补混合夹具。

**[TB-4 · 借绿] AC-37-08/09 的发射语义无生产载体**
- `grep -rn "PatternDetector" unity/Assets --include=*.cs | grep -v /Tests/` ⇒ 非测试命中**只有** `Sim/PatternDetector.cs` 自身 + `WriterExistenceGate.cs:88` 的**豁免表条目文本**;
- `WriterExistenceGate.cs:88` 原文:`("PatternRecognized", "37 同源检测写者未落 —— PatternDetector 现为纯函数,盐派生 + 冻结三元组写入归 37 实现轮(2026-10-10)")` —— **该项目自己的门把它登记为未实现债**;
- `IsFired` **同样零生产调用者**;`grep "IsFired\|FiredDiseases\|MemberSet" unity/Assets/Tests` ⇒ **仅本测试文件命中**;
- `Evaluate` 返回**前缀属性**(`:108/:128`)⇒ 「第 4 例不重发」在实现里**不由 `PatternDetector` 保证**,由「调用方先问 `IsFired`」这一**未写、未测**的契约保证;
- ⚠️ **边界说明(须如实登记)**:写者未落是 `WriterExistenceGate` 里的**显式债务 + 归属轮**(2026-10-10),可视为有意延后;但 story-004 的 §Acceptance Criteria / §Test Evidence / §Out of Scope **均未登记该延后**(Out of Scope 只列 story-003/005/006 与 53),且 Implementation Note 3(`:51`)明写「先按 `salted_key` 升序再 **Append**」—— 即 story 自认含写入步。

**[TB-5 · 必交件缺失 + 阻塞理由已于今日消失] AC-37-29 零覆盖**
- 同 CB-2(`ChainEligible` 零命中);
- **阻塞理由已解除**:`case-system.md:610-627` 记 **「✅ 会签裁决(2026-10-15,S11-2)裁① = 三案各挂 `DIS_DYSENTERY`」**(user + game-designer 双签;HEAD `4565dc6`);
- 裁决同时明写「`AC-37-12` 原样不动(三案从此**不是**纯伤情案)」⇒ 脚本链 `CandidateSeq` 非空的机制路径已定,**夹具今天就是可写的**;
- ⇒ story `:40` 自认的义务未履行,其免责前提**当日失效**。

### 代码面 ADVISORY(6)

- **CA-1** `Evaluate` 阈值用 `>=` ✅(`:108`);`firedDiseases` 按 `salted_key` 排序 ✅(`:120-126`);`FirstPerPatient` ✅ —— 三处正面判定
- **CA-2** 无 `if (kind == injury)` 类硬编码分支 ✅ 
- **CA-3** 门 A ✅(`Sim.asmdef` `noEngineReferences: true`)
- **CA-4** `IsFired` 与 `Evaluate` 的**契约分离**未写明(谁负责「已发过不再发」)—— 是 TB-4/CB-1 的土壤
- **CA-5** `MemberSet` 扁平结构(见 CB-3)—— 与测试面 A4 同
- **CA-6** `PATTERN_THRESHOLD` 构建期门仅以注释存在(与测试面 A8 同)

### 测试面 ADVISORY(8)

- **TA-1(AC-37-11)** 用例 = AC-37-08 夹具逐字重复,注释 `:137-138` 自陈「模拟」。**建议**把 AC-37-11 判据改述为「接口面不含诊断输入」,或补反射断言「`Evaluate` 入参集不含任何诊断/病名字段」
- **TA-2(自造载体)** `IsFired` 三用例(`:389-432`)把 `salted_key` **塞进 `SimEvent.Seq`**(`:400/:427`)并传 `e => (ulong)e.Seq` 作提取器 —— 真载体应是 `PatternRecognizedPayload.SaltedKey`(`CasePayloads.cs:45`)经 codec 解码。**载荷若把 `SaltedKey` 与 `MemberDiseaseSet` 写错位,这三条全绿**。另 `:392-395` 的 `caseEvents` 是**死变量**
- **TA-3** `ExtractDiseaseByPatient`(`:55`)**全文件零引用** —— 死辅助
- **TA-4(结果类型)** 同 CB-3
- **TA-5(扫错对象·窄)** AC-37-14 负断言按**字段名**判(`:288/:290` `GetField("DiseaseId")`/`GetField("DiseaseName")`);载荷按 registry `:1954` 含 `MemberDiseaseSet: DiseaseIdSet` + `AnchorCase` ⇒ 「不含裸 `disease_id`」仅在同名读法下成立,**防不住「换名携带」**
- **TA-6(系统性)** **全文件每一处夹具的输入序 == 期望输出序**(唯一例外是 AC-37-34 的**去重**判别)⇒ 定序类断言**系统性失去判别力**(TB-1/TB-2 是这一性质的两次显影)。**建议元规则:定序类 AC 的夹具必须至少一处 输入序 ≠ 期望序**
- **TA-7** GDD `:452-465` 订正② 原始动机是「第 3、4 例**同 tick**」,但第 4 例落在 `tick=400`(`:100`)⇒ 批量同 tick 边界未覆盖
- **TA-8** Impl Note 5 要求阈值「2/3 两档」,实测只有 3 一档;`PATTERN_THRESHOLD ≥ 2 构建拒` 的 Guardrail(`:25`)代码侧只以注释出现

---

## 三、正面核实(记录 —— 防「全 BLOCKING」失真)

**代码面**:
- 阈值 `>=` 边界 ✅ · `firedDiseases` 按 `salted_key` 排序 ✅ · `FirstPerPatient` 去重 ✅ · 门 A ✅ · **无 `if (kind == injury)` 类硬编码** ✅ · 零 float ✅ · 逐 D 盐派生 ✅

**测试面(8 条)**:
1. **AC-37-34(`:206-228`)是全文件唯一「断内容」的用例,且真的判别** —— 能捕获「无 `FirstPerPatient` 去重 ⇒ `{A₁,B₁,A₂}`」这一 GDD `:452-465` 点名的新旧算式分叉 ⇒ **该 AC 的 BLOCKING 判据成立**;
2. **AC-37-07/08 成对夹住 `>=` 边界**(2 例不触发 / 恰 3 例触发)⇒ 阈值的 `<` 与 `≥` 两侧都被正面钉住;
3. **去重算子(`:79-88`)有真证据**:AC-37-22 的 A,B,A 夹具 + `:355-366` 综合用例双覆盖;
4. **AC-37-13** 可捕获「共病只进首组」变异,判别力真实;
5. **盐派生有全案级黄金夹具**:`SplitMix64` 形状被 `golden_hash_v1_test.cs:103`(含 `"case-salt"` 标签向量)钉住(ADR-012 双级黄金矩阵)⇒ AC-37-27 的**盐函数本身**不是裸奔的;缺的只是**定序消费面**的判别力(TB-1);
6. **AC-37-14 的正向合取已按 GDD `:1075` 订正落实**(先断存在再断缺席),不是旧稿那种「字段不存在 ⇒ 恒真的纯负断言」;
7. **性质 2(无时间窗口下界)有判别力**(`:368-381`);
8. **纪律面诚实**:`PatternDetector.cs:7` 明写「生产代码 —— 测试测的是本文件,不是测试自己的副本」;文件无 `Assert.Pass()` / 无硬编码 `true` 桩。

**运行态基线(借用,非本次执行)**:`unity/Logs/tr-batchd-full.xml`(2026-10-09 16:16Z,`total=3181 passed=3134 failed=0 inconclusive=1 skipped=46`)内含 `PatternDetectionTest` 全部 23 条逐条 `result="Passed"`(`tr-batchc-full.xml` / `tr-batchd-targeted.xml` 同)。
⚠️ **全绿,但绿不等于有效** —— 下述 BLOCKING 均在此**绿态**下成立。⚠️ 时效为 2026-10-09,**不能**充作 2026-10-15 的当下证据。

---

## 四、验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 0) 原件缺口(已确证)
ls production/qa/evidence/ | grep -i case-system

# 1) CB-1/TB-4:PatternDetector 零生产调用者 + PatternRecognized 无写者
grep -rn "PatternDetector" unity/Assets --include=*.cs | grep -v "/Tests/"
#    期望:仅 Sim/PatternDetector.cs 自身 + Editor.Tools.Gates/WriterExistenceGate.cs:88 的豁免表文本
grep -rn "IsFired\|FiredDiseases\|MemberSet" unity/Assets/Tests --include=*.cs | grep -v case_pattern_detection_test
#    期望:零命中 ⇒ fires-once 无第二处证据

# 2) CB-2/TB-5:ChainEligible/ScriptedChain 在生产码零存在
grep -rn "ChainEligible\|ScriptedChain" unity/Assets --include=*.cs; echo "exit=$?"
#    期望:零命中

# 3) TB-1:AC-37-27 夹具的期望序 == 插入序(独立复刻 SplitMix64,golden 自校验随行)
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
k1,k2=h(12345,"case-salt",1),h(12345,"case-salt",2)
print(f"salted(1)={k1:#x}\nsalted(2)={k2:#x}\n期望序={'[1,2]' if k1<k2 else '[2,1]'}（夹具插入序=[1,2]）")
PY
#    期望输出:[1,2] == 插入序 ⇒ 删掉 PatternDetector.cs:121-126 的 Sort 后断言仍绿 ⇒ 不可捕获

# 4) TB-1 直接变异实测(黄金判据:把实现改坏,测试**必须**红)
#    注释 PatternDetector.cs:121-126 的 Sort 块 → 跑 EditMode 夹具
#    期望(正确判据)= test_ac3727 红;实测(现夹具)= 仍绿 = 假绿确证

# 5) 全量回归
pgrep -f Unity; ls unity/Temp/UnityLockfile 2>/dev/null          # 先确认编辑器已关
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath unity \
  -runTests -testPlatform EditMode -testFilter DaYiJingCheng.Tests.CaseSystem.PatternDetectionTest \
  -testResults unity/Logs/s11_case004_review_editmode.xml -logFile unity/Logs/s11_case004_review_editmode.log
```

---

## 五、未完成核查(如实标注 · 禁借绿)

1. **本次未跑测试**:全部结论为静态 + 既有运行记录(`tr-batchd-full.xml`,2026-10-09)交叉得出。**变异实测命令(§四 第 4 条)未执行** —— 属「可证伪命令已给、尚未现场证伪」
2. **batch 的 `--executeMethod` 具体类名未查证**:仓内未见已提交的 EditMode 运行脚本(`tools/ci` 无 runner);既有 XML 的生成命令未在本次核查范围定位
3. **`PATTERN_THRESHOLD` 配置注入载体未查证**:代码侧仅注释命中,未见「int ≥2、<2 构建拒」的构建期门;ADR-014 数据管线里是否已登记该旋钮未确认(故只列 ADVISORY)
4. **`PATTERN_THRESHOLD` 观测值来自 `entities.yaml:2536`**(`value: "3（P0 手工编排）"`),但「历史存档在新阈值下分叉」(Guardrail)的行为未验

---

## 六、合取判定与修复归属

**去重后核心 4 类**:
1. **AC-37-27 的定序判别力为零**(CB-3 实现侧 Dictionary 迭代序 + TB-1 测试侧不可捕获变异,两面同证,且 TB-1 有逐位复刻证据);
2. **AC-37-09/10 的成员冻结内容不可证伪**(CB-1 无真判据 + TB-2 只断 `Count`,两面同证);
3. **AC-37-29 零覆盖 + `ChainEligible` 全库零命中,且 W-1 已于 2026-10-15 结案 ⇒ 免责前提当日失效**(CB-2 ≡ TB-5);
4. **发射语义无生产载体(借绿)** —— `PatternRecognized` 无写者、`IsFired` 零调用者(CB-1/TB-4)。

**Test Evidence 门:不通过。**

**修复归属**:
- 上述 BLOCKING **一律归新一轮**,不得声称「原判定如此」;
- story 文件须登记「原件为补评件」并回填本件路径(义务第 2 条:Complete 判定须引用报告路径);
- **TB-4/CB-1 的写者缺失已由 `WriterExistenceGate.cs:88` 登记为显式债务** —— 若 producer 接受延后,须把该延后**写进 story 的 Out of Scope 并同步改 AC-37-08/09 的证据面**,而不是留在 `Complete` 里;
- **AC-37-29 的阻塞已解除**(W-1 结案)→ 夹具**今日即可写**,不再有免责理由;
- 测试面建议元规则(TA-6):**定序类 AC 的夹具必须至少一处 输入序 ≠ 期望序**。

---

# 七、勘误复核(2026-10-15 落盘后追加)

**触发**:story-002/003 勘误(见 `review-case-system-story-002-retro-2026-10-15.md` §七·一)——
复核发现补评代理存在「只核声明件却作全域否定」的失效模式。本件据此重扫全树以确认判定是否受影响。

**复核结论:本件无勘误。代码面 3 + 测试面 5 共 8 条 BLOCKING 全部保留。**

复核证据(全树 grep,含 `EditMode/`、`PlayMode/`、`tests/`):

```bash
grep -rln "PatternDetector\|IsFired\|ChainEligible" unity/Assets/Tests/
# 输出:仅 unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs
grep -rn "PatternDetector" unity/Assets --include=*.cs | grep -v "/Tests/"
# 输出:仅 Sim/PatternDetector.cs 自身 + Editor.Tools.Gates/WriterExistenceGate.cs:88 豁免表文本
```

⇒ 与 story-002/003 的情形**结构性不同**:那两件的 AC 载体**确实存在于他处**(接线件/真装配件),
而本件的 `PatternDetector` 在**测试面全树只有一处**,在**生产面零调用者** ⇒
「AC-37-27 定序判别力为零」「发射语义无生产载体」「AC-37-29 零覆盖」等判定**均不受他件影响**。

**另确认**:PlayMode 基线(`story009_review_playmode.xml`,2026-10-14 20:00,`total=186 passed=186 failed=0`)
中**无任何 `PatternDetector` / `PatternRecognized` 相关件** —— 本 story 无 PlayMode 侧证据面。
