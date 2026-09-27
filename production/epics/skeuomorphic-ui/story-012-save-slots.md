# Story 012: 存档位界面渲染 + 焦点

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`②(存档位界面 手柄走查)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: UI Toolkit 为主平面;存档位界面 = 平面拟物 UI;焦点顺序由 rank 数据驱动;零按键提示浮层

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 运行时焦点导航 post-cutoff,须 spike。

**Control Manifest Rules (this layer)**:
- Required: 存档位界面 = 平面拟物 UI;焦点顺序由 rank 数据驱动;零按键提示浮层
- Forbidden: 传统存档 UI(列表 + 按钮);硬编码字号;内联变体
- Guardrail: 焦点顺序由 rank 数据驱动;空行有格线无字

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**②: 存档位界面 手柄走查 + 目视零按键提示浮层
- [ ] **存档位界面**: 平面拟物 UI 渲染;存档槽位完整显示
- [ ] **焦点顺序**: 由 rank 数据驱动;存档槽位焦点顺序正确

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **存档位界面**: 平面拟物 UI;UI Toolkit UXML/USS
2. **存档槽位**: 每个存档槽位 = 拟物元件(纸面 / 墨迹 / 印章)
3. **焦点顺序**: 由 rank 数据驱动;存档槽位焦点顺序正确
4. **零按键提示浮层**: 目视零按键提示浮层(反幻想守门)
5. **UX Spec**: `design/ux/save-slots-7b.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: 焦点门时序与过渡(本 story 依赖其 PlayMode 验证)
- Story 015: 教学界面(零按键提示浮层联合验证)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**②:
- Given: 存档位界面 UI
- When: 手柄走查 + 目视检查
- Then: 手柄可完整导航;目视零按键提示浮层
- Edge cases: 快速导航;焦点边界切换

**存档位界面**:
- Given: 存档槽位
- When: 渲染
- Then: 平面拟物 UI 渲染;存档槽位完整显示
- Edge cases: 空存档槽;满存档槽

**焦点顺序**:
- Given: 存档槽位焦点顺序
- When: 手柄导航
- Then: 焦点顺序由 rank 数据驱动;正确
- Edge cases: 动态添加/删除存档槽

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/save-slots-walkthrough.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 010 (焦点门时序与过渡必须就绪), Story 001 (元件库基础)
- Unlocks: Story 015 (教学界面)
