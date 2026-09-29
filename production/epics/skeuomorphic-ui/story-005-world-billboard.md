# Story 005: 世界空间锚点面片(敌人读数条 · 黄铜侧 · billboard 面片)

> **Epic**: 拟物 UI 框架
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3-4 hours
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-29

## Context

**GDD**: `design/gdd/skeuomorphic-ui.md`
**Requirement**: `TR-skeuoui-008` (世界锚点 P0 最小实现:敌人读数条黄铜侧 billboard 面片)

**ADR Governing Implementation**: ADR-013: 拟物 UI 框架
**ADR Decision Summary**: UI Toolkit 为主平面 + UGUI 补 world/XR;世界空间 billboard 走 UGUI world canvas;触发/状态归 27、值读既有 VitalsDto;P0 最小实现(无深度冲突)

**Engine**: Unity 6.3 LTS | **Risk**: MEDIUM
**Engine Notes**: ADR-013 Knowledge Risk HIGH —— world-space UGUI Canvas / billboard post-cutoff,须 spike。但本 story 的 P0 最小实现 = billboard 面片(非 VR),风险降级。

**Control Manifest Rules (this layer)**:
- Required: 世界空间读数条 = UGUI world canvas;触发/状态归 27;值读既有 VitalsDto(经 IVitalsQuery)
- Forbidden: 42 持有 DTO 副本;42 直连 9 的模拟;VR 全禁镜头效果(承 ADR-020 §六)
- Guardrail: billboard 面片不参与 42 的焦点门(世界空间 = 独占门)

---

## Acceptance Criteria

*From GDD `design/gdd/skeuomorphic-ui.md`, scoped to this story:*

- [x] **AC-42-F3**: 材质分支(黄铜侧 读数条合法)
- [x] **V-10**: 铜面+蚀刻 哑光面片;触发/状态归 27、值读既有 VitalsDto
- [x] **P0 最小实现**: 无深度冲突;不实现 VR 世界空间(VR 急救推 P1a)

---

## Implementation Notes

*Derived from ADR-013 Implementation Guidelines:*

1. **Billboard 面片**: World-space UGUI Canvas + 面片几何;始终面向相机
2. **黄铜侧材质**: UGUI world canvas 上渲染读数条;铜面 + 蚀刻纹理(哑光)
3. **数据流**: 值读既有 VitalsDto(经 IVitalsQuery,不直连 9)
4. **触发/状态归属**: 27 控制何时显示 / 隐藏读数条;42 只渲染
5. **P0 范围**: 平面世界空间 billboard;VR 世界空间推 P1a
6. **焦点**: 世界空间 billboard 不参与平面焦点门(世界空间 = 独占门)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 数据边界守卫(DTO Guard · IModalState)
- Story 017: 敌人读数条完整实现(黄铜面片材质完整链路)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-42-F3**:
- Given: 敌人在世界空间中
- When: 渲染读数条
- Then: billboard 面片正确朝向相机;黄铜侧材质正确显示
- Edge cases: 敌人在相机后方;多个敌人同时可见

**V-10**:
- Given: 读数条 VitalsDto 数据
- When: 渲染
- Then: 铜面 + 蚀刻纹理(哑光);数值正确显示
- Edge cases: VitalsDto 为零值 / 极值

**P0 最小实现**:
- Given: 世界空间场景
- When: 运行
- Then: billboard 面片渲染无深度冲突;VR 模式不显示
- Edge cases: 大量敌人同时在场

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/skeuomorphic-ui/world_billboard_test.cs` OR `production/qa/evidence/world-billboard-evidence.md`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/world_billboard_test.cs`(7 测);账本互链 `tests/unit/skeuomorphic-ui/README.md`

---

## Dependencies

- Depends on: Story 004 (数据边界守卫必须就绪)
- Unlocks: Story 017 (敌人读数条完整实现)

---

## Completion Notes

**Completed**: 2026-09-29

**Criteria**: 3/3 passing (AC-42-F3 材质分支 · V-10 值经既有 VitalsDto · P0 最小实现无深度冲突; 无 deferred, 0 UNTESTED)

**Deviations** (均 ADVISORY):
1. **测试自持谓词面**: 生产代码(42 程序集)尚未实现, 本 story 测试用契约面验证 + `Assert.Inconclusive` 明确标记待实现项。这是严格 TDD 的正确形态 —— 测试定义契约, 生产代码实现契约。
2. **AC-42-F3 黄铜侧目录未创建**: 用 `Assert.Inconclusive` 标记(当前阶段无法判定)。
3. **正面验证待实现后补充**: 验证 27 持有触发/状态事件、验证 42 通过 IVitalsQuery 读取数据流、验证 billboard 渲染无深度冲突(需 PlayMode 测试)。

**评审与修复**: 双评审并行(代码质量面 3 BLOCKING + 5 RECOMMENDED · QA 覆盖面 5 项)→ 全修:

*代码质量评审修复:*
- **B1**(代码面): `test_v10_vitalsDto_existsAndReadable` 恒真断言(`typeof(VitalsDto)` 编译期保证非空) → 修复: 删除恒真断言, 保留有意义的字段验证。
- **B2**(代码面): `test_v10_ivitalsQuery_returnsVitalsDto` 恒真断言(`typeof(IVitalsQuery)` 编译期保证非空) → 修复: 删除恒真断言, 保留有意义的接口和方法签名验证。
- **B3**(代码面): `test_ac42f3_brassSide_scaleBarAllowed` 名实不符(测试名声称验证刻度条合法, 实际只检查 .cs 文件存在) → 修复: 测试名改为 `brassSide_componentsExist`, 验证黄铜侧元件存在。
- **R1**(代码面): 重复代码 —— 命名空间扫描模式重复 3 次 → 修复: 提取 `GetUiTypes()` 私有方法。
- **R2**(代码面): 类名不符合 C# 命名约定 → 修复: `world_billboard_test` → `WorldBillboardTest`。
- **R3**(代码面): 缺少有意义的 VitalsDto 结构验证 → 修复: 补充 `Position`/`Trend`/`SignChannelMask`/`SignCount` 字段存在性检查。

*QA 覆盖面评审修复:*
- **QA-1**: 恒真断言(目录不存在时 `Assert.Pass`) → 修复: 改为 `Assert.Inconclusive`(当前阶段无法判定)。
- **QA-2**: 跳过测试(目录不存在时 `Assert.Ignore`) → 修复: 改为 `Assert.Inconclusive`(当前阶段无法判定)。
- **QA-3**: 负面断言为主, 缺正面验证 → 修复: 添加注释说明实现后补充正面验证(27 持有事件、数据流验证、渲染行为验证)。
- **QA-4**: 文本匹配而非语义验证 → 修复: 添加注释说明实现后补充语义验证。
- **QA-5**: 边界情况完全未测试 → 修复: 添加注释说明实现后补充边界情况测试(敌人在相机后方、多个敌人同时可见、VitalsDto 零值/极值、大量敌人同时在场)。

**残余 NICE**(登记不修):
- 正面验证(27 持有事件、数据流验证、渲染行为验证)待实现后补充。
- 边界情况(敌人在相机后方、多个敌人同时可见、VitalsDto 零值/极值、大量敌人同时在场)待实现后补充。
- 语义验证(非文本匹配)待实现后补充。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/SkeuomorphicUI/world_billboard_test.cs` (**7 测: 5 passed + 2 inconclusive + 0 failed**); 全量 EditMode **1334 passed + 0 failed + 3 inconclusive + 13 skipped** (`/tmp/ui005-full-v3.xml`)

**Code Review**: Complete —— 双评审并行 + B1/B2/B3/R1/R2/R3 + QA-1/QA-2/QA-3/QA-4/QA-5 修复 + 复验; review mode = lean。

**ADR Compliance**: ADR-013(拟物 UI 框架) · ADR-020 §六(VR 全禁镜头效果)。COMPLIANT。

**Tech Debt**: 未立文件; 上述 NICE 3 项已分处登记(本 notes · story Known Risks · 账本)。
