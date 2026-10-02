# Story 003: DrugTreatmentApplied —— 构造、零病名与写者独占

> **Epic**: 处方用药
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 8h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/prescription-and-medication.md`(规则三 具名 Kind 与载荷七项 · 规则一 病名不给 11 双判据 · 规则十一 与 10 的共享面收窄 · AC-11-22 写者独占)
**Requirement**: TR-prescription-001(病名不给 11:零 diagnosis/disease_id/tier_named 引用,零 indications 匹配逻辑)· TR-prescription-003(载荷七项齐备,与 10 同形)· TR-prescription-013(与 10 载荷/流语义唯一;零第二份 `SkillMul`/`ResultMul`/`JudgeResult`)· TR-prescription-015(跨平台重放逐位,输入集不含技能等级)· TR-prescription-018(写者独占 + 7a 序列化白名单两支)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009 **Amendment I**(`DrugTreatmentApplied` 入病史流,写者=11;载荷字段名随 21a,承 9 的入向契约 AC-28)· ADR-024(`entities.yaml:2047` 已登记,author=11;kindgen 断言)· ADR-006(全序键 `(Tick, StreamPriority, Patient, Seq)`;`Seq` 主机按 (Tick,Patient) 发号每 tick 复位)· ADR-005(主机唯一 Append)· ADR-010(序列化白名单两支:11 的 + 10 的 `EmergencyTreatmentApplied`)
**ADR Decision Summary**: 载荷七项 = `(tick, 处置_id, 施予者, polarity, drug_potency, half_life, Seq)`;11 **恒 Applied**(无手部门槛 ⇒ 无 result_mul 分支,恒 1.0 名义路径**不入载荷链**);ResultMul 只共享「取值口径」不共享实现;与 10 除 `method`/`cause`(仅 10 侧)外形状完全一致(AC-10-06b 成对)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(流机制 = 纯 C#;跨平台逐位子句受 ADR-012 矩阵前置)
**Engine Notes**: 写流经 `IEventSink.Append` 白名单路由(kindgen 产物);P0 单机 = 主机路径,in-process fake pipe 覆盖客户端分支。

**Control Manifest Rules (this layer)**:
- Required: 构造点仅存在于 11 程序集(写者独占);载荷字段类型全整数域(Fix=raw long);`Seq` 由主机填充
- Forbidden: 11 的输入/载荷出现 `disease_id`/`tier_named`/病名字段;11 定义 `SkillMul`/`ResultMul`/`JudgeResult`(AC-11-10 程序集引用集断言);客户端自行 Append(权威在主机)
- Guardrail: 判「零病名」用**双判据**(反射签名 + 行为等值),不是 grep —— grep 只作辅助报警

---

## Acceptance Criteria

*From GDD `design/gdd/prescription-and-medication.md`, scoped to this story:*

- [ ] **AC-11-01**[A] BLOCKING:零病名双判据 —— ① 反射断言 11 的公开签名(输入/输出类型)不含 `DiagnosisResult`/病种 id/`tier_named`;② **行为断言**:体征相同、病种不同的两病人,同药同剂 ⇒ 产出的 `DrugTreatmentApplied` 载荷**逐字段相等**(8↔11 零数据流的端到端证明,承支柱一守门;对偶 = diagnosis story 001 的 AC-8-4)
- [ ] **AC-11-03**[A] BLOCKING:载荷七项齐备 —— `polarity`/`drug_potency`/`half_life`/`处置_id`/`施予者`/`tick`/`Seq`(与 10 的 AC-10-06 **同项数**;`Seq` 主机填充),缺任一项写入期被拒(联动 9 的 AC-28 写入期拒收)
- [ ] **AC-11-22**[A] BLOCKING:**写者独占** —— `DrugTreatmentApplied` 构造点仅在 11 程序集(IL/metadata 扫描唯一 `newobj` 站点集 ⊆ 11;与 AC-11-10 算法独占并列);7a 序列化白名单两支齐(本 Kind + 10 的 `EmergencyTreatmentApplied`,TR-prescription-018)
- [ ] **AC-11-10**[A] BLOCKING:算法独占 —— 11 不定义 `SkillMul`/`ResultMul`/`JudgeResult`(程序集引用集断言 + 双路径对拍测试:同 agg 形状下 11 恒 Applied 路径与 10 的 Judge 路径**不共享代码**,仅共享「恒 1.0 名义值」的口径声明)
- [ ] **AC-10-06b 成对**[A]:11/10 载荷定义构建期交叉校验 —— 字段名/类型/序数完全一致,除 `method`/`cause`(仅 10);漂移 ⇒ 构建失败(载荷漂移会静默进 9 的和式)
- [ ] **恒 Applied 无分支**[L]:任何输入(含零技艺玩家、极小剂量)⇒ 不产生 `AppliedWeak`/`Missed` 概念;不存在手部门槛代码路径(10 vs 11 对照表)
- [ ] **AC-11-15**[I] 重放半边:同 `(WorldSeed, 药, 剂, 实例集, 玩家)` 跨进程重放 ⇒ 处置事件逐位相同;**输入集刻意不含技能等级**(熟练度只经省料/解锁出口,不改载荷,story 004 对偶);三格矩阵子句 NOT-RUN(BLOCKED-BY-ADR-012,禁借绿)
- [ ] **主机权威**[A]:客户端上下文 ⇒ 零 `Append`(上行交主机路径);与 emergency story 004 的落流口同构,但 11 **无判定**(意图即效果,规则五:有用与否归 9 的离牌门)

---

## Implementation Notes

*Derived from 规则三/十一 + ADR-009 AmI:*

1. 载荷类型 = 具名 struct(阶段 2 由 kindgen/registry 约束字段 ∈ 整数域);`polarity` 值直接取 prescription 表(story 001 cooked),不回查 9。
2. 「零匹配逻辑」的静态形态:11 代码中不存在遍历 `indications[]`/`contraindications[]` 的循环(TR-prescription-001);IL 扫 11 ⇒ 零对 9 注册表 indications 字段的读符号。
3. 施予者 id = 玩家 id(`IIdAuthority` 机制 A 适用面,ADR-006 AmB 二十六批扩大适用);`处置_id` = action_id 序数。
4. 行为等值夹具(AC-11-01②):构造两病人 fixture 走 disease story 004 的空流基线,断言载荷字节相等。
5. 与 10 的成对校验放构建期(阶段 2 规则,复用 disease story 003 载体),不在运行期。

## Out of Scope

- [Story 004]: Prescribe 流程(何时构造本事件)
- 9 的离牌门(有用与否判定,disease epic)
- 上行 pipe 的实现(45 epic;本 story 依赖抽象)
- 存档序列化本体(7a persistence epic;本 story 只保证 Kind/载荷形状合规)

## QA Test Cases

*Written at story creation(lean mode).*

- **双判据**: ① 反射:11 公开面类型集 ∌ 诊断类型;② 两病人夹具载荷逐字节等(AC-11-01)。
- **缺字段拒收**: 七项任一置缺 ⇒ Append 被拒(AC-11-03 联动写入期校验)。
- **独占扫描**: 全工程 IL 扫构造点 ⇒ 集合 ⊆ 11;影子程序集注入一次构造 ⇒ 红(AC-11-22)。
- **漂移红**: 把 10 的字段序对调 ⇒ 交叉校验 throw;复原绿(AC-10-06b 成对)。
- **无手成分支**: 极小/零技艺夹具 ⇒ 仍恰一条事件,无弱档(恒 Applied)。
- **客户端零写**: fake 客户端上下文跑 Prescribe 意图 ⇒ Append 计数 0(上行计数 1)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs` — must exist and pass(AC-11-15 矩阵子句 NOT-RUN 显式登记)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(表/polarity)、Story 002(`drug_potency`/`half_life`)、disease-simulation epic story 002(流/Seq/白名单路由)+ story 003(kindgen)、emergency-procedures epic story 004(载荷成对校验的另一半)、skill-system epic(玩家 id/QueryLevel 缺席载荷的接口面)
- Unlocks: Story 004(流程调用构造)、disease epic story 004(9 消费载荷)、case-system epic(处置证据窗口)、7a persistence epic(序列化白名单)

## Completion Notes
