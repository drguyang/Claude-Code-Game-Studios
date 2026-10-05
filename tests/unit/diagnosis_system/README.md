# unit/diagnosis_system/

按系统分目录的单元测试(命名 `test_[system]_[scenario]_[expected_result]`)。
diagnosis-system 逻辑类故事的 BLOCKING 证据落点(coding-standards §Testing Evidence by Story Type)。

**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点;
`.cs` 真身落 `unity/Assets/Tests/EditMode/DiagnosisSystem/`,本 README 记真身与 AC → 测映射
(承 `tests/unit/audio_system/README.md` / `tests/unit/interaction/` 同一先例)。

## Story 002(体征词条表 schema 与 P0 数据行 —— R-8.1 / R-8.2 / AC-8-32…35 / C-6 / D-8-9)

真身(实际编译、实际运行的测试)=

**`unity/Assets/Tests/EditMode/DiagnosisSystem/sign_table_test.cs`**(类 `SignTableTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/DiagnosisSystem/sign_table_test.cs` |
| 编译中的测试源(程序集边界,story 001) | `unity/Assets/Tests/EditMode/DiagnosisSystem/boundary_guard_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef` |
| 被测校验器 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisSignTable.cs`(`DiagnosisSignTableValidator`) |
| 唯一调用点 | `unity/Assets/Editor.Tools.Bake/DiagnosisSignBinder.cs`(`Bind` —— 删它 ⇒ 违例静默烘出,负例转红) |
| 阶段 2 全链 | `DiagnosisSignBaker`(种子)→ `DiagnosisSignBinder.Bind`(绑定+校验)→ `DiagnosisSignCookedWriter`(编码) |
| 读方 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisSignCookedCodec.cs` |
| 真种子(正例) | `assets/data/diagnosis_signs.json`(34 行 = 阳性 30 + 阴性 4) |
| 夹具 | `tests/unit/diagnosis_system/fixtures/*.json`(21 个,见下表) |
| 运行方式 | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem"` |

## AC → 测试函数映射

| AC | 测试函数(`SignTableTest` 内) | 性质 |
|---|---|---|
| **反空转 ①**(种子/合法夹具真通过) | `test_ac832_repoSeedBakes34Rows` · `test_ac832_legalBaselineBakesSuccessfully` · `test_ac832_roundTrip_validatorStillPasses_configVersionStable` | BLOCKING |
| **AC-8-32 通道闭集 + AC-8-33 P1a**(双 tag) | `test_ac832_channelP1aTongue_dualTagRed`(夹具) · `test_ac833_p1aChannelValues_dualTagRed`(TestCase ×4:脉/情志/体质/时序) | BLOCKING |
| **AC-8-32/8-33 揭示法闭集 + P1a** | `test_ac833_revealP1aWang_dualTagRed`(夹具) · `test_ac833_p1aRevealValues_dualTagRed`(TestCase ×2:闻/切) · `test_ac832_revealEmptyArrayRed` | BLOCKING |
| **AC-8-32 tier 域**(35 / 中间值) | `test_ac832_tier35TestLineRed`(`tier_named=35` 触底) · `test_ac832_tierMidValue15Red`(`tier_named=15` 触底) | BLOCKING |
| **AC-8-32 neg_weight / C-6** | `test_ac832_positiveWithNegWeightRed` · `test_ac832_negativeMissingNegWeightRed`(+C-6) · `test_ac832_negativeZeroNegWeightRed`(+C-6) · `test_binder_negWeightFloatTokenHardFails` · `test_binder_negWeightUnparseableHardFails` | BLOCKING |
| **AC-8-32 必填/形状**(sign_id / display_词 / polarity) | `test_ac832_signIdMissingRed` · `test_ac832_displayTwoSlotsRed` · `test_ac832_displayEmptyStringRed` · `test_ac832_polarityMissingRed` · `test_ac832_polarityIllegalValueRed` | BLOCKING |
| **ADR-014 §三 绑定层** | `test_binder_unknownKeyHardFails`(未知键) · 空词表 `test_r81_emptySignsRejectedRed`(反假绿) | BLOCKING |
| **R-8.1 主键** | `test_r81_duplicateSignIdRed` | BLOCKING |
| **AC-8-33 lab 禁入** | `test_ac833_labRowRed` | BLOCKING |
| **AC-8-34 病史通道白名单** | `test_ac834_historyChannelNotWhitelistedRed` | BLOCKING |
| **AC-8-34 正向外键闭合**(9 → 8) | `test_ac834_forwardMissingReferencedSign_red` · `test_ac834_forwardSubsetPasses` · `test_ac834_forward9sideSignsStillEmpty_tripwire`(9 接线义务触发器) | BLOCKING |
| **AC-8-34 反向孤儿子句** | `test_ac834_reverseOrphan_blockedish` —— **NOT-RUN**(BLOCKED-BY-disease story 006 / TR-diag-013,[Ignore] 挂起,方法体诚实) | NOT-RUN · 禁借绿 |
| **TR-diag-014 / D-8-9 双向耦合** | `test_trdiag014_slotBoundsCoupled_green` · `test_trdiag014_slotBoundsDrift21_red` | BLOCKING |
| **R-8.2 存在性**(34 主键 / 30+4 / 档 / 通道) | `test_r82_existence_pinnedIdsCountsAndTiers` · `test_ac832_koplikEmptySlotExpressible` | BLOCKING |
| **R-8.2 内容金标**(id\|通道\|档\|极性\|揭示法\|三档词 逐字冻结) | `test_r82_contentFrozen_golden`(`GoldenContentHash`) | BLOCKING |
| **AC-8-35 加行常量不变** | `test_ac835_constantsGolden`(`GoldenConstantsHash`) · `test_ac835_addRow_constantsUnchanged`(前后绑金标) · `test_ac835_sensitivity_syntheticEntryReds`(敏感性) | BLOCKING |
| **ADR-014 §二 烘焙确定性** | `test_bakeDeterminism_doubleRun_byteEqual` —— 同进程;**跨会话 NOT-RUN**(承 interaction story-007 口径) | BLOCKING(限同进程) |

**金标维护纪律**:`GoldenConstantsHash` / `GoldenContentHash` 任一漂移 ⇒ **过 /design-review 并有意识重钉**,
禁顺手重钉。`AC-8-35` 的 **F-8.1/F-8.3 参数半边 = NOT-RUN**(现常量面不含 READ_FLOOR / NEG_CONF 曲线参数,
story 003/004 落地时扩金标或另立金标,见测试头注)。

## 夹具清单(`fixtures/`,21 个)

| 夹具 | 单因违例 / 角色 |
|---|---|
| `legal_baseline.json` | 正例(1 阳 + 1 阴) |
| `invalid_channel_tongue.json` | channel `舌`(P1a,双 tag) |
| `invalid_reveal_wang.json` | reveal_by `望`(P1a,双 tag) |
| `invalid_reveal_empty.json` | reveal_by `[]` |
| `invalid_tier_35.json` | tier_named 35(检验线) |
| `invalid_tier_mid.json` | tier_named 15(中间值) |
| `invalid_positive_with_weight.json` | 阳性带 neg_weight |
| `invalid_negative_missing_weight.json` | 阴性 neg_weight 缺失(C-6) |
| `invalid_negative_zero_weight.json` | 阴性 neg_weight `0`(C-6 >0) |
| `invalid_display_two_slots.json` | display_词 2 槽 |
| `invalid_display_empty_string.json` | display_词 空串(须 null) |
| `invalid_polarity_missing.json` | polarity 缺失 |
| `invalid_polarity_illegal.json` | polarity `中性`(闭集外)★修复轮 |
| `invalid_sign_id_missing.json` | sign_id 字段缺失 ★修复轮 |
| `invalid_neg_weight_unparseable.json` | neg_weight `"abc"`(FixParse 拒)★修复轮 |
| `invalid_empty_signs.json` | `signs: []`(空词表反假绿)★修复轮 |
| `invalid_unknown_key.json` | 未知键(ADR-014 §三) |
| `invalid_neg_weight_float_token.json` | neg_weight JSON number(禁 double 中转) |
| `invalid_duplicate_sign_id.json` | sign_id 重复(R-8.1 主键) |
| `invalid_lab_row.json` | `lab_*` 进 P0 表(AC-8-33) |
| `invalid_history_channel_not_whitelisted.json` | 病史通道借给非白名单行(AC-8-34) |

> 读法纪律:违例经 `DiagnosisSignBinderProbe.Bake` 端到端黑盒驱动(**生产同一台机器**),
> 断言 = 抛 + **恰一条错误**(排他)+ 触底规则 tag(`BakeFails` helper)。

## 未登记夹具(反向孤儿的输入)

反向孤儿子句(`test_ac834_reverseOrphan_blockedish`)**刻意无专属夹具** —— 其输入 =
9 的 R1 注册表引用集,由 disease epic 提供;现取空集(方法体内构造),去 Ignore 即诚实红。
