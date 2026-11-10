# ADR-039 — 52 运行期消费(密度预算 · 抽数上限 · 子区间节奏)

| Field | Value |
|-------|-------|
| **Status** | Accepted(2026-11-03 · 用户指令「继续 52 运行期消费」;§Decision D1–D7 由本轮就地拟定,未经用户逐条确认 —— 同 ADR-034 / 037 / 038 口径,实现轮前可改) |
| **Date** | 2026-11-03 |
| **Supersedes** | 无 |
| **Related** | ADR-038 §D7(52 数据集零生产消费的登记)· ADR-007(掷骰状态)· ADR-005(确定性)· ADR-009(世界流)· ADR-024(Kind 登记)· ADR-014(烘焙数据)· GDD `random-events.md` 规则一 / 规则六 / F1 / F2 |

---

## Context

### 1. 数据集已经落地,但四个值没有任何运行期读方

2026-11-03 的 ADR-038 §D4 落地了 `assets/data/52_random_events.json`:

```json
{ "ticks_per_day": {"$ref": "5_time.ticks_per_day"}, "roll_interval": 6000,
  "window_size": 6, "base": 2, "budget_max": 6 }
```

同日 Amendment A 只给 `roll_interval` 加了一个真实读方(`BootRoot.RequireCrossDatasetWindow`
的整除校验)。**另外三个值仍然零消费**,而 GDD 对它们的语义写得非常具体:

| 值 | GDD 出处 | 语义 |
|---|---|---|
| `ROLL_INTERVAL` | 规则一(`:121`) | 子区间长度 —— 「隔多久看一眼」,**不改变日预算**,只改变铺开程度 |
| `WINDOW_SIZE` | F1 步骤①(`:860`) | **每预算窗口(一天)** 至多抽几条 |
| `BASE` | F2(`:979`) | 每日基础事件数 |
| `BUDGET_MAX` | F2(`:979`) | 每日硬上限(下钳 0 后的上界) |

### 2. 现有生产代码把「节奏」与「抽数」混成了一个量

`CompositionRoot.cs:650` 有一个局部常量 `RandomEventWindowTicks = 200`,**同时**用作:

- `RandomEventSpawnChain` 的 tick 窗口(`tick - tick % _windowSize`);
- `F1PipelineInput.WindowSize`(Hamilton 配额的总名额)。

这两个量在 GDD 里是**不同的东西**(一个是 tick 数,一个是条数),而且 200 与数据集的
`roll_interval = 6000` / `window_size = 6` 都不相等 ⇒ 它是**第三个定义点**,
且同时违反 AC-5-05 的同值口径(52 只许引用 5 的日长派生量)与「单表单文件」纪律。

### 3. 密度预算有纯函数,但没有调用点

`EventBudget.ComputeDailyBudget(base, mods, budgetMax)`(`Sim/EventBudget.cs:136`)
实现了 F2 的 `clamp(BASE + ΣMod, 0, BUDGET_MAX)`,**零生产调用点**。
`RandomEventSpawnChain.TrySpawnAtTick` 里也没有任何「当日已来几条」的判断。

---

## Decision

### D1 — 四个值各自的唯一消费面

| 值 | 消费点 | 判据 |
|---|---|---|
| `ROLL_INTERVAL` | `TrySpawnAtTick` 的**边界门**:`tick % rollInterval != 0 ⇒ NotAtBoundary`(零写入) | 每个子区间**求值一次**(规则一 `:119`;不是每帧轮询) |
| `WINDOW_SIZE` | ① `F1PipelineInput.WindowSize`(Hamilton 配额总名额);② **当日抽取条数上限**:当日 `EventRolled` 计数 `≥ WINDOW_SIZE ⇒ DrawCapReached` | F1 步骤①「窗口内至多抽 N 条」 |
| `BASE` / `BUDGET_MAX` | `EventBudget.ComputeDailyBudget(BASE, mods, BUDGET_MAX)`;当日 `EventArrived` 计数 `≥ 预算 ⇒ OverBudget` | F2 规则六「超预算不刷、直接丢弃、不排队不延后」 |

### D2 — 状态从**事件流重构**,不引入内存计数器

- `rollsToday` = 当日窗口内 `EventRolled` 条数;
- `spawnsToday` = 当日窗口内 `EventArrived` 条数;
- 掷骰 `Ordinal` = `rollsToday`(不再是 `_ordinalInWindow`);
- `RollRequest.Window` = **预算窗口起点**(`tick - tick % ticksPerDay`),不再是 tick 子窗口。

理由(ADR-007 §三 / ADR-005):掷骰的每个输入必须可从事件流重构。
`_ordinalInWindow` / `_lastWindow` 两个内存字段读档后无法重建 ⇒ 删除。
计数是 `O(流长)` 的全扫,每 `ROLL_INTERVAL` 才发生一次(默认 6000 tick = 5 分钟),
流规模在 P0 量级下无感;优化会动「结构与流同源」这条保证,不预做。

### D3 — 预算 / 抽数门排在**掷骰之前**(与 F1 步骤④ 的字面次序不同,如实登记)

GDD F1 的次序是「② 抽取 → ④ 过密度预算 → 生成」。本 ADR 把预算与抽数门放在**掷骰之前**,
与既有的 CAP 预检(AC-15)同一位置、同一理由:

> `EventRolled` 一旦先落流,再被预算拒掉后半截,流里就会留下「掷过骰、事件没到」的孤儿行;
> 读档重放时它会凭空多算一次掷骰(`RandomEventSpawnChain.cs:13-15` 已就 CAP 写明同一条)。

⇒ **实现形**:预算/抽数门 `return`(零写入),不掷骰。
「超预算直接丢弃、不排队、不延后」的语义**不变** —— 丢弃点提前到掷骰前,效果相同且无孤儿行。

### D4 — `mods` 今日 = 空集

F2 的 `ΣMod` 输入(P0:`Mod_Eco` 待 6;`Mod_Rep` 归 P1a)今日**没有生产输入源**。
⇒ `ComputeDailyBudget(BASE, mods: null, BUDGET_MAX)` = `clamp(BASE, 0, MAX)` = **2**。
`ReputationMult` 按 GDD AC-52-20 在 P0 恒 1,不进预算。
**不得**为了让数字好看而塞一个假的 mod。

### D5 — 配置从 `IDataProvider` 取;取不到 ⇒ **不构造生成链**

`CompositionRoot` 在 `dataProvider == null`(未预载 / 夹具)时把 `spawnChain: null`
交给 `RandomEventsDirectorDriver`(该类已支持 `null = 未接线`)。
**不得**回落默认配置 —— 那会让「配置读错」与「一切正常」不可分(与 ADR-038 D3 同一条理由)。

⚠️ **provider 非 null 而表缺失 ⇒ 装配当场抛**(不降级)。这条是**取数方式**的直接推论:
`IDataProvider` 无 `TryLoad`,`CompositionRoot` 对 52 表做**装配期即时** Load,缺失即
`[E-13]` 级异常。**连带义务**:任何给 `CompositionRoot` 喂 **partial provider** 的装配夹具
(实测:五个 `MultiTableProvider`,分属 `reader_wiring_gate_test` / `action_axis_reader_gate_test` /
`real_registry_patient_{death,outcome_fold}_test` / `w_trend_eps_vitals_end_to_end_test`)
都必须登记 52 表,否则「只为装配面完备」的夹具会当场红 —— 这是**接线真的多了一条**的
正常后果,不是夹具坏了。

### D6 — 日期窗口用 **5 的 `TICKS_PER_DAY`**,与 52 的 `ticks_per_day` 同值

预算窗口 = 一天(DC-3)。运行时取 `RandomEventsDataSet.TicksPerDay`(烘焙期已由 `$ref` 与 5 同值断言),
作为 `TrySpawnAtTick` 的显式入参之一 —— 不再从 5 的运行时参数二次取,避免两处口径分叉。

### D7 — 删除 `RandomEventWindowTicks = 200`

它是「节奏」与「抽数」混用的第三个定义点,且与数据集两值都不等。
删除后:节奏 = `RollInterval`,抽数 = `WindowSize`,均来自 52 数据集。

---

## Consequences

- 52 的四个值全部有**生产消费点**;`EventBudget.ComputeDailyBudget` 首次有生产调用方。
- 生成链的**求值节奏**从「每 tick」变为「每 `ROLL_INTERVAL`」(默认 6000 tick = 5 分钟/次),
  这是 GDD 规则一的字面要求,也是性能上的改善。
- 掷骰序号与窗口改为从流重构 ⇒ 读档后续跑不会漂移。
- ⚠️ **生产今日仍恒 `NoPool`**:池**条目数据件不存在**(`TD-randomevents-03`)⇒ 预算与抽数的判据
  在**端到端**不可达,只有夹具可达。**不得**把本轮写成「52 已跑起来」。
  ⚠️ **义务方**:池条目归 **52**(GDD 规则二 `:160`),37 只提供医疗内容 —— 原稿沿用的
  「37 / 27 内容未注入」写法已订正(把两件事混成一句会让「52 还没出件」与「52 在等 37」不可分)。
- `RandomEventsDirectorDriver` 的 `LastSpawnOutcome` 新增三种具名结果
  (`NotAtBoundary` / `DrawCapReached` / `OverBudget`),观测面更细。

---

## Alternatives

| 方案 | 为何驳回 |
|---|---|
| **A. 保持零消费,只登记** | 数据集的四个值成为「写了没人读」的配置;AC-52-27 / AC-52-40 的判据永远没有执行体 |
| **B. 预算门放在掷骰之后(严格照 F1 步骤④)** | 会留下孤儿 `EventRolled`(与既有 CAP 预检的结论直接冲突);要它成立就得允许「掷了骰但不降临」的流形态,那是对 ADR-007 的改写,不在本件面内 |
| **C. 用内存计数器(每天重置)** | 读档后无法重建,违反 ADR-007 §三;与「事件流是唯一真源」冲突 |
| **D. 把 `ROLL_INTERVAL` / `WINDOW_SIZE` 并成一个参数(现状)** | 两个量纲不同(tick 数 vs 条数);GDD 明写 `ROLL_INTERVAL` 不改日预算 |
| **E. `mods` 塞一个假值让预算不等于 BASE** | 数值归用户;假输入会让 F2 的判据看起来跑过而实际没有 |

---

## Risks

| # | 风险 | 处置 |
|---|---|---|
| R1 | **预算/抽数门在掷骰之前**与 F1 字面次序不同 | D3 已写明理由与等价性;若将来要严格复刻 F1,须先裁「孤儿 EventRolled 合法」 |
| R2 | 每边界全流扫描 `O(n)` | 默认 5 分钟一次;逼近日预算量级前不优化(优化会动流同源结构) |
| R3 | 端到端不可达(空池) | 夹具用合成 key 集驱动;证据里如实记「生产恒 NoPool」 —— 已立 **`TD-randomevents-03`**(池条目件缺失;解除动作与义务方见债册) |
| R4 | `WINDOW_SIZE` 在 `BASE=2` 时通常不先绑定 | 它仍被消费(F1 配额总数 + 当日抽取计数上限);若用户上调 `BASE`,它会先绑定 |
| R5 | `ROLL_INTERVAL` 改值会改变求值节奏 | 已由 ADR-038 R2 的整除门守住;改值 = 改 `ConfigVersion` |

---

## ADR Dependencies

| 关系 | ADR | 说明 |
|---|---|---|
| **依赖** | ADR-038 §D4 / §R2 | 52 数据集的形状与 `ticks_per_day` 的 `$ref` 同值;整除门在启动期已校验 |
| **依赖** | ADR-007 §三 | 掷骰输入的每一项必须可从事件流重构 ⇒ D2 |
| **依赖** | ADR-005 / ADR-009 | 确定性 / 流为唯一真源 |
| **依赖** | ADR-024 | `EventRolled` / `EventArrived` 的 Kind 登记不变 |
| **约束** | GDD `random-events.md` 规则一 / 规则六 / F1 / F2 | 本 ADR 只实现其语义,不改公式 |

---

## Validation

| # | 判据 | 载体 | 状态 |
|---|---|---|---|
| V-1 | 非子区间边界 ⇒ `NotAtBoundary`,零写入、零掷骰 | `random_events_consumption_test.test_notAtBoundary_isNotEvaluated` | **PASS** |
| V-2 | 当日抽取数达 `WINDOW_SIZE` ⇒ `DrawCapReached`,零写入 | `…test_drawCapReached_stopsDrawing` | **PASS** |
| V-3 | 当日降临数达 `clamp(BASE,∅,MAX)` ⇒ `OverBudget`,零写入 | `…test_overBudget_stopsSpawning` / `…test_baseZero_meansNoSpawning` / `…test_budgetMax_clampsBaseDown` | **PASS** |
| V-4 | 界内 ⇒ 恰两条(先 `EventRolled` 后 `EventArrived`),ordinal = 当日已掷数 | `…test_atBoundary_evaluatesAndSpawns` / `…test_rollRequest_usesDayWindow_andStreamOrdinal` | **PASS** |
| V-5 | 换日 ⇒ 计数归零、`RollRequest.Window` = 新日起点 | `…test_newDay_resetsCounts` / `…test_rollRequest_usesDayWindow_andStreamOrdinal` | **PASS** |
| V-6 | `F1PipelineInput.WindowSize` 取自数据集的 `window_size`(不是 tick 数) | `…test_f1WindowSize_comesFromConfig_notTickWindow` | **PASS** |
| V-7 | `dataProvider == null` ⇒ director 的生成链为 null(未接线),不抛 | **成对两条**(缺任一即空洞正控):<br>· 反侧 = `random_events_consumption_test.test_assembly_withoutProvider_doesNotBuildChain`<br>· 正侧 = `reader_wiring_gate_test.test_readers_assembly_buildsSpawnChain_whenRandomEventsTablePresent` | **PASS** |

**V-1…V-7 全部有载体**;证伪轮 M11 / M12 / M13 / M14b / M15 / M16 各转红
(证据原件 = `production/qa/evidence/impl-adr039-random-events-consumption-2026-11-03.md`)。
⚠️ **V-7 必须是成对的**:只有「缺省 ⇒ null」一侧时,把 `if (dataProvider != null)` 整块删掉
仍然绿(F4 空洞正控)——M16(分支恒假)打的是正侧,M15(分支恒真)打的是反侧,两枪各中一条。

⚠️ **端到端仍不可达**:池条目数据件不存在(`TD-randomevents-03`)⇒ 链恒 `NoPool`,
V-2 / V-3 两道门**只有夹具可达**。本表绿 = 「门的执行体存在且可证伪」,
**不是**「52 已在生产里按预算刷事件」。**禁借绿。**

---

## Engine Compatibility

**LOW**。纯 C# 整数逻辑与事件流读取,零 post-cutoff 引擎 API。

---

## GDD Requirements Addressed

| AC | 本 ADR 的关系 |
|---|---|
| `AC-52-27`(预算下钳 0) | `ComputeDailyBudget` 首次有生产调用点;夹具覆盖 BASE=0 与负 mod 的纯函数面 |
| `AC-52-40`(子区间求值、无每帧轮询) | D1 的边界门即执行体 |
| `AC-52-26` / `AC-52-29`(超预算丢弃、不排队) | D3 的「掷骰前返回」是它们的可执行形 |
| `AC-52-48`(无队列) | 同 D3;本轮不引入任何队列 |
| `DC-3`(`ticks_per_day` 与 5 同值) | D6 使用烘焙期已断言的 `RandomEventsDataSet.TicksPerDay` |
---

## 🔁 2026-11-03 后续轮 —— 池条目表落地 + F1 步骤② 加权抽取

> 本轮由用户裁定「给数值,我一次做到底」驱动。**§D8–D13 由实现轮就地拟定**,未经逐条确认
> (同 ADR-034 / 037 / 038 口径)。

### D8 — 池条目是 **52 的数据件**,不是 37 的

承 GDD `random-events.md:160` 的裁定:「**52 拥有「池条目与调度」,37 拥有「医疗内容」**」。
⇒ 新增 `assets/data/52_event_pool.json` → `52_event_pool.cooked.bytes`(独立产物,
与 `52_random_events` 的四个调度标量**分开烘** —— 条目随内容增删,标量很少动)。
⚠️ 原注释与 ADR 文本写「52 的池内容归 37」是**错的**,已在代码 / ADR / 债册三处订正。

### D9 — `handler` 是 52 ↔ 内容系统的唯一接缝

条目带 `handler ∈ {encounter, medical}`:`encounter` = 27 敌人 AI,`medical` = 37 病例系统。
52 **不决定来了是谁**,只回答「该问谁要内容」。
⚠️ **仍未接的一条**:`medical` 条目 → 37 病种的映射表不存在 ⇒ `spawnPatient` 缝**刻意抛**
(返回假病种会让「有人开始生成病人了」静默通过)。`encounter` 条目不受影响。

### D10 — 均匀抽取 → **加权 CDF walk**(本轮的实质缺陷修复)

`EventDraw.Pick` 原实现是 `h % keys.Count` —— **完全无视 `W_base`**。
GDD `:672` 逐字警告过这种形态:「一个无视权重的实现**能通过 AC-52-06 却完全无视权重**」。
现按 GDD `:678-688` 实现:`S = SplitMix64(seed); C = ΣW_i; r = S mod C;` 按 **key 升序**游走。
`ΣW = 0` ⇒ **返回 false 而不是抛**(GDD Edge Cases `:1065`:该档跳过本轮、不报错)。

### D11 — 权重构成独立成件(`Sim/EventWeighting.cs`)

`W_i = W_base_i × TODMult_i × HistoryMult_i` 做成纯函数,输入 = (池表, 档, 是否夜间, 历史)。
理由:GDD `:694` 要求「`W_i` 的构成输入同样必须能从流重构」—— 散在装配层就无法单测。
**P0 边界**:`TODMult` 只有「夜间 · 威胁档」一项(AC-52-45);`HistoryMult ≡ 1`(无 `HIST_W` 表);
`KeyGate` 恒放行(无输入源)。三处都在源码里具名。

### D12 — `RollRequest` 增 `Tick`(四参形)

`TODMult` 是**昼夜**的函数,而昼夜在**一天之内会翻转** ⇒ 光有 `Win`(日起点)定不了权重。
掷骰发生在某个具体 tick,该值**已可从流重建**(`EventRolled` 的 `SimEvent.Tick`)
⇒ 不引入第四个不可重构输入。三参形保留为兼容(**仅供不关心昼夜的夹具**)。

### D13 — 三处装配连带义务

① `CompositionRoot` 装配期即时 `Load<EventPoolDataSet>()` + `ResolveTimeParams()`
⇒ 五个 partial `MultiTableProvider` 夹具各补两行(池表 + 5 时间参数);
② `cooked_drift_all_artifacts_test` 的 `BakedArtifacts` +1、`pinnedDeclared` 42 → **43**;
③ Addressables catalog 须重建(`EnsureDataCoreGroup` + `BuildAddressablesContent`)。

### 用户裁定的数值(2026-11-03)

| 项 | 值 | 出处 |
|---|---|---|
| `W_base`(P0 九条) | **全部 100** | 用户裁定;GDD「最小单位个数」的干净基准 |
| `TODMult`(夜 · 威胁档) | **3/2** | 用户裁定 |
| `TOD_MULT_MAX` | **2/1** | 用户裁定;构建期拒收超上界 |
| `Mod_Eco` / `Mod_Rep` | **整体推 P1a**(2026-11-03 用户裁定;见 `TD-randomevents-04` 的裁定块) | 值仍空集;`Mod_Rep` 无值来源(#31 属 P1a),`Mod_Eco` 在 P0 无可观测面 |

### 本轮 Validation

| # | 判据 | 载体 | 状态 |
|---|---|---|---|
| V-8 | 该档 `ΣW = 0` ⇒ `ZeroWeight`,不掷骰不写入 | `random_events_consumption_test.test_zeroWeight_skipsWithoutRollingOrWriting` | **PASS** |
| V-9 | 白天 `W_i = W_base`;夜间威胁档 `×3/2`;其余档不受影响 | `event_weighting_test`(13 条) | **PASS** |
| V-10 | 舍入 = `ROUND_HALF_AWAY_FROM_ZERO`(3×3/2=4.5 ⇒ **5**,非 4) | `event_weighting_test.test_oddWeight_roundsHalfAwayFromZero` | **PASS** |
| V-11 | 脚本条目**不进抽池** | `event_weighting_test.test_scriptEntries_areExcludedFromPool` | **PASS** |
| V-12 | 权重**真的改变命中分布**(3:1 ⇒ 48/64;翻转 ⇒ 18/64) | `event_weighting_test.test_distribution_changesWithWeight` | **PASS** |
| V-13 | 池表可烘焙 / 可解码 / 字节数自洽(292 B = 20 + 20 + 9×28) | `cooked_drift_all_artifacts_test`(43 份逐字节) | **PASS** |

⚠️ **V-12 的期望值(48/64、18/64、36/64)全部来自离线复算**,不是「看起来应该是一半」——
首版我写成 32/32 与 16/48,**离线复算当场证伪**(`h % 2` 在 0..63 上并不均匀)。
按纪律改断言而非改实现(承 `feedback_verify_assertions_offline_first`)。

### 仍未做(禁借绿)

| 项 | 状态 |
|---|---|
| `medical` 条目 → 37 病种映射 | **不存在** ⇒ `spawnPatient` 刻意抛(见 D9) |
| `Mod_Eco` / `Mod_Rep` | **整体推 P1a**(2026-11-03 用户裁定)⇒ 预算恒 = `BASE`;`TD-randomevents-04` 维持 open,解除条件后移为「P1a 关卡 + 四生态区 + 出诊上下文接通」 |
| `HistoryMult` / `KeyGate` | P0 无输入源 ⇒ 恒 1 / 恒放行(源码具名) |
| 端到端「事件真的降临」 | `encounter` 条目可走完链路;**医疗条目会在最后一步抛** |

---

## 🔁 2026-11-10 Amendment A —— 投放资格**前移到候选集**(窄修订 · Status 维持 Accepted)

> **提出方**:T17 的端到端实测(`production/qa/evidence/impl-t17-e2e-day-2026-11-10.md` §2.2)。
> **用户 2026-11-10 裁定「丙 · 真的改」**(候选集前置过滤),本段兑现。
> 本段**不改** D1–D13 的任何裁决,只在「哪个 key 进抽池」这一层补一条。

### 病因(实测,不是推理)

资格原先**只在抽中之后**判(`RandomEventSpawnChain` 的 `canSpawnPatient`)。而被拒的那一眼
**不写任何事件**(D3 的防孤儿行纪律),`Ordinal`(当日已掷数,从流重构)因此**不前进** ⇒
同一天里每一眼都落在同一个 `(win, tier, ordinal)` 上 ⇒ 抽到**同一个 key**、同样被拒 ⇒
**整日零产出**。

实测(种子 `0x5EED`,机会档 4 条候选,其中 2002 因技法全锁被拒):一天 6 眼
**全部** `PatientSpawnUnavailable`,零病人。跨日无此问题(`win = dayStart` 变)。

### 裁决

**投放资格前移到候选集**:不可产出的条目**根本不进抽池**。

- 落点:`EventWeighting.CandidatesOfTier(..., Func<int,bool> keyFilter = null)`;
  生产装配(`CompositionRoot`)传 `key => !requiresPatient(key) || canSpawnPatient(key)`。
- 语义:**抽到的必然可产** ⇒ `Ordinal` 照常前进 ⇒ 该日型在结构上消失。
- 链上的 `canSpawnPatient` 与 `PatientSpawnUnavailable` **保留为防御分支**:两处判据同源于
  `PatientAdmissionPolicy`(T18 乙),分叉即说明装配不一致,那时链会**响亮拒**。

### 代价(如实登记)

1. **当日分布变成条件分布**(在可产出条目上归一),不再是全档分布 ——
   这正是「不为做不成的事掷骰」应有的分布,但它与 D10 的 `W_i = W_base × TODMult` 的
   数值口径**不再逐条对齐**(分母集合变小)。**权重本身一字未改**。
2. **候选集现在是 `(档, tick, 流状态)` 的函数**,不再只是 `(档, tick)`。
   仍满足 ADR-016 §一 的三源不变量(流 + 烘焙数据 + 纯函数),重放可复现;
   但「同一天不同时刻的候选集可能不同」这件事从此是**设计**,不是缺陷。
3. **该档全部条目都被滤掉时** ⇒ `NoPool`。这是新的可达分支(改前该档总有候选),
   语义正确:今天没有一件**做得出**的事。

### 未改(禁借绿)

- `PatientSpawnUnavailable` 的**代码路径**仍在(防御分支),它的专打夹具仍在。
  改后生产上不再可达 —— 若将来某条夹具要它,须自己造出「候选集与链判据分叉」的状态。
