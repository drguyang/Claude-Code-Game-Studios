# Story 001: 病例流 Kind 登记与病例身份

> **Epic**: 病例系统
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/case-system.md`(F-37.2 身份/全序 · 承重账五 Kind · §规则一 记录≠实体)
**Requirement**: TR-case-001(case_id 三元组可从事件流重构) · TR-case-002(病例事件落独立第二条逻辑流) · TR-case-004(跨流全序键) · TR-case-005/006/008/009(载荷契约与哨兵)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例事件流;ADR-024: Kind 单一登记真源
**ADR Decision Summary**: 五 Kind 全部落**病例流**:`CaseOpened {patient_id, disease_snapshot}` / `CaseClosed {disease_set, treated}` / `PatternRecognized {PatientId.None, anchor_case, salted_key, member_set}` / `JudgmentRecorded` / `JudgmentRevised`;`entities.yaml` 为唯一真源(stream/author/payload_schema 三必填,载荷字段只允许整数域),kindgen 生成 `StreamRouting.g.cs` + 断言 A1–A5;`case_id = (Tick,Patient,Seq)`(Seq 仅同 `(Tick,Patient)` 内单调,单独取会撞号);**`opened_tick` 从载荷删除**(与 `SimEvent.Tick` 是同一事实的别名 —— ADR-006「少一份可失步的状态」);跨流全序 `(Tick, StreamPriority, Patient, Seq)`,病史流 < 病例流;世界级事件 `PatientId.None = -1` 不污染 `max(patient_id)` 高水位(ADR-007 §四,高水位扫三流并集)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 数据结构与路由为纯 C#;不触 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 五 Kind 在 `design/registry/entities.yaml` 具名(stream: case);三元组判等 = 全序无平局;`DiseaseId`/`DiseaseIdSet` = 版本化整数 ordinal(**禁 Fix** —— FixSet 之错已裁)
- Forbidden: 载荷携带 `opened_tick`/浮点/字符串判定字段(freehand_text 例外已登记 ADR-006 Amendment A,纯呈现语义);37 侧自造第七抽象点或新计数器
- Guardrail: 病例流不参与终态物理折叠(ADR-008 §六);kindgen A5 双向差集归零才算过

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-01:病例流任意 `CaseOpened` 的 `(Tick,Patient,Seq)` 全流唯一;任何被引用的 `case_id` 等于某条 CaseOpened 三元组
- [ ] AC-37-02:两病人同 tick 各立案 ⇒ 两个 case_id 不同(两人各有 Seq=0,Patient 区分,不撞号)
- [ ] kindgen 断言批:五 Kind 的 A1 唯一流别 / A2 载荷 ∈ 整数域 / A3 无重名 / A4 author=37 / A5 双向差集 全过
- [ ] 载荷形状断言:`CaseOpened`/`CaseClosed` 载荷中**无** `opened_tick` 字段(别名纪律);`PatternRecognized` 载荷**含** `salted_key`(AC-37-14 的载体半边在此建)
- [ ] 跨流全序复验:构造病史/病例交错流,`(Tick,StreamPriority,Patient,Seq)` 排序确定且无平局(哨兵 -1 行可排序不崩)
- [ ] 高水位纯净:含哨兵行的流上 `max(patient_id)` 重构不受 -1 污染(ADR-007 §四 消费面)

---

## Implementation Notes

*Derived from ADR-008/024 Implementation Guidelines:*

1. `entities.yaml` 增五行后跑 kindgen;`StreamRouting.g.cs` 为生成物不手改(承 ADR-009 §三 降级为路由注记的口径)。
2. `CaseId` 结构体 = readonly 三元组 + 比较器(比较序显式:Tick→Patient→Seq);全库禁以 `Seq` 单独为键。
3. ordinal 类型(`DiseaseId`/`LexiconId`)编码按 int/u16 走 ADR-006 §五 自定义编码器;`FixParse` 解析集**不含**它们(D-21-17 同类纪律)。
4. 宿主迁移场景的病例流序列化归 7a(ADR-010 义务表),本 story 只交付事件形状。
5. `Judgment` 结构(`lexicon_id/confidence/freehand_text`)的形状在此钉,判定语义与词表门在 story-005。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 立案触发与唯一性
- [Story 003]: 结案前置谓词
- [Story 004]: 同源检测与 salted_key 计算
- [Story 005]: DTO 守密与词表烘焙门
- [Story 006]: 重放/持久化联调与跨系统边界

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(三元组唯一)**: 撞号反例
  - Given: 同 (Tick,Patient) 两条 CaseOpened(Seq 0/1)+ 另一 Patient 同 Tick(Seq 0)
  - When: 全流建 case_id 集
  - Then: 三者两两不同;任何 Seq 单独取值会撞(断言测试故意展示)
  - Edge cases: 跨主机迁移重放后同流同键(纯函数)
- **AC-2(kindgen 门)**: 登记完整性
  - Given: entities.yaml 五 Case Kind 行
  - When: 运行生成器
  - Then: A1–A5 全绿;故意删 author ⇒ A4 构建失败
  - Edge cases: 载荷塞 float 字段 ⇒ A2 拒(白名单非 grep)
- **AC-3(别名纪律)**: 载荷字段扫描
  - Given: CaseOpenedPayload/CaseClosedPayload 类型
  - When: 反射扫描
  - Then: 无 opened_tick 成员;tick 只在 SimEvent.Tick 一处
  - Edge cases: 读取便捷属性 `CaseOpened.Tick` 合法(别名非存储)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: kindgen(ADR-024 既有件);`IEventSink`/`SimEvent` 抽象点(ADR-005 既有)
- Unlocks: Story 002/003/004(全部写流故事的形状前提)、Story 006(序列化联调)

---

## Completion Notes

*(留空 — story 关闭时回填)*
