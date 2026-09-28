# Story 001: 入向闭集与跨流聚合

> **Epic**: 医疗后果与责任
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/medical-consequences.md`(规则四 聚合按全序 · 规则五 入向闭集 · AC-53-04 / AC-53-05 · 承接 AC-37-20 / AC-37-28)
**Requirement**: TR-medcons-004(跨流聚合按全序键非到达序,covered ADR-008) · TR-medcons-005(入向闭集反渗守卫 —— gap,P1a 主照登,P0 载体 = GDD AC-53-05)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 病例事件流 · ADR-005: 确定性模拟 · ADR-016 §一: 三源不变量
**ADR Decision Summary**: 53 的全部输入 = **闭集** `{CaseClosed, PatternRecognized, JudgmentRecorded, CareApplied, 9 的病程投影}` —— 事件**按到达顺序**聚合是静默不可重建通道(到达序 ≠ 因果序):必须按跨流全序键 `(Tick, StreamPriority, Patient, Seq)` 聚合。**前向悬垂实测形**:哨兵 `PatternRecognized` 的 `Patient = -1` 排序先于同 tick 病人事件 —— 聚合器对哨兵先行必须正确(AC-53-04 置换不变 = 逐位一致)。53 永不改写计数(AC-53-05 递归反射:无 `rewrite_count` / `revision_count` / `edit_history_size` 类字段 —— 反渗守卫的**形状侧**);病程投影(D)= 边界层只读 DTO,53 不 import 9 的内部。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯排序 + 过滤,门 A 内零引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 输入类型以具名参数/结构表达(忘传 = 编译不过);聚合 = 流的纯函数(置换输入 ⇒ 置换后同结果)
- Forbidden: 读流之外的第五输入(时钟 / 表现态 / `UnityEngine.*`);把「改写史计数」类聚合量渗进 53 载荷
- Guardrail: 闭集扩张须改本 story 的输入结构 + 评审(字符串/反射扩展口零留)

---

## Acceptance Criteria

*From GDD `design/gdd/medical-consequences.md`, scoped to this story:*

- [ ] `Inputs53` 结构恰含五路(CaseClosed / PatternRecognized / JudgmentRecorded / CareApplied / 病程投影),类型级闭集;第六路存在 = AC-53-05 反射断言红
- [ ] **AC-53-04 置换不变**:同一批事件任意打乱到达序 ⇒ 聚合结果逐位一致(承接 AC-37-28 口径);排序键全序、无并列残留
- [ ] 哨兵先行用例:同 tick 的 `PatternRecognized(Patient=-1)` 正确排在病人事件前,且其语义(模式识别加速)在聚合后可被 Story 002 读出;不判为「未结案」
- [ ] **AC-53-05**:53 全部可序列化/内部类型反射扫描,无改写/修订计数类字段(零改写计数 = 承 AC-37-20)
- [ ] 病程投影经只读 DTO 边界(无 9 内部类型 import;asmdef 引用集 = BCL + 契约件)
- [ ] 空输入 / 单事件 / 纯哨兵流 ⇒ 聚合退化为空结果,不崩(边界夹具)

---

## Implementation Notes

*Derived from 规则四/五:*

1. 聚合器落 `Sim`(门 A):`Aggregate(streamSnapshot) → PerCaseInputs`(每结案病例一组 P/C/D/U-ready 键)。P 的「@结案时刻」投影在此完成(取该病例最后一笔判断记录,作者轴保留 —— 与 39 的 J(c,p) 同折叠但**各自独立实现**,53 不依赖 39)。
2. 排序复用全序比较器(与 37/39 同族三元组 int64 比较;禁减法比较防溢出)。
3. 病程投影 DTO 形状若 9 侧未就位:以接口占位 + 夹具实现,登记「投影口正式化归 9 修订轮」—— **不私读 9 的私有态**。
4. CareApplied(处置记录)来自病史流;与 10/11 写入的 `EmergencyAttempt`/`DrugTreatmentApplied` 的映射表按 GDD 规则五逐字实现。
5. ⚠️ **数值冻结**:本 story 无旋钮(纯形状与顺序)。

---

## Out of Scope

- [Story 002]: Judge 求值(本 story 只出聚合后的输入组)
- [Story 003]: 延迟发出(消费 002 结果)
- 试药史阈值判定(形状归 002,值归数值轮;OQ-53-9 已裁形状)
- TR-medcons-005 的 P1a 主照(反渗守卫的村落侧延伸)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 置换不变(AC-53-04)
  - Given: 50 事件夹具(四流混合 + 哨兵),固定伪随机置换 × 20
  - When: 各置换聚合
  - Then: 输出逐位相同
  - Edge cases: 同 (Tick, StreamPriority, Patient) 不同 Seq;同 Seq 不可能(registry 保证)—— 负夹具验证不崩

- **AC-2**: 哨兵先行
  - Given: 同 tick PatternRecognized(-1)+ JudgmentRecorded(pid=3)
  - Then: 排序 -1 先;聚合不把它并入 pid 3 的 P 折叠
  - Edge cases: 纯哨兵流 ⇒ 空病例结果集

- **AC-3**: 闭集反射守卫(AC-53-05)
  - Given: 53 程序集全部类型
  - When: 字段名/类型黑名单(rewrite_count/revision_count/edit_history)+ 输入结构成员数断言 = 5
  - Then: 零命中;注入 `int revision_count` 负夹具必红
  - Edge cases: 嵌套集合泛型参数也被扫

- **AC-4**: 边界退化
  - Given: 空流 / 单 CaseClosed / 只有病程投影
  - Then: 空结果 / 缺 P·C 按 GDD 缺省语义 / 空;各不抛
  - Edge cases: 「结案无判断」= 合法输入(P = 空投影,交 002 判 missed)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/MedConsequences/medcons_aggregation_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: 三流只读快照(既有)· registry Kind 齐名(既有,ADR-024)
- Unlocks: Story 002(结算消费聚合)· Story 003(延迟重放的输入侧)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
