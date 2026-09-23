# ADR-027: 13 病人 AI 的写路径归属 (Patient-AI Write-Path Ownership)

## Status

Accepted

> **2026-09-23 起草并转 Accepted。** 用户裁定(2026-09-23,照准):**兑现
> `architecture.md` §Required ADRs #5** —— 「13 病人 AI 的写路径归属(只读消费者的副作用归谁)」。
> **裁决 = 两条写路径皆归 10 急救动作** —— 二者都是「玩家对病人的物理干预」,与 10 已有的
> CPR / 止血同构。**13 只出表现,8 只声明存在**,二者都不写。
> 本 ADR 结清 `TR-patient-021` 与 `TR-patient-022` 两条 `gap`。

## Date

2026-09-23

## Last Verified

2026-09-23

## Decision Makers

dr_guyang(用户 · **2026-09-23 裁定写路径 = 10 急救动作**)· technical-director(起草与裁决)
· 13 病人 AI(只读消费者 · 表现映射方)· 10 急救动作模块(写路径所有者)· 8 诊断与体征揭示
(查体动作所有者 · `D-8-12` 动作词表 master)· 4 交互系统(目标选择 / 意图路由)· 9 疾病与伤情模拟
(痉挛的病程内容归属)· 45 网络层(客户端意图上行通道)

## Summary

系统 **13 病人 AI** 是 `9` 的**只读消费者** —— 它读 `IVitalsQuery → VitalsDto`(唯一浮点出口)
与 `IPresentPatients`,住**边界层**,**物理上写不了 sim**(门 A 之外)。但两个玩家动作
—— **查体诱发痉挛**(`TR-patient-021`)与**搬运昏迷病人**(`TR-patient-022`)——
**在事件流里没有写者**:`diagnosis-system.md:1253` 把前者记归「10 / 13」,而 13 写不了;
后者 13 只读、20 只管玩家自己的位移,**二者都不拥有**。本 ADR 裁决:**两条写路径皆归 10 急救动作**
(与 CPR / 止血同构的「玩家对病人的物理干预」);形态**承 `ADR-009 §七` 三段式 + `ADR-020 §四` 对称落实**;
**13 只出表现,8 只声明存在**;新增 Kind 的义务归 10 的 GDD 轮(承 ADR-024 registry 纪律)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(所有权裁决 · 事件边界) |
| **Knowledge Risk** | **LOW** —— 纯所有权裁决;表现层动画 / 物理不进 sim |
| **References Consulted** | `docs/architecture/adr-009-world-state-event-boundary.md`(§七 三段式)· `adr-016-ai-architecture.md`(§一 三源不变量 · §六 只读通道)· `adr-020-player-controller-and-camera.md`(§四 对称落实)· `adr-024-kind-single-source.md`(registry 真源)· `design/gdd/patient-ai.md`(`OQ-13-1` / `OQ-13-3`)· `design/gdd/diagnosis-system.md`(:1250-1254 破伤风事实注 · G-1)· `design/gdd/emergency-procedures.md`(规则三 · 规则十一 · ADR-009 Amendment I) |
| **Post-Cutoff APIs Used** | **None** —— 纯数据边界与所有权裁决 |
| **Verification Required** | 13 的代码路径**零 `IEventSink.Append` 调用**(结构不可达判据:构造注入白名单不含 `IEventSink`)—— 承 `AC-4-02` 同款 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-009**(Accepted —— §七 拾取三段式 · §一 Q1 判据 · 三态分类)· **ADR-016**(Accepted —— §一 三源不变量 · §六 13 只读通道 + 表现映射归属)· **ADR-020**(Accepted —— §四 玩家位移 = 表现态,跨格事件是唯一 sim 投影)· **ADR-024**(Accepted —— 新 Kind 须先改 registry 再重跑 kindgen) |
| **Enables** | **13 的 GDD 定稿**(`OQ-13-1` / `OQ-13-3` 结案)· **10 的下一轮 GDD**(须追加写路径事件)· **8 的措辞口径**(`diagnosis-system.md:1253` 的「10 / 13」收为「10」) |
| **Blocks** | **`TR-patient-021` / `TR-patient-022` 的实现故事** —— 在写路径未定前不得开工;**`OQ-13-3` 仍 P0 不实现**(本 ADR 只登记方向,不排 P0) |
| **Ordering Note** | 本 ADR **不阻塞**其他系统;它只补 13 的写路径面。**022 的 Kind / 流别归 10 的 GDD 轮**(搬运进 P0 时);**021 的 Kind 名 / 载荷亦归 10 的 GDD 轮**(承 ADR-024 的 registry 纪律:先改 `entities.yaml`,再重跑 kindgen) |

## Context

### Problem Statement

`architecture.md` §Required ADRs #5 记:`TR-patient-021`(**「查体诱发痉挛」的写路径归属**)
与 `TR-patient-022`(**搬运昏迷病人的写路径归属**)两条 `gap`,**根因同一条** ——
**13 是只读消费者(读 `VitalsDto` / `IPresentPatients`),物理上写不了 sim**
(它住边界层,`architecture.md` §Module Ownership 行 13 = **L4 边界层**)。⇒ 两个玩家动作
**在事件流里没有写者**。若不裁决:实现期 13 会各自决定「痉挛 / 搬运往哪写」,
或把效果悄悄塞进表现层(⇒ 联机 / 迁移后不可重建,违 `ADR-005 §一`)。

### Current State

- `diagnosis-system.md:1250-1254`(破伤风事实注):「若日后要落,**「查体触发了什么」的写路径
  归 10 急救动作 / 13 病人 AI**……**8 只声明它存在**,不实现、不写回病史事件流」。
- `patient-ai.md:395-400`(`OQ-13-1`):13 **是只读的**(规则一),**写不了 sim** ——
  **口径冲突**;**该特性若落地,写路径只能归 10 / 9**,13 只提供表现(姿态骤变 + 呻吟)。
- `patient-ai.md:801-806`(`OQ-13-3`):搬运 = 病人位置是**行为决策的产物(派生态)**,
  **不在事件流里**;13 只读、20 只管玩家自己 —— **二者都不拥有它**;**P0 不实现搬运**。
- `architecture.md` §Required #5 候选表:归 10(与 CPR / 止血同构)/ 归 9(痉挛是病程产物)/
  归 4(与拾取同构)。

### Constraints

- **13 只读不写**(`patient-ai.md` 规则一 · ADR-016 §六):13 不产生三流**任何**事件。
  **「只读」禁的是回写三流,不禁表现层输出** —— 13 拥有表现映射(姿态 / 动画 / 呻吟)。
- **8 只读不写**(`diagnosis-system.md` 规则一):8 的读数 / 病名 / 置信度**不回写病史事件流**。
- **三源不变量**(ADR-016 §一):AI 决策可重建的充要条件是其输入 ∈ {事件流, 版本化烘焙数据, 纯函数}。
  **效果进流,决策不进流。**
- **主机唯一 `Append`**(ADR-005):客户端意图经 ADR-001 第二 QoS 上行,主机判距后物化。
- **新 Kind 须走 registry**(ADR-024):真源 = `entities.yaml`;**先改 registry 再重跑 kindgen**。
- **玩家位移的 sim 投影 = 跨格世界流事件**(ADR-020 §四):连续位置永不写流。

### Requirements

- 为「查体诱发痉挛」的**写路径**指定**单一所有者**。
- 为「搬运昏迷病人」的**写路径**指定**单一所有者**(即使 P0 不实现)。
- 保持 13 / 8 的**只读不写**约束不被打破(13 只出表现)。
- 形态**复用既有三段式**(意图 + 主机判距 + 效果进流),不新立机制。
- 新 Kind / 载荷的**登记落点**归 10 的 GDD 轮 + `entities.yaml`(ADR-024)。

## Decision

### ① 两条写路径皆归 10 急救动作

**裁决:「查体诱发痉挛」与「搬运昏迷病人」的写路径所有者 = 10 急救动作模块。**

判据:`ADR-009 §一 Q1`(真源 / 权威 / 可感知)与本仓的**写者判据** ——
二者都是**玩家对病人的物理干预**,与 10 已有的 **CPR / 止血**同构(玩家主动动作 → 主机判定 → 效果进流),
而非 9 的**病程自发**(那类 9 自写)、也非 4 的**目标选择**(4 只选目标不结算)。

| 项 | 写路径所有者 | 13 的角色 | 8 的角色 |
|---|---|---|---|
| **TR-patient-021** 查体诱发痉挛 | **10**(写病史流效果事件) | 只出表现(姿态骤变 + 呻吟) | 只声明其存在,不写 |
| **TR-patient-022** 搬运昏迷病人 | **10**(写位置事件,**P0 不实现**) | 只出表现(被搬运动画) | 无 |

### ② 形态 = ADR-009 §七 三段式(意图 + 主机判距 + 效果进流)

**不新立机制** —— 两条写路径**复用既有三段式**(承 `ADR-009 §七` 拾取 + `ADR-020 §四` 对称落实):

```
玩家物理干预(查体 / 搬运)
  → 客户端聚合为意图事件上行(ADR-001 第二 QoS,不逐帧)
    → 主机在 Append tick 上判距(病人格 / 状态;当下权威值)
      → 效果事件进流(主机唯一 Append)
```

- **判定结果进流,判定过程不进流**(`ADR-009 §七`)。
- **客户端不上报结果**(承 ADR-011 §Amendment B 的 F1 改判):伪造成本 = 改一个枚举值 ⇒ 收益必须由主机判。
- **意图事件走第二 QoS**(`unreliable latest-value`)—— ⚠️ 其**丢包**问题与 `EmergencyAttempt`
  同源(归 `OQ-10-9`,须 ADR-001 窄修订);本 ADR **不在此打补丁**。

### ③ 13 只出表现;8 只声明存在

- **13**:痉挛 / 搬运的**表现**(姿态骤变 / 呻吟 / 被搬运动画 / 动画状态机)归 13
  —— 承 `ADR-016 §六`「13 拥有表现映射」。**13 零 `IEventSink.Append` 调用**(结构不可达判据)。
- **8**:`diagnosis-system.md:1250-1254` 的破伤风事实注**只声明存在**,不实现、不写回病史事件流
  —— **8 零 `Append`**。其「10 / 13」措辞**收为「10」**(13 写不了)。
- **9**:痉挛的**病程内容归属**(它是什么、改变什么状态)仍归 9 —— 与 `InjuryOnset`(25 写、9 内容归属)、
  `EmergencyTreatmentApplied`(10 写、9 消费)同款 **「内容归属方 ≠ `Append` 调用者」** 分工
  (`disease-simulation.md:180`)。

### ④ 新 Kind 义务归 10 的 GDD 轮(承 ADR-024)

- **TR-patient-021** 需一条**病史流 Kind**(痉挛 = 病人状态变化)——
  **Kind 名 / 载荷 / 语义归 10 的 GDD 轮定稿**,经 `entities.yaml` 登记 + kindgen 重跑(ADR-024)。
  本 ADR **只定归属与形态,不定载荷**(承 ADR-009 §三「骨架先行,载荷归系统 ADR」纪律)。
- **TR-patient-022** 的位置事件(流别 / Kind)**归 10 的 GDD 轮**(搬运进 P0 时);
  方向承 `ADR-020 §四`(跨格事件 = 位置的唯一 sim 投影)。
- ⚠️ **不得**以本 ADR 直接改 `entities.yaml` —— 先改 registry 再重跑 kindgen(ADR-024 纪律)。

### Key Interfaces

```
// 10 的写路径(主机侧)—— 形态承 ADR-009 §七
// 意图事件:客户端聚合上行(第二 QoS)
// 主机判距:病人格(IPresentPatients 的 WorldPos)+ 状态(痉挛敏感 / 昏迷)
// 效果事件:IEventSink.Append(主机唯一)—— Kind 归 10 的 GDD 轮
```

### Implementation Guidelines

1. **13 / 8 的构造注入白名单不含 `IEventSink`** —— `Append` 对其程序集**不可见**,违规 = 构建失败
   (承 `AC-4-02` 同款结构不可达判据)。
2. **判距吃整数格,不吃表现态位置**(承 ADR-016 §三 / ADR-020 §四)—— 联机时非主机的目标选择不得与主机不同。
3. **021 的实现前置**:10 的 GDD 轮先定 Kind + 载荷 + registry 登记,再写代码。
4. **022 保持 P0 不实现**(承 `OQ-13-3`)—— 本 ADR 只登记方向,不排期。

## Alternatives Considered

### Alternative 1: 归 9 疾病模拟(痉挛 = 病程产物)

- **Description**:9 自写痉挛事件 —— 痉挛是破伤风病程的产物,不是玩家的动作。
- **Pros**:语义贴切(痉挛确由病程驱动)。
- **Cons**:痉挛的**触发依赖玩家的查体动作**(外部输入)—— 归 9 会逼 9 **感知玩家输入**,
  破 9 的入向边界(9 的入向只有病程 / 处置 / 环境);且搬运是**纯玩家动作**,更不可能是病程产物。
- **Estimated Effort**:中。
- **Rejection Reason**:破 9 的入向边界;且无法统一覆盖 022。**痉挛的「内容归属」仍归 9**
  (③),只是 `Append` 调用者是 10。

### Alternative 2: 归 4 交互系统(与拾取同构)

- **Description**:4 选目标 + 主机判距,与拾取三段式完全同构。
- **Pros**:复用 4 已有的路由机制。
- **Cons**:4 明令「**只选目标,不结算**」(规则一),`AC-4-01` 判据 = 4 的代码路径**零 gameplay 结算**;
  且 4 **不拥有病人**(它把病人路由给 37 / 8 / 10 / 11)。
- **Estimated Effort**:低。
- **Rejection Reason**:违 4 的规则一(`AC-4-01` 结构断言);4 是**自报方**,不是写者。

### Alternative 3: 拆分 —— 021 归 9 · 022 归 10

- **Description**:痉挛归 9(病程),搬运归 10(玩家动作)。
- **Pros**:各自贴语义。
- **Cons**:021 归 9 的入向边界问题同上;且**两个同类玩家动作分属两个系统** ⇒ 判据不一致,维护成本高。
- **Estimated Effort**:中。
- **Rejection Reason**:判据不统一;021 的入向边界问题未解。

### Alternative 4: 13 直接写(把 13 升为可写)

- **Description**:给 13 开写权限。
- **Pros**:写者就近。
- **Cons**:**门 A / 门 B 结构性排除** —— 13 住边界层,消费 `VitalsDto`(float),物理上不可能住门 A 程序集;
  且违 `patient-ai.md` 规则一 / `disease-simulation.md:646`。
- **Estimated Effort**:高(须拆门 A)。
- **Rejection Reason**:结构性排除,非规模判断(同 ADR-017 §一)。

## Consequences

### Positive

- 两条 `gap`(`TR-patient-021` / `TR-patient-022`)结清 —— 写路径单一所有者明确。
- 复用既有三段式,**零新机制**;13 / 8 的只读约束不破。
- 与 10 已有的 CPR / 止血写路径同构 ⇒ 实现判据一致。

### Negative

- 021 的实现须等 10 的 GDD 轮定 Kind + registry 登记 ⇒ **有前置依赖**。
- 022 仍 P0 不实现 ⇒ `TR-patient-022` 虽 `covered` 但**无 P0 实现**。

### Neutral

- 痉挛的「内容归属」(9)与「`Append` 调用者」(10)分离 —— 与既有 `InjuryOnset` / `EmergencyTreatmentApplied`
  同款分工,非新形态。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 10 的 GDD 轮迟迟不追加 Kind ⇒ 021 悬空 | 中 | 中 | 本 ADR 已把 Kind 登记列为 10 的**具名义务**;`TR-patient-021` 的 `covered` 标「写路径已定,Kind 待登记」 |
| 意图事件走第二 QoS 丢包(承 `OQ-10-9`) | 中 | 中 | 归 ADR-001 窄修订(45 的 GDD 轮);本 ADR 不补丁 |
| 判距误读表现态位置 | 低 | 高(联机分叉) | 结构不可达判据:13 构造注入白名单不含表现态位置;`AC-4-02` 同款断言 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a | 无变化(复用既有判距路径) | — |
| Memory | n/a | 无变化 | — |
| Load Time | n/a | 无变化 | — |
| Network | n/a | 意图事件走既有第二 QoS(与 `EmergencyAttempt` 同通道) | — |

## Migration Plan

1. **本批**:ADR-027 落盘 Accepted;`TR-patient-021` / `TR-patient-022` 的 `adr` 字段填 `ADR-027`。
2. **10 的 GDD 轮**:追加写路径 Kind(021)+ 位置事件(022,进 P0 时);经 `entities.yaml` 登记 + kindgen 重跑。
3. **8 的措辞口径**:`diagnosis-system.md:1253` 的「10 / 13」收为「10」(13 写不了)。
4. **13 / 8 的构造注入白名单**:断言不含 `IEventSink`。

**Rollback plan**:本 ADR 为纯所有权裁决,回滚 = 撤销归属 + 恢复 `OQ-13-1` / `OQ-13-3` 为 open。
**无代码 / 数据受影响**。

## Validation Criteria

- [ ] 13 的代码路径**零 `IEventSink.Append`**(结构不可达判据通过)
- [ ] 8 的代码路径**零 `IEventSink.Append`**(承规则一)
- [ ] 021 的写路径事件经 `entities.yaml` 登记 + kindgen 重跑(10 的 GDD 轮)
- [ ] 判距吃整数格,不吃表现态位置(联机对拍:非主机目标选择 = 主机)
- [ ] `TR-patient-021` / `TR-patient-022` 的 `adr` 字段 = `ADR-027`

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/patient-ai.md` | 13 病人 AI | **TR-patient-021** 「查体诱发痉挛」的写路径归属(13 只读,写不了 sim) | 写路径 = **10**;13 只出表现(① / ③);形态承三段式(②) |
| `design/gdd/patient-ai.md` | 13 病人 AI | **TR-patient-022** 玩家搬运昏迷病人的写路径归属 | 写路径 = **10**(P0 不实现,只登记方向);位置投影承 ADR-020 §四(① / ④) |
| `design/gdd/diagnosis-system.md` | 8 诊断与体征揭示 | 破伤风事实注(:1250-1254)—— 查体触发痉挛的写路径 | 8 **只声明存在,不写**;写路径 = 10;措辞「10 / 13」收为「10」(③) |
| `design/gdd/emergency-procedures.md` | 10 急救动作 | 玩家对病人的物理干预写路径(CPR / 止血先例) | 021 / 022 与 CPR / 止血同构,归 10(①) |

## Related

- **兑现** `architecture.md` §Required ADRs **#5**(13 病人 AI 的写路径归属)。
- **结清** `OQ-13-1`(`TR-patient-021`)· `OQ-13-3`(`TR-patient-022`,P0 不实现)。
- **上游** `ADR-009`(§七 三段式)· `ADR-016`(§一 三源不变量 · §六 只读通道)· `ADR-020`(§四 对称落实)· `ADR-024`(registry 真源)。
- **同源** `ADR-011` §Amendment B(F1 改判:客户端不上报结果,主机判)· `AC-4-02`(4 零 `Append` 结构判据)。
- **未结(不在本 ADR 裁决面)**:`OQ-10-9`(意图事件第二 QoS 丢包 → ADR-001 窄修订)·
  `OQ-13-7`(`OnExamSessionChanged` 发出侧)· 10 的 GDD 轮须追加的 Kind 登记。
