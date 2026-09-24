# unit/item_database/

按系统分目录的单元测试(命名 `[system]_[feature]_test.cs`)。
逻辑类故事的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

## Story 001(FixParse 边界契约)—— 落点说明

故事头登记的证据路径(账本路径)为
`tests/unit/item_database/fix_parse_boundary_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码**。按 ADR-025 §⑤(2026-09-23 路径订正注),
实际编译落点 = **`unity/Assets/Tests/EditMode/ItemDatabase/fix_parse_boundary_test.cs`**
(装配 `Sim.Contracts.Tests`)。本目录只承载**账本与夹具**:

| 内容 | 路径 |
|---|---|
| 负向夹具(故事 QA 指定) | `fixtures/invalid_potency_float.json` |
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/ItemDatabase/fix_parse_boundary_test.cs` |

与 `tests/unit/sim/` 的既有先例一致(种子测试已由 U0 迁入 EditMode 树,根目录不留副本)。

## Story 002(Schema 类型与复合主键)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/schema_types_primary_key_test.cs`,
同 Story 001:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本目录承载账本与三个 QA 指定负向夹具:

| 内容 | 路径 |
|---|---|
| 负向夹具:复合主键重复(AC-21a-21) | `fixtures/invalid_dup_key.json` |
| 负向夹具:枚举外字面量(AC-21a-22) | `fixtures/invalid_enum.json` |
| 负向夹具:显式存储 stackable(AC-21a-59) | `fixtures/invalid_stored_stackable.json` |
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/ItemDatabase/schema_types_primary_key_test.cs` |
| 类型图违例夹具(仅测试程序集可见) | `unity/Assets/Tests/EditMode/ItemDatabase/invalid_instance_unity_ref.cs` |

**落点(unity-specialist 约束①,2026-09-23)**:schema **类型**住
`unity/Assets/Sim.Contracts/ItemDatabase/`(12 个文件,恰 = BCL);
校验/扫描**纯函数**住 `unity/Assets/Editor.Tools.Gates/ItemDbValidation.cs` 与
`PodTypeScanner.cs`(编辑期职责,不进玩家构建)。两处 asmdef 只改 `references` 数组:
Gates 增引 Sim.Contracts GUID,EditMode 增引 Gates GUID。

## Story 003(配方结算唯一求解器 F1/F2)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/recipe_settlement_solver_test.cs`,
同 Story 001/002:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本目录承载账本;本故事**无负向夹具**(QA 明文「Negative fixture: 无」—— AC-1/2/3/4/47
全为正向性质测试;AC-3 的反面 = AC-21a-9,共用 `fixtures/invalid_cap_sum.json`,执行体归 Story 005)。

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身,Logic) | `unity/Assets/Tests/EditMode/ItemDatabase/recipe_settlement_solver_test.cs` |
| 集成侧真身(AC-21a-5 / 6) | `unity/Assets/Tests/PlayMode/recipe_settlement_solver_test.cs`(见 `tests/integration/item_database/README.md`) |

**AC 覆盖映射**:

| AC | 测试函数 |
|---|---|
| AC-21a-1 | `test_outputQty_extremeNegativeModifiers_everyOutputAtLeastOne` · `test_outputQty_baseOneWithQtyMultMinRoundingToZero_stillOne` · `test_outputQty_qtyMultMinAnyPositiveValue_neverBreachesFloor` |
| AC-21a-2 | `test_outputQuality_anyInputQualityAndSkill_neverExceedsInput` · `test_outputQuality_inputOne_identityClampAtEverySkill` · `test_outputQuality_fullRetainMaxSkill_equalsInput` · `test_retain_zeroSkill_equalsRetainMin_neverZero` |
| AC-21a-3 | `test_constantTable_capSumWithinBudget_accepted` · `test_constantTable_capSumOverBudget_rejectedWithNamedValues` · `test_constantTable_negativeEnvModMax_dropsTheTerm` · `test_constantTable_allCapsZero_passesInequality` |
| AC-21a-4 | `test_sumOfModifiers_allFourPermutations_bitIdenticalRaw` · `test_solverFormula_sumOfModifiers_equalsSumOfModifiersHelper` · `test_solverFormula_permutedModifierGroups_bitIdenticalOutputs` |
| AC-21a-47 | `test_timing_singleSolveUnderOneMillisecond_advisorySmoke`(`[Category("Advisory")]` —— **不入 BLOCKING 确定性套件**) |

**AC 覆盖映射(续)—— F1 具名量 + 曲线域外守卫**(QA 复核后补;非单列某 AC,服务 AC-1/2/4 的边界):

| 目标 | 测试函数 |
|---|---|
| F1 环境合计一次性钳制 | `test_envModTotal_unboundedComponents_clampedOnceInsideFormula` |
| 曲线连续性(非两段阶跃)| `test_skillMod_curveContinuous_notTwoStep` |
| F2 实耗上取整 + EFF ≤ 1 | `test_actualConsumed_efficiencyAtMostOne_neverBelowBase` · `test_actualConsumed_nonPositiveEfficiency_throwsInsteadOfDivideByZero` |
| GDD 示范数形状对位 | `test_solve_gddWorkedExample_shapeMatchesQuantityContract` |
| 空/空引用入参 | `test_solve_emptyEntryArrays_produceEmptyOutputsNotThrow` · `test_solve_nullEntryArrays_throwsArgumentNullException` |
| 舍入模式可分辨(AC-42 交叉 Edge)| `test_outputQuality_midpointRounding_goesAwayFromZero_notTiesToEven` |
| 曲线分母 ≤ 0 具名拒(QA §4a)| `test_curveSlope_zeroSkillCap_throwsNamedNotDivideByZero` · `test_curveSlope_maxQualityOne_throwsNamedNotDivideByZero` |
| 域外输入钳制(QA §4c/4d)| `test_efficiency_skillLevelAboveCap_clampedNeverExceedsEffMax` · `test_qualityMod_inputQualityAboveMaxQuality_clampedToCap` |
| 域外输入拒(QA §4b)| `test_outputQuality_inputQualityBelowOne_throws` |
| EFF ≤ 0 守卫不随长度分叉(QA §4f)| `test_actualConsumed_guardHoldsForEmptyInputArray` |
| 溢出而非回绕(QA §4e)| `test_toInt32Checked_overflowThrowsInsteadOfWrapping` |

**测试计数**:EditMode **30** 个 `[Test]`(AC-1…4/47 + 上表补测);PlayMode **7** 个(AC-5/6)。
**执行状态:NOT-RUN**(【超算】无 Unity Editor)⇒ 全绿判据待【桌面】跑出,本 README 不代跑、不代绿。


**落点(装配决定,2026-09-24)**:求解器 / 常量表 / 入口接缝住
`unity/Assets/Sim/ItemDatabase/`(装配 **`Sim`**,`noEngineReferences: true`)——
ADR-025 §① 表把 sim 模块列在 `Sim` 职责列;`Sim.Contracts` 职责列**逐项枚举**且不含公式,
且 §④ 清单封闭性禁新增装配 ⇒ **零新 asmdef**、`Sim.asmdef` **未改动**。
求解器消费的 `Fix` / `FixParse` / `RecipeEntry` 均住 `Sim.Contracts`,`Sim` 已引用之。

**数值纪律**:常量表 `RecipeSettlementConstants` 与求解器内**零数值**(AC-21a-48)——
值经 ADR-014 烘焙产物注入(Story 008 建通路)。测试夹具值为**夹具值,非游戏数值;数值待用户**
(GDD §Tuning Knobs「默认」列留空)。

## Story 004(F4 堆叠重量与 F5 品级时间轴)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/quality_timeline_stacking_test.cs`,
同 Story 001/002/003:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本故事**无负向夹具**(QA 明文「Negative fixture: 无」/ AC 表未命名 —— 构建期拒收类夹具
`invalid_drug_offset_len.json` / `invalid_gather_char_len.json` / `invalid_axis_p0.json` /
`invalid_offset_floor.json` / `invalid_drug_char_len.json` / `invalid_weight_overflow.json`
由 QA Test Cases 点名,**执行体归 Story 006**,本故事不擅造)。

| 内容 | 路径 |
|---|---|
| 求解器(F4 堆叠/重量 + AC-64 上界) | `unity/Assets/Sim/ItemDatabase/StackingSolver.cs` |
| 求解器(F5 品级→时间轴 + AC-37/38b 谓词) | `unity/Assets/Sim/ItemDatabase/QualityTimelineSolver.cs` |
| 编译中的测试源(真身,Logic) | `unity/Assets/Tests/EditMode/ItemDatabase/quality_timeline_stacking_test.cs` |

**AC 覆盖映射**:

| AC | 测试函数 |
|---|---|
| AC-21a-32 | `test_stackKey_differentQuality_sameKey_neverMerges` · `test_stackKey_sameQuality_sameKey_merges_whenNotFull` · `test_stackKey_sameQuality_differentKey_neverMerges` · `test_stackKey_sameKey_sameQuality_differentState_neverMerges` · `test_stackKey_stackMaxOne_neverMerges` · `test_stackKey_containerInstance_neverMerges` |
| AC-21a-33 | `test_splitStack_withinCap_noOverflow_conservesTotal` · `test_splitStack_exactlyAtCap_noOverflow` · `test_splitStack_overflow_singlePartialStack` · `test_splitStack_overflow_multipleFullStacks_plusPartial` · `test_splitStack_overflow_exactDivisible_noPartial` · `test_splitStack_fullStack_add_createsAllNewInstances` · `test_splitStack_hugeAdd_multipleSegments_overflowConserved` · `test_splitStack_outOfRange_throws` · `test_splitStack_noSilentIntWrap_overflowBeyondIntMax_throws` |
| AC-21a-36 | `test_f5_axisEffective_isBasePlusOffset_rawIntegerAddition` · `test_f5_firstQuality_mapsToIndexZero` · `test_f5_lastQuality_mapsToLastIndex` · `test_f5_outOfRangeQuality_throws_runtimeGuard` · `test_f5_negativeOffset_appliesInRawDomain` |
| AC-21a-37 | `test_f5_hasQualityEffect_atLeastOneNonZero_passes` · `test_f5_hasQualityEffect_allZeroForbiddenNoPass` · `test_f5_offsetsAtOrAboveFloor_pass` · `test_f5_offsetBelowFloor_fails` · `test_f5_floorInjected_neverAssertsSpecificNumber` · `test_f5_floorNonPositive_returnsTrue_allowsNoConstraint` |
| AC-21a-38 | `test_f5_offAxisTimelines_bitIdenticalToBase` · `test_f5_noShiftWhenAxisUnset_returnsProfileFieldsBitIdentical` · `test_f5_nullableTimeline_passesThroughNullForUnsetAxes` · `test_f5_missingShiftAxisBase_throws` |
| AC-21a-38b | `test_f5_domainClamp_satisfied_whenSumPositive` · `test_f5_domainClamp_justAboveZero_passes_notTightened`(D-21-34 张力:只断言 > 0)· `test_f5_domainClamp_exactlyZero_fails` · `test_f5_domainClamp_negativeSum_fails` · `test_f5_domainClamp_allPositiveOffsets_alwaysPasses` · `test_f5_domainClamp_noOffsets_noConstraint` |
| AC-21a-64 | `test_weightedTotalFitsInt64_typicalBounds_true` · `test_weightedTotalFitsInt64_boundaryAtLimit_true` · `test_weightedTotalFitsInt64_overLimit_false` · `test_weightedTotalFitsInt64_invalidBounds_false` |

**范围边界(Out of Scope 记账)**:AC-50/50b/60/61/62/64 的**构建期硬失败执法体**(显式 throw,
装在 `Editor.Tools.Gates.*` 校验套件)= **Story 006**;本故事只落**运行期可判定的纯函数**
(求解器 + AC-37/38b 谓词算子,构建期校验消费同一谓词)。容器 children 闭包 / 容器守恒
(AC-34/58)= Story 010;堆叠动作进世界流 = 20 / Story 010。
**D-21-34(open)**:AC-38b 只断言 `> 0`,不收紧到 `≥ MIN_USABLE_HALF_LIFE`(收紧 = 改机制)。

**装配决定**:两求解器住 `unity/Assets/Sim/ItemDatabase/`(装配 **`Sim`**,
`noEngineReferences: true`,与 Story 003 同一先例)—— ADR-025 §① · 零新 asmdef。
EditMode 测试装配 `Sim.Contracts.Tests`(既有 GUID 引用集已含 Sim/Sim.Contracts)。

**测试计数**:EditMode **40** 个 `[Test]`(AC-32×6 + AC-33×9 + AC-64×4 + AC-36×5 + AC-38×4 + AC-37×6 + AC-38b×6)。
**执行状态:NOT-RUN**(【超算】无 Unity Editor)⇒ 全绿判据待【桌面】跑出,本 README 不代跑、不代绿。
