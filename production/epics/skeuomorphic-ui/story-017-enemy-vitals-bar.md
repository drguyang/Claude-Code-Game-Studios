# Story 017: 敌人读数条完整实现(黄铜面片材质 · 蚀刻刻度 · 淡入淡出 · 六态机映射)

> **Epic**: 拟物 UI 框架
> **Status**: Complete
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: 4-5 hours
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: 无专属 TR(V-10 铜面+蚀刻 哑光面片)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 敌人读数条世界锚点 → billboard 面片;黄铜侧(哑光面片 + 蚀刻刻度);触发/状态归 27、值读既有 VitalsDto;淡入淡出;六态机映射

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— world-space UGUI Canvas / billboard post-cutoff,须 spike。但本 story 的完整实现 = 材质 + 动画,风险降级。

**Control Manifest Rules (this layer)**:
- Required: 敌人读数条 = UGUI world canvas billboard;黄铜侧材质;触发/状态归 27;值读既有 VitalsDto
- Forbidden: 42 持有 DTO 副本;42 直连 9 的模拟
- Guardrail: 读数条淡入淡出;六态机映射

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **V-10**: 铜面 + 蚀刻 哑光面片(黄铜侧读数条);触发/状态归 27、值读既有 VitalsDto
- [ ] **世界锚点 → billboard 面片完整链路**: 敌人读数条世界锚点 → billboard 面片完整链路(黄铜面片材质完整)
- [ ] **淡入淡出**: 读数条淡入淡出正确;触发时机正确
- [ ] **六态机映射**: 读数条六态机映射(健康 / 受伤 / 昏迷 / 死亡等状态)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **黄铜面片材质**: UGUI world canvas 上渲染读数条;铜面 + 蚀刻纹理(哑光)
2. **蚀刻刻度**: 读数条蚀刻刻度;刻度对应数值
3. **淡入淡出**: 读数条淡入淡出;触发时机正确(敌人进入/离开感知范围)
4. **六态机映射**: 读数条六态机映射(健康 / 受伤 / 昏迷 / 死亡等状态);每态有对应渲染
5. **数据流**: 值读既有 VitalsDto(经 IVitalsQuery,不直连 9)
6. **触发/状态归属**: 27 控制何时显示 / 隐藏读数条;42 只渲染

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: 世界空间锚点面片(P0 最小实现)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**V-10**:
- Given: 敌人读数条
- When: 渲染
- Then: 铜面 + 蚀刻纹理(哑光);数值正确显示
- Edge cases: VitalsDto 为零值 / 极值

**世界锚点 → billboard 面片完整链路**:
- Given: 敌人在世界空间中
- When: 渲染读数条
- Then: billboard 面片完整链路(黄铜面片材质完整)
- Edge cases: 敌人在相机后方;多个敌人同时可见

**淡入淡出**:
- Given: 敌人进入 / 离开感知范围
- When: 渲染读数条
- Then: 读数条淡入淡出正确
- Edge cases: 快速进出感知范围

**六态机映射**:
- Given: 敌人六态(健康 / 受伤 / 昏迷 / 死亡等)
- When: 渲染读数条
- Then: 每态有对应渲染;状态切换正确
- Edge cases: 状态快速切换

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/enemy-vitals-bar-evidence.md` + screenshot + lead sign-off

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005 (世界空间锚点面片必须就绪)
- Unlocks: None
