# Story 001: EmergencyReading 读数与直读通道契约

> **Epic**: 急救动作模块
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则一 OQ-3-5 幅度独立模拟量通道 · 规则二 直读通道不穿 42 UI 栈 · 规则三 判定全归 10)
**Requirement**: TR-emergency-001(EmergencyReading 全整数读数)· TR-emergency-002(直读通道)· TR-emergency-003(3 侧零判定零 SimEvent)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011 §二(急救动作独立直读通道,<50 ms 直读动作值,不穿过 42 UI 事件栈)+ Amendment B(客户端聚合为一条 `EmergencyAttempt` 全整数意图事件)· ADR-006(舍入 HALF_AWAY_FROM_ZERO = 3 侧义务)· ADR-025(契约落 `Sim.Contracts` 整数半 / `Gameplay.Input` 读侧)
**ADR Decision Summary**: `EmergencyReading{action, hold_ticks, edges, edge_ticks[], magnitude}` 全部 int/tick 量纲;F-10.1 幅度通道 `magnitude = ClampToDomain(round_fixed(raw_axis × AXIAL_SCALE), DZ_MAG, MAG_MAX)`,浮点**只活在 3 的手感层**;结构性约束 `AXIAL_SCALE ≥ MAG_MAX`(否则幅度域压扁)、死区 `DZ_MAG` 吃摇杆漂移;`edges` 只计 press 沿(release 不计)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(Input System 6.3 直读时序行为须实测 —— ADR-011 自评级;读数结构本体纯 C# = LOW)
**Engine Notes**: 承 input-system epic story-007-direct-read-channel(直读通道交付物)与 story-010-hot-path-zero-cost(预缓存 action 引用);本 story 是**消费侧**,不新建读路径。

**Control Manifest Rules (this layer)**:
- Required: 读数类型住跨程序集边界且字段声明类型全整数;3→10 交付契约 = 类型 + 量纲(Q16.16 计数,`0 ≤ MAG_MAX ≤ 65536`)
- Forbidden: 10 侧出现 `float`/`double` 字面量或字段;3 侧引用 `Judge`/`JudgeResult`/`SimEvent`;`raw_axis` 进流或进 sim
- Guardrail: 判据 = **反射断言声明类型,不是 grep**(AC-10-01 明文,承 ADR-020 AC-20-03 先例)

---

## Acceptance Criteria

*From GDD `design/gdd/emergency-procedures.md`, scoped to this story:*

- [ ] **AC-10-01**[A] BLOCKING:`EmergencyReading` 每字段反射断言声明类型 —— 零 `float`/`double`(含 `magnitude` / `edge_ticks[]`);`edge_ticks` 单位 = tick(int)
- [ ] **AC-10-02**[A] BLOCKING:asmdef 白名单 + IL 扫描 —— 3 侧程序集**零** `Judge` / `JudgeResult` / `SimEvent` 引用(判定全归 10)
- [ ] **AC-10-03**[A] BLOCKING:10 判定路径静态检查零浮点字面量(门 A / ADR-006;操作码级谓词 `ldc.r4/r8`、`conv.r*` 同 9 的 AC-5 机制复用)
- [ ] **F-10.1 交付契约**[A]:3 侧交出的定点化含死区与 clamp:静息漂移(fixture:`raw_axis` 微抖动 ∈ (0, `DZ_MAG`))⇒ `magnitude = 0`;满偏 ⇒ `= MAG_MAX`;`AXIAL_SCALE ≥ MAG_MAX` 为构建期结构断言
- [ ] **press 沿口径**[L]:一次「按下-松开-再按下」⇒ `edges = 2`(release 不计数);`edge_ticks[]` 存 press 时刻且单调
- [ ] **直读路径**[I]:急救读数采集不经过 42 UI 事件栈(集成断言:模态打开时仍产出 Reading;与 skeuomorphic-ui epic 联测)

---

## Implementation Notes

*Derived from ADR-011 + GDD 规则一/二:*

1. 读数聚合缓冲住 `Gameplay.Input` 侧(10 消费),类型住 `Sim.Contracts`(整数域形状);tick 相位由 `ITickProvider` 换算(20 Hz 已裁,承 ADR-005 实现期义务「步相位」)。
2. `round_fixed` 舍入实现归 3(input-system epic 已登记的侧),本 story 只做**契约验收**(夹具注入含 `.5` 中间值,断 8193 型结果)。
3. `MAG_MAX` / `DZ_MAG` / `AXIAL_SCALE` 值归用户数值轮;测试以合成参数注入,结构性断言(`≥` 关系)即判。
4. 键鼠设备无模拟量 ⇒ 该路径读数由 story 006 的回退条款定义(本 story 不实现键鼠幅度)。

## Out of Scope

- [Story 003]: Judge 三扇门(消费本读数)
- [Story 004]: EmergencyAttempt 聚合与上行(F-10.5 归 004)
- [Story 006]: 键鼠回退 `magnitude ≡ MAG_MAX`、预表现
- 手柄绑重/overrides(input-system epic story-001/003/004)

## QA Test Cases

*Written at story creation(lean mode).*

- **类型反射**: 扫 `EmergencyReading` 全字段+嵌套 ⇒ 全 ∈ {int, long, int[]};负夹具:影子类型加 `float magnitude` ⇒ 红。
- **3 侧洁净**: 对 input 程序集产物跑 Cecil/IL 断言(同 audio story-001 双层机制);负夹具合成 `typeof(JudgeResult)` 引用文本 ⇒ 红。
- **死区**: `raw_axis` 序列 = ±漂移幅(合成 fixture)⇒ `magnitude` 恒 0;越过 `DZ_MAG` ⇒ 非 0 单调。
- **沿计数**: 3 按 2 松 ⇒ `edges=3`,`edge_ticks` 三点且 `d_i>0`。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/EmergencyProcedures/reading_contract_test.cs` — must exist and pass;`L_input` 实机面 NOT-RUN(归 story 006 / OQ-10-12 原型门)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: input-system epic story-007(直读通道)/ story-010(热路径零成本)/ story-006(意图边界);disease-simulation epic story-002(tick 量纲与 `ITickProvider`)
- Unlocks: Story 003(Judge 输入)、Story 004(Aggregate)、Story 006(预表现)

## Completion Notes
