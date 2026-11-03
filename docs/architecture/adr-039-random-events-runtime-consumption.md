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
- ⚠️ **生产今日仍恒 `NoPool`**:候选池为空(37 / 27 内容未注入)⇒ 预算与抽数的判据
  在**端到端**不可达,只有夹具可达。**不得**把本轮写成「52 已跑起来」。
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
| R3 | 端到端不可达(空池) | 夹具用合成 key 集驱动;证据里如实记「生产恒 NoPool」 |
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
| V-7 | `dataProvider == null` ⇒ director 的生成链为 null(未接线),不抛 | **无载体** —— `CompositionRoot.Assemble` 内的一行分支,现有 `composition_root_test` 未断言生成链指针 | **NOT-RUN** |

**V-1…V-6 已于同日实现轮落地并跑绿**;证伪轮 M11 / M12 / M13 / M14b 各转红
(证据原件 = `production/qa/evidence/impl-adr039-random-events-consumption-2026-11-03.md`)。
**V-7 仍 `NOT-RUN`** —— 不得按「已实现」计入判据面;补它需要一条整机装配夹具,
归下一轮(见证据 §六)。

⚠️ **端到端仍不可达**:生产候选池为空(37 / 27 内容未注入)⇒ 链恒 `NoPool`,
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
