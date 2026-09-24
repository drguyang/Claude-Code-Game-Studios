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
**执行状态:✅ VERIFIED 2026-09-24 桌面** —— EditMode + PlayMode 全绿;同日 Story 006 收官批后
PlayMode **12 全绿**(含 AC-21a-6 白名单条目④ `ConservationSolver.cs` 回归确认)。


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
本目录承载账本与**五个**构建期负向夹具。

**⚠️ 归属订正(2026-09-24)**:此前本段写「执行体归 Story 006」—— **该口径已作废**。
story-004 的 AC 清单自列 AC-50/50b/60/61/62(五条均在),且其 Implementation Notes 明文
「执法体统一形态 = 构建期断言(AC-37/38b/**50/50b/60/61/62**/64 全部显式 `throw`)」;
Story 006/007 的 Out of Scope 亦反向指向 004(「Story 004:axis 数组长度与地板」)。
⇒ 五条执法体**就地落在本故事**,不住 006。落点 = `Editor.Tools.Gates/DrugProfileGates.cs`
(承 Story 002 先例:构建期校验纯函数住 Gates,不进玩家构建)。

| 内容 | 路径 |
|---|---|
| 求解器(F4 堆叠/重量 + AC-64 上界) | `unity/Assets/Sim/ItemDatabase/StackingSolver.cs` |
| 求解器(F5 品级→时间轴 + AC-37/38b 谓词) | `unity/Assets/Sim/ItemDatabase/QualityTimelineSolver.cs` |
| 构建期校验纯函数(AC-50/50b/60/61/62 执法体) | `unity/Assets/Editor.Tools.Gates/DrugProfileGates.cs` |
| 编译中的测试源(真身,Logic) | `unity/Assets/Tests/EditMode/ItemDatabase/quality_timeline_stacking_test.cs` |
| 负向夹具:axis 档位表长度 ≠ MAX_QUALITY(AC-21a-50) | `fixtures/invalid_drug_offset_len.json` |
| 负向夹具:原料侧品级修饰表长度 ≠ MAX_QUALITY(AC-21a-50b) | `fixtures/invalid_gather_char_len.json` |
| 负向夹具:P0 quality_axis 非 half_life(AC-21a-60) | `fixtures/invalid_axis_p0.json` |
| 负向夹具:非零档 \|offset\| < 可感知地板(AC-21a-61) | `fixtures/invalid_offset_floor.json` |
| 负向夹具:成药侧品级修饰表长度 ≠ MAX_QUALITY(AC-21a-62) | `fixtures/invalid_drug_char_len.json` |

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
| AC-21a-50 | `test_axisOffsetLength_fixtureLengthMismatch_rejected` · `test_axisOffsetLength_exactMaxQuality_accepted` · `test_axisOffsetLength_emptyTable_acceptedP0Nullable` · `test_axisOffsetLength_bothDirections_rejected` |
| AC-21a-50b | `test_gatherQualityCharacter_fixtureLengthMismatch_rejected` · `test_gatherQualityCharacter_emptyAndExactLength_accepted` · `test_gatherQualityCharacter_lengthOne_whenMaxAboveOne_rejected` |
| AC-21a-60 | `test_p0QualityAxis_fixtureNonHalfLife_rejected` · `test_p0QualityAxis_halfLife_accepted_andP1aValuesRejected` · `test_p0QualityAxis_null_acceptedNoF5Effect` |
| AC-21a-61 | `test_perceptibleFloor_fixtureBelowFloor_rejected` · `test_perceptibleFloor_zeroOffsets_exemptAndAtFloor_passes` · `test_perceptibleFloor_negativeOffsetBelowFloor_rejected` · `test_perceptibleFloor_nonPositiveFloorOrEmptyTable_noConstraint` · `test_perceptibleFloor_neverAssertsSpecificFloorValue` |
| AC-21a-62 | `test_drugQualityCharacter_fixtureLengthMismatch_rejected` · `test_drugQualityCharacter_emptyAndExactLength_accepted` · `test_drugQualityCharacter_bothPathsDoNotCross` |

**范围边界(Out of Scope 记账)**:容器 children 闭包 / 容器守恒(AC-34/58)= Story 010;
堆叠动作进世界流 = 20 / Story 010;守恒律门(AC-39/65)= Story 005;
烘焙管线接线(五条执法体的**调用方**)= Story 008(DrugProfileGates 只提供纯函数,
聚合非空列表后 throw 由 008 执行)。
**D-21-34(open)**:AC-38b 只断言 `> 0`,不收紧到 `≥ MIN_USABLE_HALF_LIFE`(收紧 = 改机制)。

**装配决定**:两求解器住 `unity/Assets/Sim/ItemDatabase/`(装配 **`Sim`**,
`noEngineReferences: true`,与 Story 003 同一先例)—— ADR-025 §① · 零新 asmdef。
五条执法体住 `unity/Assets/Editor.Tools.Gates/`(装配 `Editor.Tools.Gates`,
`includePlatforms: ["Editor"]`,不进构建);其 asmdef **增引 Sim GUID** ——
AC-21a-61 的通过/失败由 `QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor` **单一实现**裁定
(执法体不复制公式体),故须见 Sim。AssemblyGates b2 只对 Sim/Sim.Contracts/Sim.Codec
断言引擎引用集,**不约束 Gates** ⇒ 增引安全。
EditMode 测试装配 `Sim.Contracts.Tests`(既有 GUID 引用集已含 Sim/Sim.Contracts/Gates)。

**测试计数**:EditMode **64** 个 `[Test]`(AC-32×6 + AC-33×9 + AC-64×4 + AC-36×5 + AC-38×4 +
AC-37×6 + AC-38b×6 + 复核补测 + **AC-50×4 + AC-50b×3 + AC-60×3 + AC-61×5 + AC-62×3**)。
**执行状态:✅ VERIFIED 2026-09-24 桌面** —— EditMode **239 全绿**(Story 001/002/003 回归 175 +
本故事 64);前批 221 → 本批 239,`DrugProfileGates.cs` 经 Unity 生成 `.meta` 后装配解析通过。

## Story 005(守恒律构建期与运行期门)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/conservation_law_gates_test.cs`,
同 Story 001/002/003/004:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本目录承载账本与**三个**构建期负向夹具。

| 内容 | 路径 |
|---|---|
| 运行期算术原语 + 三谓词(AC-8/39/65 单一实现) | `unity/Assets/Sim/ItemDatabase/ConservationSolver.cs` |
| 构建期校验纯函数(AC-8/40/56/65 执法体) | `unity/Assets/Editor.Tools.Gates/ConservationGates.cs` |
| 编译中的测试源(真身,Logic) | `unity/Assets/Tests/EditMode/ItemDatabase/conservation_law_gates_test.cs` |
| 负向夹具:聚合守恒上界(GDD 反例一,AC-21a-8) | `fixtures/invalid_conservation.json` |
| 负向夹具:逐条极值式(聚合过/极值红,AC-21a-65) | `fixtures/invalid_conservation_perline.json` |
| 负向夹具:EFF_MIN 下端 = 0(AC-21a-56) | `fixtures/invalid_eff_range.json` |

**AC 覆盖映射**:

| AC | 测试函数 |
|---|---|
| AC-21a-8 | `test_aggregateConservation_gddCounterExampleOne_rejected` · `test_aggregateConservation_exactEquality_accepted` · `test_aggregateConservation_effMaxAboveOne_rejectedAndEffMaxGateMerged` · `test_aggregateConservation_multiOutputMixedWeights_rejectedWhenSumExceeds` · `test_aggregateConservation_effMaxBelowOne_shrinksRightSide_rejected` · `test_aggregateConservation_emptyOrNullSide_rejected` · `test_aggregateConservation_weightLookupNonPositiveOrMultiplierNonPositive_rejected` |
| AC-21a-39 | `test_runtimeInvariant_effAndQtyMultiplierSweep_alwaysHolds`(等级 0…SKILL_CAP × 乘子 8 步整数遍历)· `test_runtimeInvariant_zeroLossPoint_effEqualsEffMax_takesEquality` · `test_runtimeInvariant_actualMultiplierBreachesAggregateFixture_violated` · `test_runtimeInvariant_effOrEffMaxNonPositive_rejectedWithoutThrow` · `test_runtimeInvariant_nullWeightFunctionOrEmptySide_rejected` |
| AC-21a-40 | `test_effMax_aboveOne_rejected` · `test_effMax_exactlyOne_accepted_zeroLoss` · `test_effMax_belowOne_accepted` · `test_effMaxAndEffMin_bothIllegal_eachGateNamesItsOwnAc` |
| AC-21a-56 | `test_effMin_fixtureZero_rejected` · `test_effMin_negative_rejected` · `test_effMin_aboveEffMax_rejected_codeConstructedSiblingCase`(代码构造,夹具 _note 已记)· `test_effMin_equalEffMax_positive_accepted_notTightened` · `test_effMin_zeroAndAboveMax_twoNamedErrors` |
| AC-21a-65 | `test_perLineExtreme_fixtureAggregatePassesButPerLineRejected`(**D-21-32 中间带必须红**)· `test_perLineExtreme_midpointRounding_goesAwayFromZeroNotTiesToEven` · `test_perLineExtreme_exactEquality_accepted` · `test_perLineExtreme_floorMaxOneSubdomain_stricterThanAggregate_passes` · `test_perLineExtreme_multipleInputsCeilAccumulation_rejected` · `test_perLineExtreme_effMaxBelowOne_enlargesConsumedSide_passes` · `test_perLineExtreme_effMaxNonPositive_premiseUnusableNamedErrorWithoutThrow` · `test_perLineExtreme_gateVerdict_equalsSolverPredicate` |
| AC-21a-64 前提 | `test_tryMultiply_overflowingProduct_rejectedWithoutWrap` · `test_tryMultiply_negativeOrZeroOperands_guard` |

**范围边界(Out of Scope 记账)**:常量表 cap 和与 QTY_MULT 下界(AC-21a-3/9/10/11/12/13)
及其余负向夹具 = Story 006/007;烘焙管线接线(四条执法体的**调用方**) = Story 008
(ConservationGates 只提供纯函数,聚合非空列表后 throw 由 008 执行);容器 children 闭包 / 容器守恒
= Story 010;`Fix.cs` / `FixParse.cs` / Story 003 三件与 Story 004 全部文件本故事零改动。
**两式分工(D-21-32)**:聚合式(AC-8)= 必要非充分,极值式(AC-65)= **唯一硬门**;
逐条极值式直接消费运行期 `OutputQty` / `ActualConsumed` ⇒ 构建期与运行期**同一实现**,
「两式结论不一致」结构性不可能(测试 `test_perLineExtreme_gateVerdict_equalsSolverPredicate` 自证同号)。

**装配决定**:运行期算术住 `unity/Assets/Sim/ItemDatabase/`(装配 **`Sim`**,
`noEngineReferences: true`,与 Story 003/004 同一先例);执法体住
`unity/Assets/Editor.Tools.Gates/`(装配 `Editor.Tools.Gates`,
`includePlatforms: ["Editor"]`,不进构建)—— 其 asmdef **已引 Sim GUID**(Story 004 加),
故本故事**零 asmdef 改动、零新 asmdef**(ADR-025 §④ 清单封闭)。
EditMode 测试装配 `Sim.Contracts.Tests` 既有 GUID 引用集已含 Sim / Sim.Contracts / Gates,同样零改动。
**数值纪律**:两源文件零调参字面量(AC-21a-48)—— `EFF_*` / `QTY_MULT_*` 一律经
`RecipeSettlementConstants` 的 PascalCase 属性读;权重经调用方注入的纯函数;
夹具数字照录 GDD 反例一/二原文(规格自带,非新造),其余均为**夹具值,非游戏平衡值**。

**测试计数**:EditMode **31** 个 `[Test]`(AC-8×7 + AC-39×5 + AC-40×4 + AC-56×5 + AC-65×8 + TryMultiply×2)。
**执行状态:✅ VERIFIED 2026-09-24 桌面** —— EditMode **270 全绿**(Story 001/002/003/004 回归 239 +
本故事 31);前批 239 → 本批 270(新增 31 测),`ConservationSolver.cs` / `ConservationGates.cs` /
测试文件经 Unity 生成 `.meta` 后装配解析通过。

## Story 006(配方表写入期校验套件)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/recipe_validation_fixtures_test.cs`,
同 Story 001/002/003/004/005:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本目录承载账本与**十一**个构建期负向夹具(与故事 QA Negative fixture 行逐一对应)。

| 内容 | 路径 |
|---|---|
| 构建期校验纯函数(11 条 AC 执法体) | `unity/Assets/Editor.Tools.Gates/RecipeValidationGates.cs` |
| 编译中的测试源(真身,Logic) | `unity/Assets/Tests/EditMode/ItemDatabase/recipe_validation_fixtures_test.cs` |
| 负向夹具:outputs 逐条 qty ≤ 0(AC-21a-7) | `fixtures/invalid_base_qty.json` |
| 负向夹具:常量表 cap 和超限(AC-21a-9) | `fixtures/invalid_cap_sum.json` |
| 负向夹具:QTY_MULT 区间为空(AC-21a-10) | `fixtures/invalid_qty_range.json` |
| 负向夹具:RETAIN 三条件(AC-21a-11,单文件三子记录) | `fixtures/invalid_retain.json` |
| 负向夹具:ENV_MOD 区间倒置(AC-21a-12) | `fixtures/invalid_env_range.json` |
| 负向夹具:配方项 qty ≤ 0 双侧 + 空侧(AC-21a-16) | `fixtures/invalid_recipe_qty.json` |
| 负向夹具:item_key 外键悬空(AC-21a-17) | `fixtures/invalid_recipe_fk.json` |
| 负向夹具:duration_ticks ≤ 0(AC-21a-18) | `fixtures/invalid_duration.json` |
| 负向夹具:skill_gate 越界(AC-21a-19) | `fixtures/invalid_skill_gate.json` |
| 负向夹具:min_quality 越界(AC-21a-20) | `fixtures/invalid_min_quality.json` |
| 负向夹具:owner 缺失/null/枚举外(AC-21a-66) | `fixtures/invalid_recipe_owner.json` |

**AC 覆盖映射**:

| AC | 测试函数 |
|---|---|
| AC-21a-7 | `test_outputQty_fixtureNonPositive_rejected` · `test_outputQty_singleInvalidNegativeEntry_rejected` · `test_outputQty_multipleInvalidEntries_eachNamed` · `test_outputQty_allPositive_accepted` |
| AC-21a-9 | `test_capSum_fixtureEnvModPushesOverLimit_rejected`(主案 = 仅 EnvModMax 推过界,原稿漏 EnvMod 的回归点)· `test_capSum_exactBoundary_accepted` · `test_capSum_nonPositiveEnvModCountedAsZero_accepted` · `test_capSum_gateVerdict_equalsSelfConsistentPredicate_sameCriterionAsAc3`(门/谓词同号) |
| AC-21a-10 | `test_qtyRange_fixtureEqualBounds_rejected`(含等号拒)· `test_qtyRange_minAboveMax_rejected` · `test_qtyRange_differByOneRawUnit_accepted` · `test_qtyRange_minBelowMax_accepted` |
| AC-21a-11 | `test_retain_fixtureMinAboveMax_rejected`(条件一)· `test_retain_fixtureMaxAboveOne_rejected`(条件二)· `test_retain_fixtureMinNonPositive_rejected`(条件三)· `test_retain_maxExactlyOne_accepted` · `test_retain_minEqualsMax_accepted` |
| AC-21a-12 | `test_envRange_fixtureInverted_rejected` · `test_envRange_equalBounds_accepted`(只拒 `>`,相等过)· `test_envRange_allNegative_accepted` · `test_envRange_crossingZero_accepted` |
| AC-21a-16 | `test_recipeQty_fixtureInputsZero_rejected` · `test_recipeQty_outputsNegative_rejected` · `test_recipeQty_emptyInputsOrEmptyOutputs_rejected`(空侧拒,GDD :759/:760)· `test_recipeQty_allPositiveBothSides_accepted` |
| AC-21a-17 | `test_fk_fixtureBaseExistsButStateDangling_rejected`(base 在而 state 不成条目)· `test_fk_danglingBaseId_rejected` · `test_fk_outputsSideDangling_rejected` · `test_fk_crossBaseRecipe_bothSidesExist_accepted` · `test_fk_caseSensitiveBaseId_variantCasingDangling_rejected` |
| AC-21a-18 | `test_duration_fixtureZero_rejected` · `test_duration_negative_rejected` · `test_duration_singleTick_accepted`(=1 过;只判 >0) |
| AC-21a-19 | `test_skillGate_fixtureNegative_rejected` · `test_skillGate_aboveSkillCap_rejected`(边界 = `FixtureSkillCap + 1` 拼,随常量表走)· `test_skillGate_zero_accepted` · `test_skillGate_atSkillCap_accepted` |
| AC-21a-20 | `test_minQuality_fixtureZero_rejected` · `test_minQuality_aboveMaxQuality_rejected`(边界 = `FixtureMaxQuality + 1` 拼)· `test_minQuality_one_accepted` · `test_minQuality_atMaxQuality_accepted` |
| AC-21a-66 | `test_ownerPartition_fixtureMissingNullAndOutOfSet_rejected`(缺失/null/枚举外三条同拒)· `test_ownerPartition_nullLiteralOnly_rejected` · `test_ownerPartition_caseMisspelledLiteral_rejected` · `test_ownerPartition_threeSubsets_unionEqualsFullTablePairwiseDisjoint` · `test_ownerPartition_emptySubset_accepted` · `test_ownerPartition_indexPartitionMutuallyExclusive_regardlessOfRecipeId` |

**范围边界(Out of Scope 记账)**:AC-24(state 通路符合性)= Story 007;烘焙管线接线
(11 条执法体的**调用方**) = Story 008(RecipeValidationGates 只提供纯函数,聚合非空列表后
throw 由 008 执行 —— 本文件零 throw);守恒律与 EFF 区间(AC-8/39/40/56/65)= Story 005;
axis 长度与地板(AC-50/50b/61/62)= Story 004;recipe_id 唯一性/枚举闭合/存储 stackable
(AC-21/22/59)= Story 002;`Fix.cs` / `FixParse.cs` / Story 001…005 全部文件本故事零改动。
**单一实现(承重纪律)**:AC-9 算术**委托** `RecipeSettlementConstantTableValidator.IsSelfConsistent`
(Story 005 产物,禁在门里重抄不等式);AC-66 枚举解析**委托** `ItemDbValidation.TryParseRecipeOwner`
(Story 002 产物,禁另写 switch/Enum.TryParse)。

**装配决定**:执法体住 `unity/Assets/Editor.Tools.Gates/`(装配 **`Editor.Tools.Gates`**,
`includePlatforms: ["Editor"]`,不进构建)—— 其 asmdef **已引 Sim + Sim.Contracts GUID**
(Story 002/004 加),故本故事**零 asmdef 改动、零新 asmdef**(ADR-025 §④ 清单封闭)。
EditMode 测试装配 `Sim.Contracts.Tests` 既有 GUID 引用集已含 Sim / Sim.Contracts / Gates,同样零改动。
**数值纪律**:两源文件零调参字面量(AC-21a-48)—— `SkillCap` / `MaxQuality` 一律经
`RecipeSettlementConstants` 的 PascalCase 属性读;测试边界用 `FixtureSkillCap` / `FixtureMaxQuality`
常量拼接,**不裸写 60 / 5**;夹具数字均为**夹具值,非游戏平衡值**。

**测试计数**:EditMode **47** 个 `[Test]`(AC-7×4 + AC-9×4 + AC-10×4 + AC-11×5 + AC-12×4 +
AC-16×4 + AC-17×5 + AC-18×3 + AC-19×4 + AC-20×4 + AC-66×6)。
**执行状态:✅ VERIFIED 2026-09-24 桌面** —— EditMode **317 全绿**(前批 270 + 本批 47)。

## Story 007(物品表写入期校验套件)—— 落点说明

证据账本路径登记为 `tests/unit/item_database/item_validation_fixtures_test.cs`,
同 Story 001…006:**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 测试真身落 EditMode 树。
本目录承载账本与**七**件构建期负向夹具(与故事 QA Negative fixture 行逐一对应;
AC-25 指名两件:`invalid_injury_fk.json` + `injury_set_drift.json` —— 后者不以 `invalid_` 开头是 GDD 原文)。

| 内容 | 路径 |
|---|---|
| 构建期校验纯函数(6 条 AC 执法体,AC-25 拆 a/b 两门 = 7 方法) | `unity/Assets/Editor.Tools.Gates/ItemValidationGates.cs` |
| 编译中的测试源(真身,Logic) | `unity/Assets/Tests/EditMode/ItemDatabase/item_validation_fixtures_test.cs` |
| 负向夹具:MAX_QUALITY < 2 或非整数(AC-21a-13) | `fixtures/invalid_max_quality.json` |
| 负向夹具:quality_distribution 支撑越界(AC-21a-14) | `fixtures/invalid_quality_dist.json` |
| 负向夹具:stack_max/weight 越界 + Fix 形式 "1/2"(AC-21a-15) | `fixtures/invalid_stack_weight.json` |
| 负向夹具:P1a 泄漏 honey_fried / dry_fried / 非空 tcm_profile(AC-21a-23) | `fixtures/invalid_p1a_leak.json` |
| 负向夹具:state 对未在 legal_transitions 声明(AC-21a-24) | `fixtures/invalid_transition.json` |
| 负向夹具:weapon 类别门 + 外键悬空(AC-21a-25a) | `fixtures/invalid_injury_fk.json` |
| 负向夹具:inflicts ⊉ maps 集合漂移(AC-21a-25b) | `fixtures/injury_set_drift.json` |

**AC 覆盖映射**:

| AC | 门方法 | 测试函数 |
|---|---|---|
| AC-21a-13 | `ValidateMaxQuality(raw string)` | `test_maxQuality_fixtureBelowTwo_rejected` · `test_maxQuality_fixtureZero_rejected` · `test_maxQuality_fixtureFloatString_rejected` · `test_maxQuality_stringFormInteger_accepted` · `test_maxQuality_exactlyTwo_accepted` |
| AC-21a-14 | `ValidateQualityDistribution(IReadOnlyList<int>, int maxQuality)` | `test_qualityDist_fixturePointZero_rejected` · `test_qualityDist_pointAboveMax_rejected` · `test_qualityDist_singleOutlierAmongValid_rejected` · `test_qualityDist_zeroWeightSlotStillCounts_rejected` · `test_qualityDist_boundaryInclusive_accepted` · `test_qualityDist_maxQualityLowered_rerunRejects` |
| AC-21a-15 | `ValidateStackWeight(raw string, raw string)` | `test_stackWeight_fixtureStackMaxZero_rejected` · `test_stackWeight_fixtureWeightZero_rejected` · `test_stackWeight_fixtureWeightNegative_rejected` · `test_stackWeight_fixtureWeightFixForm_rejected` · `test_stackWeight_fixtureWeightFloat_rejected` · `test_stackWeight_stackMaxOne_accepted` · `test_stackWeight_weightOne_accepted` |
| AC-21a-23 | `ValidateP0Narrowing(raw state, raw tcm 块体)` | `test_p0Narrowing_fixtureHoneyFried_rejected` · `test_p0Narrowing_fixtureDryFried_rejected` · `test_p0Narrowing_fixtureTcmProfileNonNull_rejected` · `test_p0Narrowing_tcmPlaceholderAllNull_accepted` · `test_p0Narrowing_p0EnumStateTcmAbsent_accepted` |
| AC-21a-24 | `ValidateTransition(声明 raw 串[], typed ProcessingTransition[])` | `test_transition_fixtureUndeclaredPair_rejected` · `test_transition_nonAdjacentUndeclared_rejected` · `test_transition_emptyLegalTransitions_rejected` · `test_transition_crossBase_checksSourceItemDeclarations_rejected` · `test_transition_declaredPair_accepted` · `test_transition_declaredButUnused_accepted` · `test_transition_nonAdjacentExplicitlyDeclared_accepted` |
| AC-21a-25a | `ValidateInjuryBinding(category, 集合, knownIds)` | `test_injuryBinding_fixtureWeaponMissingField_rejected` · `test_injuryBinding_materialWithField_rejected` · `test_injuryBinding_fixtureDanglingInjuryId_rejected` · `test_injuryBinding_weaponValidArray_accepted` · `test_injuryBinding_weaponSingleValueForm_accepted` · `test_injuryBinding_materialWithoutField_accepted` |
| AC-21a-25b | `ValidateInjurySetSuperset(inflicts, maps)` | `test_injurySetDrift_fixtureMapsValueOutsideInflicts_rejected` · `test_injurySetDrift_setsExactlyEqual_accepted` |

**签名决定(raw vs typed,ADVISORY)**:AC-13/15 收 **raw string** —— QA 必须验 `"3.5"` / `"1/2"` /
`1.5` 这类**类型错误拒收**,typed int 入口在绑定层就已先拒,门无法独立执法(与
`FindStoredStackableKeys` 收 raw keys 同型);AC-23 收 raw state 字面量 + raw tcm 块体
(占位全 null / 含非 null 值的区分只能在 raw 层);AC-24 声明侧收 raw 串、boundary 收 typed
`ProcessingTransition[]`;AC-14/25 收 typed(枚举闭合归 AC-22,夹具不含非法字面量)。
**AC-24 箭头格式(GDD 未定字面格式)**:单点定义于 `ParseDeclaredTransitions`,接受
`"raw>dried"` 与 `"raw→dried"`(U+2192)—— 若数据轮/GDD 后续裁定其他格式,只改这一处;
`processing_state` / `category` 字面解析**委托** `ItemDbValidation.TryParseProcessingState` /
`TryParseItemCategory`(Story 002 产物,单一实现,禁另写 switch)。
**AC-14 权重 0 档仍计支撑**:门只收支撑点索引不收权重(分布形状归 17,结构体未建)——
「零权重档照传索引」是**调用方义务**,已写入门 doc-comment,测试以夹具 `zero_weight_slot` 案验证。

**范围边界(Out of Scope 记账)**:枚举闭合本身(AC-21/22)= Story 002;axis 长度与地板
(AC-50/50b/60/61/62)= Story 004;配方表侧夹具(AC-7~20/66)= Story 006;管线执行本套校验的
落点(调用方接线 + 聚合后 throw)= Story 008;伤情语义(逐次命中归 25 的 maps_to_injury 判定)
不在本故事 —— 只断存在性 + 类别门 + 集合 ⊇;反向漂移(25 删动作)按 QA 注记归 25,不测;
`ItemDef.cs` / `ProcessingState.cs` 等 doc-comment 里残留的「归 Story 006」字样 = Story 002 期
陈旧注记(编排器可选清理项),本故事未触碰。
**9 侧 injury 枚举 / 25 侧动作表未建** ⇒ `known_injury_ids` / `maps_to_injury` 均为
**注入参数 + 夹具替身**(系统真实数据落地后由 Story 008 接线传入,门不硬编码名单)。

**装配决定**:执法体住 `unity/Assets/Editor.Tools.Gates/`(装配 **`Editor.Tools.Gates`**,
`includePlatforms: ["Editor"]`,不进构建)—— 其 asmdef **已引 Sim + Sim.Contracts GUID**
(Story 002/004 加),故本故事**零 asmdef 改动、零新 asmdef**(ADR-025 §④ 清单封闭)。
EditMode 测试装配 `Sim.Contracts.Tests` 既有 GUID 引用集已含 Sim / Sim.Contracts / Gates,同样零改动。
**数值纪律**:两源文件零调参字面量(AC-21a-48 扫描 NONE)—— 规则阈值(MAX_QUALITY ≥ 2、
stack_max ≥ 1、weight > 0)是规则本身可写字面量;平衡值(MAX_QUALITY 具体档数等)一律注入参数;
测试边界用 `FixtureMaxQuality` 常量拼,**不裸写 5**;夹具数字均为**夹具值,不是游戏平衡值**。

**测试计数**:EditMode **38** 个 `[Test]`(AC-13×5 + AC-14×6 + AC-15×7 + AC-23×5 + AC-24×7 + AC-25×8)。
**执行状态:✅ VERIFIED 2026-09-24 桌面** —— EditMode **355 全绿**(前批 317 + 本批 38)。
过程插曲:首跑 354 绿 / 1 红(`test_tuningKnobIdentifiers_hardcodedInSourceCode_none` 报
`ItemValidationGates.cs` 旋钮「weight」裸局部变量),`64a2149` 改名 `parsedWeight` 后转绿;
`.meta` ×2 由桌面 `711dc93` 补提交。
