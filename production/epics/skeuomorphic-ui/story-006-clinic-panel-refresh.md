# Story 006: 医馆面板渲染 + 刷新延迟契约

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: 无专属 TR(24 侧 AC-24-09 迁入 42 的刷新延迟契约)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 42 只读 DTO(IVitalsQuery → VitalsDto)不持状态;触发/状态归各系统;刷新延迟 = 下一帧

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH;但本 story 只涉及 DTO 读取 + 刷新时机,不涉及焦点桥 / 自定义材质等高风险 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 42 只读 DTO(IVitalsQuery → VitalsDto);零模拟写;42 内不存在任何写三流 / 写存档 / 写 assets/data/ 的代码路径
- Forbidden: 42 持有 DTO 副本(缓存 = 第二份真相);42 持有设置值;42 直连 9 的模拟
- Guardrail: Structure* 事件 Append 后**下一帧**刷新为新的乘子/情境摘要

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F5**: Structure* 事件 Append 后**下一帧**刷新为新的乘子/情境摘要(不是同一帧内立即刷新)
- [ ] **AC-42-D2**: 42 的代码中不存在写三流 / 写存档 / 写 assets/data/ 的调用(禁 IEventSink.Append 等写入口)
- [ ] **AC-42-D3**: 42 的类型树不持有 DTO 副本、不持有设置值

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **刷新延迟契约**: Structure* 事件 Append 后,42 在**下一帧**刷新显示(不是同一帧内立即刷新)
2. **DTO 读取**: 经 IVitalsQuery 读取 VitalsDto;不直连 9 的模拟
3. **无缓存**: 不持有 DTO 副本;每帧从 IVitalsQuery 读取最新值
4. **符号级禁令**: AC-42-D2 须 Roslyn 分析器 / 编译期断言守门
5. **刷新时机**: 使用 Unity 的帧回调(如 `OnEnable`/`LateUpdate`)确保下一帧刷新

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 数据边界守卫(DTO Guard · 符号禁令)
- Story 008: 无血条替代反馈(纸面物理行为)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F5**:
- Given: Structure* 事件 Append
- When: 检查刷新时机
- Then: 42 在**下一帧**刷新显示(不是同一帧内立即刷新)
- Edge cases: 同一帧内多次 Append;多帧无 Append

**AC-42-D2**:
- Given: 42 程序集的全部代码
- When: 符号级校验(Roslyn banned members)
- Then: 不存在写三流 / 写存档 / 写 assets/data/ 的调用
- Edge cases: 42 引用 Sim.Contracts(只读)因而能看见 IEventSink;实质判据是符号级调用点

**AC-42-D3**:
- Given: 42 的类型树
- When: 检查字段 / 属性
- Then: 不持有 DTO 副本字段;不持有设置值字段
- Edge cases: 42 的 Presenter 基类若含缓存字段 => 失败

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/skeuomorphic-ui/clinic_panel_refresh_test.cs` OR `production/qa/evidence/clinic-panel-refresh-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (数据边界守卫必须就绪)
- Unlocks: Story 008 (无血条替代反馈)
