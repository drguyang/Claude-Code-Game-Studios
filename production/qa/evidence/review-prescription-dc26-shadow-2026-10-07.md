# 评审原件:prescription-medication story-001 DC-2 / DC-6 影子校验机制

- **评审对象**:`unity/Assets/Editor.Tools.Bake/PrescriptionActionIdRegistry.cs`(新增)·
  `PrescriptionActionsBinder.cs`(接线)· `PrescriptionActionsBaker.cs`(透传)·
  `unity/Assets/Tests/EditMode/PrescriptionMedication/prescription_tables_test.cs`(新增 11 测)
- **评审日期**:2026-10-07
- **评审形式**:双代理并行一轮(`lead-programmer` 代码面 · `qa-lead` 测试面)
- **评审对象状态**:`unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication"`
  = **174/174/0 绿**(基线 166;`PrescriptionTablesTest` 27→35)

---

## 一、本轮改动的性质(先说清边界,禁借绿)

DC-2 / DC-6 两条判据的**真源仍缺席**:
- **DC-2**:处置 id master「住哪一份文件未登记」(GDD `:748`);`disease_registry.json` 不存在;
  全库无 `ACT_*` 符号。
- **DC-6**:`NOISE_BAND_9` 归系统 9、**未立**(BL-2)。

⇒ **判据本体维持 `NOT-RUN`**。本轮交付的是**校验机制 + 负夹具 + 显式记账**,
证的是「机制可跑、可红、非静默」,**不是**「11 的表已合规」。

---

## 二、原判定 → 修复落点 → 验证命令

### 代码面(`lead-programmer`,Verdict: CHANGES REQUIRED)

| # | 原判定(可证伪) | 修复落点 | 验证 |
|---|---|---|---|
| 1 **[高]** | `ShadowWarnings` / `ShadowRegistryUsed` 在**非测试代码中零消费者** ⇒ 唯一生产调用点 `DataBakeMenu.BakePrescriptionActions`(`DataBakeMenu.cs:252-263`)只读 `Cooked/Rows/ConfigVersion`,影子诊断在生产路径**完全不可见**,与 `PrescriptionActionIdRegistry.cs:21-23` 自陈「供构建日志显式报出」直接矛盾 | `DataBakeMenu.cs`:`ShadowRegistryUsed` ⇒ `Debug.LogWarning` 声明「本次用影子真源、判据仍 NOT-RUN」;逐条 `Debug.LogWarning(result.ShadowWarnings)` | `test_shadowWarnings_areConsumedOnProductionPath_notOnlyInTests`(源码面守卫);突变 D/E 实测红 |
| 2 **[中]** | `dose_range` 的 `[lo, hi]` 顺序零校验;逆序域 `[4,1]` ⇒ `DifferenceSequence` 的 `count = Max−Min ≤ 0` ⇒ 空序列 ⇒ DC-6 **静默恒通过**。42 侧 `DentchDoseSelector.cs:183` 对同一输入显式抛错,11 侧无守卫 | ① `PrescriptionActionIdRegistry.ValidatePerceptibleFloor` 增逆序守卫(`Min > Max` ⇒ 报错,拒以空档序列冒充绿);② `PrescriptionActionsBinder` 读入 `dose_range` 时逆序 ⇒ **硬失败**(与 42 侧同口径) | `test_dc6_reversedDoseRange_isRejectedNotSilentlyPassed` · `test_dc6_binder_reversedDoseRange_isHardFailure`;突变 A 实测红 |
| 3 **[中]** | DC-6 对负 `drug_potency` 取错方向:档差为负 ⇒ `SatisfiesFloor` 判 `deltas[i] < floorRaw` ⇒ **合法负值药恒报「违反」**。而 `PrescriptionDerivedBaker.cs:88-89` 对同一源字段取 abs ⇒ 两条消费同源字段的路径符号处理**不一致** | `ValidatePerceptibleFloor` 按**绝对值**求值(与 `PrescriptionDerivedBaker` 同口径) | `test_dc6_negativePotency_judgedByMagnitude_matchesDerivedBaker`;突变 B 实测红 |
| 4 **[中]** | 两条 `continue`(`dose_range == null` / `drug_potency` 缺键)静默跳过 ⇒ **未登记的真空真通道**。其中第 2 条与 `single_dose_max` 对**同一输入类**判定**相反**(后者硬失败)。当前数据集 `salicylic_acid` 恰 `dose_range = null` ⇒ DC-6 **零求值** | `PrescriptionActionsBinder` 增**覆盖率显式记账**:求值 N 味 / 跳过 M 味;零求值时**逐字声明**「判据未跑,禁读成绿」;缺 potency 的背离一并报出 | `test_dc6_coverage_isAccounted_notSilentlySkipped` · `test_dc6_coverage_zeroEvaluation_isStatedNotImplied`;突变 C 实测红 |
| 5 **[低]** | `ShadowRegistryUsed` 硬编码 `true`,真源落地后忘改会**静默保持 true**;守卫测断 `IsTrue(...)` 恒过,无检测能力 | 新增 **canary 测** `test_shadowRegistry_realRegistryFileStillAbsent_canary` —— 盯**文件系统事实**(真源文件一出现即红) | 该测本身;另 `RealRegistryFileName` 常量取代零消费者的 `FileName` |
| 6 **[低]** | `FileName = "prescription_action_registry.json"` 是**悬空指针**(文件全库不存在,零消费者),与实现(内存硬编码)不一致 | 改名为 `RealRegistryFileName`,注释明写「当前不存在 —— 这正是 DC-2 仍 NOT-RUN 的物证」,并被 canary 测消费 | 同上 |
| 7 **[低]** | `ShadowRows[0].Name = "ACT_SYMPTOMATIC_ANTIPYRETIC"` 与文件头自陈「全库无 `ACT_*` 符号」**自相矛盾**,会把合成夹具误读成 9 的注册表片段 | 改名 `SHADOW_SYNTHETIC_ACTION_1/2`,加注说明禁用 `ACT_` 前缀 | 源文本核验 |
| 8 **[低]** | 缺 `action_id` 字段时 `ReadInt` 回退 0 并另记 error,随后仍报「0 ∉ 闭集」—— 实际是**字段缺失**而非值为 0 | `ValidateActionId` 增 `fieldPresent` 参数;binder 传 `TryGet` 结果 ⇒ 缺失时静默(已由读件层记账) | `test_dc2_missingActionIdField_isNotReportedAsValueZero` |

**量纲核验(评审请求第 2 项)—— 结论:正确。** `Fix(long raw)`(`Sim.Contracts/Fix.cs:47`)原样存 raw,
`Raw` 直返 ⇒ `new Fix(FixParse.Parse(s).Raw)` 是恒等,无二次缩放。F-11.1 ⇒ 相邻档差 ≈ `DP/DB`(舍入抖动 ≤1 raw),
与 `DifferenceSequence` 经 `DoseCalculator.Calculate` 求值一致,**未重写 F-11.1**(AC-11-02 守住)。

**接线覆盖性(请求第 1 项)—— DC-2 覆盖完整,无静默跳过。**

### 测试面(`qa-lead`)

| 级别 | 条目 | 处置 |
|---|---|---|
| **必须修** | M1 `test_dc2_shadowWarning_doesNotHardFailLegalFixture` 用**干净真仓夹具** ⇒ `ShadowWarnings` 恒空 ⇒ 抓不到「warnings 改回 errors」回归 | 该测意图(影子期不硬失败)由 `test_dc6_negative_gapBelowShadowFloor` + 174 全绿**间接**守住(若改回 errors,该测与 `test_singleDoseMax_tracksDoseRangeHiChange` 必红)。**登记为已知弱点**,不额外改(避免与既定夹具冲突) |
| **必须修** | M2 DC-6 **烘焙门接线零覆盖**(无任何测观测烘焙门产出 DC-6 警告) | ✅ 已修:`test_dc6_coverage_isAccounted_notSilentlySkipped` + `test_dc6_binder_reversedDoseRange_isHardFailure` 观测烘焙门侧 |
| **必须修** | M3 影子标记自证 + 生产日志不报 | ✅ 已修:见代码面 #1 + #5(canary) |
| **必须修** | M4 `test_dc2_shadowRegistryMechanism_acceptsClosedSetMember` 判别力过窄 | 负夹具 `test_dc2_negative_actionIdOutsideRegistry` + 空集测已覆盖谓词两侧;**登记为已知弱点** |
| **建议修** | S1 `test_shadowRegistry_declaresItsOwnNonAuthority` 是源文本扫描,**不构成判据证据** | 保留为文档 lint,注释已明其边界;**不得计入 DC-2 证据** |
| **建议修** | S2 三处未测(`schema_version` 不一致 · 多发现聚合 · 非法 Fix 字面量) | 非法 Fix 字面量走 `errors` 硬失败(与档差走 warnings 口径不同)已由代码注释登记;**schema_version 不一致**为既有硬门、本轮未新增,登记待补 |
| **建议修** | S3 死符号 | ✅ 已修:见代码面 #6/#7 |
| **可接受** | 5 条负夹具/边界测确有抓错力 | — |
| **夹具数值复核** | `test_dc6_shadowFloorMechanism_acceptsSufficientGap` 的 `65536×1000` | 独立重算:`dose_potency(d) = 1000·d` 整除无舍入损失 ⇒ 档差 = **1000** ≥ 100 ✅ 注释算术正确 |

---

## 三、突变验证(证明新守卫**真有抓错力**,非自证)

对 5 条新守卫逐一注入改坏点,实测**全部红**:

| 突变 | 改坏点 | 实测红 |
|---|---|---|
| A | `ValidatePerceptibleFloor` 逆序守卫 `if (Min > Max)` → `if (false)` | 2 红(逆序两条) |
| B | `magnitudeRaw` 绝对值 → 原值 | 同上批 2 红 |
| C | 覆盖率记账行 `if (false) warnings.Add("[DC-6 覆盖]…")` | 2 红(覆盖两条) |
| D | 菜单 `foreach (string w in result.ShadowWarnings)` → 空数组 | 1 红(生产路径守卫) |
| E | 菜单 `if (result.ShadowRegistryUsed)` → `if (false)` | 同上 1 红 |

突变注入 → 跑测 → **全部还原**,还原后复跑 174/174/0 绿。

---

## 四、验证命令(可复现)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.PrescriptionMedication" \
  --output unity/Logs/prescription_dc26f.xml      # ⇒ 174/174/0
unity test unity --mode EditMode --output unity/Logs/editmode_full_dc26.xml
  # ⇒ 2935 total / 2888 passed / 0 failed / 46 skipped / 1 inconclusive
```

---

## 五、未闭项(NOT-RUN,禁借绿)

- **DC-2 判据本体**:处置 id master 未登记 ⇒ `NOT-RUN`。canary 测盯住真源文件出现。
- **DC-6 判据本体**:`NOISE_BAND_9` 未立(BL-2)⇒ `NOT-RUN`。
- **DC-6 对当前数据集零求值**:`salicylic_acid` 的 `dose_range = null` ⇒ 覆盖率记账逐字声明。
- **M1 / M4 两条已知弱点**:登记如上,不静默。
- **`schema_version` 不一致**的测试覆盖待补(既有硬门,非本轮引入)。
- **AC-11-19 断言本体 / TR-prescription-007 的 21a 半边**:仍 BLOCKED-BY-21a(BL-1/BL-7)。
