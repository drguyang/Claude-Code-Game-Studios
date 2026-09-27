# Story 008: 无血条替代反馈(纸面物理行为 · 印章 · 页边记号 · 拒绝权降级白名单)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: 无专属 TR(拒绝权降级白名单 = 续页唯一路径)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 拒绝权降级白名单 = 续页唯一路径;开发期报冲突;不得静默裁切/缩字/破纸;记号登记表(勾/点/叠角/划改痕/印/折角);无血条替代反馈(进度=页数/纸厚/墨迹密度)

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH;但本 story 不涉及焦点桥 / 自定义材质,只涉及纸面物理行为 / 记号渲染,风险降级。

**Control Manifest Rules (this layer)**:
- Required: 拒绝权降级白名单 = 续页唯一路径;开发期报冲突;记号登记表完整
- Forbidden: 静默裁切 / 缩字 / 破纸;无血条替代反馈用传统血条
- Guardrail: 记号渲染不破纸(纸张边界断言)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **拒绝权降级白名单**: 续页唯一路径(当内容超限时,只能续页,不能裁切 / 缩字 / 破纸);开发期报冲突;不得静默裁切/缩字/破纸
- [ ] **记号登记表**: 记号类型完整(勾 / 点 / 叠角 / 划改痕 / 印 / 折角);每种记号有明确渲染形态
- [ ] **无血条替代反馈**: 进度 = 页数 / 纸厚 / 墨迹密度(不是传统血条)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **拒绝权降级白名单**: 当内容超限时,42 报告冲突(开发期断言);降级路径 = 续页;不得静默裁切/缩字/破纸
2. **记号登记表**: 六种记号(勾 / 点 / 叠角 / 划改痕 / 印 / 折角);每种记号有明确渲染形态
3. **无血条替代反馈**: 进度指示 = 页数 / 纸厚 / 墨迹密度;不是传统血条
4. **纸张物理行为**: 纸面渲染遵循纸张物理约束(不破纸 / 不超界)
5. **记号渲染**: 记号渲染不破纸(纸张边界断言)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: 医馆面板渲染 + 刷新延迟契约
- Story 011: 脉案页渲染

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**拒绝权降级白名单**:
- Given: 内容超限场景
- When: 渲染
- Then: 42 报告冲突(开发期断言);降级路径 = 续页;不得静默裁切/缩字/破纸
- Edge cases: 刚好在边界;超限 1 字符;超限大量内容

**记号登记表**:
- Given: 六种记号(勾 / 点 / 叠角 / 划改痕 / 印 / 折角)
- When: 渲染
- Then: 每种记号有明确渲染形态;不混淆
- Edge cases: 同一控件叠加多种记号

**无血条替代反馈**:
- Given: 进度指示场景
- When: 渲染
- Then: 进度 = 页数 / 纸厚 / 墨迹密度;不是传统血条
- Edge cases: 进度 = 0%;进度 = 100%

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/skeuomorphic-ui/no-healthbar_feedback_test.cs` OR `production/qa/evidence/no-healthbar-feedback-evidence.md`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 007 (数据边界收尾必须就绪)
- Unlocks: Story 011 (脉案页渲染)
