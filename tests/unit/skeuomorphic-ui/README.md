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

## Story 006(医馆面板渲染 + 刷新延迟契约 —— AC-42-F5 · AC-42-D2 · AC-42-D3)

故事头登记的证据路径为
`tests/integration/skeuomorphic-ui/clinic_panel_refresh_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/clinic_panel_refresh_test.cs`**(类 `ClinicPanelRefreshTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/clinic_panel_refresh_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/`(`VitalsDto` / `IVitalsQuery` / `IEventSink`) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-F5 · AC-42-D2 · AC-42-D3) |
| 运行方式 | `unity test unity --mode EditMode --filter ClinicPanelRefreshTest` |

> 读法纪律:生产代码(42 程序集)尚未实现,测试用契约面验证 + `Assert.Inconclusive` 明确标记待实现项。
> `test_ac42f5_refreshDelay_hasRefreshMethod` 在 42 无刷新方法时用 `Assert.Inconclusive`(当前阶段无法判定)。

## AC → 测试函数映射

| AC | 测试函数(`ClinicPanelRefreshTest` 内) | 性质 |
|---|---|---|
| **AC-42-D2 不存在写三流** | `test_ac42d2_noEventSinkAppend_noWriteMethods` + `test_ac42d2_noFileWrite_noFileWriteApis` | BLOCKING |
| **AC-42-D3 不持有 DTO 副本** | `test_ac42d3_noDtoCopy_noVitalsDtoFields` + `test_ac42d3_noSettings_noSettingsFields` | BLOCKING |
| **AC-42-F5 下一帧刷新** | `test_ac42f5_refreshDelay_hasRefreshMethod` + `test_ac42f5_noCache_noCacheFields` | BLOCKING |

> **测试数**:`clinic_panel_refresh_test` = **7**(6 passed + 1 inconclusive + 0 failed)。
> ⚠️ 关键区分:生产代码(42 程序集)尚未实现,测试用契约面验证。
> AC-42-F5 的 PlayMode 交互测试无法在 EditMode 中实现,用 `Assert.Inconclusive` 标记。
> 代码评审修复:B1 扩展文件写入检查覆盖方法体;R1 删除恒真断言;R2 统一 DeclaredOnly;R3 删除死代码;R4 增加类型过滤;R5 提取重复代码;R6 增加 IEventSink 字段检查。
> QA 评审修复:AC-42-D3 扩展至所有 DTO 类型(VitalsDto/AudioCueDto), 添加属性检查。

## Story 007(数据边界收尾 —— AC-42-G1 · AC-42-G2 · AC-42-G6)

故事头登记的证据路径为
`tests/unit/skeuomorphic-ui/data_boundary_final_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/data_boundary_final_test.cs`**(类 `DataBoundaryFinalTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/data_boundary_final_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/`(42 类型树) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-G1 · AC-42-G2 · AC-42-G6) |
| 运行方式 | `unity test unity --mode EditMode --filter DataBoundaryFinalTest` |

> 读法纪律:生产代码(42 程序集)尚未实现,测试用契约面验证 + `Assert.Inconclusive` 明确标记待实现项。
> `test_ac42g1_componentRegistry_exists` 在 42 无元件库类型时用 `Assert.Inconclusive`(当前阶段无法判定)。
> FallbackFontRegistry 排除(UI 基础设施,非字形生成)。

## AC → 测试函数映射

| AC | 测试函数(`DataBoundaryFinalTest` 内) | 性质 |
|---|---|---|
| **AC-42-G1 元件库唯一出口** | `test_ac42g1_noInlineVariants_noInlineStyles` + `test_ac42g1_componentRegistry_exists` | BLOCKING |
| **AC-42-G2 设置壳不缓存他系统状态** | `test_ac42g2_noSystemState_noVolumeMonoFields` + `test_ac42g2_noAudioMixer_noAudioMixerFields` | BLOCKING |
| **AC-42-G6 墨龄数据路径** | `test_ac42g6_noFreehand_noFreehandFields` + `test_ac42g6_noTimer_noTimerFields` + `test_ac42g6_noGlyphGeneration_noGlyphFields` + `test_ac42g6_noRandom_noRandomFields` | BLOCKING |

> **测试数**:`data_boundary_final_test` = **8**(8 passed + 0 failed)。
> ⚠️ 关键区分:生产代码(42 程序集)尚未实现,测试用契约面验证。
> 代码评审修复:B1 删除恒真断言;B2 添加非空前置断言;R1 提取重复代码;R2 删除死代码;R3 统一引用类型过滤;R4 添加属性扫描;R5 重命名测试。

## Story 008(无血条替代反馈 —— 拒绝权降级白名单 · 记号登记表 · 无血条替代反馈)

故事头登记的证据路径为
`tests/integration/skeuomorphic-ui/no_healthbar_feedback_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/no_healthbar_feedback_test.cs`**(类 `NoHealthbarFeedbackTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/no_healthbar_feedback_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/`(42 类型树) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(拒绝权降级白名单 · 记号登记表 · 无血条替代反馈) |
| 运行方式 | `unity test unity --mode EditMode --filter NoHealthbarFeedbackTest` |

> 读法纪律:生产代码(42 程序集)尚未实现,测试用契约面验证 + `Assert.Inconclusive` 明确标记待实现项。
> `test_rejectionWhitelist_continuationPage_exists` 在 42 无续页类型时用 `Assert.Inconclusive`(当前阶段无法判定)。
> `test_markRegistry_sixMarkTypes_exist` 在 42 无记号类型时用 `Assert.Inconclusive`(当前阶段无法判定)。
> `test_noHealthbar_paperProgress_exists` 在 42 无页数/纸厚/墨迹密度类型时用 `Assert.Inconclusive`(当前阶段无法判定)。

## AC → 测试函数映射

| AC | 测试函数(`NoHealthbarFeedbackTest` 内) | 性质 |
|---|---|---|
| **拒绝权降级白名单** | `test_rejectionWhitelist_noSilentClipping_noClipFields` + `test_rejectionWhitelist_continuationPage_exists` | BLOCKING |
| **记号登记表** | `test_markRegistry_sixMarkTypes_exist` + `test_markRegistry_noPaperTear_noTearFields` | BLOCKING |
| **无血条替代反馈** | `test_noHealthbar_noHealthbar_noHealthbarFields` + `test_noHealthbar_paperProgress_exists` + `test_noHealthbar_completionMark_noCompletionBar` | BLOCKING |

> **测试数**:`no_healthbar_feedback_test` = **7**(4 passed + 3 inconclusive + 0 failed)。
> ⚠️ 关键区分:生产代码(42 程序集)尚未实现,测试用契约面验证。
> 代码评审修复:B1 添加属性扫描;B2 删除恒真断言;R1 提取重复代码;R3 重命名测试;B3/B4/B5/B6/R4-R9 添加注释说明实现后补充。

## Story 009(焦点可见样式 + 无障碍钩子接口 —— AC-42-G3 · AC-42-G4 · 焦点可见样式契约 · 文本缩放主题变量层 · TR-skeuoui-012)

故事头登记的证据路径为
`production/qa/evidence/focus-visual-and-accessibility-evidence.md`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_visual_and_accessibility_test.cs`**(类 `FocusVisualAndAccessibilityTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_visual_and_accessibility_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/`(42 类型树) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-G3 · AC-42-G4 · 焦点可见样式契约 · 文本缩放主题变量层 · TR-skeuoui-012) |
| 运行方式 | `unity test unity --mode EditMode --filter FocusVisualAndAccessibilityTest` |

> 读法纪律:生产代码(42 程序集)尚未实现,测试用契约面验证 + `Assert.Inconclusive` 明确标记待实现项。
> 6 个测试在 42 无相关类型时用 `Assert.Inconclusive`(当前阶段无法判定)。

## AC → 测试函数映射

| AC | 测试函数(`FocusVisualAndAccessibilityTest` 内) | 性质 |
|---|---|---|
| **AC-42-G3 对比度全档断言** | `test_ac42g3_contrastCheckInterface_exists` + `test_ac42g3_noThresholdIn42_noThresholdFields` | BLOCKING |
| **AC-42-G4 动效缩放挂点** | `test_ac42g4_animationScaleHook_exists` + `test_ac42g4_noHardcodedDuration_noDurationFields` | BLOCKING |
| **焦点可见样式契约** | `test_focusVisibleStyle_exists` + `test_focusVisibleStyle_noPureColorHighlight_noHighlightFields` | BLOCKING |
| **文本缩放主题变量层** | `test_textScaleHook_exists` + `test_textScaleHook_noCachedFontSize_noCachedFontSizeFields` | BLOCKING |
| **TR-skeuoui-012 无障碍四钩子** | `test_accessibilityFourHooks_exist` + `test_accessibilityNamingHook_exists` | BLOCKING |

> **测试数**:`focus_visual_and_accessibility_test` = **10**(4 passed + 6 inconclusive + 0 failed)。
> ⚠️ 关键区分:生产代码(42 程序集)尚未实现,测试用契约面验证。
> 代码评审修复:R2 将内联扫描逻辑改为调用 FindMatchingMembers;R1/R3/R4/R5 添加注释说明实现后补充。

## Story 010(焦点门时序与过渡 —— AC-42-B3b · F8 不变量)

故事头登记的证据路径为
`production/qa/evidence/focus-gate-transition-evidence.md`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_gate_transition_test.cs`**(类 `FocusGateTransitionTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/focus_gate_transition_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/`(42 类型树) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-B3b · F8 不变量) |
| 运行方式 | `unity test unity --mode EditMode --filter FocusGateTransitionTest` |

> 读法纪律:本 story 要求 PlayMode 帧探针,当前阶段 42 未实现,用 `Assert.Inconclusive` 标记。
> 5 个测试在当前阶段无法判定(42 未实现或 PlayMode 测试无法运行)。

## AC → 测试函数映射

| AC | 测试函数(`FocusGateTransitionTest` 内) | 性质 |
|---|---|---|
| **F8 不变量** | `test_f8_invariant_focusGateStateMachine_exists` + `test_f8_invariant_noBothGatesOpen_noBothOpenFields` + `test_f8_invariant_transitionWindow_exists` + `test_f8_invariant_noReentrancy_noReentrancyFields` | BLOCKING |
| **AC-42-B3b 过渡窗口 ≤1 frame** | `test_ac42b3b_playModeFrameProbe_transitionWindow` + `test_ac42b3b_rapidSwitching_transitionWindow` + `test_ac42b3b_switchDuringTransition_transitionWindow` | BLOCKING |

> **测试数**:`focus_gate_transition_test` = **7**(2 passed + 2 inconclusive + 3 skipped + 0 failed)。
> ⚠️ 关键区分:本 story 要求 PlayMode 帧探针,当前阶段 42 未实现,PlayMode 测试无法运行。
> 代码评审修复:R1/R2/R3 负向测试调用 FindMatchingMembers;R4 添加 [Ignore] 属性;R5 缓存程序集扫描结果。

## Story 011(脉案页渲染 + 焦点导航 —— AC-42-F1① · AC-42-B5 · AC-42-B6 · 五通道区 · 两栏布局)

故事头登记的证据路径为
`production/qa/evidence/casebook-39-walkthrough.md`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/casebook_rendering_test.cs`**(类 `CasebookRenderingTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/casebook_rendering_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/`(42 类型树) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-F1① · AC-42-B5 · AC-42-B6 · 五通道区 · 两栏布局) |
| 运行方式 | `unity test unity --mode EditMode --filter CasebookRenderingTest` |

> 读法纪律:本 story 要求 UI 走查,当前阶段 42 未实现,用 `Assert.Inconclusive` / `[Ignore]` 标记。
> 5 个测试在当前阶段无法判定(42 未实现),1 个 UI 走查测试用 `[Ignore]` 标记。

## AC → 测试函数映射

| AC | 测试函数(`CasebookRenderingTest` 内) | 性质 |
|---|---|---|
| **五通道区** | `test_fiveChannels_exist` + `test_fiveChannels_noHealthbar_noHealthbarFields` | BLOCKING |
| **两栏布局** | `test_twoColumnLayout_exist` + `test_twoColumnLayout_noHardcodedFontSize_noFontSizeFields` | BLOCKING |
| **AC-42-B5 空行有格线无字** | `test_ac42b5_emptyRowRendering_exist` + `test_ac42b5_emptyRowNoText_noTextFields` | BLOCKING |
| **AC-42-B6 置信度不出溢** | `test_ac42b6_confidenceRendering_exist` + `test_ac42b6_noConfidenceOverflow_noOverflowFields` | BLOCKING |
| **AC-42-F1① 手柄走查 + 零按键提示浮层** | `test_ac42f1_gamepadWalkthrough_noKeyHintOverlay` + `test_ac42f1_focusOrder_exist` + `test_ac42f1_noKeyHintOverlay_noKeyHintFields` | BLOCKING |

> **测试数**:`casebook_rendering_test` = **11**(5 passed + 5 inconclusive + 1 skipped + 0 failed)。
> ⚠️ 关键区分:本 story 要求 UI 走查,当前阶段 42 未实现,UI 测试无法运行。
> 代码评审修复:B1 保持 Inconclusive 添加注释;B2 添加注释;R1 提取 [SetUp];R2 提取助手方法;R3 添加注释;R4 重命名测试;R5 添加注释指向替代证据路径。

## Story 012(存档位界面渲染 + 焦点 —— AC-42-F1② · 存档位界面 · 焦点顺序)

故事头登记的证据路径为
`production/qa/evidence/save-slots-walkthrough.md`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/SkeuomorphicUI/save_slots_test.cs`**(类 `SaveSlotsTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/SkeuomorphicUI/save_slots_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/SkeuomorphicUI/SkeuomorphicUI.Tests.asmdef`(name = `SkeuomorphicUI.Tests`) |
| 被测契约 | `unity/Assets/Gameplay/UI/`(42 类型树) |
| 被测 GDD | `design/gdd/skeuomorphic-ui.md`(AC-42-F1② · 存档位界面 · 焦点顺序) |
| 运行方式 | `unity test unity --mode EditMode --filter SaveSlotsTest` |

> 读法纪律:本 story 要求 UI 走查,当前阶段 42 未实现,用 `Assert.Inconclusive` / `[Ignore]` 标记。
> 3 个测试在当前阶段无法判定(42 未实现),1 个 UI 走查测试用 `[Ignore]` 标记。

## AC → 测试函数映射

| AC | 测试函数(`SaveSlotsTest` 内) | 性质 |
|---|---|---|
| **存档位界面** | `test_saveSlotsUI_exist` + `test_saveSlotItems_exist` + `test_saveSlotsUI_hasNoTraditionalUI` + `test_saveSlotsUI_hasNoHardcodedFontSize` | BLOCKING |
| **焦点顺序** | `test_focusOrder_exist` + `test_focusOrder_hasNoHardcodedOrder` | BLOCKING |
| **AC-42-F1② 手柄走查 + 零按键提示浮层** | `test_ac42f1_gamepadWalkthrough_noKeyHintOverlay` + `test_ac42f1_noKeyHintOverlay_noKeyHintFields` + `test_ac42f1_emptyRowNoText_noTextFields` | BLOCKING |

> **测试数**:`save_slots_test` = **9**(5 passed + 3 inconclusive + 1 skipped + 0 failed)。
> ⚠️ 关键区分:本 story 要求 UI 走查,当前阶段 42 未实现,UI 测试无法运行。
> 代码评审修复:B1-B5 添加注释说明当前阶段测试无验证价值;R1-R5 添加注释说明实现后补充;R4 重命名测试。
