# 评审报告原件 —— interaction-system Story 002(确定性全序目标选择)

**日期**: 2026-10-04
**对象**: `unity/Assets/Tests/EditMode/Interaction/target_selection_test.cs` ·
`unity/Assets/Tests/EditMode/Interaction/replay_selection_test.cs` ·
`unity/Assets/Gameplay.Presentation/Interaction/InteractionSelector.cs`
**评审形式**: 双代理(结构评审 + QA 评审),均**静态阅读**(未跑 Unity —— batch 需独占工程)
**原判定**: **两方均 REJECT**

---

## 1. 原判定(评审时点,未修)

### 结构评审 —— REJECT(3 BLOCKING / 1 MAJOR / 3 MINOR)

| # | 级别 | 落点 | 判定 |
|---|------|------|------|
| 1 | BLOCKING | `replay_selection_test.cs:161` | AC-4-14 三条测试经**测试本地** `TargetSelectionTest.ArgMin`,从不驱动生产 `InteractionSelector.Select` ⇒ 删掉生产 F-4.1 实现三条照绿(story-001「两台机器」缺陷复现) |
| 2 | BLOCKING | `target_selection_test.cs:326-343, 419-433` | int64 拓宽夹具**不可证伪**:extreme(id=2) 与 zero(id=1) 皆 Drop,正确/错误实现下胜者同为 id=1 ⇒ 断言文案「胜者反转」为假 |
| 3 | BLOCKING | `target_selection_test.cs:277-319` | 哈希序负夹具**空转**:`HashOrderWinner` 排列不变 ⇒ `.All(...)` ≡ 一次求值;且从不驱动生产 |
| 4 | MAJOR | `:147-150,163-165,179-181` | F-4.1b 三例未照抄(用 `patient_id=7`、Drop @ `(11,0,5)`;GDD 为 `17`、`(10,0,6)`)—— 故事原文要求「逐字一致」 |
| 5 | MAJOR | `replay_selection_test.cs` 全文件 | 两条故事 QA 案例缺失:「到达序打乱夹具」「缓存上一个目标负夹具」 |
| 6 | MINOR | `:93,101-103` | 硬编码闭集大小 `10`;`ContainsKey` 只守正表不守反序表 |
| 7 | MINOR | `:301-303` | `hashWinners` 计算后从未断言(死代码) |
| 8 | MINOR | `:365-480` | 生产对拍仅覆盖键①/③,键② 只作自洽断言 |

### QA 评审 —— REJECT(F-1…F-13)

| # | 级别 | 判定 |
|---|------|------|
| F-1 | MAJOR(初读时 BLOCKING) | AC-4-06 参考段断言绑**本地 helper**,非生产 |
| F-2 | MAJOR | AC-4-06(b) 生产断言对**同源表**自洽 ⇒ 恒真;未测 GDD 的 `Container(44)` |
| F-3 | MAJOR | 两文件各持一份 `DemoKindPriority`,AC-4-16 无法察觉单侧错字 |
| F-4 | MAJOR | 「200 次打乱」与两条负夹具空转(`ArgMin` 线性扫描与加入序无关) |
| F-5 | MAJOR | `Assert.IsFalse(hashIsFaithfulEverywhere)` 对任何 F-4.1 实现不可证伪 |
| F-6 | MINOR | AC-4-06 断言了归 story 006 的前置(4-DC-3) |
| F-7 | MINOR | 缺「全体出半径 ⇒ 空 ⇒ None」等 QA 案例 |
| F-8 | MAJOR | AC-4-14「缓存上一个目标」负夹具**完全缺失** |
| F-9 | BLOCKING | 三条 AC-4-14 重放测试为**无状态恒真**:本地 ArgMin 纯函数,二次运行必同;长序列输入只有 tick 变而 tick 不被读 |
| F-10 | MINOR | AC-4-16 出处「审阅」是手搓 3 条字典,非真夹具审计 |
| F-11 | MINOR | 交付物未跟踪 / Completion Notes 待填 |
| F-12 | MINOR | `:148` 误引 GDD 值(写 `patient_id=7`,GDD 为 17) |
| F-13 | MINOR | 非缺陷(完全性判据成立) |

---

## 2. 修复落点(逐条)

| 原判定 | 修复 |
|--------|------|
| 结构 #1 / QA F-1 / F-9 | `replay_selection_test.cs` **全部断言改经生产** `InteractionSelector.Select`:新增 `ProductionSelector` 适配 `ISelectorLike`;三条重放测试与负夹具均走该类。**删除**测试侧参考机器在断言路径上的一切参与 |
| 结构 #2 / QA F-4(半) | int64 夹具改为**命中陷阱的 Δ**:玩家置原点、extreme 置 `(int.MinValue,0,0)` ⇒ Δ = `int.MinValue` **恰**为 int 域 `Abs` 回绕为负的唯一条件(实测 `int.MinValue−1` 回绕为**正**,不触发,已订正);StableId 使胜者反转可观测。**变异测试验过**:去掉 int64 拓宽 ⇒ 该测试红 |
| 结构 #3 / QA F-5 | 哈希序负夹具重写为**驱动生产 + 穷举找鉴别组**:在固定候选池中搜寻「`GetHashCode` 序首 ≠ 生产(F-4.1)解」的一组,断言生产解 == F-4.1 键序胜者 **且 ≠ 哈希序胜者**。**变异测试验过**:键③ 改哈希序或删除 ⇒ 该测试红 |
| 结构 #4 / QA F-12 | F-4.1b 三例**逐字照抄** GDD(`patient_id=17`、B @ `(10,0,6)`、C @ `(11,0,5)`、B'' @ `(10,0,4)`);文件头出处校准块订正;`replay_selection_test.cs` 的 `source: GDD` 误引改 `manual:` |
| 结构 #5 / QA F-8 | 补齐两条故事 QA 案例:①「到达序打乱夹具」= `test_ac414_twoInterleavedInstancesDoNotDiverge` 用**两个独立生产实例**交错;②「缓存上一个目标负夹具」= `test_ac414_negativeFixture_cacheLastTargetImplementationDiverges`,`CachingSelector` 实现「就近偏好」(缓存命中即沿用、不重算),断言**复用实例**跑「正序预热 + 反序」与新鲜实例结果**不同**,而生产同构跑法结果**相同** |
| 结构 #6 / QA F-6 / F-3 | 闭集大小改 `Enum.GetValues(...).Length`;反序表同守成员完整性;`DemoKindPriority` 重复副本在 replay 侧**删除**(该侧不再需要);AC-4-06 前置(4-DC-3)责任边界在文件头与断言文案中显式声明归 story 006 |
| 结构 #7 | `hashWinners` 死代码随负夹具重写一并移除 |
| 结构 #8 / QA F-2 | F-4.1b (b) 两表分歧**显式登记**:新增 `test_ac406_slippedProductionKindLiterals` 断言生产(枚举序)解 `B(812)`、GDD 演示表解 `C(44)`、**二者必不同** ⇒ 真表落地后本断言失效并由 story 006 取代 |
| QA F-4(半)/F-10 | 「200 次打乱」改经生产实例;**表出处负夹具**保留可红形态(缺出处即红) |
| QA F-7 | 「全体出半径 ⇒ None」为**裁剪**面,归 story 003/004(故事 Out-of-Scope 明写);在 story Completion Notes 记为**刻意缺口**,不借绿 |
| 生产(评审外发现) | `InteractionSelector` 第一键**改测 `d∞(playerCell, cell)`** —— 原实现测「到原点」,与 F-4.1 判定式 `d∞(player_cell, cell(c))` 不符,是 F-4.1b 三例全部失败的真因。属本故事(**story 002 = 全序本体**)正管面 |

---

## 3. 验证命令与结果(可证伪)

```
# 基线
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Interaction" \
  --output unity/Logs/interaction-s002-final.xml
⇒ BoundaryDisciplineTest 24/24 · ReplaySelectionTest 4/4 · TargetSelectionTest 13/13 = 41/41 green

# 变异测试(逐项证明测试可红 —— 反空转证据)
MUT1 删 int64 拓宽(改 int 域相减)        ⇒ 红:test_ac406_chebyshevWidensToInt64BeforeAbs
MUT2 d∞ 改从原点量                        ⇒ 红:test_ac406_exampleA_* + replay 负夹具
MUT3 删键③ StableId                       ⇒ 红:test_ac406_exampleC_* + 哈希序负夹具
MUT4 键③ 改 GetHashCode 序                ⇒ 红:test_ac406_exampleC_*
MUT5 删键② KindPriority                   ⇒ 红:exampleB / transitive / slippedLiterals / shuffled 四条
（每项变异后生产已还原,`diff` 与备份一致）
```

**结论**:八个 BLOCKING/MAJOR 级原判定全部有对应修复,且修复后经**变异测试**证明夹具具备可证伪性(不再空转)。转 ACCEPT 的关闭条件(a)–(e)均由上表落点满足。
