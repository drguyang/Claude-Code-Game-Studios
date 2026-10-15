# Story 001: 入向闭集与跨流聚合

> **Epic**: 医疗后果与责任
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-16

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

- [x] `Inputs53` 结构恰含五路(CaseClosed / PatternRecognized / JudgmentRecorded / CareApplied / 病程投影),类型级闭集;第六路存在 = AC-53-05 反射断言红
- [x] **AC-53-04 置换不变**:同一批事件任意打乱到达序 ⇒ 聚合结果逐位一致(承接 AC-37-28 口径);排序键全序、无并列残留
- [x] 哨兵先行用例:同 tick 的 `PatternRecognized(Patient=-1)` 正确排在病人事件前,且其语义(模式识别加速)在聚合后可被 Story 002 读出;不判为「未结案」
- [x] **AC-53-05**:53 全部可序列化/内部类型反射扫描,无改写/修订计数类字段(零改写计数 = 承 AC-37-20)
- [x] 病程投影经只读 DTO 边界(无 9 内部类型 import;asmdef 引用集 = BCL + 契约件)
- [x] 空输入 / 单事件 / 纯哨兵流 ⇒ 聚合退化为空结果,不崩(边界夹具)

> ✅ **6 条 AC 于 2026-10-16 经 `/story-done` 逐条核账后勾选**(每条均有具名专测,映射见 §Test Evidence;
> 承 S11-4 口径「打勾归 /story-done」,本行为关闭记录)。

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
**Actual evidence**: ✅ 同上路径 —— **20 条测,全 Passed**(2026-10-15 · S11-5)
**Status**: [x] Created and passing

> **✅ 实现记录(2026-10-15 · S11-5)** —— 实现件 = `unity/Assets/Sim/Consequences/ConsequenceInputAggregator.cs`
> (门 A 内零引擎 API;`Sim` 引用集仍恰 = `["Sim.Contracts"]`,**无 asmdef 改动**)。
> 判据:filtered EditMode `20/20` · 全量零回归 `3366/3320/0/46`(较 S11-4 后 **+20**,恰本轮新增)·
> **反证 ×2**(① 停用 `ordered.Sort` ⇒ 恰红 2 条**排序专向**探针;② 注入第六路字段 ⇒ 恰红 1 条并**逐字点名**)。
> 证据原件:`production/qa/evidence/s11-5-medcons-story-001-aggregation-2026-10-15.md`。
>
> ⚠️ **基线订正(S12-0 · 2026-10-15 规划期补)** —— 上行 `3366/3320/0/46` 是本 story 实现时点
> (S11-5)的零回归基线,该判据本身有效;**但该值不是 sprint-11 的终值** —— 当日批 6
> (`a5ef7c5` · 13:50)把 EditMode 终值推到 **`3388/3342/0/46`**。
> 后续引用「本 story 之后的全量态」时须用终值,**不得**沿用 `3366/3320/0/46`。
> 本 story 的判据口径**不变**:20/20 + 当时零回归,二者均为当刻真值。
>
> ⚠️ **命名口径(本卡内异名,就地登记,原文不追改)**:本卡 **AC 第一条逐字**写「`Inputs53` 结构恰含五路」,
> 而 **§Implementation Notes 1 同一物写作 `PerCaseInputs`** —— 两者是同一结构的**异名**。
> 实现**取 AC 名** `Inputs53`(**AC 是判据**),**不复名**(避免第二真源)。
>
> ⚠️ **两处张力未在本 story 裁定(如实登记,归 53 的 GDD 轮)**:① **处置 Kind 集** ——
> §Implementation Notes 4 提 10/11 两支(`EmergencyAttempt` / `DrugTreatmentApplied`),
> 而 GDD 规则五 `:165` 只列 `CareApplied` ⇒ 实现取 `careKinds` **显式参数**,由装配层决定;
> ② **处置窗口下界** —— 取 `[CaseOpened, Close]` 窗口须加第六路,与 AC-1「恰五路」**直接冲突**
> ⇒ 实现取 `Tick ≤ 结案 tick`(无下界)。
>
> ⚠️ **6 条 AC 复选框未勾** —— 判据已全绿(每条均有专测),但**关闭归 `/story-done`**(承 S11-4 口径)。
> ✅ **已于 2026-10-16 由 `/story-done` 逐条核账后勾选**(见 §Acceptance Criteria 关闭记录)。
> ⚠️ **本件不产出 `ConsequenceResolved`** —— 本 story 只做**聚合器(输入侧)**;
> `WriterExistenceGate.cs:117` 的「53 未实现」注记**不应因本件改动**(产出归 story-003)。

> **证伪轮补账(S12-4 · 2026-10-16 `/story-done` · 五类形态自此全数反证)** ——
> S11-5 原件 §四 已反证 ①定序 / ③定义域收窄;本轮对实现件(接线轮 ADR-031 V-8 ② 后形态,
> md5 `a702ee32bba07c6a4f6b00b258dcdcf5`)补跑 ②④⑤ 三类,**改 → 跑 → 还原同一次调用内完成,
> 三跑三还原 md5 一致、残留 0**:
>
> | 命中形态 | 反证:停用什么 ⇒ 哪条转红 | 轮次 |
> |---|---|---|
> | ① 定序 | 停用 `ordered.Sort(EventOrder.Compare)` ⇒ `permutationInvariance` + `sameTick…Seq` 2 红 | S11-5 |
> | ② 共存/互斥(跨病人隔离) | 停用 `e.Patient != close.Patient continue` ⇒ `careProjection_excludesAfterClose` + `keepsOnlyLastJudgment` 2 红 | **本轮 A**(ec=2) |
> | ③ 定义域收窄 | 注入 `Inputs53.SixthLaneProbe` ⇒ `hasExactlyFiveLanes` 1 红(逐字点名) | S11-5 |
> | ④ 一次性/覆盖 | 停用 P「最后一笔」覆盖(`else judgments.Clear()`)⇒ `keepsOnlyLastJudgmentBeforeClose` 1 红 | **本轮 C**(ec=2) |
> | ⑤ 阈值边界 | 窗边界 `>` 改 `>=` ⇒ `sameTick…Seq` + `sentinelNotFolded` + `includesEventsAtExactlyCloseTick` 3 红 | **本轮 B**(ec=2) |
>
> 基线:filtered `MedConsequences` **61/61 绿**(aggregation 20 + judge 41,ec=0)。
> 每轮均含「旧实现下该夹具会怎样」栏:s11-5 原件 §四「反证失败的消息」段(消息精确指向变异点,非泛红)。

---

## Dependencies

- Depends on: 三流只读快照(既有)· registry Kind 齐名(既有,ADR-024)
- Unlocks: Story 002(结算消费聚合)· Story 003(延迟重放的输入侧)

---

## Completion Notes
**Completed**: 2026-10-16
**Criteria**: 6/6 passing(无 deferred;AC↔测映射见 §Test Evidence,0 UNTESTED)
**Verdict**: **COMPLETE WITH NOTES**(S12-4 Falsifiability Gate = **PASS** —— 五类命中形态全数反证转红)
**Test Evidence**: Logic —— `unity/Assets/Tests/EditMode/MedConsequences/medcons_aggregation_test.cs`
(20 条全 Passed;filtered 61/61;全量零回归基线承 sprint-12 终轮 `3462 / 3416 / 0 / 46`,commit `85f5469`)
**Code Review**: Complete(S11-5 实现报告原件 + 2026-10-16 `/story-done` 证伪补账;
lean 模式下经用户裁定「已有实现证据,视为已评审」)

**Deviations(均为 advisory,已处理)**:
- **ADVISORY-1(Manifest 版本)**:卡面原嵌 `2026-10-02` vs 现值 `2026-10-15`。
  核实 `control-manifest.md` **无 53 专节**(grep `53` / `ConsequenceResolved` / `医疗后果` 仅命中行号巧合)
  ⇒ 期间变更**不含影响本 story 的 Required/Forbidden 条目** ⇒ 已 bump `Manifest Version: 2026-10-15`,**判据未改**。
- **ADVISORY-2(措辞滞后于实现)✅ 已订正**:2026-10-15 接线轮(承 **ADR-031 §V-8 ②「退」**、用户裁定
  「6 处全做,含改 `Inputs53`」)已把 P / C 路元素从 `SimEvent` 改为**解码行** `JudgmentRow53` / `CareRow53`
  (经 `Sim.Contracts` 的 `IPayloadDecoder` 解出载荷内容)⇒ 两处**同期文本未同步**:
  ① `s11-5-medcons-story-001-aggregation-2026-10-15.md` §3.1「零载荷解码」—— 已加订正注(**原文不删**,
     其为 S11-5 时点真值);
  ② `medcons_aggregation_test.cs:544` 断言**消息**「53 不读载荷内容(P/C 的内容归 story-002)」—— 已改为
     「**不经 `Sim.Codec` 读载荷**(解码经 `Sim.Contracts` 的 `IPayloadDecoder` 抽象点,ADR-031 §①)」;
     ⚠️ **断言逻辑一字未动**(判据仍是 `Sim` 引用集不含 `Sim.*` 除 `Sim.Contracts`),仅措辞与现实现口径对齐。
  **性质 = 自述滞后,非实现错误**:测试**通过**(引用集约束仍成立),误导的只是注释文本。

**两处张力(本 story 不裁定,具名登记归 53 的 GDD 轮)**:
① **处置 Kind 集** —— 实现取 `careKinds` **显式参数**(§Impl Note 4 提 10/11 两支 vs GDD 规则五 `:165` 只列
`CareApplied`),测试只钉「参数被尊重」(窄/宽/null 三态),**不**钉该集内容;
② **处置窗口下界** —— 实现取 `Tick ≤ 结案 tick`(**无下界**),因取下界须加第六路 `CaseOpened`,
与 AC-1「恰五路」直接冲突。

**命名口径(就地登记,原文不追改)**:AC-1 逐字 `Inputs53` vs §Impl Note 1 `PerCaseInputs` = 同一结构异名;
实现**取 AC 名**(AC 是判据),不复名(避免第二真源)。

**扫描面载体(承 `review-workflow` 义务 4)**:AC-53-05 反射扫描面 =
`DaYiJingCheng.Sim.Consequences` **命名空间**(53 无独立 asmdef);**刻意不扫全 `Sim`**
—— 那会把 9 / 25 / 37 的类型纳入 53 判据 = 越权。

**边界登记**:`WriterExistenceGate.cs:124`(原 `:117`)的 `ConsequenceResolved`「53 未实现」待办
**本件不动**(本 story 只出聚合器输入侧,不产事件);其解除归 **story-003**(S13-2)。
