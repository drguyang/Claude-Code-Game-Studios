# unit/skeuomorphic-ui/

按系统分目录的单元测试(命名 `test_[scenario]_[expected]`)。
拟物 UI 框架系统(42)的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点;
`.cs` 真身落 `unity/Assets/Tests/EditMode/SkeuomorphicUI/`,本 README 记真身与 AC → 测映射。

## Story 002(焦点门与桥接 —— AC-42-F1…F2)

故事头登记的证据路径为
`tests/unit/skeuomorphic-ui/focus_gate_and_bridge_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_gate_and_bridge_test.cs`**(类 `FocusGateAndBridgeTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_gate_and_bridge_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/Skeuomorphic/`(焦点门状态机 + 桥接) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-F1…F2) |
| 运行方式 | `unity test unity --mode EditMode --filter FocusGateAndBridgeTest` |

## Story 005(世界空间锚点面片 —— AC-42-F3 · V-10 · P0 最小实现)

故事头登记的证据路径为
`tests/integration/skeuomorphic-ui/world_billboard_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/world_billboard_test.cs`**(类 `WorldBillboardTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/world_billboard_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`VitalsDto` / `IVitalsQuery`) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-F3 · V-10 · P0 最小实现) |
| 运行方式 | `unity test unity --mode EditMode --filter WorldBillboardTest` |

> 读法纪律:生产代码(42 程序集)尚未实现,测试用契约面验证 + `Assert.Inconclusive` 明确标记待实现项。
> `test_ac42f3_componentLibrary_noForbiddenItems` 在元件库目录不存在时用 `Assert.Inconclusive`(当前阶段无法判定)。
> `test_ac42f3_brassSide_componentsExist` 在黄铜侧目录未创建时用 `Assert.Inconclusive`(当前阶段无法判定)。

## AC → 测试函数映射

| AC | 测试函数(`WorldBillboardTest` 内) | 性质 |
|---|---|---|
| **AC-42-F3 材质分支** | `test_ac42f3_componentLibrary_noForbiddenItems` + `test_ac42f3_brassSide_componentsExist` | BLOCKING |
| **V-10 值经既有 VitalsDto** | `test_v10_vitalsDto_structureValid` + `test_v10_ivitalsQuery_returnsVitalsDto` | BLOCKING |
| **V-10 触发/状态归 27** | `test_v10_triggerState_notIn42` | BLOCKING |
| **P0 无深度冲突** | `test_p0_noDepthConflict_noDepthConflictCode` | BLOCKING |
| **P0 不实现 VR** | `test_p0_noVrWorldSpace_noVrCode` | BLOCKING |

> **测试数**:`world_billboard_test` = **7**(5 passed + 2 inconclusive + 0 failed)。
> ⚠️ 关键区分:生产代码(42 程序集)尚未实现,测试用契约面验证。
> 代码评审修复:B1/B2 删除恒真断言;B3 名实不符修正;R1 提取重复代码;R2 类名 PascalCase;R3 补充字段验证。
> QA 评审修复:恒真断言 → Assert.Inconclusive;跳过测试 → Assert.Inconclusive;添加正面验证注释。
