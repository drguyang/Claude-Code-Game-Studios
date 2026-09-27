# Story 009: 焦点可见样式 + 无障碍钩子接口(字号缩放 / 动效缩放 / 焦点可见样式契约)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-012` (无障碍四钩子:字号缩放 / 动效缩放 / 焦点可见样式 / 屏幕阅读器)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 无障碍四钩子(字号缩放 / 动效缩放 / 焦点可见样式 / 屏幕阅读器);焦点可见样式 = 墨色加深 / 纸面压痕(禁纯色高亮);文本缩放主题变量层(重新求值不缓存最终字号)

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 6.3 无障碍 API(如 Accessibility 命名空间)为 post-cutoff,须 spike(§6.6 假设 6)。

**Control Manifest Rules (this layer)**:
- Required: 无障碍四钩子完整;焦点可见样式契约(墨色加深/纸面压痕 · 禁纯色高亮);文本缩放主题变量层(重新求值 · 不缓存最终字号)
- Forbidden: 纯色高亮(焦点可见样式);缓存最终字号(文本缩放须重新求值)
- Guardrail: 动效缩放置 0 后仍可用(不可完全禁动效导致功能不可用)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-G3**: 对比度全档断言:阈值归 49 但义务归 42;42 提供对比度检查接口
- [ ] **AC-42-G4**: 动效缩放挂点:置 0 后仍可用(不可完全禁动效导致功能不可用)
- [ ] **焦点可见样式契约**: 墨色加深 / 纸面压痕(禁纯色高亮);焦点样式 = 纸面物理反馈
- [ ] **文本缩放主题变量层**: 重新求值不缓存最终字号;主题变量层文本缩放须响应系统设置
- [ ] **TR-skeuoui-012**: 无障碍四钩子完整(字号缩放 / 动效缩放 / 焦点可见样式 / 屏幕阅读器)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **无障碍四钩子接口**: 字号缩放 / 动效缩放 / 焦点可见样式 / 屏幕阅读器;四钩子完整
2. **焦点可见样式**: 墨色加深 + 纸面压痕(禁纯色高亮);焦点样式 = 纸面物理反馈
3. **文本缩放**: 主题变量层文本缩放须重新求值不缓存最终字号;响应系统设置
4. **动效缩放**: 动效缩放挂点;置 0 后仍可用(不可完全禁动效导致功能不可用)
5. **对比度检查**: 42 提供对比度检查接口;阈值归 49 但义务归 42
6. **屏幕阅读器**: 屏幕阅读器钩子(具体实现视 49 进度)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 焦点门状态机 + 焦点导航呈现桥(本 story 依赖其就绪)
- Story 001: 拟物元件库基础(本 story 依赖其主题变量层)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-G3**:
- Given: 全部 UI 控件
- When: 检查对比度
- Then: 对比度全档断言;42 提供对比度检查接口
- Edge cases: 极小字号;低对比度主题

**AC-42-G4**:
- Given: 动效缩放 = 0
- When: 运行 UI
- Then: 仍可用(不可完全禁动效导致功能不可用)
- Edge cases: 动效缩放 = 0 时焦点导航;动效缩放 = 0 时模态开闭

**焦点可见样式契约**:
- Given: 控件获得焦点
- When: 渲染焦点样式
- Then: 墨色加深 + 纸面压痕(禁纯色高亮)
- Edge cases: 深色主题下焦点样式;浅色主题下焦点样式

**文本缩放主题变量层**:
- Given: 系统字号缩放设置变更
- When: 重新渲染
- Then: 主题变量层文本缩放须重新求值不缓存最终字号
- Edge cases: 缩放 = 150%;缩放 = 200%

**TR-skeuoui-012**:
- Given: 无障碍四钩子接口
- When: 检查完整性
- Then: 四钩子完整(字号缩放 / 动效缩放 / 焦点可见样式 / 屏幕阅读器)
- Edge cases: 屏幕阅读器未启用时钩子仍存在

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/focus-visual-and-accessibility-evidence.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (元件库基础必须就绪), Story 002 (焦点门状态机必须就绪)
- Unlocks: Story 014 (设置界面壳)
