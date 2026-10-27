# S20-4 实现轮证据原件 —— F-18.2 准入谓词 + 候选集三条件合取

**日期**:2026-10-27 · **执行**:AutoClaw 会话(`181888b1`)
**对象**:sprint-20 §S20-4(`processing-001` 配方子集 + 可采纳谓词)· 测试载体
`unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs`(新,24 条)
**权威件**:`design/gdd/processing.md` F-18.2(`:437-456`)/ 规则三(`:167`)/ AC-18-05 · 10 · 18 · 20 ·
ADR-011 **Amendment B** · ADR-025 §① · ADR-017 门 A

---

## 一、改动清单(Landed)

| 文件 | 动作 | 内容 |
|---|---|---|
| `unity/Assets/Sim.Contracts/ISkillLevelQuery.cs` | 新 | 只读等级查询接缝(`int QueryLevel(int actorId, int skillId)`)。**从 `ISkillGrowthPort` 按接口隔离原则拆出** —— 后者另带 `EmitGrowth`(写能力),纯谓词不得持有写通道 |
| `unity/Assets/Sim.Contracts/ISingleFurnaceQuery.cs` | 新 | 单炉空闲接缝(`bool IsBusy(int actorId)`)。AC-18-10 第三条件的**只读投影**:`FurnaceState.IsBusy` 需整条事件流 + 解码器,若进谓词签名则客户端预答路径**物理上不可用**,AC-18-20 的同源要求随之落空 |
| `unity/Assets/Sim/Processing/AdmissiblePredicate.cs` | 新 | F-18.2 两道门 `Judge`(`AdmissionRejection` 四值具名拒绝理由)· `IsAdmitted`(**「算不算过」的唯一定义点**)· `PreviewVerdict` / `TerminalVerdict` 两个具名调用点,**各自直接转调 `Judge` + `IsAdmitted`,体内零判据逻辑** |
| `unity/Assets/Sim/Processing/ProcessingCandidateSet.cs` | 新 | AC-18-10 三条件合取 `IsCandidate`(`owner==process` ∧ `¬IsBusy` ∧ `IsAdmitted(Judge(...))`)+ 保序批量 `Evaluate` |
| `unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs` | 新 | 24 条(见 §二) |
| `.meta` ×5 | 新 | guid 各自独立 |

**不动面**:零 `SimEvent.Kind` 增删(判定是派生态,不进流 —— ADR-016 §一)· 零 `IEventSink` / `IPayloadEncoder` 依赖(不持写通道 ⇒ 想写也写不出来)· `Sim` 引用集**一字未改**(仍恰 = `{BCL, Sim.Contracts}`)。

---

## 二、测试面(24 条新增,全绿)

| 组 | 测试 | 钉什么 |
|---|---|---|
| **AC-18-05** | `test_ac1805_minQuality_usedOnlyInsideAdmissiblePredicate` | 源码扫描 `Assets/Sim/**`(实测 **123 个 `.cs`**),`MinQuality` 使用点 ⊆ `AdmissiblePredicate.cs` |
| | `test_ac1805_scannerPositiveControl_detectsInjectedUse` | **正控**:注入的合成源必须被扫到 —— 缺此则「零命中」与「扫描器看不见」不可区分 |
| | `test_ac1805_minQuality_neverEntersAmplitudeExpression` | `MinQuality` 与 `*` / `/` 同行出现即红(幅值调制形态) |
| | `test_ac1805_predicateNeverReadsRecipeInputsOrOutputs` | 谓词不碰产出侧(碰即幅值路径) |
| **AC-18-10** | `test_ac1810_iffMatrix_allEightCombinations` | gate 上/下 × quality 上/下 × 空闲/占用 **8 组合**逐格断双向 |
| | `test_ac1810_iffReverse_nonProcessOwnerNeverCandidate` | **反向**(进候选集 ⇒ 三条件全真)+ Craft/Build 排除 + 正控 |
| | `test_ac1810_boundary_equalityPasses` | 阈值边界:`gate` 恰等 / `min_quality` 恰等均 **≥ 过** |
| | `test_ac1810_minIsOverNamedInstances_notPerInputRow` | F-18.2 补注①:`{5,1}` 的 min=1 ⇒ 拒(行级 min 会放行) |
| | `test_ac1810_emptyInstances_shortCircuitsFalseWithNamedReason` | 空集 ⇒ `NoConsumedInstances`(非 `MinQuality`) |
| | `test_ac1810_skillGateRejectedBeforeQuality_soReasonIsSkillGate` | 两门同时不过时的**次序语义** |
| | `test_ac1810_crossActorIndependence` | per-player 单炉(甲忙不连累乙) |
| | `test_ac1810_evaluatePreservesOrderAndAgreesWithIsCandidate` | 批量路径 ≡ 逐条 + **保序**(排序归 42,O-18-R6 未裁) |
| | `test_ac1810_nullRecipes_throwsNotSilentEmpty` | fail-closed:null 配方表 ≠ 零候选 |
| **AC-18-20** | `test_ac1820_bothCallSites_callTheSameMethodReference` | IL 面:两调用点 `call` 的 `MethodReference.FullName` **逐位相同** |
| | `test_ac1820_bothCallSites_shareTheSameAdmittedGate` | 「算不算过」亦同源 |
| | `test_ac1820_bothCallSites_haveNoComparisonOfTheirOwn` | 两包装体内**零比较指令** |
| | `test_ac1820_ilScannerPositiveControl_seesComparisonInJudge` | **正控**:`Judge` 体内必须扫到比较指令 |
| | `test_ac1820_admittedIsTheSoleDefinitionPoint_acrossSimAssembly` | 符号面:`AdmissionRejection.None` 全程序集唯一处 + 匹配器正控 |
| | `test_ac1820_candidateSet_goesThroughIsAdmitted` | IL 面:`IsCandidate` 必须 `call IsAdmitted` |
| | `test_ac1820_pureFunction_sameInputSameOutput_noCrossCallState` | 纯函数性 + 无跨调用缓存 |
| | `test_ac1820_craftSkillId_matchesLiteralSeven` | 字面量 7 ≡ `(int)SkillId.炮制` ≡ `CraftSkillId`(防自指:期望值用字面量) |
| | `test_ac1820_judge_queriesTheCraftSkill_notAnyOther` | 查的是炮制技能而非别的 |
| | `test_ac1820_nullInputs_failClosed` | 三道 null 全抛 `ArgumentNullException` |
| **AC-18-18** | `test_ac1818_partitionJudgementLivesInItsOwnFixtureOnly` | 存在性 + 归属校验:**不重写**判据(真身 = `recipe_owner_partition_test.cs`,6 条) |

---

## 三、回归数字(三元组)

> ⚠️ **2026-10-27 评审修复轮后,本节数字已整体更新。** 实现轮原始数字留档于下方「历史」小节(不删)。

### 3.1 终态(评审修复轮之后)

- **全量 EditMode**:`5414 / 5379 / 0 / 35` @ **`2026-10-27 10:07 CST`** · 批次 **S20-4 评审修复轮(全量)** · commit **`51193cf`**(修复未提交)
  - 前基线 `5409 / 5374 / 0 / 35` @ `2026-10-27 09:44 CST`(S20-4 实现轮)⇒ **total +5 / passed +5,零失败零回归**
  - **全名级差集**:新增 **5** · 消失 **0** · **非通过集新增 0 / 消失 0**
  - 复算见 §六
- **夹具终验**(证伪轮后):`29 / 29 / 0 / 0`(原 24 + 修复轮 5)—— `unity/Logs/s20-4-fix.xml`
- **PlayMode**:未触(纯 sim 逻辑测)—— 如实登记 **NOT-RUN**

### 3.2 历史(实现轮原始数字,留档不删)

- **全量 EditMode**:`5409 / 5374 / 0 / 35` @ **`2026-10-27 09:44 CST`** · 批次 **S20-4 实现轮(全量)** · commit **`51193cf`**
  - 前基线 `5407 / 5372 / 0 / 35` @ `2026-10-27 09:27:31 CST`(本会话,早于 `IsAdmitted` 重构与两条新夹具)⇒ **total +2 == 新增 2 条,零失败零回归**
  - **全名级差集**:新增 **2** · 消失 **0** · 翻转 **0**:
    - `+ …AdmissiblePredicateTest.test_ac1820_admittedIsTheSoleDefinitionPoint_acrossSimAssembly`
    - `+ …AdmissiblePredicateTest.test_ac1820_candidateSet_goesThroughIsAdmitted`
- **夹具终验**(证伪轮后):`24 / 24 / 0 / 0` —— `unity/Logs/s20-4-admissible.xml`

---

## 四、证伪轮 8/8(官方驱动 · `tools/falsify/unity_falsify.py`)

> ⚠️ **2026-10-27 评审修复轮后扩为 8 条**(原 6 条 + M7/M8 钉 AR1 的两处 query 校验)。实现轮原始 6 条留档于 §4.2(不删)。

**规格**:`tools/falsify/specs/s20-4-admissible-predicate.json` · **命中形态**:③定义域收窄 · ⑤阈值边界 · ②共存互斥 · ④一次性

| # | 变异(停用哪个守卫) | 落点 | 结果 | 转红测试 |
|---|---|---|---|---|
| **M1** | 技能门停用 | `AdmissiblePredicate.cs:122` | 5 红 | iffMatrix / evaluatePreservesOrder / skillGateRejectedBeforeQuality / judge_queriesTheCraftSkill / pureFunction |
| **M2** | 品级闸停用 | `AdmissiblePredicate.cs:137` | 2 红 | iffMatrix / minIsOverNamedInstances |
| **M3** | **预答内联复制一份判据**(行为等价) | `AdmissiblePredicate.cs:165` | **1 红** | `bothCallSites_callTheSameMethodReference` |
| **M4** | 空集短路停用 | `AdmissiblePredicate.cs:125` | 1 红 | emptyInstances_shortCircuitsFalseWithNamedReason |
| **M5** | `min_quality` 渗入幅值路径 | `ProcessingCandidateSet.cs:135` | 1 红 | minQuality_usedOnlyInsideAdmissiblePredicate |
| **M6** | owner 子集过滤停用 | `ProcessingCandidateSet.cs:135` | 2 红 | evaluatePreservesOrder / iffReverse_nonProcessOwnerNeverCandidate |
| **M7** | 入口 query 校验停用(逐条路径) | `ProcessingCandidateSet.cs:132` | 1 红 | `defaultQuery_throwsArgumentNull_notNullReference` |
| **M8** | query 校验移进循环(批量空表分支) | `ProcessingCandidateSet.cs:170` | 1 红 | `emptyRecipeTable_stillValidatesQuery` |

**判据一(断言须转红)**:8/8 恰如预期(exactRed 逐条比对)。
**判据二(不留痕)**:SHA-256 逐字节还原通过 · `FALSIFY-` 零残留 · 受保护路径
(`unity/Assets/AddressableAssetsData` · `unity/Assets/Audio`)指纹未变。

> 🔁 **M3 是判别力核心**:它把预答改成内联复制一份判据 —— **行为完全等价**,
> 8 组合 iff 矩阵 / 边界 / 纯函数性**全部保持绿**,只有 IL 面的方法引用相等会红。
> ⇒ 它证明「**行为等价**」与「**同源**」是两个不同属性,后者只能靠**符号面**断言。
> ⚠️ M3 首版替换文本括号不平衡导致**编译失败**(驱动退出码 6、无结果 XML),
> 已改为完整重写方法体(局部函数作第二实现)后重跑。**首次失败的记录不删**。

> 🔁 **M5 同理**:注入的 `MinQuality * 2` **行为上完全无害**(`amplitudeProbe < 0` 对合法品级恒假),
> 所有行为面夹具保持绿 —— 这正是 AC-18-05 必须用**符号面**判据的理由。

### 4.1 补跑的三条判别力探针(评审修复轮新增夹具的独立验证)

新夹具先经探针确认有判别力,才写进 spec:

| 探针 | 变异 | 结果 |
|---|---|---|
| **P1** | `AmplitudeOffenders` 的 `'*'` → `'+'` | `test_ac1805_minQuality_neverEntersAmplitudeExpression` **红** |
| **P2** | 停用 `ValidateQuery` 调用 | `defaultQuery_…` + `emptyRecipeTable_…` **2 红** |
| **P3** | `ISkillGrowthPort` 拆回独立声明 | `skillLevelReadFace_isSingleDeclaredSource` **红** |

> 🔁 **P1 是评审 T1 的直接证据**:同样的变异下,**初版正控不会红**(它只在字面量上跑 `IndexOf`),
> 修复后会。详见 `review-s20-4-2026-10-27.md` §二 T1 行。
>
> ⚠️ **P1 首轮留痕(不删)**:首版用 `sed` 施加变异**未命中**(`||` 转义),驱动照样报
> `total=29 failed=0 RED=[]` —— 那是「变异根本没应用」,不是「夹具假绿」。改用 python
> 精确替换(断言锚点出现次数 == 1)后重跑。这是「证伪轮六种假存活」里『**变异打错靶**』的活例。
>
> ⚠️ **P3 首轮留痕(不删)**:首版直接删 `: ISkillLevelQuery` ⇒ **编译失败**(无结果 XML)。
> 改为「删继承 + 重新声明 `QueryLevel`」这种能编译的形态后才得到干净的红。
> **编译失败不是判别力证据。**

### 4.2 历史(实现轮原始 6 条,留档不删)

M1 / M2 / M3 / M4 落点同上;**M5 / M6 原锚点 `ProcessingCandidateSet.cs:108`**
(评审修复轮在 `IsCandidate` 前插入了 `ValidateQuery`,行号漂移至 `:135`)。
实现轮结论:`6/6 恰如预期`。

### 不可证伪 / 不立变异的守卫(如实登记,禁假装覆盖)

| 守卫 | 为何不立变异 | 覆盖方式 |
|---|---|---|
| `Judge` 的两条 `ArgumentNullException` | 停用后抛 `NullReferenceException`,是**不同异常类型**,`Assert.Throws<ArgumentNullException>` 仍红 —— 不构成「守卫失效」形态 | 正向:`test_ac1820_nullInputs_failClosed` |
| `CandidateQuery` 构造器三条 null 检查 | 同上 | 同上 |
| `ProcessingCandidateSet.Evaluate` 的 `recipes == null` | 同上 | 正向:`test_ac1810_nullRecipes_throwsNotSilentEmpty` |

### 判别面受限的守卫(如实登记,勿读成全面验证)

- **M1 / M2** 各自只红「技能门」/「品级门」的判别面;两门**同时**不过时的次序语义由
  `skillGateRejectedBeforeQuality_soReasonIsSkillGate` 单独承担。
- **AC-18-05 的扫描面 = `Assets/Sim/**`**(123 个 `.cs`),**不含** `Sim.Contracts` 与消费侧装配。
  跨装配的使用点归 S20-8(processing-002)的 AC-18-02 结算路径扫描,不在本 spec 面内。

---

## 五、本轮自查抓到的一条假绿(留痕)

**不是变异抓的,是读代码抓的。**

`ProcessingCandidateSet.IsCandidate` 初版自写 `Judge(...) == AdmissionRejection.None`,
而 `AdmissiblePredicate.IsAdmitted` 的头注声称自己是「`AdmissionRejection.None` 的**唯一**比较处」。
**两者直接矛盾** ⇒ 「算不算过」有第二个定义点。当时 8 组合 iff 矩阵**全绿** ——
**行为等价掩盖了结构分叉**:改 `IsAdmitted` 的口径时,候选集会静默不跟随。

处置:改为经 `IsAdmitted`,并补两条夹具 ——
符号面(`test_ac1820_admittedIsTheSoleDefinitionPoint_acrossSimAssembly`)
+ IL 面(`test_ac1820_candidateSet_goesThroughIsAdmitted`)。**这两条正是该注释的判据化。**

**教训**:「唯一定义点」写进注释时须**当场 grep 该判据的全部出现点** —— **注释不是门**。

> 🔁 **本轮另一条同类教训(扫描器自身)**:`IsComparison` 首版只列了**值形** opcode
> (`ceq`/`cgt`/`clt`),而 C# 把 `if (a < b) return` 编译成 `blt`/`bge` **分支**形。
> 结果「包装方法零比较」那条**空转绿**。是**正控**
> `test_ac1820_ilScannerPositiveControl_seesComparisonInJudge` 抓出来的 ——
> 它断言 `Judge` 体内必须扫到比较(那里明明有两处 `<`)。
> 修正:改按**助记符前缀**匹配(Cecil 把长/短形做成不同枚举值,Debug 构建大量用 `_S` 变体,
> 白名单必漏)。

---

## 六、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 全量回归计数(三元组:2026-10-27 09:44 CST · S20-4 实现轮 · 51193cf)
python3 - <<'EOF'
import re
for f,label in [("unity/Logs/s20-4-full-editmode.xml","基线 09:27:31"),
                ("unity/Logs/s20-4-full-regression.xml","本次")]:
    t=re.search(r'<test-run[^>]*>',open(f,encoding='utf-8',errors='replace').read()).group(0)
    g=lambda k:(re.search(rf'{k}="([^"]*)"',t) or [None,'?'])[1]
    print(f"{label:12} total={g('total')} passed={g('passed')} failed={g('failed')} skipped={g('skipped')}")
EOF
# 期望:基线 total=5407 … / 本次 total=5409 passed=5374 failed=0 skipped=35

# ② 全名级差集(期望 新增 5 / 消失 0 · 非通过集 新增 0 消失 0)
python3 - <<'EOF'
import xml.etree.ElementTree as ET
def rd(f):
    r=ET.parse(f).getroot()
    return r, {tc.get('fullname') for tc in r.iter('test-case') if tc.get('result')!='Passed'}
o,os_=rd('unity/Logs/s20-4-full-regression.xml')
n,ns=rd('unity/Logs/s20-4-fix-full.xml')
print(f"基线 {o.get('total')}/{o.get('passed')}/{o.get('failed')}/{o.get('skipped')}")
print(f"本次 {n.get('total')}/{n.get('passed')}/{n.get('failed')}/{n.get('skipped')}")
print("新增", len(ns-os_), "消失", len(os_-ns), "(非通过集)")
EOF

# ③ AC-18-05 扫描面实测(期望 123)
find unity/Assets/Sim -name "*.cs" | wc -l
# ④ MinQuality 在 Sim/ + Sim.Contracts/ 的全部出现(期望 6 行:Recipe.cs 1 + AdmissiblePredicate.cs 5)
grep -rn "MinQuality" unity/Assets/Sim/ unity/Assets/Sim.Contracts/

# ⑤ AC-18-18 ② 真表覆盖度(期望 4 配方 {process:2, craft:1, build:1})
python3 -c "
import json,collections
d=json.load(open('assets/data/item_database_recipes.json',encoding='utf-8'))
rows=d.get('rows') or d.get('recipes') or []
print(len(rows), dict(collections.Counter(r.get('owner') for r in rows)))"

# ⑥ 证伪轮重跑(期望「══ 证伪轮通过:8 个变异全部恰如预期 ══」)
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/s20-4-admissible-predicate.json --keep-logs

# ⑦ 变异残留与还原判据(期望零残留 / 五文件 SHA 与 §七 一致)
grep -rn "FALSIFY-" unity/Assets/ || echo "零残留"
sha256sum unity/Assets/Sim/Processing/AdmissiblePredicate.cs \
          unity/Assets/Sim/Processing/ProcessingCandidateSet.cs \
          unity/Assets/Sim/Prescription/PrescribeFlow.cs \
          unity/Assets/Sim.Contracts/ISkillLevelQuery.cs \
          unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs
```

---

## 七、还原判据(SHA-256,可入库)

| 文件 | SHA-256(终态 · 评审修复轮后) |
|---|---|
| `unity/Assets/Sim/Processing/AdmissiblePredicate.cs` | `02900b1974df03523db91acb1a829886839ec1cd721cfc4e3bd202057e61480e` |
| `unity/Assets/Sim/Processing/ProcessingCandidateSet.cs` | `6c14d08fac2efd4d53e4856e9e0b59190c02fb57433997e1a441b1b0515f7219` |
| `unity/Assets/Sim/Prescription/PrescribeFlow.cs` | `42cbfc57634f07892edd01a23a5621604d1867472eb2269a5d4ba3eca1e8d5bd` |
| `unity/Assets/Sim.Contracts/ISkillLevelQuery.cs` | `efa9362f8249138227edc54d8ad169cd6e61d4417fabc6cde987ae216794c1a1` |
| `unity/Assets/Tests/EditMode/Processing/admissible_predicate_test.cs` | `041dc6fab753e4b161b179e9ca097beba0fbeb575d707a5243172e8c14bfa5e6` |

<details><summary>历史(实现轮终态,已失效,留档)</summary>

| 文件 | SHA-256 |
|---|---|
| `AdmissiblePredicate.cs` | `e9d793d11adf3a13c697d931fc26112b846fb7b7435fd2127bcfba14deb6318b` |
| `ProcessingCandidateSet.cs` | `218d1fb779d38f05bf0bfae4dd5d7cf4191318c014693a1e6431d484e5e38ad0` |

</details>

> **承 QA 签核 C-5(sprint-19)的教训**:上一轮的 `fz-s193/` 备份目录随会话清理,
> 使「SHA-256 逐字节还原」**过程不可复演**。
> 本件改用**可入库的替代判据**:驱动把起跑基线 SHA-256 打进输出(见 §六 ⑥ 的重跑),
> 且上表记下**终态**哈希 —— 任何人 `sha256sum` 即可验证「文件确实回到了被变异前的样子」。
> ⚠️ 判据强度如实登记:这验的是**结果**,不是**过程**。过程的可复演性由
> `tools/falsify/unity_falsify.py` 的 `restore_all()` + `sha256()` 在**同一命令内**保证
> (驱动 `:204-221`),不依赖任何留存目录。

---

## 八、残余与边界(禁借绿)

- **消费者仍为零**:`PreviewVerdict` / `TerminalVerdict` 今天只被夹具引用。
  客户端预答的真实接线归 42(呈现半边),主机终裁的接线归 **S20-8(processing-002)**。
  本件只落谓词与候选集形状,**不发明消费者**。
- **`ISingleFurnaceQuery` 的真实现归 S20-9**(processing-004 已 `done`,`FurnaceState.IsBusy` 已在盘);
  本件的夹具用的是 **Spy 桩**,不是真装配件。
- **AC-18-18 的两段记分**(禁合并成一条绿):
  - ① **判据机制已证** —— 合成集三段 + 负向夹具,6/6 绿(`recipe_owner_partition_test.cs`)
  - ② **真表覆盖度已证** —— 2026-10-27 复算:4 配方 `{process:2, craft:1, build:1}`,三子集非空
  - ⚠️ ② 只证「数据落在三个桶里」,**不证配方内容正确**(数值归数值轮 / 21a 内容批)
- **AC-18-10b(呈现半边)** 归 42,不在本件面内。
- **`TD-processing-07`(AC-18-15 的 `Seq` 发号维)与本件无关** —— 那是 S20-9 的账,本件不触碰发号器。
- **评审**:见 `production/qa/evidence/review-s20-4-2026-10-27.md`(同批落盘)。
