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
