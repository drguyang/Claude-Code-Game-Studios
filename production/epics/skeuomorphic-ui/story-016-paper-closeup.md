# Story 016: 教学纸近景(ModalId.PaperCloseup48 · 世界内单张纸近景 · 走近摊纸)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`⑦(教学纸近景 手柄走查), `design/ux/paper-closeup-48.md`(教学纸近景主语)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: ModalId.PaperCloseup48 = 教学纸近景(世界内单张纸近景);走近摊纸触发;ModalId 闭集 7 员之一;IModalState 接口

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— world-space UGUI Canvas / billboard post-cutoff,须 spike。但本 story 的 P0 最小实现 = 走近摊纸触发,风险降级。

**Control Manifest Rules (this layer)**:
- Required: ModalId.PaperCloseup48 世界内单张纸近景;走近摊纸触发;IModalState 接口
- Forbidden: 42 持有 DTO 副本;42 直连 9 的模拟
- Guardrail: 走近摊纸触发距离配置

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**⑦: 教学纸近景 手柄走查
- [ ] **AC-42-F1**⑦: 世界内单张纸近景(走近摊纸触发)
- [ ] **ModalId.PaperCloseup48**: IModalState.Modal 返回 ModalId.PaperCloseup48 或 None

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **ModalId.PaperCloseup48**: IModalState 接口返回 ModalId.PaperCloseup48 或 None
2. **世界内单张纸近景**: World-space UGUI Canvas;走近摊纸触发
3. **走近摊纸触发**: 玩家走近纸张 => 触发 ModalId.PaperCloseup48;触发距离配置
4. **纸面渲染**: 教学纸近景纸面渲染;拟物材质
5. **手柄走查**: 手柄可完成教学纸近景交互
6. **UX Spec**: `design/ux/paper-closeup-48.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 015: 教学界面(本 story 依赖其教学纸近景触发)
- Story 005: 世界空间锚点面片(本 story 依赖其 world-space UGUI Canvas)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**⑦:
- Given: 世界内教学纸近景
- When: 手柄走查
- Then: 手柄可完成全部交互
- Edge cases: 快速走近;远离纸面

**世界内单张纸近景**:
- Given: 玩家走近纸张
- When: 进入触发距离
- Then: ModalId.PaperCloseup48 触发;纸面渲染正确
- Edge cases: 触发距离边界;快速穿过

**ModalId.PaperCloseup48**:
- Given: IModalState 接口
- When: 读取 Modal 属性
- Then: 返回 ModalId.PaperCloseup48 或 None
- Edge cases: 其他模态界面打开时

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/paper-closeup-48-walkthrough.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 015 (教学界面必须就绪), Story 005 (世界空间锚点面片)
- Unlocks: None (last story in epic)
