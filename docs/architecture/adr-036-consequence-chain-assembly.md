# ADR-036: 53 判定链的生产装配(D 路真值 · 判定 · 后果发出)

## Status

✅ **Accepted(2026-10-30)** —— **只裁方向,不实现。** §Validation V-1…V-10 **全部未跑(禁借绿)**。

**三项用户裁定(2026-10-30,逐条照准)**:① **D 路真值 = 9 扩投影形状**;② **范围 = 全链一次接完**;
③ **产出物 = 先写 ADR 再实现**。
⚠️ **§Decision 的 D3–D6 与 §Alternatives Considered 由起草轮就地拟定,未经用户逐条确认**
—— 与 ADR-034 §Status 首段同口径,**实现轮之前可改**。

**本件的由来**:`TD-medcons-13` / `-18` / `-19` 三条 P0 面债的触发条件都写着「装配轮」,
而本轮实测发现它们的真实依赖**不在 53 内部** —— 见 §Context「Current State」。

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

### R1 · 9 的病程状态**能否回答「该 tick 是否已痊愈」—— 本轮未核**

这是 D1 的**实现前置**:若 9 的病程状态只有「当前体征」而无「已痊愈」判据,
则 D1 须退回重裁(不得由实现轮自行发明语义)。**未实测 = 不得借绿。**

### R2 · 空表惰性 = 「门在那儿,什么也没发生」

无 `dataProvider` 时全链绿但不发任何后果。这与「机制未启用」**同形**
⇒ 必须显式告警,否则将来排查「为什么没有后果」时会指向错误方向。

### R3 · 每 tick 全流排序的既有成本被搬上热路径

`ConsequenceInputAggregator.Aggregate` 的实现是「**复制整条流 → 全排序 → 后处理**」
(置换不变性的**结构性**来源,不是低效写法)。接进 tick 边沿后,这段从「测试里跑」变成「20 Hz 跑」
⇒ O(n log n) 随流长线性增长。**本件不优化它**(优化会动 AC-53-04 的结构性保证),
但须:① 实现轮实测流长与耗时;② 若逼近 tick 预算(50 ms),**另开 ADR**,
不得在本件内部改排序结构。

## Performance Implications

- 驱动频率 = tick(20 Hz,`TICK_SECONDS = 0.05`),**不随渲染帧**;
- 单 tick 成本 = 全流排序 + 18 次查表 + (有到期行时)少量 `Append`;
- 定量判据**本轮未测** —— 实现轮须给出「流长 N 条时单 tick 耗时」的实测数,登记进本件 V-7。

## Migration Plan

| # | 步骤 | 归属 | 前置 |
|---|---|---|---|
| **S1** | 9 扩 `DiseaseProjectionSnapshot` + 生产实现 `IDiseaseProjection` | 9 / 边界层 | **R1 实测通过** |
| **S2** | `ConsequenceEmitter.BuildDueRows` 去重键剔除 outcome(D6 下层) | 53 | — |
| **S3** | `ConsequenceDriver`(Aggregate → Judge → Emit)+ 构造期 D5 断言 + D6 上层 fail-loud | `Gameplay.Boot` | S1 / S2 |
| **S4** | `CompositionRoot` 装配(表装载 / driver 构造 / `OnTicks` 注册 / 空表告警) | `Gameplay.Boot` | S3 |
| **S5** | 生产口径 `careKinds` 三支(D4)—— 并为三支各补一条夹具 | 53 | — |
| **S6** | 债册归口:`TD-medcons-13` / `-18` / `-19` 改指本件 | 治理 | **S4 之后**才能结 |
| **S7** | 性能实测(R3)并回填 V-7 | 实现轮 | S4 |

**S1 与 S2 可并行;S6 不得抢跑**(债的解除动作是「生产装配面存在」,不是「ADR 写了」)。

## Validation Criteria

**V-1…V-10 全部未跑(禁借绿)。** 每条注明「判据 + 可复算载体」,实现轮逐条勾。

| # | 判据 | 载体 |
|---|---|---|
| V-1 | `DiseaseProjectionSnapshot` 带痊愈位,且 53 侧只经 `IDiseaseProjection` 取值 | 反射断言 + `grep` 53 侧零 9 类型引用 |
| V-2 | 生产侧 `new ConsequenceEmitter(` **非空** | `grep -rn "new ConsequenceEmitter" unity/Assets \| grep -v /Tests/` |
| V-3 | 生产侧 `Judge.Evaluate` 调用点**非空** | 同上形态 |
| V-4 | driver 构造期读流≠写流 ⇒ fail-loud | 夹具:传入两条不同流 ⇒ 构造抛 |
| V-5 | 同一 `CaseClosed` 不同 outcome 重喂 ⇒ **不产生两条后果** | 夹具:双喂 ⇒ `Emit` 返回 1 条;上层断言抛 |
| V-6 | `careKinds` 三支各有一条夹具 | 三支各一 |
| V-7 | 单 tick 耗时实测(流长 N 条) | 性能夹具 / 日志,数值回填本表 |
| V-8 | 无 provider ⇒ 全链不发**且**有显式告警 | 夹具:空 provider ⇒ `Emitted = 0` + 日志断言 |
| V-9 | 非主机调用 ⇒ 零写入(既有 AC 不回归) | 既有 `test_required_clientAuthorityGate_nonHostWritesNothing` |
| V-10 | 空判定表时**不得**静默产出后果 | 夹具:空表 + 有结案 ⇒ 不发 |

## GDD Requirements Addressed

| GDD | 条款 | 本件如何兑现 |
|---|---|---|
| `medical-consequences.md` | 规则六 / F-53.2(后果在结案后延迟显形) | D2 全链接通后,延迟发出第一次有生产载体 |
| `medical-consequences.md` | 「每病例至多一条后果」 | D6 两层处置 |
| `medical-consequences.md` | `AC-53-04` 置换不变 | 本件**不改** `Aggregate` 的结构性保证(R3 明写) |

---

**关联债**:`TD-medcons-13`(结算器无装配点)· `TD-medcons-18`(三条装配期缺陷,本件兑现 D-1/D-3;
**D-2 未在本件裁定** —— 见 §Implementation Guidelines 的「遇抛即终止本 tick」,
其**类型面表达**仍归实现轮)· `TD-medcons-19`(生产装配面未被约束)。
