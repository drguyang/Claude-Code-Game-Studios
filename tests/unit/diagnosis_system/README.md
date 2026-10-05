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
| 夹具 | `tests/unit/diagnosis_system/fixtures/*.json`(story-002 段 **21 个**,见下表;全目录共 **45 个** = 21 + story-003 的 10 个 + story-004 的 14 个) |
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

---

## Story 004(F-8.3 阴性把握度与不泄漏不变量 —— AC-8-10/11/12/14/15/16/17/18 · AC-8-F1/F2/F4/F5 · AC-8-50 · C-3/C-4/C-6/C-7)

真身(实际编译、实际运行)=

**`unity/Assets/Tests/EditMode/DiagnosisSystem/confidence_leak_test.cs`**(类 `ConfidenceLeakTest`,**39 条**)

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/DiagnosisSystem/confidence_leak_test.cs` |
| 共享金标扫描真源 | `unity/Assets/Tests/EditMode/DiagnosisSystem/DiagnosisGoldenScan.cs`(story-002/003/004 共用) |
| 被测运行期 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisNegativeConfidenceTable.cs`(`DiagnosisNegativeConfidenceTable` 定表 + `DiagnosisNegativeConfidenceEvaluator` 求值器) |
| 阶段 2 全链 | `DiagnosisNegativeConfidenceBaker`(种子)→ `DiagnosisNegativeConfidenceBinder.Bind`(**唯一**校验点)→ `DiagnosisNegativeConfidenceCookedWriter`(编码) |
| 读方 | `unity/Assets/Gameplay.Presentation/Diagnosis/DiagnosisNegativeConfidenceCookedCodec.cs` |
| 真种子(正例) | `assets/data/diagnosis_negative_confidence.json`(合成旋钮;**数值归用户数值轮**) |
| 夹具 | `tests/unit/diagnosis_system/fixtures/neg_conf_*.json`(**14 个**,见下表) |
| 运行方式 | `unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem.ConfidenceLeakTest"` |

**C-4 正交的物化面**:F-8.3 与 F-8.1 **分表分文件、各走一条消费路径** —— 阴性族
(`diagnosis_negative_confidence.*` + `{NEG_*}` 旋钮)与阳性族(`diagnosis_read_floor.*` +
`{READ_*, READ_GAMMA}` 旋钮)在**绑定器键集**、**定表类型**、**求值器实参表**三处互不相认。

### AC → 测试函数映射

| AC | 测试函数(`ConfidenceLeakTest` 内) | 性质 |
|---|---|---|
| **AC-8-10 C-3 死内容扫描** | `test_ac810_seedNegatives_noDeadContent`(四条阴性逐条算 `L*_j`,`≤ SKILL_CAP` 且非 `NeverExcludes`,**点名**违规行) · `test_ac810_deadContentAlarm_namesRow`(合成死内容定表 ⇒ 报警面真实,非恒绿) | BLOCKING |
| **AC-8-11 C-7** | `test_ac811_negativeGroup_lStarEqualsTierNamed`(`L*_j ≥ tier_named_j`;阴性组四行同锚 `tier_named=20` ∧ `L*=20`) | BLOCKING |
| **AC-8-12 C-4 正交** | `test_ac812_readGamma_doesNotMoveNegativeFamily`(阴性烘焙接缝**无**读地板入口 + 阴性族**确实**响应自己的旋钮 + 该旋钮**确实**驱动阳性可读性) · `test_ac812_negGamma_doesNotMovePositiveFamily`(镜像:阳性烘焙接缝**无**阴性定表入口 + 阳性族**确实**响应自己的 READ_GAMMA) · `test_ac812_noCrossFamilyParameter`(两求值器实参表互不相认 + 两表字段名零跨族 token) · `test_ac812_separateFiles_crossFeedRejected`(**交叉喂源两方向皆硬失败** + 反向自证各自真源能过) —— ⚠️ 字面的「换旋钮重算另一族」**按构造不可表达**(烘焙唯一入参 = 本族源文本,那正是 C-4 的物化面),故四条测均取**等价可证伪命题**(接缝实参表 + 反向响应),**已登记为口径替代** | BLOCKING |
| **AC-8-14 多证据不合并** | `test_ac814_multiEvidence_notMerged`(两强一弱;调低第三条不改前两条) · `test_ac814_noAggregateConfidenceType`(反射扫描:前缀内零聚合置信度字段/置信度条) | BLOCKING |
| **AC-8-15 `neg_weight` 只影响排除路径** | `test_ac815_positiveWeight_hasNoEffectOnReadabilityOrExclusion`(阳性误填 ⇒ 可读性/四态不变、恒不构成排除、把握度走兜底) · `test_ac815_positivePolarityGate_isLoadBearing`(高兜底夹具:阳性把握度**已达阈值**仍不得构成排除 —— 极性门是唯一防线) | BLOCKING |
| **把握度 clamp**(F-8.3 Note 3) | `test_confidence_clampedToUnitRange`(`W_j > 1` 合法 ⇒ 乘积 4.0 钳到 1.0;负权重钳到 0;全档 ∈ [0,1]) | BLOCKING |
| **Q16 常量锚定**(D-FIX 禁 `Fix.OneRaw`) | `test_q16one_matchesFixCanonical`(本地 `Q16One` 逐位锚到 `Fix.ToFloat()` / `FractionalBits`;`RawToFloat` 唯一换算出口) | BLOCKING |
| **退化表消费 = 硬失败** | `test_curveat_unloadedTable_throws`(`default` 定表不得静默返回 `0f` —— 否则 C-3 死内容报警被伪装) | BLOCKING |
| **AC-8-16 F-8.4 无随机** | `test_ac816_tenThousandReplays_bitIdentical`(N=10⁴,**交错行×档**驱动,与独立重算表逐位对) · `test_ac816_productionAssembly_zeroPrngCallSites`(源层 + IL 层零 PRNG;扫描面非空) | BLOCKING |
| **AC-8-17 极性不翻转** | `test_ac817_polarityNeverFlips`(六档;阳性不得见 `Negative`、阴性不得见 `Positive`;输出 ⊆ 字母表) | BLOCKING |
| **AC-8-18 F-8.5 不泄漏(公式层)** | `test_ac818_confidenceArgTable_lacksSignValue`(求值器零 `float`/`double` 形参 + `C_neg` 只吃 `int`) | BLOCKING |
| **AC-8-F4 不泄漏(数值层,两极性)** | `test_ac818_ac8f4_noLeak_bothPolarities`(阳性/阴性各一条;F-8.1 侧**确实**随 `Sign_j` 分叉 ⇒ 非恒真;F-8.3 侧逐位等于独立重算 `C_neg×W_j`) | BLOCKING |
| **AC-8-F1 `L* ∈ (15,20]`** | `test_ac8f1_signAbdSoft_lStarInHalfOpenRange`(`sign_abd_soft`;`L*=20` + 等价式 `C_neg(15)<MIN ≤ C_neg(20)`) · `test_ac8f1_lv15AndLv20_readAsNegativeNotPositive`(UC-8-F1 公式级) | BLOCKING |
| **AC-8-50 回归锚常驻** | `test_ac850_anchorSuiteResident`(F1/F2/F4/F5 四条入口名存在性) | BLOCKING |
| **AC-8-F5 Mono 侧自洽** | `test_ac8f5_monoSideSelfConsistent`(同源双烘逐位一致 + 定表值自洽);**跨平台三格矩阵 NOT-RUN** | BLOCKING(限 Mono 侧) |
| **构建期校验(正例)** | `test_bind_seedAndLegalBaseline_pass`(种子 + 合法夹具真通过;定表 61 档 + `skill_cap` 同源) · `test_bind_halfGammaAndPlateau_pass`(C-5 半整数 + 平段) | BLOCKING |
| **C-5 违例**(G-1 闭集) | `test_bind_gammaThird_red` · `test_bind_gammaFloatToken_red` | BLOCKING |
| **C-6 违例** | `test_bind_zeroWeight_red`(`neg_weight_fallback = 0`) | BLOCKING |
| **F-8.3 值域违例** | `test_bind_capBelowZero_red`(曲线反向) · `test_bind_excludeAboveCap_red` · `test_bind_conf0OutOfRange_red` | BLOCKING |
| **绑定层**(skill_cap 漂移 / 未知键 / schema 版本) | `test_bind_skillCapDrift_red` · `test_bind_unknownKey_red` · `test_bind_schemaVersionMismatch_red` | BLOCKING |
| **C_neg 形状** | `test_curve_monotonicAndEndpoints`(`C_neg(0)=NEG_CONF_0` · `C_neg(CAP)=NEG_CONF_CAP` · 60 对单调) | BLOCKING |
| **codec 硬失败(E-13)** | `test_codec_badMagic_red` · `test_codec_truncatedPayload_red` · `test_codec_schemaMismatch_red` · `test_configVersion_deterministic` | BLOCKING |

### 夹具清单(`fixtures/neg_conf_*.json`,14 个)

| 夹具 | 单因违例 / 角色 |
|---|---|
| `neg_conf_legal_baseline.json` | 正例(与种子同旋钮) |
| `neg_conf_half_gamma.json` | 正例(`neg_gamma = 1/2`,走 `FixPow` 半整数支;C-5 闭集内) |
| `neg_conf_plateau_cap_eq_zero.json` | 正例(平段:`neg_conf_cap == neg_conf_0`,序关系非严格) |
| `neg_conf_strict_monotonic.json` | 正例(`0 → 1` + `gamma=2`,严格单调) |
| `neg_conf_high_fallback.json` | 正例(兜底权重 = 1.0 —— 让**阳性把握度达阈值**,使极性门成为唯一防线) |
| `neg_conf_gamma_third_red.json` | C-5 违例(gamma `1/3`,闭集外) |
| `neg_conf_gamma_float_token_red.json` | gamma JSON 数字(禁 double 中转;须字符串) |
| `neg_conf_zero_weight_red.json` | C-6 违例(`neg_weight_fallback = 0`) |
| `neg_conf_cap_below_zero_red.json` | F-8.3 违例(`cap < 0`,曲线反向) |
| `neg_conf_exclude_above_cap_red.json` | F-8.3 违例(`exclude_conf_min > neg_conf_cap` ⇒ 全表死) |
| `neg_conf_conf0_out_of_range_red.json` | F-8.3 违例(`neg_conf_0 < 0`,值域外) |
| `neg_conf_skillcap_drift_red.json` | skill_cap ≠ 30 侧 SKILL_CAP(59) |
| `neg_conf_unknown_key_red.json` | 未知键(ADR-014 §三) |
| `neg_conf_schema_version_red.json` | `schema_version = 2` ⇒ 构建期硬失败(ADR-014 §五;修复轮 —— 堵「漏到运行期装载才抛 E-13」) |

> 读法纪律同 story-002/003:违例经 `DiagnosisNegativeConfidenceBinderProbe.Bake` 端到端黑盒驱动
> (**生产同一台机器**),断言 = 抛 + **恰一条错误**(排他)+ 触底规则 tag(`NegBakeFails` helper)。

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)

- **AC-8-F5 的跨平台三格矩阵**(ADR-012:Linux-x64-Mono / Linux-x64-IL2CPP /
  Linux-ARM64-IL2CPP)—— 未实跑;本 story 只证 **Mono 侧自洽**(同进程双跑 + 定表值自洽)。
  跨平台逐位面归 ADR-012 矩阵,禁借绿。
- **AC-8-16 的「跨两个独立进程」半边** —— EditMode 无法起独立进程;本 story 证**同进程**
  N ≥ 10⁴ 次逐位相同 + 静态守门零 PRNG(跨进程确定性由 ADR-005 结构性保证,判据归 ADR-012 矩阵)。
- **AC-8-13(UC-8-F1 端到端)** —— 依赖 9 侧 `Project(Sign_j)` 未落地(disease-simulation
  story 004),本 story Out of Scope;公式级 AC-8-F1 在此判。
- **数值轮占位**(承 story-002 S-9 / story-004 Note 7)—— R-8.2 阴性条目的 `neg_weight = "1"`
  与 `assets/data/diagnosis_negative_confidence.json` 的五个旋钮均为**合成值**(GDD 原值 `*待裁*`)。
  本 story 以合成旋钮判**形状**(存在性 / 序关系 / 不等式 / 正交性),**不把 `1` 当终值**;
  数值轮落定后须重签相关向量。
- **AC-8-35 的 F-8.3 曲线参数半边** —— 同 story-003:参数住 `assets/data/*.json`(数据,
  非代码常量),由 ConfigVersion 覆盖;金标面 = 前缀 ns 代码常量 + DIAG_TIERS。

### 金标重钉记录

`GoldenConstantsHash`:`b9354110`(story-002 建立)→ `5bba361c`(story-003 首轮)
→ `f75a8170`(story-003 修复轮)→ **`a7bfec31`**(story-004)→ **`72db379c`**(story-004 评审修复轮:**消重** —— 求值器自持的
第二份 `Q16One` 副本删除,前缀 ns 常量少一行)。story-004 入面的四个新常量即
F-8.3 的**结构形状**:`ExpectedSchemaVersion=1` · `FixedHeadBytes=48` · `NeverExcludes=-1` ·
`Q16One=65536`(×2 处),**非数值轮产物**。
