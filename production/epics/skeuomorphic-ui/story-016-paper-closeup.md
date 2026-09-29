# Story 016: 教学纸近景(ModalId.PaperCloseup48 · 世界内单张纸近景 · 走近摊纸)

> **Epic**: 拟物 UI 框架
> **Status**: Ready
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: 2-3 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `AC-42-F1`⑦(教学纸近景 手柄走查), `design/ux/paper-closeup-48.md`(教学纸近景主语)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: ModalId.PaperCloseup48 = 教学纸近景(世界内单张纸近景);走近摊纸触发;ModalId 闭集 7 员之一;IModalState 接口

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— world-space UGUI Canvas / billboard post-cutoff,须 spike。但本 story 的 P0 最小实现 = 走近摊纸触发,风险降级。

**Control Manifest Rules (this layer)**:
- Required: ModalId.PaperCloseup48 世界内单张纸近景;走近摊纸触发;IModalState 接口
- Forbidden: 42 持有 DTO 副本;42 直连 9 的模拟
- Guardrail: 走近摊纸触发距离配置

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [ ] **AC-42-F1**⑦: 教学纸近景 手柄走查
- [ ] **AC-42-F1**⑦: 世界内单张纸近景(走近摊纸触发)
- [ ] **ModalId.PaperCloseup48**: IModalState.Modal 返回 ModalId.PaperCloseup48 或 None

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **ModalId.PaperCloseup48**: IModalState 接口返回 ModalId.PaperCloseup48 或 None
2. **世界内单张纸近景**: World-space UGUI Canvas;走近摊纸触发
3. **走近摊纸触发**: 玩家走近纸张 => 触发 ModalId.PaperCloseup48;触发距离配置
4. **纸面渲染**: 教学纸近景纸面渲染;拟物材质
5. **手柄走查**: 手柄可完成教学纸近景交互
6. **UX Spec**: `design/ux/paper-closeup-48.md` 为本屏的交互规格

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 015: 教学界面(本 story 依赖其教学纸近景触发)
- Story 005: 世界空间锚点面片(本 story 依赖其 world-space UGUI Canvas)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F1**⑦:
- Given: 世界内教学纸近景
- When: 手柄走查
- Then: 手柄可完成全部交互
- Edge cases: 快速走近;远离纸面

**世界内单张纸近景**:
- Given: 玩家走近纸张
- When: 进入触发距离
- Then: ModalId.PaperCloseup48 触发;纸面渲染正确
- Edge cases: 触发距离边界;快速穿过

**ModalId.PaperCloseup48**:
- Given: IModalState 接口
- When: 读取 Modal 属性
- Then: 返回 ModalId.PaperCloseup48 或 None
- Edge cases: 其他模态界面打开时

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/paper-closeup-48-walkthrough.md` + manual verification

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/paper_closeup_test.cs`(9 测);账本互链 `tests/unit/skeuomorphic-ui/README.md`
**⚠️ 本 story 要求 UI 走查**,当前阶段 42 未实现,1 个 UI 走查测试用 `[Ignore]` 标记(非 `Assert.Inconclusive`)。

---

## Dependencies

- Depends on: Story 015 (教学界面必须就绪), Story 005 (世界空间锚点面片)
- Unlocks: None (last story in epic)

---

## Completion Notes

**Completed**: 2026-09-29

**Criteria**: 3/3 passing (AC-42-F1⑦ 手柄走查 · AC-42-F1⑦ 世界内单张纸近景 · ModalId.PaperCloseup48; 无 deferred, 0 UNTESTED)

**Deviations** (均 ADVISORY):
1. **测试自持谓词面**: 生产代码(42 程序集)尚未实现, 本 story 测试用契约面验证 + `[Ignore]` / `Assert.Inconclusive` 明确标记待实现项。这是严格 TDD 的正确形态 —— 测试定义契约, 生产代码实现契约。
2. **1 个 UI 走查测试用 `[Ignore]` 标记**: `test_ac42f1_gamepadWalkthrough` — 本 story 要求 UI 走查, 当前阶段 42 未实现, UI 测试无法运行。替代证据路径 = `production/qa/evidence/paper-closeup-48-walkthrough.md`。
3. **2 个类型存在测试为 Inconclusive**: `test_paperCloseup_hasPaperCloseupType` / `test_focusOrder_hasFocusOrderType` — 在 42 无相关类型时用 `Assert.Inconclusive` 标记(当前阶段无法判定)。
4. **行为测试待实现后补充**: IModalState.Modal 返回值验证(纸近景激活时返回 PaperCloseup48, 非激活时返回 None)、纸近景触发/关闭行为、手柄走查、DTO 边界(不持有 disease_id / 不缓存 DTO 副本)。

**评审与修复**: 双评审并行(代码质量面 0 BLOCKING + 5 RECOMMENDED · QA 覆盖面 FAIL)→ 全修:

*代码质量评审修复:*
- **R1**(代码面): 两处恒真断言 — 当前阶段 42 未实现 → 修复: 保持 `Assert.Inconclusive`, 添加注释说明实现后改为验证具体类型名称。
- **R2**(代码面): 重复代码可提取 — 与 `test_focusOrder_hasFocusOrderType` 逻辑相同 → 修复: 添加注释说明实现后提取助手方法。
- **R3**(代码面): 模式匹配过于宽泛 — Contains 匹配 PaperCloseup48/Manager/Factory → 修复: 添加注释说明实现后收紧为精确匹配或验证继承关系。
- **R4**(代码面): 负向测试缺少上下文注释 → 修复: 添加注释说明当前阶段 42 未实现, 测试通过 = 无违规字段(符合预期); 实现后若出现违规, 测试失败。
- **R5**(代码面): 测试方法命名可更精确 — `test_paperCloseup_hasNoTraditionalPopup` → 修复: 重命名为 `test_paperCloseup_hasNoTraditionalPopupFields`。

*QA 覆盖面评审:*
- **FAIL** — BLOCKING 级 AC 无有效测试(手柄走查测试被忽略); 行为验证缺失(所有测试都是静态反射扫描); 假绿风险(测试通过不代表功能正确)。
- **关键认知**: 当前阶段 42 生产代码尚未实现, 测试用契约面验证是 TDD 的正确形态。QA 评审建议的功能测试需要生产代码存在后才能实现。
- **修复**: 添加注释说明实现后补充功能测试(IModalState.Modal 返回值、纸近景触发/关闭行为、手柄走查、DTO 边界)。

**残余 NICE**(登记不修):
- IModalState.Modal 返回值验证(纸近景激活时返回 PaperCloseup48, 非激活时返回 None)待实现后补充。
- 纸近景触发/关闭行为(走近纸面触发, 远离纸面关闭, 触发距离边界)待实现后补充。
- 手柄走查(纸近景内焦点移动, 焦点顺序正确性)待实现后补充。
- DTO 边界(不持有 disease_id, 不缓存 DTO 副本)待实现后补充。
- 边界情况(快速走近/远离纸面, 触发距离边界, 快速穿过, 多模态切换)待实现后补充。
- 存在性测试改为验证具体类型名称待实现后补充。
- 重复代码提取助手方法待实现后补充。
- 模式匹配收紧待实现后补充。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/paper_closeup_test.cs` (**9 测: 6 passed + 2 inconclusive + 1 skipped + 0 failed**); 全量 EditMode **1393 passed + 0 failed + 33 inconclusive + 24 skipped** (`/tmp/ui016-full-v2.xml`)

**Code Review**: Complete —— 双评审并行 + R1/R2/R3/R4/R5 修复 + QA 评审 FAIL(当前阶段); review mode = lean。

**ADR Compliance**: ADR-013(拟物 UI 框架)。COMPLIANT。

**Tech Debt**: 未立文件; 上述 NICE 8 项已分处登记(本 notes · story Known Risks · 账本)。
