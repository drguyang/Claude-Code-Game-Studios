# Story 011: 脉案页渲染 + 焦点导航(五通道区 + 两栏 · 焦点顺序面色→语声→呼吸→触感→病名)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 4-5 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`①(脉案页 手柄走查 + 目视零按键提示浮层), `AC-42-B5`(空行有格线无字), `AC-42-B6`(置信度不出溢)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: UI Toolkit 为主平面;脉案页 = 五通道区(面色 / 语声 / 呼吸 / 触感 / 病名) + 两栏;焦点顺序 = 面色 → 语声 → 呼吸 → 触感 → 病名;空行有格线无字;置信度不出溢体征栏

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 运行时焦点导航 / 自定义材质 post-cutoff,须 spike。

**Control Manifest Rules (this layer)**:
- Required: 脉案页 = 五通道区 + 两栏;焦点顺序 = 面色 → 语声 → 呼吸 → 触感 → 病名;空行有格线无字;置信度不出溢体征栏
- Forbidden: 传统血条;硬编码字号;内联变体
- Guardrail: 焦点顺序由 rank 数据驱动;空行渲染格线但不渲染文本

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**①: 脉案页 手柄走查 + 目视零按键提示浮层
- [ ] **AC-42-B5**: 空行有格线无字(格线渲染存在但无文本节点)
- [ ] **AC-42-B6**: 置信度不出溢体征栏(置信度数值 ≤ 体征栏承载上限)
- [ ] **五通道区**: 面色 / 语声 / 呼吸 / 触感 / 病名五通道完整渲染
- [ ] **两栏布局**: 脉案页两栏布局正确;左栏体征 / 右栏诊断

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **五通道区**: 面色 / 语声 / 呼吸 / 触感 / 病名;每通道独立渲染
2. **两栏布局**: 左栏体征 / 右栏诊断;UI Toolkit 两栏 UXML
3. **焦点顺序**: 面色 → 语声 → 呼吸 → 触感 → 病名;由 rank 数据驱动
4. **空行渲染**: 格线渲染存在但无文本节点(`TextElement` 不挂或 visibility = hidden)
5. **置信度上限**: 体征栏承载上限配置;置信度数值渲染不超溢
6. **零按键提示浮层**: 目视零按键提示浮层(反幻想守门 AC-3-F1a)
7. **UX Spec**: `design/ux/casebook-39.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: 焦点门时序与过渡(本 story 依赖其 PlayMode 验证)
- Story 015: 教学界面(零按键提示浮层联合验证)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**①:
- Given: 脉案页 UI
- When: 手柄走查 + 目视检查
- Then: 手柄可完整导航;目视零按键提示浮层
- Edge cases: 快速导航;焦点边界切换

**AC-42-B5**:
- Given: 脉案页空行
- When: 渲染
- Then: 格线渲染存在但无文本节点
- Edge cases: 空行获得焦点时(仍无字)

**AC-42-B6**:
- Given: 脉案页体征栏 + 置信度数值
- When: 渲染置信度
- Then: 置信度数值 ≤ 体征栏承载上限;不出溢
- Edge cases: 置信度 = 上限;置信度 > 上限

**五通道区**:
- Given: 脉案页五通道
- When: 渲染
- Then: 面色 / 语声 / 呼吸 / 触感 / 病名五通道完整渲染
- Edge cases: 单通道数据为空;全部通道数据为空

**两栏布局**:
- Given: 脉案页两栏
- When: 渲染
- Then: 左栏体征 / 右栏诊断;布局正确
- Edge cases: 窄屏布局;宽屏布局

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/casebook-39-walkthrough.md` + manual verification

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 010 (焦点门时序与过渡必须就绪), Story 001 (元件库基础), Story 008 (无血条替代反馈)
- Unlocks: Story 015 (教学界面, 零按键提示浮层联合验证)
