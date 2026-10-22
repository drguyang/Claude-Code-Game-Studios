# F4 痊愈支 / 迁延支 + 多病聚合 + 照护适用集 + 生产接线 —— 证据(2026-10-22 第三轮)

**批次**:S18 后续 · 第二轮遗留五项的修复
**前置**:`impl-f4-outcome-evaluator-2026-10-22.md`(第一轮,commit `655c8a3`)
**判据面**:GDD `disease-simulation.md` §F4 `:1113-1125` · F5 `:1294-1340` · 照护适用集 `:1135-1140` · Tuning `:1743-1744`

---

## 一、上一轮登记的七项未实现,本轮逐项处置

第一轮证据件 §七 列了七项。本轮按用户四项裁定处置:

| # | 第一轮登记 | 本轮处置 | 用户裁定 |
|---|---|---|---|
| ① | 痊愈支未实现(需 `trend_backward_agg` + `relapse`) | ✅ **已实现**(六条件合取) | 「结构先落,数值作入参」 |
| ② | 迁延支未实现 | ✅ **已实现** | 同上 |
| ③ | `position_agg` 的 argmax 选择归调用方 | ✅ **已实现**(`DiseaseAggregator`) | — |
| ④ | 照护杠杆适用集归调用方 | ✅ **已实现**(`CareLeverApplicability`) | — |
| ⑤ | `F4OutcomeEvaluator` 生产调用点 = 0 | ✅ **已接线**(`DiseaseVitalsService.StepAll`) | 「接 F4 求值,**但不写流**」 |
| ⑥ | PlayMode 未跑 | ✅ **本轮已跑**(见 §八之二) | — |
| ⑦ | `lethal` 代理是 `Lethality > 0`(字段错位) | ✅ **已裁定并订正注释** | 「保持 0-3,明确语义为致死等级」 |

**第 ⑧ 项(本轮新发现)**:`ActorClass` 无病人值 ⇒ F4 对病人无类别可传。
已由用户裁定新增 `ActorClass.Patient = 4`。

**本轮两套回归的终值**(均本轮实跑,带三元组,详见 §八):

| 套件 | 结果 | 截至时刻 | 结果件 |
|---|---|---|---|
| EditMode | `4584 / 4548 / 0 / 36` | `19:06 CST` | `test-editmode-round7.xml` |
| PlayMode | `202 / 202 / 0 / 0` | `19:11 CST` | `test-playmode-round3.xml` |

**证伪轮**:N6 假存活 → 靶子订正 → **N6′ 恰好一条转红**(`4584 / 4547 / 1`),还原后
`sha256` 逐字节回到基线。详见 §七。**假存活本身已留档**(§7.1),不掩盖。

---

## 二、本轮落盘

| # | 件 | 文件 | 性质 |
|---|---|---|---|
| 1 | `ActorClass.Patient = 4` | `Sim.Contracts/ActorClass.cs` | 改 |
| 2 | `IsCombatant` 增 `Patient ⇒ false`;`LethalityOf` 文档重写 | `Sim/EntityDeathGates.cs` | 改 |
| 3 | 照护杠杆适用集 | `Sim/CareLeverApplicability.cs` | **新建** |
| 4 | 多病聚合 + argmax 的 `d` | `Sim/DiseaseAggregator.cs` | **新建** |
| 5 | 痊愈支 + 迁延支 + `RecoveryWindowInput` | `Sim/F4OutcomeEvaluator.cs` | 改 |
| 6 | 有效对因处置判据提为公开唯一入口 | `Sim/ProgressionEvaluator.cs` | 改 |
| 7 | **F4 接进 `StepAll`** + `GetOutcome` | `Gameplay.Boot/DiseaseVitalsService.cs` | 改 |
| 8 | 三处失实自述订正 | `Sim/StateMachine.cs` · `Sim/RegistrySchema.cs` | 改(注释) |
| 9 | 新件测试 35 条 | `Tests/EditMode/DiseaseSimulation/care_lever_and_aggregator_test.cs` | **新建** |
| 10 | 接线测试 8 条 | `Tests/EditMode/DiseaseSimulation/f4_wiring_test.cs` | **新建** |

---

## 三、三处失实自述的订正(本轮自查发现)

### 3.1 `StateMachine.cs` 的 `RECOVER_THRESHOLD` 注释

原文写「F4 的痊愈支…**仍未实现**」。本轮实现了它 ⇒ 该句失效。
**处置**:就地加 🔁 注,说明现已有两个消费者(注册表 R1-35 + F4 痊愈支),
并明写 `W` / `TREND_EPS` 仍作入参不写死。**原文不删**。

### 3.2 `SEVERE_THRESHOLD` / `MODERATE_THRESHOLD` 的归属是错的

原文写「重度档下界(**GDD 九态分级**)」。**实测**:

```bash
grep -n "SEVERE_THRESHOLD\|MODERATE_THRESHOLD\|重度档\|中度档" design/gdd/*.md
→ 零命中
```

GDD 九态表(`:536-546`)是 潜伏 · 发病 · 进展 · 好转 · 迁延 · 危殆 · 昏迷 · 痊愈 · 死亡
—— **没有「轻度 / 中度 / 重度」三档**,全篇亦无 `2000` / `1000` 作阈值。
⇒ 这两个常量的出处是 **`InjuryState` 这个 C# 枚举本身**,不是 GDD 九态。

### 3.3 `InjuryState` 与 GDD 九态**不是同一套词汇**(本轮最重要的发现)

| | 值 |
|---|---|
| **GDD 九态** | 潜伏 · 发病 · 进展 · 好转 · 迁延 · 危殆 · 昏迷 · 痊愈 · 死亡 |
| **`InjuryState`** | Healthy · Mild · Moderate · Severe · Critical · Coma · Recovered · Chronic · Deceased |

**可对齐的 5 对**:`Coma↔昏迷` · `Critical↔危殆` · `Recovered↔痊愈` · `Chronic↔迁延` · `Deceased↔死亡`。
**GDD 有而枚举无**:潜伏 / 发病 / 进展 / 好转 —— 四者是**病程过程态**,由 `τ` 与 `sign(trend)` 分段,
**不是 `position` 阈值的函数**。
**枚举有而 GDD 无**:`Healthy` / `Mild` / `Moderate` / `Severe` —— 按 `position` 切的三档 + 健康。

**为什么不是缺陷、也不该随手改**(三条,已落 `InjuryState` 的 XML 注释):

1. **两个枚举服务两个消费者**:`InjuryState` 是 `EventKind.InjuryStateChanged` 的载荷词汇
   (`entities.yaml:2382`),服务**受伤实体集 {敌人, 玩家}**、落**世界流**;
   GDD 九态是**病人侧**病程语义、落**病史流**。改枚举 = 改一条已登记 Kind 的载荷域。
2. **GDD 明确禁止下游重建九态**:13 只按 `position`/`trend` 自有 `BEHAVIOR_BAND_*` 分档
   (`:528` / `:646` / `:1487`,ADR-016 §六)⇒ GDD 九态**从未计划**成为跨系统状态枚举。
3. **病人侧状态转移本就不走本枚举**:`entities.yaml:2383` 明写「病人侧的状态转移由 9 的病史流
   既有 Kind 承载」;终态由 `PatientOutcomeChanged`(ADR-032)承载。

**实际可达性**:`InjuryState` 的唯一生产读取方是 `InjuryStateChangedWriter`,
而该写者**自身零生产构造点**(只在 `death_candidate_writers_test.cs:47`)⇒ 整条链不可达。
**本轮只加注、不改值** —— 改值须先裁「受伤实体状态词汇是否与 GDD 九态合并」,
那会动已登记 Kind 的载荷域,不在实现轮自行处置。

### 3.4 `RegistrySchema.cs:557-558` 的「字段错位」定性

原文把 `Lethality ∈ [0,3]` 对 GDD 的 `bool lethal` 称为「字段错位(既有 16 字段问题)」。
用户裁定推翻该定性:**保持 0-3,明确语义为「致死等级」** ⇒ `lethal ⇔ Lethality > 0`
是**正确定义**,不是代理近似。
**但原文的另一半仍成立且重要**:binder 把 JSON 的 `lethal: bool` 映射为 `? 2 : 0`
(`DiseaseRegistryBinder.cs:180`)⇒ 等级 1 与 3 **今天从作者态不可达**。
这是作者态 schema 的刻意简化,不是缺陷。已就地加注。

---

## 四、痊愈支与迁延支的实现口径

### 4.1 `W` / `TREND_EPS` 作入参,不写死

GDD Tuning `:1743-1744` 两者均标 `*待定*`。实现取 **`RecoveryWindowInput`** 三字段:

| 字段 | 含义 | 未配置时的行为 |
|---|---|---|
| `WindowTicks` | `W`(低位须稳定多久) | `0` ⇒ 未配置 |
| `TrendEpsRaw` | `TREND_EPS`(趋势死区) | — |
| `LowPositionHeldTicks` | 低位已持续多久(**状态**,非常量) | — |

`IsConfigured => WindowTicks > 0 && TrendEpsRaw >= 0`。
**`default` ⇒ 两支都不求值**,退回纯阈值分级。

**为什么 `default` 必须让整支不判**:若把 `W = 0` 当真值用,
「低位稳定 0 tick」恒成立 ⇒ 痊愈支被无条件放行 ⇒ **静默假绿**。
`test_unconfiguredWindow_recoveredNotEvaluated` 钉住这条。

### 4.2 六条件的判定次序

| # | 条件 | GDD 出处 | 缺它的后果 |
|---|---|---|---|
| ① | `position_agg < RECOVER` | `:1114` | — |
| ② | 无 `relapse.*` 块 | `:1119` / R3.3 | 「好转 → 复发」的医学链路消失 |
| ③ | ¬豁免类(`plateau ∧ ¬lethal`) | R3.6 | 神经衰弱落地即成「痊愈」,「他还在病着」为假 |
| ④ | 可愈性谓词(`self_limit` 型) | 校验 16 | 噪声 / 负 bonus 把不可愈病种压进痊愈 |
| ⑤ | 非上升期门(`trend ≤ EPS` 或 `τ > τ_peak`) | 四轮 K5 | 病刚发(τ<incubation)就判愈 |
| ⑥ | 低位稳定 ≥ `W` | B5 | ±σ 抖动把终态判成随机 |

### 4.3 迁延支

GDD `:1125`「其余 ← 迁延,由 `sign(trend_backward_agg)` 分段」。
**分段的是迁延内部的呈现(进展 / 好转),九态里只有「迁延」一个态**
⇒ 本支只判「是否迁延」,不产出子状态。判据 = 未达任何阈值 ∧ 未达痊愈 ∧ 趋势非上升。

---

## 五、多病聚合与 argmax 的 `d`

### 5.1 两个 max 是独立求的

GDD `:1301-1303` 明写 `position_agg = max_d position_d` 与
`trend_backward_agg = max_d trend_backward_d` **各自取大**。
⇒ **不保证来自同一个 `d`**。`test_trendAgg_isIndependentMax_notFromDominant` 钉住这条。

### 5.2 并列裁决 = `DiseaseId` 最小者

GDD `:1120`。**这不是优化,是确定性要求**:
若并列时取「先到者」,结果取决于 `D` 的遍历序,而遍历序来自事件流入流序 ⇒
同一组病程在不同到达序下会选出**不同的 `d`** ⇒ F4 的两门取到不同条目 ⇒ 逐位重放不一致。

实现写成「大于 **或**(等于 ∧ id 更小)才替换」,不是只写「大于」。
`test_tie_picksSmallestDiseaseId_bothOrders` 用**两种遍历序**钉住。

### 5.3 argmax 必须返回 `d` 本身

F4 的 `LethalFor` / `SelfLimited` 两门**吃单个 `d`**。只回传 `position_agg` 的数值
而不同时回传「这个值来自哪个 `d`」,调用方就得**再扫一遍** —— 那第二次遍历
**没有 GDD 依据**,且两次遍历可能选出不同的 `d`。

---

## 六、生产接线(`DiseaseVitalsService.StepAll`)

### 6.1 接了什么

```
StepAll(tick) → 每 course:
  ProgressionEvaluator.Evaluate(...)  →  _results[p]
  EvaluateOutcome(course, entry, result, tick)  →  _outcomes[p]      ← 新增
```

`GetOutcome(PatientId)` 是读取面。**本类全程零 `IEventSink.Append`** ——
「接 F4 求值但不写流」是用户裁定,`PatientOutcomeChanged` 仍归 9 的写者轮。

### 6.2 接线面上的三处诚实边界(已在代码注释中逐条写明)

| # | 边界 | 理由 |
|---|---|---|
| ① | **`position_agg` 退化 = 单病种投影** | `DiseaseCourseBook.ApplyOnset` 对同一病人的第二次 `DiseaseOnset` **fail-loud 拒绝**(「病人出现是单次事件」)⇒ 每病人恰一个 course ⇒ `D` 恒单元素,`max_d` 退化为恒等。**刻意不调** `DiseaseAggregator`:对单元素集调它只是绕一圈拿回同一个值,且会让「多病聚合」看起来已接线 |
| ② | **`trend_backward_agg` 恒 0** | `ProgressionEvaluator.ComputeTrend` 是 tripwire(恒 `Fix.Zero`)。故痊愈支的「非上升期门」当前**恒放行** —— 但被 ③ 挡在外面,不会静默判愈 |
| ③ | **`window` = `default`** | `W` / `TREND_EPS` 未裁定 ⇒ 痊愈支与迁延支**不求值**,退回纯阈值分级 |

### 6.3 照护通道恒空,且是结构性的

`CareApplied` 的写者未落(`WriterExistenceGate` 具名登记「24 医馆与案头写者未落」)
⇒ 流里**零**护理事件 ⇒ 无从算 `CareChannelInput`。
**本段不伪造该通道** —— 伪造 = 让「照护续命」在数据面不存在时看起来已接线。
破伤风的双约束(K8)由 `CareLeverApplicability.AppliesTo` 保证(有测试),
写者落地后在此处按 `AppliesTo(entry)` 分流即可,**求值侧不必改**。

### 6.4 「有效对因处置」判据提为唯一入口

原 `IsInTreatableBy` 是 `private`。接线需要**同一组三条件**
(`polarity = causal` ∧ `drug_potency 与 Base 异号` ∧ `∈ treatable_by`),而它在
`ComputeDrugContribution` 里已有一份。若再写一遍 ⇒ **静默分叉**:
F1 认为某处置有效、F4 认为它不续命 ⇒ 「玩家一直在治、病人照样死」,且没有任何测试会红。

⇒ 提为 `ProgressionEvaluator.IsEffectiveCausalDose(entry, in dose)`,**两处共用**。

---

## 七、证伪轮

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。**未用** `git checkout` / `git stash`。

基线 sha256(变异前):

```
8645ad496fa142406e5e5790ed627c042759ef53b122c5866ce723bb4965ca81  DiseaseVitalsService.cs
44e0a82b22e2523c7ff234dc69ac9cc8eb52b831b66b4b9fb0b38a86d634bbdd  ProgressionEvaluator.cs
b2d5cafba2daeab5d7267e406ec4f8f0448fc8f11108bb6dadc2b86cc30ef6d6  f4_wiring_test.cs
```

**还原后复核**(2026-10-22 19:1x):三件哈希与上表**逐字节相同**(`sha256sum` 实跑)。

| 变异 | 内容 | 结果 | 转红的测试 |
|---|---|---|---|
| **N6(第一次)** | 接线实体类别 `ActorClass.Patient` → `Player` | `4584 / 4548 / 0` | **零红 —— 假存活** |
| **N6′(订正后)** | 同上 | `4584 / 4547 / **1**` | `test_wiringUsesPatientClass_notPlayer`(恰好一条) |

### 7.1 N6 假存活 —— 我的靶子打错了(必留档)

**变异确实生效了**(`Patient` → `Player`),但**零条测试转红**。原因不是变异没打进去,
是**我的测试构造让两条路径同效**:

第一版夹具用「白名单伤情 `INJ_STAB` + 急性保持型(`self_limit = false`)」,于是:

| 传入类别 | `SelfLimited` 的取值路径 | 结果 |
|---|---|---|
| `Patient` | 注册表原值 | `false` |
| `Player` | 战伤破例(白名单 ∧ lethal) | `false` |

**两者都是 `false`** ⇒ 死亡支在两种类别下同样开启 ⇒ 都判死 ⇒ 断言
`AreEqual(Deceased, ...)` **恒真** ⇒ 变异不可观测。

这正是本项目已登记的「**变异打错靶**」形态(见 `project-falsification-probe-pitfalls`)。
它与「变异没生效」的表现**完全相同**(都是零红),只能靠**读变异后的代码确认改动真的在盘上**
来区分 —— 本轮已确认(`grep` 到 `ActorClass.Player` 在变异版本里)。

**订正**:让两条路径真的分叉 —— 注册表 `self_limit = true`:

| 传入类别 | `SelfLimited` | 死亡支 | 断言 |
|---|---|---|---|
| `Patient` | `true`(注册表原值) | **关闭** | 不死 |
| `Player` | `false`(破例翻转) | 开启 | 死 |

判据改为 **`AreNotEqual(Deceased, ...)`** ⇒ 误传 `Player` 时转红。

**连带的一个坑**:`self_limit = true` 会让曲线在 `τ ≥ τ_peak` 后指数衰减。
若 `τ_fall` 取常规值,`position` 会在窗口到期前掉到 `DEATH` 之下 ⇒ **两种类别都不死**
⇒ **又一次假存活**。故订正版取 `τ_fall = 100000` tick,使 `τ = 3610` 时
`position ≈ 0.676` 仍 ≥ `DEATH = 0.58` ⇒ 分叉只发生在「门」那一层。

### 7.2 N6′ 结果

**变异**:`DiseaseVitalsService.cs:368` 的 `ActorClass.Patient` → `ActorClass.Player`
(注释未动 —— 变异只落在实参上,`grep` 已确认改动在盘上)。

**变异后 sha256**:`e206744950e128a42c9782bbe6ae975e1397b8cb5c0f996f27610e06cdaaa557`

**实测**(全量 EditMode,`Logs/test-editmode-round8.xml`):

```
total=4584  passed=4547  failed=1  inconclusive=0  skipped=36
```

**转红的测试恰好一条**:

```
Failed | DaYiJingCheng.Tests.DiseaseSimulation.F4WiringTest.test_wiringUsesPatientClass_notPlayer
```

| 项 | N6(假存活) | N6′(订正后) |
|---|---|---|
| total / passed / failed | `4584 / 4548 / 0` | `4584 / 4547 / **1**` |
| 转红测试 | 无 | `test_wiringUsesPatientClass_notPlayer` |
| 判定 | 靶子打错,不可观测 | **变异可观测,判据成立** |

**差分自洽**:`4548 − 4547 = 1` ⇒ 恰好一条由绿转红,无连带失败、无连带跳过
(`skipped` 两轮同为 `36`)。这说明该变异**只**被这一条测试钉住 ——
其余 4546 条与该实参无关,符合预期。

**还原**:`cp` 回备份 ⇒ sha256 = `8645ad496fa142406e5e5790ed627c042759ef53b122c5866ce723bb4965ca81`,
与 §七 基线**逐字节相同**;`grep` 确认 `:368` 已回到 `ActorClass.Patient`。
**未用** `git checkout` / `git stash`。

---

## 八、回归(带三元组)

### 8.1 本轮终值

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 19:06 CST` |
| 批次 | 全量 EditMode(F4 接线 + 43 条新测试;**N6 靶子订正后**的终值) |
| commit | `655c8a3`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-editmode-round7.xml` |

```
total=4584  passed=4548  failed=0  inconclusive=0  skipped=36
```

> ⚠️ **为什么用 round7 而不是 round6**:round6 跑在**订正测试之前**(夹具仍是
> 那个两条路径同效的版本),数字虽相同但**不构成订正版的基线**。round7 是
> 订正后重跑的结果;round8 是 N6′ 变异轮(见 §7.2),**不得**当基线引用。

**差分核对**:

| 轮次 | total | passed | 新增 |
|---|---|---|---|
| round4(第一轮终值,§6.1) | 4541 | 4505 | — |
| round7(本轮终值) | 4584 | 4548 | **+43 / +43** |

| 新增测试 | 条数 |
|---|---|
| `CareLeverApplicabilityTest` | 7 |
| `DiseaseAggregatorTest` | 9 |
| `F4RecoveredAndChronicTest` | 19 |
| `F4WiringTest` | 8 |
| **合计** | **43** |

`4584 − 4541 = 43` = `4548 − 4505` ✓ ⇒ 零回归,且新增数是算出来的。

### 8.2 PlayMode(本轮补跑)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 19:11 CST` |
| 批次 | 全量 PlayMode(本轮首次跑,前一次是 `17:53` 的 `test-playmode-full-20261022.xml`,早于本轮全部改动) |
| commit | `655c8a3`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-playmode-round3.xml` |

```
total=202  passed=202  failed=0  inconclusive=0  skipped=0
```

**与 `17:53` 批次对比**:两批同为 `202 / 202 / 0`,**零回归**。
但 `17:53` 那批**跑在本轮改动之前**,不能当「本轮 PlayMode 已验」的凭据 ——
本轮实际重跑于 `19:11`,上表是**本轮实跑**的值。

> ⚠️ **PlayMode 对 F4 的覆盖 = 「跑到过」,不是「验过」**(实测口径):
>
> | 问题 | 实测 | 含义 |
> |---|---|---|
> | PlayMode 里有没有测试**引用** `F4OutcomeEvaluator` / `GetOutcome` / `F4Wiring`? | `grep -rln` ⇒ **零命中** | **零条**测试断言 F4 的输出 |
> | PlayMode 会不会**执行**到 F4? | `vertical_slice_test.cs:279/293/319/339` 调 `VitalsService.OnTickEdge` ⇒ 走 `StepAll` ⇒ 走 `EvaluateOutcome` | **会执行** ⇒ F4 若抛异常/崩,PlayMode 会红 |
>
> ⇒ PlayMode 全绿证明的是「F4 求值在真 tick 驱动下**不崩**」+「既有 202 条未回归」;
> **不证明**「F4 判出的九态是对的」—— 那由 `F4WiringTest` 的断言链证明(见 §六)。
> 两者是**互补**的,不可互相顶替。

---

## 九、明确未实现(禁借绿)

| # | 未做 | 影响 / 归属 |
|---|---|---|
| ① | **`PatientOutcomeChanged` 未写** | 用户裁定「接 F4 求值,但不写流」。终态落流归 9 的写者轮(ADR-032) |
| ② | **多病聚合(F5)未接进 Step** | 档案簿不支持一病人多 course(`ApplyOnset` 拒绝第二次 onset)。`DiseaseAggregator` 已实现且有测试,但**调用点 = 0** |
| ③ | **`trend_backward` 未实现** | `ProgressionEvaluator.ComputeTrend` 是 tripwire(恒 0)。`trend_backward` 的真差分需要 `W`(未裁定) |
| ④ | **照护通道未接** | `CareApplied` 写者未落(24 轮) |
| ⑤ | **`W` / `TREND_EPS` 无数值** | 归数值轮。本批只落结构 |
| ⑥ | ~~**PlayMode 未跑**~~ → ✅ **本轮已跑** | `202/202/0/0/0`,见 §八之二。**原文不删**:本轮开工时它确实是未跑状态 |
| ⑦ | **`InjuryState` 与 GDD 九态未合并** | 见 §3.3。合并须先裁「受伤实体状态词汇是否与 GDD 九态统一」,会动已登记 Kind 载荷域 |
| ⑧ | **潜伏 / 发病 / 进展 / 好转 四态无枚举落点** | 它们是 `τ` 与 `sign(trend)` 的函数,不是 `position` 阈值的函数;GDD 也未计划让其成为跨系统枚举 |

⇒ **能说的**:「F4 的九支判据全部实现,且已接进生产 tick 循环;死亡/昏迷/危殆/轻症分级四支
在真实读数上可观测,痊愈/迁延两支因 `W` 未裁定而**结构就位、求值不启用**」。
⇒ **不能说的**:「F4 全部九态在游戏里可见」「多病聚合已生效」「照护能续命」。

---

## 十、复算命令

```bash
cd unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/test-editmode-round7.xml" \
  -logFile "$PWD/Logs/test-editmode-round7.log"
# 预期:total=4584 passed=4548 failed=0 skipped=36

python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('Logs/test-editmode-round7.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"

# 新增测试按类计数(与 43 对账)
for c in CareLeverApplicabilityTest DiseaseAggregatorTest F4RecoveredAndChronicTest F4WiringTest; do
  printf "%s: " "$c"
  grep -o "methodname=\"[^\"]*\" classname=\"DaYiJingCheng.Tests.[A-Za-z.]*$c\"" Logs/test-editmode-round7.xml | wc -l
done

# N6′ 变异轮的判据复算(变异后跑,期望恰好一条红)
python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('Logs/test-editmode-round8.xml').getroot()
print({k:r.get(k) for k in ('total','passed','failed','skipped')})
for tc in r.iter('test-case'):
    if tc.get('result')=='Failed': print('FAILED:', tc.get('fullname'))
"
# 预期:failed=1,且唯一失败 = ...F4WiringTest.test_wiringUsesPatientClass_notPlayer

# PlayMode 全量(§8.2)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/Logs/test-playmode-round3.xml" \
  -logFile "$PWD/Logs/test-playmode-round3.log"
python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('Logs/test-playmode-round3.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"
# 预期:total=202 passed=202 failed=0 skipped=0
```
