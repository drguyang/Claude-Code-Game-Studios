# Story 014: 设置界面壳(总线音量 + mono · 条目语义归 44 · 42 不缓存)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`④(设置界面 手柄走查), `AC-42-G2`(设置壳不缓存他系统状态)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架 (primary); ADR-018: 音频架构 (secondary)
**ADR Decision Summary**: 设置界面壳 = 平面拟物 UI;条目语义归 44(音频总线 / 混音参数);42 不缓存他系统状态(音量 / mono 等);只持有 UI 呈现态(开关 / 滑块位置)

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH;但本 story 只涉及设置壳 UI 渲染 + 焦点,不涉及焦点桥 / 自定义材质,风险降级。

**Control Manifest Rules (this layer)**:
- Required: 设置壳只持有 UI 呈现态(开关 / 滑块位置);不持有他系统状态;条目语义归 44
- Forbidden: 42 持有设置值;42 直连 9 的模拟;内联变体
- Guardrail: 设置壳零缓存(每帧从 IVitalsQuery 或 44 接口读取最新值)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**④: 设置界面 手柄走查 + 目视零按键提示浮层
- [ ] **AC-42-G2**: 设置壳不缓存他系统状态(音量 / mono / 任何游戏量);设置壳只持有 UI 呈现态(开关 / 滑块位置)
- [ ] **音频总线**: 设置界面包含音频总线音量 / mono 控制;条目语义归 44

---

## Implementation Notes

*Derived from ADR-013 / ADR-018 Implementation Guidelines:*

1. **设置壳状态**: 只持有 UI 呈现态(开关 / 滑块位置);不持有他系统状态(音量 / mono 等)
2. **音频总线控制**: 设置界面包含音频总线音量 / mono 控制;条目语义归 44
3. **数据流**: 42 通过 44 接口读取音频总线状态;不直连 AudioMixer
4. **焦点顺序**: 由 rank 数据驱动;设置条目焦点顺序正确
5. **零按键提示浮层**: 目视零按键提示浮层(反幻想守门)
6. **UX Spec**: `design/ux/settings-shell-42.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: 焦点可见样式 + 无障碍钩子(本 story 依赖其字号缩放 / 动效缩放)
- Story 015: 教学界面(零按键提示浮层联合验证)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**④:
- Given: 设置界面 UI
- When: 手柄走查 + 目视检查
- Then: 手柄可完整导航;目视零按键提示浮层
- Edge cases: 快速导航;焦点边界切换

**AC-42-G2**:
- Given: 设置壳类型树
- When: 检查字段 / 属性
- Then: 不持有他系统状态(音量 / mono / 任何游戏量);只持有 UI 呈现态(开关 / 滑块位置)
- Edge cases: 设置壳缓存 AudioMixer 引用(引用本身合法,但不持有音量值)

**音频总线**:
- Given: 音频总线设置
- When: 调整音量 / mono
- Then: 条目语义归 44;42 不缓存
- Edge cases: 音量 = 0;音量 = 100%

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/settings-shell-walkthrough.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009 (焦点可见样式 + 无障碍钩子必须就绪), Story 004 (数据边界守卫)
- Unlocks: Story 015 (教学界面)
