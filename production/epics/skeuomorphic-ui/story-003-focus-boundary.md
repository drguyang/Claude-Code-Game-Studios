# Story 003: 焦点导航边界(rank 数据断言 · 焦点悬空回退 · 空行反馈 · 同键双触发禁令)

> **Epic**: 拟物 UI 框架
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-003` (焦点单栈门: rank 数据满射/单射 · K=0 门关 · 空行无文本 · 焦点悬空回退)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架 (primary); ADR-011: 输入架构 (secondary)
**ADR Decision Summary**: 焦点单栈门;官方桥 NavigationMoveEvent 唯一真源;禁同键双触发;K=0 门关界面仍渲染;焦点悬空回退

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 运行时焦点导航 post-cutoff,须 spike。

**Control Manifest Rules (this layer)**:
- Required: 焦点导航意图流经焦点门状态机;K=0 门关界面仍渲染;空行有格线无字;置信度不出溢体征栏
- Forbidden: 自实现焦点算法(除非 spike 失败降级);同一控件同时被官方 Navigate 与自建焦点动作绑上;焦点悬空不处理
- Guardrail: rank 数据满射/单射装载断言

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-B1**: rank 数据满射/单射装载断言:对全部可聚焦控件,rank 数据满足满射(每控件有唯一 rank) + 单射(rank 值唯一)
- [ ] **AC-42-B2**: K=0 的合成夹具屏打开时,门不开,界面仍渲染
- [ ] **AC-42-B5**: 空行有格线无字(格线渲染存在但无文本节点)
- [ ] **AC-42-B6**: 置信度不出溢体征栏(置信度数值 ≤ 体征栏承载上限)
- [ ] **AC-42-B7**: 焦点悬空回退:焦点当前控件被销毁/禁用时,焦点自动回退到最近有效控件(不回弹到无效控件)
- [ ] **同键双触发禁令**(E-3): 同一物理输入不得同时绑官方桥 Navigation 与自实现路径

---

## Implementation Notes

*Derived from ADR-013 / ADR-011 Implementation Guidelines:*

1. **rank 数据断言**: 构建期 / 装载期检查 rank 数据满射 + 单射
2. **K=0 门关**: 焦点门状态机对 K=0 合成夹具屏保持门关状态;界面仍渲染(纸面/元件正常)
3. **空行反馈**: 格线渲染存在但无文本节点(`TextElement` 不挂或 visibility = hidden)
4. **置信度上限**: 体征栏承载上限配置;置信度数值渲染不超溢
5. **焦点悬空回退**: 焦点当前控件被销毁/禁用时,自动寻找最近有效控件;不回弹到无效控件
6. **同键双触发禁令**: 构造期断言拒绝同一控件同时被官方 Navigate 与自建焦点动作绑定

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 焦点门状态机 + 焦点导航呈现桥(本 story 依赖其就绪)
- Story 004: 模态开集契约 + 数据边界守卫

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-B1**:
- Given: 全部可聚焦控件的 rank 数据
- When: 构建期 / 装载期检查
- Then: rank 数据满足满射(每控件有唯一 rank) + 单射(rank 值唯一)
- Edge cases: 动态添加控件后 rank 数据重新分配

**AC-42-B2**:
- Given: K=0 的合成夹具屏
- When: 屏打开
- Then: ① 门不开;② 界面仍渲染(纸面/元件正常显示)
- Edge cases: K=0 屏切换回 K≥1 屏

**AC-42-B5**:
- Given: 空行控件
- When: 渲染
- Then: 格线渲染存在但无文本节点(TextElement 不挂或 visibility = hidden)
- Edge cases: 空行获得焦点时(仍无字)

**AC-42-B6**:
- Given: 体征栏 + 置信度数值
- When: 渲染置信度
- Then: 置信度数值 ≤ 体征栏承载上限;不出溢
- Edge cases: 置信度 = 上限;置信度 > 上限

**AC-42-B7**:
- Given: 焦点当前控件被销毁 / 禁用
- When: 执行焦点搜索
- Then: 焦点自动回退到最近有效控件;不回弹到无效控件
- Edge cases: 全部控件被销毁(焦点进入悬空态);焦点回退经过禁用控件

**同键双触发禁令**:
- Given: 全部 UI 控件绑定
- When: 构造期检查
- Then: 同一控件不同时被官方 Navigate 与自建焦点动作绑定
- Edge cases: 动态添加控件时重复检查

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/SkeuomorphicUI/focus_boundary_test.cs` — must exist and pass

**Status**: [x] Created + VERIFIED — 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_boundary_test.cs`(6 测全过)

---

## Dependencies

- Depends on: Story 002 (焦点门状态机 + 焦点导航呈现桥必须就绪)
- Unlocks: Story 010 (焦点门时序与过渡)

---

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 6/6 AC passing（rank 满射单射 / K=0 门关 / 空行无字 / 置信度不溢 / 焦点悬空回退 / 同键双触发禁令）
**Deviations**: None
**Test Evidence**: Logic — `unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_boundary_test.cs` — all passing
**Code Review**: Complete — APPROVED
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
