# 补做评审原件 —— case-system story-003(结案前置·处置证据窗口·快照)

**评审时点**:2026-10-15(**补做评审** —— 评当下代码,承 `.claude/docs/review-workflow.md` §义务 3 不可追补)
**被评对象**:`unity/Assets/Sim/CaseCloseDecider.cs`(含 `CaseCloseQuery` / `TreatmentKinds`)+ `unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs`
**轮次**:双代理(unity-specialist 代码面 + qa-lead 测试面)
**原件缺口确证**:`review-case-system-001-004-gap-audit-2026-10-15.md` §二(003 判定分级未能确证)
**commit 自述(31e3f9b)的处置**:「核心修复:IsClosed/IsOpen 匹配用 Patient 而非 Tick/Seq」被当作**待验证主张**独立复核 → 见 B-1(**该修法只对单案病人成立**)。

---

## 一、逐条 AC 判定(代码面)

| AC | 判据在何处 | 判定 | 备注 |
|---|---|---|---|
| **AC-37-04** 未勾 ⇒ 拒绝 | 实现 `:54-55`;测试 `:52-69` | ✅ | 真判据(调生产 `Decide`);未显式断言发事件数 = 0 |
| **AC-37-21** 已勾但窗口内无处置 ⇒ 拒绝 | 实现 `:58-59,99-113`;测试 `:76-136` | ✅ | 窗口方向为订正后式;复诊后门测试 `:87-110` 是**有效回归哨兵**(旧式下必红) |
| **AC-37-03** 同 case_id 第二条 CaseClosed 幂等 | 实现 `:50-51`(Decide);测试 `:143-166` | ⚠️ Decide ✅ / **流层 ❌** | 流层经 `IsClosed` 的真实路径被 **B-1 破坏**;测试 `:144-150` 传入 `isOpen=true ∧ isClosed=true` **不可达状态**,绕开真实流 |
| 窗口边界闭区间 | 实现 `IsInWindow:175-178`;测试 `:172-216` | ✅ | 四条边界夹具齐(==opened 计 / ==close 计 / <opened 不计 / >close 不计) |
| **CaseClosed 载荷** `{disease_set, treated}` | 类型 `Sim.Contracts/Payloads/CasePayloads.cs:23-34`;测试 `:262-302` | ✅ | 反射断言字段存在性 + 类型;与 ADR-008 §三 载荷表逐字一致 |
| **AC-37-18** 死亡未结仍可结;病名可空 | 实现 `IsOpen:147-166`;测试 `:309-350` | ⚠️ 部分 | 「死亡」未建模;`:327-350` 硬编码 `Decide(...)` 与 `disease_set` **无耦合** ⇒ 近恒真 |
| 结案副作用序列(→8 清除 / →53 冻结 / 37 不产生后果) | 测试 `:356-374` | ⚠️ 部分 | 仅证「CaseClosed → 病例流」;其余三项无判据 |

---

## 二、判定清单

### BLOCKING(1)

**[B-1 · 实现与 GDD/ADR 冲突 + 扫错对象] `CaseCloseQuery.IsClosed` 匹配键过宽 —— 复诊新案被误判「已结」,复诊案无法结案**

- **范围**:`unity/Assets/Sim/CaseCloseDecider.cs:121-139`(尤 **`:135`**)
- **代码证据**:`:135` = `if (e.Patient.Value == caseId.Patient) return true;` —— 把「该病人**曾有过**任何 `CaseClosed`」当作「**本** case_id 已结」,匹配粒度从 `case_id` 退化为 `Patient`;作者注释 `:129` 已自承「实际生产代码应通过 PayloadRef 解码 case_id 匹配」⇒ **未完成态,非等价实现**
- **行为证伪(本机已执行,dotnet 8 逐字复刻循环体)**:流 `[Opened@100, Closed@200, Opened@300]`(病人 1)⇒
  ```
  revisit new case  -> IsOpen=True  IsClosed=True    ← 自相矛盾
  first closed case -> IsOpen=False IsClosed=True
  ```
  代入 `Decide`(`:46-51`,`isClosed` 命中 `:50-51`)⇒ **`RejectAlreadyClosed`** ⇒ **复诊案永远无法结案**
- **反噬面**:AC-37-21 的 Edge 测试「复诊窗口内补处置 → 放行」(`case_close_precondition_test.cs:112-136`)因 `:133` 硬编码 `isClosed:false` 而**假绿**
- **冲突落点**:GDD `case-system.md:250-256`(规则六-4,幂等定义 = **同一 case_id**)/ `:667`(F-37.2)/ `:818-819`(EC-37-1 复诊「新病例,case_id 三元组不同」);story 自身 `:49`(Implementation Note #3「幂等判据 =『**该案**已有 CaseClosed』」)
- **修复落点(归新一轮)**:`IsClosed` 以 `CaseId` 三元组精确匹配 —— 经 `PayloadRef` 解码 `CaseClosedPayload.CaseId`(`CasePayloads.cs:26`)判等;或 `IsOpen`/`IsClosed` 统一为「该案 `CaseOpened` 与其后属于该案的 `CaseClosed` 之间」扫描;并**同批**补流层判据(`IsClosed(revisit) == false`)
- **缓解事实(供分级裁量)**:**当前无生产调用点** —— `CaseClosed` 写者未落(`Editor.Tools.Gates/WriterExistenceGate.cs:87` 具名豁免自陈「CaseCloseDecider 现为纯函数」);故为**交付契约层缺陷**,非当前运行期故障。但后续结案写者 story 一旦消费 `IsClosed`,复诊案即不可结案

### ADVISORY(7)

- **A-1** `TreatmentKinds.Set` 是 `public static readonly` **可变 `HashSet`**(`:72-77`)—— `readonly` 只锁引用,集合内容全仓可写,破坏确定性。建议 `private` + `Contains` 出口或 `IReadOnlySet`
- **A-2** 处置集成员权威未钉死:集合 = `{EmergencyTreatmentApplied, DrugTreatmentApplied, InjuryOnset}`(`:74-76`);story Note #1(`:47`)要求「`InjuryOnset` 是否算处置 → 以 ADR-008 §四 原文与 53 GDD 为准,**不自行扩集**」,而 ADR-008 §四 未枚举任何处置 Kind(`adr-008:205-217`)。`InjuryOnset` 在 `disease-simulation.md:174` 语义是**受伤**非处置。建议补权威出处或收窄
- **A-3** AC-37-03 测试构造不可达状态(`:144-150`)⇒ 无法覆盖流层幂等,**掩盖 B-1**
- **A-4** AC-37-18 两条测试恒真化(`:327-350`):`Decide` 无 `disease_set` 形参
- **A-5** 「37 自身不产生后果事件」+ 副作用序列(→8/→53)无判据
- **A-6** `IsClosed`/`IsOpen` 对同一形参 `caseEvents` **隐含不同输入契约**(`:128` 注释「该案子集」vs `:152-153` 整表三元组查找)—— 是 B-1 的土壤
- **A-7** 流程性:story 记 Complete 无报告路径可引用 ⇒ 义务第 2 条「借绿」形态

---

## 三、正面核实(记录)

- **窗口方向 ✅ 正确**:`CaseCloseDecider.cs:108` = `if (e.Tick < openedTick || e.Tick > closeTick) continue;` ⇒ `e.Tick ∈ [opened, close]`,与 GDD `:667`(F-37.2 订正式)/ `adr-008:156`(§四 订正式)/ AC-37-21 一致。**未复现 ADR-008 §四 的旧式反写错误** —— 这是本 story 技术核心之一,确认落地
- 结案四铁律:唯一路径 ✅(无 Update/协程/计时器)· 前置 ✅ · 单例 ✅(形参只容单案布尔)· 不可撤销 ⚠️(流层被 B-1 破坏)
- 规则七「永不自动结案」✅ 结构满足
- 架构纪律:纯函数 ✅ · 门 A ✅(`:9-11` 仅 System/BCL/Sim.Contracts)· 零 float ✅ · 命名 ✅

---

## 四、验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) 静态复核两核心(期望 :108 方向正确;:135 仅比 Patient = B-1)
sed -n '95,113p;121,139p;147,166p' unity/Assets/Sim/CaseCloseDecider.cs
# 2) B-1 行为证伪(dotnet 8 复刻循环体):流 [Opened@100,Closed@200,Opened@300]
#    实际输出: revisit new case -> IsOpen=True IsClosed=True   ← 自相矛盾 ⇒ B-1 成立
# 3) 权威判据(Unity UTF):追加 test_b1_revisitNewCase_notClosed 后跑
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath unity -runTests -testPlatform EditMode \
  -testFilter DaYiJingCheng.Tests.CaseSystem.CaseClosePreconditionTest \
  -testResults unity/Logs/case003-repro.xml -logFile unity/Logs/case003-repro.log
#    断言 IsClosed(revisit) == false 当前应失败 ⇒ 反证 B-1
# 4) 无生产调用点佐证
grep -rn "CaseCloseQuery\|CaseCloseDecider" unity/Assets --include=*.cs | grep -v Tests
#    期望:仅 WriterExistenceGate.cs:87 + CaseOpenWriter.cs 注释
```

---

## 五、修复归属与边界

- B-1 修复**归新一轮**;修复后须**同批**补 A-3 的流层幂等判据(否则同盲区复发)
- story-003 状态须标注「原件为补评件」

---

# 六、测试面半边(qa-lead · 2026-10-15 · 同轮合并)

**取证归属(强制转述)**:13 号证据 —— `unity/Logs/full_editmode_story003_final.xml:650` 记该 fixture `testcasecount="33" passed="33" failed="0"`(2026-10-06 15:21:25Z);测试文件 mtime = 10-06 09:35,**早于**该次运行 ⇒ 该运行覆盖当前文件内容,33 条**确实全绿**。但**时效为 2026-10-06**,**不能**充作 2026-10-15 的当下证据(且此绿正是 B1/B2/B5 的**假绿**形态)。
⚠️ 该 XML 全部 2916 个 `test-case` 的 `asserts` 属性**恒为 `"0"`**(Unity UTF 不填该字段),**不得**据此读作「零断言」。

## 六·一 逐条 AC 判定(测试面)

| # | AC / 断言点 | 覆盖测试 | 判定 | 断言强度 |
|---|---|---|---|---|
| 1 | AC-37-04 未勾 ⇒ 拒 | `T:52-59`、`T:62-69` | ✅ | 中 —— 直调 `Decide`,输入全字面量;未走事件流 |
| 2 | AC-37-21 决策表行 | `T:77-83` | ✅ | 中 |
| 3 | **AC-37-21 复诊后门负例** | **`T:88-110`** | ✅ | **高 —— 全文件唯一把生产 `HasTreatmentInWindow` 真求值(`T:102`)与 `Decide` 串联的用例** |
| 4 | AC-37-21 正向边 | `T:114-136` | ✅ | 高 |
| 5 | AC-37-03 幂等(决策表) | `T:143-150` | ✅ | 中 |
| 6 | AC-37-03「同 tick 双意图」 | `T:154-166` | ⚠️ | 低 —— **名实不符**:无同 tick 夹具、无第二条事件,`T:163` 的 `isClosed:true` 是手写常量 |
| 7 | AC-37-03 流前缀真幂等 | `T:394-403` + `T:449-490` | ✅ | 高(步骤 4-5 真把 `CaseClosed` 加进列表再查) |
| 8 | 窗口边界 `e.Tick == CaseOpened.Tick` | `T:174-178` + **`T:494-517`(真实谓词路径)** | ✅ | 高 |
| 9 | 窗口边界 `e.Tick == CloseTick` | `T:182-186`(**仅 `IsInWindow`**) | ⚠️ | 中 —— **载体错位**(见 A1);真实路径无此端夹具 |
| 10 | 窗口边界两端外(99/201) | `T:190-202` | ✅ | 高(同样存在 A1) |
| 11 | `CaseClosed` 载荷形状 | `T:264-302`、`T:367-374` | ❌ | 极低 —— 5 个测试**重复同一反射事实**(字段存在),**零行为断言** |
| 12 | `disease_set` 可空 | `T:340-350` | ❌ | 极低 —— `T:344` 是构造器恒真;空集从未进入 `Decide`、从未进入载荷 |
| 13 | **结案后新发病不追溯改写旧案(EC-37-2)** | **无** | ❌ NOT-RUN | 无 |
| 14 | AC-37-18 死亡未结 ⇒ 可结 | `T:310-325` | ❌ | **无 —— 夹具不含任何死亡输入,断言与命题不符** |
| 15 | AC-37-18 病名为空可结 | `T:329-336` | ❌ | 极低 —— 「空名」根本不在夹具里 |
| 16 | AC-37-18 纯伤情空集 | `T:340-350` | ❌ | 极低 —— 同 #12 |
| 17 | 副作用:`CaseClosed` 入病例流 | `T:357-363` | ⚠️ | 低 —— 只查 `StreamRouting` 路由表,**不查「结案时真的 Append 了」** |
| 18 | 副作用:读数清除(→8)/ 判断冻结(→53)/ 37 自身不产生后果 | **无** | ❌ NOT-RUN | 三件中两件在测试文件内**零出现** |
| 19 | 处置集白名单 | `T:224-256` | ❌ | 见 B5 |

**统计**:33 个 `[Test]`;其中 **16 个(48%)不构造任何 `SimEvent`**,输入全为字面量。**无 `[Ignore]`、无 `Assert.Pass` 桩、无测试自造的 decider 副本。**

## 六·二 测试面 BLOCKING(5)

| # | 判定 | 证据 |
|---|---|---|
| **B1** | **复诊后门在「流前缀层」被重新打开 —— 且测试恰好在同一夹具上规避了会变红的那一行断言** | `CaseCloseDecider.cs:121-139` 尤 `:135`(`if (e.Patient.Value == caseId.Patient) return true;`)。**可证伪反例**:流 `[Opened(100,1,0), Closed(200,1,1), Opened(300,1,2)]` 上 `IsClosed(new CaseId(300,1,2))` ⇒ 返回 **true**。`P:130-134` 的辩护「同一病人同时至多一个开案」只约束**同时开**的案件数,**不约束「已结一案 + 新开一案」**——而后者正是本 story 存在的理由。测试侧缺口:`T:432-443` 复诊夹具**只断言 `IsOpen`**;`T:394-416` 的 `IsClosed` 用例全部使用单案流。**在同一夹具上补一行 `IsClosed(newCaseId) == false` 即转红** |
| **B2** | **快照半场零行为覆盖 ——「Complete」属借绿** | story §AC 第 5 条承诺 `{disease_set(集合,可空), treated}`;测试全部证据是 5 处反射**字段存在性**检查(`T:264-302`、`T:367-374`)。快照计算在生产侧**不存在**:`WriterExistenceGate.cs:87` 明示 `("CaseClosed", "37 结案写者未落 …… CaseCloseDecider 现为纯函数(2026-10-10)")`。连带:`CaseCloseDecider`/`IsClosed`/`IsOpen` 生产中**零消费者**(全仓 grep 仅 `CaseOpenWriter.cs:18` 注释 + `WriterExistenceGate.cs:87` 字符串)⇒「结案前置」目前不被任何写入路径强制 |
| **B3** | **AC-37-18 三条用例的夹具均不含该 AC 的输入(恒真断言)** | `T:310-325` 夹具仅一条 `CaseOpened`,**无任何死亡输入**,却断言「病人死亡但案未结 ⇒ 可正常结案」。删掉 `T:314` 的 `CaseOpened` 即转红 ⇒ 实际测的是「有 CaseOpened ⇒ CanClose」,与「死亡」正交。`T:329-336` 的「病名为空」不在夹具里。GDD `case-system.md:1101` 将该 AC 标为 **`[L]` 单测 · BLOCKING**。可行性注:病人在事件流中是否可表态(`EventKind.cs:55` 只有 `PlayerDied`)需权威澄清;**若不可表态,正解是记 NOT-RUN 并说明**,而非断言一个与命题无关的常量 |
| **B4** | **结案副作用序列:三件中两件零测试** | AC 要求「读数清除(→8)+ 判断冻结(→53)+ `CaseClosed` 入病例流」。测试文件内 `→8` 与 `→53` **零出现**;唯一相关的 `T:357-363` 只验路由表,非副作用发生 |
| **B5** | **`TreatmentKinds` 收录 `InjuryOnset`,且测试把它断言成正确 —— 规格无授权,收录方向与语义相反** | 生产 `CaseCloseDecider.cs:72-77`;测试 `T:240-244` `Assert.That(TreatmentKinds.Contains(EventKind.InjuryOnset), Is.True, "处置集须含 InjuryOnset(25 格斗)")` —— **期望值唯一真源 = 生产代码自身的默认值**。权威面**无任何一处**枚举处置集(ADR-008 §四 `:191`/`:213` 只写 `∃ treatment event e ∈ 病史流`;GDD F-37.2 `:666`、规则六-2 `:254`、`tr-registry.yaml:188` 同样不枚举);story `:47` 明令「**不自行扩集**」。`InjuryOnset` 语义 = 25 的战斗伤害结算 ⇒ **「病人挨了打」被计入「已被处置」** ⇒ 玩家把病人打到即可满足结案前置,正面击穿 `case-system.md:567`/`:839`「玩家空手连关三例 ⇒ 不可能」。**反差点(加重)**:同集**正确排除**了 `EmergencyAttempt`(判定输入 ≠ 结算,ADR-009 Amendment I)✅,却**收进了**伤害事件;而 `CareApplied`(8 的护理动作,病史流,`EventKind.cs:21`)**未收录** —— 取舍方向与语义相反。现状缓解:25 的 `InjuryOnset` 写者同样未落 ⇒ 尚不可实际触发;但测试**锁死了该定义**,属「规格与测试同源」的假绿 |

## 六·三 测试面 ADVISORY(6)

- **A1(载体错位)** `CaseCloseQuery.IsInWindow`(`:175-178`)生产中**零调用点**(grep 全仓仅 `T:176/184/192/200`);真实谓词 `HasTreatmentInWindow` 在 `:108` **自行内联**同样比较 ⇒ `T:174-202` 四个边界用例测的是「未被任何生产路径使用的方法」;`CloseTick` 端在真实路径上无夹具
- **A2** AC-37-03「同 tick 双意图」名实不符(见判定表 #6)
- **A3(变异敏感度分布不均)** `Decide` 四分支中 **`RejectNotOpen`(`P:47`)零测试** —— **删掉 `if (!isOpen)` 行,33 个用例全绿**。整个文件没有一条「案件流 + 病史流 → `Decide`」的端到端组装用例
- **A4** `T:114-136` 复诊正向用例窗口上界用臆想值 `now=400`,流里并无对应 `CaseClosed`。P0 纯函数下可接受,但非真实夹具
- **A5(traceability 不一致)** story `:13` 称 TR-case-016「本 story 兑现机制层」,而 `tr-registry.yaml:126-132` 仍为 `status: gap`;TR-case-026(`:213-219`)为 `partial`。**本评审不追认该自述**
- **A6** `CaseCloseDecider` 生产零消费者(见 B2)。按 §Out of Scope 不构成本 story 的 BLOCKING,但**必须**在收口记录里登记为残余,否则「结案前置已交付」是纸面判据

## 六·四 测试面正面发现(应保留)

1. **AC-37-21 复诊后门负例(`T:88-110`)是本 story 立项根因的真哨兵**:若把窗口方向改回旧式(`openedTick ∈ [e.Tick, closeTick]`),`T:102` 的 `HasTreatmentInWindow` 会返回 `true`,`T:107-109` 随之转红。具备证伪力,且是文件内**唯一**把生产谓词与服务决策串起来的用例。ADR-008 §Validation `:472` 那条未勾判据在此**实际已被覆盖**;
2. 被测对象确为生产类型(无 `Decide` 的测试侧副本),无 `[Ignore]`、无桩、无 `Assert.Pass`;
3. 处置集**正确排除** `EmergencyAttempt`(ADR-009 Amendment I 的「判定输入 ≠ 结算」)。

## 六·五 测试面验证命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# V-1 复跑本 fixture(基线) —— 先确认编辑器已关
pgrep -f Unity; ls unity/Temp/UnityLockfile 2>/dev/null
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath unity \
  -runTests -testPlatform EditMode -testFilter DaYiJingCheng.Tests.CaseSystem.CaseClosePreconditionTest \
  -testResults unity/Logs/review-story003-2026-10-15.xml -logFile unity/Logs/review-story003-2026-10-15.log
#    期望:testcasecount="33" passed="33" failed="0"

# V-2 证伪 B1 —— 在 T:441 之后追加三行:
#   Assert.That(CaseCloseQuery.IsClosed(caseEvents, newCaseId), Is.False,
#       "复诊新案(300,1,2)不得因首案的 CaseClosed(200,1,1)被判为已结");
#    预期:该断言失败(IsClosed 返回 true)⇒ B1 成立。若通过 ⇒ 撤回 B1。

# V-3 证伪 B5 —— 注释 CaseCloseDecider.cs:76 的 EventKind.InjuryOnset,再跑 V-1
#    预期:test_treatmentKinds_containsInjuryOnset 失败 ⇒ 证明该测试期望值唯一真源是生产代码自身

# V-4 证伪 B3 —— 删除 T:314 的 CaseOpened(或改 T:322 的 isOpen:true → false)再跑 V-1
#    预期:T:323 断言失败 ⇒ 证明该用例敏感的是「有无 CaseOpened」,与「病人死亡」无关

# V-5 证伪 A3 —— 删除 CaseCloseDecider.cs:46-47 的 if (!isOpen) return RejectNotOpen; 再跑 V-1
#    预期:33 条全绿 ⇒ A3 成立

# V-6 全量回归
/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath unity \
  -runTests -testPlatform EditMode -testResults unity/Logs/review-story003-full-2026-10-15.xml \
  -logFile unity/Logs/review-story003-full-2026-10-15.log
```

## 六·六 测试面收口判据

story-003 的 Test Evidence 门(Logic 类 = BLOCKING)**不通过**。B1/B2/B3/B4/B5 **全部落在 story §Acceptance Criteria 明文列举的条目上**(复诊后门、快照、AC-37-18、副作用序列、处置集),**无一条属 Out of Scope**。
建议:以本报告为**新判定**(不追认 2026-10-06 的 Complete),补 B1 的反例断言、B5 的权威裁定(或撤 `InjuryOnset`)、B3 的死亡输入或改记 NOT-RUN、B2/B4 实装后再评,方可重开 `/story-done`。

**合取判定(story-003)**:**代码面 1 BLOCKING(B-1)+ 测试面 5 BLOCKING(B1–B5),去重后核心 5 类** ——
① **`IsClosed` 按 `Patient` 匹配**(B-1 ≡ 测试面 B1,两面同证,含 dotnet 8 行为证伪);② 快照半场零行为覆盖(B2);③ AC-37-18 恒真夹具(B3);④ 副作用序列两件零测试(B4);⑤ **`InjuryOnset` 入处置集无规格授权且语义相反**(测试面 B5,独家且最重)。
**Test Evidence 门不通过。**

---

# 七、勘误(2026-10-15 落盘后复核追加 —— 本段修正上文,不删原判定)

**触发**:同 story-002 勘误(见 `review-case-system-story-002-retro-2026-10-15.md` §七·一)——
补评代理只核 story §Test Evidence 声明的文件,却使用全域否定表述。复核已扫全树(含 PlayMode)。

## 七·一 被推翻(1 条)

**[测试面 B4「结案副作用序列三件中两件零测试」] → 推翻**

原判定称「`→8` 与 `→53` 在测试文件内**零出现**」并据此判 BLOCKING。
实测:该两件的判据在 **8 自己的测试件**里,只是不在 37 的证据文件里 ——

`unity/Assets/Tests/EditMode/DiagnosisSystem/reading_state_test.cs`(覆盖清单自陈
「AC-8-21 三态可分 · AC-8-22 不改口 · AC-8-24 恰四态/恰三态 · **AC-8-25 快照冻结**」)
的 `test_...`(读取区段 `:395-410`)**逐条断言**:

| 原判定称「零测试」的件 | 实际判据 | 行 |
|---|---|---|
| **→8 读数清除** | `Assert.AreEqual(SignReadState.Blank, DiagnosisReadingFsm.OnCaseClosed(s), `「任意读数状态关病例 ⇒ 清除」`,`foreach` 遍历 `Enum.GetValues(typeof(SignReadState))` 全状态 | `:409-411` |
| **→53 判断冻结** | `Assert.IsTrue(r.FrozenJudgment.Frozen, "② 判断冻结")` + `Assert.AreEqual(JudgmentState.Suspected, r.HandoffTo53, "③ 交 #53 = 冻结时刻的判断状态")` + 竞态断言「冻结后落笔必拒」 | `:395-403` |
| **37 自身不产生后果事件** | `CaseSecrecyDisciplineTest.test_case_ac37_20_noConsequenceOwnership`(断言 5 个出站载荷零 `consequence`/`treatment_content`/`outcome`/`penalty` 等越权字段) | — |

⚠️ 附带澄清:`→53 medical-consequences` 目前**仅有 GDD**(`design/gdd/medical-consequences.md`),
**无代码、无测试**(P0 未起首,承 S11-5)。故 `HandoffTo53` 在 8 侧只到「交出冻结状态」为止 ——
该 AC 的载体在 **8 侧**,**不应由 37 的证据面承担**。

## 七·二 仍成立(保留)

| 判定 | 复核结论 |
|---|---|
| **B-1**(`CaseCloseQuery.IsClosed` 按 `Patient` 而非 `case_id` 匹配) | **成立**。实现层缺陷,与测试面无关;代理另用 dotnet 8 逐字复刻循环体行为证伪(`IsOpen=True ∧ IsClosed=True` 自相矛盾)。当前无生产调用点(`WriterExistenceGate.cs:87` 具名豁免自陈「CaseCloseDecider 现为纯函数」)⇒ **契约层缺陷** |
| **B2**(快照半场零行为覆盖) | **成立**。已复核 003 的全部 `CaseClosed` 相关测试名(`caseClosedPayload_containsCaseId`/`containsDiseaseSet`/`containsPatientId`/`containsTreated`/`caseClosedPayloadHasSnapshot`/`caseClosedGoesToCaseStream`)—— **全为反射字段存在性 + 路由表**,零行为断言。且「快照计算」在生产侧确实不存在(`WriterExistenceGate.cs:87`) |
| **B3**(AC-37-18 夹具不含死亡输入 ⇒ 恒真) | **成立**。声明件自身性质,他件不影响 |
| **B5**(`TreatmentKinds` 收录 `InjuryOnset` 无权威授权) | **成立**(且**加重**):复核新发现的 `case_open_writer_test.cs` / `judgment_writer_test.cs` 均为真装配件,而**处置集的定义仍只在 `CaseCloseDecider.cs:72-77`** —— 三处权威件(ADR-008 §四 / GDD F-37.2 / registry)**均不枚举** ⇒ 「期望值唯一真源 = 生产代码自身默认值」的形态不变 |
| **TA-1/TA-3/TA-4/A1…A6** | **成立**(均为声明件自身性质) |
| **正面发现**(窗口方向 `:108` 正确 / `:84-110` 复诊后门负例是真哨兵) | **成立不变** |

## 七·三 净结论(修正后)

- 代码面 1 BLOCKING → **保留 1**(B-1);
- 测试面 5 BLOCKING → **保留 4**(B1/B2/B3/B5),**推翻 1**(B4);
- **门结论不变**:story-003 **Test Evidence 门仍不通过**(B-1 是最重的一条 —— 真代码缺陷)。
- **修复面收窄**:B4 划去;B2 的修复须与「结案写者落地」同批(否则快照无载体可测)。
