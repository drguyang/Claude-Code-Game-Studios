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
| 夹具 | `tests/unit/diagnosis_system/fixtures/*.json`(story-002 段 **21 个**,见下表;全目录共 **31 个** = 21 + story-003 的 10 个) |
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
禁顺手重钉。`AC-8-35` 的 **F-8.1/F-8.3 参数半边 = NOT-RUN**(常量面 = 前缀 ns **代码常量** + DIAG_TIERS;
**不含** `assets/data/*.json` 的曲线**数据参数** —— 那些由产物 ConfigVersion 覆盖。story-003 已把
运行期**枚举 / 映射常量**扩入面并重钉 `b9354110`→`5bba361c`;**曲线参数**半边仍 NOT-RUN,见测试头注)。

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

---

## Story 003(F-8.1 可读地板与 F-8.2 精度档槽 —— AC-8-5/7/8/9/46 · C-1/C-5 · G-1/G-3)

真身(实际编译、实际运行)=

**`unity/Assets/Tests/EditMode/DiagnosisSystem/read_floor_slots_test.cs`**(类 `ReadFloorSlotsTest`)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/DiagnosisSystem/read_floor_slots_test.cs` |
| 共享金标扫描真源 | `unity/Assets/Tests/EditMode/DiagnosisSystem/DiagnosisGoldenScan.cs`(story-002/003 共用) |
| 被测运行期 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisReadFloorTable.cs`(`DiagnosisReadFloorEvaluator` + `DiagnosisSlot` / `SignReadState` 枚举) |
| Note 6 通道映射 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisChannelMaskMap.cs`(双向断言,由 `DiagnosisSignTableValidator.Validate` 接生产路径) |
| 阶段 2 全链 | `DiagnosisReadFloorBaker`(种子)→ `DiagnosisReadFloorBinder.Bind`(**唯一**校验点)→ `DiagnosisReadFloorCookedWriter`(编码) |
| 读方 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisReadFloorCookedCodec.cs` |
| 真种子(正例) | `assets/data/diagnosis_read_floor.json`(合成系数;**数值归用户数值轮**) |
| 夹具 | `tests/unit/diagnosis_system/fixtures/read_floor_*.json`(**10 个**,见下表) |
| 运行方式 | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem"` |

### AC → 测试函数映射

| AC | 测试函数(`ReadFloorSlotsTest` 内) | 性质 |
|---|---|---|
| **AC-8-5 端点+单调扫描** | `test_ac85_repoSeed_endpointsAndMonotonic`(61 档全查:三端点 + `BASE>MIN>0` + 60 对单调) | BLOCKING |
| **AC-8-F3 `READ_FLOOR_MIN>0`** | `test_ac8f3_readFloorMin_strictlyPositive`(满技能仍可读不出) | BLOCKING |
| **双条件「与」门** | `test_ac85_andGate_fourQuadrants_readableExactlyWhenBoth`(四象限 + 边界恰值 ⇒ 可读集恰 {(1,1)}) | BLOCKING |
| **有词 ≠ 可读** | `test_ac85_wordPresent_readableIndependent`(同 (Skill,sign) 两输出可分) | BLOCKING |
| **AC-8-7 整数档位** | `test_ac87_slotBoundaries_integerTierIndex`(每切点 T−1/T 相邻两档) · `test_ac87_floatPerturbation_doesNotChangeSlot`(G-3;反射断参型 int) | BLOCKING |
| **AC-8-8 空白档回退** | `test_ac88_koplikEmptyCoarseMedium_fallsDownToFineOnly` · `test_ac88_allSlotsEmpty_returnsUnreadableNegative` · `test_ac88_fallback_neverGoesUp` | BLOCKING |
| **哨兵三值可分** | `test_sentinel_blank_unreadable_positive_distinct` | BLOCKING |
| **AC-8-9 手段不上锁** | `test_ac89_allRevealMethods_availableAtMinSkill`(五法在 Skill=0 各有真读数;枚举零锁闭成员) | BLOCKING |
| **掉级回升(AC-8-46 曲线半)** | `test_ac846_skillDrop_floorRises_slotFallsBack`(floor 回升 + slot 3→2) | BLOCKING |
| **G-1 定表化** | `test_g1_tableLookup_deterministicAndSelfEvident`(查表命中 + 跨档区分 + 反射零幂名) | BLOCKING |
| **ADR-014 §二 烘焙确定性** | `test_bakeDeterminism_doubleRun_byteEqual` · `test_roundTrip_codecByteStable` —— 同进程;**跨会话/跨平台 NOT-RUN**(承 story-002 / interaction 007 口径) | BLOCKING(限同进程) |
| **C-1 违例**(生成期硬失败) | `test_c1_readFloorMinZero_red` · `test_c1_baseBelowMin_red` · `test_c1_endpointEqual_red` | BLOCKING |
| **C-5 违例**(G-1 闭集) | `test_c5_gammaThird_red` · `test_gamma_floatToken_red` | BLOCKING |
| **绑定层**(skill_cap 漂移 / 未知键) | `test_skillcap_drift_red` · `test_unknownKey_red` | BLOCKING |
| **形状夹具**(单调/平段/半整数) | `test_shape_strictMonotonicFixture` · `test_shape_plateauFixture_monotonicStillHolds` · `test_shape_halfGamma_fixture` | BLOCKING |
| **AC-8-35 加行常量不变** | `test_ac835_constantsGolden_sharedScan` · `test_ac835_sensitivity_syntheticEntryReds`(含 story-002 侧同名测) | BLOCKING |

### 夹具清单(`fixtures/read_floor_*.json`,10 个)

| 夹具 | 单因违例 / 角色 |
|---|---|
| `read_floor_strict_monotonic.json` | 正例(严格单调) |
| `read_floor_plateau_tiny_span.json` | 正例(平段:base_read 仅比 min 大 1/65536) |
| `read_floor_half_gamma.json` | 正例(gamma `1/2`,走 FixPow 半整数支) |
| `read_floor_gamma_third_red.json` | C-5 违例(gamma `1/3`,闭集外) |
| `read_floor_endpoint_equal_red.json` | C-1 违例(base == min,曲线退化) |
| `read_floor_min_zero_red.json` | C-1 违例(min ≤ 0) |
| `read_floor_base_below_min_red.json` | C-1 违例(base < min) |
| `read_floor_skillcap_drift_red.json` | skill_cap ≠ 30 侧 SKILL_CAP(59) |
| `read_floor_gamma_float_token_red.json` | gamma JSON 数字(禁 double 中转;须字符串) |
| `read_floor_unknown_key_red.json` | 未知键(ADR-014 §三) |

> 读法纪律同 story-002:违例经 `DiagnosisReadFloorBinderProbe.Bake` 端到端黑盒驱动
> (**生产同一台机器**),断言 = 抛 + **恰一条错误**(排他)+ 触底规则 tag(`BakeFails` helper)。

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)

- **AC-8-F3 的 `≥ Project(σ)` 联动子句** —— `Project(` 在 `unity/Assets/**.cs` 零命中
  (D-8-4 的 9 侧投影归 disease-simulation story 004);本 story 只判 `READ_FLOOR_MIN>0` 半边。
- **AC-8-9 的 EmitGrowth 实际门控调用** —— 归 story 005(Out of Scope 明写)。
- **AC-8-46 的「词变粗」子句** —— 四档配三档词 ⇒ 满→细同词,**数据形状下不可观测**;
  词面粗化归呈现层 story 006(本 story 只判 floor 回升 + slot 跌落)。
- **AC-8-46 的病名持久化半边** —— 归 37 / story 005。
- **跨会话/跨平台烘焙逐位一致** —— 只证同进程;跨平台浮点面归 AC-8-F5(story 004 矩阵)。
- **AC-8-35 的 F-8.1/F-8.3 曲线参数半边** —— 参数住 `assets/data/*.json`(数据,非代码常量),
  由 ConfigVersion 覆盖;金标面 = 前缀 ns 代码常量 + DIAG_TIERS。

### Note 6 义务(通道同名不同物)

`Diagnosis.SignChannel`(R-8.1 序数 0..5)与 `Sim.Contracts.SignChannel`(AC-21 位掩码 `1<<n`)
**同名不同物** —— `DiagnosisChannelMaskMap` 立映射表 + **构建期双向断言**(五通道恰满 ·
零重复位 · 零悬空 · Wound=唯一悬空位显式登记 · History→0 显式登记),由
`DiagnosisSignTableValidator.Validate`(生产路径)调用。
