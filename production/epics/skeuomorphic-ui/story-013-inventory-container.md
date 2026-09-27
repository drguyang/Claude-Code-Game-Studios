# Story 013: 库存容器界面渲染 + 焦点(翻页制 · ≤12 件/屏 · 器物有重量)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`③(库存容器 手柄走查)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: UI Toolkit 为主平面;库存容器 = 翻页制(≤12 件/屏);器物有重量;焦点顺序由 rank 数据驱动;零按键提示浮层

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 运行时焦点导航 post-cutoff,须 spike。

**Control Manifest Rules (this layer)**:
- Required: 库存容器 = 翻页制(≤12 件/屏);器物有重量;焦点顺序由 rank 数据驱动;零按键提示浮层
- Forbidden: 传统列表滚动(>12 件);硬编码字号;内联变体
- Guardrail: 每屏 ≤12 件;器物重量影响翻页节奏

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**③: 库存容器 手柄走查 + 目视零按键提示浮层
- [ ] **翻页制**: ≤12 件/屏;翻页导航正确
- [ ] **器物有重量**: 器物重量影响翻页节奏(重物翻页慢 / 轻物翻页快)
- [ ] **焦点顺序**: 由 rank 数据驱动;器物焦点顺序正确

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **翻页制**: 每屏 ≤12 件;翻页导航(左 / 右翻页)
2. **器物重量**: 器物重量影响翻页节奏(重物翻页慢 / 轻物翻页快);重量数据来自 IVitalsQuery / VitalsDto
3. **焦点顺序**: 由 rank 数据驱动;器物焦点顺序正确
4. **零按键提示浮层**: 目视零按键提示浮层(反幻想守门)
5. **UX Spec**: `design/ux/inventory-container-20.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: 焦点门时序与过渡(本 story 依赖其 PlayMode 验证)
- Story 015: 教学界面(零按键提示浮层联合验证)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**③:
- Given: 库存容器 UI
- When: 手柄走查 + 目视检查
- Then: 手柄可完整导航;目视零按键提示浮层
- Edge cases: 快速导航;焦点边界切换

**翻页制**:
- Given: 库存容器(>12 件)
- When: 翻页导航
- Then: 每屏 ≤12 件;翻页正确
- Edge cases: 恰好 12 件;1 件;0 件

**器物有重量**:
- Given: 不同重量的器物
- When: 翻页
- Then: 重物翻页慢 / 轻物翻页快;节奏正确
- Edge cases: 重量 = 0;重量 = 最大

**焦点顺序**:
- Given: 库存器物焦点顺序
- When: 手柄导航
- Then: 焦点顺序由 rank 数据驱动;正确
- Edge cases: 动态添加/删除器物

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/inventory-container-walkthrough.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 010 (焦点门时序与过渡必须就绪), Story 001 (元件库基础)
- Unlocks: Story 015 (教学界面)
