# T11 投放面复核 —— 实施与证据(2026-11-09)

> **范围**:`technique-unlock-map-2026-11-09.md` §8 **R1**「技法门做得太严 ⇒ 玩家能治的病太少」
> 的处置项「每章解锁后复核『可投放病种数 ≥ 3』」。
> **用户指令**:「走甲」。
> **上游**:T1(拆表)· T2(两列)· T4(技法门抽取)· T7(7 门技法 · 17 行)。
> **本件交付的是「信息」,不是「能力」** —— 这是 T 序列里第一次产出**玩家会直接感受到的数字**。

---

## 1. 病因

T1–T10 十步全绿,但**没有任何一步量过「玩家到底能遇到几种病」**。
技法门(`PatientDrawEligibility` ④)是**硬门**:一门都不解锁 ⇒ 整行拒收。
而 `treatable_by` 的覆盖面是作者态数据,不经实测看不出来。

本件把 R1 变成一条可跑的命令 + 一张表,并把当前值**钉进夹具**(内容一变就红)。

---

## 2. 实测(复算)

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.DiseaseSimulation.draw_surface_audit_test" \
  -testResults "$PWD/Logs/t11.xml" -logFile "$PWD/Logs/t11.log"
# 读 <output> 里的 [T11] 行
```

`@ 2026-11-09T16:15:22Z` · `50f79c1b` + 工作树 · 14/14 通过:

```text
[T11] 总行数 = 17
[T11] 空技法集即可投放 = 2 [DIS_NEURASTHENIA, INJ_DRUG_HARM]
[T11] 全解锁仍拒 = 0 []
[T11] 技法 0  覆盖 4 行:[INJ_STAB, INJ_LACERATION, INJ_HEMORRHAGE, INJ_CLAW]
[T11] 技法 1  覆盖 0 行:[]
[T11] 技法 2  覆盖 3 行:[INJ_BITE, DIS_TETANUS, INJ_BLUNT]
[T11] 技法 3  覆盖 1 行:[TOX_ENVENOMATION]
[T11] 技法 10 覆盖 5 行:[DIS_RHEUMATIC_FEVER, DIS_TYPHOID, DIS_MEASLES, DIS_DYSENTERY, DIS_FEBRILE_SEIZURE]
[T11] 技法 11 覆盖 1 行:[DIS_HEART_FAILURE]
[T11] 技法 12 覆盖 1 行:[DIS_MALARIA]
[T11] 起步集 = {0,1} ⇒ 可投放 6 行:INJ_STAB, INJ_LACERATION, INJ_HEMORRHAGE, INJ_CLAW,
                              DIS_NEURASTHENIA, INJ_DRUG_HARM
```

### 2.1 按解锁阶段看「能遇到几种**病**」(R1 的门槛口径)

| 阶段(已解锁技法) | 可投放**病种** | 来源 |
|---|---|---|
| `{0, 1}`(开局) | **1**(`DIS_NEURASTHENIA`)—— 且是**数据类型副作用**,见 §3.2 | 0 门技法覆盖它 |
| `+2` 清创缝合 | +1(破伤风)= **2** | 技法 2 |
| `+3` 蛇伤处置 | +1(蛇毒)= **3** | 技法 3 |
| `+10` | +5 = **8** | 技法 10 |
| `+11` / `+12` | +1 / +1 = **10** | 技法 11 / 12 |

**R1 判定(如实写,不粉饰)**:**解锁技法 10 之前,玩家能遇到的病种到不了 3**
(2 门时 = 2,3 门时 = 3 —— 而「3 门」要求先拿到蛇伤处置)。
⇒ 地图 §8 R1 的处置门槛**当前内容不满足**。这是**内容缺口**,不是代码缺陷;
补数据或调门槛是**下一轮要裁的事**,本件只负责把数字摆出来。

---

## 3. 实测揪出的两处内容/契约问题

### 3.1 技法 `1`(节奏型通气)覆盖 **0 行**

它是起步集的一员(T3 的 `StartingTechniques = {0,1}`),但**没有任何病种把它列进 `treatable_by`**
⇒ 这一门技法在投放面上**完全无效**:玩家开局就「会」它,却没有任何病用得上。
判据:`test_realData_techniqueOneCoversNothing`(期望值 0,补数据后应改)。

> 这是 T 序列此前**没有任何一步能发现**的形态 —— 它绿,因为「有了技法」和「技法有用」是两件事。

### 3.2 `DIS_NEURASTHENIA` 的 `[]` 不是「永拒」,是「放行」

`PatientDrawEligibility` 的 ③/④ 两条注释**互相矛盾**:③ 说空声明在产物里不留痕 ⇒ `Undeclared` 放行;
④ 说空数组 ⇒ 恒假 ⇒ 拒收。**实测落 ③**:`DIS_NEURASTHENIA` 出现在 `AlwaysDrawable`。

⇒ 已在源码就地订正(原文不删,加注并给判据出处)。
⇒ 副作用:`DIS_NEURASTHENIA` 与 `INJ_DRUG_HARM` 两行**永远可投放,与技法无关**
—— 这正是「开局 1 个病种」的来源,但它**不是设计意图**。

---

## 4. 改动清单

| # | 文件 | 层 | 内容 |
|---|---|---|---|
| 1 | `Gameplay.Boot/DrawSurfaceAudit.cs` | **新** | `TechniqueCoverage` / `DrawSurfaceReport` / `DrawSurfaceAudit.Compute` |
| 2 | `Gameplay.Boot/PatientDrawEligibility.cs` | 订正注释 | ④ 的「空数组 ⇒ 拒收」就地加注(2026-11-09 T11 实测) |
| 3 | `Tests/EditMode/DiseaseSimulation/draw_surface_audit_test.cs` | **新** | **14 条**判据 |

**零新增 Kind、零 registry、零存档格式、零 ADR、零烘焙产物。**
`Sim` / `Sim.Contracts` 引用集一字未动 —— 审计住 `Gameplay.Boot`,与判定面同一程序集。

---

## 5. 判据(14 条)

| # | 判据 | 判别力 |
|---|---|---|
| 1 | 未声明的行 ⇒ 空技法集也放行 | 把「没声明」当「永拒」⇒ 红 |
| 2 | 索引里没出现过的 key 判未声明 | 前提声明(先证明夹具地基) |
| 3 | `DrawableUnder({t}) == AlwaysDrawable ∪ Coverage(t)` | 两条代码路径分叉 ⇒ 红 |
| 4 | 逐技法覆盖 == 手算值 | 技法 id 空间取错 ⇒ 红 |
| 5 | 未声明行**不**计进技法覆盖 | 计进去 ⇒ 每门技法价值虚高(实测差 2 行/门) |
| 6 | 多技法取并集(不是交集) | 写成交集 ⇒ 红 |
| 7 | 等级门在技法门之后独立生效 | 两道门乘在一起 ⇒ 红 |
| 8 | index 为 null ⇒ 抛 | 不抛 ⇒ 恒 `NotWired`,测的不是投放面 |
| 9 | 负等级 ⇒ 抛 | — |
| 10 | `DrawableUnder(null)` ⇒ 抛 | 空集与 null 混用 ⇒ 红 |
| 11 | **真数据分类穷尽**:17 行都落在三类之一 | 审计静默丢行 ⇒ 红(**反空洞正控**) |
| 12 | **真数据快照**:起步集 = 6 行 | 内容一变就红,逼人回来复核 |
| 13 | **真数据**:`DIS_NEURASTHENIA` 在 `AlwaysDrawable` 且 `NeverDrawable` 为空 | 产物形状变了 ⇒ 红 |
| 14 | **真数据**:技法 1 覆盖 0 行 | 补了数据 ⇒ 红(该改的是期望值) |

> 第 11 条为什么必须有:审计读的是**真产物**,若哪天 `TechniqueDrawIndex` 改了形状导致
> 某类行**根本没被扫到**,其余 13 条全都可能仍然绿(它们各自只看自己那一面)。
> 「分类穷尽」是唯一能抓到「漏了一批行」的判据。

---

## 6. 回归

**三元组**:`2026-11-09T16:18:40Z`(EditMode 结束)/ `16:19:53Z`(PlayMode 结束) · **全量** · `50f79c1b` + 工作树。

| 套件 | 本次 | 对照基线(T10 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7310 / 7273 / 0 / 0 / 37`** | `7296 / 7259 / 0 / 0 / 37` @ `2026-11-09T15:58:38Z` · T10 批 · `50f79c1b` | **+14 / +14** |
| PlayMode(全量) | **`373 / 368 / 0 / 2 / 3`** | `373 / 368 / 0 / 2 / 3` @ `2026-11-09T15:59:54Z` · T10 批 · `50f79c1b` | **零变化** |

**等式核对**:EditMode 增量 **14** = `draw_surface_audit_test` 的 `[Test]` 条数。

### 6.1 中途两轮红(如实登记:都是我的夹具写错,不是实现错)

1. 首轮 `12 / 8 / 4`:三条合成夹具的期望值写错 —— 我把「未声明的行」当成了「被技法拒收」,
   实际 `Decide` 对未声明行**先于**技法门放行。**这一轮不是浪费**:它暴露了
   `Coverage` 与 `DrawableUnder` 语义不一致(覆盖表把未声明行算了进去 ⇒ 每门技法价值虚高),
   已改成「覆盖只算**真正被该技法解锁**的行」,并把不变式写成
   `DrawableUnder({t}) == AlwaysDrawable ∪ Coverage(t)`。
2. `CS0037`:`TechniqueCoverage` 是 `readonly struct`,不能赋 `null` 当哨兵 —— 改 `bool found`。

---

## 7. 未做(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **R1 门槛做成硬门** | **未做** | 「≥3 病种」是**设计门槛**;做成构建失败会让今天的内容直接红。本件只钉**快照**,判断留给设计 |
| **按章复核** | **未做** | 「每章解锁后」需要**章状态**,而它在运行期不存在(T5a 已如实登记)⇒ 今日只能按「技法集」复核 |
| 补内容(技法 1 / 第三章前病种数) | **归内容轮** | 本件只测量 |
| `INJ_DRUG_HARM` 未声明是否为预期 | **待裁** | 它是「自限型药毒」,T7 已登记为具名豁免;但「未声明 ⇒ 永久放行」未必是它想要的效果 |
