# 修复证据原件 —— S11-7 / case-system story-002 BLK-8(`LastJudgmentOf` 死参 + 墨龄取错病人实害链)

**日期**:2026-10-15
**对象**:`unity/Assets/Sim/CaseOpenDecider.cs`(`CaseStreamQuery.LastJudgmentOf`,**生产代码**)
+ `unity/Assets/Gameplay.Boot/CasebookVitalsSource.cs`(注释面)
+ `unity/Assets/Tests/EditMode/CaseSystem/case_open_and_judgment_test.cs`
+ `unity/Assets/Tests/EditMode/Boot/ink_age_test.cs`
**来源判定**:`production/qa/evidence/review-case-system-story-002-retro-2026-10-15.md` §二 **BLK-8**(BLOCKING)
**排期**:`production/sprints/sprint-11.md` **S11-7**(用户裁定「必须项优先」并入;估 0.75 天)
**性质**:本件 = **修复证据**,**不是评审**(承 `.claude/docs/review-workflow.md` §义务 3 不可追补)。

> ⚠️ 承 S11-8 / S11-10 / S11-11 的同一纪律:原始 XML 落在 `unity/Logs/`(**`.gitignore:196`**)
> ⇒ **易失中间产物**,不得充当交付证据。故判据、命令、输出原文收进本件,使其**自洽可复核**。

---

## 一、原缺陷判定(逐字承补评件 §二 BLK-8)

> `LastJudgmentOf` 的 `caseId` 形参**从未被使用**;⚠️ **实害已存在**
> `CaseOpenDecider.cs:110-125`(`caseId` 体内零引用,注释自承「P0 简化:调用方负责传入该案的事件子集」);
> `test:455-468` 输入为单案升序 ⇒ 跨案/乱序缺陷不可见。**实害**:`CompositionRoot.cs:245-246` 把
> `stream.Events`(`EventStream.cs:65` = 全部混合流)传给 `CasebookVitalsSource`,其 `InkAgeTicks`
> (`:221-224`)据 `LastJudgmentOf(_caseStream, openCase.Value)` 求墨龄 ⇒ **会取到别的病人的判断事件**;
> 而 `CasebookVitalsSource.cs:214` 注释写「按 case 过滤」,**与实现相悖**

**这是三件「真实现缺陷」中的最后一件**(另两件:B-1 归 S11-8 · B5 归 S11-10)。

### 1.1 缺陷的准确性质:不是「调用方传错」,是**接口本身失守**

修复前的契约是「**调用方负责传入该案的事件子集**」。但该契约在生产路径上**不可能被满足**:

- 唯一生产调用方传的是 `EventStream.Events` = **全部病人混合流**;
- 且**没有任何一层能合法地切片** —— `SimEvent.Payload` 是 `PayloadRef`(BlobId/Offset/Length),
  解码归 `Sim.Codec`,而 `Sim` 的引用集**恰 = {BCL, Sim.Contracts}**(ADR-025 §① / b2 门)
  ⇒ `Sim` 侧**物理上够不着**载荷里的 `CaseId`。**与 S11-8 同一结构约束**。

⇒ 契约把责任推给了一个**做不到的调用方**。故修复必须在**被调用方**完成。

---

## 二、修复落点

### 2.1 生产代码:归属判据改为**流序关联**(承 `OpenCaseOf` 既有正确范式)

```csharp
public static SimEvent? LastJudgmentOf(IReadOnlyList<SimEvent> events, CaseId caseId)
{
    SimEvent? last = null;
    bool inCase = false;

    foreach (var e in events)
    {
        if (e.Patient.Value != caseId.Patient)      // Patient 已是三元组的一维
            continue;

        if (e.Kind == EventKind.CaseOpened)
        {
            inCase = e.Tick == caseId.Tick && e.Seq == caseId.Seq;   // 案件身份 = 三元组
            continue;
        }

        if (e.Kind == EventKind.CaseClosed)
        {
            inCase = false;                          // 结案即离开本案
            continue;
        }

        if (!inCase) continue;                       // 别的案 / 案外 → 一律不计

        if (e.Kind == EventKind.JudgmentRecorded || e.Kind == EventKind.JudgmentRevised)
            last = e;
    }
    return last;
}
```

**为何不读载荷里的 `CaseId`**(判据存在,但够不着):`JudgmentRecordedPayload.CaseId` /
`JudgmentRevisedPayload.CaseId` **确实存在**(`CasePayloads.cs:60`/`:77`),
但同 §1.1 —— `Sim` 无法解码。故走流序关联,**与既有正确范式 `CaseStreamQuery.OpenCaseOf`
完全同构**(该函数本就是「按 Patient 过滤 + 跟踪 Opened/Closed 得当下开案」)。

**代价(与 S11-8 同款,如实登记)**:归属是**推断**而非**读取**;与载荷解码
**在合法流上等价**,仅在违反「同一病人同时至多一个开案」(GDD 规则二)的**非法流**上分歧
—— 该流下写者行为本身未定义。已写进函数文档注释。

### 2.2 ⚠️ 计划口径订正:**`CompositionRoot` 无须改动**(已报用户)

S11-7 计划写「`CompositionRoot.cs:245-246` **改传该案子集**(现传 `stream.Events` = 全局混合流)」。
**实测该处方是基于错误前提的**:

- 前提「调用方传错了」**不成立** —— 传整条流**本就是正确用法**,错在被调用方没用 `caseId`(§1.1);
- 且「该案子集」**构造不出来**:同 §1.1 的引用集约束,`Gameplay.Boot` 侧要切片只能自己解码载荷
  ⇒ **把 37 的查询语义复制到装配层**,是更差的架构。

⇒ **修复落在 `LastJudgmentOf` 内部,调用点(含 `CompositionRoot.cs:245-246`)** **一字未改**。
`CompositionRoot` 传全局流从此**成为正确行为**。**属「计划写了一个做不到、也不需要做的形状」,非范围缩减。**

### 2.3 `CasebookVitalsSource.cs` 注释对齐

原注释(`:211-215`)有两处与实现相悖/自述缺陷,已重写:

| 处 | 原文 | 现文 |
|---|---|---|
| `:211` | 「= 当前 tick − **该病人**最近 JudgmentRecorded 事件的 Tick」 | 「= 当前 tick − **该病人开案**最近一条判断事件的 Tick(= GDD 规则四① 的 `J(c)`)」 |
| `:214` | 「`LastJudgmentOf` **按 case 过滤**,此处取该病人最近一条 Judgment」 | 「…**修复前** caseId 零引用 ⇒ 实际取**该流最后一条判断事件**,**可能是别的病人的**;现已自按 case 过滤 ⇒ **传整条流即正确**,此处**无须也不应**预切片」 |

### 2.4 测试面(EditMode/Boot `ink_age_test.cs` 4 → 5;CaseSystem `case_open_and_judgment_test.cs` **23 → 25**)

**① 三条旧夹具改写 —— 它们编码的正是缺陷契约。**
`test_lastJudgmentOf_{noJudgment,hasJudgment,singleJudgment}_*` 原夹具**不含 `CaseOpened`**,
即「调用方已切片」形状 —— 这正是「caseId 死参」逃过测试面的**直接原因**
(与 S11-10 那条**反向断言缺陷**的旧测同型)。现按新契约(**传整条流**)重写,补上 `CaseOpened`。

**② 新补三条判别夹具**(旧实现下**必红**):

| # | 测试 | 判别什么 |
|---|---|---|
| 1 | `test_lastJudgmentOf_crossPatientInterleaved_ignoresOtherPatientsJudgments` | **跨病人**:病人 2 的判断全流更晚 ⇒ 不得算进病人 1 的案 |
| 2 | `test_lastJudgmentOf_samePatientSecondCase_ignoresFirstCaseJudgments` | **同病人复诊**:两案 `Patient` **相同**,只有 `CaseOpened` 三元组不同 ⇒ **不能被 #1 替代** |
| 3 | `test_inkAge_crossPatient_usesOwnJudgmentNotOtherPatients` | **实害链终点**:走**全生产路径**(`CompositionRoot` 装配),病人 A 的墨龄须为 2 而非 1 |

★ #3 是唯一**端到端**证明「墨龄取错病人」的夹具 —— 直接打在 BLK-8 的实害描述上。

---

## 三、验证命令(可证伪 · 逐字可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
U=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
FILTER_A=DaYiJingCheng.Tests.CaseSystem.CaseOpenAndJudgmentTest
FILTER_B=DaYiJingCheng.Tests.Boot.InkAgeTest

# ── ① 两处夹具(修复后须全绿)────────────────────────────────────────
for FLT in "$FILTER_A" "$FILTER_B"; do
  "$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
    -testFilter "$FLT" -testResults "$PWD/unity/Logs/s11_s7_${FLT##*.}.txt" \
    -logFile "unity/Logs/s11_s7_${FLT##*.}.log"
done

# ── ② 反证:把 LastJudgmentOf 方法体改回旧实现 ⇒ 须转红 ───────────────
#   旧体 = 只取「传入列表里最后一条判断事件(不论病人/案件)」:
#       foreach (var e in events) { if (Kind 非判断) continue; last = e; } return last;
#   ⚠️ 只回退**方法体**,保留签名与新夹具(整段回退 HEAD 会连新夹具一起没掉,红/绿失去可比性)

# ── ③ 全量双平台(零回归)─────────────────────────────────────────────
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testResults "$PWD/unity/Logs/s11_s7_editmode.xml" -logFile unity/Logs/s11_s7_editmode.log
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform PlayMode \
  -testResults "$PWD/unity/Logs/s11_s7_playmode.xml" -logFile unity/Logs/s11_s7_playmode.log
```

判决口径 = **退出码 0 且 XML `failed="0"`** 双信号一致。

> ⚠️ **执行期踩坑如实登记(供复跑者避雷)**:反证脚本里若把 `-testFilter` 的循环变量与
> **待回退的源文件路径变量同名**(如都用 `F`),`for` 循环会**覆盖**该变量,导致
> 「还原」步骤把备份写到**一个以测试类全名命名的垃圾文件**上,而 `diff -q` 与之比对
> **假报「已还原」** —— 生产文件实际留在旧实现态。
> 本件已按此教训改用**不同变量名**(`FLT` vs 路径字面量),并在还原后**用 `grep` 独立复核
> 关键符号存在**(不是只信 `diff` 的退出码)。

---

## 四、结果

### ① 反证(旧实现 + 新夹具) —— 恰 3 红,逐条命中实害描述

| 套件 | 退出码 | 结果 |
|---|---|---|
| `CaseOpenAndJudgmentTest` | **2** | `total=25 passed=23 **failed=2**` |
| `InkAgeTest` | **2** | `total=5 passed=4 **failed=1**` |

```
[Failed] test_lastJudgmentOf_crossPatientInterleaved_ignoresOtherPatientsJudgments
    须是病人 1 的事件            Expected: 1   But was: 2      ← 取到了病人 2 的

[Failed] test_lastJudgmentOf_samePatientSecondCase_ignoresFirstCaseJudgments
    案 A 只认 [100,200) 窗内的判断;旧实现返回 310(案 B 的)
                                 Expected: 110 But was: 310    ← 复诊案污染首案

[Failed] test_inkAge_crossPatient_usesOwnJudgmentNotOtherPatients
    病人 A:5 − 3 = 2;旧实现取全流末条(病人 B 的 tick=4)⇒ 1 —— 墨龄取错病人
                                 Expected: 2   But was: 1
```

★ 末条是**端到端**的实害复现:**墨龄取错病人**,与补评件 BLK-8 的措辞逐字一致。

### ② 修复后 —— 两处夹具全绿

| 套件 | 退出码 | 结果 | 测试数变化 |
|---|---|---|---|
| `CaseOpenAndJudgmentTest` | **0** | `25/25 · 0 failed` | 23 → **25**(+2 判别夹具) |
| `InkAgeTest` | **0** | `5/5 · 0 failed` | 4 → **5**(+1 跨病人) |

### ③ 全量零回归

| 套件 | 结果 | 退出码 | 对比 |
|---|---|---|---|
| EditMode | `total=3337 passed=3291 failed=0 inconclusive=0 skipped=46` | **0** | 较 S11-11 后 `3334/3288` ⇒ **+3 total / +3 passed**,**恰为本轮新增的三条判别夹具** |
| PlayMode | `total=186 passed=186 failed=0` | **0** | **逐位等于 M2 基线**(**M2 链零回退**) |

### ④ 静态面

`grep -c "error CS"` = **0**。**引用集一字未动** —— 无 asmdef 改动
⇒ 门 A / b2 / ADR-025 §① / ADR-029 全部继续成立。改动共 4 文件(1 生产 + 1 注释 + 2 测试)。

---

## 五、未清项

- **story-002 维持 `In Review`** —— BLK-8 是本件唯一取走的代码面 BLOCKING。
  **余项顺延 sprint-12**(须带状态 + 归属,禁借绿):5 条**断言形态**
  (BLK-2/3/5/9 + BLK-1 恒真半边)· BLK-9(药材短缺负断言的 fixture)· BLK-10(必交夹具类型错位)。
- **未动但已识别(本件范围外)**:`test_ac3705_storeNewValue_judgmentIsLastEvent`(`:168-195`)仍然
  **不调用** `LastJudgmentOf`,而用测试自造的 `.Where().OrderBy().Last()` —— 属 §六·四 B3 点名的
  「自造点」,归 **sprint-12 断言形态批**(与本件同一缺口的两半:本件修了生产函数,
  该测试的断言形态仍待改调生产)。
- **`CaseOpenDecider.Decide` 无生产调用者**(ADV-2 孪生体)亦未动,归同类批次。
