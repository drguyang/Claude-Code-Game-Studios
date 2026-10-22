# `trend_backward` 真差分 + 接线语义订正 —— 证据(2026-10-22 第四轮)

**批次**:S18 后续 · 第三轮遗留项 ③ 的修复
**前置**:`impl-f4-round3-recovery-and-wiring-2026-10-22.md`(第三轮,commit `beb608f`)
**判据面**:GDD `disease-simulation.md` `:921-922`(定义)· `:926-928`(死区)· `:936-937`(因果性)· `:1111`(痊愈支的门)

---

## 一、本轮修的是什么

第三轮 §九 登记了 ③「`trend_backward` 未实现」,当时的表述是:

> `ProgressionEvaluator.ComputeTrend` 是 tripwire(恒 0)。`trend_backward` 的真差分需要 `W`(未裁定)

**本轮复核发现这条表述里混了两件事**,而其中一件是**缺陷而非未实现**:

| # | 事项 | 性质 |
|---|---|---|
| ① | `trend_backward` 的**差分逻辑**未实现 | 未实现(依赖 `W` 数值) |
| ② | 接线处把 **F2 的中心差分** `result.Trend` 传给了名为 `trendBackwardAgg` 的形参 | **语义缺陷** —— 与数值无关 |

② 之所以一直没被发现:两个量当时**都是 tripwire 恒 0**,传谁都一样。
但 GDD `:936-937` 明写「状态转移不再用中心差分 —— 中心差分要读 `t + W/2`(**未来**)
…… F4 在 tick `t` 上用未来时刻决定状态 = **非因果**」。
⇒ 一旦 `W` 被裁定、中心差分被实现,这条接线会让 F4 **读未来**。

**本轮处置**:
- 实现 `ComputeTrendBackward`(真后向差分,只读过去);
- 把 `Progress` 的求值提为**唯一实现** `ComputeProgress`,`Evaluate` 与 `ComputeTrendBackward` 共用;
- 接线改调 `ComputeTrendBackward`,不再传 `result.Trend`。

---

## 二、`W` / `TREND_EPS` 仍是入参,不代拍

| 量 | GDD 出处 | 状态 |
|---|---|---|
| `W` | Tuning `:1743` | `*待定*` ⇒ 调用方传 `windowTicks` |
| `TREND_EPS` | Tuning `:1744` | `*待定*` ⇒ 调用方传 `trendEpsRaw` |

`windowTicks ≤ 0` ⇒ `ComputeTrendBackward` 返回 0,**不求值**。
用 0 当真窗口会退化成 `(P(t) − P(t)) / 0` ⇒ 除零。

⇒ 本轮的接线仍传 `windowTicks: 0` ⇒ 生产路径上 `trend_backward` 仍是 0。
**结构与语义已就位,数值一填即生效** —— 这是「结构先落、数值作入参」那条裁定的延续。

---

## 三、`ComputeProgress` 提取的理由

`trend_backward(t)` 按定义要读 `Progress(t − W)`。若在求趋势处**另写一份** F1 式:

- 两份实现一旦分叉,「同一个 tick 的 Progress」就会有两个值;
- 而趋势正是**用它自己那份**算出来的 ⇒ 判据与读数不自洽;
- 且两处各自都「对」,**没有任何测试会红**。

⇒ 唯一实现,两处共用。与 `IsEffectiveDose` 提为公开入口(第三轮)同一纪律。

**过去的 tick 是合法输入**:`t − W` 可以早于 `onset_tick` 甚至为负 ——
潜伏抑制的 `τ < incubation` 分支把它压成 0;处置侧 `Δ < 0` 归零(AC-27 时间反演)。
这正是因果差分要的语义。

---

## 四、量纲陷阱(实现期自查)

`trend = diff / W`,其中 `W` 是 **tick 数**(整数),不是 `Fix`。

- ❌ 初版写 `Fix.FromRational(diffRaw, windowTicks)` —— 该函数内部做 `numerator << 16`,
  分子又被放大 2¹⁶ 倍 ⇒ 结果放大 65536 倍;
- ✅ 改为 `diff / new Fix(windowTicks * Fix.OneRaw)`,走 `Fix.Div` 一次舍入
  (与 `ExpNeg` 的 `new Fix(Δ×2¹⁶) / τ` 同法)。

---

## 五、测试(`TrendBackwardTest`,8 条)

| # | 测试 | 钉什么 |
|---|---|---|
| ① | `test_trendBackward_matchesDefinitionExactly` | 数值**逐位等于**定义式(独立复算,不经实现) |
| ② | `test_trendBackward_signFollowsDirection` | 上升为正、衰减为负(**双向**,抓整体取反) |
| ③ | `test_trendBackward_differsFromCentralDifference_onConvexRise` | 后向 ≠ 中心(本文件存在的理由) |
| ④ | `test_trendBackward_withinDeadzone_returnsZero` | `\|trend\| < TREND_EPS` ⇒ 返回 0 |
| ⑤ | `test_trendBackward_exactlyAtEps_isNotZeroed` | 死区是**严格小于** |
| ⑥ | `test_unconfiguredWindow_returnsZero_notDivideByZero` | `W ≤ 0` ⇒ 不求值、不除零 |
| ⑦ | `test_trendBackward_ignoresFutureTreatments` | **因果性**:`t` 之后的处置不影响 `t` |
| ⑧ | `test_trendBackward_beforeOnset_treatsProgressAsZero` | `t−W` 早于 onset ⇒ `P = 0` |

### 5.1 ④ 的第一版是废测试(已订正,留档)

第一版取 `t = 100000`。`Falling()` 的 `τ_fall = 1000` ⇒ 该点 `Progress` 已衰减到
**远小于 1 LSB** ⇒ `P(t) − P(t−W)` 本来就是 0。

测试里有一条前提断言:

```csharp
Fix raw = Trend(entry, t, w, eps: 0);
Assert.AreNotEqual(0, raw.Raw, "前提:无死区时该点趋势非零(否则本条退化为恒真,钉不住死区)");
```

**它当场转红了**(round9:`4592/4555/1`,唯一失败就是这条)。
⇒ 若没有那条前提,本条会在「差本来就是 0」时**恒绿** —— 又一个假存活形态。

**订正**:取 `t = 5000`(衰减中段,该点趋势约 60 raw)。
`τ = 8000` 也试过,差只剩个位数 raw,离 0 太近,做前提不够稳。

> 这是本轮唯一一次测试自身出错,已如实留档 —— 前提断言的价值正在于此。

---

## 六、回归(带三元组)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 19:29 CST` |
| 批次 | 全量 EditMode(趋势测试 8 条 + 接线订正) |
| commit | `beb608f`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-editmode-round10.xml` |

```
total=4592  passed=4556  failed=0  inconclusive=0  skipped=36
```

**差分核对**:

| 轮次 | total | passed | 新增 |
|---|---|---|---|
| round7(第三轮终值) | 4584 | 4548 | — |
| round10(本轮终值) | 4592 | 4556 | **+8 / +8** |

`4592 − 4584 = 8` = `4556 − 4548` ✓ ⇒ 零回归,新增数按类对账 = `TrendBackwardTest: 8`。

---

## 七、证伪轮

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。**未用** `git checkout` / `git stash`。

基线 sha256:

```
63c607224f79d8bca0ed435c518f60863a5ce361fbbc38ce323d84c22407a8fd  ProgressionEvaluator.cs
7edcb9e8d99b114859e675c73d848768cd14f2995cc4749003a47f119e5f05dd  DiseaseVitalsService.cs
```

| 变异 | 内容 | 结果 | 转红的测试 |
|---|---|---|---|
| **N7** | `now − before` → `before − now`(趋势取反) | `4592 / 4552 / **4**` | 见 §7.1 |
| **N8** | 后向差分 → 中心差分(读 `t ± W/2`) | `4592 / 4552 / **4**` | 见 §7.2 |
| **N9** | 未配置保护 `W ≤ 0` → `W == 0`(负 W 走进除法) | `4592 / 4555 / **1**` | 见 §7.3 |

### 7.1 N7 结果

转红 4 条:

```
test_trendBackward_beforeOnset_treatsProgressAsZero
test_trendBackward_exactlyAtEps_isNotZeroed
test_trendBackward_matchesDefinitionExactly
test_trendBackward_signFollowsDirection
```

差分 `4556 − 4552 = 4` ⇒ 恰好四条由绿转红,无连带失败、无连带跳过。

**未转红的三条**:`differsFromCentralDifference`(取反后仍不等于中心差分)、
`withinDeadzone_returnsZero`(取反后仍在死区内)、`ignoresFutureTreatments`(取反前后都不变)
—— 符合预期,它们钉的不是符号。

### 7.2 N8 结果 —— 后向改中心差分

转红 4 条:

```
test_trendBackward_beforeOnset_treatsProgressAsZero
test_trendBackward_differsFromCentralDifference_onConvexRise
test_trendBackward_ignoresFutureTreatments
test_trendBackward_matchesDefinitionExactly
```

差分 `4556 − 4552 = 4`,无连带失败、无连带跳过。

**这一轮是本文件的核心证伪**:它同时打中两条独立判据 ——
① `matchesDefinitionExactly`(数值不等于定义式);
② `ignoresFutureTreatments`(**因果性** —— `t` 之后的处置污染了 `t` 的读数)。
后者正是 GDD `:936-937` 点名禁止的失效模式。

**未转红的三条**:`signFollowsDirection`(中心差分的符号方向与后向一致)、
`withinDeadzone` / `exactlyAtEps`(死区判据与差分口径无关)
—— 符合预期。

### 7.3 N9 结果 —— 去掉负 W 保护

转红**恰好一条**:

```
test_unconfiguredWindow_returnsZero_notDivideByZero
```

差分 `4556 − 4555 = 1`。

**为什么只红一条**:该测试是唯一同时断言 `W = 0` **与** `W < 0` 两种输入的
(两条 `Assert`)。变异只放开了负 W 一侧 ⇒ 只有它转红。
其余 7 条都不传负 W,故不受影响 —— 这符合预期,也说明该测试的两条断言各司其职。

### 7.4 还原

三轮变异后 `cp` 回备份,两件 sha256 **逐字节**回到 §七 基线:

```
63c607224f79d8bca0ed435c518f60863a5ce361fbbc38ce323d84c22407a8fd  ProgressionEvaluator.cs
7edcb9e8d99b114859e675c73d848768cd14f2995cc4749003a47f119e5f05dd  DiseaseVitalsService.cs
```

**未用** `git checkout` / `git stash`。

**还原后复跑**(round11,`2026-10-22 19:40 CST`):

```
total=4592  passed=4556  failed=0  inconclusive=0  skipped=36
```

与 round10 逐字段相同 ⇒ 三轮变异**零残留**(若有残留,这里必然少 passed)。

---

## 八、明确未实现(禁借绿)

| # | 未做 | 影响 / 归属 |
|---|---|---|
| ① | **`W` / `TREND_EPS` 无数值** | 生产接线仍传 `windowTicks: 0` ⇒ `trend_backward` 在生产路径上仍是 0。归数值轮 |
| ② | **中心差分 `trend` 仍是 tripwire** | `ComputeTrend` 恒 0。它是 F2 的**展示**量(给 8 的读数),不归本轮 |
| ③ | **多病聚合(F5)未接进 Step** | 见 §8.1 —— 本轮复核发现阻塞点比第三轮登记的更具体 |
| ④ | **`PatientOutcomeChanged` 未写** | 按第三轮用户裁定「接求值,不写流」 |

⇒ **能说的**:「`trend_backward` 的差分逻辑已实现、已接进生产 tick 循环、
且被 8 条测试钉住(含因果性);`W` 一填即生效」。
⇒ **不能说的**:「生产路径上的趋势已经非零」「痊愈支已在游戏里生效」。

### 8.1 多病聚合的阻塞点(本轮复核,比第三轮登记更具体)

第三轮把 §九 ② 写成「档案簿不支持一病人多 course(`ApplyOnset` 拒绝第二次 onset)」。
本轮往下挖了一层,**真正卡住的不是档案簿,是载荷**:

| 层 | 现状 | 是否阻塞 |
|---|---|---|
| `DiseaseOnsetPayload` | **带 `DiseaseId`**(`:35`) | ✅ 足够建多个 course |
| `DiseaseCourseBook.ApplyOnset` | 以 `patient_id` 为键 ⇒ 第二病种**冲突** | ⚠️ 需改成 `(patient, disease)` 复合键 |
| `DrugTreatmentAppliedPayload` | **不带 `DiseaseId`**(`:195-201`) | ❌ **真正的阻塞** |
| `EmergencyTreatmentAppliedPayload` | **不带 `DiseaseId`**(`:172-180`,已核实) | ❌ |

⇒ **即使把档案簿改成复合键,处置也无法路由**:一条 `DrugTreatmentApplied` 只带
`patient` + `treatmentId`,当一个病人有多个 course 时,无法判断该剂量属于哪个病种。

**这不是实现轮能自行补的** —— 加 `disease_id` 到载荷 = **改一条已登记 Kind 的载荷域**
(ADR-024 A2 的 schema 面),须走 `entities.yaml` + kindgen + ADR 修订。
GDD `:135-142` 已定 `treatable_by` 是「处置 × 病种」二元关系,故 11 侧**知道**该给哪个病种;
缺的是把它写进载荷的裁决。

⇒ **归属**:9 / 11 的 GDD 轮 + ADR 窄修订。本文件只登记,不处置。

---

## 九、复算命令

```bash
cd unity
# 本轮终值(round10)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/test-editmode-round10.xml" \
  -logFile "$PWD/Logs/test-editmode-round10.log"
# 预期:total=4592 passed=4556 failed=0 skipped=36

python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('Logs/test-editmode-round10.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"

# 新增测试按类计数(与 +8 对账)
grep -o 'methodname="[^"]*" classname="DaYiJingCheng.Tests.[A-Za-z.]*TrendBackwardTest"' \
  Logs/test-editmode-round10.xml | wc -l
# 预期:8

# 三轮证伪(变异后跑,期望分别 4 / 4 / 1 条红)
for n in n7 n8 n9; do
  python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('Logs/test-editmode-$n.xml').getroot()
print('$n', {k:r.get(k) for k in ('total','passed','failed','skipped')})
for tc in r.iter('test-case'):
    if tc.get('result')=='Failed': print('   FAILED:', tc.get('fullname'))
"
done
```

> ⚠️ **`-projectPath` 必须用绝对路径**。本轮实测踩到一次:`cd unity` 写在前台命令里,
> 但后台任务的 cwd 会重置回仓库根 ⇒ `"$PWD"` 指到仓库根 ⇒ Unity 在**无 `Assets/` 的空工程**
> 上启动、`EXIT=1`、零结果件。改用绝对路径后正常。
> (仓库根也有 `ProjectSettings/` + `Packages/`,看起来像工程 —— 这正是 CLAUDE.md 点名的坑。)

---

## 十、本轮改动清单

| # | 件 | 文件 | 性质 |
|---|---|---|---|
| 1 | `ComputeProgress` 提取为唯一实现 | `Sim/ProgressionEvaluator.cs` | 改(重构) |
| 2 | `ComputeTrendBackward` 实现 | `Sim/ProgressionEvaluator.cs` | 改(新增方法) |
| 3 | 接线改调 `ComputeTrendBackward` | `Gameplay.Boot/DiseaseVitalsService.cs` | 改(语义订正) |
| 4 | `EvaluateOutcome` 增 `worldSeed` 形参 | `Gameplay.Boot/DiseaseVitalsService.cs` | 改(签名) |
| 5 | `TrendBackwardTest` 8 条 | `Tests/EditMode/DiseaseSimulation/trend_backward_test.cs` | **新建** |

**未改动**:`F4OutcomeEvaluator.cs` / `DiseaseAggregator.cs` / 任何 `entities.yaml` 载荷 schema。

