# Story 007: SkillGrown 事件发射

> **Epic**: 技能与熟练度
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skill-system.md`(§3.2 成长规则 · §8 功能验收第 4 条)
**Requirement**: —(无专属 TR;验收标准来自 GDD §3.2 规则二「SkillGrown 落病史流」与 §8 AC-4)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟与状态同步模型 · ADR-007: 事件权威与掷骰状态 · ADR-009: 世界状态的事件化边界 · ADR-024: 三流 Kind 单一登记真源
**ADR Decision Summary**: SkillGrown 落病史流(三流之一);主机唯一 Append;载荷 (actor_id, patient_id, skill_id, object_id, novelty_class) + Level(升级时携带);Level 未升级写哨兵不写 0;三流全序键 (Tick, StreamPriority, Patient, Seq);SkillGrown 已在 entities.yaml 具名登记(stream: history, author: 30)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 本 story 跨 sim 程序集(30)与 IEventSink 接口 —— 须经 ADR-025 的 Sim.Contracts 传送;零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: SkillGrown 载荷全部整数域(actor_id int / patient_id PatientId / skill_id int / object_id int / novelty_class int / Level int);主机唯一 Append
- Forbidden: 客户端直接 Append SkillGrown(须经主机 IEventSink);浮点字段进入载荷
- Guardrail: SkillGrown 不携带 disease_id(承 ADR-018 PresentationDtoGuard 递归扫描口径)

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] `EmitGrowth(int actorId, int skillId, int objectId, NoveltyClass novelty, int level)` 是唯一对外入口
- [ ] 每次成长触发一条 SkillGrown 事件,经主机唯一 Append 入病史流
- [ ] 载荷结构 = (actor_id, patient_id, skill_id, object_id, novelty_class, Level)
- [ ] 升级时 Level = 新等级(绝对等级);未升级时 Level = 哨兵(-1,非 0)
- [ ] patient_id 语义:技能成长通常与具体病例无关 → PatientId.None(-1)(世界级成长)
- [ ] 三流全序键自动附加(Tick / StreamPriority / Patient / Seq),30 不构造
- [ ] 51 可离线重算「练了多少 / 新 vs 重复 / ΔLevel」,零重算 XP_gain(承 51 的 AC-51-B11)
- [ ] SkillGrown 不承载 disease_id( PresentationDtoGuard 递归扫描覆盖)

---

## Implementation Notes

*Derived from ADR-026 / ADR-007 / ADR-009 Implementation Guidelines:*

1. **`EmitGrowth(int actorId, int skillId, int objectId, NoveltyClass novelty, int? level)`** —— 唯一对外入口。`level` 为 nullable int:有值 = 升级时携带新等级;null = 未升级,写入哨兵。
2. SkillGrown 的 `payload` 是**值 struct**(无引用字段),住 `Sim.Contracts.Payloads` 程序集。
3. **主机唯一 Append**:`EmitGrowth` 不直接调用 `IEventSink.Append` —— 它把 SkillGrown 事件**交给主机权威**(ADR-007 §Decision 一)。实现形态 = 30 提供「请求 Append SkillGrown」的接口,主机在 Step 中批量处理。
4. **patient_id = PatientId.None**:技能成长通常不绑定具体病人(采集药材、格斗训练等)。例外:8 诊断的技能成长绑定具体病例(patient_id = 该病例 id) —— 由 8 在调用 `EmitGrowth` 时传入 patientId。
5. **调用方**:8 诊断 / 11 处方用药 / 17 采集炮制 / 10 急救 / 25 格斗 —— 各自在完成动作后调用 `EmitGrowth`。30 **不主动触发**成长(不监听事件流),成长判定由调用方完成(调用方判断「这次动作是否满足成长条件」)。
6. **与 Story 002 的接缝**:调用方先算 `xpGained = ComputeXpGain(skill, object, novelty, difficulty)`,再 `EmitGrowth(actor, skill, object, novelty, newLevel)` —— 两步原子性由调用方保证。

---

## Out of Scope

- [Story 002]: XP_gain 计算(本 story 的前置,emit 之前调用)
- [Story 003]: 升级判定(本 story 消费 level 但不计算升级)
- [Story 008]: 从流重构 SkillGrown(本 story 只写,不读)

---

## QA Test Cases

**[Integration story — automated test specs]:**

- **AC-1**: EmitGrowth 触发 SkillGrown 事件
  - Given: 主机 IEventSink 可用, actorId=1, skillId=诊断(0), objectId=大叶性肺炎(1), novelty=First
  - When: EmitGrowth(1, 0, 1, First, null)
  - Then: 一条 SkillGrown 事件经主机 Append 入病史流;载荷包含 actor_id=1, skill_id=0, object_id=1, novelty_class=First, Level=-1(哨兵)
  - Edge cases: 同一 actor 同一对象第二次 → novelty=Stale → 载荷 novelty_class=Stale

- **AC-2**: 升级时 Level 携带新等级
  - Given: 诊断当前等级=1, 本次 XP 足够升级到 2
  - When: EmitGrowth(1, 0, 1, First, 2)
  - Then: SkillGrown 载荷 Level=2(绝对等级,非增量)
  - Edge cases: Level=60(满级) → 仍携带 60

- **AC-3**: 未升级时 Level 写哨兵
  - Given: 诊断当前等级=5, 本次 XP 未达升级阈值
  - When: EmitGrowth(1, 0, 1, Normal, null)
  - Then: SkillGrown 载荷 Level=-1(哨兵,非 0)
  - Edge cases: Level=0 是真实等级(第 0 级),与哨兵 -1 不冲突

- **AC-4**: patient_id 默认 PatientId.None
  - Given: 采集药材(不绑定病例)
  - When: EmitGrowth(actorId, 采集, 药用植物品种, First, null)
  - Then: SkillGrown 载荷 patient_id = -1(PatientId.None)
  - Edge cases: 8 诊断传入具体 patientId → 载荷包含该病例 id

- **AC-5**: 三流全序键自动附加
  - Given: 主机 Append SkillGrown 事件
  - When: IEventSink.Append(SkillGrownPayload)
  - Then: 事件自动附加 (Tick, StreamPriority=病史, Patient, Seq)(30 不构造这些字段)
  - Edge cases: 同一 tick 同一 actor 多条成长事件 → Seq 自增

- **AC-6**: 载荷无 disease_id
  - Given: SkillGrownPayload 定义
  - When: 反射扫描载荷字段
  - Then: 字段集合 = {actor_id, patient_id, skill_id, object_id, novelty_class, Level};零 disease_id
  - Edge cases: PresentationDtoGuard 递归扫描不报警

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/skill-system/skill_grown_stream_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(ComputeXpGain)· Story 006(NoveltyTracker)· Story 001(调参表)
- Unlocks: Story 008(流重构依赖 SkillGrown 事件已可写)
