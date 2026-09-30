# Story 004: F2 密度预算与 DeferredThreatSlot

> **Epic**: 随机事件导演
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/random-events.md`(F2 密度预算 · DeferredThreatSlot 唯一例外 · AC-52-25/26/27/28/29/30/47 · DC-1 的 `ThreatDeferred`/`ThreatDeferralCleared` Kind)
**Requirement**: TR-randomevents-014(冷却去重窗口 —— gap,登记不立件) · TR-randomevents-016(事件流体积有界性 —— gap,承 ADR-009 §六) · TR-randomevents-027(死亡复活与事件窗 —— gap) · TR-randomevents-023(可感知门槛 —— gap,预算与可感知性交互在此登记)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性 · ADR-007: 掷骰状态可重构 · ADR-009: 世界流有界性
**ADR Decision Summary**: `EventBudgetPerDay = clamp(BASE + ΣMod, 0, BUDGET_MAX)`(全整数);**超限直接弃置,无队列** —— 唯一例外 = `DeferredThreatSlot`:**威胁档 ∧ 在医馆 ∧ 同日** 的事件不静默丢,而是存入**有界槽**(`DEFER_MAX`),延迟到同日后续窗口重抽资格;**跨日作废,不结转**;槽内容**持久在流**(`ThreatDeferred {slot_index, event_key}` / `ThreatDeferralCleared {slot_index, event_key}`),换主机可重构(Story 002 的 AC-52-46 已覆盖其重建)。**联机不抬预算**(队规模不进 BASE/ΣMod);**dwell time(驻留时长)永不作为输入**(AC-52-28 —— 反「玩家故意磨时间」诱导)。冷却去重:同 key 在 `COOLDOWN_TICKS` 内不再被抽(W_i ×0 路,非删除)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 整数预算算术 + 流 Kind 路由,门 A 内;无引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 预算判定发生在 F1 step④(生成前);弃置 = 不写流(超预算的选中事件**不发 `EventArrived`**,也不发 `EventRolled`?—— 否:抽取记录 `EventRolled` **仍发**,弃置只发生在降临侧 —— 见 Implementation Notes 2 的钉死口径);槽 = 有界数组语义
- Forbidden: 隐式队列 / 溢出堆积;`Time` 或 dwell 计数进 BASE;把槽内容只放导演本地(必须进流可重构)
- Guardrail: 流体积上界 = 事件率不变量(每窗 ≤ WINDOW_SIZE 条降临 + 每槽 ≤ 2 条 defer 记录);`DEFER_MAX` 与 BUDGET_MAX 同侧有界

---

## Acceptance Criteria

*From GDD `design/gdd/random-events.md`, scoped to this story:*

- [ ] `EventBudgetPerDay = clamp(BASE + ΣMod, 0, BUDGET_MAX)` 整数求值;ΣMod 的输入闭集中**无** dwell / 联机人数(AC-52-28 / 联机不抬预算)
- [ ] 超限弃置:当日预算耗尽后,后续窗口 F1 照常抽取(`EventRolled` 入流,保「掷骰可重构」)但**不降临**(`EventArrived` 零新增);弃置无队列(AC-52-26)
- [ ] DeferredThreatSlot 三条件门:威胁档 ∧ 在医馆地块 ∧ 同日 ⇒ 入槽;任一不满足 ⇒ 普通弃置;槽满(`DEFER_MAX`)⇒ 最旧按 GDD 规则处置(覆盖/丢弃口径在夹具钉死)
- [ ] **跨日作废不结转**(AC-52-27):日界 tick 上,未清槽发 `ThreatDeferralCleared`(作废)并空槽
- [ ] 槽状态从流重构:任意前缀重放 ⇒ 槽内容与在线运行态逐位一致(挂 AC-52-46 夹具族扩展)
- [ ] 冷却去重:key ∈ `COOLDOWN_TICKS` 内 ⇒ W_i ×0;冷却表从 `EventArrived` 流重构
- [ ] 流体积有界性论证用例(承 TR-016 gap 自证):跑 30 游戏日夹具,降临+defer 记录数 ≤ 上界公式;超出 = 断言失败
- [ ] 死亡复活交互(gap TR-027 自证):玩家死亡复活不产生预算补偿 / 不追发弃置事件;窗口照常

---

## Implementation Notes

*Derived from F2 / DC-1:*

1. 关键钉死口径(评审重点):**抽取与降临解耦** —— 预算检查在 F1 step④,即「已抽中但未发 `EventArrived`」。是否发 `EventRolled`:GDD DC-1 的不变量是「掷骰输入可重构」⇒ **`EventRolled` 照发**(否则重放的抽骰序列断链),弃置只省 `EventArrived`。本 story 以此口径实现并在测试夹具注释引用 DC-1 原文;若实现轮发现口径歧义,升级 ADR-007 修订而**非自裁**。
2. 槽 = 定长 `int[]`(slot_index ∈ [0, DEFER_MAX)),`ThreatDeferred/Cleared` 各带 `slot_index`(载荷已在 registry);槽是派生态 —— 现算自流,对象字段不持久。
3. 「同日」判定 = 窗口 win 归属日(`win / TICKS_PER_DAY`),纯整数。
4. dwell 禁入:ΣMod 输入表以**类型闭集**表达(新增输入 = 编译改动 + 评审),不留字符串扩展口。
5. ⚠️ **数值冻结**:BASE / BUDGET_MAX / DEFER_MAX / COOLDOWN_TICKS / ΣMod 各项值归用户数值轮;本 story 交算式与边界夹具(±1 / 恰满槽 / 日界 tick)。

---

## Out of Scope

- [Story 005]: 预告窗与避险(槽释放后的降临走 005 生命周期)
- [Story 003]: F1 内部(本 story 是其 step④ 的调用方)
- 联机队态读预算(P1b,TR-026 gap)
- 灾难题材内容(37/叙事)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 预算 clamp 与零队列
  - Given: BASE 夹具使预算 = 3;同窗 F1 出 4 条
  - Then: 降临 3、弃置 1;`EventRolled` 4 条、`EventArrived` 3 条;次日预算恢复,昨日弃置**不补发**
  - Edge cases: ΣMod 为负触底 0 ⇒ 当日零降临;>BUDGET_MAX 触顶

- **AC-2**: defer 三条件门 + 槽界
  - Given: {威胁+医馆+同日} × {机会 / 野外 / 跨日} 对照表
  - Then: 恰第一条入槽;其余普通弃置;DEFER_MAX 满时最旧处置 = 夹具钉死口径复现
  - Edge cases: 入槽事件本身等同日后续窗 ⇒ 重获降临资格(槽清 + 降临)

- **AC-3**: 跨日作废(AC-52-27)
  - Given: 日界前一 tick 入槽
  - When: 过日界
  - Then: `ThreatDeferralCleared` 发出、槽空、不结转;重放逐位同
  - Edge cases: 同 tick 日界 + 降临竞态(顺序按全序键)

- **AC-4**: 从流重构槽态(挂 002 夹具族)
  - Given: 任意流前缀对
  - When: 重放
  - Then: 槽 = 在线态;迁移续跑抽取序列一致(AC-52-46 扩样)
  - Edge cases: 槽满 + 清槽同窗交错

- **AC-5**: 冷却去重
  - Given: key X 于 t 降临
  - Then: t..t+COOLDOWN 内 X 的 W_i=0;从流重建冷却表同判
  - Edge cases: 恰在边界 tick 恢复资格

- **AC-6**: 体积上界
  - Given: 30 游戏日脚本化夹具(全窗口跑满)
  - Then: 流新增条目 ≤ 上界式;GC.Alloc = 0 子项(AC-52-39 [B][I])本 story 只交分配断言桩,profiling 归性能轮
  - Edge cases: dwell 注入(挂机 10 小时等效 tick)⇒ 预算与输入零变化(AC-52-28)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/RandomEvents/event_budget_defer_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(Kind 路由 / 重放夹具族)· Story 003(F1 step④ 接口)
- Unlocks: Story 005(defer 后的降临进预告)· Story 006(冷却表与状态机联动)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `BudgetParams` — 预算参数（BASE / BUDGET_MAX / DEFER_MAX / COOLDOWN_TICKS）
- `BudgetState` — 预算状态（派生态，从流重构）
- `EventBudget` — F2 密度预算与 DeferredThreatSlot（ComputeDailyBudget / ShouldDefer / TryConsumeBudget / ResetForNewDay）
- 预算 clamp + 零队列
- 跨日作废不结转
- 冷却去重
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- DeferredThreatSlot 为简化版（无 ThreatDeferred / ThreatDeferralCleared 流登记），完整版归 Story 005/006

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/RandomEvents/event_budget_defer_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，无 BLOCKING 问题

**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
