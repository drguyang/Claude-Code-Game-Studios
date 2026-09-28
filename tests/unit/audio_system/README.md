# unit/audio_system/

按系统分目录的单元测试(命名 `test_[scenario]_[expected]`)。
音频系统逻辑类故事的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点;
`.cs` 真身落 `unity/Assets/Tests/EditMode/Audio/`,本 README 记真身与 AC → 测映射
(承 `tests/integration/audio_system/README.md` / `tests/unit/item_database/` 同一先例)。

## Story 002(音频事件表 schema 与白名单门 —— AC-44-09 / AC-44-D9 / 规则 1–9)

故事头登记的证据路径为
`tests/unit/audio_system/event_table_gate_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs`**(类 `EventTableGateTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测门 | `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs`(`Validate` + 逐规则子方法) |
| 真种子(正例) | `assets/data/audio_events.json`(仓库根,不被 Unity 导入) |
| 夹具 | `tests/unit/audio_system/fixtures/*.json`(见下表) |
| 运行方式 | `unity test unity --mode EditMode --filter EventTableGate` |

> 读法纪律:玩家构建零 JSON 解析器 ⇒ **测试侧同样手写抽取**(引号态 + `{}`/`[]` 深度配对的结构扫描,
> **零正则首配**、零 Newtonsoft —— 正则首配会让 `rows[0]` 代表全表、`-?\d+` 把 0.95 截成 0,两者都是假绿/假红源)。
> 承 `tests/unit/item_database/` 的手写读取先例;生产侧 stage-1 词法 = `JsonStage1Lexer`(Story 010 绑定层接入)。
>
> ⚠️ **本门不查 `assets[]` 素材存在性(NOT-RUN,归 Story 010 D6)** —— 正例只证 schema / 白名单侧。

## AC → 测试函数映射

| AC | 测试函数(`EventTableGateTest` 内) | 性质 |
|---|---|---|
| **AC-44-09 正例**(合法表绿) | `test_validMinimalTable_zeroErrors` · `test_realSeedAudioEvents_zeroErrors` | BLOCKING |
| **AC-44-09 负向 ①**(Sting 类别) | `test_stingCategory_reportsRed` | BLOCKING |
| **AC-44-09 负向 ②**(类别合法 + `VitalsCrossed`) | `test_legalCategoryForbiddenTrigger_reportsRed` · `test_forbiddenTriggerSet_allRejected`(禁止集全字面量) | BLOCKING |
| **AC-44-09 负向 ③**(第三负向:空表 / 缺必需 cue) | `test_emptyTable_reportsRed` · `test_missingRequiredCue_reportsRed` | BLOCKING |
| **AC-44-09 配对夹具**(规则 6 混搭) | `test_crossPairCategoryTrigger_reportsRed` · `test_musicLayerPairedWithPlayerAction_reportsRed` · `test_pairingTable_closedSetBoundary` | BLOCKING |
| **AC-44-09 NOT-RUN 守卫**(字段缺失 = 报错退出) | `test_missingTriggerSource_reportsNotRunGuard` · `test_missingWhitelistCategory_reportsNotRunGuard` · `test_rowsFieldMissing_reportsNotRunGuard` | BLOCKING |
| **AC-44-D9**(每行三字段齐;三连 = 缺失 / 非整数 / 不匹配) | `test_missingRowSchemaVersion_reportsRed` · `test_rowSchemaVersionNonInteger_reportsRed` · `test_rowSchemaVersionMismatchedWithTopLevel_reportsRed` · `test_missingTopLevelSchemaVersion_reportsNotRunGuard` | — |
| **三层白名单**(未知键 = 硬失败,ADR-014 §三) | `test_unknownTopLevelKey_reportsRed` · `test_unknownRowKey_reportsRed` · `test_presentKeysMissing_reportsNotRunGuard` · tier_map 恰 6 列四测(下一行) | — |
| **规则 5**(tier_map 三键六列 / per-row 禁滤波列) | `test_tierParamsFilterColumn_reportsRed`(**嵌套形状** · 2026-09-26 审查修) · `test_tierParamsFlatShape_reportsRed`(**扁平形状** · 同批) · `test_tierMapMissingTierKey_reportsRed` · `test_tierMapMissingColumn_reportsRed` · `test_tierMapUnknownColumn_reportsRed` · `test_tierMapMissing_reportsNotRunGuard` · `test_tierMapSixColumnsAllTiers_accepted` | — |
| **规则 8 + AC-44-14**(clock_ref 闭合 / 相位窗) | `test_danglingClockRef_reportsRed` · `test_clockRefTargetNotBaseLayer_reportsRed` · `test_adventitiousRowMissingClockRefAndPolicy_reportsNotRunGuard` · `test_triggerPhaseOutOfWindow_reportsRed`(**QA 场景:trigger_phase = 0.5,2026-09-26 改判**) · `test_triggerPhaseNonNumericLiteral_reportsRed` · `test_triggerPhaseAboveInspireWindow_reportsRed` · `test_jitterPushesWindowOutOfInspireSegment_reportsRed` · `test_missingTriggerPhase_reportsNotRunGuard` | — |
| **编码防线 ②**(分数 → 绝对相位唯一映射) | `test_inspirePhaseMapping_windowMapsInsideInspireSegment` · `test_inspirePhaseMapping_outOfWindowPhaseBeforeLateInspireWindow` · `test_inspirePhaseMapping_knobChangeKeepsRelativePosition`(被测 = `Gameplay.Presentation/Audio/InspirePhaseMapping.cs`) | — |
| **xfade_ms 存在性 / 类型**(数值断言归 Story 012) | `test_musicRowMissingXfade_reportsRed` · `test_musicRowNonPositiveXfade_reportsRed` · `test_nonMusicRowNonIntegerXfade_reportsRed` | — |
| **subtitle_text 键集**(AC-44-15 [A] 半) | `test_subtitleMissingOnVoiceCue_reportsRed` · `test_subtitleEmptyOnVoiceCue_reportsRed` · `test_narrationCueSubtitleCovered_reportsRedWhenMissing` · `test_nonSubtitleCue_withoutSubtitle_accepted` | — |
| **2026-09-26 审查补测**(6 REC + qa 缺口) | `test_topLevelSchemaVersionNonInteger_reportsRed`(顶层非整数) · `test_rule8_adventitiousAssetEntry_catchesRenamedCue`(规则 8 第四入口 `assets[]`) · `test_cueDuplicate_reportsRed`(**规则 9** cue 唯一) · `test_rule8_loopFalse_reportsRed`(`loop ≠ true`) · `test_forbiddenTriggerSet_caseSensitivity`(禁止集大小写) · `test_truncatedParse_sabotagesWhitelist`(扫描器截断哨兵) | — |
| **行身份守卫**(规则 3 补 · 2026-09-26 复审) | `test_rowMissingCue_reportsNotRunGuard`(行缺 `cue`) · `test_rowNullElement_reportsNotRunGuard`(`rows` 混入 null 行) · `test_validateNullTable_reportsNotRunGuard`(`Validate(null)`) | — |
| **2026-09-26 复审补测**(零覆盖分支 8 条) | `test_rowPresentKeysMissing_reportsNotRunGuard`(行级 `PresentKeys`) · `test_tierParamsTierBodyMissing_reportsNotRunGuard`(档位值缺对象体) · `test_jitterMissing_reportsNotRunGuard` / `test_jitterNonNumeric_reportsRed` / `test_jitterNegative_reportsRed`(jitter 三态) · `test_tierMapExtraTierKey_reportsRed` / `test_tierMapNullTierValue_reportsRed`(档位键集) · `test_truncatedNestedValue_reportsNotRunGuard`(嵌套值未闭合 ⇒ 哨兵,REC-1) | — |

## 夹具清单(`fixtures/`)

| 夹具 | 单因违例 |
|---|---|
| `valid_minimal_table.json` | 正例(四支必需 cue + 乐层行) |
| `invalid_sting_category.json` | 规则 1:`whitelist_category: Sting` |
| `invalid_trigger_vitalscrossed.json` | 规则 2:类别合法 + `trigger_source: VitalsCrossed` |
| `invalid_trigger_crosspair.json` | 规则 6:`PlayerAction` × `ContinuousPhysiology` 混搭 |
| `empty_table.json` | 规则 7:`rows: []` |
| `invalid_tiermap_filter_key.json` | 规则 5:行级 `tier_params`(**per-tier 嵌套**形状)含滤波列 |
| `invalid_tier_params_flat_shape.json` | 规则 5:行级 `tier_params` 用**扁平**形状(键 = 列名)⇒ 档位键 ∉ {"0","1","2"} 拒收 |
| `invalid_clock_ref_dangling.json` | 规则 8:`clock_ref` 悬空 |
| `invalid_subtitle_missing.json` | AC-44-15 [A]:语声 cue 缺 `subtitle_text` |
| `invalid_trigger_phase_out_of_window.json` | 规则 8(QA 场景,2026-09-26 改判):`trigger_phase = 0.5`(相对吸气段分数,出窗 [0.8,1.0]) |

> 每个负向夹具**只含一处违例**(其余行合法),使总门错误恰 = 1 —— 单因归因,防红得不明不白。

## Story 004(听诊呼吸两层与精度档 —— AC-44-01 / 02[L] / 08[A] / 14)

故事头登记的证据路径 = 真身路径(`unity/Assets/Tests/EditMode/Audio/breath_layers_test.cs`,
本 story Test Evidence 已直接钉真身)。真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/breath_layers_test.cs`**(类 `BreathLayersTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/breath_layers_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测门(表侧) | `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs`(`ValidateAc4401DataLevel` · `ValidateAdventitiousLayerPresence` · 委托 002 的 `ValidateTierMap` / `ValidateLoopClock`) |
| 被测门(mixer 侧) | `unity/Assets/Editor.Tools.Gates/MixerTopologyGates.cs`(`ValidateNoTierMuteFlags` · `CountMuteScanKeys` · `ValidateNoMuteCallSites`) |
| 被测运行期(纯函数) | `unity/Assets/Gameplay.Presentation/Audio/`(`FilterRamp` · `BreathPhaseGate` · `AudioTuning` · `BreathLayerDriver` · `TierFilterDriver`) |
| 事件表夹具 | **无独立夹具** —— 表侧正/负例在测试内直接构造(`AudioEventTable` 纯数据 DTO,零文件依赖;JSON 夹具读取承 Story 002) |
| mixer 夹具 | 真资产 `unity/Assets/Audio/DaYiJingCheng.mixer`(正例)+ 测试内联 YAML 负例 |
| 运行方式 | `unity test unity --mode EditMode --filter BreathLayersTest` |

> 读法纪律:仓库文件读取(真 `.mixer` / 证据文档)一律**前置 `File.Exists`**(缺失即红,不静默跳过);
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**(Audio→EditMode→Tests→Assets→unity→仓库根)。

## AC → 测试函数映射

| AC | 测试函数(`BreathLayersTest` 内) | 性质 |
|---|---|---|
| **AC-44-01 ① 正例**(六列齐 + 附加层行在且 loop: true) | `test_tierMapSixColumnsAndAdventitiousLoop_present_zeroErrors` | BLOCKING(结构前提) |
| **AC-44-01 ① 负向**(删列 / 无附加层行 / loop ≠ true) | `test_tierMapSixColumns_missingColumn_red` · `test_adventitiousRow_absent_red` · `test_adventitiousRow_loopNotTrue_red` | BLOCKING |
| **AC-44-01 ② 资产静态**(m_Mute / 捕获值 ≤ −80 / tier×mute 名同现) | `test_mixerNoTierMuteFlag_assetSide_zeroErrors` · `test_mixerNoTierMuteFlag_mutedGroup_red` · `test_mixerNoTierMuteFlag_tierMuteExposedName_red` | BLOCKING |
| **AC-44-01 ② 非空转守卫**(解析 0 键 = NOT-RUN) | `test_muteScan_resolvesAtLeastOneKey_notRunGuard` | BLOCKING |
| **AC-44-01 ② 脚本调用点**(SetFloat ≤ −80 / `.mute = true`) | `test_mixerNoTierMuteFlag_scriptSide_zeroErrors` · `test_mixerNoTierMuteFlag_setFloatBelowFloor_red` | BLOCKING |
| **AC-44-01 ② 真身面**(B1 · 2026-09-26 双评审) | `test_muteCallSites_realRuntimeTrees_zeroViolations`(遍历五棵运行期装配树逐 `.cs` 跑判据;排除 `Editor.Tools.*` = 不进构建、`Tests` = 内联负例本体) | BLOCKING |
| **AC-44-01 ② 判据补全**(B2/B3 · 同批) | `test_mixerNoTierMuteFlag_trueAssignment_red`(`.mute = true` 赋值分支负例)· `test_mixerNoTierMuteFlag_captureValueBelowFloor_red`(捕获 −90 ⇒ 红)· `test_mixerNoTierMuteFlag_duckCaptureValue_notFlagged`(−6 边界不误伤 duck) | BLOCKING |
| **R1 总门并入**(同批) | `MixerTopologyGates.ValidateMixerTopology` 现聚合 `ValidateNoTierMuteFlags`(资产半进构建期;回归面 = 003 既有 fixture 测试) | — |
| **R2/R3 防线负例**(LogError 分支,同批) | `test_filterRamp_beginNonPositiveDuration_logsAndMarksInactive` · `test_filterRamp_negativeDt_noAdvance` · `test_filterRamp_zeroDt_noAdvanceNoLog` · `test_filterRamp_nonPositiveHz_logsSafeZero` · `test_cyclePhase_invalidDenominator_logsSafeZero` · `test_shouldFire_invalidInspireFraction_returnsFalse` | — |
| **exposed 配置守卫探针**(2026-09-27 改判) | `test_busVolumeExposedConfig_matchesGroupVolumeHash_andGetFloatReadable`(反射读回 7 条 guid 与 YAML 写入逐条一致 + `GetFloat` 返回 true;**不再以 `SetFloat` 为判据** —— H-B 确认见下) | — |
| **exposed guid 修复批**(跨 story:003 `MixerAssetGenerator.NormalizeExposedGuids` 改判) | 断言住 `mixer_asset_generator_test.cs`:`test_normalizeExposedGuids_guidEqualsGroupVolumeHash` · `..._synthesizedGuid_repairedToRealHash` · `..._unknownExposedName_throws`(**旧两测「全零⇒b500…/非零保留」作废替换**)。⚠️ 该批**只证 guid 对齐**,后续实测证明 guid 对齐 **不是** 通路根因 | — |
| **✅ H-B 确认(2026-09-27,实验 1–7)** | 统一口径:**`AudioMixer.SetFloat` 在 EditMode 对任何名字都返回 false(包括明显不存在的名字),非 exposed 配置问题** —— `GetFloat` 走另一条路径(遍历组调 `GetGUIDForVolume()` 兜底),实测返回 true。⇒ 探针判据改为配置正确性 + `GetFloat` true;`SetFloat` 在**运行期(播放态)仍有效**(EditMode 限制不适用于运行期)。登记处 = `MixerRegistry.BusVolumeParameters` doc ✅ 段 + 本行 | — |
| **🚨 tier 五名缺口(登记,不做)** | **无测试(禁写 —— 写了就是假红)**:`.mixer` 25 个 effect 全 `Attenuation`、零 filter effect ⇒ `TierFilterParameters` 五名无真实参数可挂;「往 mixer 加 filter effect」= **全案未认领的资产拓扑裁定**;**阻塞 AC-44-02 将来听测** —— 详见 `MixerRegistry.TierFilterParameters` doc 的 🚨 阻塞登记 | BLOCKING(缺口) |
| **AC-44-08 [A]**(相位窗 ⊆ 吸气段) | `test_adventitiousPolicyWindow_withinInspireSegment_zeroErrors` · `test_adventitiousPolicyWindow_outOfWindow_red` | — |
| **AC-44-08 ③**(咳嗽不 EndLoop —— 假 sink 行为测试,unity-specialist Q5 主判据) | `test_coughNeverEndsBreathLoop_zeroEndLoopCalls` · `test_endBreathCycle_endsBothLayersExactlyOnce`(对照面) | — |
| **AC-44-14 运行期半**(两层同节拍 / 禁独立自由循环) | `test_adventitiousWindow_derivedFromBasePeriod_singleMultiplyPath`(与 `InspirePhaseMapping` 逐位一致) · `test_adventitiousWindow_nonPositivePeriod_invalid` · `test_twoLayersShareSameBeat_zeroErrors` | — |
| **AC-44-14 schema 半回归哨兵**(悬空 clock_ref;实现归 002) | `test_clockRefDangling_schemaHalf_red` | — |
| **AC-44-02 [L] → 证据骨架**(结构断言,非听测) | `test_listenEvidenceSkeleton_protocolMarkersPresent` · `test_listenEvidenceSkeleton_missingThresholdMarker_red` | [L] 听测**不自动化** |
| **[L] 半不自动化声明** | AC-44-02 全部听测 + AC-44-08 的波形/听测半 ⇒ 唯一证据 = `production/qa/evidence/breath-layers-listen-evidence.md`(骨架已建,听测未执行) | — |

> **测试数**:`breath_layers_test` = **37**(骨架 26 → 双评审修复批 +10 → guid 探针 +1);
> 同批 `mixer_asset_generator_test` 7 → **8**(guid 两旧测作废、换三新测)。

> **[L] 边界**:上表两条 `test_listenEvidenceSkeleton_*` 只断言**证据文档存在且含协议阈值标记**
> (`n ≥ 6` / `≥ 75%` / 三对 / 未签署行)—— **不是听测本身**;听测结论只能由证据文档签署表承载。

## Story 006(语声变体库与 Intensity 分桶 —— AC-44-03 / AC-44-04 / F-44.6 / 回退门)

故事头登记的证据路径为
`tests/unit/audio_system/voice_variants_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/voice_variants_test.cs`**(类 `VoiceVariantsTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/voice_variants_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测运行期(纯函数) | `unity/Assets/Gameplay.Presentation/Audio/VoiceVariantLib.cs`(`ComputeBucket` / `ComputeGainScale` / `ComputeDensityMult` / `SelectVariant` / `ValidateVoiceVariantLib` / `ValidateBucketThresholdsDecoupled`) |
| 被测运行期(ramp) | `unity/Assets/Gameplay.Presentation/Audio/VolumeRamp.cs`(`Begin` / `Step` / `MaxStepForFrame`) |
| 运行方式 | `unity test unity --mode EditMode --filter VoiceVariantsTest` |

> 读法纪律:仓库文件读取(真源文件扫描)前置 `File.Exists`(缺失即红,不静默跳过);
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**(Audio→EditMode→Tests→Assets→unity→仓库根)。

## AC → 测试函数映射

| AC | 测试函数(`VoiceVariantsTest` 内) | 性质 |
|---|---|---|
| **AC-44-03 正例**(同输入恒选同变体) | `test_selectVariant_sameInput_sameOutput` | BLOCKING |
| **AC-44-03 正例**(不同桶选不同变体) | `test_selectVariant_differentBuckets_differentVariants` | BLOCKING |
| **AC-44-03 正例**(Tier 不参与库轴) | `test_selectVariant_tierNotInLibAxis` | BLOCKING |
| **AC-44-03 正例**(缺 cue ⇒ -1) | `test_selectVariant_missingCue_returnsMinusOne` | BLOCKING |
| **AC-44-03 正例**(缺桶 ⇒ -1) | `test_selectVariant_missingBucket_returnsMinusOne` | BLOCKING |
| **AC-44-04 正例**(ramp ≥ 50ms) | `test_volumeRamp_reachesTarget_afterAtLeastRampFloor` | BLOCKING |
| **AC-44-04 正例**(单帧推进 ≤ maxΔ) | `test_volumeRamp_singleFrameStep_withinMaxStep` | BLOCKING |
| **AC-44-04 负例**(10ms ramp ⇒ 红) | `test_volumeRamp_shortDuration_red` | BLOCKING |
| **F-44.6 正例**(桶序单调) | `test_bucket_monotonicWeakMediumStrong` | BLOCKING |
| **F-44.6 正例**(u=0 ⇒ 弱+GAIN_MIN) | `test_bucket_intensityZero_weakAndGainMin` | BLOCKING |
| **F-44.6 正例**(边界 BUCKET_LOW 恰含) | `test_bucket_boundaryLow_exactInclusion` | BLOCKING |
| **F-44.6 正例**(边界 BUCKET_HIGH 恰含) | `test_bucket_boundaryHigh_exactInclusion` | BLOCKING |
| **F-44.6 正例**(gain_scale 线性映射) | `test_gainScale_linearMapping` | BLOCKING |
| **F-44.6 正例**(density_mult 线性映射) | `test_densityMult_linearMapping` | BLOCKING |
| **回退门正例**(全组合非空) | `test_validateLib_fullTable_zeroErrors` | BLOCKING |
| **回退门负例**(缺 cue ⇒ 红) | `test_validateLib_missingCue_red` | BLOCKING |
| **回退门负例**(缺桶 ⇒ 红) | `test_validateLib_missingBucket_red` | BLOCKING |
| **回退门负例**(空表 ⇒ 红) | `test_validateLib_emptyTable_red` | BLOCKING |
| **分桶边界解耦正例**(无重叠) | `test_validateBucketDecoupled_noOverlap_zeroErrors` | BLOCKING |
| **分桶边界解耦负例**(重叠 ⇒ 红) | `test_validateBucketDecoupled_overlapRed` | BLOCKING |
| **五项调制 Forbidden**(零出现) | `test_forbiddenModulations_zeroAppearance` | BLOCKING |

> **测试数**:`voice_variants_test` = **21**。
> ⚠️ 三条易错点的落地证据:
> 1. **Tier 不参与库轴**(F-44.3:364):`test_selectVariant_tierNotInLibAxis` 验证同 cue 同桶的不同 tier 选相同变体。
> 2. **bucket(u) 边界含端点方向**(F-44.6:419):`test_bucket_boundaryLow_exactInclusion` / `test_bucket_boundaryHigh_exactInclusion` 验证 BUCKET_LOW/HIGH 本身落中/强桶。
> 3. **五项调制 Forbidden**:`test_forbiddenModulations_zeroAppearance` 扫描 VoiceVariantLib.cs 源文件,验证音高/气声/语速/断句/共鸣零出现。

## Story 007(联机分叉与远端派生 · 静态半 —— AC-44-07 / AC-44-D1 / AC-44-D7 / AC-44-D10 / EndLoop 兜底)

故事头登记的证据路径为
`tests/unit/audio_system/net_derivation_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs`**(类 `NetDerivationTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/net_derivation_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/PresentationDtos.cs`(`AudioCueDto` / `IAudioCueSink` / `IPositionalChannel` / `WorldPosLatest` / `TierSource`) |
| 被测全序键 | `unity/Assets/Sim.Contracts/EventOrderKey.cs`(`EventOrderKey`) |
| 被测哈希 | `unity/Assets/Sim.Contracts/SplitMix64.cs`(`SplitMix64`) |
| 被测生产 44 类型 | `unity/Assets/Gameplay.Presentation/Audio/`(全命名空间,IL 扫描) |
| 被测 GDD | `design/gdd/audio-system.md`(AC-44-07 / AC-44-D1 / AC-44-D7 / AC-44-D10) |
| 运行方式 | `unity test unity --mode EditMode --filter NetDerivationTest` |

> 读法纪律:本 story 为**静态半** —— 三腿(IL 扫描 / 重排差分 / 负向夹具)在 P0 可跑;
> **4 人双实例对拍 = BLOCKED-BY-45**(P1b),不在本断言面。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`NetDerivationTest` 内) | 性质 |
|---|---|---|
| **AC-44-07 静态半**(SetTier 默认 Local) | `test_setTier_defaultLocalRecorded` | BLOCKING |
| **AC-44-07 文档一致性**(GDD 无「取主机技能」残句) | `test_gdd_netSection_noHostTierWording` | BLOCKING |
| **AC-44-D1 来源纪律**(IPositionalChannel 单实现) | `test_positionalChannel_singleConsumerImplementation` | BLOCKING |
| **AC-44-D1 契约面**(WorldPosLatest 携带 ServerTick) | `test_worldPosLatest_carriesServerTick` | BLOCKING |
| **AC-44-D1 + D-44 消费纪律**(派生面零 IPositionalChannel 引用) | `test_derivationSurfaceTypes_zeroPositionalChannelRefs` | BLOCKING |
| **AC-44-D1 负向**(fixture 含通道引用 ⇒ 红) | `test_derivationSurfaceScan_fixtureWithChannelRef_flagged` | BLOCKING |
| **AC-44-D7 ② 时钟面**(派生面 IL 禁 RNG/帧/墙钟) | `test_derivationSurfaceTypes_ilBody_noClockOrRandomRefs` | BLOCKING |
| **AC-44-D7 ② 负向**(fixture 含 UnityEngine.Random ⇒ 红) | `test_derivationScan_ilBodyFixtureRandom_flagged` | BLOCKING |
| **AC-44-D7 ③ 载荷面**(AudioCueDto 全字段整数域) | `test_audioCueDto_allFieldsIntegerDomain` | BLOCKING |
| **AC-44-D7 ③**(Intensity = byte) | `test_audioCueDto_intensityIsByte` | BLOCKING |
| **AC-44-D7 ③**(Cell = Int3) | `test_audioCueDto_cellIsInt3` | BLOCKING |
| **AC-44-D7 重排差分核心**(同流乱序到达哈希一致) | `test_reorderInvariance_arrivalOrderPreservesCueHash` | BLOCKING |
| **重排差分载荷面自证**(Intensity 进哈希) | `test_reorderInvariance_intensityFeedsHash` | BLOCKING |
| **重排差分事件集自证**(事件数进哈希) | `test_reorderInvariance_eventCountFeedsHash` | BLOCKING |
| **AC-44-D10 锚点缺席**(一次性丢弃/循环挂起) | `test_anchorAbsent_oneShotDropped_loopCuePending` | BLOCKING |
| **AC-44-D10 锚点到达**(挂起补发) | `test_anchorArrived_pendingLoopReleased` | BLOCKING |
| **AC-44-D10 陈旧检查**(ServerTick 陈旧拒读) | `test_anchorStale_refused` | BLOCKING |
| **EndLoop 自评兜底**(纯函数判停 + 有界阈值) | `test_endLoop_selfAssessPredicate_pureAndBounded` | BLOCKING |
| **EndLoop 有界性**(阈值为正常数) | `test_endLoop_selfAssessThreshold_isPositiveBoundedConstant` | BLOCKING |

> **测试数**:`net_derivation_test` = **19**。
> ⚠️ 三条易错点的落地证据:
> 1. **IL 扫描用 token 解析**(不是字节 Contains):`MethodBodyReferences` 用 `Module.ResolveMethod` 解析 IL 里的 call/callvirt/newobj token,字符串字面量(ldstr)指向 #Blob 堆不在字节数组里。
> 2. **重排差分哈希用 SplitMix64 链式折叠**:事件 → `EventOrderKey` 全序 → cue 记录 → `SplitMix64.Fold` 逐条折叠,哈希输入含 Intensity(载荷面自证)。
> 3. **负向夹具住测试命名空间**:`ChannelRefFixture` / `RandomBodyFixture` 在 `DaYiJingCheng.Tests.Unit.Audio` 下,不入 44 生产扫描键(同 Story 001 `assembly_boundary_test` 分工)。

## Story 008(空间化与世界语境呼吸 —— AC-44-D2 / D3 / D8 / 16 / 优先级公式)

故事头登记的证据路径为
`tests/unit/audio_system/spatialization_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/spatialization_test.cs`**(类 `SpatializationTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/spatialization_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/PresentationDtos.cs`(`AudioCueDto.Cell:Int3` / `IPositionalChannel`) |
| 被测生产 44 类型 | `unity/Assets/Gameplay.Presentation/Audio/`(禁反推 Cecil 扫描 + IEventSink 扫描) |
| 被测事件表 | `assets/data/audio_events.json`(worldBreath 行接线断言) |
| 被测哈希 | `unity/Assets/Sim.Contracts/SplitMix64.cs`(`SplitMix64.Hash`) |
| 运行方式 | `unity test unity --mode EditMode --filter SpatializationTest` |

> 读法纪律:本 story 生产代码(LatticeToWorld/cell_jitter/rank_key/AudioSource 池)尚未实现,
> 测试用自持谓词面(FakeSourcePool/FakeOcclusionDriver/RankKeyComputer/CellJitter/FakeLattice)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`SpatializationTest` 内) | 性质 |
|---|---|---|
| **AC-44-D2 禁反推** | `test_latticeToWorld_noFloatToWorldPosMethod` | BLOCKING |
| **AC-44-D2 负向** | `test_latticeToWorld_reverseDerivationFixture_flagged` | BLOCKING |
| **AC-44-D3 不写 sim** | `test_spatialization_noEventSinkRefs` | BLOCKING |
| **AC-44-D8 ① 遮挡低通正例** | `test_occlusionDriver_occluded_lowpassPositive` | BLOCKING |
| **AC-44-D8 ① 无遮挡** | `test_occlusionDriver_notOccluded_noLowpass` | BLOCKING |
| **AC-44-D8 ① 负向 cutoff≤0** | `test_occlusionDriver_zeroCutoff_rejected` | BLOCKING |
| **AC-44-D8 ② 同 cell N 独立** | `test_sourcePool_sameCell_nSourcesNotMerged` | BLOCKING |
| **AC-44-D8 ② N=1** | `test_sourcePool_singlePatient_oneSource` | BLOCKING |
| **AC-44-D8 ② 释放不泄漏** | `test_sourcePool_release_reusable` | BLOCKING |
| **rank_key 确定性** | `test_rankKey_deterministicAcrossCalls` | BLOCKING |
| **rank_key 同类距离序** | `test_rankKey_sameClassCloserFirst` | BLOCKING |
| **rank_key 等距平局** | `test_rankKey_equalDistance_cellJitterBreaksTie` | BLOCKING |
| **rank_key 重算时机** | `test_rankKey_recalcOnlyOnBirthDeath` | BLOCKING |
| **duck 只压不丢** | `test_priority_duckDoesNotDrop` | BLOCKING |
| **bus ≠ 优先级类别** | `test_priority_busNotPriorityClass` | BLOCKING |
| **AC-44-16 接线正例** | `test_eventTable_worldBreathRow_presentAndWired` | BLOCKING |
| **AC-44-16 负向缺行** | `test_eventTable_worldBreathRow_missing_red` | BLOCKING |
| **AC-44-16 负向错 bus** | `test_eventTable_worldBreathRow_wrongBus_red` | BLOCKING |
| **F-44.4 LatticeToWorld** | `test_latticeToWorld_integerCellToFloat` | BLOCKING |
| **F-44.4 cell_jitter 确定性** | `test_cellJitter_deterministicAndDistinguishesSameCell` | BLOCKING |

> **测试数**:`spatialization_test` = **20**(原 21,删除 1 个恒真 `test_cellJitter_doesNotWriteBack`)。
> ⚠️ 四条易错点的落地证据:
> 1. **禁反推用 Cecil 扫描器**:`test_latticeToWorld_noFloatToWorldPosMethod` 调 `CheckAudioAssemblyIl` 扫生产类型签名,无 float→WorldPos 方法。
> 2. **RankKeyComputer 输入哈希缓存**:同输入跳过重算(防增益抖动),输入变化(声源生灭)才重算。
> 3. **duck 用 dB→线性幅度**:`ApplyDuck(linearVolume, depthDb)` = `linearVolume * 10^(depthDb/20)`,验证 duck 后音量 > 0。
> 4. **负例构造用 JSON Replace**:missing_red 用 Replace cue 名,wrongBus_red 用 Replace bus 值,构造真正违例数据。

## Story 009(声源生命周期与贴耳交接 —— EndLoop 自评兜底 / 贴耳交接契约 / 重复过期句柄 / 咳嗽不停呼吸)

故事头登记的证据路径为
`tests/unit/audio_system/loop_lifecycle_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/loop_lifecycle_test.cs`**(类 `LoopLifecycleTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/loop_lifecycle_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/PresentationDtos.cs`(`IAudioCueSink` / `AudioCueHandle` / `TierSource`) |
| 被测生产 44 类型 | `unity/Assets/Gameplay.Presentation/Audio/`(Cecil 时钟 token 扫描) |
| 被测咳嗽通道 | `unity/Assets/Gameplay.Presentation/Audio/CoughChannel.cs`(`Dispatch` 只调 Emit) |
| 运行方式 | `unity test unity --mode EditMode --filter LoopLifecycleTest` |

> 读法纪律:本 story 生产代码(EndLoop 兜底/交接状态机/句柄管理)尚未实现,
> 测试用自持谓词面(FakeLoopWatchdog/FakeHandoverFsm/FakeHandleManager)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`LoopLifecycleTest` 内) | 性质 |
|---|---|---|
| **EndLoop 兜底:流停更后有界停** | `test_endLoopWatchdog_staleStream_stopsWithinBound` | BLOCKING |
| **EndLoop 兜底:流恢复不重复停** | `test_endLoopWatchdog_resumeNoDoubleStop` | BLOCKING |
| **EndLoop 兜底:有界常量 0 当轮必停** | `test_endLoopWatchdog_zeroBoundStopsImmediately` | BLOCKING |
| **EndLoop 兜底:时钟源 = sim tick** | `test_endLoopWatchdog_noWallClockRefs` | BLOCKING |
| **EndLoop 契约形状:本地句柄** | `test_endLoop_contractShape_localHandleOnly` | BLOCKING |
| **贴耳交接:进入世界层压出** | `test_handover_enterStethoscope_worldFadesOut` | BLOCKING |
| **贴耳交接:退出镜像** | `test_handover_exitStethoscope_worldFadesIn` | BLOCKING |
| **贴耳交接:幂等重入** | `test_handover_idempotentReentry` | BLOCKING |
| **贴耳交接:听诊中世界 cue 不重启** | `test_handover_worldCueArrives_noRestart` | BLOCKING |
| **贴耳交接:硬切拒绝** | `test_handover_hardCut_rejected` | BLOCKING |
| **重复句柄:同源重复以最后为准** | `test_duplicateBeginLoop_lastWins` | BLOCKING |
| **过期句柄:过期 EndLoop 忽略** | `test_expiredEndLoop_ignored` | BLOCKING |
| **咳嗽不停呼吸(联合断言)** | 注释引用 Story 004 `breath_layers_test.test_coughNeverEndsBreathLoop_zeroEndLoopCalls` | — |

> **测试数**:`loop_lifecycle_test` = **12**(原 13,删除 1 个与 Story 004 重复的咳嗽测试)。
> ⚠️ 四条易错点的落地证据:
> 1. **EndLoop 兜底用 Cecil 扫描器**:`test_endLoopWatchdog_noWallClockRefs` 调 `AssemblyGates.CheckAudioClockTokens` 扫生产类型方法体 IL,禁 DateTime/Time 引用。
> 2. **FakeHandoverFsm 用相对计数**:`_handoverElapsed++` 达到 `_handoverTicks` 时激活听诊层,不用绝对 tick 差(避免魔数)。
> 3. **咳嗽测试不重复**:Story 009 AC 明确把「咳嗽不停呼吸」列为验收标准,但 Story 004 已有同名同逻辑测试,本 story 不重复实现,改为注释引用。
> 4. **GDD 补登记 LOOP_EVAL_MAX_TICKS**:QA 评审发现 AC 引用的「登记常量」在 GDD §Tuning Knobs 中不存在,已补登记(安全范围 ≥ 1 tick)。

## Story 010(素材管线与事件表烘焙门 —— AC-44-D4 / D5 / D6 / 听诊窗预载)

故事头登记的证据路径为
`tests/unit/audio_system/pipeline_gate_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`**(类 `PipelineGateTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Sim.Contracts/PresentationDtos.cs`(`AudioCueDto` / `IAudioCueSink`) |
| 被测烘焙工具 | `unity/Assets/Editor.Tools.Bake/`(`BakeValidationException` / `CookedWriter`) |
| 被测 Addressables 组 | `unity/Assets/AddressableAssetsData/AssetGroups/` |
| 运行方式 | `unity test unity --mode EditMode --filter PipelineGateTest` |

> 读法纪律:本 story 生产代码(烘焙管线/Addressables 组配置)尚未实现,
> 测试用自持谓词面(FakePreloadGate/GroupSeparabilityPredicate)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`PipelineGateTest` 内) | 性质 |
|---|---|---|
| **AC-44-D4 零解析器** | `test_noJsonParser_noNewtonsoftRefs` · `test_noJsonParser_noJsonReaderRefs` · `test_noFixParse_refs` | BLOCKING |
| **AC-44-D5 组区分正例** | `test_addressables_audioGroupSeparateFromDataCore`(音频组未配置时 `Assert.Ignore`) | BLOCKING |
| **AC-44-D5 组区分负例** | `test_addressables_mixedGroup_rejected`(调谓词 `GroupSeparabilityPredicate.IsMixedGroup`) | BLOCKING |
| **AC-44-D6 存在性门** | `test_assetExistence_missingWav_rejected`(自包含夹具) | BLOCKING |
| **AC-44-D6 存在性门负例** | `test_assetExistence_missingWavFixture_rejected` | BLOCKING |
| **AC-44-D6 聚合 throw** | `test_assetExistence_bakeThrowsNotSilent` | BLOCKING |
| **听诊窗预载正例** | `test_preloadGate_stethoscopeFocus_preloaded` | BLOCKING |
| **听诊窗预载负例** | `test_preloadGate_notLoaded_blocks` | BLOCKING |
| **一次性 cue 迟发有界** | `test_preloadGate_oneShotCueDelay_bounded` | BLOCKING |

> **测试数**:`pipeline_gate_test` = **11**(10 passed + 1 ignored)。
> ⚠️ 四条易错点的落地证据:
> 1. **零解析器扫描公开方法签名**:三个 `test_noJsonParser_*` 扫描 44 生产类型公开方法的返回类型和参数类型,无 Newtonsoft/JsonReader/FixParse 引用。
> 2. **组区分用谓词函数**:`test_addressables_mixedGroup_rejected` 调 `GroupSeparabilityPredicate.IsMixedGroup` 判定混组,不对测试自己构造的字符串做 Contains。
> 3. **素材存在性用自包含夹具**:真种子事件表引用了不存在的 wav(content 批未完成),测试用自包含夹具验证谓词逻辑本身工作正常。
> 4. **扫描键单一出处**:`Prefix` 引用 `AssemblyGates.AudioModuleNamespacePrefix`,不重复定义字面量。

## Story 011(设置暴露面与归零机制 —— 注册表 AC① / AC-44-13 mono / AC-44-15 字幕键集 / AC-44-19 归零机制)

故事头登记的证据路径为
`tests/unit/audio_system/settings_exposure_test.cs`,但该路径在仓库根、**Unity 不编译**
⇒ 真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs`**(类 `SettingsExposureTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(name = `Sim.Contracts.Tests`) |
| 被测契约 | `unity/Assets/Gameplay.Presentation/Audio/MixerRegistry.cs`(`BusVolumeParameters` 7 路) |
| 被测 GDD | `design/gdd/audio-system.md`(AC-44-12/13/15/19) |
| 运行方式 | `unity test unity --mode EditMode --filter SettingsExposureTest` |

> 读法纪律:本 story 生产代码(设置 store/归零机制/注册表)尚未实现,
> 测试用自持谓词面(FakeSettingsStore/FakeResetMechanism)。
> `repoRoot()` = `[CallerFilePath]` 上溯 **5 层**。

## AC → 测试函数映射

| AC | 测试函数(`SettingsExposureTest` 内) | 性质 |
|---|---|---|
| **注册表 AC①:7 路 ∈ 注册表** | `test_settingsRegistry_busVolumes_subsetOfRegistry` | BLOCKING |
| **注册表 AC①:mono ∈ 注册表** | `test_settingsRegistry_monoEnabled_inRegistry` | BLOCKING |
| **注册表 AC① 负向:未登记参数** | `test_settingsRegistry_unregisteredParam_rejected` | BLOCKING |
| **注册表 AC①:恰 8 条** | `test_settingsRegistry_exactly8Entries` | BLOCKING |
| **AC-44-13 mono 选项** | `test_monoOption_existsWithValidDefault`(`Assert.Inconclusive`:出厂默认待生产实现) | BLOCKING |
| **AC-44-15 字幕键集正例** | `test_subtitleKeySet_coveredByEventTable`(per-cue 验证) | BLOCKING |
| **AC-44-15 字幕键集负例** | `test_subtitleKeySet_missingSubtitle_rejected` | BLOCKING |
| **AC-44-19 归零三步** | `test_resetMechanism_threeStepsReachFactory` | BLOCKING |
| **AC-44-19 重启持久化** | `test_resetMechanism_persistsAfterRestart` | BLOCKING |
| **AC-44-19 负向:跳过 sidecar** | `test_resetMechanism_skipSidecar_rejected` | BLOCKING |
| **AC-44-12 文档判据** | `test_a11yDocumentation_threeItemsPresent` | BLOCKING |

> **测试数**:`settings_exposure_test` = **11**(10 passed + 1 inconclusive)。
> ⚠️ 四条易错点的落地证据:
> 1. **注册表 7 路验证**:`test_settingsRegistry_busVolumes_subsetOfRegistry` 验证 `MixerRegistry.BusVolumeParameters.Count() == 7`(OQ-SS-3=甲:数据层恒 7 路)。
> 2. **字幕 per-cue 验证**:`test_subtitleKeySet_coveredByEventTable` 用 `HasSubtitleForCue` 检查该 cue 行内 500 字符内是否有 subtitle_text(非全局搜索)。
> 3. **归零契约面形状**:3 个 reset 测试验证归零三步语义(写默认 → 推 mixer → 落 sidecar)+ sidecar 持久化契约面形状,生产实现后应替换为调用真实接口。
> 4. **mono 出厂默认 inconclusive**:出厂默认值归 ADR-014 烘焙分区(用户调),当前阶段生产代码未实现,用 `Assert.Inconclusive` 明确标记待验证。
