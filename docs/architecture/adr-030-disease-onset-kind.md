# ADR-030: 病程 onset 与病人出现的 Kind 归属 (DiseaseOnset Kind Ownership)

## Status

Accepted

> **2026-10-09 起草并转 Accepted。** 用户裁定(2026-10-09,照准「按建议来」):
> **Q1 = 甲案(病人出现与病程 onset 是同一个事件)** · **Q2 = 一个 Kind `DiseaseOnset`** ·
> **Q3 = 写者(Append 调用者)= 9** · **Q4 = 落病史流**。
> 本 ADR **结清一处 registry 缺口** —— 三处权威件引用「病程 onset / 病人出现」事件,
> 而 `entities.yaml` 的 34 支 `SimEvent.Kind` 中**无一登记它**(同 `CompoundTriggered` /
> `ActorCellEntered` / 关卡工具的「引用却无登记」失效模式)。

## Date

2026-10-09

## Last Verified

2026-10-09

## Decision Makers

dr_guyang(用户 · **2026-10-09 四项裁定:同一个事件 / `DiseaseOnset` / 写者 9 / 病史流**)
· technical-director(起草与裁决)· 9 疾病与伤情模拟(Kind 登记轮所有者 · 写者)
· 52 随机事件导演(病人出现注入方 · `random-events.md:683`)· 37 病例系统(立案落地方)
· 13 病人 AI(在场视图消费方 · 规则十一)· 47(神经衰弱 `causes[]` 的 onset 义务方)

## Summary

「病人出现」与「病程 onset」在**三处权威件**被当作**同一个事件**引用 ——
`disease-simulation.md:162`(规则六病史流第一行 `(onset_tick, 病种_id, patient_seed, Seq)`)、
`disease-simulation.md:174`(三流写入边界表「病程类(onset / 处置 / …)」)、
`persistence-service.md:161`(F-7a-4 折叠行首字段 `onset`)—— 但 `entities.yaml` 的 34 支
`SimEvent.Kind` **无一登记它**,故 `IEventSink.Append` 的按 `Kind` 纯函数白名单(ADR-008 §二)
**无法路由**这一事件(列表外构建期拒绝)。同时,垂直切片测试的病人腿注释把「病人出现」
**误标为 `InjuryOnset`** —— 而 `InjuryOnset` 是 25 的战斗伤害结算(`actor_id`/`target_id`/
`injury_id`/`magnitude`/`dose_seq`),**语义完全不同**。本 ADR 裁决:**立一个 `Kind`
`SimEvent.Kind.DiseaseOnset`**,落**病史流**,写者 = **9**(病人创建与 `patient_id` 分配唯一
归主机 9 —— `random-events.md:683` / `ADR-007:386`);「病人出现」= 该事件的**语义投影**,
不立第二个 Kind;载荷逐字对齐 9 规则六病史流第一行与 7a F-7a-4 折叠行。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(事件边界 · Kind 登记 · 所有权裁决) |
| **Knowledge Risk** | **LOW** —— 纯数据边界裁决,零引擎 API,零 post-cutoff 依赖 |
| **References Consulted** | `design/registry/entities.yaml`(34 支 `SimEvent.Kind` · ADR-024 真源)· `docs/architecture/adr-024-kind-single-source.md`(registry 单一真源 + kindgen 断言)· `docs/architecture/adr-008-case-event-stream.md`(§二 按 Kind 路由 · §六 有界性)· `docs/architecture/adr-009-world-state-event-boundary.md`(§二 三流分类 · §三 骨架)· `docs/architecture/adr-007-event-authority-and-roll-state.md`(:386 病人创建唯一归 9)· `design/gdd/disease-simulation.md`(规则六 :162 / :174 · :436 神经衰弱 · :709 战斗 onset)· `design/gdd/patient-ai.md`(规则十一 :167)· `design/gdd/random-events.md`(:683)· `design/gdd/case-system.md`(规则二 立案两路径)· `design/gdd/persistence-service.md`(F-7a-4 :161) |
| **Post-Cutoff APIs Used** | **None** —— 纯数据边界与所有权裁决 |
| **Verification Required** | `DiseaseOnset` 条目经 `entities.yaml` 登记后重跑 kindgen;生成器断言 **A1(唯一流别)/ A2(载荷 ∈ 整数域)/ A3(无重名)/ A4(author 必填)/ A5(双向差集归零)** 全过(ADR-024 §⑤)。**注意**:34 支既有 Kind 的三字段回填是 kindgen 的**编译前置**(ADR-024 Migration 步骤 4),本 ADR 只追加条目,不承担该回填 |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-024**(Accepted —— `entities.yaml` = `Kind` 单一登记真源;新 Kind 须先改 registry 再重跑 kindgen)· **ADR-008**(Accepted —— §二 按 `Kind` 纯函数路由 · §六 病人出现率上界 = 有界性硬界)· **ADR-009**(Accepted —— §二 三态/三流分类)· **ADR-007**(Accepted —— 病人创建唯一归 9 · `IIdAuthority` 机制 A)· **ADR-005**(Accepted —— 病史流唯一真源 · 主机唯一 `Append`) |
| **Enables** | **9 的 GDD 轮**(病史流第一行事件自此有 registry 具名条目,可进 kindgen 白名单)· **37 立案链**(`CaseOpened` 的前置病人实体自此有事件来源可依)· **7a 持久化**(F-7a-4 折叠行首字段 `onset` 自此有可折叠的源事件)· **垂直切片测试的病人腿**(误标 `InjuryOnset` 可订正为 `DiseaseOnset`) |
| **Blocks** | **9 的写者实现**(`IEventSink.Append(DiseaseOnset)` 在 registry 未登记前被构建期拒收);**47 的神经衰弱 onset 义务**(`disease-simulation.md:436` —— 47 是义务方,须有具名 Kind 可发) |
| **Ordering Note** | 本 ADR **只裁 Kind 归属与登记**(名称 / 流别 / 写者 / 载荷形状);**9 侧写者代码**、**47 的 `causes[]` 注入落地**、**垂直切片测试订正**归各自实现轮。**不阻塞**其他系统 |

## Context

### Problem Statement

**「病人出现」在事件流里没有具名 `Kind`,而三处权威件已经引用它。** 这是 ADR-024 立项时
识别的同一失效模式的又一实例(引用却无登记 —— 同 `CompoundTriggered` / `ActorCellEntered` /
ADR-022 关卡工具)。后果不是文档不齐:ADR-008 §二 定死 `IEventSink.Append` 的**按 `Kind`
纯函数路由白名单,列表外构建期拒绝** —— 因此 9 创建病人时**无法写出一条合法事件**,
7a 的 F-7a-4 折叠行**没有可折叠的源**,垂直切片测试的病人腿**只能借 `InjuryOnset` 假替**
(而后者是 25 的战斗伤害结算,载荷与语义都不同)。

### Current State

- **34 支 `SimEvent.Kind`**(`entities.yaml`,ADR-024 真源)中,**病史流 13 支**:`CareApplied` /
  `CompoundTriggered` / `CompoundExpired` / `InjuryOnset` / `EmergencyAttempt` /
  `EmergencyTreatmentApplied` / `DrugTreatmentApplied` / `EventRolled` / `EventArrived` /
  `ThreatDeferred` / `ThreatDeferralCleared` / `HistoryFlagChanged` / `SkillGrown`
  —— **无一表示「病人出现 / 病程 onset」**。
- **`InjuryOnset` 不是它**:载荷 = `{ actor_id, target_id, injury_id, magnitude, tick, dose_seq }`
  (作者 = 25 格斗与武器线,战斗伤害施加方;`combat-and-weapon-lines.md:238` 规则二)。
  它与「病人实体被创建」无任何字段重叠。
- **三处权威件引用「病程 onset」**:
  - `disease-simulation.md:162`(规则六病史流第一行)`病史 = [ (onset_tick, 病种_id, patient_seed, Seq), … ]`
  - `disease-simulation.md:174`(三流写入边界表)`病史流 | 病程类(onset / 处置 / 护理动作起止 / compounds 触发/窗止 / 终态折叠)`
  - `persistence-service.md:161`(F-7a-4 折叠行)`折叠行 = (onset, 病种_id, patient_id, patient_seed, outcome, t_end)`
- **「病人出现」的语义**:`patient-ai.md:167`(规则十一)「病人出现由 **52 随机事件导演注入、
  经 37 立案落地**;『病人出现率上限』归 **9**(`TR-disease-021`)」—— 52 **只发事件、不改任何状态**,
  **病人的创建与 `patient_id` 分配唯一归主机 9**(`random-events.md:683`)。
- **`DiseaseOnset` 这一名字全库零出现**(grep 确认)—— 本 ADR 是它的**命名首现**。

### Constraints

- **`Kind` 唯一真源 = `entities.yaml`**(ADR-024 ①);追加通道 = **registry 直登**
  (ADR-024 ③;Amendment 追加通道已退役)—— 本 ADR 落盘后须同步登记条目并重跑 kindgen。
- **一个 `Kind` 无法同时落两条流**(ADR-008 §一)—— 路由是 `Kind` 的纯函数。
- **载荷必须 ∈ 整数域**(ADR-024 ② A2;ADR-006 边界 —— 禁 `float`)。
- **病史流是病人侧**(`InjuryOnset` 条目 constraint:「玩家亦是 `PatientState`,同流」)。
- **病人创建唯一归 9**(`random-events.md:683` · `ADR-007:386`「52 零 `IIdAuthority.Next()` 调用」)。

### Requirements

- 立一个 `Kind`,使 9 能在创建病人时写出一条**可路由、可折叠、可重建**的事件。
- 载荷须**逐字对齐** 9 规则六病史流第一行(`onset_tick` / 病种_id / `patient_seed` / `Seq`),
  并**保留 `patient_id`**(7a F-7a-4 折叠行明文「`patient_id` 必留」—— ADR-006 Amendment B
  的高水位重构依赖它)。
- 「病人出现」与「病程 onset」若为同一事件,**不得**立第二个 `Kind`(Q1 = 甲)。

## Decision

### ① 「病人出现」= 病程 onset(同一个事件 · 甲案)

**不立第二个 `Kind`。** 9 创建病人实体、分配 `patient_id` 的那一 tick,写出的**唯一**事件
即病程 onset —— 「病人出现」是它在**在场视图 / 立案链**上的**语义投影**,不是独立事件。
依据:`patient-ai.md:167` 已定「病人出现由 52 注入、经 37 立案落地」,而 `random-events.md:683`
定「病人的创建与 `patient_id` 分配唯一归主机 9」—— 病人实体存在的**唯一时刻**即 9 分配 id 的时刻,
此时刻写一条事件,语义上同时是「病人出现」与「病程开始」。

> **被否的乙案**(立两个 `Kind`:`PatientAppeared` + `DiseaseOnset`)见 §Alternatives 1 ——
> 它会造出两条**同一时刻、同一主体、同一因果**的事件,而二者之间没有任何独立信息
> (病种在病人身上开始 = 病人带着这个病出现),徒增路由面与去重负担。

### ② 新 `Kind` = `SimEvent.Kind.DiseaseOnset`,落**病史流**

- **名称**:`SimEvent.Kind.DiseaseOnset`(命名首现于本 ADR)。
- **流别**:`stream: history`(病史流)—— 它是病人侧事件,与 `InjuryOnset` / `CareApplied` 同流;
  病史流可随终态折叠(9 的主场)。
- **登记**:`entities.yaml` 新增条目(§Migration Plan 步 1),`author` 字段必填(ADR-024 A4)。

### ③ 写者(`Append` 调用者)= **9**

**9 是唯一写者** —— 依据 `random-events.md:683`「病人的创建与 `patient_id` 分配唯一归主机 9」
与 `ADR-007:386`「52 **零** `IIdAuthority.Next()` 调用(病人创建唯一归 9)」。
**52 只发事件,不改任何状态**:52 注入的「急召出诊 / 原型疫情」是**输入**(经 ADR-016 §八
「52 不直接驱动 13」的入向数据引用),**真正创建病人实体、分配 id、写 `DiseaseOnset` 的是 9**。

> **⚠️ 与 9 边界表「调用者按事件逐一指定」口径一致**(`disease-simulation.md:180`):
> 病史流表列名是「9 写入的 Kind」= **内容归属**,不是「9 是 `Append` 调用者」的断言;
> 但 `DiseaseOnset` 是**内容归属与调用者同为 9** 的一支(与 `InjuryOnset` 的「内容归 9 /
> 调用者 25」形成对照)。

### ④ 载荷 = `(onset_tick, disease_id, patient_id, patient_seed, seq)`

逐字对齐 9 规则六病史流第一行 `(onset_tick, 病种_id, patient_seed, Seq)`,并**补 `patient_id`**
(7a F-7a-4 折叠行明文「`patient_id` 必留」):

```
onset_tick    : i64    // 病人出现 / 病程开始的那一 tick
disease_id    : i32    // 病种枚举(P0 · 8 项,R3)
patient_id    : i32    // IIdAuthority 机制 A 分配(ADR-006 Amendment B 高水位重构依赖)
patient_seed  : i64    // = hash(world_seed, patient_id) —— 禁 Random.Range(规则六)
seq           : i64    // 主机 Append 时分配(ADR-006 Amendment A/C;同一 (Tick, Patient) 内单调)
```

**去重键** = `(onset_tick, patient_id)` —— 同一病人在同一 tick 只出现一次。
**`patient_seed` 由 `hash(world_seed, patient_id)` 派生**(规则六 :164),**禁 `Random.Range`**
—— 它是事件流的纯函数派生量,主机迁移后可重构(ADR-007 核心不变量)。

### ⑤ 有界性 ≤ 病人出现率上界(`PATIENT_APPEARANCE_CAP`)

`DiseaseOnset` 的事件率 = **病人创建率** ≤ 9 的「病人出现率上限」配置项
(`TR-disease-021` = `PATIENT_APPEARANCE_CAP`,值 **24**;ADR-008 §六 有界性硬界)。
**与 tick 频率无关** —— 病人创建是**玩家抵达 / 事件驱动**的离散动作,不随帧率增长。
本 ADR **扩展** ADR-008 §六 的有界性论证(新增一支病史流 Kind 的界),**不重写**。

### ⑥ 47 的「发 onset 事件」义务 = 47 侧落地债,`Kind` 登记归 9 的 GDD 轮

`disease-simulation.md:436` 记 `DIS_NEURASTHENIA`(神经衰弱)`causes[]` = 空(自发)/ **47 入向事件**
—— 「47 承担『发 onset 事件』的义务」(2026-09-16 三轮:F6 下 Foundation 不反向依赖 Feature;
47 发事件 = **入向数据引用**,非 9 认识 47)。本 ADR 裁决:**该义务的 `Kind` 即 `DiseaseOnset`**
(47 发的就是这条 onset 事件),**但 47 不是写者** —— 47 通过入向数据引用触发 9 创建病人,
**由 9 执行 `Append`**。47 的落地归 **47 的 GDD 轮**;`Kind` 登记归 **9 的 GDD 轮**(本 ADR 已具名)。

### Key Interfaces

```csharp
// Sim.Contracts —— 载荷结构(ADR-024 A2:全整数域)
public readonly struct DiseaseOnsetPayload
{
    public readonly long OnsetTick;    // 病人出现 / 病程开始 tick
    public readonly int  DiseaseId;    // 病种枚举(P0 · 8 项)
    public readonly int  PatientId;    // IIdAuthority 机制 A
    public readonly long PatientSeed;  // hash(world_seed, patient_id)
    public readonly long Seq;          // 主机 Append 时分配
}

// 9 的写路径(唯一写者)
IEventSink.Append(new SimEvent {
    Kind = SimEvent.Kind.DiseaseOnset,   // → 纯函数路由到病史流
    Patient = patientId,                  // 病人侧事件(非 PatientId.None)
    Tick = onsetTick, Seq = seq,
    Payload = encoder.Encode(SimEvent.Kind.DiseaseOnset, payload)  // ADR-029 IPayloadEncoder
});
```

### Implementation Guidelines

- **`entities.yaml` 条目**照 `InjuryOnset` / `DrugTreatmentApplied` 格式(`stream` / `author` /
  `payload_schema` 三字段必填,ADR-024 ①)。
- **kindgen 重跑**后断言 A1–A5 全过;`DiseaseOnset` 出现在 `src/Sim/StreamRouting.g.cs` 的病史流分支。
- **垂直切片测试**的病人腿注释须由 `InjuryOnset` 订正为 `DiseaseOnset`(该测试当前为 `Assert.Pass` 桩)。

## Alternatives Considered

### Alternative 1: 立两个 `Kind`(`PatientAppeared` + `DiseaseOnset`)—— **否决**

「病人出现」与「病程 onset」拆成两条事件。**否决理由**:二者**同一时刻、同一主体、同一因果**
—— 病人在出现的那一刻就带着某个病种(9 创建病人必须指定 `disease_id`),两条事件之间
**零独立信息**,徒增路由面、去重键与排序负担。且 9 规则六病史流第一行**只有一行**
`(onset_tick, 病种_id, patient_seed, Seq)`,没有第二行的位置。**用户裁定 Q1 = 甲案**。

### Alternative 2: 复用 `InjuryOnset`(把病人出现塞进战斗伤害 Kind)—— **否决**

垂直切片测试的病人腿**事实上这么做了**(注释误标)。**否决理由**:`InjuryOnset` 载荷 =
`{ actor_id, target_id, injury_id, magnitude, tick, dose_seq }`,语义 = **战斗伤害结算**
(25 写,「命中写 onset」);病人出现**没有** `actor_id`(谁造成的?)、**没有** `injury_id`
(伤情 ≠ 病种)、**没有** `magnitude`。强行复用会把「病人出现」伪装成「有人打了他」,
破坏 9 的病程模型(伤情 D 集 vs 病种)与 25 的去重键。

### Alternative 3: 落**世界流**(病人出现是「世界状态变化」)—— **否决**

**否决理由**:病史流是**病人侧**事件流(`InjuryOnset` 条目 constraint:「玩家亦是
`PatientState`,同流」);病人出现的主体是病人自己,不是世界。9 的边界表
(`disease-simulation.md:174`)明列病史流承载「病程类(onset / …)」。落世界流会与
`EnemyInjuryOnset`(敌人伤情,ADR-016 §二)的**分流判据**冲突 —— 敌人伤情落世界流
**正是为了不污染病史流**;病人 onset 与之相反,应**留在**病史流。

### Alternative 4: 写者 = 52(注入方直接创建)—— **否决**

**否决理由**:`random-events.md:683` 明文「52 **只发事件,不改任何状态** —— 病人的创建与
`patient_id` 分配**唯一归主机 9**」;`ADR-007:386` 断言「52 **零** `IIdAuthority.Next()` 调用」。
若 52 直接创建病人,须把 `IIdAuthority` 交给 52 —— 破坏 ADR-007 的核心不变量
(掷骰的每个输入必须可从事件流重构;52 的 `Roll` 不得读运行时对象)。

## Consequences

### Positive

- **结清一处 registry 缺口** —— 9 规则六病史流第一行 / 7a F-7a-4 折叠行首字段 / 9 边界表
  「病程类(onset …)」三处引用自此有具名 `Kind` 可依。
- **9 的写路径合法化** —— `IEventSink.Append(DiseaseOnset)` 通过 kindgen 白名单,不再被构建期拒收。
- **7a 折叠可行** —— F-7a-4 折叠行 `(onset, …)` 有可折叠的源事件,`patient_id` 随载荷保留,
  ADR-006 Amendment B 的高水位重构成立。
- **垂直切片测试的病人腿可订正** —— 误标 `InjuryOnset` 得到修正(语义归位)。

### Negative

- **47 的义务须落地** —— `DiseaseOnset` 登记后,`DIS_NEURASTHENIA` 的「47 入向事件」义务
  从「无名债」变为「具名债」(47 的 GDD 轮须实现入向数据引用 → 9 创建病人)。
- **kindgen 编译前置暴露** —— 追加条目后,34 支既有 Kind 的三字段回填成为 kindgen 的编译前置
  (ADR-024 Migration 步骤 4)—— 这是**既有义务**,非本 ADR 引入,但会因本次重跑而更显性。

### Neutral

- 「内容归属」(9)与「`Append` 调用者」(9)在 `DiseaseOnset` 上**重合** —— 与 `InjuryOnset`
  (内容归 9 / 调用者 25)、`EnemyInjuryOnset`(内容归 9 / 调用者 25)形成对照,非新形态。
- 「病人出现」这一措辞在 GDD 中保留(它是**语义投影**的名字),`Kind` 名用 `DiseaseOnset`
  (它是**事件**的名字)—— 二者不冲突。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 9 的写者实现迟迟不落 ⇒ `DiseaseOnset` 登记后仍无生产者 | 中 | 中 | 本 ADR 已把「9 创建病人时 `Append(DiseaseOnset)`」列为 9 的**具名义务**;垂直切片测试订正后其病人腿即该义务的验收点 |
| 47 的 onset 义务与 `DiseaseOnset` 的耦合被误读为「47 是写者」 | 中 | 中 | ③ / ⑥ 明文:47 是**义务方 / 入向数据引用方**,`Append` 调用者**恒为 9**;措辞在条目 `author` 字段固化 |
| `patient_seed` 被误用 `Random.Range` 派生 | 低 | 高(非确定性) | ④ 明文「`hash(world_seed, patient_id)` 派生,禁 `Random.Range`」(承规则六 :164);ADR-007 核心不变量覆盖 |
| 与 `InjuryOnset` 混淆(测试 / 实现误标) | 中 | 中 | §Alternative 2 明文否决 + 载荷字段零重叠(`DiseaseOnset` 无 `actor_id`/`injury_id`/`magnitude`) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a | 无变化(一次 `Append` per 病人创建) | — |
| Memory | n/a | 无变化(载荷 = 5 整数字段,ADR-029 `IPayloadEncoder` 编码入池) | — |
| Load Time | n/a | 无变化 | — |
| Network | n/a | 病史流事件走主机权威通道(ADR-005 主机唯一 `Append`) | — |

## Migration Plan

1. **本批(2026-10-09)**:ADR-030 落盘 Accepted;**`entities.yaml` 追加 `SimEvent.Kind.DiseaseOnset`
   条目**(`stream: history` / `author: "9 疾病与伤情(病人创建唯一归主机 9)"` / `payload_schema` 五字段);
   `technical-preferences.md` ADR 日志追加 ADR-030 条目。
2. **9 的 GDD 轮**:`disease-simulation.md` 边界表「病程类(onset …)」的 `onset` 挂具名
   `DiseaseOnset`;9 写者实现(`IIdAuthority.Next()` → `Append(DiseaseOnset)`)。
3. **47 的 GDD 轮**:`DIS_NEURASTHENIA` 的 `causes[]` 入向事件落地(触发 9 创建病人)。
4. **测试订正**:垂直切片测试病人腿由 `InjuryOnset` 订正为 `DiseaseOnset`(该测试当前为桩)。
5. **kindgen 重跑**:生成 `src/Sim/StreamRouting.g.cs`,断言 A1–A5 全过。

**Rollback plan**:本 ADR 为纯 `Kind` 登记与所有权裁决,回滚 = 撤销 `entities.yaml` 条目 +
恢复 ADR 日志;无代码 / 数据受影响(9 写者尚未实现)。

## Validation Criteria

- [ ] `entities.yaml` 含 `SimEvent.Kind.DiseaseOnset` 条目,三字段(`stream` / `author` /
      `payload_schema`)齐备且 `stream: history`(ADR-024 A1 / A4)
- [ ] 载荷全字段 ∈ 整数域(无 `float` / 引用类型 —— ADR-024 A2 / ADR-006)
- [ ] kindgen 重跑后 `DiseaseOnset` 落 `StreamRouting.g.cs` 病史流分支,断言 A1–A5 全过
- [ ] 9 的代码路径 `Append(DiseaseOnset)` 通过构建期白名单(不再被「列表外拒绝」)
- [ ] 载荷含 `patient_id`(7a F-7a-4 折叠行「`patient_id` 必留」成立)
- [ ] 52 的代码路径**零** `IIdAuthority.Next()`(ADR-007:386 不变量维持)
- [ ] 垂直切片测试病人腿由 `InjuryOnset` 订正为 `DiseaseOnset`
- [ ] `TR-disease-024` 的 `adr` 字段 = `ADR-030`(已落)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | 规则六 :162 病史流第一行 `(onset_tick, 病种_id, patient_seed, Seq)` —— 事件须有具名 `Kind` | 立 `DiseaseOnset`,载荷逐字对齐该行并补 `patient_id`(② / ④) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | :174 三流写入边界表「病程类(onset / 处置 / …)」 | `onset` 挂具名 `DiseaseOnset`,落病史流(② / ③) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | :436 `DIS_NEURASTHENIA` 的 `causes[]`「47 承担『发 onset 事件』的义务」 | 该义务的 `Kind` = `DiseaseOnset`;47 是义务方,`Append` 调用者恒为 9(⑥) |
| `design/gdd/patient-ai.md` | 13 病人 AI | 规则十一 :167「病人出现由 52 注入、经 37 立案落地」 | 「病人出现」= `DiseaseOnset` 的语义投影,不立第二个 `Kind`(①) |
| `design/gdd/random-events.md` | 52 随机事件导演 | :683「52 只发事件,不改任何状态 —— 病人的创建与 `patient_id` 分配唯一归主机 9」 | 写者 = 9;52 零 `IIdAuthority.Next()`(③) |
| `design/gdd/persistence-service.md` | 7a 持久化服务 | F-7a-4 :161 折叠行 `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)` | 折叠行首字段 `onset` 有源事件;`patient_id` 随载荷保留(④) |
| `design/gdd/case-system.md` | 37 病例系统 | 规则二 立案两路径(病人实体须先存在) | `DiseaseOnset` 是病人实体的创建事件,立案链的前置(② / Enables) |

## Related

- **结清** registry 缺口 —— 三处权威件(9 规则六 :162 / :174 · 7a F-7a-4 :161)引用「病程 onset /
  病人出现」却无 `entities.yaml` 登记(同 `CompoundTriggered` / `ActorCellEntered` / ADR-022 关卡工具的
  失效模式)。
- **上游** ADR-024(registry 单一真源)· ADR-008(§二 按 `Kind` 路由 · §六 有界性)·
  ADR-009(§二 三流分类)· ADR-007(病人创建唯一归 9)· ADR-005(病史流唯一真源 · 主机唯一 `Append`)·
  ADR-006(Amendment A/C `Seq` · Amendment B `patient_id` 高水位)· ADR-029(`IPayloadEncoder` 编码路径)。
- **同源** `InjuryOnset` / `EnemyInjuryOnset` / `InjuryStateChanged` 三元组(25 的战斗 onset 登记轮,
  **`TR-combat-005`** = 「伤情 onset 三元组 Kind 登记」)—— 同一「系统 GDD 定稿 `Kind` 载荷」纪律;
  本件与之同型(**`TR-disease-024`** = 「病程 onset / 病人出现的 Kind 登记」)。
- **未结(不在本 ADR 裁决面)**:9 的写者实现 · 47 的 `causes[]` 落地 · 垂直切片测试订正
  —— 均归各自实现轮(见 §Migration Plan)。
