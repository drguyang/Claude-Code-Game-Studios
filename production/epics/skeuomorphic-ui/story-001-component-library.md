# Story 001: 拟物元件库基础(纸/卷轴/墨迹/印章 · 九宫格 · 主题变量 · 图集)

> **Epic**: 拟物 UI 框架
> **Status**: Complete
> **Layer**: Foundation
> **Type**: UI
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-29

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-011` (图集配额护栏: max N 元件 / 每元件 max M 变体 / 超限构建期报冲突)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: 拟物视觉 = 自建 USS 元件库(纸纹 / 墨迹 / 卷轴九宫格 `-unity-slice-*` + 主题变量) + UXML 组合;UGUI 侧语义对齐;z 序 = 域内序表

**Engine**: Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— UI Toolkit 运行时自定义材质 / 图集 / 九宫格 slicing post-cutoff,须 spike(§6.6 假设 6)。

**Control Manifest Rules (this layer)**:
- Required: 元件库唯一出口;主题变量层文本缩放须重新求值不缓存最终字号;九宫格 `-unity-slice-*`
- Forbidden: 内联变体(AC-42-C4 lint);硬编码字号/文本(AC-42-C5 lint);超过图集配额
- Guardrail: fallback 字体位必须存在

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-C1**: 九宫格装载断言:对全部注册元件,`unity-slice-*` 边框值落在合法区间(>0 且 < 半尺寸)
- [ ] **AC-42-C2**: 主题变量引用完整:全部 USS 颜色 / 字号 / 间距引用变量,无硬编码色值 / 硬编码尺寸
- [ ] **AC-42-C3**: 图集配额构建断言:max N 元件 / 每元件 max M 变体;超限 => 构建期报冲突(不是运行时降级)
- [ ] **AC-42-C4**: 内联变体 lint:组件树内不存在同类型元件通过颜色/尺寸差异模拟变体
- [ ] **AC-42-C5**: 硬编码字号/文本 lint:USS 内不存在硬编码 px 字号 / 硬编码文本内容
- [ ] **AC-42-C6**: fallback 字体位必须存在(中英文至少各一)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **元件库结构**: 纸 / 卷轴 / 墨迹 / 印章四种基础元件,每种含主题变量引用
2. **九宫格 USS**: `-unity-slice-left` / `-unity-slice-right` / `-unity-slice-top` / `-unity-slice-bottom`
3. **主题变量层**: USS 变量定义(颜色 / 字号 / 间距);文本缩放须重新求值
4. **图集配额检查**: 构建期工具扫描元件库注册表,断言 max N 元件 / 每元件 max M 变体
5. **lint 工具**: Roslyn / 构建期脚本检查内联变体 / 硬编码字号 / 硬编码文本
6. **fallback 字体**: 中英文 fallback 字体位注册

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 焦点门状态机 + 焦点导航呈现桥
- Story 009: 焦点可见样式 + 无障碍钩子

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-C1**:
- Given: 全部注册元件的 USS 样式
- When: 构建期断言检查
- Then: `unity-slice-*` 边框值落在合法区间(>0 且 < 半尺寸)
- Edge cases: 零边框元件(不应存在);负值边框

**AC-42-C2**:
- Given: 全部 USS 样式
- When: lint 扫描
- Then: 颜色 / 字号 / 间距引用变量,无硬编码色值 / 硬编码尺寸
- Edge cases: `rgb()` 字面量;`px` 硬编码值

**AC-42-C3**:
- Given: 元件库注册表
- When: 构建期配额检查
- Then: max N 元件 / 每元件 max M 变体;超限 => 构建期报冲突
- Edge cases: 刚好在配额边界;零变体元件

**AC-42-C4**:
- Given: 组件树
- When: lint 扫描
- Then: 不存在同类型元件通过颜色/尺寸差异模拟变体
- Edge cases: 合法变体(通过元件库注册的变体)

**AC-42-C5**:
- Given: USS 样式
- When: lint 扫描
- Then: 不存在硬编码 px 字号 / 硬编码文本内容
- Edge cases: USS 变量引用字号(合法);`font-size: 14px`(非法)

**AC-42-C6**:
- Given: 字体注册表
- When: 检查 fallback
- Then: 中英文至少各一 fallback 字体位存在
- Edge cases: 仅中文字体;仅英文字体

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/component-library-evidence.md` + EditMode `ComponentLibraryTest.cs`

**Status**: [x] VERIFIED — EditMode `ComponentLibraryTest.cs` 37/37 passed (2026-09-29 r3)
**Test File**: `unity/Assets/Tests/EditMode/SkeuomorphicUI/ComponentLibraryTest.cs`
**Result**: `TestResults-639262389081743350.xml` r3 — passed=37, failed=0

---

## Dependencies

- Depends on: None
- Unlocks: Story 011 (脉案页渲染), Story 012 (存档位界面), Story 013 (库存容器), Story 014 (设置界面), Story 015 (教学界面)

---

## Completion Notes

**Completed**: 2026-09-29

**Criteria**: 6/6 passing (AC-42-C1 九宫格装载断言 · AC-42-C2 主题变量引用完整 · AC-42-C3 图集配额构建断言 · AC-42-C4 内联变体 lint · AC-42-C5 硬编码字号/文本 lint · AC-42-C6 fallback 字体位; 无 deferred, 0 UNTESTED)

**Deviations** (均 ADVISORY):
1. **测试已存在**: Story 001 的测试文件 `ComponentLibraryTest.cs` 在之前的会话中已创建并验证(37 测全过), 本次收口仅更新 story 状态为 Complete。
2. **AC 覆盖完整**: 所有 6 个 AC 均有对应测试覆盖, 包括边缘情况(零边框/负值边框/半尺寸边界/仅中文字体/仅英文字体)。

**评审与修复**: 双评审并行(代码质量面 0 BLOCKING · QA 覆盖面 PASS)→ 无需修复:

*代码质量评审:*
- **PASS** — 测试结构清晰, 遵循 arrange/act/assert 纪律, 命名规范, 无假绿风险。

*QA 覆盖面评审:*
- **PASS** — 所有 BLOCKING 级 AC 均有有效测试覆盖, 包括边缘情况。

**残余 NICE**(登记不修):
- 无(测试已完整覆盖所有 AC)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/ComponentLibraryTest.cs` (**37 测: 37 passed + 0 failed**); 全量 EditMode **1403 passed + 0 failed + 38 inconclusive + 26 skipped** (`/tmp/ui018-full-v2.xml`)

**Code Review**: Complete —— 双评审并行 + 无需修复; review mode = lean。

**ADR Compliance**: ADR-013(拟物 UI 框架)。COMPLIANT。

**Tech Debt**: 未立文件; 无残余 NICE。
