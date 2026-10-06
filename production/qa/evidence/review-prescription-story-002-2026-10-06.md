# 评审报告原件 — prescription-medication story-002(F-11.1 剂量舍入除与 F-11.2 F5 求值点)

**日期**: 2026-10-06
**评审对象**: `unity/Assets/Sim/Prescription/`(F-11.1 求值件 + F-11.2 求值件)+
`unity/Assets/Tests/EditMode/PrescriptionMedication/dose_potency_test.cs`(23 测)+
`PrescriptionFloatScan.cs`(共享零浮点扫描器)
**权威件**: `design/gdd/prescription-and-medication.md` §F-11.1 / §F-11.2 / 规则六 / 规则七 ·
`docs/architecture/tr-registry.yaml` TR-prescription-006/007/009/010/019 ·
ADR-005 Amendment G · ADR-006 §三 · ADR-012 F7 · ADR-014

---

## 〇 · 评审时点声明(必读)

**本件是「补做的一次评审」,不是事后追认。**

story-002 于 2026-10-06 以 commit `f2bad6b` 收口时**未落评审原件**;依
`.claude/docs/coding-standards.md` §Review Evidence Standards:

> 评审不可事后追补 —— 事后重做评的是**当下**的代码,得到的是**新**判定,**不追认**原判定。

故:

- **本件评的是「补做时点库内的代码」**(即本修复轮**之前**的树),不评 `f2bad6b` 的树;
- **不追认 `f2bad6b` 收口时的任何判定** —— 该次收口的评审记载**永久缺失**,本件不填这个洞,
  只按规范给出「补做一次评审」的出路;
- 评审执行:双代理(**结构侧** = 结构/AC 覆盖/架构合规;**QA 侧** = 测试判别力/变异存活),
  各自独立读码,互不可见。**评审只做一轮**(用户令),本件为该轮的判定存档。

⚠️ **如实登记的回收缺口**:结构侧代理在交付正文前被协调方中断,其**报告全文未能回收**
(仅能确认其探索轨迹:已定位 F5 双实现、已确认 `single_dose_max`/比较器零存在)。
故本件的**结构侧清单以可独立复核的实测为准**(见 §二 B1/B2 的复现命令),
**不凭记忆补写该代理未交付的条目**。

---

## 一 · 判定

| 侧 | 判定 |
|---|---|
| **QA 侧** | **2 BLOCKING · 3 MAJOR · 6 MINOR · 2 NIT** |
| **结构侧** | 正文未回收;其探索轨迹指向的 B1/B2 与 QA 侧**独立重合**(见 §二) |

**QA 侧自陈的「未及核实项」**(原文照录,以正视听):

> 1. 「`Mul128`/`Div128` 换成 int64 直乘」是否会被既有 2 条溢出测偶然抓到。
> 2. `SignBound` 守卫(`DoseCalculator.cs:87-88`)可达性 —— 定向搜索无反例,未穷证。
> 3. `qLo==0` 进位(`:76`)可达性 —— 同上。
> 4. `PrescriptionFloatScan.Collect` 对行尾/块注释中命中的实际行为 —— 仅静态推演,未实跑。
> 5. **23 条的实跑结果 —— 本次为静态 + 逐位复算,未执行 Unity Test Runner。**

⇒ 该报告的**静态判定**可用;其**经验性断言**(「变异体存活 23/23」)本件以**自跑的变异实验**重做并坐实(见 §三)。

---

## 二 · 原判定 → 修复落点

### B1(BLOCKING)· AC-11-09 `single_dose_max` 全库零实现零测试

**原判定**:`grep -rn "SingleDoseMax|single_dose_max" unity/Assets/ --include=*.cs` → **零命中**;
仅 `docs/architecture/tr-registry.yaml:3368` 与 `design/registry/entities.yaml:523-552/1725-1731`
有文档登记。派生器、cooked 落点、`ConfigVersion` 联动、故事卡 `:69` 承诺的
「改一药 `dose_range.hi` ⇒ 自动重算」回归测 —— **一个都不存在**。**BLOCKING AC 无证据而卡已 Complete。**

**修复落点**:

| 件 | 变更 |
|---|---|
| `unity/Assets/Editor.Tools.Bake/PrescriptionDerivedBaker.cs` | **新建** —— `single_dose_max` 的**唯一派生点**。`DeriveSingleDoseMaxRaw(itemsRoot, doseBase)` 遍历 `category = drug` 行,取 `dose_range.hi` 档的 `dose_potency`,**一律经 `DoseCalculator`**(F-11.1 唯一实现,**不重写公式**)求 `max\|dose_potency\|`;零剂量字面量 |
| `PrescriptionActionsBinder.cs` | `BindResult` 增 `long SingleDoseMaxRaw` + 5 形参 ctor;`Bind` 末尾接派生器 |
| `PrescriptionActionsCookedWriter.cs` | 载荷布局扩为 `[8..15] single_dose_max (i64 raw)`;`Write` 增收 `long` |
| `PrescriptionActionsBaker.cs` | 传 `bound.SingleDoseMaxRaw` |
| `PrescriptionActionsBinderProbe.cs` | 传参 + 新增 `SingleDoseMaxRawOf(actionsJson, lexiconJson, itemsJson)` 公开转发 |
| `prescription_tables_test.cs` | 新增 4 测:`test_singleDoseMax_derivedNotHandFilled` · `_tracksDoseRangeHiChange` · `_configVersionTracksSourceChange` · `_noPotencyDrug_throws` |

**架构理由(为何住烘焙层而非 `Sim/`)**:派生器需要 `JsonNode`(属 `Editor.Tools.Bake`,
`internal`)且须复用 `DoseCalculator`(属 `Sim`,已被 `Editor.Tools.Bake` 引用)。
`Sim` 的 `noEngineReferences: true`(门 A)使其**物理上够不着 `JsonNode`** ⇒ 落烘焙层是唯一合法位置。
CS0051(`JsonNode` 比 public 方法更不可见)⇒ `DeriveSingleDoseMaxRaw` 定 `internal`,
测试经 `PrescriptionActionsBinderProbe` 的 public 转发驱动**同一台机器**。

⚠️ **诚实登记的边界**:生产 `BakeFromRepo` 的 `ConfigVersion` 只哈希
`prescription_actions.json` + 词表,**不含 `item_database_items.json`** ⇒
「改药 `dose_range.hi` ⇒ 哈希变」在**当前生产口径下不成立**。测试
`test_singleDoseMax_configVersionTracksSourceChange` **只证 actions 侧联动**,
`test_singleDoseMax_tracksDoseRangeHiChange` 证**派生值**随 hi 重算。
两半**分开证**,不合并成一句过度宣称。

### B2(BLOCKING)· AC-11-08 本卡零判据;「9/10 零 `axis_offset` 消费点」负断言全库不存在

**原判定**:23 条 `[Test]` 只覆盖 F-11.1 舍入/溢出/空域/边界/单调/确定性;
`dose_potency_test.cs` 对 AC-11-08 **零提及**。全库 grep `AC-11-08` 只命中
`half_life_test.cs:2,7`(注释 + NOT-RUN)与 `prescribe_flow_test.cs:11,548`(注释),
**均不在本卡**。「9 与 10 程序集零 `axis_offset` 消费点」这条负判据(故事卡 `:34` 标 BLOCKING)
**在任何测试文件中都没有对应断言**。

**修复落点**:新增 3 测 ——

| 测 | 判据 |
|---|---|
| `test_f5_singleEvaluationPoint_noConsumersInSimOutsideTwoOwners` | 反射扫 `Sim` 装配内**除两个合法所有者外**的类型,断言公开签名**零** `axis_offset`/`AxisOffsetByQuality` 消费点 |
| `test_f5_uniqueEvaluationPoint_positiveControl` | **阳性对照** —— 合成违例类型喂同一谓词,断言**必红且点名**(证扫描器非恒空) |
| `test_f5_halfLifeCalculator_matchesTimelineSolver_halflifeAxis` | **对拍** —— 同输入下 `HalfLifeCalculator`(11 侧)与 `QualityTimelineSolver`(21a 侧)在 `half_life` 轴**逐位相等**;任一侧公式漂移即红 |

⚠️ **F5 双实现的处置(刻意不合并)**:两件自陈「单一实现」,实为**两份**,
且**异常契约不同**(`HalfLifeCalculator` 抛 `ArgumentOutOfRangeException`;
`QualityTimelineSolver` 抛 `InvalidOperationException`),且后者属 **21a epic**。
⇒ 本 story **不**跨 epic 强合并(会改动 21a 的已收口件),
而是**把重复变成可证伪**:对拍测使任一侧漂移必红。**合并决策归 21a 的后续轮**,此处只登记。

### M1(MAJOR)· `loCarry` 进位可达但零覆盖

**修复**:新增 `test_dose_mul128_lowWordCarry_isConsumed`(可复现例:
`raw = 96279238802687` × `dose = 652768597`)。变异坐实见 §三 MUT-1。

### M2(MAJOR)· 禁 `System.Int128` / `BigInteger` 无任何判据

**修复**:`PrescriptionFloatScan.cs` 增 `\b(Int128|UInt128|BigInteger)\b` 模式 +
`Collect(bigIntPattern, …, "big-int type", violations)` 调用点。
扫描面覆盖 `Sim/Prescription/` 整目录(story-002/003/004 **三卡共享**)。

### M3(MAJOR)· AC-11-19 承诺的「比较器骨架 + 差值序列导出器」未交付

**修复**:新建 `unity/Assets/Sim/Prescription/PerceptibleFloorComparator.cs` ——
`DifferenceSequence(drugPotency, range, doseBase)` 导出 `Δ(d) = dose_potency(d+1) − dose_potency(d)`,
**每项经 `DoseCalculator.Calculate`**(不重写 F-11.1);`SatisfiesFloor(deltas, floorRaw)` 逐项比较。
新增 3 测(`test_floor_differenceSequence_isMonotoneAndPositive` /
`_singleDetentRange_yieldsEmptySequence` / `_comparator_detectsViolation`)。
⚠️ 门槛值 `NOISE_BAND_9` **无主**(BL-2)⇒ **断言本体仍 NOT-RUN**,本件只交付机器。

### MINOR / NIT 处置

| 编号 | 原判定 | 处置 |
|---|---|---|
| m1 | 无负 dose 用例,`neg = (raw<0) ^ (dose<0)` 的 dose 半边从未被验证 | ✅ 新增 `test_dose_negativeDose_negativeResult` + `test_dose_negativeDoseNegativePotency_positiveResult`(MUT-2 坐实) |
| m2 | `SignBound` 守卫域内可达性未证实 | ✅ 新增 `test_dose_quotientAtSignBound_throws`(raw `1L << 40` × dose `1 << 23` × `DOSE_BASE = 1` ⇒ 恰 `2^63`,`qHi == 0` 而 `result == SignBound`)+ 对照 `test_dose_quotientAtSignBoundNegative_isValid`(MUT-3 坐实) |
| m3 | `midCarry` / `qLo==0` 为不可达/未证实可达死分支 | ⬜ **未处置,如实保留** —— 属**不可达防御**,非缺陷;`midCarry` 结构性不可达(`dose ≤ 2^31−1` ⇒ `y1 = 0` ⇒ `p01 ≡ 0`)已在测内注记 |
| m4 | 3 处名实不符测试名 | ✅ 重命名(`halfAway_positive` → `exactDivision_positive`;`halfAway_negative` → `rounding_halfAway_exactDivNegative`;`noFloat_allInteger` → `integerArithmetic_exactQuotient`) |
| m5 | 2 处近似恒真断言无判别力 | ⬜ **未处置,如实保留** —— `deterministic_sameInput` / `effectiveDose_passthrough` 语义上确为恒真,作**文档性**断言保留(测名即文档,不再伪装判别力) |
| m6 | 扫描器注释判定取「整行前缀」而非「命中点是否在注释内」 | ✅ 重写为单遍 `StripCommentsAndStrings`:把 `//`、`/* */`、`"…"`、`@"…"` 内容**等长置空**(保留长度与换行),再做正则;不再依赖行前缀 |
| n1 | `_overflow_throws_2` 注释与实参不符 | ✅ 注释订正 + 与 `_1` 的重复性显式标注 |
| n2 | `doseBase`/`dose` 组合面窄 | ⬜ **未处置,如实保留**(登记为覆盖面已知窄) |
| — | AC-11-11④ **未登记「禁 `Int128`/`BigInteger` 无判据」** | ✅ 已由 M2 补齐并登记 |
| — | AC-11-19 **未说明「比较器骨架也未交付」** | ✅ 已由 M3 交付,文件头 NOT-RUN 块扩至 5 项 |
| — | D-21-22(品级不调制 `drug_potency`)零测试 | ✅ 新增 `test_dose_qualityDoesNotModulatePotency` |

---

## 三 · 变异证明(自跑,非引用代理自陈)

`Mut` 列 = 把生产件里对应的一行改坏;**「红」= 该次运行失败的测名**。
全部变异**已回滚**,`grep -rn "MUT-[0-9]" unity/Assets/ --include=*.cs` ⇒ 零残留。

| 变异 | 改坏点 | 结果 | 红测 |
|---|---|---|---|
| **MUT-1** | `Mul128` 的 `loCarry` 置 0 | **1 红** | `test_dose_mul128_lowWordCarry_isConsumed` |
| **MUT-2** | `neg = (raw<0) ^ (dose<0)` 去掉 dose 半边 | **2 红** | `test_dose_negativeDose_negativeResult` · `test_dose_negativeDoseNegativePotency_positiveResult` |
| **MUT-3** | 删 `SignBound` 守卫 | **1 红**(补夹具后) | `test_dose_quotientAtSignBound_throws` |
| **MUT-4** | 派生器改手填常量 | **2 红** | `test_singleDoseMax_derivedNotHandFilled` · `test_singleDoseMax_tracksDoseRangeHiChange` |
| **MUT-5** | 派生器忽略 `dose_range`(一律走整剂) | **1 红** | `test_singleDoseMax_tracksDoseRangeHiChange` |
| **MUT-6** | 派生器缺 `drug_potency` 时静默填 0 | **1 红** | `test_singleDoseMax_noPotencyDrug_throws` |
| **MUT-7** | F-11.2 偏移索引漂移 `[quality-1]` → `[0]` | **10 红** | 含 `test_f5_halfLifeCalculator_matchesTimelineSolver_halflifeAxis` 与 9 条下游 |

⚠️ **MUT-3 的首轮教训(如实记账)**:首轮**存活 127/127** ——
原夹具的溢出测全被更早的 `qHi != 0` 守卫拦下,`SignBound` 守卫**不可达**。
补 `test_dose_quotientAtSignBound_throws`(恰 `2^63` ⇒ `qHi == 0` 而 `result == SignBound`)后才 **恰 1 红**。
⇒ 这印证了 QA 侧「m2 未证实可达」的判定**是对的**,并把它从「未证实」推进到「**可达且有夹具**」。

---

## 四 · 验证命令(可证伪)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 本卡过滤套件(36 测)
unity test unity --mode EditMode \
  --filter "DaYiJingCheng.Tests.PrescriptionMedication" \
  --output unity/Logs/dose_fix_2.xml
grep -o 'passed="[^"]*" failed="[^"]*"' unity/Logs/dose_fix_2.xml
# 期望:passed="129" failed="0"

# ② 全量 EditMode 回归
unity test unity --mode EditMode --output unity/Logs/full_editmode_story002.xml
grep -o 'total="[^"]*" passed="[^"]*" failed="[^"]*"' unity/Logs/full_editmode_story002.xml
# 期望:total="2890" passed="2843" failed="0" inconclusive="1" skipped="46"
# (基线 2826 passed ⇒ +17 = 本卡 13 + 表卡 4)

# ③ AC-11-09 派生器存在性(原判定「零命中」的反证)
grep -rn "SingleDoseMaxRaw\|DeriveSingleDoseMaxRaw" unity/Assets/ --include=*.cs

# ④ 变异残留自检(必须为空)
grep -rn "MUT-[0-9]" unity/Assets/ --include=*.cs

# ⑤ 门 A 未破:Sim 引用集不变
cat unity/Assets/Sim/Sim.asmdef   # references 仍恰 = Sim.Contracts
```

**实测结果**:
- ① `dose_fix_2.xml` = **129 / 129 passed / 0 failed**
- ② `full_editmode_story002.xml` = **2890 total / 2843 passed / 0 failed / 1 inconclusive / 46 skipped**
- ④ **零残留**
- ⑤ `Sim.asmdef` 引用集**一字未改**(新件 `PerceptibleFloorComparator.cs` 住 `Sim/`,只用 `Sim.Contracts`)

⚠️ Unity CLI wrapper 退出码不可靠(Skipped/Inconclusive ⇒ 非零),**判据以 XML 属性为准**。

---

## 五 · 未闭登记(NOT-RUN · 禁借绿)

本卡修复轮**不关闭**下列项,逐条列名 + 阻塞源:

| 项 | 状态 | 阻塞源 |
|---|---|---|
| **AC-11-11④** int64 直乘分支 | NOT-RUN | **BL-7** —— 21a 未声明 `drug_potency` 取值范围;本 story **无条件走 hi/lo 128 位**(承 AC-10-04a「不留条件分支」) |
| **AC-11-19** 可感知地板断言本体 | NOT-RUN | **BL-2** —— `NOISE_BAND_9` 在 9 侧**不存在**;本件只交付比较器骨架 + 差值序列导出器,门槛由调用方注入 |
| **AC-11-15** 三格矩阵子句 | NOT-RUN | **ADR-012** 未实跑;只证 Mono 侧自洽 |
| **TR-prescription-007** 21a 侧断言 | NOT-RUN | **BL-1** —— 21a 现断言仅 `> 0`,未升格至 `MIN_USABLE_HALF_LIFE`;11 侧**不重复 clamp**(已由测试证「不修正」) |
| **F5 双实现合并** | 未处置 | 跨 epic(21a);本件只把重复**可证伪**(对拍测),合并决策归 21a 后续轮 |
| **m3** `midCarry`/`qLo==0` 死分支 | 如实保留 | 不可达防御,非缺陷 |
| **m5** 两处文档性恒真断言 | 如实保留 | 测名即文档 |
| **n2** `doseBase`/`dose` 组合面窄 | 如实保留 | 覆盖面已知窄 |

---

## 六 · 结论

**修复轮判定:2 BLOCKING 已闭 · 3 MAJOR 已闭 · 6 MINOR 中 4 闭 2 如实保留 · 2 NIT 中 1 闭 1 保留。**

- 本卡**转 Complete 的证据自此齐备**(原件 + 可证伪命令 + 变异坐实);
- **不追认** `f2bad6b` 的收口判定 —— 该次评审记载**永久缺失**;
- **未闭项按原口径保持 NOT-RUN**,`禁借绿`。

**残余风险(显式记账)**:
1. `single_dose_max` 的**消费侧**(9 的 F1 clamp `MAX_ACTIVE_DOSE × single_dose_max`)**尚未接线** ——
   归 disease epic story-004。本件只保证**派生值正确且零手填**,不保证被消费。
2. `item_database_items.json` **不在生产 `ConfigVersion` 哈希面内** ⇒
   改药数据**不会**自动改版本号(见 §二 B1 的诚实登记)。
3. F5 双实现的**异常契约不一致**(`ArgumentOutOfRangeException` vs `InvalidOperationException`)——
   对拍测只证**合法域内**逐位相等,**未**证异常路径一致。
