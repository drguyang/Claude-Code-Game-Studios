# Story 009: Craft 事件载荷与全序键

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-012(ActualConsumed 仅派生自 Craft 事件)◆ · TR-itemdb-014(Craft 事件的载荷形状)· TR-itemdb-021(D-21-28:Craft 事件的全序键)
◆ = `status: no-adr-by-design`,归属件 = GDD 自身;永不因补 ADR 转 ✅。
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 世界状态事件化边界 Amendment J · ADR-024(次): 三流 Kind 单一登记真源 · ADR-024/008(全序键侧): 病例事件流跨流全序
**ADR Decision Summary**: ADR-009 Amendment J 追加病史流 Kind `Craft`(实际消耗量只派生自 Craft 事件,不回写配方静态数据);ADR-024 规定 Kind 的 `stream`/`author`/`payload_schema` 真源 = `entities.yaml`,禁从散文解析,载荷字段类型只允许整数域;ADR-008 定跨流全序键 `(Tick, StreamPriority, Patient, Seq)`,Patient = `PatientId.None = -1` 哨兵(不污染高水位,ADR-007)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-009 MEDIUM(表现态掉落锚点改整数格后由 HIGH 降级;ADR-015 落盘);ADR-008 HIGH 但刻意不用 post-cutoff API;ADR-024 LOW(纯数据边界)。

**Control Manifest Rules (this layer)**:
- Required: 病史事件流是唯一真源(ADR-005);`ActualConsumed` 仅在 Craft 事件载荷中,配方静态数据与 ItemInstance 快照均不得含该字段;`IEventSink.Append` 按 `Kind → StreamId` 纯函数白名单路由,列表外构建期拒绝;Patient 哨兵 = `PatientId.None = -1`
- Forbidden: 把 `ActualConsumed` 回写配方文件或存进 ItemInstance 快照;从散文/文档解析 Kind 表(ADR-024 §Amendment 追加通道已退役);客户端本地铸造 `output_instance_ids`(TR-itemdb-020 / D-21-27)
- Guardrail: `SimEvent.Kind` 扩容必须同时改 `design/registry/entities.yaml` 并重跑 `tools/kindgen/` 生成 `StreamRouting.g.cs` + 断言 A1–A5(ADR-024 §⑤)

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-52**: 一次含 EFF < EFF_MAX 的结算,经 7a 持久化往返 ⇒ 配方数据文件与物件实例里只有基数 inputs[].qty;ActualConsumed 只出现在该次 Craft 的历史事件载荷中(由 EFF 可再推出)—— 实耗是运行期派生量 `Ceil(Base/EFF)`(D-21-15)

**共同断言口径**(QA spec 已细化五子条件):①配方 cooked 中 `inputs[].qty` 仍为基数,无 `ActualConsumed` 字段;②ItemInstance 快照无 `ActualConsumed` 字段;③Craft 事件载荷含逐项 `ActualConsumed`,与 `Ceil(基数/当次 EFF)` 重算逐位一致(回放由「基数 + 当时 EFF」重演,不回读冻结数);④载荷字段集 = `entities.yaml` Craft `payload_schema` 真源(`actor_id` / `output_instance_ids[]` / `tool_cell` / `ActualConsumed`,ADR-024 §①);⑤事件全序键 `(Tick, StreamPriority, Patient, Seq)` 可排全序,`Patient = PatientId.None`,`actor_id` 位由载荷吸收(ADR-008/ADR-007 哨兵口径)。

---

## Implementation Notes

*Derived from ADR-009 Amendment J (primary) / ADR-024 (Kind 真源) / ADR-008 (全序键):*

- ADR-009 Amendment I 的三支 Kind(`EmergencyAttempt` / `EmergencyTreatmentApplied` / `DrugTreatmentApplied`)之后,世界流/病史流的追加必须走 `entities.yaml` + kindgen,不再走 Amendment 追加通道(ADR-024 §Amendment 追加通道退役)—— `Craft` 若尚未入 registry,以本故事落地,不新写 Amendment
- **`ActualConsumed` 只在事件流**,配方与快照是派生侧/只读侧 —— 三态分类(ADR-009 §一 Q1)下的「模拟态」:进流为真源,不折叠(承 ADR-008 处置事件为证同型:实耗不采信玩家自报)
- 载荷字段类型只允许整数域 —— ADR-024 §①;`ActualConsumed` 是 `int` 计数(D-21-17 weight 同族),不是 `Fix`
- 跨流全序键 `(Tick, StreamPriority, Patient, Seq)` —— ADR-008 §跨流全序;世界流/病史流的 `Patient` 哨兵 = `PatientId.None = -1`(ADR-007 §④),不进 `max(patient_id)` 高水位
- 有界性:每次 Craft 结算 ≤ 载荷字段数固定条 —— 承 ADR-009 §六 世界流有界性论证的同型(与帧率无关,事件由结算触发)
- `output_instance_ids[]` 由**主机**于点火 tick 铸造(ADR-005 主机唯一 `Append` + TR-itemdb-020 / D-21-27);客户端不铸造
- 执法体 = 构建期断言(`entities.yaml` 缺 `Craft` 条目 / 载荷字段不在 `payload_schema` 白名单 ⇒ kindgen A1–A5 或路由断言红)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: F1/F2 求解本体(本故事只消费求解输出的 `ActualConsumed`)
- Story 010: ItemInstance 编码与容器守恒(AC-31/34/35/53/58/63)—— 本故事 AC-52 的「快照无 ActualConsumed」是负向断言,编码本身归 010
- Story 008: ConfigVersion / Addressables 预载
- Story 011: 跨平台哈希与 IL2CPP 对拍

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-52**: 一次含 EFF < EFF_MAX 的结算,经 7a 持久化往返 ⇒ 配方数据文件与物件实例里只有基数 inputs[].qty;ActualConsumed 只出现在该次 Craft 的历史事件载荷中(由 EFF 可再推出)—— 实耗是运行期派生量 `Ceil(Base/EFF)`(D-21-15)。
  - Given: 配方 `inputs[].qty` 为基数;EFF ∈ [EFF_MIN, EFF_MAX) 即 EFF < EFF_MAX(实耗 > 基数);结算产生 `ActualConsumed_j = Ceil(inputs_j.qty / EFF)`。
  - When: 触发 Craft → 7a 存档 → 读档 → 读取该 Craft 事件。
  - Then: ① 配方 cooked 中 `inputs[].qty` 仍为基数,无 `ActualConsumed` 字段;② ItemInstance 快照无 `ActualConsumed` 字段;③ Craft 事件载荷含逐项 `ActualConsumed`,且与 `Ceil(基数/当次 EFF)` 重算逐位一致(回放由「基数 + 当时 EFF」重演,不回读冻结数);④ 载荷字段集 = `entities.yaml` Craft `payload_schema` 真源(`actor_id` / `output_instance_ids[]` / `tool_cell` / `ActualConsumed`,ADR-024 §① 禁从散文解析);⑤ 事件全序键 `(Tick, StreamPriority, Patient, Seq)` 可排全序,`Patient = PatientId.None`,`actor_id` 位由载荷吸收(ADR-008/注册头注 [甲] 裁定)。
  - Edge cases: EFF = EFF_MAX ⇒ 实耗 = 基数取等(仍只在事件载荷);多条 inputs 逐项对位(Amendment J:ActualConsumed 逐实例对位);`output_instance_ids[]` 由主机于点火 tick 铸造(客户端不铸造,TR-020);存档往返后重放同 Tick 段事件 ⇒ 输出逐位一致;静态数据里出现 `ActualConsumed` ⇒ 装配期断言失败(Edge Cases 同族)。
  - Negative fixture: AC 表未命名(装配断言,代码构造;不擅造文件名)。
  - Suggested test path: `tests/integration/item_database/craft_event_payload_test.cs`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/item_database/craft_event_payload_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(求解器输出 ActualConsumed),Story 010(ItemInstance 编码,验证快照无该字段)
- Unlocks: Story 011(黄金夹具覆盖事件载荷),Story 008(ConfigVersion 联动存档头)
