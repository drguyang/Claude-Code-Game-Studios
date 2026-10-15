# 修复证据原件 —— S11-9 / case-system story-004 CB-2 + TB-5(`ChainEligible`/`ScriptedChain` 零命中 · AC-37-29 零覆盖)

**日期**:2026-10-15
**对象**:`unity/Assets/Sim/PatternDetector.cs`(**生产代码**)+ `unity/Assets/Tests/EditMode/CaseSystem/case_pattern_detection_test.cs`
**来源判定**:`production/qa/evidence/review-case-system-story-004-retro-2026-10-15.md` **CB-2**(代码面 BLOCKING)+ **TB-5**(测试面 BLOCKING)—— 二者同源
**排期**:`production/sprints/sprint-11.md` **S11-9**(用户裁定「必须项优先」并入;估 0.75 天)
**性质**:本件 = **修复证据**,**不是评审**(承 `.claude/docs/review-workflow.md` §义务 3 不可追补)。

---

## 一、原缺陷判定(逐字承补评件)

### 1.1 CB-2(代码面 BLOCKING)

> **[CB-2 · 必交缺失] `ChainEligible` / `ScriptedChain` 全库 `.cs` 零命中**
> - AC-37-29 在 story `:40` 登记「非 ScriptedChain 半边……**谓词实现先落**(`ChainEligible` 判定域),夹具待 9+37 会签后补」;
> - 实测 `grep -rn "ChainEligible\|ScriptedChain" unity/Assets --include=*.cs` ⇒ **零命中**(唯二命中:`case-system.md:588` 定义、story `:40`);
> - ⇒ **连「先落」的那半边也没落**,而这半边**从未被 W-1 阻塞**。

### 1.2 TB-5(测试面 BLOCKING)

> **[TB-5 · 必交件缺失 + 阻塞理由已于今日消失] AC-37-29 零覆盖**
> - 同 CB-2(`ChainEligible` 零命中);
> - **阻塞理由已解除**:`case-system.md:610-627` 记 **「✅ 会签裁决(2026-10-15,S11-2)裁① = 三案各挂 `DIS_DYSENTERY`」**;
> - 裁决同时明写「`AC-37-12` 原样不动(三案从此**不是**纯伤情案)」⇒ 脚本链 `CandidateSeq` 非空的机制路径已定,**夹具今天就是可写的**;
> - ⇒ story `:40` 自认的义务未履行,其免责前提**当日失效**。

### 1.3 复核确认(本件执行期实测)

- `grep -rn "ChainEligible\|ScriptedChain" unity/Assets --include=*.cs` ⇒ 修复前**零命中**(唯二命中仍在 GDD 定义与 story `:40`)。
- **脚本链的成员数据在 P0 不存在** —— `assets/data/` 无该表;`design/gdd/content/he-returns.md` §四 只列**具名角色**(甲/乙/丙),**无数字 `patient_id`**。GDD F-37.1b 逐字:

  > 「**具名角色 → 数字 `patient_id` 的绑定**仍属内容值:由 7a 高水位在首次立案时分配,绑定落 ADR-014 烘焙(承「骨架先行,载荷归内容」纪律 —— **本 GDD 只登记机制与判定域,不落数值**)。」

  ⇒ **机制与数据分离是 GDD 的明文纪律**,不是本件的范围裁剪。本件交付**机制层**,数据由调用方 / ADR-014 烘焙注入。

---

## 二、修复落点

### 2.1 `ScriptedChain` + `ChainEligible`(GDD F-37.1b 的机制载体)

GDD F-37.1b 逐字定义:

```
ScriptedChain(D) := 内容条目显式列出的有序 patient_id 三元组
ChainEligible(D) := 病例 c 满足  c.Patient ∈ ScriptedChain(D)
```

新增两个类型(均住 `Sim` —— **零引擎引用**,门 A 不破):

- `ScriptedChain{ int DiseaseOrdinal; IReadOnlyList<int> MemberPatientIds; }` —— **不含任何具体 `patient_id`**(承 1.3 的 GDD 纪律)。
- `ChainEligible.IsEligible(in ScriptedChain, int patientId)` —— 逐字兑现上式。
- `ChainEligible.TryChainOf(chains, diseaseOrdinal, out chain)` —— 逐 D 取链(同一 D 至多一条;数据合法性不在此层,归 ADR-014 阶段 2)。

### 2.2 `Evaluate` 定义域收窄(逐 D 二选一)

抽出私有 `ComputeCandidateSeq` / `SortBySaltedKey` 使 `Evaluate` 与新增入口共用单一定序源;新增 5 参重载(`scriptedChains`),原 4 参重载转发 `null` ⇒ **现行为逐位不变**。

收窄点(GDD 逐字「成员判定域 = 显式指定,不是『全局首次三例』(旧算法的行为保留给**非脚本**的同源检测,即自然疫情)。**脚本条目点名的三个 `patient_id` 之外的同病种病例 ⇒ 不进该链、不消耗该链的判定名额**」):

```
命中链的病种 ⇒ CandidateSeq(D) 只留链成员(链外病例既不计数也不占名额)
未命中链的病种 ⇒ 维持 F-37.1 原算式(自然检测)
```

⇒ **逐 D 二选一,不是全局模式**(GDD 明写「旧算法的行为保留给非脚本的同源检测」)。

### 2.3 `NextEmissions` —— **发射决策层**(TB-4/CB-1 的载体)

**⚠️ 本节是一处语义厘清,须如实登记(非判定推翻)**:

补评件 TB-4/CB-1 记「**发射语义无生产载体(借绿)**」—— 该**观察成立**,但其指向须收正:

- `Evaluate` 返回**前缀属性**(已达标的 D 在第 4、5、6 例结案后**仍然** `Fired = true`)。**这本身不是缺陷** —— AC-37-07/08 测的就是这一层,现有 `test_ac3709` 断言「第 4 例仍触发(已触发过)」是**正确的**。
- 缺的是**另一层**:「**本 tick 真正应发射几条**」。AC-37-09/29 的「第 4 例不重发 / 永不重发」属该层。
- ⇒ 原诊断的准确表述是「**缺一层**」,不是「`Evaluate` 有问题」。

新增 `PatternDetector.NextEmissions(...) → IReadOnlyList<PatternEmission>`,内部 = `ComputeCandidateSeq` → 达标 D 集 → `SortBySaltedKey` → **逐个过 `IsFired` 门** → 切该 D 成员。`PatternEmission{ DiseaseOrdinal; SaltedKey; MemberSet; }`。

**⚠️ 写者仍未落 —— 不得误读**:`NextEmissions` **不触 `IEventSink`、不 `Append`、不发号 `Seq`**。它只到「该做什么」。写者(`Append` + 发号)按 **2026-10-15 用户裁定「接受延后并正式登记」** 仍缺(具名豁免 `Editor.Tools.Gates/WriterExistenceGate.cs:88`)。本件**不得**被引为「写者已落」。

**⚠️ CB-3 未触及**(顺延 sprint-12):`PatternDetectionResult.MemberSet` **仍扁平**(CB-3 ②),`ComputeCandidateSeq` 的 `Dictionary` 迭代序**仍非契约**(CB-3 ①)。`NextEmissions` 自带逐 D 归属是**本件所需**,**不是**对 CB-3 的修复。

### 2.4 测试面(23 → 30,新增 7 条)

| 夹具 | 覆盖 | 反证①敏感 | 反证②敏感 |
|---|---|---|---|
| `test_chainEligible_memberIsEligible` | 「二符号有实现」正 | — | — |
| `test_chainEligible_nonMemberIsNotEligible` | 同上,负 | — | — |
| `test_ac3729_chainQuota_notConsumedByRandomCases` | 名额不被消耗(+ 反向对照:同流按自然检测确实达标) | ✅ | — |
| `test_ac3729_chainComplete_firesWithChainMembersOnly` | AC-37-29 逐字(MemberSet = 链成员三例,链外不占名额) | ✅ | — |
| `test_ac3729_firesOnce_neverRefires` | 「一次性」(含对照:`Evaluate` 仍返 true,钉死两层边界) | — | ✅ |
| `test_ac3729_declarationOrder_notConsumed` | 声明序不被消费(钉死裁定②) | — | — |
| `test_ac3729_chainAndNaturalCoexist_perDisease` | 逐 D 二选一(GDD「旧算法保留给非脚本」) | ✅ | — |

---

## 三、证伪三步(逐字可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
U=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
PRJ="/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity"
# ⚠️ 不加 -quit(UTF -runTests 自行退出;加 -quit 会截断测试)

# ── ① 修复后(须 30/30 绿)─────────────────────────────────────────────
$U -batchmode -nographics -projectPath "$PRJ" -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.CaseSystem.PatternDetectionTest" \
  -testResults /tmp/n.xml -logFile "$PRJ/Logs/x.log"

# ── ② 反证①:停用 ComputeCandidateSeq 内的定义域收窄块 ⇒ 须 3 红 ────────
#   把 `if (ChainEligible.TryChainOf(...)) { ... candidates = narrowed; }` 整块注释掉
#   ⇒ 期望红:chainQuota / chainComplete / chainAndNaturalCoexist

# ── ③ 反证②:删 NextEmissions 内的 `if (IsFired(...)) continue;` ⇒ 须 1 红 ─
#   ⇒ 期望红:firesOnce_neverRefires

# ── ④ 全量双平台(零回归)──────────────────────────────────────────────
$U -batchmode -nographics -projectPath "$PRJ" -runTests -testPlatform EditMode  -testResults /tmp/e.xml -logFile "$PRJ/Logs/e.log"
$U -batchmode -nographics -projectPath "$PRJ" -runTests -testPlatform PlayMode  -testResults /tmp/p.xml -logFile "$PRJ/Logs/p.log"
```

> **⚠️ 还原复核纪律(S11-7 教训)**:每次回退/还原后**不得只信 `cp` 或 `diff` 的退出码** ——
> 须 **grep 关键符号独立复核**(本件用 `grep -c narrowed` / `grep -c 'if (IsFired(caseEvents, disease'`,
> 期望 3 / 1;并确认临时标记 `反证 N 临时回退` 命中 **0**)。

---

## 四、结果(双信号一致)

### ① 反证①(停用定义域收窄) —— 恰 **3 红**

```
test-run: total="30" passed="27" failed="3"
  FAIL test_ac3729_chainQuota_notConsumedByRandomCases
       「链外同 D 病例不消耗脚本链名额 —— 链成员仅 1 例,不达标」Expected: False  But was:  True
  FAIL test_ac3729_chainComplete_firesWithChainMembersOnly
       「MemberSet = 链成员三例(断内容,非仅计数)」Expected: equivalent to <CaseId,CaseId,CaseId>  But was: ...
  FAIL test_ac3729_chainAndNaturalCoexist_perDisease
```

### ② 反证②(停用「一次性」门) —— 恰 **1 红**

```
test-run: total="30" passed="29" failed="1"
  FAIL test_ac3729_firesOnce_neverRefires
```

### ③ 修复后 —— **30/30 绿**

```
test-run: testcasecount="30" result="Passed" total="30" passed="30" failed="0"
error CS = 0 · 退出码 0
```

### ④ 全量零回归

| 平台 | 结果 | 基线对照 |
|---|---|---|
| EditMode | **`3344 / 3298 / 0 failed` / 46 skipped** | S11-7 后 `3337/3291/0` ⇒ **+7 恰为本轮新增** |
| PlayMode | **`186 / 186 / 0 failed`** | **逐位等于 M2 基线** |

**引用集零改动**(无 asmdef 改动)⇒ 门 A / b2 门 / ADR-025 §① / ADR-029 全部继续成立。

---

## 五、证伪轮内发现的**本件自身缺陷**(已修正 · 须登记)

**⚠️ 夹具 7 `test_ac3729_chainAndNaturalCoexist_perDisease` 首版是假绿。**

- **症状**:反证① 第一次跑只红 **2** 条(期望 3),夹具 7 **未红**。
- **根因**:首版 `ExtractChainVsNatural` 把病人 1/2/3 映射到 D=1、病人 10(**唯一**一个 D=0)映射到 D=0
  ⇒ **D=0 的链外病例数 = 0** ⇒ 收窄与否结果完全相同 ⇒ **该夹具对「收窄」这一变异毫无判别力**。
  (它只证明了「链成员 1 例不达标」,而这在没有收窄时也成立。)
- **修正**:改 extractor 为「病人 ≤3 ⇒ D=1;其余 ⇒ D=0」,并在 D=0 补入**链外**病例 20/21/22
  ⇒ 无收窄时 D=0 以 **4 例**达标(与期望 `FiredDiseases = {1}` 不符)⇒ 判别力成立。
  修正后反证①**恰红 3 条**。
- **⚠️ 意义**:这条夹具若不经证伪轮,会以「绿」的身份留在文件里,却什么都没测 ——
  **正是补评件 TA-6 元规则「定序/变异类 AC 夹具须至少一处 输入序 ≠ 期望序」的同型病灶**。
  本件的证伪三步不是形式,它**实际抓出了本件自己的假绿**。

---

## 六、两处语义裁定(2026-10-15 用户裁定,本件执行)

**① 「一次性」的载体 = 新增纯函数决策入口**(`NextEmissions`)。
理由:AC-37-29 要求夹具覆盖「一次性」,而若夹具自己写 `if (!IsFired(...))` 则**正是 TB-4 所斥的「未写、未测的调用方契约」的复制**
⇒ 测试自造契约,判据不可证伪(删掉门夹具照绿)。新增入口后该语义由**生产函数**保证,夹具测的是真判据。
**代价已登记**:该入口**不触 `IEventSink`**,写者仍缺(承 2026-10-15「接受延后」裁定)。

**② 链内声明序** = **不消费**。
GDD F-37.1b 内部张力:`ScriptedChain(D) := …显式列出的【有序】patient_id 三元组`,同句又写「触发仍走 F-37.1 的
**同一算式**(只是 `CandidateSeq` 的定义域收窄)…**不需要第二套公式**」;而 F-37.1 的算式按全序键 `(Tick, Patient, Seq)` 定序
⇒ 声明序**不被消费**。取「同一算式」这一显式裁法,声明序作**内容语义**(叙事先后 甲→乙→丙),
以 `test_ac3729_declarationOrder_notConsumed` **钉死**。
**⚠️ 该张力未在 GDD 就地加注**(改设计件需单独授权,本轮未批)—— 登记待裁。

---

## 七、未清项(如实登记,禁借绿)

1. **写者仍未落** —— `Append` + 发号 `Seq` 缺(2026-10-15 用户裁定「接受延后并正式登记」)。
   `NextEmissions` 只到「该做什么」。**不得**引本件为「写者已落」。
2. **CB-3 未触及** —— `MemberSet` 仍扁平、`Dictionary` 迭代序仍非契约,归 **sprint-12**
   (sprint-11「本批不取」清单已列 CB-1/CB-3 实现侧)。
3. **story-004 其余 6 条顺延 sprint-12**:TB-1 定序判别力 / TB-2 成员冻结 / TB-3 真空夹具 / CB-1 实现侧 / 及 TA-6 元规则。
4. **GDD 两处待裁(本件未动)**:
   - `case-system.md` F-37.1b 的「有序」张力**未就地加注**;
   - `case-system.md:738` / `:718` 两处**自引行号漂移**(S11-10 登记,尚未裁)。
5. **脚本链的内容数据仍不存在** —— `patient_id` 绑定归内容轮 + ADR-014 烘焙(承 GDD「不落数值」纪律)。
   ⇒ 本条**不是缺口**,是 GDD 明文的归属划分;但须防「`ScriptedChain` 已落 = 三案链可跑」的误读。
6. **「先触发歧义」仍未钉死** —— GDD F-37.1b 注块登记为澄清项(自然病例若先凑满阈值则抢跑,一次性门挡住脚本链)。
   本件的逐 D 二选一**已消除**该歧义的一半(有链的 D **不再**走自然检测)⇒ 与 GDD 给出的裁法一致;
   但 GDD 的注块仍写「归 37 实现轮」,**是否据此更新注块待裁**(改 GDD 未授权)。
