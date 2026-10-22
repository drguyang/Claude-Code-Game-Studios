# 照护通道接真 —— 证据(2026-10-22 第五轮)

**批次**:S18 后续 · 第四轮遗留项「照护通道恒 None」的修复
**前置**:`impl-trend-backward-2026-10-22.md`(第四轮,commit `6f73835`)
**判据面**:GDD `disease-simulation.md` `:1126-1133`(照护支派生式)· `:1135-1140`(适用集)
· `:208-226`(照护事件化 + 集群 2 修正)· `:164`(病史元组)· `:2013`(AC-30)

---

## 一、本轮修的是什么

第四轮 §八 与第三轮 §九 都把「照护通道」登记为**恒 `None`**,理由是
「`CareApplied` 的写者(24)未落 ⇒ 流里没有护理事件」。

**本轮复核发现这条理由只覆盖了一半**:

| # | 事项 | 第四轮时的判断 | 本轮实测 |
|---|---|---|---|
| ① | 流里没有护理事件 | ✅ 属实(写者未落) | ✅ 仍属实 |
| ② | **即使有护理事件,读取面也接不上** | ❌ **未登记** | ⚠️ **属实** —— `Apply()` 的 `switch` **完全没有 `CareApplied` 分支** |

⇒ ② 是**代码缺口**,不是「等写者」。写者落地后,护理事件会进流、进 blob 池,
但 `DiseaseVitalsService.Apply()` 会走到 `default: break;` —— **静默跳过**,
照护通道仍恒 `None`,而**没有任何测试会红**(第四轮的 `test_*` 全是
`CareChannelInput.None` 的手工构造,不经过 `Apply()`)。

这与第三轮 `trend_backward` 的发现同型:**「等外部依赖」的登记掩盖了一个已可修的缺口**。

---

## 二、五处改动

| # | 件 | 文件 | 性质 |
|---|---|---|---|
| 1 | `Apply()` 增 `case EventKind.CareApplied:` | `Gameplay.Boot/DiseaseVitalsService.cs` | 改(解码 + 归档) |
| 2 | `DiseaseCourse` 记两个照护时刻 + 值域门 | `Sim/DiseaseSimulation/DiseaseCourse.cs` | 改(状态) |
| 3 | `DiseaseCourseBook.ApplyCare` | `Sim/DiseaseSimulation/DiseaseCourseBook.cs` | 改(档案簿入口) |
| 4 | 接线按适用集分流 | `Gameplay.Boot/DiseaseVitalsService.cs` | 改(语义) |
| 5 | `F4WiringTest` 增 6 条 | `Tests/EditMode/DiseaseSimulation/f4_wiring_test.cs` | 改(新增测试) |

### 2.1 `DiseaseCourse` 的两个字段

```csharp
public long? LastCareStopTick { get; private set; }     // 只由 phase=1 写
public long? LastCareActionTick { get; private set; }   // 任意 phase 都写
```

**取大而非直接赋值**:入流序理论上升序,但乱序重放(AC-16)下直接赋值会让
「最后一条」退化成「**最后到达**的一条」。

### 2.2 值域门(fail-loud)

`entities.yaml` 的 `CareApplied` 条目只写「`phase`: 枚举(起 止)」——
**全库没有序数定义**。本实现取 `0 = 起 / 1 = 止`,这是**落地约定,不是权威件**。

⇒ 越界值当场抛:

```csharp
if (phase != CarePhaseStart && phase != CarePhaseStop)
    throw new InvalidOperationException(...);
```

**为什么必须抛**:若写者(24)用了反序编码,静默取值会让 `LastCareStopTick`
永远为空 ⇒ 照护支退化(见 §三),而**没有任何测试会红**。

### 2.3 接线侧

```csharp
var care = CareLeverApplicability.AppliesTo(entry)
    ? new LastInterventionAggregator.CareChannelInput(
        course.LastCareStopTick, course.LastCareActionTick)
    : LastInterventionAggregator.CareChannelInput.None;
```

**适用集门放在接线侧,不放在档案里** —— 破伤风护理**仍进流、仍进档**
(它是有效动作),只是不参与 F4 的窗口。把适用集判据复制进 `DiseaseCourse`
就是第二份实现。

---

## 三、本轮发现:GDD 内部一处真冲突(待裁,不代拍)

**这是本轮最重要的产出,且它不是实现问题,是规格问题。**

### 3.1 冲突内容

GDD `:1127` 的**公式**:

```
照护支 = max(最后一条护理动作止, 最后一条护理动作事件 tick + CARE_GAP)
```

GDD `:226` 的**散文**:

> 「止」存在则**从止起计时**。

### 3.2 公式使散文不可实现

「止」本身就是一条护理动作事件 ⇒

```
最后动作 tick ≥ 最后止 tick
⇒ 最后动作 + CARE_GAP > 最后止        (CARE_GAP = 600 > 0)
⇒ max(…) 恒取第二项
```

⇒ **`LastCareStopTick` 对 `last_intervention` 零影响**。
公式算出来的结果与「删掉止项」完全一致,而散文说「从止起计时」。

**两者不可能同时为真。**

### 3.3 本轮的处理(不代拍)

| 项 | 处置 |
|---|---|
| 实现 | **按公式**(公式是该段的正式定义;散文是意图说明) |
| 字段 | **仍照记** `LastCareStopTick` —— 删掉会让「按散文修」变成签名变更;且 `phase` 是载荷的一部分,折叠 / 8 的护理记录等其他消费者需要它 |
| 冲突 | **登记待裁**,判据落成测试 `test_careStop_isRedundantUnderDerivedFormula` |
| 不做的 | 不改 GDD 任一侧;不擅自把公式改成「取止」或「止 + CARE_GAP」 |

**裁定轮需要回答的问题**:照护支的语义是
(a)「最后一次动作后 CARE_GAP 内仍算照护」(公式),
还是 (b)「止之后立刻停算」(散文)?
两者在「玩家主动点了止」这一情形上给出**不同**的死亡时刻 —— 差 600 tick(= 30 秒)。

### 3.4 这条冲突为什么值得单列

它不是笔误。`:221-226` 那一段的**立段缘由**正是「字面『最后一条护理动作止』
在『起』后玩家走开时语义为空」—— 所以三轮加了 `+ CARE_GAP` 项。
但加完之后**忘了「止」项已被覆盖**,于是留下一条永远不起作用的项,
而散文那句「从止起计时」也就成了**描述了公式做不到的事**。

---

## 四、测试(F4WiringTest 增 6 条)

| # | 测试 | 钉什么 | 能否证伪 |
|---|---|---|---|
| ① | `test_care_refreshesDeathWindow` | 护理动作**真的**把死亡推后(伤寒) | ✅ 接线恒 `None` ⇒ 红 |
| ② | `test_care_nonApplicableDisease_doesNotRefreshWindow` | 破伤风护理**不**刷窗口(K8) | ✅ 漏适用集门 ⇒ 红 |
| ③ | `test_careStop_isRedundantUnderDerivedFormula` | 「止」项在派生式下恒冗余 | ✅ 改公式取止 ⇒ 红 |
| ④a | `test_careStop_recordedSeparatelyFromAction` | `phase` 被读(止不被后到的起顶掉) | ✅ 不区分 phase ⇒ 红(N12) |
| ④b | `test_careTimes_takeMax_notOverwrite` | 取大而非直接赋值(乱序重放) | ✅ 直接赋值 ⇒ 红(N13) |
| ⑤ | `test_carePhase_outOfRange_throws` | 越界 phase fail-loud | ✅ 静默取值 ⇒ 红 |

### 4.1 ①② 必须成对(否则各留一个假存活形态)

| 只测 | 漏掉 | 漏掉的错法 |
|---|---|---|
| ①(伤寒不死) | ② | 漏适用集门 —— 破伤风也被护理无限续命,① 仍绿 |
| ②(破伤风死) | ① | 恒 `None` 实现 —— 谁都不刷新窗口,② 仍绿 |

**两条同时成立**才排除这两种错法。这是本轮在测试设计上唯一需要特别说明的地方。

### 4.2 ③ 的由来:原计划的一条测试打不中靶

原本打算写「止 6500 + 动作 3000 ⇒ 窗口从 6500 起算 ⇒ 不判死」,
用九态作判据。**算下来发现打不中**:

| 读法 | `last_intervention` | 驱动到 10000 时 `t − li` | 结果 |
|---|---|---|---|
| 正确(读 phase,止 6500) | 6500 | 3500 < 3600 | 昏迷 |
| 漏读 phase(止也当动作 ⇒ 派生 7100) | 7100 | 2900 < 3600 | **也昏迷** |

⇒ 两种读法**同效**,九态判据**钉不住**。
这与第四轮 N6 的假存活同型(靶子构造让两条路径等价)。

**订正**:判据改落在**字段值**上(④a / ④b),并把「止项冗余」本身写成 ③。
③ 是从这次「打不中靶」的调查里**直接发现规格冲突**的那条测试。

---

## 五、回归(带三元组)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 20:00 CST` |
| 批次 | 全量 EditMode(变异**前**的干净基线) |
| commit | `6f73835`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-editmode-round14.xml` |

```
total=4598  passed=4562  failed=0  inconclusive=0  skipped=36
```

> **变异后复跑(round15)= 零残留的判据**:同样 `4598 / 4562 / 0`,逐字段相同,见 §5.7。

**差分核对**:

| 轮次 | total | passed | 新增 | 说明 |
|---|---|---|---|---|
| round10(第四轮终值) | 4592 | 4556 | — | 基线 |
| round12 | 4596 | 4560 | **+4 / +4** | 照护接线 + 4 条测试(值域门与另 2 条尚未落) |
| round13 | 4597 | 4561 | +5 / +5(对 round10) | 含值域门;但 ④ 号测试**打不中靶**(§5.3) |
| round14 | 4598 | 4562 | **+6 / +6**(对 round10) | ④ 拆成两条,靶子修好 |

`4598 − 4592 = 6` = `4562 − 4556 = 6` ✓ ⇒ 零回归。
新增数按类对账 = `F4WiringTest` 里 `test_care*` **6 条**(实跑命令与输出见 §七)。

> ⚠️ **对账口径的坑(本轮实测)**:裸 `grep -o 'test_care[A-Za-z_]*'` 得 **12** 条 ——
> 它把 `CareLeverApplicabilityTest` / 病例投影等其他类里同前缀的**既有**测试也捞进来。
> 计数必须**限定到 `F4WiringTest`**(§七 已改为按 `fullname` 过滤),否则
> 「+6」会被自己写的命令证成「+12」。

**轮次间的条数变化都可解释**(不是凭空多/少):

| 变化 | 原因 |
|---|---|
| round12 的 4 条 → round13 的 5 条 | 删 `test_careStop_extendsWindowBeyondDerived`(§4.2 查明打不中靶),增 `test_careStop_isRedundantUnderDerivedFormula` + `test_carePhase_outOfRange_throws` ⇒ `4 − 1 + 2 = 5` |
| round13 的 5 条 → round14 的 6 条 | ④ `test_careStop_isRecordedSeparately_andMonotone` **对「忽略 phase」假存活**(§5.3),拆成 `..._recordedSeparatelyFromAction` + `test_careTimes_takeMax_notOverwrite` ⇒ `5 − 1 + 2 = 6` |

> ⚠️ round12 是在本轮最后两处改动(值域门 `phase` 校验 + ⑤ 测试)落盘**之前**
> 启动的,编译的是旧快照。它作为**中间基线**记录,不是终值。

---

## 五之二、证伪轮

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。**未用** `git checkout` / `git stash`。

基线 sha256:

```
5e1b80fff6b3aa0a2db2d0d78f492f12ece8e494fad5071157752c6f6ec464df  DiseaseVitalsService.cs
2cf641d93afbf57aceb3d5c245ac9d910363c38988622db7b5b44f328f2490b3  DiseaseCourse.cs
44e5ee8a151f242a987544be97dc59b18d2ed3878eec3859f67035d6ffe0b431  DiseaseCourseBook.cs
4a0dcdc9cb8527d36c80ac1d53f8e3bd7ce5dbe1a782445e0327721735aa0848  LastInterventionAggregator.cs
```

| 变异 | 内容 | 结果 | 转红的测试 |
|---|---|---|---|
| **N10** | 照护通道恒 `None`(删掉整段适用集分流) | `4597 / 4560 / **1**` | `test_care_refreshesDeathWindow` |
| **N11** | 漏适用集门(无条件构造 `CareChannelInput`) | `4597 / 4560 / **1**` | `test_care_nonApplicableDisease_doesNotRefreshWindow` |
| **N12** | `phase` 不区分(止也当普通动作) | `4598 / 4561 / **1**` | `test_careStop_recordedSeparatelyFromAction` |
| **N13** | 取大 → 直接赋值 | `4598 / 4561 / **1**` | `test_careTimes_takeMax_notOverwrite` |

### 5.1 N10 结果 —— 照护通道恒 None

转红**恰好一条**:

```
test_care_refreshesDeathWindow
```

差分 `4561 − 4560 = 1`(基线 = round13 的 `4597 / 4561 / 0`;N10 跑在该快照上,
④ 尚未拆成两条,故 total 为 4597 而非 4598)。结果件 `unity/Logs/test-editmode-n10.xml`。

**这正好验证了 §4.1 的预言**:

| 测试 | N10 下 |
|---|---|
| ① `test_care_refreshesDeathWindow` | **红** —— 恒 `None` ⇒ 伤寒护理不续命 ⇒ 死 |
| ② `test_care_nonApplicableDisease_doesNotRefreshWindow` | **仍绿** —— 破伤风本来就不该续命,恒 `None` 与正确实现**同效** |

⇒ **②单独立不住**,必须与①成对。这正是 §4.1 说的那个假存活形态,本轮实测到了。

### 5.2 N11 结果 —— 漏适用集门

转红**恰好一条**:

```
test_care_nonApplicableDisease_doesNotRefreshWindow
```

差分 `4561 − 4560 = 1`(基线同 N10 = round13 的 `4597 / 4561 / 0`)。结果件
`unity/Logs/test-editmode-n11.xml`。

**与 N10 完全互补,这是本轮最强的结构性证据**:

| 变异 | ① 伤寒续命 | ② 破伤风不续命 |
|---|---|---|
| N10(恒 `None`) | **红** | 绿(同效) |
| N11(漏适用集门) | 绿(同效) | **红** |

⇒ ① 与 ② **各自只能抓一种错法**,任一条单独存在都留一个盲区。
这正是 §4.1 事前写下的判断,现在有了实测。

### 5.3 ④ 号测试的第一版打不中「phase 不区分」(自查,留档)

> ⚠️ 本节记录的是**变异开跑之前**的靶子核对,故仍按当时的 ④ 单条编号叙述;
> 下文 ④a / ④b 是订正后的拆分结果。实跑结果见 §5.4。

写 N12 变异之前先核对靶子,发现 **④ 原版对「忽略 phase」假存活**:

| 原夹具 | 正确实现 | 忽略 phase |
|---|---|---|
| 起 3000 → 止 6500 | `StopTick = 6500` | `StopTick = max(3000,6500) = 6500` |

**同值** ⇒ 变异不红。

**订正**:拆成两条,各自靶子明确 ——

| 测试 | 夹具 | 正确 | 忽略 phase |
|---|---|---|---|
| `test_careStop_recordedSeparatelyFromAction` | 起 3000 → 止 4000 → **起 5000** | `Stop=4000` / `Action=5000` | `Stop=**5000**` ⇒ 分叉 |
| `test_careTimes_takeMax_notOverwrite` | 止 6500(先) → 起 3000(乱序) | 两字段 = 6500 | 直接赋值 ⇒ 两字段 = 3000 ⇒ 分叉 |

⇒ 这是本轮**第二次**「原计划的靶子打不中」(第一次见 §4.2)。两次都是
**先算一遍两种读法的输出**才发现的 —— 若直接写测试就跑,两条都会假绿。

### 5.4 N12 实跑结果 —— `phase` 不区分

变异:`NoteCareAction` 里把 `if (phase == CarePhaseStop && …)` 换成无条件的
`if (!LastCareStopTick.HasValue || tick > LastCareStopTick.Value)`,即**任何动作都当「止」**。

结果件 `unity/Logs/test-editmode-n12.xml`(`2026-10-22 20:03 CST`):

```
total=4598  passed=4561  failed=1  inconclusive=0  skipped=36
FAILED: DaYiJingCheng.Tests.DiseaseSimulation.F4WiringTest.test_careStop_recordedSeparatelyFromAction
```

转红**恰好一条**,差分 `4562 − 4561 = 1`,且正是 §5.3 表里预言的那条
(`起 3000 → 止 4000 → 起 5000`,忽略 phase 时 `Stop` 被顶成 5000 ≠ 4000)。

**同变异下另两条仍绿,是设计内的**:

| 测试 | N12 下 | 为什么 |
|---|---|---|
| `test_careStop_isRedundantUnderDerivedFormula` | 绿 | 它**根本不经过 `DiseaseCourse`** —— 直接调 `LastInterventionAggregator.Evaluate` 传两个字段值 ⇒ 与 `NoteCareAction` 的写法无关 |
| `test_careTimes_takeMax_notOverwrite` | 绿 | 夹具是「止 6500 → 起 3000」,忽略 phase 后 `Stop = max(6500,3000) = 6500` **与正确值同** |

⇒ **两条测试各自只覆盖一种错法**,与 §4.1 的成对论证同构:N12 只有
`recordedSeparatelyFromAction` 抓得住,N13(取大→赋值)只有 `takeMax_notOverwrite` 抓得住。

### 5.5 N13 实跑结果 —— 取大 → 直接赋值

变异:`NoteCareAction` 的两处 `if (!…HasValue || tick > …Value)` 全部换成裸赋值
(`LastCareActionTick = tick;` / `if (phase == CarePhaseStop) LastCareStopTick = tick;`),
即**最后到达的一条覆盖最后一条**。

结果件 `unity/Logs/test-editmode-n13.xml`(`2026-10-22 20:05 CST`):

```
total=4598  passed=4561  failed=1  inconclusive=0  skipped=36
FAILED: DaYiJingCheng.Tests.DiseaseSimulation.F4WiringTest.test_careTimes_takeMax_notOverwrite
```

转红**恰好一条**,差分 `4562 − 4561 = 1`。

**与 N12 完全互补(第四个成对)**:

| 变异 | `recordedSeparatelyFromAction` | `takeMax_notOverwrite` |
|---|---|---|
| N12(忽略 phase) | **红**(`Stop` 被 5000 顶掉) | 绿(仍取大 ⇒ `Stop=max(6500,3000)=6500`,与正确值同) |
| N13(裸赋值) | 绿(夹具升序 ⇒ `Stop` 裸赋值仍是 4000,与取大同值) | **红**(`Action` 被后到的 3000 顶掉) |

> ⚠️ 上表 N12 × `takeMax` 格的精确理由:夹具是「止 6500(先)→ 起 3000(后)」。
> 忽略 phase 时 `Stop = max(6500,3000) = 6500`,仍取大 ⇒ 与正确值同 ⇒ 绿。
> **两个变异打的是两条不同的代码行**(phase 判据 vs 取大判据),两条测试各守一条。

### 5.6 四轮变异后的还原核验

| 文件 | 变异后 sha256 | 还原后 sha256 | 与 §五之二 基线 |
|---|---|---|---|
| `DiseaseCourse.cs` | `e29370c6…c3a4`(N13 态) | `2cf641d9…90b3` | ✅ 逐字节相同 |
| `DiseaseVitalsService.cs` | ⚠️ **未留档**(见下) | `5e1b80ff…64df` | ✅ |
| `DiseaseCourseBook.cs` | 未变异 | `44e5ee8a…0b431` | ✅ |
| `LastInterventionAggregator.cs` | 未变异 | `4a0dcdc9…a0848` | ✅ |

> ⚠️ **诚实边界**:`DiseaseCourse.cs` 的 N12 态、以及 `DiseaseVitalsService.cs` 的
> N10 / N11 态,**变异后哈希都没留档** —— 当时只做了「备份 → 改 → 跑 → `cp` 回」,
> 没有在变异态上跑 `sha256sum`。⇒ 这两格**能证的是「还原后 = 基线」**(这一列是实测的),
> **不能证的是「变异确实改动了文件」** —— 后者靠的是**变异轮的测试转红**(N10/N11/N12/N13
> 各红一条),而不是哈希。不要把这格读成「已双向核验」。

还原手段 = `cp` 备份回原位(**未用** `git checkout` / `git stash`),判据 = **SHA-256 逐字节**,
不是「探针串不在文件里」。

**零残留的最终判据** = round15 干净回归回到 `4598 / 4562 / 0`(见 §5.7)。

### 5.7 round15 —— 还原后的干净复跑

还原四件(sha256 已逐字节核过,见 §5.6)后原样重跑全量 EditMode:

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 20:08 CST` |
| 批次 | 全量 EditMode(变异**后**、还原**后**) |
| commit | `6f73835`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-editmode-round15.xml` |

```
total=4598  passed=4562  failed=0  inconclusive=0  skipped=36
```

**与 round14 逐字段相同** ⇒ 四轮变异(N10 / N11 / N12 / N13)**零残留**。

辅助判据(非替代):`grep -rn "\[N1[0-9]\]" unity/Assets/` 零命中 ——
变异标记串未残留。⚠️ 但**主判据是 sha256 逐字节 + 本轮复跑**,
「串扫不到」单独不足以证明还原(承 `CLAUDE.md` §构建前门:还原判据 = SHA-256,不是 grep 探针串)。

---

## 六、明确未实现(禁借绿)

| # | 未做 | 影响 / 归属 |
|---|---|---|
| ① | **`CareApplied` 的写者(24)仍未落** | 生产流里零护理事件 ⇒ 生产路径上照护通道仍恒 `None`。**但读取面已完整**:写者一落地即自动生效,接线侧不必再改 |
| ② | **`phase` 序数无序数级权威定义** | `0 = 起 / 1 = 止` 是本实现的落地约定(`entities.yaml` 只写「枚举(起 止)」)。值域门保证越界当场抛,但**写者落地时须与本约定对齐** |
| ③ | **GDD 公式 vs 散文的冲突未裁** | 见 §三。判据已落成 ③ 号测试 |
| ④ | `W` / `TREND_EPS` 仍无数值 | 第四轮遗留,未动 |
| ⑤ | 多病聚合(F5)未接 | 第四轮 §8.1 已登记,未动 |

⇒ **能说的**:「照护通道的读取面已完整 —— 事件解码、档案归档、适用集分流、
值域校验四段全落,并被 6 条测试钉住;写者一落地即生效」。
⇒ **不能说的**:「游戏里照护已经能续命」「生产路径上的照护通道已非零」。

---

## 七、复算命令

```bash
cd unity
# 本轮终值(round15 —— 变异还原后的干净复跑;与 round14 逐字段相同)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity" \
  -runTests -testPlatform EditMode \
  -testResults "/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Logs/test-editmode-round15.xml" \
  -logFile "/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity/Logs/test-editmode-round15.log"

python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('Logs/test-editmode-round15.xml').getroot()
print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
for tc in r.iter('test-case'):
    if tc.get('result')=='Failed': print('FAILED:', tc.get('fullname'))
"

# 九轮结果件一把对账(round10/12/13/14/15 + n10..n13)
for f in round10 round12 round13 round14 round15 n10 n11 n12 n13; do
  printf "%-8s " "$f"
  python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('Logs/test-editmode-$f.xml').getroot()
print(f\"{r.get('total')} / {r.get('passed')} / {r.get('failed')} / skipped={r.get('skipped')}\")
"
done

# 本轮新增测试按类计数(与 +N 对账)
# ⚠️ 必须限定到 F4WiringTest —— 裸 `test_care*` 前缀会捞进别的类里的既有测试
#    (实测裸 grep 得 12 条,其中 6 条属 CareLeverApplicabilityTest 等其他类)
python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('Logs/test-editmode-round15.xml').getroot()
names=sorted(tc.get('name') for tc in r.iter('test-case')
             if 'F4WiringTest' in (tc.get('fullname') or '')
             and (tc.get('name') or '').startswith('test_care'))
print(len(names)); print('\n'.join(names))
"
# 预期:6 条 —— test_carePhase_outOfRange_throws / test_careStop_isRedundantUnderDerivedFormula /
#              test_careStop_recordedSeparatelyFromAction / test_careTimes_takeMax_notOverwrite /
#              test_care_nonApplicableDisease_doesNotRefreshWindow / test_care_refreshesDeathWindow

# 「止项冗余」的可复算判据
grep -n "恒冗余" Assets/Sim/LastInterventionAggregator.cs
# 预期:119:        /// <para><b>⚠️ 该式使「止」项恒冗余(2026-10-22 第五轮发现,GDD 内部冲突,待裁)</b>:
```

> ⚠️ **`-projectPath` 必须用绝对路径**(第四轮实测踩坑,承 `CLAUDE.md`)。
> 后台任务的 cwd 会重置回仓库根 ⇒ `"$PWD"` 指到仓库根 ⇒ Unity 在无 `Assets/`
> 的空工程上启动、`EXIT=1`、零结果件。

---

## 八、本轮改动清单

| # | 件 | 文件 | 性质 |
|---|---|---|---|
| 1 | `Apply()` 增 `CareApplied` 分支 | `Gameplay.Boot/DiseaseVitalsService.cs` | 改 |
| 2 | 照护通道按适用集分流 | `Gameplay.Boot/DiseaseVitalsService.cs` | 改 |
| 3 | 陈旧注释就地加注(原文不删) | `Gameplay.Boot/DiseaseVitalsService.cs` | 改(注释) |
| 4 | `LastCareStopTick` / `LastCareActionTick` / `NoteCareAction` / `CarePhaseStart` / `CarePhaseStop` | `Sim/DiseaseSimulation/DiseaseCourse.cs` | 改(新增) |
| 5 | `ApplyCare` | `Sim/DiseaseSimulation/DiseaseCourseBook.cs` | 改(新增) |
| 6 | 照护支冗余性说明 + 待裁登记 | `Sim/LastInterventionAggregator.cs` | 改(注释) |
| 7 | `F4WiringTest` 增 6 条 + 2 个夹具方法 | `Tests/EditMode/DiseaseSimulation/f4_wiring_test.cs` | 改 |

**未改动**:`F4OutcomeEvaluator.cs` / `CareLeverApplicability.cs` / `StateMachine.cs` /
任何 `entities.yaml` 载荷 schema / 任何 GDD 文本。
