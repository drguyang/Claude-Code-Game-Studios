# Story 015: 教学界面(纸堆翻页走查 · 零按键提示浮层 · 手柄单机走查)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`⑤(教学界面 手柄走查 + 目视零按键提示浮层), `AC-3-F1a`/`F1b`(42+48 联合 BLOCKING)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架 (primary); ADR-011: 输入架构 (secondary)
**ADR Decision Summary**: 教学界面 = 平面拟物 UI;纸堆翻页;零按键提示浮层;手柄单机走查;42+48 联合反幻想守门

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 运行时焦点导航 post-cutoff,须 spike。ADR-011 Knowledge Risk HIGH —— Input System 6.3 具体行为须实测;接口层为纯 C# 契约不受影响。

**Control Manifest Rules (this layer)**:
- Required: 教学界面 = 平面拟物 UI;纸堆翻页;手柄单机走查;零按键提示浮层
- Forbidden: 传统教学 UI(弹窗 + 文本);硬编码字号;内联变体;按键提示浮层
- Guardrail: 42+48 联合反幻想守门(零按键提示浮层)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**⑤: 教学界面 手柄走查 + 目视零按键提示浮层
- [ ] **AC-3-F1a**: 42+48 联合 BLOCKING:零按键提示浮层(目视检查)
- [ ] **AC-3-F1b**: 手柄单机走查(手柄可独立完成全部教学流程)
- [ ] **纸堆翻页**: 教学界面 = 纸堆翻页;翻页手势 / 按钮正确
- [ ] **教学纸近景**: ModalId.PaperCloseup48 世界内单张纸近景(走近摊纸)

---

## Implementation Notes

*Derived from ADR-013 / ADR-011 Implementation Guidelines:*

1. **教学界面**: 平面拟物 UI;纸堆翻页;UI Toolkit UXML/USS
2. **纸堆翻页**: 翻页手势 / 按钮;翻页动画
3. **零按键提示浮层**: 目视零按键提示浮层(反幻想守门 AC-3-F1a)
4. **手柄单机走查**: 手柄可独立完成全部教学流程(AC-3-F1b)
5. **教学纸近景**: ModalId.PaperCloseup48;世界内单张纸近景(走近摊纸)
6. **UX Spec**: `design/ux/tutorial-48.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: 焦点门时序与过渡(本 story 依赖其 PlayMode 验证)
- Story 016: 教学纸近景(ModalId.PaperCloseup48 独立成 story)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**⑤:
- Given: 教学界面 UI
- When: 手柄走查 + 目视检查
- Then: 手柄可完整导航;目视零按键提示浮层
- Edge cases: 快速导航;焦点边界切换

**AC-3-F1a**:
- Given: 42 + 48 全部 UI
- When: 目视检查
- Then: 零按键提示浮层
- Edge cases: 模态界面;tooltip;所有屏幕

**AC-3-F1b**:
- Given: 手柄
- When: 单机走查(无键鼠)
- Then: 手柄可独立完成全部教学流程
- Edge cases: 快速操作;长按操作

**纸堆翻页**:
- Given: 教学纸堆
- When: 翻页
- Then: 翻页手势 / 按钮正确;翻页动画流畅
- Edge cases: 第一页;最后一页;快速翻页

**教学纸近景**:
- Given: 世界内教学纸近景
- When: 走近摊纸
- Then: ModalId.PaperCloseup48 正确触发;纸面渲染正确
- Edge cases: 快速走近;远离纸面

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/tutorial-48-walkthrough.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 010 (焦点门时序与过渡必须就绪), Story 011 (脉案页渲染), Story 012 (存档位界面), Story 013 (库存容器), Story 014 (设置界面)
- Unlocks: Story 016 (教学纸近景)
