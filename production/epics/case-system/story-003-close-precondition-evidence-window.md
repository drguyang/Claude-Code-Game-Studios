# Story 003: 结案前置 —— 处置证据窗口与快照(复诊后门关闭)

> **Epic**: 病例系统
> **Status**: Complete ✅ 2026-10-06(`31e3f9b`)
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/case-system.md`(规则六 结案唯一出口 · F-37.2 已处置/可结案 + 2026-09-17 方向订正 · F-37.3 一案多病种 · EC-37-3)
**Requirement**: TR-case-016(规则六:结案为唯一出口、玩家显式、单例、不可撤销,原 gap —— 本 story 兑现机制层) · TR-case-023(可结案 = 病史流 ≥1 处置事件(窗口内)∧ 玩家勾选) · TR-case-006/007(CaseClosed.disease_set 集合 + treated 快照) · TR-case-026(重复结案幂等拒收)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-008(主): 处置证据窗口化 + 快照
**ADR Decision Summary**: 「已处置」= **处置事件为证**(非玩家自述复选框 —— 否则空手连关三例触发 Boss,与支柱四冲突,ADR-008 B-2 裁定);**2026-09-17 方向订正**:窗口 = `e.Tick ∈ [c.opened_tick, c.CloseTick]`(旧式把区间写反 ⇒ 复诊第二例凭首诊处置通过前置的后门**实际未堵**,本轮才在公式层真正关闭;错误源头 ADR-008:156 已同批订正);结案四铁律:唯一路径 = 玩家 39 显式 / 前置 = 已处置 ∧ 勾选(UI 仪式不产生独立事件)/ 单例(禁全选)/ 不可撤销 + 重复幂等拒收(判定 = 流前缀纯函数);结案时:8 读数态清除 + 判断冻结交 53(AC-8-47 落点)→ `CaseClosed` 快照 `disease_set`(可空,F-37.3 纯伤情案空集)+ `treated`;病名可空照样结(《他回来了》收尾条件);系统**永不自动结案**(规则七)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 谓词 = 跨流读 + 前缀纯函数,纯 C#。

**Control Manifest Rules (this layer)**:
- Required: 窗口方向 = `e.Tick ∈ [CaseOpened.Tick, CloseTick]`;结案写路径仅玩家意图(经 45 上行、主机 Append);单例参数校验
- Forbidden: 「全选结案」入口;自动结案计时器;勾选复选框产生独立事件;把处置内容对错判定塞进 37(AC-37-31:正确性只存在于 53 输入侧)
- Guardrail: 病人死亡/离场不影响结案可行性(状态正交);无处置事件 ⇒ 拒绝路径必须可测(AC-37-21)

---

## Acceptance Criteria

*From GDD `design/gdd/case-system.md`, scoped to this story:*

- [ ] AC-37-04:未勾「已处置」⇒ 结案拒绝(无事件)
- [ ] AC-37-21:已勾选但窗口内**无**处置事件 ⇒ 拒绝(复诊后门正向封堵:首诊处置不满足第二例窗口)
- [ ] AC-37-03:同一 case_id 第二条 CaseClosed 幂等拒收,不产生新事件
- [ ] 窗口边界:`e.Tick == CaseOpened.Tick`(立案即处置)与 `== CloseTick` 均计入(闭区间夹具)
- [ ] `CaseClosed` 载荷 = `{disease_set(集合,可空), treated}` 快照;结案后病人新发病**不追溯**改写旧案(EC-37-2 行)
- [ ] AC-37-18:病人死亡但案未结 ⇒ 病例仍「开」、可正常结案(零自动转移边);病名为空可结案(空是合法终态)
- [ ] 结案副作用序列:读数清除信号(→8)+ 判断冻结(→53)+ CaseClosed 入病例流;37 自身不产生后果事件(后果归 53 订阅)

---

## Implementation Notes

*Derived from ADR-008 Implementation Guidelines:*

1. `已处置(c) := ∃ e ∈ 病史流: e.Patient=c.patient_id ∧ e.Kind ∈ 处置集 ∧ e.Tick ∈ [CaseOpened.Tick, CloseTick]`;处置集 = 10/11/25 写的 Kind 白名单(`EmergencyTreatmentApplied`/`DrugTreatmentApplied`/`InjuryOnset` 是否算处置 → **以 ADR-008 §四 原文与 53 GDD 为准,实现前核对**,不自行扩集)。
2. `可结案(c) := state=开 ∧ 已处置(c) ∧ 勾选` —— 勾选是意图载荷字段,非事件(仪式语义)。
3. 幂等判据 = 「该案已有 CaseClosed」前缀检查,非锁非标志位。
4. `disease_set` 快照口径 = 病程 overlap(结案时算 F-37.3 的 CaseDiseases),存进载荷后流即真相;重放**永不**跨流再查(treated 同理)。
5. 单例 = 意图参数只容一个 case_id;批量入口在 37 侧类型面不存在(非运行时拒绝)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 立案/判断记录(本 story 的开案前提)
- [Story 004]: 结案触发的同源评估(CaseClosed 入流后由 004 消费)
- [Story 005]: 「处置对错不进 37」的类型面断言批(AC-37-31/32 归守密 story)
- 系统 39/42:结案页勾选 UI;53:后果订阅

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(方向订正回归)**: 复诊后门必须关
  - Given: 病人 p:首诊处置@t₁,首诊结案@t₂,复诊立案@t₃(t₁<t₂<t₃),复诊无新处置
  - When: 复诊勾选结案
  - Then: **拒绝**(t₁ ∉ [t₃, now])—— 旧式会通过的回归哨兵
  - Edge cases: 复诊窗口内补一条处置 → 放行
- **AC-2(空名结案)**: S-8.3 合法终态
  - Given: 病名栏空 + 有处置事件 + 勾选
  - When: 结案
  - Then: 成功;disease_set 按病程快照(可非空 —— 病名 ≠ 病种)
  - Edge cases: 纯伤情案(无病种)→ disease_set 空集,结案合法(不入检测归 story-004)
- **AC-3(幂等)**: 连点两次结案
  - Given: 案 c 已结
  - When: 再发结案意图
  - Then: 流零新增;副作用不重复(读数清除/冻结信号不再发)
  - Edge cases: 同 tick 双意图 → 恰一条 CaseClosed

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/CaseSystem/case_close_precondition_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(载荷)、002(开案);9/10/11/25 处置事件在病史流(既有 Kind,任一在场即可测;缺则 fixture 直写流)
- Unlocks: Story 004(CaseClosed 是同源检测唯一输入)、Story 006(结案态重放)

---

## Completion Notes

*(留空 — story 关闭时回填)*
