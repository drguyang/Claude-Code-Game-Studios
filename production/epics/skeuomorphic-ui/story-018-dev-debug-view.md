# Story 018: 开发者调试视图(仅 Development Build · 焦点栈/元件库/DTO 绑定结果 · 不显示游戏数值)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: 无专属 TR(规则九:调试视图内容)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 调试视图 = 仅 Development Build;焦点栈 / 元件库 / DTO 绑定结果;不显示游戏数值;42 只渲染不持状态

**Engine**: Unity 6.3 LTS | **Risk**: LOW
**Engine Notes**: 本 story 只涉及 Development Build 调试视图,不涉及任何 post-cutoff API。Risk = LOW。

**Control Manifest Rules (this layer)**:
- Required: 调试视图仅 Development Build;焦点栈 / 元件库 / DTO 绑定结果;不显示游戏数值
- Forbidden: 调试视图显示游戏数值(生命 / 伤情 / 乘子 / 置信度等);调试视图进入玩家构建
- Guardrail: 调试视图内容 ⊆ 白名单(枚举 / 状态字段 / bool / 计数字段)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-A6**: 构建期 debug 视图代码路径不存在于玩家构建(#if UNITY_EDITOR || DEVELOPMENT_BUILD)
- [ ] **AC-42-D4**: 42 的调试视图(#if UNITY_EDITOR || DEVELOPMENT_BUILD)读到的 DTO 成员 ⊆ 白名单(枚举/状态字段 / bool / 计数字段);不读任何量纲为游戏量的字段
- [ ] **规则九**: 调试视图内容 = 焦点栈 / 元件库 / DTO 绑定结果;不显示游戏数值

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **调试视图**: 仅 Development Build(#if UNITY_EDITOR || DEVELOPMENT_BUILD)
2. **调试视图内容**: 焦点栈 / 元件库 / DTO 绑定结果;不显示游戏数值
3. **白名单**: 调试视图内容 ⊆ 白名单(枚举 / 状态字段 / bool / 计数字段)
4. **构建剥离**: 调试视图代码路径不存在于玩家构建(构建剥离 / 代码裁剪)
5. **符号级禁令**: AC-42-D4 断调试视图只读白名单成员,禁读游戏量字段

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 数据边界守卫(本 story 依赖其调试视图白名单)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-A6**:
- Given: 玩家构建
- When: 检查代码路径
- Then: 调试视图代码路径不存在于玩家构建
- Edge cases: Development Build;Release Build

**AC-42-D4**:
- Given: 42 的调试视图代码(#if UNITY_EDITOR || DEVELOPMENT_BUILD)
- When: 符号级校验
- Then: 读到的 DTO 成员 ⊆ 白名单(枚举 / bool / 计数字段);不读 float / int 游戏量字段
- Edge cases: 调试视图读取 VitalsDto 的 float 字段(生命值等) => 失败

**规则九**:
- Given: 调试视图
- When: 检查内容
- Then: 焦点栈 / 元件库 / DTO 绑定结果;不显示游戏数值
- Edge cases: 调试视图显示 VitalsDto 的 float 字段 => 失败

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/dev-debug-view-evidence.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (数据边界守卫必须就绪)
- Unlocks: None
