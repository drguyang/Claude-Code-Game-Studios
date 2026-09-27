# Story 005: 世界空间锚点面片(敌人读数条 · 黄铜侧 · billboard 面片)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-008` (世界锚点 P0 最小实现:敌人读数条黄铜侧 billboard 面片)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: UI Toolkit 为主平面 + UGUI 补 world/XR;世界空间 billboard 走 UGUI world canvas;触发/状态归 27、值读既有 VitalsDto;P0 最小实现(无深度冲突)

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— world-space UGUI Canvas / billboard post-cutoff,须 spike。但本 story 的 P0 最小实现 = billboard 面片(非 VR),风险降级。

**Control Manifest Rules (this layer)**:
- Required: 世界空间读数条 = UGUI world canvas;触发/状态归 27;值读既有 VitalsDto(经 IVitalsQuery)
- Forbidden: 42 持有 DTO 副本;42 直连 9 的模拟;VR 全禁镜头效果(承 ADR-020 §六)
- Guardrail: billboard 面片不参与 42 的焦点门(世界空间 = 独占门)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F3**: 材质分支(黄铜侧 读数条合法)
- [ ] **V-10**: 铜面+蚀刻 哑光面片;触发/状态归 27、值读既有 VitalsDto
- [ ] **P0 最小实现**: 无深度冲突;不实现 VR 世界空间(VR 急救推 P1a)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **Billboard 面片**: World-space UGUI Canvas + 面片几何;始终面向相机
2. **黄铜侧材质**: UGUI world canvas 上渲染读数条;铜面 + 蚀刻纹理(哑光)
3. **数据流**: 值读既有 VitalsDto(经 IVitalsQuery,不直连 9)
4. **触发/状态归属**: 27 控制何时显示 / 隐藏读数条;42 只渲染
5. **P0 范围**: 平面世界空间 billboard;VR 世界空间推 P1a
6. **焦点**: 世界空间 billboard 不参与平面焦点门(世界空间 = 独占门)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 数据边界守卫(DTO Guard · IModalState)
- Story 017: 敌人读数条完整实现(黄铜面片材质完整链路)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F3**:
- Given: 敌人在世界空间中
- When: 渲染读数条
- Then: billboard 面片正确朝向相机;黄铜侧材质正确显示
- Edge cases: 敌人在相机后方;多个敌人同时可见

**V-10**:
- Given: 读数条 VitalsDto 数据
- When: 渲染
- Then: 铜面 + 蚀刻纹理(哑光);数值正确显示
- Edge cases: VitalsDto 为零值 / 极值

**P0 最小实现**:
- Given: 世界空间场景
- When: 运行
- Then: billboard 面片渲染无深度冲突;VR 模式不显示
- Edge cases: 大量敌人同时在场

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/skeuomorphic-ui/world_billboard_test.cs` OR `production/qa/evidence/world-billboard-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (数据边界守卫必须就绪)
- Unlocks: Story 017 (敌人读数条完整实现)
