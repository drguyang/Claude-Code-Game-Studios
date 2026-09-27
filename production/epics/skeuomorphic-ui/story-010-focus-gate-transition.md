# Story 010: 焦点门时序与过渡(PlayMode 帧探针 · 过渡窗口 ≤1 frame · 不可重入)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Visual/Feel
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: 无专属 TR(F8 焦点门时序不变量)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 焦点单栈门;切换走先关后开;过渡窗口 0 ≤ W_trans ≤ 1 frame;两门皆开永不可观测(F8 不变量)

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH;但本 story 只涉及焦点门状态机时序,不涉及焦点桥 / 自定义材质,风险降级。

**Control Manifest Rules (this layer)**:
- Required: 过渡窗口 0 ≤ W_trans ≤ 1 frame;两门皆开永不可观测;过渡态内不可重入
- Forbidden: 两门同时开;过渡窗口内重入
- Guardrail: PlayMode 帧探针实测过渡窗口 ≤1 frame

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-B3b**: PlayMode 过渡窗口实测:焦点门切换的过渡窗口 ≤1 frame(用帧探针测量)
- [ ] **F8 不变量**: 两门皆开永不可观测(在全部采样点上,不存在两门同时开的状态)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **PlayMode 帧探针**: 使用 Unity 的帧探针(如 `Camera.onPostRender` 或 `OnRenderObject`)测量焦点门切换的实际帧数
2. **过渡窗口测量**: 记录切换开始帧 / 结束帧;断言间隔 ≤1 frame
3. **F8 不变量断言**: 在全部采样点上,断言两门不同时开
4. **不可重入**: 过渡窗口内再次请求切换 => 合并 / 去重(不产生第二次关/开序列)
5. **测试方法**: PlayMode 测试 + 帧探针;不是 EditMode 模拟

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 焦点门状态机 + 焦点导航呈现桥(本 story 依赖其就绪)
- Story 003: 焦点导航边界(rank 数据 / 焦点悬空)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-B3b**:
- Given: FocusGateStateMachine 运行中
- When: 执行模式切换(平面 → 世界空间)
- Then: PlayMode 帧探针测量过渡窗口 ≤1 frame
- Edge cases: 快速连续切换;切换请求落在过渡窗口内

**F8 不变量**:
- Given: 焦点门运行中(全部模式切换序列)
- When: 逐帧采样焦点门状态
- Then: 不存在两门同时开的状态
- Edge cases: 极端快速切换;多线程切换请求

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: `production/qa/evidence/focus-gate-transition-evidence.md` + PlayMode frame probe recording

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (焦点门状态机 + 焦点导航呈现桥必须就绪), Story 003 (焦点导航边界必须就绪)
- Unlocks: Story 011 (脉案页渲染)
