# Story 002: 抽象点、SimEvent 与病史流机制

> **Epic**: 疾病与伤情模拟
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/disease-simulation.md`(规则一 事件流唯一真源 · 规则十 折叠 · Dependencies 实现顺序 2「抽象点」/ 3「SimEvent」)
**Requirement**: TR-disease-005(patient_seed = hash 派生禁 Random)· TR-disease-006(ITickProvider)· TR-disease-007(病史事件流唯一真源)· TR-disease-008(五抽象点 + SimEvent 形状,含 Seq/Payload)· TR-disease-009(主机唯一执行 Step/CatchUp)· TR-disease-010(终态折叠)· TR-disease-011/012(patient_id 跨权威稳定 + 折叠行保留 id)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主)+ Amendment A(SimEvent 补 `Seq` + `Payload`)· ADR-006(全序键 + Amendment B 机制 A)· ADR-007(IEventAuthority 第六抽象点;WorldSeed 归存档头非 SimEvent)· ADR-010(折叠)· ADR-024(Kind 白名单路由,列表外构建期拒绝)
**ADR Decision Summary**: `SimEvent` 形状 = `(Kind, Tick, Patient, Seq, Payload)`,全序键 `(Tick, StreamPriority, Patient, Seq)`;patient_id = 计数器永不复位 + 迁移后 `next = max(patient_id)+1` 由事件流重构,**终态折叠行必须保留 patient_id**;世界级事件用 `PatientId.None = -1` 哨兵不污染高水位;`Seq` 按 (Tick, Patient) 发号每 tick 复位。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯 C# 契约与机制;抽象点形状刻意不用任何 post-cutoff API)
**Engine Notes**: 契约住 `Sim.Contracts`(七抽象点 + `SimEvent`;成文时为六,ADR-029 后增第七个);`IEventSink.Append` 的路由实现读 kindgen 产物 `src/Sim/StreamRouting.g.cs`(ADR-024)。

**Control Manifest Rules (this layer)**:
- Required: 一切病情状态变更 = 进流事件(唯一真源);主机关口执行 Step/CatchUp;patient_seed 纯函数派生
- Forbidden: `UnityEngine.Random` / 墙钟 / 表现态量入 sim;「先改状态后补事件」的第二真源写法;绕过 `IIdAuthority` 自造 id
- Guardrail: 9 的白名单 = 「列表外构建期拒绝」—— 新 Kind 必须走 `entities.yaml` + kindgen,不得在代码里加例外

---

## Acceptance Criteria

*From GDD `design/gdd/disease-simulation.md`, scoped to this story:*

- [ ] **AC-2**[A]:五+一抽象点接口齐备且 `Sim` 内零引擎引用
  > ⚠️ **2026-10-03 注(不追改判据)**:本 AC 的枚举 = **成文时**的六支,**正确且已由本 story 交付**。ADR-029(2026-10-02)已追加**第七个** `IPayloadEncoder`;该点由 `persistence-service/story-002` 交付,归其判据面。**本条不扩**(改已 Complete story 的判据 = 范围变更);引用抽象点计数时以 `control-manifest` 为准。
  > 原文: —— `ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery` / `IPresenceQuery` / `IEventAuthority`(承 ADR-007 第六点裁定)
- [ ] **AC-2**[A]:`SimEvent` 载荷字段类型全 ∈ 整数域(Kind int 枚举 / Tick long / PatientId int / Seq int / Payload 整数域);反射断言判据(非 grep)
- [ ] **AC-15 / 有界性**[A]:同场被模拟病人数硬上限 `PATIENT_APPEARANCE_CAP = 24`(超界拒收 = 集成断言失败);**在场才模拟**(离屏病人不进 Step,由 F3 CatchUp 补算 —— 补算本体归 story 006,本 story 交付 Presence 门与拒收断言)
- [ ] **AC-36 系 id 机制**[L]:IIdAuthority 计数器永不复位 0;由事件流重构 `next = max(patient_id)+1`(`None=-1` 排除于 max);终态折叠行保留 patient_id 的往返断言
- [ ] **AC-16 流侧**[L]:同一 (Tick, Patient) 内 Seq 单调且每 tick 复位;跨流全序键四元组比较器逐位确定(纯函数,无稳定排序兜底)
- [ ] **AC-15 处置去重**[L]:重发拒收(同一 (Kind, Patient, Tick, dose_seq) 只铸一条) + 同 tick 多剂各叠加 + `MAX_ACTIVE_DOSE` clamp —— 五元组去重键断言
- [ ] **TR-disease-005**[L]:patient_seed = SplitMix64(WorldSeed 派生盐, patient_id) 纯函数;重复派生同值;禁任何运行期随机源
- [ ] **AC-15**[I]:主机唯一执行门 —— 客户端调 `Step`/`CatchUp` 入口即抛(运行期断言 + 编辑期可测的 fake 注入)

---

## Implementation Notes

*Derived from ADR-005 / ADR-006 / ADR-007:*

1. `SimEvent` 为 `readonly struct` 或不可变 class(分配压力留实现期优化,形状先钉死);`Payload` 为具名整数域联合(每 Kind 一 schema,由 kindgen 生成的路由表约束)。
2. `IEventSink.Append(event)` 是**纯函数**:按 Kind→StreamId 白名单路由(读 `StreamRouting.g.cs`),列表外**构建期拒绝** ⇒ 运行期面 = 单元测试以「合成未知 Kind」验证拒绝路径(承 ADR-024)。
3. `Seq` 发号器内聚于主机侧写出路径(与 ADR-011 Amendment B「主机发 Seq」同口径);9 自身只消费,不铸造他系统 Seq。
4. 折叠谓词 `Folded(p)` 的**读侧**归 7a 持久化 epic;本 story 只交付:终态行进入流 + patient_id 保留 + SkillGrown 豁免行不被动折叠(ADR-026 案 1 的守卫断言)。
5. `IPresenceQuery` 提供方 = 表现层注入(格/在场视图);sim 侧只读整数在场标志,**禁**接收连续位置(AC-20-03 同构纪律)。
6. WorldSeed 从**存档头**注入(ADR-007 裁定:非一条 SimEvent);接口形状 = `IWorldSeedSource` 或构造入参,实现落点避免费用「事件流里找种子」。

## Out of Scope

- [Story 001]: SplitMix64 / Fix 原语(本 story 复用)
- [Story 003]: Kind 注册表 schema、烘焙管线、kindgen 生成物本身
- [Story 006]: F3 CatchUp ≡ Step 对拍(本 story 只留入口)
- 存档二进制 codec / 折叠的持久化侧(7a persistence epic)
- 网络上行与重排缓冲(45 网络层 epic;ADR-001)

## QA Test Cases

*Written at story creation(lean mode — specs self-authored from AC text).*

- **白名单拒绝**: Given kindgen 产物含全部登记 Kind。When `Append` 一个注册表外合成 Kind。Then 拒绝(异常/失败返回值按接口契约),且无任何状态被改。
- **Seq 复位**: Given (Tick=100, Patient=7) 连发 3 事件。When 到 Tick=101 再发。Then Seq 从复位值重头单调;比较器对 (101,·,·,0) < (100,·,·,9) 为假(Tick 主键)。
- **id 重构**: Given 事件流含终态折叠行。When 从空计数器重放。Then `next = max+1` 成立,None=-1 不入 max;折叠行 patient_id 可读。
- **CAP 拒收**: Given 在场病人已达 24。When 第 25 个进入在场集。Then 集成断言失败(拒收),非静默扩集。
- **种子纯函数**: Given 同 (WorldSeed, patient_id)。When 双跑派生。Then 同值;源码层零 `UnityEngine.Random`(IL/源文本双层扫描,同 story 001 机制)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DiseaseSimulation/event_stream_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/DiseaseSimulation/event_stream_test.cs`)

---

## Dependencies

- Depends on: Story 001(Fix/哈希原语)、skill-system epic(`SkillGrown` Kind 已入 registry 的折叠豁免守卫)、item-database / kindgen 产物(story 003 交付生成器,本 story 以登记 YAML 为契约面先行)
- Unlocks: Story 004 / 005 / 006(全部求值面)、emergency-procedures epic(004 的 Judge+Append 落流)、prescription-medication epic(DrugTreatmentApplied 写入)、casebook / case-system epic(病例流 Kind 消费同管道)

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `IPresenceQuery` — 在场查询抽象点
- `EventStream` — 病史事件流实现（IEventSink）
- CAP 拒收（PATIENT_APPEARANCE_CAP = 24）
- 去重（五元组键）
- Seq 发号（每 tick 复位）
- id 重构（max+1，None=-1 排除）
- 测试: 5 条单元测试（全部通过）

**Deviations**: 无

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/DiseaseSimulation/event_stream_test.cs` — 5 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
