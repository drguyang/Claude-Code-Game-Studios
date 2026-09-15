# ADR-001: 联机选型(45 网络层 pipe 抽象与库裁决)

## Status

Accepted

> **2026-09-15 起草。** 三条用户裁定已锁:① **先定抽象,库延后**(45 网络层的 pipe 抽象
> 即本 ADR 的承重产物,底层库 NGO/Fusion 推迟到 P1b 前做一次 swap 评审再定);
> ② **pipe 契约可靠即可,保序非必需**(重排由流合并排序按全序键吸收 —— 架构复核 §12.1 收窄);
> ③ **45 网络层 = P0 预埋 · P1b 实现**(现 P1b 状态不变,pipe 抽象已在 P0 生效,
> 实现留到 P1b)。ADR-005 / 007 / 008 / 009 / 010 已把约束升为「承载三条逻辑流 + 表现态位置同步」。
> 引擎侧经 unity-specialist lean 复核(2026-09-15):**无引擎侧 blocker**;结论并入 §Risks
> (F2 HOL 阻塞 + 第二 QoS 通道 · F3 线程模型 · F4 接口纪律 · F5 IL2CPP 冒烟)。
> 独立评审由下一轮 `/architecture-review` 进行。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 三条裁定,均照准**)· technical-director(起草与裁决)
· network-programmer(45 网络层 · pipe 抽象)· unity-specialist(引擎复核,2026-09-15)
· 系统 45 网络层与同步(现 P1b · 未开始)

## Summary

ADR-005 / 007 / 008 / 009 / 010 **五份 Accepted ADR 已把联机约束层层收紧**,
**ADR-001 却还不存在** —— 架构复核 C-10 记为承重缺口,`TR-concept-002` 是唯一 blocking TR。
本 ADR 裁决:**45 网络层 = 一条「自定义可靠有序消息流」之上的 pipe 抽象**(可靠即保序非必需 +
有界重排缓冲 · 传输层连保序都不必需),**底层库(NGO / Fusion)推迟到 P1b 前一次 swap 评审再定**。
选型差异被上游 ADR 收窄到**工程 / 成本维**,而非协议能力维 —— 库选型不再承重。
系统 45 保持 **P0 预埋 · P1b 实现**;pipe 抽象 P0 生效(Authority-agnostic 预埋,ADR-005 C6)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(网络 / 同步 / 消息) |
| **Knowledge Risk** | **HIGH** —— NGO 与 Fusion 的 6.3 具体行为均属 post-cutoff 知识,须实测;本裁决刻意**不在库 API 层做承诺**(pipe 抽象隔离),重排缓冲 / 流合并排序为纯 C# 逻辑,不受引擎版本影响 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `docs/architecture/adr-005-deterministic-sim.md` · `adr-007-event-authority-and-roll-state.md` · `adr-008-case-event-stream.md` · `adr-009-world-state-event-boundary.md` · `adr-010-persistence-save-format.md` · `architecture-review-2026-09-15.md`(§12.1 结论一 · C-10 · R-3) |
| **Post-Cutoff APIs Used** | **None** —— 本 ADR 不承诺任何具体库 API;pipe 抽象 = 纯 C# 接口 |
| **Verification Required** | ① 有界重排缓冲单测(乱序注入 → 全序键吸收 → 无丢、无重放);② 流合并排序单测(三流全序键无平局,沿用 ADR-008/009);③ 丢包 + HOL 场景的 QoS 联调实测(第二 QoS 通道,引擎复核 F2);④ IL2CPP build 冒烟(`in` 参数 + 值类型键排序,引擎复核 F5);⑤ P1b 库 swap 评审的验收标准(见 §四) |

> **Note**: Knowledge Risk HIGH —— NGO / Fusion 库面行为升级引擎时须重读;pipe 抽象本身不受影响。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 主机唯一 Step / CatchUp · 客户端持流副本 · 五个抽象点)· **ADR-007**(Accepted —— `IEventAuthority` 第六抽象点 · 掷骰权天然唯一)· **ADR-008**(Accepted —— 病例流 · 两流传输遵循全序键)· **ADR-009**(Accepted —— 世界流 · 表现态位置同步 · 拾取意图)· **ADR-010**(Accepted —— 三流序列化 · 存档非网络通道 · authority-agnostic)—— 五者均须 Accepted |
| **Enables** | **系统 45 网络层的实现**(P1b)· P0 的「架构预留」预埋(pipe 抽象生效)· `TR-concept-002`(partial → covered)|
| **Blocks** | **45 网络层实现**;凡依赖 45 的联机 AC(P1b);`TR-randomevents-026`(联机语义)|
| **Ordering Note** | 本 ADR 是 **R-3 的落点**(R-3 = 联机选型)。**P0 只需 pipe 抽象在接口层生效,不阻塞任何 P0 实现**。**不阻塞** 21a / 52 / 9 / 37 的既有实现。库 swap 评审是 P1b 前的一次性动作 |

## Context

### Problem Statement

初稿 GDD 同时列了两套互斥联机方案(NGO vs Fusion),`TR-concept-002` 因此 blocking。
但五份 ADR 已把裁决空间层层收窄到只剩「管道语义」:

1. **ADR-005**:主机唯一执行 `Step` / `CatchUp`,客户端只持「流副本 + 快照」,本地求值 `Progress`
   不算跑模拟,绝不写回事件流 —— **状态同步机制被裁掉了**,只剩事件流传输;
2. **ADR-007**:`IEventAuthority` 掷骰权天然唯一(只有主机),迁移后一切可重放 ——
   掷骰 / 权威迁移也被裁掉了;
3. **ADR-008 / 009**:三流传输与合并排序遵循 `(Tick, StreamPriority, Patient, Seq)` 全序键 ——
   **保序本身也被裁掉了**(全序键吸收重排);
4. **ADR-010**:存档 authority-agnostic,存档非网络通道。

**架构复核 §12.1 结论一进一步收窄**:「架构把 netcode 降级为**可靠消息管道** …… 传输层
**连保序都不必需**(可靠 + 有界重排缓冲即可)。这大幅缩小了 ADR-001 的裁决空间。」
**而不裁决的代价**:P0 的「架构预留」无处落点 —— 6 个抽象点接口没定,`TR-concept-002` 悬空,
P1b 重构是三个月级灾难(systems-index C6)。

### Current State

- `systems-index.md`:45 网络层 | Foundation | **P1b** | 未开始 | 依赖 = ADR-001。
- `technical-preferences.md`:ADR-001 待建(NGO vs Fusion 二选一);`Allowed Libraries` 为空。
- `tr-registry.yaml`:`TR-concept-002`(联机 1–4 人,P0 起架构预留)· Networking · **partial · blocking**,
  note「ADR-001 缺失(报告 C-10)」。**45 网络层本身无 TR 行,`TR-*45*` 不存在。**
- 五份 ADR 已断言「NGO 与 Fusion 均满足约束」—— 裁决空间已被收窄到管道语义。

### Constraints

- 不得破坏 ADR-005:主机唯一 Step / CatchUp;客户端只持流副本;事件流唯一真源。
- 不得破坏 ADR-007:`IEventAuthority` 掷骰权天然唯一;迁移后一切可重放。
- 不得破坏 ADR-008 / 009:三流全序键 `(Tick, StreamPriority, Patient, Seq)`;表现态位置走 45。
- 不得破坏 ADR-010:存档非网络通道;三流序列化 authority-agnostic。
- **P0 不承诺任何具体库 API**(Knowledge Risk HIGH);pipe 抽象隔离库面。
- 后台 / 传输线程不得调 Unity API(与 ADR-010 §六 同纪律)。

### Requirements

- 45 网络层必须承载**三条逻辑流**(病史 / 病例 / 世界)+ **表现态位置同步**(ADR-009)。
- pipe 契约 = **可靠即可,保序非必需**(有界重排缓冲吸收乱序,全序键排序)。
- 必须隔离底层库面(P0 实现零依赖,库 swap 评审一次完成)。
- 必须 authority-agnostic(与 ADR-010 存档同构);迁移后一切可重放。
- 必须满足 `TR-concept-002`(partial → covered)。

## Decision

**裁决:45 网络层 = 一条「自定义可靠有序消息流」之上的 pipe 抽象;
底层库(NGO / Fusion)推迟到 P1b 前一次 swap 评审再定。系统 45 = P0 预埋 · P1b 实现。**

### 一、pipe 抽象(本 ADR 的承重产物)

```csharp
// ── 网络层管道 ──
interface IReplayPipe {
    void Publish(in SimEvent e);            // 主机:发出(事件已按三流路由落流)
    void Subscribe(Action<SimEvent> onEvent); // 客户端:接收(重排缓冲吸收后回调)
}

// ── 有界重排缓冲 ──
sealed class ReorderBuffer {
    int MaxLag;                              // 有界:乱序容忍深度(旋钮)
    bool TryEnqueue(in SimEvent e);          // 按全序键插入;超界 → 丢弃 + 告警
    IEnumerable<SimEvent> Drain();           // 输出已就绪前缀(全序)
}
```

- **发布 / 订阅语义**:主机 `Publish`,客户端 `Subscribe`;事件已由 `IEventSink` 按 Kind 路由落流,
  网络层**不再做语义判断**(纯管道)。
- **重排缓冲 = 按全序键的 `ReorderBuffer`**:乱序注入 → 全序键吸收 → 无丢、无重放。
  缓冲是**有界**的(`MaxLag` 旋钮),超界丢包 + 告警(客户端重连 = 拉流重放,ADR-010 三流是重放源)。
- **可靠 + 传输顺序不依赖**(引擎复核 F2 修正措辞):传输层只保证每条消息可靠到达;
  顺序由流合并排序吸收。**注意**:两库的可靠通道(UTP ReliableSequenced / Fusion reliable)
  在单连接内**实际保序** ——「保序非必需」是**安全的下界契约**,不是两库的真实行为;
  契约措辞以「传输顺序不依赖」为准(实现不依赖传输顺序,但传输实际有序是好的)。
- **HOL 阻塞提醒(引擎复核 F2)**:可靠通道遇大事件(Craft 载荷 / 模式识别)重传会**头阻塞**
  其后所有消息 ⇒ 表现态位置若走同一条可靠 pipe,急救 <50 ms 预算有风险。
- **第二 QoS 通道(unreliable latest-value)**:表现态位置同步走**独立不可靠最新值通道**
  (两库均支持),与三流可靠 pipe **分离** —— 避免大事件重传阻塞位置更新(ADR-009 表现态走 45)。
- **线程模型(引擎复核 F3)**:`Publish` 在主机 Step 边界(主线程);传输线程**零 Unity API**
  (ADR-010 §六 同纪律);`Subscribe` 回调排队到主线程 PlayerLoop 注入
  (IL2CPP 下 `SynchronizationContext` 不可用时);`Drain` 在主线程消费。
- **P0 预埋**:接口与 `ReorderBuffer` 是纯 C# 逻辑,P0 全实现为**本地占位**(同 ADR-005 抽象点)。
  P1b 时把 `IReplayPipe` 接到 NGO / Fusion 的可靠消息通道。
### 二、库裁决:延后(先定抽象)

- **裁决**:P0 **不选** NGO / Fusion;pipe 抽象是唯一的网络契约。
  库 swap 评审(**§四**)在 P1b 前做一次,用当时的最新版本 + 实测数据定。
- **为什么延后**:
  1. 五份 ADR 已把裁决空间收窄到管道语义,库的**协议能力差异不再承重**;
  2. 差异落在工程 / 成本维(NGO 免费开源 vs Fusion 商用 CCU 计价)—— 这些**在 P0 决策会过期**,
     在 P1b 决策才有时效性;
  3. pipe 抽象把库面**隔离在接口之后**,swap 是一次性动作,不是迁移灾难。

### 三、45 网络层归属

- **系统 45 保持 P1b**(systems-index 不变);pipe 抽象 P0 生效(Authority-agnostic 预埋)。
- **P1b 实现时**,`IReplayPipe` 接底层库;客户端持流副本 + 快照(ADR-005),表现态位置走 45(ADR-009)。

### Architecture Diagram

```
     主机(权威)
      │  IEventSink.Append(按 Kind 路由)
      ▼
   三流(病史 / 病例 / 世界)── ADR-010 存档 ──▶ 7a(非网络通道)
      │
      ▼
   IReplayPipe.Publish(in SimEvent)      ← 45 网络层(pipe 抽象)
      │  可靠消息通道(库 = P1b swap 评审)
      ▼
   ReorderBuffer(有界 · 全序键吸收重排)
      │
      ▼
   客户端:Subscribe(Action<SimEvent>) → 流副本 + 快照(ADR-005)
          本地求值 Progress(不算跑模拟)· 绝不写回事件流
```

### Key Interfaces

```csharp
// ── 第六抽象点(ADR-007 §一,承重)──
public interface IEventAuthority   // 掷骰权(天然唯一)
{
    RollResult Roll(in RollRequest req);   // 只有权威侧可调用
}

// ── 本 ADR 新增(pipe 抽象)—— 与 ADR-005 五抽象点 + IEventAuthority 并立
interface IReplayPipe
{
    void Publish(in SimEvent e);                 // 主机:发出(三流可靠 pipe)
    void PublishPositional(in WorldPosLatest p); // 主机:表现态位置(独立 QoS,unreliable latest-value)
    IDisposable Subscribe(Action<SimEvent> onEvent); // 客户端:接收;Dispose = 退订(防重订阅泄漏,引擎复核 F4)
}

sealed class ReorderBuffer
{
    int MaxLag;                                 // 有界:乱序容忍深度(旋钮)
    bool TryEnqueue(in SimEvent e);             // 按全序键插入;超界丢弃 + 告警
    // Drain():P1b 热路径改 NonAlloc,避免每次 yield 分配(引擎复核 F4)
}
```

> **接口纪律(引擎复核 F4/F5)**:`SimEvent` 为 `readonly struct`(`in` 参数避免每次调用点防御复制);
> `Drain()` 在 P1b 热路径改 NonAlloc;`Subscribe` 返回 `IDisposable` 支持退订。
> IL2CPP 编译期冒烟覆盖 `in` 参数 + 值类型键排序(引擎复核 F5)。

### Implementation Guidelines

1. **先写 `ReorderBuffer` 与流合并排序单测**:全序键吸收重排是 pipe 的核心正确性。
2. **pipe 抽象零引擎依赖**:P0 全实现为本地占位,不 import 任何 NGO / Fusion。
3. **客户端持流副本 + 快照**:本地求值 `Progress` 不算跑模拟;绝不写回事件流(ADR-005)。
4. **表现态位置走 45**:掉落物理轨迹 / 落点 / 地形表现 = 表现态,不进模拟域(ADR-009)。
5. **拾取意图事件走 45**:`PickupIntent` → 主机判距 → `DropClaimed` 进世界流(ADR-009 §七)。
6. **表现态位置走第二 QoS 通道**(unreliable latest-value),与三流可靠 pipe 分离(引擎复核 F2)。
7. **P1b 库 swap 评审一次完成**(§四),pipe 契约不变。

### 四、P1b 库 swap 评审验收标准(引擎复核 F1 补)

swap 评审在 P1b 前强制完成,用当时最新版本 + 实测数据定库。验收标准:

1. **可靠消息实测**(两库各一):主机 `Publish` → 客户端 `Subscribe` 全量到达;断线重连后
   三流重放一致(ADR-010 三流为重放源)。
2. **乱序注入联调**:`ReorderBuffer` 吸收乱序,三流全序键排序无平局。
3. **丢包模拟**:表现态位置走第二 QoS 通道,急救操作 <50 ms 延迟满足(手感预算)。
4. **HOL 阻塞场景**:大事件(Craft 载荷 / 模式识别)重传时,表现态位置**不被阻塞**(两库对比)。
5. **IL2CPP build 冒烟**:发布构建可连、可传;`in` 参数 + 值类型键排序通过(引擎复核 F5)。
6. **Steam P2P / 中继实测**:NGO 需第三方 Steam transport,Fusion 内置 —— **真实差异项**,
   须实测后定。
7. **CCU 成本**:1-4 人合作以 Steam P2P 免费路径为主;若选 Fusion,CCU 计价记入 P1b 成本表。

## Alternatives Considered

### Alternative 1: P0 选 NGO(免费开源官方)

- **Pros**:零成本;官方支持;开源
- **Cons**:其状态同步能力(NetworkBehaviour / RPC)恰是本架构刻意不用的 ——
  NGO 的很多复杂度是**我们不需要的复杂度**;6.3 具体行为 post-cutoff,须 spike
- **Rejection Reason**:不承重 —— 协议能力差异已被上游 ADR 收窄;选型无 P0 价值

### Alternative 2: P0 选 Fusion(商用 CCU 计价)

- **Pros**:自带 tick / 房间 / 中继 / 迁移,工程完整
- **Cons**:**恰是本架构刻意不用的能力**(tick = 确定性 tick 由 `ITickProvider` 提供,
  房间 / 中继 = Steam P2P 够用,迁移 = 三流重放);CCU 计价持续成本;P0 决策会过期
- **Rejection Reason**:同 Alternative 1 —— 选型差异是工程 / 成本维,不是能力维

### Alternative 3: P0 把 NGO / Fusion 都做 spike 后选一个

- **Pros**:决策有实测依据
- **Cons**:spike 成本高(两库都写 Demo);结论在 P1b 大概率过期;pipe 抽象让 spike 的价值归零
- **Rejection Reason**:pipe 抽象已隔离库面,swap 评审一次即可,spike 是浪费

### Alternative 4: 传输层保序(pipe 契约要求按流有序送达)

- **Description**:pipe 契约要求每条消息按逻辑流有序送达
- **Pros**:语义更直白;Replayer 更简单
- **Cons**:与架构复核 §12.1 收窄相反(传输层保序 = 连接层做重排序,自造复杂度);
  底层库的保序能力依赖具体库,pipe 抽象反而被库面污染
- **Rejection Reason**:全序键已在流合并层吸收重排,传输层保序是**多余约束**(用户裁定②)

## Consequences

### Positive

- **`TR-concept-002` 从 blocking 解除**(partial → covered,§GDD Requirements Addressed)
- **P0 的「架构预留」落点**:pipe 抽象 P0 生效,接口层无需再等库选型
- **库选型不再承重**:NGO / Fusion 的差异被收窄到工程 / 成本维,swap 评审一次完成
- **不引入 P0 依赖**:pipe 抽象零引擎依赖,`Allowed Libraries` 保持空(P0)
- **45 保持 P1b**:不改变 systems-index 的分层与优先级

### Negative

- **P0 无法联机实测**:pipe 抽象是接口契约,联机正确性验证推到 P1b
- **swap 评审本身是一次成本**:P1b 前须做一次 NGO / Fusion 实测评审(§八 验收标准)
- **45 网络层无 TR 行**:系统 45 的 TR 条目需在后续 `/architecture-review` 补登记

### Neutral

- 客户端「流副本 + 快照」的本地求值(ADR-005)继续成立
- 表现态位置同步(ADR-009)走 45,与存档 / 模拟域分离

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **P1b swap 评审发现库不满足 pipe 契约**(可靠消息能力缺失) | 低 | 高 | §四 验收标准前置到 R-3 落点;pipe 契约只要求可靠消息,两库均满足(ADR-005 断言) |
| **HOL 阻塞致表现态延迟超预算**(可靠通道大事件重传阻塞其后消息) | 中 | 中 | 第二 QoS 通道(unreliable latest-value)承载表现态位置,与三流可靠 pipe 分离(引擎复核 F2) |
| **`ReorderBuffer` 有界性失控**(MaxLag 过小 ⇒ 客户端丢事件) | 中 | 中 | 有界 + 超界丢弃 + 告警;客户端重连 = 拉流重放(ADR-010 三流是重放源) |
| **传输线程调 Unity API**(网络层错误耦合主线程) | 中 | 高 | 与 ADR-010 §六 同纪律:传输线程零 Unity API;回调排队到主线程 PlayerLoop 注入 |
| **pipe 抽象被绕过**(实现者直接接 NGO / Fusion API) | 中 | 高 | 代码评审 + P0 抽象点测试(EditMode 断言 P0 实现为零依赖) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU | — | pipe = 纯管道 O(1);`ReorderBuffer` 按全序键插入 O(log n) | 16.6 ms 平面 / 11.1 ms VR(传输线程零主线程开销) |
| Memory | — | 有界重排缓冲(MaxLag 旋钮) | 待定 |
| Network | — | 三流传输;存档非网络通道(ADR-010) | 待定 |
| Latency | — | 表现态位置走 45(拾取意图 → 主机判距 → DropClaimed) | 急救操作 < 50 ms(手感预算) |

## Migration Plan

**本项目尚无 45 的实现,故无迁移 —— 本 ADR 是「第一次就做对」。(同 ADR-005 / ADR-010)**

1. **P0**:写 `IReplayPipe` + `ReorderBuffer`(纯 C# 逻辑,零依赖)。
2. **P0**:六个抽象点(P0 全实现为本地占位)+ 三流全序键单测。
3. **P1b 前**:库 swap 评审(§八 验收标准),选 NGO 或 Fusion。
4. **P1b**:把 `IReplayPipe` 接到所选库的可靠消息通道;客户端流副本 + 快照落地。

**Rollback plan**:若最终不做联机,pipe 抽象仍是六个抽象点的自然延伸(P0 本地占位不变),
**无多余成本**。若做联机,pipe 抽象 + 三流全序键已进接口层,**不可回退**(改接口 = 重写六抽象点)。
库选型本身可回退(P1b swap 评审可改选)。

## Validation Criteria

- [ ] `ReorderBuffer` 单测:乱序注入 → 全序键吸收 → 无丢、无重放;超界丢弃 + 告警
- [ ] 三流合并排序单测:全序键 `(Tick, StreamPriority, Patient, Seq)` 无平局(沿用 ADR-008/009)
- [ ] P0 抽象点测试:pipe 实现零 NGO / Fusion 依赖(EditMode 断言 import 检查)
- [ ] **丢包 + HOL 模拟**:表现态位置走第二 QoS 通道,大事件重传不阻塞位置更新,急救 <50 ms(引擎复核 F2)
- [ ] **IL2CPP build 冒烟**:发布构建可连可传;`in` 参数 + 值类型键排序通过(引擎复核 F5)
- [ ] `TR-concept-002`(联机 1–4 人,P0 起架构预留)覆盖 —— registry 状态更新为 covered
- [ ] P1b 库 swap 评审验收(§四):七项标准实测通过后定库

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/systems-index.md` | 45 网络层与同步 | 系统 45(依赖 = ADR-001) | 本 ADR 即其权威件;P1b 实现 |
| `design/gdd/game-concept.md` | 联机定位 | 1-4 人联机合作,P0 架构预留 | pipe 抽象 P0 生效,库选型 P1b |
| `docs/registry/architecture.yaml` | — | `TR-concept-002`(联机 1–4 人) | 本 ADR 覆盖 → covered |

## Related

- **ADR-005 确定性模拟与状态同步模型**(Accepted)—— 主机唯一 Step / CatchUp · 客户端流副本 ·
  五个抽象点。本 ADR 的 pipe 抽象是这些的传输落点
- **ADR-006 定点域边界数据契约**(Accepted)—— 事件流禁 float;pipe 传输沿用(传输层零 float)
- **ADR-007 事件权威与掷骰状态**(Accepted)—— `IEventAuthority` 第六抽象点;掷骰权天然唯一
- **ADR-008 病例事件流**(Accepted)—— 三流全序键;两流传输遵循 `(Tick, StreamPriority, Patient, Seq)`
- **ADR-009 世界状态的事件化边界**(Accepted)—— 世界流 · 表现态位置走 45 · 拾取意图
- **ADR-010 持久化与存档格式**(Accepted)—— 三流序列化;存档非网络通道;authority-agnostic
- **R-3 联机选型**(architecture-review 2026-09-15)—— 本 ADR 是其落点
- **架构复核 §12.1 结论一**(2026-09-15)—— netcode 降级为可靠消息管道;保序非必需
- `design/registry/entities.yaml` —— 待登记 pipe 常量(MaxLag 默认等)
