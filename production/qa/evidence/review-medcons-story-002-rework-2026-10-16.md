# 重评原件:story-002 返工件(`/code-review` 第二轮)

> **评审类型**:`/code-review` **重评**(read-only;本文件为该轮记录原件)
> **评审日期**:2026-10-16
> **评审对象**:`unity/Assets/Sim/Consequences/{Outcome53,TrialHistoryPredicate}.cs` ·
> `unity/Assets/Editor.Tools.Bake/{OutcomeTableBinder,OutcomeTable53,OutcomeTableCookedWriter}.cs` ·
> `unity/Assets/Editor.Tools.Gates/MedConsFloatGate.cs` ·
> `unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs` ·
> `production/epics/medical-consequences/story-002-deterministic-outcome-judge.md`
> **被评变更**:首轮裁定 CHANGES REQUIRED 后的返工件
> (首轮原件:`production/qa/evidence/review-medcons-story-002-2026-10-15.md`)
> **专家执行**:`unity-specialist`(引擎/C# 面)· `qa-tester`(测试面)—— 并行派发
> **本文件裁定**:**APPROVED WITH SUGGESTIONS**(引擎面)/ **GAPS**(QA 面)——
> 合并裁定 = **APPROVED WITH SUGGESTIONS**,QA 建议的 3 条补强**已全部落地并证伪**

---

## 一、原 14 项判据核账

| 编号 | 首轮级别 | 重评结论 | 证据 |
|---|---|---|---|
| R-1 | BLOCKING | **已结清** | `OutcomeTableCookedWriter.cs` 头 offset 8 写 `schemaVersion`;载荷首字段已删;`CookedCodec.cs:98-101` 读侧确实比对 |
| R-2 | BLOCKING | **已结清** | 两批证伪轮共 **18 条全 RED**(`s12-4-falsify.txt` 11 条 + `s12-4-falsify2.txt` 7 条) |
| R-3 | BLOCKING | **已结清** | `MedConsFloatGate.cs` 三形态浮点字面量正则 + `FindFloatLiterals` 纯函数;IL 级残余缺口如实登记 |
| A | WARNING | 已结清 | 乘子改逐表量 `bandCount`;`Materialize` 键碰撞显式抛;全序比较器 |
| B | WARNING | 已结清 | `inputs.UBand` 进查表;双档表证明档位可判别 |
| C | WARNING | 已结清 | 未命中回落 default 行(不再吐 `PatientLives`) |
| D | WARNING | 已结清 | 逐病人 run 字典;死参 `decoder` 已删 |
| E | MINOR | 已结清 | `u_band` 必填(短路先挡,不再 NRE) |
| F | MINOR | 已结清 | `SearchOption.AllDirectories` |
| G | MINOR | 已结清 | `BandOf` 除零守护 + `u_band_count` 上界 65536 |
| H | WARNING | 已结清 | token 单一真源留门侧,测试改读门数组 |
| I | WARNING | 已结清 | story 如实登记 3 处 AC 缺口(专家核实登记属实) |
| J | MINOR | **首轮部分结清 → 本轮已结清** | 首轮拆出的 `ParseRows` 仍 69 行;本轮再拆 `ParseDefaultRow` / `ParseCombinationRow` |
| R-4 | (返工新增) | 已结清 | `ReadSchemaVersion` 比对期望值 |

---

## 二、重评新发现的缺陷(全部已修)

### 引擎面(unity-specialist)

| 编号 | 级别 | 内容 | 处置 |
|---|---|---|---|
| **N-1** | WARNING | `RunAll` 内 `literalText` 死局部(与 `FindFloatLiterals` 重复造同一管线) | ✅ 已删(`ScanFile` 拆分时一并清) |
| **N-2** | WARNING | `RunAll` 49 行 / `ParseRows` 69 行,均超 40 行标准;`ParseRows` 圈复杂度 ≈12 | ✅ 已拆(`ScanFile` + `ParseDefaultRow` + `ParseCombinationRow`) |
| **N-3** | WARNING | **`TryLookup` 不校验 `uBand` 域 ⇒ 越界档位静默别名到相邻 (P,C,D) 行** | ✅ 已加域守护 + F16 证伪 |
| **N-4** | INFO | `Table` 构造器不校验 `uBandCount ≥ 1`(= 0 时全表键坍缩,字典静默丢 17 行) | ✅ 已加 + F17 证伪 |
| **N-5** | INFO | default 行的 `u_band` 键被静默忽略(非整数也无声通过) | ✅ 已加存在性 kind 检查 |
| **N-6** | INFO | 浮点字面量正则漏 C# 7.0+ **数字分隔符**形(`1_000f` / `1_000.5`) | ✅ 已补 `\d+(?:_\d+)*` + 正例入 F11 |
| **N-7** | INFO | `StripStrings` 不识 verbatim / 插值 / char 字面量 | **登记不修**(当前 3 文件无此形态;归入源级判据边界清单) |
| **N-8** | INFO | 测试用 `BitConverter.ToUInt32` 读 LE 载荷(平台序相关) | ✅ 改显式 `ReadU32Le` |
| **N-9** | INFO | 测试注释「词法层拒绝」为误(实际拒收在阶段 2 `ReadUBand`) | ✅ 已订正 |

### QA 面(qa-tester)—— 3 条建议补强(全部落地)

| 编号 | 内容 | 处置 |
|---|---|---|
| **QA-1** | `test_bake_missingCombination_hardFails` 走错分支 ⇒ **校验① 覆盖分支从未被执行**(`errors.Count == 0` 短路) | ✅ 新增 `test_bake_missingWholeRow_coverageValidationFires`(整行删除) |
| **QA-2** | **C 轴 `matched=true` 分支零正例载体**(`CareLaneOf(cares, true)` 从未被断言) | ✅ 新增 `test_careLaneOf_matchedTrue_yieldsAppliedMatched`(含端到端) |
| **QA-3** | **F1 子断言② 在现网数据下不可判别**(`schema_version == u_band_count == 1` ⇒ 旧布局下同样绿) | ✅ 改为**推导载荷长度**断言(旧布局会多 4 字节 ⇒ 可判别) |

QA 另报 **BandOf uRaw 域无守护**(负档序数)—— 与引擎面独立发现同源,✅ 已加 + F14 证伪。

---

## 三、第二批证伪轮(7 条新探针)

`unity/Logs/s12-4-falsify2.txt`;每条独立 `trap RETURN` 还原 + 全局快照 `EXIT` 还原;
跑完 `grep -rn FZ2MARK unity/Assets/` = **零残留**。

| # | 夹具 | 对应 | 停用 X | 实证 |
|---|---|---|---|---|
| F12 | `test_bake_missingWholeRow_coverageValidationFires` | 校验① | 停用覆盖循环 | ✅ RED |
| F13 | `test_careLaneOf_matchedTrue_yieldsAppliedMatched` | C 轴 | `CareLaneOf` 忽略 `matched` | ✅ RED |
| F14 | `test_bandOf_uRawBeyondDomain_doesNotReturnNegativeBand` | B | 删 uRaw 域守护 | ✅ RED |
| F15a | `test_oq539_nullAndEmptyStream_returnsFalse` | D | 删空态短路 | ✅ RED |
| F15b | `test_oq539_arrivalOrderPermutation_invariantResult` | D | 删全序键排序 | ✅ RED |
| F16 | `test_table_uBandOutOfRange_fallsBackToDefault_notAliasToNextCombo` | N-3 | 删 uBand 域守护 | ✅ RED |
| F17 | `test_table_uBandCountZero_rejected_notSilentRowCollapse` | N-4 | 删构造器校验 | ✅ RED |

**两批合计 18 条证伪探针,全部 RED。**

### ⚠️ F15b 的一处如实记录(判别性设计)

返工首版 F15b 的两组夹具在「排序后」与「到达序」下结论**相同** ⇒ 删掉排序也照样绿。
已改为:`J` 的 tick 大于前两条 `D` 而**小于**第四条 `D` ——
排序后 `D,D,J,D` ⇒ run=2 被打断、尾段 1 ⇒ **false**;到达序 `J,D,D,D` ⇒ J 先到(无 run 可断)、
随后连跑 ⇒ **true**。这样删排序必红。*证伪夹具本身也须可证伪。*

---

## 四、回归

| 轮次 | Passed / Failed / Skipped / Inconclusive |
|---|---|
| 首轮(S12-9 终轮) | 3444 / 3398 / 0 / 46 |
| 返工终轮(rework6) | 3455 / 3409 / 0 / 46 |
| 重评返工(rework7) | 3462 / 3414 / 0 / 46 —— 2 红 = 新测试自身缺陷 |
| 重评返工(rework8) | 3462 / 3415 / 0 / 46 —— 1 红 = 删行留双逗号致 JSON 非法 |
| **重评终轮(rework9)** | **3462 / 3416 / 0 / 46** |
| 证伪轮后复跑(rework10) | 见下 |

**增量**:+18 测试(11 返工探针 + 7 重评探针)⇒ **+18 / +18,零回归**。

**禁借绿**:IL2CPP 三格(AC-53-02/15)归 CI 执行,本机 **NOT-RUN**。

---

## 五、QA 面登记的剩余 Minor 批(不阻塞)

1. `golden-v1.txt` 的 `C01_degraded_uraw 32768` 是**孤儿行** —— 无测试读取,退化值由测试本地
   字面量钉(自证)。建议改为被测读取。
2. 「表值与 Python 参考实现同表」对 18 组合中的 **5 组合**只由测试内镜像 `FixtureOutcome`
   兜底,story 表述对这 5 行过强。行值已声明归数值轮,不构成 AC 级假绿。
3. AC-53-03 后半句「`FixParse` 为唯一数值解析入口(旋钮表)」无载体且未登记 —— 可辩称
   「结构上虚空」(表内字段是枚举名 / `int`,无 `Fix` 字段)。
4. 门的**构建期注册**层无夹具:`AssemblyGates.RunAll()` 确已接入(`AssemblyGates.cs:123`),
   且 `test_assemblyGates_runAll_zeroErrors_*` 端到端调用它,但该测试**检不出注册被删**
   (删了反而更空 = 仍绿)。同 b7 已建的「扫描面非空」防护同族。
5. `Materialize` 键碰撞硬失败分支(`internal`)无直驱载体(修正后构面不可达,纯纵深防御)。
6. `Judge.Evaluate(inputs, null)` 的 `ArgumentNullException` 无测试。
7. `FindFloatLiterals` 已改为取全部匹配(N-1 附带),但同形态多字面量的诊断完备性无测试。
8. 共享 `static readonly` `SharedPool`/`Encoder`/`Decoder` 跨测试无 teardown —— 对照
   `.claude/rules/test-standards.md`「never shared mutable state」属偏离(当前无断言依赖 blob id,风险低)。
9. EditMode 单元测试做真实文件 I/O + 依赖 `Application.dataPath` —— 违反「Unit tests do not
   call ... file I/O」;golden 读取可辩为必要,`LoadTable` 走 `dataPath/../../` 解析仓根在 CI
   检出布局变化时即红(返工期已因此红过一次)。

---

## 六、裁定

## **APPROVED WITH SUGGESTIONS**

**理由**:
- 首轮 3 条 BLOCKING **全部实质结清**,且每条都有可证伪夹具 + 已跑通的证伪记录。
- 10 条 WARNING/MINOR **全部结清**(J 在首轮只做一半,本轮补齐)。
- 七项硬约束(门 A / b2 / b6 / AC-53-12 / AC-53-03 / ADR-006 / ADR-014)**逐条实测成立**。
- 重评新发现 9 条引擎缺陷 + 3 条 QA 补强 + 1 条 BandOf uRaw **全部已修并证伪**。
- 剩余 §五 9 条为 Minor / 登记项,不阻塞转 Complete。

**未发现假绿**:两轮共 18 条证伪探针全部 RED;两处「夹具本身判别力不足」的案例
(F2 对非稳定排序、F15b 对删排序)**均被实证发现并已加固**,未留恒真式。
