# integration/audio_system/

按系统分目录的集成测试账本路径(coding-standards §Testing Evidence by Story Type)。
**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点。

## Story 001(装配边界与 DTO 护栏 —— AC-44-B1 / B2 / B3 / B4)—— 落点说明

故事头登记的证据路径为
`tests/integration/audio_system/assembly_boundary_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs`**

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/assembly_boundary_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测门(b5) | `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs`(`RunAudioBoundary` / `CheckAudioAssemblyIl` / `CheckPresentationReferenceSet` / `CheckAudioSourceText`) |
| 被测守卫 | `unity/Assets/Editor.Tools.Gates/PresentationDtoGuard.cs`(`Scan` / `AssertNoDiseaseId`) |
| 运行方式 | `unity test unity --mode EditMode --filter AssemblyBoundary` |

> 承 `tests/integration/item_database/README.md` 同一先例:账本路径登记 + 真身落
> `unity/Assets/Tests/` 树,两处以本 README 互链。

## AC → 测试函数映射

| AC | 测试函数(`AssemblyBoundaryTest` 内) | 性质 |
|---|---|---|
| **AC-44-B1 ①**(类型级 IL 扫描) | `test_ilScan_negativeFixtureInTestAssembly_flagged`(端到端负例:对测试装配产物跑同一谓词)· `test_fullGate_currentTree_zeroErrors`(生产产物正例) | BLOCKING |
| **AC-44-B1 ①b**(源文本层:using / nameof / 反射字符串) | `test_sourceScan_forbiddenUsingAndTypeName_reportsRed` · `test_sourceScan_commentMention_notFlagged` · `test_sourceScan_reflectionString_reportsRed` | BLOCKING |
| **AC-44-B1 ②**(引用集 = 登记集 ∪ 引擎白名单 ∪ 基线) | `test_referenceSet_newDrift_reportsRedWithThreeSegments` · `test_referenceSet_baselineSim_warnsNotRed` · `test_referenceSet_simCodecBlacklist_reportsRed` · `test_referenceSet_engineAndBcl_pass` | BLOCKING |
| **AC-44-B1 ③**(DOTS 黑名单) | `test_referenceSet_dotsFamily_reportsRedUnconditionally` | BLOCKING |
| **AC-44-B1 三段总门** | `test_fullGate_currentTree_zeroErrors` | BLOCKING |
| **AC-44-B2**(守卫递归扫描 `AudioCueDto`) | `test_dtoGuard_audioCueDto_noDiseaseField`(正)· `test_dtoGuard_nestedPrivateField_reportsRed`(负①嵌套+私有)· `test_dtoGuard_listElementField_reportsRed`(负②泛型元素)· `test_dtoGuard_baseClassPrivateField_reportsRed`(负③基类私有)· `test_dtoGuard_assertThrows_onViolation` | — |
| **AC-44-B3**(入口白名单 / 公开接口仅 `IAudioCueSink`) | `test_entryPoints_production44_noUnknownEntry`(正)· `test_entryPoints_vitalsDtoParam_reportsRed`(负)· `test_entryPoints_simSignature_reportsRed`(负)· `test_entryPoints_wrongInterface_reportsRed`(负)· `test_audioCueSink_onlyIntoNoStateOut`(契约只入不出) | — |
| **AC-44-B4**(字段类型白名单) | `test_fieldTypes_production44_withinWhitelist`(正,44 空集平凡成立)· `test_fieldTypes_simStateAndEngineNonAudioField_reportsRed`(负) | — |

## 负例夹具落位(不污染生产扫描面)

- **B1 端到端**:文件末命名空间块 `DaYiJingCheng.Gameplay.Presentation.Audio` 下的
  `IlScanNegativeFixture`(住**测试装配** `Sim.Contracts.Tests`)—— 零新增 asmdef
  (b3 Manifest 封闭性会拒),真编译产物真 IL;对 `Library/ScriptAssemblies/Sim.Contracts.Tests.dll`
  跑与生产同一谓词 `CheckAudioAssemblyIl`。
- **B1 源文本**:合成文本喂 `CheckAudioSourceText`(免把生产源写坏)。
- **B2**:测试类私有夹具(不在 44 前缀下)。**B3/B4**:显式传入类型列表(纯函数面)。

## Story 003(混音拓扑与快照纪律 —— AC-44-C1 / AC-44-E3 / 注册表 AC②③)—— 落点说明

故事头 Test Evidence 登记的证据路径 =
`unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs`(本 story 即钉该路径,本文件即真身;
账本互链 = 本 README)。

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs`(类 `MixerTopologyTest`) |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测门 | `unity/Assets/Editor.Tools.Gates/MixerTopologyGates.cs`(`ValidateMixerTopology` + 逐判据子方法 + `ScanTransitionCallsites`) |
| 运行期注册表(单一出处) | `unity/Assets/Gameplay.Presentation/Audio/MixerRegistry.cs`(七总线 / `bus_volume_*` 7 员 / 快照五员) |
| 被测实现(44 表现层) | `unity/Assets/Gameplay.Presentation/Audio/SnapshotDirector.cs` · `ReverbPresetSwitcher.cs` · `PlayerBusVolumeInitializer.cs` |
| 资产生成器(batch) | `unity/Assets/Editor.Tools.Gates/MixerAssetGenerator.cs`(`Create` / `Discover`,产物 = `Assets/Audio/DaYiJingCheng.mixer`) |
| 夹具 | `tests/integration/audio_system/fixtures/*.yaml`(见下表) |
| GDD 对账源 | `design/gdd/audio-system.md` §UI Requirements 注册表(~:896,语义源) |
| 运行方式 | `unity test unity --mode EditMode --filter MixerTopology` |

> 读法纪律(承 Story 001/002):`.mixer` 是 Force Text YAML ⇒ **手写逐行状态机扫描**
> (零正则首配、零第三方解析器);IL 扫描 = **Mono.Cecil + ReadingMode.Deferred**
> (SRM 在本工程编译不过,`AssemblyGates.cs:195-200` 实测)—— 故事原文「System.Reflection.Metadata」为过时口径。
> 层级断言**沿 `m_MasterGroup → m_Children` 的 fileID 树**,不用 `m_Name` 计数冒充层级。

## AC → 测试函数映射

| AC | 测试函数(`MixerTopologyTest` 内) | 性质 |
|---|---|---|
| **AC-44-C1 正例**(全部调用点 ∈ 白名单) | `test_transitionCallsites_currentTree_allWhitelisted`(含空集不豁免下限断言) | BLOCKING |
| **AC-44-C1 负例**(体征 handler 调 `TransitionTo(DialogueFocus)` ⇒ 红) | `test_transitionCallsites_stateHandlerFixture_red`(端到端:对测试装配产物跑同一谓词) | BLOCKING |
| **AC-44-C1 扫描面守卫**(产物缺失 = 红) | `test_transitionCallsites_missingAssembly_red` | — |
| **AC-44-C1 DialogueFocus 半边**(仅玩家对话入口) | `test_dialogueFocus_callsiteFieldOwnership_onlyPlayerEntry`(按快照字段 `_dialogueFocus` 归属断言) | BLOCKING |
| **AC-44-C1 白名单反向**(死条目 = 腐化) | `test_whitelistRules_allExistInPresentationAssembly` | — |
| **AC-44-C1 源文本层**(反射字符串旁路) | `test_transitionReflectionStringLiteral_reportsRed` | — |
| **AC-44-E3 ①**(七总线组齐备) | `test_mixerYaml_validTopology_zeroErrors` · `test_mixerYaml_missingBusGroup_reportsRed`(负) | BLOCKING |
| **AC-44-E3 ②**(send 清单单一出处,新增未登记 = 红) | `test_mixerYaml_unregisteredSend_reportsRed`(负)+ 正例含于 `test_mixerYaml_validTopology_zeroErrors` | BLOCKING |
| **AC-44-E3 ③**(reverb 切换所有者 = 44) | `test_reverbSwitcher_ownerIsAudio44Namespace`(正,反射)· `test_reverbSwitcher_foreignOwner_reportsRed`(负,纯函数) | — |
| **真资产**(API 层 + YAML 层 + 初始化器;缺失即红) | `test_realMixerAsset_presentApiLayerAndValid`(`AssetDatabase.LoadAssetAtPath` + `FindMatchingGroups` + `FindSnapshot` + gate) | BLOCKING |
| **注册表 AC②**(快照捕获 ∩ 玩家 exposed = ∅) | `test_snapshotCaptures_disjointFromPlayerExposed_zeroErrors`(正)· `test_snapshotCapture_playerVolume_reportsRed`(负:注入 `bus_volume_music`) | BLOCKING |
| **两级组结构**(每总线拆两级) | `test_singleLevelBus_reportsRed`(缺 `duck_music`,负) | — |
| **快照五员表**(GDD §States) | `test_snapshotRoster_missingDialogueFocus_reportsRed`(负,手术移除文档) | — |
| **两级组运行期纪律**(禁 `ClearFloat`) | `test_clearFloat_playerParamsNeverCleared`(合成负例 + 当前 44 源树正例) | — |
| **注册表 AC③**(计数 = 7 含 Master) | `test_busVolumeRegistry_sevenIncludingMaster`(正)· `test_busVolumeRegistry_missingMasterRow_reportsRed`(负,喂 6 条) | BLOCKING |
| **注册表 AC③ 对账**(GDD 7 行 ↔ 常量 7 员) | `test_gddBusRegistry_reconcilesWithConstants`(正,读真 GDD)· `test_gddBusRegistry_missingRow_reportsRed`(负,合成行集) | — |

## 夹具清单(`fixtures/`)

| 夹具 | 判据 / 单因违例 |
|---|---|
| `valid_mixer_topology.yaml` | 正例:七总线两级组 + 1 条登记 send + 快照五员 + 7 exposed + duck 捕获 |
| `invalid_missing_bus.yaml` | AC-44-E3 ①:`Stethoscope` 组改名 ⇒ 七总线缺 |
| `invalid_unregistered_send.yaml` | AC-44-E3 ②:注入 `send_extra_air`(未登记) |
| `invalid_snapshot_captures_player_volume.yaml` | 注册表 AC②:`StethoscopeFocus` 捕获 `bus_volume_music`(fileID 2102) |
| `invalid_single_level_bus.yaml` | 两级组:`Music` 缺 `duck_music` 子组 |

> 负向夹具各含**单因违例**;`TransitionNegativeFixture`(体征 handler)住真身文件末命名空间块
> `DaYiJingCheng.Gameplay.Presentation.Audio`(借测试装配编译,不污染生产扫描面 —— 承 Story 001 先例)。

## 预期红项(2026-09-26 交付时点,非假绿)

- `test_realMixerAsset_presentApiLayerAndValid`:`Assets/Audio/DaYiJingCheng.mixer` 尚未生成 ——
  跑 `unity build <project> --target StandaloneLinux64 --executeMethod
  DaYiJingCheng.EditorTools.Gates.MixerAssetGenerator.Create` 生成后转绿。
- 若生成后 YAML 层仍红:`MixerTopologyGates` 的字段口径(`m_ValueMap target` / `m_Sends` 条目)
  是**登记过的对齐点** —— 以生成产物为黄金样例回改提取函数(夹具与判据语义不变),
  生成器 batch 日志已自带 `[YAML对齐]` 判词。

---

## Story 010(素材管线与事件表烘焙门 —— AC-44-D4 / D5 / D6 / 听诊窗预载)—— 落点说明

故事头登记的证据路径为
`tests/integration/audio_system/pipeline_gate_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码** ⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`**(类 `PipelineGateTest`)

> ⚠️ **S14-6(2026-10-16)重写记录**:原 11 测的**四个自造替身全部删除**
> (`FakePreloadGate` / `GroupSeparabilityPredicate` / `ExtractFieldName` / 内联 `File.Exists`),
> 且删除了原 `:212-228` 的**文件系统写入副作用**(`Directory.CreateDirectory(repoRoot/assets/audio)`
> + `File.WriteAllText` + `File.Delete`)—— 那个副作用是 `assets/audio/` 目录存在的**唯一**原因。
> 现 **52 测**,全部打真生产对象;夹具自身「零文件写入」由 `test_discipline_noFilesystemWrites` 自证。
>
> ⚠️ **S14-6 补口(2026-10-17)再扩记录**:52 → **61 测**。新增 **4 条菜单硬失败夹具**
> (`test_d5_menu_groupMissing_throwsHard` · `test_dcore_menu_{settingsMissing,noCooked,groupMissing}_throwsHard`)
> + **4 条适配器夹具**(`test_preload_adapter_{mapsAllFourUnityLoadStates,nullClip_isFailedNotLoaded,
> nullResolver_isHardFailure,unresolvableCue_blocksGate}`);**并删掉 1 条缺口哨兵** ——
> `test_preload_realAudioClipLoadState_pendingPlayModeHalf` **已按其自身口径转成真断言**
> (适配器已落 + PlayMode 半边已跑),**不是被当回归修掉**。
> 证据:`production/qa/evidence/impl-td-audio-01-fix-2026-10-17.md` §十。
>
> ⚠️ **悬项收口轮(2026-10-17 第二轮)再扩记录**:61 → **68 测**。新增 **7 条**,分两组:
> **Play Mode 门 2 条**(`test_playModeScript_isAssetDatabase` —— 真设置断言;
> `test_playModeScript_predicate_nullOrMiss_returnsMinusOne` —— 纯谓词定义域)
> + **Import Load Type 门 5 条**(`test_importSettings_requiredLoadType_matchesGddTable` ·
> `test_importSettings_boundaries_areInclusiveExclusiveAsSpecified` ·
> `test_importSettings_musicPrefix_overridesNumericTier` ·
> `test_importSettings_realMetas_matchRequiredTier` ·
> `test_importSettings_unreadableInput_isViolationNotSilentSkip`)。
> 被测生产件 = `AudioImportSettingsGate.cs`(**新**,判据源 GDD §OQ-44-2 附 `:1208-1247`)
> + `DataBakeMenu.EnsureAssetDatabasePlayMode`(**由 private 升 public**,并拆出纯谓词
> `FindAssetDatabaseBuilderIndex`)。
> 证据:`production/qa/evidence/impl-td-audio-01-fix-2026-10-17.md` §十二 / §十四。
>
> ⚠️ **同轮自查再 +1 ⇒ 69 测**(2026-10-17):复核本轮自己新落的生产件时发现
> `AudioImportSettingsGate.Validate` 用了 `Directory.GetFiles(dir, "*.wav")` —— 而该 pattern
> **在 Linux 上区分大小写**(独立 net8.0 探针实测:目录含 `lower.wav`/`UPPER.WAV`/`Mixed.Wav` 时
> **只返回 `lower.wav`**)。CI 平台正是 Linux(ADR-012)⇒ 素材批若交付 `.WAV`,门会**少报违例**。
> **修** = 纯谓词 `IsWavName`(`OrdinalIgnoreCase`)+ `Validate` 枚举 `"*"` 再过滤;
> **夹具** = `test_importSettings_wavExtension_isCaseInsensitiveAcrossPlatforms`(**不碰文件系统**);
> **反证** = spec 新增 **F5**(定义域收窄类 ⇒ 反证强制)⇒ 恰转红。
> ⚠️ **与 story-017 的 W-2 是同一缺陷的两处** —— `AudioAssetGroupGates.cs:197` 同型,
> 归 **story-017**(`AC-44-D5b`);本处是本门自带的,**两处不合并记账**。
> 证据:同上 §12.5。

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 烘焙管线(D4/D6) | `unity/Assets/Editor.Tools.Bake/AudioEventTableBinder.cs` · `AudioEventTableCookedWriter.cs` · `AudioEventTableBake.cs` · `AudioAssetExistence.cs` · `AudioSettingsRegistryCookedWriter.cs` · `AudioSettingsRegistryBake.cs` |
| 组区分门(D5) | `unity/Assets/Editor.Tools.Gates/AudioAssetGroupGates.cs`(纯谓词 `ValidateEntries` + 真设置 `ValidateSettings`) |
| 预载门 | `unity/Assets/Gameplay.Presentation/Audio/StethoscopePreloadGate.cs` |
| 运行期载体 + codec | `unity/Assets/Gameplay.Presentation/Audio/AudioEventDataSet.cs` · `AudioEventCookedCodec.cs` · `Settings/AudioSettingsDataSet.cs` · `Settings/AudioSettingsRegistryCookedCodec.cs` |
| 接线 | `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs`(两条 audio 支路)· `unity/Assets/Gameplay.Boot/BootRoot.cs`(`ApplyAudioSettingsProvider`)· `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`(三菜单项) |
| 素材本体(占位) | `unity/Assets/Audio/*.wav`(5 个;2044 B = 44 B RIFF/WAVE 头 + 2000 B 全零 PCM(单声道 44100 Hz / 16 bit / 1000 样本 ≈ 22.7 ms 静音)) |
| 运行方式 | `unity test unity --mode EditMode --filter DaYiJingCheng.Tests.Unit.Audio` |
| **机器前置** | ⚠️ **2026-10-17 订正**:`AddressableAssetsData/` 的**组定义已随仓库入库**(TD-audio-01a 收窄 `.gitignore`)⇒「首次须建组」的前提**不再成立**;**设置缺席 = 断言报错,不跳过**(组缺失 = 入库文件被删/损坏,恢复靠 `git checkout`) |
| **机器前置 · 预建 catalog** | ⚠️ **2026-10-17 新发现(TD-audio-01f)**:PlayMode 解析的是 `unity/Library/com.unity.addressables/aa/<platform>/catalog.bin`(**gitignored · 机器本地**)⇒ **改过组条目后须跑菜单「构建 Addressables 内容(BuildPlayerContent)」**,否则 `AddressablesDataProvider.Preload()` 抛 `InvalidKeyException`,**9 条 PlayMode 启动序测试全红** |
| **PlayMode 件(真 `loadState`)** | `unity/Assets/Tests/PlayMode/Audio/stethoscope_preload_loadstate_test.cs`(**3 测**,类 `StethoscopePreloadLoadStateTest`)—— 真导入 clip 的 `Unloaded→Loaded` 过渡 + 门翻面 |

## AC → 测试函数映射(`PipelineGateTest` 内)

| AC | 测试函数 | 性质 |
|---|---|---|
| **AC-44-D4** 装配零 Newtonsoft | `test_d4_presentationAssembly_noNewtonsoftRef`(含 `GetReferencedAssemblies()` 扫描键非空守卫)· `test_d4_noJsonReaderRefs_inProductionAudioTypes` · `test_d4_noFixParseRefs_inProductionAudioTypes` | BLOCKING |
| **AC-44-D4** 运行期只解字节 | `test_d4_runtimeCodec_decodesBakedBytesOnly` · `test_d4_settingsCodec_decodesBakedBytesOnly` | BLOCKING |
| **AC-44-D4** 头/载荷分叉守卫 | `test_d4_runtimeCodec_payloadSchemaDivergedFromHeader_rejected` · `test_d4_settingsCodec_payloadSchemaDivergedFromHeader_rejected`(断言**钉报文**) | — |
| **AC-44-D5** 真设置(正向) | `test_d5_realSettings_audioAssetsSeparateFromDataCore` · `test_d5_audioAssetsOnDisk_nonEmpty`(扫描键非空守卫) | BLOCKING |
| **AC-44-D5** 纯谓词(负向) | `test_d5_pure_audioAssetInDataCoreGroup_rejected` · `test_d5_pure_audioAssetUnregistered_rejected` · `test_d5_pure_strayAudioEntryInDataCore_rejected` · `test_d5_pure_cleanEntries_pass`(对照面)· `test_d5_pure_nullEntries_rejected` · `test_d5_audioGroupName_isNotDataCore` | BLOCKING |
| **AC-44-D6** 真种子(正向) | `test_d6_realSeed_bakesWithNoMissingAsset` | BLOCKING |
| **AC-44-D6** 负向 + 谓词 | `test_d6_missingAsset_rejectedWithAggregateThrow` · `test_d6_missingAssetError_namesFileAndCue` · `test_d6_nullAssetRoot_isHardFailure` · `test_d6_existencePredicate_existingAndMissing` · `test_d6_missing_isOrderPreservingAndDeduped` · `test_d6_nullAssetList_yieldsNoMissing` | BLOCKING |
| **听诊窗预载** | `test_preload_notLoaded_blocks` · `test_preload_allLoaded_allows` · `test_preload_partialLoaded_blocks` · `test_preload_failedState_blocks` · `test_preload_emptyCueSet_isHardFailure` · `test_preload_nullQuery_isHardFailure` · `test_preload_fromTable_derivesStethoscopeFamily` · `test_preload_stethoscopeBusName_matchesMixerRegistry` · `test_preload_handoverMux_{gateBlocks,gateAllows,noGate}` | BLOCKING |
| **听诊窗预载 · 适配器** | `test_preload_adapter_mapsAllFourUnityLoadStates` · `test_preload_adapter_nullClip_isFailedNotLoaded`(null clip **≠ 已载**)· `test_preload_adapter_nullResolver_isHardFailure` · `test_preload_adapter_unresolvableCue_blocksGate` | BLOCKING |
| **菜单硬失败(W-5② + 01e 扩面)** | `test_d5_menu_groupMissing_throwsHard` · `test_dcore_menu_{settingsMissing,noCooked,groupMissing}_throwsHard`(均**先验后改**:抛后设置零改动) | BLOCKING |
| ~~**听诊窗预载 · NOT-RUN**~~ | ~~`test_preload_realAudioClipLoadState_pendingPlayModeHalf`~~ ⇒ **✅ 2026-10-17 已按其自身口径转真断言**;真 clip 半边归 `unity/Assets/Tests/PlayMode/Audio/stethoscope_preload_loadstate_test.cs` | ✅ **已跑** |
| **烘焙确定性 / 往返 / 头契约** | `test_bake_sameSeedTwice_bitIdentical` · `test_bake_settings_sameSeedTwice_bitIdentical` · `test_bake_rowOrderIsCueOrdinal` · `test_bake_rowOutOfRange_throws` · `test_bake_headerContract_{audioAndSettings,rejectsBrokenHeaders}` · `test_bake_materialize_crossSideMatches` · `test_bake_settings_payloadIndependentOfSourceRowOrder` · `test_bake_settings_fixtureFromFallbackPassesGate` | BLOCKING |
| **接线** | `test_wiring_providerInjection_overridesFallback` · `test_wiring_providerAbsentOrEmpty_usesFallback` · `test_wiring_bakedRows_matchFallbackValues` · `test_wiring_addresses_singleSourceOfTruth` · `test_wiring_bakeMenuEntriesExist` | — |
| **接线 · BootRoot 真身** | `test_bootroot_injection_whenPreloaded_rowsComeFromProvider`(供给 1 行合成集 ⇒ 「注入了没」可判)· `test_bootroot_noInjection_whenNotPreloaded_rowsStayFallback` · `test_bootroot_noInjection_whenProviderNull_rowsStayFallback` | BLOCKING |
| **夹具纪律自证** | `test_discipline_noFilesystemWrites`(扫本文件源码,注释剥离) | — |
| **机器前置 · Play Mode 门**(TD-audio-01f) | `test_playModeScript_isAssetDatabase`(真设置:`ActivePlayModeDataBuilderIndex` **须等于**谓词求得的索引)· `test_playModeScript_predicate_nullOrMiss_returnsMinusOne`(纯谓词定义域:null 设置 / 名单无匹配 ⇒ **`-1`**,非 `0`) | BLOCKING |
| **素材 Import Load Type 门**(OQ-44-2) | `test_importSettings_requiredLoadType_matchesGddTable`(GDD §附 5 行逐行,**边界值豁免**)· `test_importSettings_boundaries_areInclusiveExclusiveAsSpecified`(512 KB / 5 s **闭**、4 MB / 30 s **开**)· `test_importSettings_musicPrefix_overridesNumericTier`(前缀**优先于**数值档;`StartsWith` 非 `Contains`)· `test_importSettings_realMetas_matchRequiredTier`(**真环境扫描** 5 个 wav × `.meta`)· `test_importSettings_unreadableInput_isViolationNotSilentSkip`(**负面**:不可读 ⇒ **报违例**,不静默跳过) · `test_importSettings_wavExtension_isCaseInsensitiveAcrossPlatforms`(**跨平台大小写**:三形态须识别 / 伪后缀须拒;反证 F5) | BLOCKING |

> **证伪轮(2026-10-16 · BLOCKING)**:11 例逐条「改 → 跑 → 还原(同命令内)+ SHA-256 逐字节校验」,
> 全部**转红**。逐例记录见 `production/qa/evidence/impl-s14-6-asset-pipeline-2026-10-16.md` §3。
>
> **证伪轮(2026-10-17 补口 · BLOCKING)**:6 处变异(M1–M6)一次施加 ⇒ EditMode
> `total=61 passed=51 failed=10` —— **10 条红全部可归因**(M1×1 / M2×1 / M3×1 / M4×2 / M6×5),
> **零意外红**;其余 51 条照绿。PlayMode 半边(M6):`3 跑 2 红 1 绿`,**不依赖门的适配器断言正确保持绿**
> ⇒ 红是**定向**的。三件源码还原后 `SHA-256 逐字节校验通过`。逐例见
> `production/qa/evidence/impl-td-audio-01-fix-2026-10-17.md` §10.3。
> ⚠️ **C4(D5(b) 漏网条目判据)首轮 0 红 = 弱判别面(BLOCKING)**:`test_d5_pure_strayAudioEntryInDataCore_rejected`
> 的期望集成员**未合法登记** ⇒ 断言被 (a) 半边的误报满足,与 (b) 是否生效无关。
> 修 = 夹具形态(期望集成员合法登记在 `audio` 组)+ **报文钉定**(`Does.Contain("orphan.wav")` /
> `Does.Not.Contain("sfx_breath_base_loop_small")`)⇒ 复跑 1 红。
>
> **证伪轮(2026-10-17 悬项收口轮 · BLOCKING · 已**固化为可复用驱动**)**:
> 由 `tools/falsify/unity_falsify.py` 驱动(**取代**此前的 `/tmp` 一次性 shell),
> 4 个变异**全部 `exactRed: true`**(红集须**恰好**等于预期)、**逐个施加逐个还原不叠变异**:
>
> | 变异 | 停用什么 | 恰转红 |
> |---|---|---|
> | F1-musicPrefix | `RequiredLoadType` 的 `music_` 前缀覆盖 | 3 条(`…musicPrefix_overridesNumericTier` / `…realMetas_matchRequiredTier` / `…matchesGddTable`) |
> | F2-smallTierInclusive | 小档闭区间 `<=` → `<` | 1 条(`…boundaries_areInclusiveExclusiveAsSpecified`) |
> | F3-playModePredicateMinusOne | `FindAssetDatabaseBuilderIndex` 的 `-1` → `0` | 1 条(`…predicate_nullOrMiss_returnsMinusOne`) |
> | F4-unreadableMetaSilentZero | 不可读 meta ⇒ 静默归零 | 1 条(`…unreadableInput_isViolationNotSilentSkip`) |
>
> `FALSIFY_EXIT=0`;还原后 **SHA-256 逐字节校验通过**;受保护路径
> (`unity/Assets/AddressableAssetsData` + `unity/Assets/Audio`)**指纹未变**。
> 逐例见 `production/qa/evidence/impl-td-audio-01-fix-2026-10-17.md` §12.3。
> ⚠️ **驱动边界(不隐藏)**:它守的是**路径**不是**语义** —— 污染写到守卫路径**之外**即看不见;
> 且只保证**被检出**,不保证**不被写入**(防写入仍需夹具侧 `AssertThrowsAndLeaveNoTrace`)。
> **教训**:负向夹具只写「非空 / 抛了」不钉「是谁报的」,就会被同一函数内**别的**分支满足
> (S14-5 第 ⑦ 例同型,本 sprint 第二次)。

---

## Story 017(音频门加固 —— AC-44-D5b / D5c / D5d)—— 落点说明

故事头登记的证据路径 = `tests/integration/audio_system/pipeline_gate_test.cs`(账本约定),
**真身与 story-010 同一文件**:`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(类 `PipelineGateTest`)。
本 story **不新立测试文件**,只在既有文件内**追加** —— 既有测**零删改**。

> ⚠️ **S17(2026-10-17)计数演进记录** —— **两个口径别混**:
> ① **源码**(HEAD `d21e43a` `pipeline_gate_test.cs`)= 落盘前 **52** 测(D5/D6 = **17**:D5 10 · D6 7),
>    落盘后 **74** 测(D5/D6 = **23**);
> ② **XML**(`s19d-full-final.xml` → `s22-full.xml`)= `PipelineGateTest` **69 → 74**,逐 fullname
>    **+5 / −0**(`d5b` ×3 · `d5c` ×1 · `d5d` ×1)⇒ 全量 `3630 → 3635` 的 `+5` 对账成立。
> 源码侧多出的 5 条(`d5b` ×3 + `d5c` + `d5_menu_groupMissing_throwsHard`)**早已在源码里但未被 `s19d` 采集**。
> 新增 6 条(源码口径):
> `test_d5b_audioAssetName_extensionCaseInsensitive` ·
> `test_d5b_filterAudioNames_threeCaseFormsAllKept` ·
> `test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob` ·
> `test_d5c_validateEntries_withinLineAndComplexityBudget` ·
> `test_d5d_errorSetIsByteIdentical` · `test_d5_menu_groupMissing_throwsHard`。
> 被测生产件 = `Editor.Tools.Gates/AudioAssetGroupGates.cs`(**改**:新谓词 `IsAudioAssetName` +
> 新映射纯函数 `FilterAudioNames` + `ValidateEntries` 拆三 + `AudioAssetsOnDisk` 简化)
> + `Editor.Tools.Bake/AudioImportSettingsGate.cs`(**改**:`IsWavName` 改委派,单一出处)。
> 证据:`production/qa/evidence/impl-s17-gate-hardening-2026-10-17.md`。

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(类 `PipelineGateTest`) |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测门(组区分 / W-2 / W-3) | `unity/Assets/Editor.Tools.Gates/AudioAssetGroupGates.cs`(`IsAudioAssetName` · `FilterAudioNames` · `ValidateEntries` + 两私有半 · `AudioAssetsOnDisk`) |
| 被测门(导入档,委派方) | `unity/Assets/Editor.Tools.Bake/AudioImportSettingsGate.cs`(`IsWavName` ⇒ **委派**到 `Gates`) |
| 运行方式 | `unity test unity --mode EditMode --filter PipelineGateTest` |

### AC → 测试函数映射(`PipelineGateTest` 内,本 story 新增部分)

| AC | 测试函数 | 性质 |
|---|---|---|
| **AC-44-D5b**(扩展名过滤跨平台一致) | `test_d5b_audioAssetName_extensionCaseInsensitive`(纯谓词:三形态识别 + 伪后缀拒 + **委派非漂移**循环)· `test_d5b_filterAudioNames_threeCaseFormsAllKept`(**映射结果**直断三形态全留 —— QA 用例的字面要求)· `test_d5b_audioAssetsOnDisk_filtersViaPredicateNotGlob`(**源码扫描**:枚举用 `"*"` + 走纯函数,**禁** `"*" + AudioAssetExtension` glob) | BLOCKING |
| **AC-44-D5c**(≤40 行 / CC<10) | `test_d5c_validateEntries_withinLineAndComplexityBudget`(**自校准**计数:先量被拆方法自身,再断主方法 ≤40 行 ∧ CC<10 ∧ **两个私有半各自 ≤40 行**) | BLOCKING |
| **AC-44-D5d**(错误集逐字不变) | `test_d5d_errorSetIsByteIdentical`(**黄金串表**:6 例,钉**条数 + 逐字文本 + 顺序**,含「两半同时触发 ⇒ (a) 在前、(b) 在后」的次序断言) | BLOCKING |

> **为什么 D5b 要三条而不是一条**:首版只钉了**谓词**与**调用点源码**,对「映射结果」只有
> **复合论证**(谓词对 ∧ 调用点用了谓词)—— **不是** QA 用例字面要求的「两者**均被返回**」。
> 补法**不是**往盘上造 `.WAV` 副本:副本会同时进 D5 期望集与 D6 存在性判据(**假红**),
> 且违夹具「零文件写入」纪律(`test_discipline_noFilesystemWrites` 自证)⇒
> 把映射半边抽为**纯函数** `FilterAudioNames`,夹具注入路径直断。

### 证伪轮(S17 · BLOCKING · 10 变异,全部 `exactRed: true`)

| 变异 | 停用什么 | 期望红 | 实红 |
|---|---|---|---|
| F1 | `music_` 语义档覆盖 | 3 | **3** |
| F2 | 小档闭区间 `<=` → `<` | 1 | **1** |
| F3 | `FindAssetDatabaseBuilderIndex` 的 `-1` → `0` | 1 | **1** |
| F4 | 不可读 `.meta` 静默归零 | 1 | **1** |
| **F5** | `IsAudioAssetName` 的 `OrdinalIgnoreCase`(**W-2 修复点**) | 3 | **3** |
| **F6** | `AudioAssetsOnDisk` 枚举回退 glob(**W-2 调用点**) | 1 | **1** |
| **F7** | `ValidateEntries` 行数预算(**W-3**) | 1 | **1** |
| **F8** | `ValidateEntries` 圈复杂度预算(**W-3**) | 1 | **1** |
| **F9** | 错误报文文案漂移(**AC-44-D5d**) | 1 | **1** |
| **F10** | 映射半边跳过谓词(**单一出处**) | 4 | **4** |

`FALSIFY_EXIT = 0`;还原后 **SHA-256 逐字节校验通过**;受保护路径
(`unity/Assets/AddressableAssetsData` + `unity/Assets/Audio`)**指纹未变**。逐例见
`production/qa/evidence/impl-s17-gate-hardening-2026-10-17.md` §三。

> **F5 红 3 条 = 「单一出处」的可观测形态**:判据只有一条实现(`Gates.IsAudioAssetName`),
> `Bake.IsWavName` 委派 ⇒ 停用 `OrdinalIgnoreCase`,**谓词侧 / 映射侧 / 委派侧同时翻**。
> ⚠️ 该变异的锚点**本轮迁移过**(旧锚 `AudioImportSettingsGate.cs:258` 已随委派化消失);
> 锚点不搬 ⇒ 改的是死代码 ⇒ **静默 0 红**。
> **F6 是 F5 的补集**:只把**枚举**换回 glob,前三条仍全绿(素材目录恰好只有小写 `.wav`),
> **只有源码扫描那条看得见** —— 这就是它存在的理由。
