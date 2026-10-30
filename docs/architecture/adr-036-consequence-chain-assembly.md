# ADR-036: 53 判定链的生产装配(D 路真值 · 判定 · 后果发出)

## Status

✅ **Accepted(2026-10-30)** —— **只裁方向,不实现。** §Validation V-1…V-12 **全部未跑(禁借绿)**。

**三项用户裁定(2026-10-30,逐条照准)**:① **D 路真值 = 9 扩投影形状**;② **范围 = 全链一次接完**;
③ **产出物 = 先写 ADR 再实现**。
⚠️ **§Decision 的 D3–D6 与 §Alternatives Considered 由起草轮就地拟定,未经用户逐条确认**
—— 与 ADR-034 §Status 首段同口径,**实现轮之前可改**。

**本件的由来**:`TD-medcons-13` / `-18` / `-19` 三条 P0 面债的触发条件都写着「装配轮」,
而本轮实测发现它们的真实依赖**不在 53 内部** —— 见 §Context「Current State」。

🔁 **2026-10-30 补记 · R1 已核(用户指令「核 R1」)**:原 §Risks R1 写「9 的病程状态能否回答
『该 tick 是否已痊愈』**本轮未核**」。现已核:**能答,但今天答不出来** —— 判据在、调用点在、
访问器在,唯被一个**未裁的数值门**挡着(`trend_window_ticks = 0`);另查出一条**次序陷阱**
(`GetOutcome` 只返回当前态)。两条均见 §Risks R1 / R4 与 §Implementation Guidelines。
**D1 不回炉。**

🔁 **2026-10-30 实现轮补记 · 两项新裁定(用户逐条照准)** —— 实现 S3 时实测出**两处本件未覆盖的
接缝**,两条都由用户裁定,现补为 **D7 / D8**:

① **D7 · 「结案那一刻」的 D 须能回溯** —— 本件 §Risks R4 原写「`TryProject` 的 tick 只校验不回溯」,
   而 `Aggregate` 是按 `close.Tick` 问 D 的(`:305`);延迟窗内每条历史结案都早于当前 tick
   ⇒ 那条口径**在驱动上接不上**。裁定:**9 增「F4 状态转移历史」**(每病人只在状态变化时追加
   `(tick, state)`,≤ 9 条),适配器按「≤ 目标 tick 的最后一次转移」作答,**只有未来 tick 抛**。
   ⇒ §Risks R4 的第二段与 V-12 已就地订正(**原文留档**)。

② **D8 · `Inputs53 → JudgeInputs` 的桥,三个取值口径** —— 本件只写「driver 内调 `Judge.Evaluate`」,
   而 `Judge.Evaluate` 收的是 `JudgeInputs` 而非 `Inputs53`,桥从未被定义(生产侧
   `new JudgeInputs` 零调用点)。裁定:**C 轴命中位按轴查** `∃ care: axis.TryGetPolarity(病种 key, care.ActionId)`
   · **U 的盐**取世界级 `PatternRecognized` 中「盐 == `ComputeSaltedKey(seed, bit)` 且
   `bit ∈` 本病例 `DiseaseSet`」的第一条(bit 升序),无命中 ⇒ 退化中点 32768 ·
   **试药史位逐病人**(`TrialHistoryPredicate` 增逐病人重载 —— 流级口径会让甲的连续用药污染乙的结案)。

✅ **2026-10-30 实现轮已落(S1–S5 + S7)** —— 代码:`DiseaseVitalsService` 增状态转移历史与
`TryGetOutcomeAt` · `DiseaseProjectionAdapter` · `ConsequenceDriver`(含 D5/D6/D8 桥)·
`ConsequenceEmitter` 去重键 · `CompositionRoot` + `BootRoot` 装配 · `P0HostEventAuthority`(全仓首个
`IEventAuthority` 生产实装)· `TrialHistoryPredicate` 逐病人重载 + 每 tick 一次。
**§Validation V-1…V-12 全部有载体**(含 V-7 实跑数;S6 债册归口见本件 §Risks R1 与债册)。
定向回归 **242/242**(MedConsequences);**全量 EditMode `6148/6111/0/37`**(@ 2026-10-30 15:08:42 CST · 批次 = 本件实现轮 · `5982b520`+工作树),基线 `6124/6087/0/37` ⇒ **+24/+24**(= 新增 `[Test]` 计数);**全量 PlayMode `238/234/0/2/2`**(@ 15:09:49 CST)与基线**逐位相同**(零回归)。**S1b(数值轮)与 `-18` 的 D-2 半边仍 open**。

## Date

2026-10-30

## Last Verified

2026-10-30(所有 `file:line` 均当日实读;未跑任何构建 / 测试 / 烘焙)

## Decision Makers

- **用户**(三项裁定,2026-10-30;见 §Status)
- 起草依据:本轮机械勘察(见 §Context「Current State」的六条可复跑命令)

## Summary

53(用药后果)**整套判定链在生产侧不存在**:`ConsequenceEmitter` 零装配点、
`Judge.Evaluate` 零调用点、D 路(痊愈位)**没有任何真值来源**。
三条登记债把它写成「等装配轮」,但它们真正等的是 **9 的病程投影形状**。

本件裁六件事:
1. **D 路真值 = 9 扩 `DiseaseProjectionSnapshot`**(加痊愈位),实现住 9 的边界层;
2. **全链一次装配**:新增 `ConsequenceDriver`,tick 边沿驱动 `Aggregate → Judge → Emit`;
3. **表装载**经 `IDataProvider`,缺 provider ⇒ 空表惰性(**不发,不是不判**);
4. **处置 Kind 集**取三支(测试侧今日只覆盖一支 —— 生产口径是**新覆盖面**);
5. **D-1 契约**:读流与写流须同一引用,driver 构造期 fail-loud;
6. **D-3 去重**:键剔除 outcome(纵深防御)+ driver 内不一致即抛(上层 fail-loud)。

## Engine Compatibility

**不涉及任何引擎 API。** 纯 C# 装配与接口契约。
`Sim` / `Sim.Contracts` 的引用集**一字不改**(门 A / b2 门 / ADR-025 §① 继续成立);
新增代码住 `Gameplay.Boot`(已是引擎侧装配层)。
Engine Knowledge Risk **LOW**。

## ADR Dependencies

| ADR | 本件用到 | 状态 |
|---|---|---|
| **ADR-005** | 主机唯一写 · `ITickProvider` 驱动 `Step`(不由渲染帧驱动) | Accepted |
| **ADR-009** | 三问判据 / 域归属(效果进流,决策不进流)· §六 有界性 | Accepted |
| **ADR-016** | §一 **三源不变量** —— 53 不得因装配而获得事件流之外的输入 | Accepted |
| **ADR-025** | 程序集清单:装配住 `Gameplay.Boot`;`Sim` 引用集恰 = {BCL, `Sim.Contracts`} | Accepted |
| **ADR-029 / ADR-031** | 编码 / 解码唯一抽象点(装配层只传接口,不自己编码) | Accepted |
| **ADR-032** | `PatientOutcomeChanged` 是**病程终态**、逐病种 —— 本件**明确不复用它**当 D 路(Alt 1) | Accepted |
| **ADR-014** | 53 三张表的烘焙产物与地址常量 | Accepted |

**本件不依赖任何未 Accepted 的 ADR。**

## Context

### Problem Statement

三条 P0 面债(`TD-medcons-13` 结算器无生产装配点 · `-18` 三条装配期缺陷 · `-19` 生产装配面未被约束)
的触发条件都写着「装配轮」。但**「装配轮」到底要接什么**从未被裁决 —— 于是三条债互相引用、
指不到一个可开工的对象。

### Current State(逐条可复跑)

| # | 实测 | 命令 | 输出 |
|---|---|---|---|
| ① | `ConsequenceEmitter` **零生产装配点** | `grep -rn "new ConsequenceEmitter" --include=*.cs unity/Assets \| grep -v /Tests/` | 空 |
| ② | `Judge.Evaluate` **零生产调用点** | `grep -rn "Judge.Evaluate\|new Outcome53.JudgeInputs" --include=*.cs unity/Assets \| grep -v /Tests/` | 仅 `OutcomeTableCookedCodec.cs:111` 的一行注释 |
| ③ | D 路入口**只有 tick、没有痊愈位** | `sed -n '42,60p' unity/Assets/Sim/Consequences/ConsequenceInputAggregator.cs` | `DiseaseProjectionSnapshot` 仅 `Tick`;自述「形状归 9 修订轮」 |
| ④ | `CaseClosed` 载荷**无痊愈位** | `sed -n '23,34p' unity/Assets/Sim.Contracts/Payloads/CasePayloads.cs` | `{PatientId, CaseId, DiseaseSet, Treated}` |
| ⑤ | 病程终态**有载体但无生产调用点** | `grep -rn "PatientOutcomeChangedWriter" --include=*.cs unity/Assets \| grep -v /Tests/` | 只有类自身与门的注释;**零实例化** |
| ⑥ | 表装载面**已就绪** | `grep -n "EnsureDelayTable\|EnsureOutcomeTable" unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs` | `:346`/`:351` 分派 · `:511`/`:512` 预载 · `:758` 实现 |

⇒ **不是「算完没人接」,是「连算都不算」;而且算不了 —— D 路无真值。**

### Constraints

- **C1** `Sim` 不得 import 9 的程序集(ADR-025 §① / 门 A)。53 经 `IDiseaseProjection` 取 `D`,
  **接口住 `Sim.Consequences`,实现住边界层**(`ConsequenceInputAggregator.cs:42` 原文即如此)。
- **C2** 主机唯一写(ADR-005):非主机调用 `Emit` 须空转(`ConsequenceEmitter.cs` 首段已实现)。
- **C3** 事件率有界:后果 ≤ 每结案一次(ADR-009 §六);驱动频率 = tick(20 Hz),
  **不得随渲染帧提高**。
- **C4** 判定表 18 格中 `Recovered` 占 6 格 —— D 路缺真值 ⇒ 这 6 格**生产上永不可达**。
- **C5** 空表 / 空档位线的惰性形态已有先例(`CompositionRoot.cs:1467-1487` 的 `CostTable53` 装配)。

### Requirements

- **R-1** D 路须有**生产真值**,且语义正确(「结案那一刻该病人是否痊愈」,不是「病程终态」)。
- **R-2** 判定与发出须**同源**:同一 tick、同一条流、同一批输入。
- **R-3** 无数据源时须**惰性但不静默**(不发明后果,也不假装在跑)。
- **R-4** 三条装配期缺陷(D-1/D-2/D-3)须在**装配层**定契约,不得留给调用方纪律。

## Decision

### D1 · D 路真值 = 9 扩投影形状(用户裁定)

`DiseaseProjectionSnapshot` 增一个痊愈位(建议 `bool Recovered`);语义 = **该病人在该 tick 是否已痊愈**。
`IDiseaseProjection` 的**生产实现**住 9 的边界层(适配 9 的病程状态),
**不 import 9 内部类型**,53 只经接口取值(C1)。

⚠️ **本条的语义边界(最容易被读错的一处)**:53 的 `RecoveryLane` 是「**痊愈 @ 结案**」,
9 的 `PatientOutcome`(ADR-032)是「**病程终态**」。二者在结案那一刻**不恒等**
⇒ **不得**拿 `PatientOutcomeChanged` 直接当 D(见 Alt 1)。

⚠️ **可达性边界(R1 已核 · 2026-10-30)**:9 的痊愈判据**存在且已接线**
(`F4OutcomeEvaluator` 六条件合取 → `DiseaseVitalsService.cs:647` → 公开访问器 `GetOutcome(p) :217`),
但**今天不可达** —— 痊愈支受 `RecoveryWindowInput.IsConfigured`(`F4OutcomeEvaluator.cs:97`)门控,
而 `disease_registry.json` 实测 `trend_window_ticks = 0` / `trend_eps = 0`(数值轮未裁)
⇒ 两支都不求值 ⇒ **`InjuryState.Recovered` 生产上恒不可达**。

⇒ **本裁与 Alt 2(恒 `NotRecovered`)在今天的效果完全相同**,唯一差别是:
接了 D1,数值一填那 6 格**自动活**;Alt 2 是写死的,填了也不活。
**不得**把「D1 已接」读成「Recovered 6 格已可用」。

### D2 · 全链一次装配(用户裁定)

新增 `ConsequenceDriver`(住 `Gameplay.Boot`),`OnTickEdge(long tick)` 内依次:
`ConsequenceInputAggregator.Aggregate(...)` → `Outcome53.Judge.Evaluate(...)` →
`ConsequenceEmitter.Emit(stream, eligible, tick, out emitted)`。
注册进 `CompositionRootServices.OnTicks` —— 与 `EmergencyChannelDriver` / `EmergencyAttemptDriver`
**同构**(既有先例,非新机制)。

### D3 · 表装载经 `IDataProvider`,缺 provider ⇒ 惰性

`OutcomeTable53DataSet` / `DelayTable53DataSet` 一律 `dataProvider.Load<T>()`;
`dataProvider == null` ⇒ 空判定表(`bandCount = 1`)+ 空延迟表 ⇒ **全链不发**。
**⚠️ 空表 = 不发,不是不判** —— 须打一条显式日志,否则与「机制未启用」同形(R-3 / §Risks R2)。

### D4 · 处置 Kind 集 = 三支

`careKinds = { CareApplied, EmergencyTreatmentApplied, DrugTreatmentApplied }`
—— 与 `ConsequenceInputAggregator.CareRowOf` 的三臂 `switch` 对齐。

⚠️ **测试侧今日只喂 `{ CareApplied }`**(`medcons_aggregation_test.cs:133` 等)。
生产口径比测试宽 ⇒ **「生产 C 路覆盖」是新增面,不得拿现有夹具当已证**。

### D5 · D-1 契约:读流与写流须同一引用

`ConsequenceEmitter.Emit(stream, …)` 的 `stream`(读面)与构造时注入的 `IEventSink`(写面)
**必须是同一条流的两个面** —— 否则幂等差额(`CountEmittedByPatient`)读的是一个世界、
写的是另一个世界,**静默失效**。

判据落在 **`ConsequenceDriver` 构造期** `ReferenceEquals(stream, sink.Events)` 断言 fail-loud。
**不放进 `ConsequenceEmitter`** —— Emitter 只拿到 `IEventSink`,看不到 sink 的读面,
在 Emitter 里根本无法表达(Emitter 加参数 = 改已有签名,且仍测不出「同源」)。

### D6 · D-3 去重:键剔除 outcome(纵深防御)+ 上层 fail-loud

同一 `CaseClosed` 以**不同 outcome** 重喂 ⇒ 现去重键 `(emitTick, patient, outcome, closeTick, seq)`
含 outcome ⇒ **会发两条后果**,违 GDD「每病例至多一条」。

两层处置:
- **下层(纵深防御)**:`BuildDueRows` 的去重键**剔除 outcome** ⇒ 即使上游重喂,也只出一条;
- **上层(fail-loud)**:driver 内若发现同一 `CaseClosed` 两次投影的 outcome 不一致 ⇒ **抛** ——
  那是上游 bug,静默吞掉会让「为什么后果错了」永远查不出来。

🔁 **2026-10-30 实现轮实测订正(下层键式,原文留档)**:**只剔 `outcome` 不够**。
`emitTick = closeTick + DelayTable[outcome]` **由 outcome 派生** ⇒ 两个 outcome 延迟不同时
`emitTick` 也不同,四元组 `(emitTick, patient, closeTick, seq)` 仍不相等 ⇒ **照发两条**
(夹具 `test_sameCaseClosed_twoOutcomes_emitsExactlyOne_winnerIsEarliest` 首轮即红,`emitted = 2`)。
⇒ 现行键 = **`(patient, closeTick, closeSeq)`**(结案**身份**),`emitTick` 与 `outcome` 都不进身份键。
「零跨 tick 态」不变(仍是函数内的局部集)。上层 fail-loud 的**可达性**另见 D8 后的补注。

⚠️ **上层 fail-loud 今日零输入可达(如实登记)**:`Aggregate` 对每条 `CaseClosed` 只产一组,
同一条结案在同一 tick 内的 D 也不可能两个值 ⇒ **冲突在 `OnTickEdge` 的路径上构造不出来**。
它的载体只能是**直接调判据方法**(`ConsequenceDriver.AssertNoConflictingOutcomes`)。
按 ADR-035 N-4 的纪律:这是「**零输入可达的门**」,不得宣称「守卫已兑现」。

## Architecture

```
CompositionRoot(装配)
  ├─ stream.Events ─────────────┐
  ├─ eventAuthority ────────────┤
  ├─ encoder / decoder ────────┤
  ├─ IDataProvider.Load<OutcomeTable53DataSet / DelayTable53DataSet>
  └─ IDiseaseProjection(9 边界层适配)   │
                                        ▼
                         ConsequenceDriver(OnTickEdge)
                           ├─ Aggregate(stream, decoder, projection, careKinds) → Inputs53[]
                           ├─ Judge.Evaluate(JudgeInputs, table)               → Outcome
                           └─ ConsequenceEmitter.Emit(stream, eligible, tick)  → 世界流 Append
```

**单向**:driver 只读流、只经 `IEventSink` 写;不持游戏状态(承 ADR-013 §9 C3 / ADR-018 §一 的同构纪律)。

## Implementation Guidelines

- driver **不得**缓存跨 tick 的可变状态(承 AC-53-06「零跨 tick 态」)。
- ⚠️ **次序(R4)**:`ConsequenceDriver.OnTickEdge` 在 `BootRoot` 的边沿序列里须排在
  `_services.VitalsService.OnTickEdge(tick)`(`BootRoot.cs:1314`)**之后**。排之前 ⇒ 读上一 tick 的 F4 态,
  **静默偏差一个 tick 且无报错**。此约束须有夹具(V-11),不得只写在注释里。
- ⚠️ **`TryProject` 的 `tick` 只校验不回溯(R4)**:适配器拿到非当前 tick 须 fail-loud,
  不得近似成「当前态」—— 那会把「问某个历史时刻」静默答成「此刻」。
- `Aggregate` 每 tick **复制并全排序整条流** —— 见 §Performance Implications R3,实现轮须实测。
- `ConsequenceEmitter` 的 **precondition 不变**:调用方**不得 catch `Emit` 抛出的异常后于更低 tick 重试**
  (Append 逐条即时提交,吞异常再试 = 撞 BCD-码-3)。
  ⇒ driver 遇抛即**终止本 tick**,不重试、不回滚 —— 与 D-2 的处置同批裁。

## Key Interfaces

| 面 | 归属 | 本件是否改 |
|---|---|---|
| `IDiseaseProjection.TryProject(PatientId, long, out DiseaseProjectionSnapshot)` | `Sim.Consequences`(接口) | **不改签名**;`DiseaseProjectionSnapshot` **加一个痊愈位** |
| `ConsequenceInputAggregator.Aggregate(...)` | `Sim.Consequences` | 不改 |
| `Outcome53.Judge.Evaluate(...)` | `Sim.Consequences` | 不改 |
| `ConsequenceEmitter.Emit(...)` | `Sim.Consequences` | 不改签名;`BuildDueRows` 去重键剔除 outcome(D6) |
| `ConsequenceDriver` | `Gameplay.Boot` | **新增** |

## Alternatives Considered

### Alternative 1: D 路取病史流 `PatientOutcomeChanged` 的 outcome 位

载体类已在(`Sim/PatientOutcomeChangedWriter.cs`),`PatientOutcome` 也有 `Recovered` 成员。
**驳回三条**:① 语义不同 —— 它是**病程终态**(吸收态),不是「结案那一刻是否痊愈」;
② 它是**事件**不是**投影**(须先落流才可读,而 D 路求值发生在结案 tick 的同一批输入里,
存在「先有鸡还是先有蛋」的时序依赖);③ 该写者**生产侧零调用点**(§Context ⑤)。

### Alternative 2: 恒 `NotRecovered` 占位(最小闭环)

一天就能接完整链。**驳回**:判定表 18 格里 `Recovered` 占 6 格,恒不命中
⇒ 那 6 格**生产上永不可达**,而表面上是绿的 —— 与 ADR-035 驳回「运行期 clamp / 设计纪律」
同一理由(**本项目已五次抓过「自述顶替门」的假绿**)。

### Alternative 3: 只接发出半边(`ConsequenceEmitter` + 表装载),判定留给下一轮

**驳回**:`TD-medcons-19` 的原文就是「生产装配面未被约束」 —— 判定不在生产链上,
就没有任何装配面可供它约束;`TD-medcons-13` 同样解不了。

### Alternative 4: 让 53 直接读 9 的程序集

**驳回**:破 ADR-016 §一 三源不变量 + ADR-025 §① 引用集(门 A)。承 ADR-029 Alt 1 同因。

## Consequences

### Positive

- 53 的判定链**从零到有**,且每条边都有生产载体;
- 三条 P0 面债(`-13` / `-18` / `-19`)获得**同一个可开工对象**;
- D-1/D-3 两条静默缺陷在**装配层**被定死,不再依赖调用方纪律。

### Negative

- **跨域**:D1 要 9 的 GDD 轮同批改 —— 本件**不能单方面落地**(§Risks R1);
- 新增每 tick 的全流排序成本(R3)。

### Neutral

- `Sim` / `Sim.Contracts` 的引用集与公开面**一字不改**(除 `DiseaseProjectionSnapshot` 加一个只读字段)。

## Risks

### R1 · 9 的病程状态能回答「是否已痊愈」,**但今天被数值门挡住** —— ✅ 已核(2026-10-30)

**结论:能答。D1 不回炉。** 三条实测:

| # | 事实 | 出处 |
|---|---|---|
| ① | 9 **有**痊愈判据 —— F4 痊愈支 = 六条件合取(GDD `:1113-1120`) | `Sim/F4OutcomeEvaluator.cs:288`(返回 `InjuryState.Recovered`) |
| ② | 有**生产调用点**(不是「只有测试」) | `Gameplay.Boot/DiseaseVitalsService.cs:647` → `F4OutcomeEvaluator.Evaluate(...)` |
| ③ | 有**现成公开访问器**,S1 的适配器不用新造查询 | `DiseaseVitalsService.cs:217` `public InjuryState GetOutcome(PatientId p)` |

**但今天答不出来**:痊愈支受 `RecoveryWindowInput.IsConfigured`
(`WindowTicks > 0 && TrendEpsRaw >= 0`,`F4OutcomeEvaluator.cs:97`)门控,
而实测 `assets/data/disease_registry.json` 的 `trend_window_ticks = 0`、`trend_eps = 0`
(数值轮未裁)⇒ 痊愈支与迁延支**都不求值** ⇒ `InjuryState.Recovered` **生产上恒不可达**。

> ⚠️ **这条必须在实现轮读三遍**:接完 D1 **不会**让那 6 格变活。
> 「D1 已接」≠「`Recovered` 6 格已可用」;可达性 = 数值轮裁 `W` / `TREND_EPS`。

### R4 · `GetOutcome` 只给**当前态**,不给任意 tick 的历史态(次序陷阱)

`IDiseaseProjection.TryProject(patient, tick, …)` 的语义写着「取该病人在 `tick` 时刻的投影」,
但现行访问器 `DiseaseVitalsService.GetOutcome(p)` 返回的是**最近一次 `OnTickEdge` 求值后的状态** ——
它**没有** `tick` 形参,**不能回溯**。

两条直接后果:
- **次序约束**:`ConsequenceDriver` 在 `BootRoot` 的 tick 边沿序列里**必须排在
  `VitalsService.OnTickEdge`(`BootRoot.cs:1314`)之后**;排在之前 ⇒ 读到的是**上一 tick** 的 F4 态
  —— 静默偏差一个 tick,不会有任何报错。
- ~~**形参陷阱**:`TryProject` 的 `tick` 只能**校验**(≠ 当前 tick 时 fail-loud),不能回溯。
  实现轮若要真回溯,须另立「F4 历史态」查询面 —— **不得**由适配器自行近似。~~
  (**2026-10-30 实现轮就地订正,原文留档**)—— 该口径**接不上驱动**:`Aggregate` 按
  `close.Tick` 取 D,延迟窗内的历史结案恒早于当前 tick ⇒ 「只允许当前 tick」会让**第二 tick 起
  全部抛**。用户裁定取 **D7**:9 增状态转移历史(`DiseaseVitalsService._outcomeHistory` +
  `TryGetOutcomeAt`),适配器按「≤ 目标 tick 的最后一次转移」作答,**只有未来 tick 抛**。
  「不得由适配器自行近似」这半句仍然成立 —— 回溯的事实来源在 **9 的转移历史**,不在适配器。

**「次序约束」那一条(R4 上半)不受影响,仍然成立。**

**本条不是新裁,是对既有实现的如实登记**;它在 §Implementation Guidelines 与 V-11 各留一处。

### R2 · 空表惰性 = 「门在那儿,什么也没发生」

无 `dataProvider` 时全链绿但不发任何后果。这与「机制未启用」**同形**
⇒ 必须显式告警,否则将来排查「为什么没有后果」时会指向错误方向。

### R3 · 每 tick 全流排序的既有成本被搬上热路径

`ConsequenceInputAggregator.Aggregate` 的实现是「**复制整条流 → 全排序 → 后处理**」
(置换不变性的**结构性**来源,不是低效写法)。接进 tick 边沿后,这段从「测试里跑」变成「20 Hz 跑」
⇒ O(n log n) 随流长线性增长。**本件不优化它**(优化会动 AC-53-04 的结构性保证),
但须:① 实现轮实测流长与耗时;② 若逼近 tick 预算(50 ms),**另开 ADR**,
不得在本件内部改排序结构。

🔁 **2026-10-30 实测(见 V-7)**:现实负载(144 条)**0.198 ms/tick** · 压力口径(≈1008 条)
**1.494 ms/tick** —— **远低于** 50 ms ⇒ **R3 的触发条件未达到**,不另开 ADR。
⚠️ 首版实测曾到 **1135 ms/tick**(2000 条),但归因不是 `Aggregate` 的排序,而是**本件新增的桥**
逐例重排整条流问试药史(`TrialHistoryPredicate.HasTrialHistory(stream, patient)`)——
改成每 tick 扫一次(`PatientsWithTrialHistory`)后降到 1.494 ms(同口径 ≈700×)。
**这条写在这里的理由**:性能问题先怀疑**新加的代码**,不要先怀疑既有结构性设计。

## Performance Implications

- 驱动频率 = tick(20 Hz,`TICK_SECONDS = 0.05`),**不随渲染帧**;
- 单 tick 成本 = 全流排序 + 18 次查表 + (有到期行时)少量 `Append`;
- 定量(**2026-10-30 实跑**,EditMode 夹具 `test_v7_perTickCost_realisticLoad_withinTickBudget`):
  现实负载 24 病人 × (1 结案 + 5 处置)= 144 条 ⇒ **0.198 ms/tick**;
  压力口径 ≈1008 条 ⇒ **1.494 ms/tick**(只记录,非判据)。预算 `TICK_SECONDS = 50 ms`。

## Migration Plan

| # | 步骤 | 归属 | 前置 |
|---|---|---|---|
| **S1** | 9 扩 `DiseaseProjectionSnapshot` + 生产实现 `IDiseaseProjection`(取 `DiseaseVitalsService.TryGetOutcomeAt`)+ **D7 状态转移历史** | 9 / 边界层 | **R1 已核通过**(判据 / 调用点 / 访问器均在) |
| **S1b** | 数值轮裁 `trend_window_ticks` / `trend_eps` —— **只影响 `Recovered` 6 格是否可达**,不阻塞 S1–S5 | 数值轮(用户) | — |
| **S2** | `ConsequenceEmitter.BuildDueRows` 去重键 → **结案身份 `(patient, closeTick, closeSeq)`**(D6 下层;⚠️ 实测「只剔 outcome」不够,见 D6 补注) | 53 | — |
| **S3** | `ConsequenceDriver`(Aggregate → Judge → Emit)+ 构造期 D5 断言 + D6 上层 fail-loud + **D8 桥(`Inputs53 → JudgeInputs`)** | `Gameplay.Boot` | S1 / S2 |
| **S4** | `CompositionRoot` 装配(表装载 / driver 构造)+ **`BootRoot.StepVitals` 边沿驱动(排在 `VitalsService` 之后——R4)**+ 缺席告警 + `P0HostEventAuthority` 占位 | `Gameplay.Boot` | S3 |
| **S5** | 生产口径 `careKinds` 三支(D4)—— 并为三支各补一条夹具 | 53 | — |
| **S6** | 债册归口:`TD-medcons-13` / `-18` / `-19` 改指本件 | 治理 | **S4 之后**才能结 |
| **S7** | 性能实测(R3)并回填 V-7 | 实现轮 | S4 |

**S1 与 S2 可并行;S6 不得抢跑**(债的解除动作是「生产装配面存在」,不是「ADR 写了」)。

## Validation Criteria

🔁 **2026-10-30 实现轮** —— **V-1…V-12 全部已勾**(V-7 的实跑数见该行)。载体全部落在 `unity/Assets/Tests/EditMode/MedConsequences/`(medcons_driver_test ·
medcons_disease_projection_test · medcons_aggregation_test · medcons_delayed_emit_test)。
**定向复算命令**:

```bash
# 期望 242/242(218 基线 + 本件新增 24)
#   Unity -batchmode -runTests -testPlatform EditMode \
#     -testFilter DaYiJingCheng.Tests.MedConsequences
```


| # | 判据 | 载体 |
|---|---|---|
| V-1 ✅ | `DiseaseProjectionSnapshot` 带痊愈位,且 53 侧只经 `IDiseaseProjection` 取值 | `ConsequenceInputAggregator.cs:67` `Recovered` + `Outcome53.RecoveryLaneOf`;`grep -rn "DaYiJingCheng.Sim.DiseaseSimulation" unity/Assets/Sim/Consequences/` ⇒ **零命中** |
| V-2 ✅ | 生产侧 `new ConsequenceEmitter(` **非空** | `grep -rn "new ConsequenceEmitter" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ `CompositionRoot.cs:1508` |
| V-3 ✅ | 生产侧 `Judge.Evaluate` 调用点**非空** | ⇒ `ConsequenceDriver.cs:122` |
| V-4 ✅ | driver 构造期读流≠写流 ⇒ fail-loud | `test_d5_differentWriteFace_throwsAtConstruction`(并断言报文含「同一实例」) |
| V-5 ✅ | 同一 `CaseClosed` 不同 outcome 重喂 ⇒ **不产生两条后果** | 下层 `test_sameCaseClosed_twoOutcomes_emitsExactlyOne_winnerIsEarliest` + 对照 `test_twoDifferentCaseCloses_sameOutcome_emitTwo_control`;上层 `test_d6_sameCaseClosedTwoOutcomes_throws`(**零输入可达**,载体只能直调) |
| V-6 ✅ | `careKinds` 三支各有一条夹具 | `test_productionCareKinds_coversAllThreeKinds_notJustCareApplied`(三条全在)+ 上界 `test_productionCareKinds_rejectsKindsOutsideTheSet` |
| V-7 ✅ | 单 tick 耗时实测:现实负载(144 条)< 50 ms;压力口径只记录 | `test_v7_perTickCost_realisticLoad_withinTickBudget` —— 实跑 **0.198 ms/tick**(现实)· **1.494 ms/tick**(≈1008 条)。首轮 1135 ms 的归因见 §Risks R3 |
| V-8 ✅ | 无 provider ⇒ 全链**不装配**(不发)**且**有显式告警 | `test_noDataProvider_leavesDriverUnassembled`(`bag.ConsequenceDriver == null`)+ `test_bootRoot_warnsWhenDriverUnassembled`(源文本:`ConsequenceDriver == null` + `Debug.LogWarning`) |
| V-9 ✅ | 非主机调用 ⇒ 零写入(既有 AC 不回归) | 既有 `test_required_clientAuthorityGate_nonHostWritesNothing`(MedConsequences 242/242 内含) |
| V-10 ✅ | 空判定表时**不得**静默产出后果 | `test_emptyOutcomeTable_emitsNothing`(空表 + 真延迟表 + 真结案 ⇒ 零后果) |
| V-11 ✅ | **边沿次序**:`ConsequenceDriver` 排在 `VitalsService.OnTickEdge` 之后 | `test_s4_bootRoot_drivesAfterVitals_onSameEdge`(`StepVitals` 方法体内两处 `IndexOf` 定序)+ `test_s4_compositionRoot_constructsDriver` |
| V-12 ✅ | 🔁 **2026-10-30 订正(原文「收到非当前 tick ⇒ fail-loud」)**:`TryProject` 收到**未来** tick ⇒ 抛;收到**历史** tick ⇒ 按 9 的转移历史作答(且快照 `Tick` = 被问的时刻) | 夹具:`t+1` ⇒ 抛;`t−4` ⇒ 有投影且 `Tick = t−4`、与当前态解耦 |

**R1 已核,但「已核」≠「已证」**:`Recovered` 6 格的可达性依赖 S1b(数值轮),
在那之前 **V-1 只能证「接口通」,不能证「6 格活」** —— 不得借绿。

## GDD Requirements Addressed

| GDD | 条款 | 本件如何兑现 |
|---|---|---|
| `medical-consequences.md` | 规则六 / F-53.2(后果在结案后延迟显形) | D2 全链接通后,延迟发出第一次有生产载体 |
| `medical-consequences.md` | 「每病例至多一条后果」 | D6 两层处置 |
| `medical-consequences.md` | `AC-53-04` 置换不变 | 本件**不改** `Aggregate` 的结构性保证(R3 明写) |

---

**关联债**:`TD-medcons-13`(结算器无装配点)→ ✅ **closed** ·
`TD-medcons-18`(三条装配期缺陷,本件兑现 D-1/D-3)→ ✅ **closed**;
**D-2 的处置**(「不得 catch 重试」的类型面表达)= 新增 `ConsequencePartialEmitException`
(继承 `InvalidOperationException`,携带 `EmittedCount` / `FailedEmitTick`),**边界刻意保留**:
零写入时的失败仍是裸异常(那时重试安全)。该处置**由实现轮就地裁定**(本件未预写其形状),
载体 3 条 + 证伪 3/3(M1 去 `written > 0` 过滤 / M2 `EmittedCount` 报 0 / M3 改继承)。
· `TD-medcons-19`(生产装配面未被约束)→ 仍 `open`(其前置已解除,判别力未重做)。
