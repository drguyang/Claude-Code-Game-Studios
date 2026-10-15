# 复评原件 —— case-system story-001(修复后 · 评当下代码)

**评审时点**:2026-10-15(**修复后复评** —— 评当下代码,承 `.claude/docs/review-workflow.md` §义务 3 不可追补)
**被评对象**:`production/epics/case-system/story-001-case-stream-kinds-identity.md`
**原判定来源**:`production/qa/evidence/review-case-system-story-001-retro-2026-10-15.md` §二(补评件,4 BLOCKING)
**声称的修复**:
- B-3 → `production/qa/evidence/fix-s11-11-case-system-story-001-b3-2026-10-15.md`(S11-11)
- B-2 / B-4 → `production/qa/evidence/fix-s11-12-case-system-story-001-b2-b4-2026-10-15.md`(S11-12)
- B-1 → GDD 收窄(`design/gdd/case-system.md:1121`)
**评审方式**:单代理 · 独立复跑(未采信自述;A2 判别力以**变异**独立实证)
**硬约束遵守**:未跑 Unity / 未起 batch(仅 `pgrep` 只读);未改任何仓内文件(变异在 `/tmp` 副本上做)。

---

## 一、原判定(逐字引补评件 §二 · 4 条 BLOCKING)

> **[B-1 · 扫错对象 + 测试自造函数 + 恒真断言] AC-37-06 打的是测试内 LINQ,生产载体零调用**
> - 生产载体 `IdAuthority.ReconstructPatientHighWater`(`Sim/ItemDatabase/IdAuthority.cs:90-102`)**存在且零调用**;
> - 测试 `:342-345` 自写 `Where(!IsNone).Select(Value).Max()`,`:347` 断言 `Is.EqualTo(5)` 由测试字面量 `{1,5,None,3}` 决定 ⇒ **任何实现下恒真**;
> - **更深一层**:AC 原文判据**算术上不可证伪** —— 哨兵 −1 永不可能成为含非负 id 集合的最大值;真实风险只在「把 −1 重解释为无符号大值」,而该路径全库不存在;
> - ⚠️ `if (id < 0) continue;`(`IdAuthority.cs:96`)在 `candidate ≥ 0` 不变量下是**观测冗余** ⇒ **不存在能打红的正例**;
> - **修复**:AC-37-06 须**重述判据**(如「`next = max(seen)+1` 只升不降」)并改打生产载体。仅加大输入无效。

> **[B-2 · 空跑断言(vacuous)] AC-37-03 的 A2 子项零断言**
> - `:153-172` 内层 `foreach` 在当下 5 条 case `payload_schema` 上**执行 0 次**(实测 float/double 命中数 = 0);
> - 唯一活着的断言是 `:157` 的 `Count == 5`;且扫的是 registry **散文**,非载荷 struct 字段类型;
> - 旁注:kindgen 自己的 A2 是同款弱实现,但**已登记在案**(`kindgen.py:11-14`「严格形未实现」)⇒ 测试把一份**已承认的弱检查**当成 AC 达标证据。

> **[B-3 · 覆盖虚报 / AC 号一号两义]**
> - `:5-8` 测试头**自填** `AC-37-03/04/05/06`,与 GDD 权威编号(`case-system.md:1036-1051`)**指向完全不同的需求** …
> - ⇒ 任何按 AC 号做的覆盖矩阵都会**重复计数**,把 story-001 的测试误记为 AC-37-03/05/06 的证据;
> - …**修复零成本**:改为无编号引用。

> **[B-4 · 借绿(载体未跑却报绿)] AC-37-03 的执行半边 = NOT-RUN**
> - story §QA Test Cases 明写 Given「entities.yaml 五 Kind 行」→ When「**运行生成器**」;
> - 测试**从不运行 kindgen**(`grep kindgen` 零命中),改为在测试内复刻其扫描(`:361-442`);
> - `StreamRouting.g.cs:3,142` 的「产物存在即断言已过」是**生成物自述**,不是证据 …
> - ⇒ **把 kindgen 删掉,13 条测试照样全绿**;
> - **修复**:「CI 执行 `kindgen --check`」应为 AC-37-03 的**唯一载体**,测试面不再复刻(B-2/B-4 是同一缺口的两半)。

---

## 二、修复落点(逐条对 —— 独立核到的实际改动)

| BLOCKING | 修复件 | 实际落点(我核到的) |
|---|---|---|
| **B-1** | `design/gdd/case-system.md:1121` | AC-37-06 行内加注(四小项),**原文一字保留**。加注声明:① 真实前置 = 7a + 45,均未实现 ⇒ P0 不可测;② 本轮可测半边 = `ReconstructPatientHighWater`;③ 未测半边登记 `BLOCKED-BY-7a/45`;④ 原判据形态「算术上不可证伪」**不构成证据**,修复 = 改打生产载体 |
| **B-2** | `tools/kindgen/kindgen.py` | `a2_payload_types()`(新,`:167-210`):对象换成 `unity/Assets/Sim.Contracts/` 全树 `<Kind>Payload` 的 **C# 字段声明**;判据换成**白名单**(`INT_PRIMITIVES` = 8 整数宽度 + bool,`:64-66`);递归展开嵌套 struct(`:186-206`);**非空跑守卫**两处(`:189-191` 逐 struct 零字段 FAIL · `:209-210` 全局 `stats['fields']==0` FAIL)。旧散文扫描降级为 `a2b_registry_prose()`(`:213-233`),docstring 自陈「单独不足以构成 A2 达标」 |
| **B-4** | `.github/workflows/tests.yml` | 新增独立 job `kindgen-assertions`(`:38-62`):步骤① `python3 tools/kindgen/kindgen.py --check`(`:52`,非零即红);步骤② 重生成 + `git diff --quiet -- unity/Assets/Sim/StreamRouting.g.cs`(`:56-61`,有 diff 即红)。**不并入**占位 `guard-assertions`(`:20-29`,`exit 0` TODO 桩) |
| **B-4**(收口) | `unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs` | 删掉测试内复刻:4 条测试(`test_kindgenA2/A3/A4/A5_*`)+ 4 个手搓 `entities.yaml` 解析器(`GetCaseKindNamesFromRegistry` / `GetCasePayloadSchemas` / `GetCaseAuthors` / `FindRepoPath`)。**测试数 13 → 9** |
| **B-3** | 同上测试件 | 全文件去号(头 4 + 体注释 14 + **方法名 10**,共 25 处);保留 `AC-37-01`/`AC-37-02`/`AC-37-14`(与 GDD 一致) |

---

## 三、验证命令(可复跑 · 可证伪)+ 我**实跑**的输出

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
F=unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs
```

### ① A2 是否真跑(非空跑)—— kindgen 本体

```bash
python3 tools/kindgen/kindgen.py --check; echo "EXIT=$?"
```
实测输出:
```
kindgen: A1–A5 全过(--check 不写文件)· A2 严格形:39 个载荷 struct / 158 个字段类型受判(非空跑)
EXIT=0
```
**结构性核实**(读码,非采信打印):`stats["fields"] += 1` 位于 `for fname, ftype in fields:`(`:192-193`)体内,
该循环位于 `while todo:`(`:186`)体内 ⇒ 计数**随扫描累加**,非硬编码。两处零字段守卫均在扫描路径上。

### ② A2 的**判别力**(独立变异实证 —— 在 `/tmp` 副本上做,不动仓)

```bash
D=/tmp/kgmut_$$   # 复制 tools/kindgen + design/registry + design/gdd/systems-index.md + unity/Assets/Sim.Contracts
# 基线
python3 $D/tools/kindgen/kindgen.py --check; echo "EXIT=$?"
# 变异 1:CaseOpenedPayload.PatientId  int → float
sed -i 's/public readonly int PatientId;/public readonly float PatientId;/' $D/unity/Assets/Sim.Contracts/Payloads/CasePayloads.cs
python3 $D/tools/kindgen/kindgen.py --check; echo "EXIT=$?"
```
实测输出:
```
基线:  kindgen: A1–A5 全过 · A2 严格形:39 个载荷 struct / 158 个字段类型受判(非空跑)   EXIT=0
变异1: FAIL A2 CaseOpenedPayload: 字段 CaseOpenedPayload.PatientId 类型 'float' ∉ 整数域白名单 —— …
       FAIL A2 CaseClosedPayload: … 'float' …
       FAIL A2 PatternRecognizedPayload: … 'float' …
       FAIL A2 JudgmentRecordedPayload: … 'float' …
       FAIL A2 JudgmentRevisedPayload: … 'float' …
       kindgen: 5 条断言失败 —— 构建失败(ADR-024 §⑤)                              EXIT=1
```
变异 2(抽空 struct 字段,验非空跑守卫):
```
变异2: FAIL A2 CaseOpenedPayload: 零字段 —— 该 struct 的字段扫描为空(空跑)          EXIT=1
```
⇒ **A2 可证伪**:同型输入改一字符即由绿转红,**非恒真**。还原后 EXIT=0。

### ③ B-4 载体唯一性(测试面不再复刻)

```bash
grep -nE "GetCaseKindNamesFromRegistry|GetCasePayloadSchemas|GetCaseAuthors|FindRepoPath|kindgenA[2-5]" $F
grep -c "\[Test\]" $F
grep -rn "kindgen.py" --include=*.yml --include=*.yaml --include=*.sh --include=*.py . | grep -v "^./tools/kindgen/kindgen.py"
```
实测输出:
```
（第一条）(无输出 —— 复刻与手搓设施已清零)
（第二条）9
（第三条）.github/workflows/tests.yml:52:        run: python3 tools/kindgen/kindgen.py --check
          .github/workflows/tests.yml:56:          python3 tools/kindgen/kindgen.py
          （其余命中均为 kindgen.py 自身注释/docstring / tr-registry 的散文引用,非执行点）
```
⇒ 全仓**唯一**执行 kindgen 的位置 = CI job `kindgen-assertions`。

### ④ CI 判据②的**前提**是否成立(产物与 registry 是否已同步)

```bash
# 在 /tmp 副本重生成,与仓内已提交产物逐字节比对(不在仓内跑 —— 避免改文件)
diff $D2/unity/Assets/Sim/StreamRouting.g.cs unity/Assets/Sim/StreamRouting.g.cs && echo "IN SYNC"
```
实测输出:
```
kindgen: A1–A5 全过 → 写出 unity/Assets/Sim/StreamRouting.g.cs(35 支 switch)…
IN SYNC (byte-identical)
```
⇒ CI 判据②若执行,今日**会过**(产物与真源一致)。

### ⑤ B-1:story-001 测试是否改打生产载体

```bash
grep -n "IdAuthority\|Reconstruct" $F              # 期望:若已改打,应有 ReconstructPatientHighWater
grep -rn "ReconstructPatientHighWater" unity/Assets --include=*.cs
```
实测输出:
```
（第一条）(无输出 —— story-001 测试**未**调用生产载体)
（第二条）unity/Assets/Tests/EditMode/ItemDatabase/id_authority.cs:168:  authority.ReconstructPatientHighWater(new[] { -1, 0 });
          unity/Assets/Sim/ItemDatabase/IdAuthority.cs:90:  public void ReconstructPatientHighWater(IEnumerable<int> patientIds)
```
现测试 `test_highWaterMark_sentinelMinusOne_noPollution`(`:261-285`)的判据体**仍是**:
```csharp
var maxPatientId = events.Where(e => !e.Patient.IsNone).Select(e => e.Patient.Value).Max();
Assert.That(maxPatientId, Is.EqualTo(5), "max(patient_id) 须忽略哨兵 -1(ADR-007 §四)");
```
⇒ **与补评件点名的那段逐字同型,未被改动**。

### ⑥ GDD 收窄文本(逐字)

```bash
grep -n "AC-37-06" design/gdd/case-system.md
```
实测(`:1121`):
> … ② **本轮可测半边(= 收窄后的判据)** —— `IdAuthority.ReconstructPatientHighWater` 由**事件流**重构病人 id 高水位,与迁移前状态一致;该半边**不依赖 7a/45**(纯流函数),**已由 story-001 的测试载体承接**;③ **未测半边**显式登记 **`BLOCKED-BY-7a/45`** …;④ ⚠️ **原判据形态的缺陷(须记)**:以「id 集合最大值」断言高水位在**算术上不可证伪**(`-1` 哨兵永不为非负 id 集合的最大值)⇒ 该形态**不构成本 AC 的证据**;修复 = **改打生产载体**而非加大输入。

### ⑦ 借用运行基线(来源:`unity/Logs/s11-final-EditMode.xml`,2026-10-15 12:54 本机)

```bash
grep -o 'test-case[^>]*CaseIdentityRoutingTest[^>]*' unity/Logs/s11-final-EditMode.xml | …
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"[^>]*' unity/Logs/s11-final-EditMode.xml | head -1
```
实测输出:
```
Passed  test_ac3701_caseIdTriple_uniqueAcrossStream
Passed  test_ac3701_referencedCaseId_equalsSomeOpenedTriple
Passed  test_ac3702_twoPatientsSameTick_caseIdsDiffer
Passed  test_crossStreamOrder_deterministicNoTies
Passed  test_crossStreamOrder_streamPriority_orderHistoryCaseWorld
Passed  test_highWaterMark_sentinelMinusOne_noPollution
Passed  test_kindgenA1_fiveCaseKinds_routeToCaseStream
Passed  test_payloadShape_patternRecognizedPayload_containsSaltedKey
Passed  test_payloadShape_payloads_noOpenedTickField
total="3387" passed="3341" failed="0" inconclusive="0" skipped="46"
```
⚠️ **归属声明**:该 XML 是**借用**本机 `unity/Logs/`(易失中间产物,`.gitignore`)的**只读**日志,
**非本件产出**(本件受硬约束未跑 Unity)。它证明「9 测当下全绿」,**不证明任何 AC 被验证**。
⚠️ `pgrep -af Unity` 实测**无 `Unity -batchmode` 进程**(仅 UnityHub 授权客户端 / VBCSCompiler),故未与主轮 batch 冲突。

---

## 四、独立复核结论(逐条)

| # | 原判定 | 现状态 | 依据 |
|---|---|---|---|
| **B-1** | AC-37-06 判据算术上不可证伪 / 生产载体零调用 | **PARTIALLY-CLOSED** | GDD 侧**已**收窄且**如实**记下原判据「不构成证据」(④);但 retro 的修复原文是「重述判据 **并改打生产载体**」—— **后半未做**:story-001 测试仍用测试内 `Where(...).Max()`,`ReconstructPatientHighWater` 在 story-001 面内**仍零调用**(§三⑤)。更糟:§三⑥ 的 GDD 加注②自称「已由 **story-001 的测试载体**承接」,**与代码不符**(真实调用点在 `ItemDatabase/id_authority.cs:168`,属 21a 的载体,非 story-001) |
| **B-2** | kindgen A2 空跑(内层循环 0 次) | **CLOSED** | 严格形已落;对象换成 C# 字段声明、判据换成白名单;**变异实证** int→float ⇒ EXIT=1(§三②),抽空字段 ⇒ 零字段守卫 EXIT=1;基线 158 字段受判(§三①)。**非恒真**已由独立变异坐实 |
| **B-3** | 测试头 AC 号一号两义 | **CLOSED** | 全文件去号(25 处);grep 复刻/编号残留为零;保留的 `AC-37-01/02/14` 与 GDD 权威含义一致(§三③) |
| **B-4** | 载体借绿(测试从不运行 kindgen) | **CLOSED**(带一处未闭) | CI job `kindgen-assertions` 独立成 job、真跑非占位(`:38-62`);测试面复刻**已清零**;全仓唯一执行点 = 该 job(§三③);产物与真源今日逐字节一致(§三④)。⚠️ **未闭**:**该 job 从未在真 runner 上执行过**(见 §六) |

**载体唯一性专项(任务点名)**:保留的 `test_kindgenA1_fiveCaseKinds_routeToCaseStream`(`:152-168`)
打的是**生成物** `StreamRouting.Of(...)`(生产码,`Sim/StreamRouting.g.cs`),**不运行 kindgen、不扫 registry**。
⇒ **它不构成第二载体**。判定:**载体唯一 = CI**,成立。(其断言内容确与 CI 的 A1 + 重生成 diff 重叠,属冗余而非第二载体。)

---

## 五、Test Evidence 门判定

> ## **不通过**

**理由(单条,窄口径)**:**B-1 只闭合了一半 —— story-001 测试件内仍留着 GDD 自己宣告为「非证据」的断言形态。**

- B-2 / B-3 / B-4 三条**真闭**,且经我**独立实证**(变异 + grep + 逐字节 diff),非采信自述;
- 但 B-1 的修复原文要求**两半**(重述判据 **并** 改打生产载体),只做了前一半:
  - GDD `:1121` 已把 AC-37-06 收窄,并**如实**承认原「id 集合最大值」形态**不构成证据**;
  - 而 story-001 的 `test_highWaterMark_sentinelMinusOne_noPollution`(`:261-285`)**逐字仍是**那个形态 ⇒
    **story-001 的 Test Evidence 文件里,仍有一条自认「非证据」的断言被当作判据**;
- 且 §三⑥ 的 GDD 加注②「**已由 story-001 的测试载体承接**」**与代码不符** —— 生产载体在 story-001 面内零调用。
  这正是本项目反复出现的病灶:**自述 ≠ 代码**。按「不得凭自述判定」的纪律,该承接声明**不成立**。

⚠️ **判定的边界(防过度)**:门不通过的**唯一**残余 = 高水位这一条测试(B-1 半边)。
其余 8 测 + CI 载体**均成立**;B-2/B-3/B-4 **不得再记为本轮阻塞**。
补上「测试改打 `ReconstructPatientHighWater` + 断『只升不降』」后,本门即可转**通过**
(同型做法在 `id_authority.cs:163 test_ac21a63_patientReconstruct_ignoresNoneSentinel` 已有现成范例:
`NextPatientId()` ×2 ⇒ `ReconstructPatientHighWater([-1,0])` ⇒ 断言仍为 2)。

---

## 六、新发现问题 / 未闭项 / 不确定项(如实,禁借绿)

### 新发现(本轮修复引入或暴露)

1. **【GDD 承接声明与代码不符】(与 B-1 同源,已并入判定)**
   `case-system.md:1121` ②称本轮可测半边「**已由 story-001 的测试载体承接**」,实测 story-001 测试
   **零调用** `ReconstructPatientHighWater`。真实调用点仅 `ItemDatabase/id_authority.cs:168`(归 21a)。
   ⇒ 该句**应为「由 21a 的 `id_authority.cs` 承接」或改 story-001 测试**;现状是一处**自述虚报**。

2. **【story 文件陈旧计数/笔误】(非阻塞,登记)**
   `story-001:92` 仍写「测试文件存在且 **13/13** 绿」(`:126` 同)—— 现测试数 **9**(§三③)。
   `story-001:99` 写「**AC-37-30/06** 断言形态错误」—— 疑为「AC-37-03/06」笔误(30 号在本 story 不存在)。
   ⚠️ 本件**未改 story**(硬约束),仅登记。

3. **【story 自陈「B-1 已闭合」不成立】(登记)**
   `story-001:159-161` 称 B-1「已按用户授权收窄 GDD 文本」⇒ 隐含闭合。但如上,B-1 的**测试半边**未做。

### 未闭项(修复件已自陈者,我复核属实)

4. **【CI job 从未在真 runner 上执行】** `kindgen-assertions` 的**接线与判据**已在
   (`.github/workflows/tests.yml:38-62`),且我实测其两条判据**今日会过**(§三①②④);
   但**该 job 从未在 GitHub Actions 上实跑过** ⇒ 「失败即红」是**设计判据**,非**已观测行为**。
   `fix-s11-12 §四` 已如实登记;本件确认其**仍未闭**。(归属:devops / 首次 push 时验证)

5. **【变异矩阵无常驻执行体】** `fix-s11-12 §四` 的 7 变异是沙箱一次性实测,**未固化为 CI 步骤**。
   本件**独立重做了其中 2 个**(int→float / 抽空字段,§三②)并**复现 EXIT=1**,故「A2 有判别力」
   本件可**背书**;但「判别力**持续受检**」仍无载体。(归属:kindgen 侧 / 后续批)

6. **【同型号复刻在他处,本 story 面外】** `Combat/combat_actions_schema_test.cs:121`
   `test_kindgen_registeredKinds` 亦以测试内反射复查 registry 登记面 —— 与 B-4 同型,归系统 25,
   **不在 story-001 面内**(本件未动,如实登记)。

### 不确定项(本件无法判定 / 需发起方)

7. **【AC-37-03 载体从「Unity 测试树」迁到「CI job」是否合规】** `coding-standards.md` 的 Logic 故事
   证据落点写的是 `unity/Assets/Tests/EditMode/[System]/`;AC-37-03 的唯一载体现为 **CI job(纯 python)**。
   这是**补评件自己开的处方**(retro B-4 修复段明写「CI 执行 `kindgen --check` 应为唯一载体」),
   故我按处方判其**成立**;但「CI job 能否算 story 的 Test Evidence 载体」这一**标准问题**本件**不裁**。
8. **【`asserts="0"` 的 XML 计数】** 借用日志(`s11-final-EditMode.xml`)所有套件均报 `asserts="0"`
   (NUnit 约束式断言不计数的 UTF 已知现象)。⇒ 本件只能据 `result="Passed"` 判绿,**不能据 assert 计数**。
9. **【本件未跑 Unity】** 全部 Unity 侧结论均**借用**本机 `unity/Logs/` 只读日志(§三⑦ 已声明来源),
   **非本件直跑**。若发起方需**权威复跑**,须在 batch 空闲时由发起方执行
   `-testFilter DaYiJingCheng.Tests.CaseSystem.CaseIdentityRoutingTest`(期望 `total=9 passed=9 failed=0`)。

---

## 七、判定摘要(一行)

**门 = 不通过**(B-2 / B-3 / B-4 **CLOSED** · **B-1 PARTIALLY-CLOSED** —— story-001 测试内的高水位断言仍为
GDD 自认「非证据」的形态,且 GDD「已由 story-001 承接」一句与代码不符)。
