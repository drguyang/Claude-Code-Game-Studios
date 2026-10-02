# EditMode 全量复跑(2026-10-02【超算】batchmode)

**命令**:`Unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath unity`
**结果 XML**:`/tmp/editmode-align.xml`(本次会话临时产物,未入仓库 —— 仓内只保留本摘要)
**引擎**:Unity 6.3.24f1(6000.3.24f1)· NUnit 3.5.0.0 · CLR 4.0.30319.42000
**时长**:35.4774334 s

## 汇总

| 指标 | 值 |
|---|---|
| total | 1964 |
| passed | 1933 |
| **failed** | **1** |
| skipped | 29 |
| inconclusive | 1 |

**上一轮对比**(2026-10-01 我修复 `2abef89` 编译破损后):1834/1864 passed / 4 failed。
本轮 **1933/1964 passed / 1 failed** —— 3 项先前失败已被 emergency-procedures / PlayMode 批带走的
改动消解(见下),**无新增失败**。

## 唯一失败项(既有信息门,非 modular / world-ecozones)

- `DaYiJingCheng.Tests.Unit.Audio.PipelineGateTest.test_addressables_audioGroupSeparateFromDataCore`
  - 报文:`data-core 组必须存在(ADR-014:烘焙数据组)` / `Expected: collection containing "data-core"` /
    `But was: < "Default Local Group" >`(真身:`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs:165`)
  - 成因:Addressables 只建了「Default Local Group」,`data-core` 组从未创建(ADR-014 §五)。
    **`unity/Assets/AddressableAssetsData/` 是机器生成物、永不入仓** ⇒ 该组只在桌面工程里存在,
    故本项在【超算】**结构性不可过**。归桌面批 / ADR-014 落地轮。
  - 编译通过 ⇒ `DataPipelineBakeTest` 55/55 全绿(2026-10-01 的 2 项失败已消解)、
    `DeterminismGoldenFixturesTest` 5/5 全绿、`SchemaTypesPrimaryKeyTest` 随 foraging WIP 落地后转绿。

## 本轮取证的两个 epic(状态对齐依据)

| Fixture | 结果 | 例数 |
|---|---|---|
| `Tests.ModularBuilding.BuildSlotCatalogTest` | Passed | 7 |
| `Tests.ModularBuilding.PlaceableCheckerTest` | Passed | 10 |
| `Tests.ModularBuilding.StructureKindsTest` | Passed | 7 |
| `Tests.ModularBuilding.WorldTest` | Passed | 8 |
| `Tests.ModularBuilding.OccupancyOverlayTest` | Passed | 6 |
| `Tests.ModularBuilding.ModifiableCheckerTest` | Passed | 5 |
| `Tests.ModularBuilding.DemolishCheckerTest` | Passed | 3 |
| `Tests.ModularBuilding.RefundCalculatorTest` | Passed | 5 |
| **ModularBuilding 小计** | **全绿** | **51** |
| `Tests.WorldEcozones.WorldLatticeTest` | Passed | 42 |
| `Tests.WorldEcozones.EcozoneQueryTest` | Passed | 25 |
| `Tests.WorldEcozones.PoiStateMachineTest` | Passed | 13 |
| `Tests.WorldEcozones.ChunkActivationTest` | Passed | 7 |
| **WorldEcozones 小计** | **全绿** | **87** |

⇒ `epics/index.md` / `sprint-04.md` / `active.md` 记的「51/51」「87/87」**逐例复跑坐实**。

## 逐例明细(fixture · case · result)

| Fixture | Case | Result |
|---|---|---|
| BuildSlotCatalogTest | test_registeredSlot_returnsCorrectMask | Passed |
| BuildSlotCatalogTest | test_registeredSlot_returnsTrue | Passed |
| BuildSlotCatalogTest | test_tryGetSlot_registered_returnsTrue | Passed |
| BuildSlotCatalogTest | test_tryGetSlot_unknown_returnsFalse | Passed |
| BuildSlotCatalogTest | test_unknownPosition_notInRegion | Passed |
| BuildSlotCatalogTest | test_unknownPosition_returnsFalse | Passed |
| BuildSlotCatalogTest | test_unknownPosition_returnsZero | Passed |
| DemolishCheckerTest | test_entityOnCell_returnsEntityOnCell | Passed |
| DemolishCheckerTest | test_nonexistent_returnsNotFound | Passed |
| DemolishCheckerTest | test_validStructure_returnsSuccess | Passed |
| ModifiableCheckerTest | test_invalidOrientation_returnsTypeMismatch | Passed |
| ModifiableCheckerTest | test_newCellsOccupied_returnsNewCellsOccupied | Passed |
| ModifiableCheckerTest | test_noChange_returnsNoChange | Passed |
| ModifiableCheckerTest | test_nonexistentStructure_returnsNotFound | Passed |
| ModifiableCheckerTest | test_validOrientationChange_returnsSuccess | Passed |
| OccupancyOverlayTest | test_effectiveWalkable_bothTrue | Passed |
| OccupancyOverlayTest | test_effectiveWalkable_navAndOverlay | Passed |
| OccupancyOverlayTest | test_emptyOccupancy_allWalkable | Passed |
| OccupancyOverlayTest | test_payloadFields_allInteger | Passed |
| OccupancyOverlayTest | test_rebuildFromEvents_equalsLiveState | Passed |
| OccupancyOverlayTest | test_singleSource_noDrift | Passed |
| PlaceableCheckerTest | test_anchorInLocalOccupancy | Passed |
| PlaceableCheckerTest | test_invalidOrientation_throws | Passed |
| PlaceableCheckerTest | test_invalidSlot_returnsSlotNotFound | Passed |
| PlaceableCheckerTest | test_rotation_0_identity | Passed |
| PlaceableCheckerTest | test_rotation_180_inverts | Passed |
| PlaceableCheckerTest | test_rotation_270_swapsBack | Passed |
| PlaceableCheckerTest | test_rotation_90_swapsXY | Passed |
| PlaceableCheckerTest | test_typeMismatch_returnsTypeMismatch | Passed |
| PlaceableCheckerTest | test_unknownModule_returnsModuleNotFound | Passed |
| PlaceableCheckerTest | test_validSlotAndType_returnsSuccess | Passed |
| RefundCalculatorTest | test_refund_roundDown | Passed |
| RefundCalculatorTest | test_refundRatioOne_returnsFullCost | Passed |
| RefundCalculatorTest | test_validateRefundable_R1_alwaysPass | Passed |
| RefundCalculatorTest | test_validateRefundable_RHalf_costThreshold | Passed |
| RefundCalculatorTest | test_zeroCost_returnsZero | Passed |
| StructureKindsTest | test_modify_noChange_returnsFalse | Passed |
| StructureKindsTest | test_modify_orientation_emitsModified | Passed |
| StructureKindsTest | test_place_emitsStructurePlaced | Passed |
| StructureKindsTest | test_rebuildFromEvents_equalsLiveState | Passed |
| StructureKindsTest | test_remove_emitsStructureRemoved | Passed |
| StructureKindsTest | test_remove_nonexistent_returnsFalse | Passed |
| StructureKindsTest | test_tryGet_nonexistent_returnsFalse | Passed |
| WorldTest | test_effectiveWalkable_occupied | Passed |
| WorldTest | test_effectiveWalkable_unwalkableNav | Passed |
| WorldTest | test_effectiveWalkable_walkableAndFree | Passed |
| WorldTest | test_emptyOccupancy_allWalkable | Passed |
| WorldTest | test_getModuleAt_empty_returnsMinusOne | Passed |
| WorldTest | test_occupyThenFree_restoresWalkable | Passed |
| WorldTest | test_outOfBounds_returnsFalse | Passed |
| WorldTest | test_singleOccupancySource_consistent | Passed |
| ChunkActivationTest | test_activeChunks_pureFunction_identicalRuns | Passed |
| ChunkActivationTest | test_chunkConversion_inverse | Passed |
| ChunkActivationTest | test_chunkIndex_roundTrip | Passed |
| ChunkActivationTest | test_outOfBoundsChunk_notActive | Passed |
| ChunkActivationTest | test_streamingRadius_correctActivation | Passed |
| ChunkActivationTest | test_unloadedChunk_treatedAsBlock | Passed |
| ChunkActivationTest | test_zeroRadius_onlySelfChunk | Passed |
| EcozoneQueryTest | test_ecozone_allZones_queried | Passed |
| EcozoneQueryTest | test_ecozone_boundaryArbitration_minId | Passed |
| EcozoneQueryTest | test_ecozone_emptyRegistry_allNone | Passed |
| EcozoneQueryTest | test_ecozone_extentGuard_throwsBeyondSafeRange | Passed |
| EcozoneQueryTest | test_ecozone_externalCell_returnsNone | Passed |
| EcozoneQueryTest | test_ecozone_flatAndSteepPolygons_sameResult | Passed |
| EcozoneQueryTest | test_ecozone_getAllIds_isSortedAscending | Passed |
| EcozoneQueryTest | test_ecozone_il_noFloatOps_noYReads | Passed |
| EcozoneQueryTest | test_ecozone_ilClosure_containsCoreKernels | Passed |
| EcozoneQueryTest | test_ecozone_ilGate_negativeFixture_reportsRed | Passed |
| EcozoneQueryTest | test_ecozone_internalCell_returnsId | Passed |
| EcozoneQueryTest | test_ecozone_minId_independentOfRegistrationOrder | Passed |
| EcozoneQueryTest | test_ecozone_noneSentinel_semantics | Passed |
| EcozoneQueryTest | test_ecozone_pointOnEdge_goesToBoundaryArbitration | Passed |
| EcozoneQueryTest | test_ecozone_pureFunction_doubleRun_identical | Passed |
| EcozoneQueryTest | test_ecozone_rayThroughVertex_noDoubleCount | Passed |
| EcozoneQueryTest | test_ecozone_register_acceptsValidSharingVertex | Passed |
| EcozoneQueryTest | test_ecozone_register_rejectsCoincidentAdjacentVertices | Passed |
| EcozoneQueryTest | test_ecozone_register_rejectsDuplicateId | Passed |
| EcozoneQueryTest | test_ecozone_register_rejectsFewerThanThreeVertices | Passed |
| EcozoneQueryTest | test_ecozone_register_rejectsSelfIntersecting | Passed |
| EcozoneQueryTest | test_ecozone_register_rejectsSentinelId | Passed |
| EcozoneQueryTest | test_ecozone_singleValue_sharedCorner_minId | Passed |
| EcozoneQueryTest | test_ecozone_vertexCell_boundaryNotInterior | Passed |
| EcozoneQueryTest | test_ecozone_yComponent_noInfluence | Passed |
| PoiStateMachineTest | test_boundedEvents_atMostTwoPerPoi | Passed |
| PoiStateMachineTest | test_discoveredToResolved_succeeds | Passed |
| PoiStateMachineTest | test_eachTransfer_emitsExactlyOneEvent | Passed |
| PoiStateMachineTest | test_idempotent_alreadyAtState | Passed |
| PoiStateMachineTest | test_initialState_isUndiscovered | Passed |
| PoiStateMachineTest | test_noSecondStorage_snapshotIsReadOnly | Passed |
| PoiStateMachineTest | test_poiNotFound_returnsNotFound | Passed |
| PoiStateMachineTest | test_poiState_hasThreeValues | Passed |
| PoiStateMachineTest | test_rebuildFromEvents_equalsLiveState | Passed |
| PoiStateMachineTest | test_reverseTransfer_discoveredToUndiscovered_rejected | Passed |
| PoiStateMachineTest | test_reverseTransfer_resolvedToDiscovered_rejected | Passed |
| PoiStateMachineTest | test_skipDiscovered_undiscoveredToResolved_succeeds | Passed |
| PoiStateMachineTest | test_undiscoveredToDiscovered_succeeds | Passed |
| WorldLatticeTest | test_logicalLayerOrigin_isSingleDefinedConstant | Passed |
| WorldLatticeTest | test_logicLayerTypes_noFloatDoubleVectorOrLocalFrame | Passed |
| WorldLatticeTest | test_originConsistency_bakedOriginOffByOne_throws | Passed |
| WorldLatticeTest | test_typeGraphScan_negativeFixtures_reportRed | Passed |
| WorldLatticeTest | test_typeGraphScan_registeredSet_coversAc601NamedSurface | Passed |
| WorldLatticeTest | test_worldGeometry_constructedFromBaked_preservesSizes | Passed |
| WorldLatticeTest | test_worldGeometry_extentGuard_onLoad | Passed |
| WorldLatticeTest | test_worldGeometry_gridLengthMismatch_throws | Passed |
| WorldLatticeTest | test_worldGeometry_outOfBounds_isNotWalkable | Passed |
| WorldLatticeTest | test_worldGeometry_poiAndResource_defaultMinusOne | Passed |
| WorldLatticeTest | test_worldGeometry_toIndex_noAliasingAcrossCells | Passed |
| WorldLatticeTest | test_worldGeometry_toIndex_outOfBoundsReturnsMinusOne | Passed |
| WorldLatticeTest | test_worldGeometry_walkabilityFromBakedData_isRespected | Passed |
| WorldLatticeTest | test_worldGeometry_zeroSizeAxis_throws | Passed |
| WorldLatticeTest | test_worldLattice_baselineRowDuplicated_throws | Passed |
| WorldLatticeTest | test_worldLattice_baselineRowMissing_throws | Passed |
| WorldLatticeTest | test_worldLattice_baselineRowUnique_passes | Passed |
| WorldLatticeTest | test_worldLattice_boundaryEquality_passes | Passed |
| WorldLatticeTest | test_worldLattice_dimensionIsInexpensiveToGetWrong_guardedByBoundaryFixture | Passed |
| WorldLatticeTest | test_worldLattice_extentAtBoundary_passes | Passed |
| WorldLatticeTest | test_worldLattice_extentBeyondBoundary_throws | Passed |
| WorldLatticeTest | test_worldLattice_extentConstant_isBoundaryValue | Passed |
| WorldLatticeTest | test_worldLattice_extentMaxInt_throws | Passed |
| WorldLatticeTest | test_worldLattice_extentWithinSafeZone_passes | Passed |
| WorldLatticeTest | test_worldLattice_kAccelOrKDecelNonPositive_throws | Passed |
| WorldLatticeTest | test_worldLattice_kSpeedNegativeRow_throws | Passed |
| WorldLatticeTest | test_worldLattice_kSpeedTableEmpty_throws | Passed |
| WorldLatticeTest | test_worldLattice_kSpeedTableNull_throws | Passed |
| WorldLatticeTest | test_worldLattice_kSpeedTableReadonlyExposure_doesNotAliasCaller | Passed |
| WorldLatticeTest | test_worldLattice_kSpeedZeroRow_throws | Passed |
| WorldLatticeTest | test_worldLattice_kTerrainMax_derivedFromTable | Passed |
| WorldLatticeTest | test_worldLattice_kTerrainMax_followsTableChange | Passed |
| WorldLatticeTest | test_worldLattice_latticeBelowBound_throws | Passed |
| WorldLatticeTest | test_worldLattice_latticeBoundTracksTerrainTable | Passed |
| WorldLatticeTest | test_worldLattice_negativeFactors_throw | Passed |
| WorldLatticeTest | test_worldLattice_safetyMarginEqualsOne_throws | Passed |
| WorldLatticeTest | test_worldLattice_speedMax_derivesFromExpansion | Passed |
| WorldLatticeTest | test_worldLattice_speedMaxProduct_overflowsInt_throws | Passed |
| WorldLatticeTest | test_worldLattice_storyTc3Table_isRejectedByBaselineRule | Passed |
| WorldLatticeTest | test_worldLattice_validParams_constructs | Passed |
| WorldLatticeTest | test_worldLattice_zeroFactors_throw | Passed |
| WorldLatticeTest | test_worldPos_fieldsAreInt32 | Passed |
