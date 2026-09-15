# ADR-021: POI 状态所有权与世界流承载 (POI State Ownership & World-Stream Carriage)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 一条核心用户裁定已锁(用户 2026-09-15 照准):
> **POI 状态的写者 = 6 世界与生态区**(容器顺理成章成为所有者,不新立系统)。
> **本 ADR 结清三方复核(2026-09-15)登记的洞 H2** ——
> 见 `design/research/expansion-review-2026-09-15.md` §五 / §十,登记于 `systems-index.md` §11。
> **另外两洞(H1 模态解锁 · H3 敌人可救治)不在本 ADR 范围** —— 用户裁定二者 ADR 推迟 P1a,
> 本轮仅在 `systems-index.md` §11 登记所有权。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 裁定写者 = 6 世界与生态区**)· technical-director(起草与裁决)
· 6 世界与生态区(POI 容器 · 状态所有者)· 17 采集(读 POI 定义做资源点绑定)
· 52 随机事件导演(读 POI 定义做 `spawn_anchor` 过滤)· 7a 持久化(世界流序列化承载方)

## Summary

系统 **6 世界与生态区**是全案 POI 的**容器**,`ADR-015 §一` 已把 **POI 锚点**放入确定性
**逻辑整数层**(手工烘焙固定世界)。但「POI 是否已清 / 已发现」这类**状态**不在烘焙数据里 ——
它随玩家行为改变,**丢失后不可由静态烘焙层重建**。三方复核将其登记为 **H2:POI 状态无拥有者**。
本 ADR 裁决:**POI 定义 = 派生态(烘焙逻辑层);POI 状态 = 模拟态(进第三条世界流);
所有者与唯一写者 = 6 世界与生态区**;新增世界流 Kind `PoiStateChanged`;并把该义务追加进
`ADR-010 §三` 的义务汇总表(唯一出处)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(世界状态事件化 / 所有权) |
| **Knowledge Risk** | **LOW** —— 纯 C# 数据边界与所有权裁决,不触及任何引擎 API |
| **References Consulted** | `docs/architecture/adr-009-world-state-event-boundary.md` · `adr-010-persistence-save-format.md` · `adr-015-world-geometry-fixed-world-lattice.md` · `design/research/expansion-review-2026-09-15.md` · `design/gdd/systems-index.md` |
| **Post-Cutoff APIs Used** | **None** —— 不依赖任何引擎 API |
| **Verification Required** | **None**(判据是纯数据流规格) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-009**(Accepted —— 世界流 / 三态判据 / `Kind → StreamId` 路由)· **ADR-010**(Accepted —— 义务汇总表 §三 · 世界流序列化 · 不折叠)· **ADR-015**(Accepted —— POI 定义落逻辑层整数 · 单一世界格) |
| **Enables** | **6 世界与生态区的 GDD / 实现**(首个架构约束)· 52 的 `spawn_anchor` 过滤(读 POI **定义**,不读状态) |
| **Blocks** | **6 的世界状态实现** —— 在写「POI 已清」之前须先 Accepted(该 GDD 在 P0 队列内,`systems-index.md` §11) |
| **Ordering Note** | 本 ADR **不阻塞** p0 其他系统;它只补 6 的状态面。ADR-009 §三 的 8 个 Kind 骨架**不含 POI**,本 ADR 是**第一条**以「系统 ADR 追加世界流 Kind」方式扩骨架的实例(ADR-009 §三 明写「载荷归各系统 ADR」) |

## Context

### Problem Statement

`systems-index.md:263` 记 6 世界与生态区 = 「生态区、地形、POI 的**容器**」。
`ADR-015 §一` 把 **POI 锚点 / 资源点 / 导航格**放进**确定性逻辑整数层**(手工烘焙,
经 ADR-014 烘成 `*.cooked`)。但「**POI 已清 / 已发现 / 已锁**」这类**状态**不在烘焙数据里 ——
它**随玩家行为改变**,而烘焙数据是静态的。

三方复核(2026-09-15,`expansion-review-2026-09-15.md` §五 接缝 8)将其登记为
**H2:POI 状态无拥有者**,原话:「`已清` 是**派生态**(`ADR-009 §四`)⇒ **不可推导**,
必须进世界流并指定写者」。**若不裁决**:6 的实现会各自决定「已清」往哪存(存档字段?
场景对象? 内存标志?),而**丢失该历史后无法重建** —— 直接违反 `ADR-005 §一`
的「事件流是唯一真源」。

### Current State

- `ADR-009 §二` 三态表有「静态地形 / 生态区布局 / 植被点 = 派生态」行,**没有 POI 状态行**。
- `ADR-009 §三` 世界流 **8 个 Kind 骨架不含 POI**(`Structure*` / `Drop*` / `Craft` / `ResourceHarvested`)。
- `ADR-010 §三` 义务汇总表(10 行)**无对应行**。
- 6 的依赖列为空(`systems-index.md:39`)—— 6 谁都不依赖;而**没人依赖 6 的状态**,
  只依赖它的**定义**做 `spawn_anchor` 过滤(`random-events.md:524`)。
  ⇒ 6 的**状态**此前既无写者、也无读者 —— 典型无主状态。

### Constraints

- **POI 定义**(位置 / 守卫配置 / 类型)是**手工烘焙内容**,属 `ADR-015 §一` 逻辑层整数数据,
  是唯一真源,**不进流**(加载期重建即验)。
- **POI 状态**随玩家行为变更,需**主机裁决**(并发:两玩家同时清同一 POI),需**跨客户端一致**。
- 世界流**不折叠**(`ADR-009 §六`),故本 ADR 须给出**有界性论证**。
- 禁令:世界状态类事件**不得**落病史流 / 病例流(`architecture.yaml` `world_state_events_in_history_stream`);
  反之亦然。
- 进流字段**禁 float**(`ADR-006 §Decision 二`)—— 状态一律整数枚举。

### Requirements

- 为 POI 状态指定**单一所有者与唯一写者**。
- 状态变更**事件化**,可从世界流**重构**(迁移 / 重放后状态一致)。
- **有界**:状态转移总数有限(满足 `ADR-009 §六` 的世界流有界性论证)。
- 该义务**进 `ADR-010 §三` 义务汇总表**(唯一出处,一处不留)。

## Decision

**① POI 一分为二:定义 = 派生态;状态 = 模拟态。**

| 面 | 三问 | 三态 | 承载 |
|----|------|------|------|
| **POI 定义**(位置 / 守卫配置 / 类型 / 锚点) | Q1(可派生且不变更) | **派生态** | 烘焙逻辑层(`*.cooked`,ADR-015 §一)· 加载期重建 · 不进流 |
| **POI 状态**(已发现 / 已清 / …) | Q1 + Q2 + Q3 | **模拟态** | **世界流**(`PoiStateChanged`)· 主机裁决 |

**② 所有者与唯一写者 = 6 世界与生态区。** 6 从「纯容器」升为「**POI 状态所有者 + 状态机**」。
不新立系统(与 `expansion-review` §四「不加 #54」取向一致;6 本就是 POI 容器,归属自然)。
写经 `IEventSink.Append`,**主机唯一执行**(承 `ADR-005` 主机唯一 Step)。

**③ 新增世界流 Kind `PoiStateChanged`,载荷 `{ poi_id, new_state }`。**
- `poi_id` = 逻辑层 POI **整数 id**(由烘焙数据分配,与 `WorldPos` 格同源)。
- `new_state` = **整数枚举**。**具体枚举值(未发现 / 已发现 / 已清 / 已锁 …)归 6 的 GDD** ——
  本 ADR 只定**边界与所有权**,**不定枚举**(同 `ADR-009 §三`「骨架先行,载荷归系统 ADR」)。
- `Patient = PatientId.None`(世界级事件,`ADR-007 §四`;不污染 `max(patient_id)` 高水位)。

**④ 有界性论证(补 `ADR-009 §六`)。**
POI 集**有限**(手工烘焙逻辑层,`ADR-015 §一`)+ 状态枚举**有限** ⇒
转移总数 ≤ `|POI| × |STATE|`。单 POI 的状态是否可逆由 6 定义,但**两侧均有界** ⇒
世界流仍是**玩家有界流**(与 `ADR-009 §六` 的「玩家动作 + 有限资源点」论证同构)。

**⑤ 与 52 的边界(锁死)。**
52 的 `spawn_anchor` 过滤**读 POI 定义(静态)**,**不读 POI 状态** ——
抽池不得依赖动态状态(否则抽池变成状态的函数,破坏 `random-events.md` 的确定性抽池前提)。

### Architecture

```
[烘焙逻辑层 *.cooked]  POI 定义(位置 / 守卫 / 类型)  ── 加载期重建,不进流
        │
        ▼
[6 世界与生态区]  POI 状态机(初始「未发现」)
        │  玩家行为触发转移(清点 / 发现)
        ▼
  IEventSink.Append(PoiStateChanged { poi_id, new_state })   ← 主机唯一
        │
        ▼
[世界流](第三条逻辑流,不折叠)
        ├──► 7a 持久化(序列化 / 迁移,ADR-010 §一/§二)
        ├──► 45 网络层(同步)
        └──► 读方(37? 无 —— 37 读病人;读 POI 状态者为表现 / UI,归后续 GDD)
```

### Key Interfaces

```
enum PoiId    : i32            // 逻辑层 POI id(烘焙数据分配;与 WorldPos 同源)
enum PoiState : i32            // 枚举值归 6 的 GDD(ADR-021 只定「是整数枚举」)

SimEvent {
  Kind    = PoiStateChanged
  Payload = { poi_id : PoiId, new_state : PoiState }
  Patient = PatientId.None     // 世界级(ADR-007 §四)
}

// 写者契约
6 世界与生态区 : 主机唯一 Append(PoiStateChanged)
```

### Implementation Guidelines

- 6 的状态机在读**烘焙 POI 表**时初始化(默认态);玩家动作触发转移时**写事件**,不改烘焙层。
- **重放 / 迁移语义**:POI 状态从**世界流重建**,**不读存档快照作为真源**
  (承 `ADR-009 §六` / `ADR-010 §一` 的「快照 = 优化非真相」)。
- `new_state` 一律**整数枚举**,禁 float / 禁字符串(ADR-006 边界)。
- POI **定义**继续走 ADR-014 烘焙管线(`assets/data/*.json` → `*.cooked`),本次不新增数据文件。

## Alternatives Considered

### Alternative 1: POI 状态 = 派生态(烘焙数据带状态)

- **Description**:把「已清」写进烘焙逻辑层,随世界一起出货。
- **Pros**:零新 Kind、零新写者。
- **Cons**:烘焙数据是**静态**的,不能承载「玩家是否清过」;一旦如此即制造**第二份真相**
  (烘焙值 vs 玩家实际),直接违反 ADR-005 §一。
- **Estimated Effort**: 低
- **Rejection Reason**: 结构性错误 —— 派生态的定义就是「无历史、可确定性重建」,而 POI 状态有历史。

### Alternative 2: POI 状态 = 表现态(走 45 网络层,不进流)

- **Description**:把「已清」当纯表现,由网络层 latest-value 同步。
- **Pros**:零事件流负担。
- **Cons**:违反 `ADR-009 §一` Q3(可感知性)——「已清」**决定掉落 / 资源 / 后续事件归属**,
  须所有客户端同一值;latest-value 通道到达时序不确定 ⇒ 状态分叉。
- **Estimated Effort**: 低
- **Rejection Reason**: Q3 判据直接否。

### Alternative 3: 并入建造流(复用 `StructureModified`)

- **Description**:POI 也是一种「结构」,复用 23 的 Kind。
- **Pros**:不新增 Kind。
- **Cons**:语义不同(POI 是烘焙地形对象,非玩家建造物);共用 Kind 会让 23 的载荷契约
  (`ADR-015 §五`:格坐标 + 模块 id + 朝向 + 变体)**被污染**,且 6 / 23 的所有权边界模糊。
- **Estimated Effort**: 中
- **Rejection Reason**: 载荷异形,共用 = 契约污染(同 `ADR-008` 拒绝「病例并入病史流」之理)。

### Alternative 4: 新立 POI 子系统

- **Description**:为 POI 生命周期单开一个系统行(±发现 / 清点 / 解锁)。
- **Pros**:语义最干净,单一职责。
- **Cons**:P0 加一个系统行,与三方复核「不加 #54」的取向相悖;6 本就是 POI 容器,
  归属它零新增。
- **Estimated Effort**: 中(新 GDD + TR + ADR + §9 小切法语义)
- **Rejection Reason**: 过度建制 —— 一个状态机不值得一个系统行。

## Consequences

### Positive

- **H2 闭合**:POI 状态有了所有者、写者、流归属,6 的 GDD 不再有静默缺口。
- **6 职责明确**:从「容器」升为「容器 + 状态所有者」,依赖图清晰。
- **7a 免费覆盖**:世界流已序列化(ADR-010 §一/§二),POI 状态随之落档,**零新增存档码**。
- 有界性论证**扩展而非重写**(ADR-009 §六 同构)。

### Negative

- **6 的 GDD 须写一个状态机** —— 此前 6 被当作「纯容器」,现在多一块实现面(M 级中的一小块)。
- **`PoiState` 枚举是新的洞** —— 枚举值须在 6 的 GDD 定稿;本 ADR 刻意不定(避免越权,
  同 ADR-009 §三 的教训)。

### Neutral

- 世界流新增 **1 个 Kind**(8 → 9);
  `architecture.yaml`(世界流 interface / IEventSink 路由 / 新增 `poi_state` 所有权条目)·
  `entities.yaml`(新 `SimEvent.Kind.PoiStateChanged`)· `ADR-010 §三`(追加第 11 行)各一处追加。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| `PoiState` 枚举定得过大 ⇒ 转移数膨胀 | 低 | 低 | 6 的 GDD 定稿时按 `ADR-009 §六` 复核有界性 |
| 6 / 23 / 17 对「POI」的定义漂移(谁是 POI) | 中 | 中 | POI 定义以 ADR-015 逻辑层为准;6 独占 POI 台账;本 ADR 不含 23 / 17 的 POI |
| 读方(谁显示「已清」)未定 | 中 | 低 | 归属后续 GDD(6 / 39 / 42);本 ADR 只定「写」侧 |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | — | 可忽略(转移频率 = 玩家清点速率) | 急救 16.6 ms / VR 11.1 ms 不受影响 |
| Memory | — | 可忽略(状态表 ≤ \|POI\| × 枚举宽度) | 待定(未定最低硬件) |
| Network | — | 可忽略(离散低频事件) | — |

## Migration Plan

**无既有实现**(6 未开始)。本 ADR 是 6 的**首个架构约束**,在写 6 的世界与生态区 GDD 之前
生效。ADR-009 §二/§三/§六 的三处就地修订见其 **Amendment F**。

1. 落地本 ADR 的涟漪:`ADR-009`(三处就地修订)· `ADR-010 §三`(追加行 11)·
   `architecture.yaml` · `entities.yaml` · `systems-index.md` §11(H2 闭合)。—— **本次即完成**
2. 写 6 的 GDD 时,定稿 `PoiState` 枚举 + 状态机规则。

**Rollback plan**:若 6 最终证明需要更细 / 更多的状态面,以**新 ADR 扩展 Kind**
(不得就地改本 ADR 的边界 —— 同 ADR-017 §三 的「另开 ADR」纪律)。

## Validation Criteria

- [ ] POI 状态变更**全部**经 `PoiStateChanged` 落世界流,**无第二存储**(grep 无旁路字段)。
- [ ] 重放 / 迁移后 POI 状态**可从世界流重建**(不依赖快照作为真源)。
- [ ] 世界流 POI 转移数 ≤ `|POI| × |STATE|`(有界性实测)。
- [ ] `PoiStateChanged` 的 `Patient = PatientId.None`(不污染高水位)。

## GDD Requirements Addressed

<!-- MANDATORY -->

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| *(无独立 GDD —— 需求由三方复核生成)* `design/research/expansion-review-2026-09-15.md` §五 H2 + `design/gdd/systems-index.md:263` | 6 世界与生态区 | 「POI 已清」状态须**可重构**且**跨客户端一致**;此前无写者 | 定为**模拟态**进世界流(`PoiStateChanged`),写者 = 6 |

> 需求文本为**代生**(同 1 / 2 的 `TR-player-*` / `TR-camera-*` 例):6 无独立 GDD,
> 需求由 ADR-021 裁决 + systems-index §11 H2 条目生成。**6 撰写 GDD 时须回溯核对**。

## Related

- **修订** `ADR-009 §二 / §三 / §六`(见其 **Amendment F**)—— 就地完成,不单列修订清单。
- **追加** `ADR-010 §三` 义务汇总表第 11 行(POI 状态的世界流承载)。
- **上游** `ADR-015`(POI 定义 = 逻辑层整数)· `ADR-014`(烘焙管线)。
- **洞来源** `design/research/expansion-review-2026-09-15.md` §五 接缝 8 / §十;`systems-index.md` §11 H2。
- **同批未结**:H1(模态解锁)· H3(敌人可救治)—— 用户裁二者 ADR **推迟 P1a**,
  本轮仅在 §11 登记所有权。
