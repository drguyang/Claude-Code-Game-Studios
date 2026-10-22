# F4 求值体 + `last_intervention` 聚合器 + registry 4b —— 实现与证伪证据(2026-10-22)

**批次**:S18 后续 · 「值已裁 ≠ F4 可求值」三件缺口
**commit(改动前)**:`cb798e2`
**判据面**:GDD `disease-simulation.md` §F4 `:1080-1140` · 校验 4b / 14 / 17 · ADR-005 · ADR-016 §一/§二

---

## 一、为什么是这三件

2026-10-22 复核实测:「值已裁」与「可求值」是两件事。六个阈值常量已有裁定值并落在
`entities.yaml` + GDD §五,但:

| # | 缺口 | 实测(改动前) |
|---|---|---|
| ① | **F4 求值体不存在** | `F4OutcomeEvaluator` 全库只在**注释里**出现一次(`StateMachine.cs:123`,作为「裁定名」),无任何类型定义 |
| ② | **`last_intervention` 聚合器不存在** | 只有 `InjuryStateChangedWriter.cs:85` 的**形参**在传,无任何聚合实现 |
| ③ | **registry 侧 4b 零校验** | `RegistrySchema.cs:545` 自陈「`A_peak/SCALE ≥ DEATH_THRESHOLD` 那一半全库**零校验**」;`R1_CHECK_23` 实现的是 `Scale > 0 ∧ Scale ≥ A_peak` —— **完全不同的判据** |

**额外发现(比登记的更严重)**:`TREATMENT_WINDOW` / `CARE_GAP` / `RECOVER_THRESHOLD`
三个常量在 C# 侧**连符号都不存在**:

```
grep -rn "TREATMENT_WINDOW\s*=\|CARE_GAP\s*=\|RECOVER_THRESHOLD\s*=" unity/Assets/ --include=*.cs
→ 改动前:零命中
```

值登记在 `entities.yaml` + GDD,但 C# 侧没有任何地方能读到它。

---

## 二、本轮落盘(四件)

| # | 件 | 文件 |
|---|---|---|
| 1 | **补三个常量** + 提两个裸字面量为具名常量 | `Sim/StateMachine.cs` |
| 2 | **`last_intervention` 聚合器**(三通道 max + 派生式 + 哨兵) | `Sim/LastInterventionAggregator.cs`(**新建**) |
| 3 | **F4 求值体**(死亡支四条件合取 + 昏迷支无门 + 低区间分级) | `Sim/F4OutcomeEvaluator.cs`(**新建**) |
| 4 | **R1-34 / R1-35**(4b + 14a + 14b) | `Sim/RegistrySchema.cs` |

### 2.1 补的常量(值 = 2026-10-22 用户裁定,非本轮自造)

```csharp
public const int TREATMENT_WINDOW = 3600;    // 3 分钟 @ 20 Hz
public const int CARE_GAP         = 600;     // 30 秒 @ 20 Hz
public const int RECOVER_THRESHOLD = 7864;   // ≈ 0.12
public const int SEVERE_THRESHOLD  = 2000;   // 原裸字面量,值未改
public const int MODERATE_THRESHOLD = 1000;  // 原裸字面量,值未改
```

⚠️ 后两个**不是新裁定** —— 它们本来是 `ComputeTransition` 里的裸字面量 `2000` / `1000`,
提为具名常量是因为 `F4OutcomeEvaluator` 也要用。两处各写一个字面量就是两份真源。
**值一字未改**。

### 2.2 `last_intervention` 的三个设计决定

| 决定 | 理由 |
|---|---|
| 收**已筛选**的处置 tick,不自己筛 | 「causal ∧ 异号 ∧ ∈ treatable_by」与 F1 的处置和式**是同一组判据**。再筛一遍 = 两份实现,改一处不改另一处即静默分叉 |
| 收**已归一化**的护理时刻,不碰事件流 | Sim 内无解码路径(承 b2 门),与 `ProgressionEvaluator` 的剂量重载同法 |
| 返回 `long` 而非 `long?` | GDD `:1131-1134`「哨兵必须显式。缺了它…死亡判定整条短路」。返回 `long` 使「空」在**类型上不可能**漏判 |

### 2.3 F4 求值体与 `ComputeTransition` 的分工

| | `ComputeTransition` | `F4OutcomeEvaluator` |
|---|---|---|
| 输入 | 5 个标量 | 实体类别 + 注册表条目 + 三通道 |
| 实体集 | 受伤实体 {敌人, 玩家}(世界流) | 病人侧病史流(F4) |
| 死亡判定 | **无条件** `position ≥ DEATH` ⇒ Deceased | **须过两门与窗口**才 Deceased |

⚠️ 二者**不是**「新旧版本」,是两个不同的判据面。`ComputeTransition` 保持原样,
其调用点(`InjuryStateChangedWriter`)不受影响。

### 2.4 短路次序是判据,不是风格

死亡支的四条件写成:`两门 → 阈值 → 窗口`。

**为什么两门必须在前**:`IsCombatant(Unspecified)` 要 fail-loud。若阈值先短路,
一个 `Unspecified` 实体在低 position 下会**静默返回非 Deceased**(与门契约相反)。
`test_unspecifiedActorClass_throwsEvenAtLowPosition` 就是钉这个的。

---

## 三、R1-34 / R1-35 的整数域判据

校验 4b / 14 的判据是 `a/scale ≥ thr/ONE`,即 `a × ONE ≥ thr × scale`。

**为什么不真做除法**:ADR-006 §三 的 `ROUND_HALF_AWAY_FROM_ZERO` 只约束「必须除」的地方。
这里两边同乘正数(`scale > 0` 已由 R1-23 保证)⇒ 序不变,不需要除法。

**为什么必须 128 位**:`a` / `scale` / `thr` 都是 raw,可达 2^48 量级;
`2^48 × 2^16 = 2^64` ⇒ **超 int64**。

**实现**:手工 hi/lo 两 `ulong` 带进位,四路拆分,交叉项全程无符号 + 掩码提取。
- ❌ 不用 `System.Int128`(Unity 6.3 netstandard2.1 / IL2CPP 无此 .NET 7 类型,ADR-005 Amendment G)
- ❌ 不用 `BigInteger`(ADR-005 Amendment G / AC-4)
- ✅ 无有符号右移(ADR-012 F7:IL2CPP 下 C# 有符号溢出为 UB)

`CompareProducts` 的溢出正确性由 `test_compareProducts_handlesInt64Overflow` 钉住:
`2^40 × 2^40` vs `2^41 × 2^39`(均 = 2^80,直接用 int64 乘会溢出)。

### 3.1 🔁 实现轮自查修正:R1-35 下界用了近似式(2026-10-22)

**原写法**:`if (!AtLeast(StateMachine.RECOVER_THRESHOLD + 1))`,即判据
`a_peak × ONE ≥ (RECOVER + 1) × scale`。

**这是错的**。GDD 校验 14 的原文是**严格大于** `RECOVER_THRESHOLD < A_peak/SCALE`,
精确式 = `a_peak × ONE > RECOVER × scale`。两者在 `scale ≠ ONE` 时**不等价** ——
`+1` 是「raw 轴上的 1 个 LSB」,不是「比值轴上的 1 个 LSB」,`scale` 越大偏差越大。

**可达的误拒(不是理论风险)**:

| a_peak | scale | 比值 | GDD 判定 | 旧写法判定 |
|---|---|---|---|---|
| 12 | 100 | 0.12 | **合法**(0.12 > 7864/65536 ≈ 0.119995) | **红**(要求 a_peak ≥ 13) |
| 15 | 100 | 0.15 | 合法 | 合法 |

⇒ 修正为 `StrictlyAbove(RECOVER)`,在整数域精确表达 `>`(仍是 128 位乘积比较,
仍不做除法)。**判据面不变,只把近似换成精确。**

**新增两条回归夹具**(`RegistryCurveFloorTest` 12 → 14):

| 测试 | 钉什么 |
|---|---|
| `test_r1_35_exempt_exactlyAtRecover_throws` | `A_peak/SCALE` **恰等于** RECOVER ⇒ 红(严格大于的下边界) |
| `test_r1_35_exempt_justAboveRecover_passes` | `0.12 > RECOVER` ⇒ **绿**;若下界被写回 `RECOVER+1` 近似式,本条转红 |

⚠️ 本修正**不在原 §二 的四件之内** —— 它是落盘后自查发现的判据偏差,
按「原文不删,就地加注」纪律登记于此。

---

## 四、判据不适用型(防门过宽)

| 型 | 约束 | 判据 |
|---|---|---|
| 急性保持型(`self_limit=false ∧ plateau=false`) | `A_peak/SCALE ≥ DEATH` | R1-34 |
| `plateau ∧ lethal` | `A_peak/SCALE ≥ DEATH` | R1-34 |
| 豁免类(`plateau ∧ ¬lethal`) | `RECOVER < A_peak/SCALE < CRITICAL` | R1-35 |
| **自限型**(`self_limit=true`) | **不受约束** | — |

自限型关死亡支(F4 要求 `self_limit = false`),故它**不需要**打到死线。
`test_r1_34_selfLimitType_notConstrained` 钉这一点 —— 把自限型也要求 ≥DEATH
会让疟疾 / 麻疹全部非法。

---

## 五、证伪轮 —— 五轮,全部证明「真的会红」

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。**未用** `git checkout` / `git stash`。

备份 sha256(⚠️ N5 轮之前 / 之后 `RegistrySchema.cs` 的值不同 —— 因为 N5 的修正
改动了该文件;下方两组分别对应各自时点):

```
# N1–N4 轮的备份(R1-35 修正前)
33eac0f1468742e0e04b88d61462361870bf07bf4848aeb7e568eb2818cad1b5  RegistrySchema.cs
dae9afeff43739ce96840327b0a5bfbf5029de986f9c884aaf51816e6fca52e1  F4OutcomeEvaluator.cs
0448a02dbc24e6c71c3a6478187b48cdc04c8c91c771c8beeb06eefc9ce7c397  LastInterventionAggregator.cs

# N5 轮的备份(R1-35 修正后 = 当前状态)
fac3a98230eef320a1489e2c6607ef6d3e400cb5d231c0eec91323e628c44c59  RegistrySchema.cs
dae9afeff43739ce96840327b0a5bfbf5029de986f9c884aaf51816e6fca52e1  F4OutcomeEvaluator.cs
0448a02dbc24e6c71c3a6478187b48cdc04c8c91c771c8beeb06eefc9ce7c397  LastInterventionAggregator.cs
```

| 变异 | 内容 | 结果 | 转红的测试 |
|---|---|---|---|
| **N1** | 死亡支的窗口条件 → `if (true)`(删掉窗口) | `4539 / 4500 / 3` | `test_death_exactlyAtWindowBoundary_isNotDeceased`<br>`test_death_recentTreatment_resetsWindow_notDeceased`<br>`test_death_activeCare_resetsWindow_notDeceased` |
| **N2** | 照护派生式 → `动作`(丢掉 `+ CARE_GAP`) | `4539 / 4501 / 2` | `test_lastIntervention_careDerived_isActionPlusCareGap`<br>`test_lastIntervention_careDerivedLaterThanStop_wins` |
| **N3** | R1-34 判据 → `if (false && ...)` | `4539 / 4501 / 2` | `test_r1_34_acuteHold_belowDeathThreshold_throws`<br>`test_r1_34_plateauLethal_belowDeathThreshold_throws` |
| **N4** | R1-35 下界 → `if (false && ...)` | `4539 / 4502 / 1` | `test_r1_35_exempt_belowOrAtRecover_throws` |
| **N5** | R1-35 下界 → 回退到 `AtLeast(RECOVER + 1)` 近似式(§3.1 的原写法) | `4541 / 4504 / 1` | `test_r1_35_exempt_justAboveRecover_passes` |

**每轮还原后 sha256 与备份逐字节相同**;变异残留扫描(`grep -c "N1 变异\|N2 变异\|N3 变异\|N4\|N5 变异"`)三文件均为 0。

### 判据解读

- **N1 只打掉 3 条**(不是全部 22 条)—— 正确。窗口是死亡支的**一个**条件,
  另外三条(两门 + 阈值)各有独立判据,不该被窗口的变异连带打红。
- **N2 只打掉 2 条** —— 正确。照护派生式只在「无止事件」与「止早于派生值」两种形态下才可观测。
- **N3 / N4 各打掉本规则的判据** —— 正确。R1-34 与 R1-35 是两个独立规则,变异一个不影响另一个。
- **N5 只打掉 1 条** —— 正确。近似式的偏差只在 `scale ≠ ONE` 的合法行上可观测;
  它**不会**让「恰等于 RECOVER」那条转绿(近似式在 `scale = ONE` 时与精确式同效),
  故只有 `justAboveRecover` 一条转红。这正是「修正有效」的判据形态。

---

## 六、回归(带三元组)

### 6.1 终值(§3.1 修正后)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 18:29 CST` |
| 批次 | 全量 EditMode(N5 证伪**已还原**后的终跑) |
| commit | `cb798e2`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-editmode-f4-fix.xml` |

```
total=4541  passed=4505  failed=0  inconclusive=0  skipped=36   EXIT=0
```

**差分核对**:`4541 − 4505 = 36`。

| 新增测试 | 条数 |
|---|---|
| `F4OutcomeEvaluatorTest` | 22 |
| `RegistryCurveFloorTest` | 14 |
| **合计** | **36** |

等式成立 ⇒ 零回归,且新增数不是「照抄」而是算出来的。

### 6.2 修正前的中间值(保留,不覆盖)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 18:21 CST` |
| 批次 | 全量 EditMode(四轮证伪 N1–N4 均还原后的终跑) |
| commit | `cb798e2` |
| 结果件 | `unity/Logs/test-editmode-final-f4.xml` |

```
total=4539  passed=4503  failed=0  inconclusive=0  skipped=36   EXIT=0
```

`4539 − 4505 = 34` = 22 + 12。**此值已被 §6.1 取代**,保留作 N5 修正的差分依据
(`4541 − 4539 = 2` = §3.1 新增的两条回归夹具)。

**基线来源**:`4505/4469/0/36` —— `impl-thresholds-and-8-disease-rows-2026-10-22.md` §九.5(该轮实测值)。

---

## 七、明确未实现(禁借绿)

| # | 未做 | 影响 |
|---|---|---|
| ① | **F4 痊愈支**(GDD `:1113-1120` 四条件合取) | 需要 `trend_backward_agg`(F5)+ `relapse` 块状态,超出「死亡/昏迷两支」的裁定面。求值落在 `[0, CRITICAL)` 时返回纯阈值分级 |
| ② | **F4 迁延支**(`Chronic`) | 需要趋势分段,同上 |
| ③ | **`position_agg` 的 argmax 选择**(GDD `:1111-1119`) | 归调用方 —— 并列取 `DiseaseId` 最小者,本类不承担 |
| ④ | **照护杠杆适用集**({伤寒/痢疾/心衰},破伤风不在内) | 归调用方 —— 本类对「传了就采信」负责,对「该不该传」不负责 |
| ⑤ | **`F4OutcomeEvaluator` 零生产调用点** | 9 的 `Step` 尚未接线。本轮交付的是**可求值的纯函数**,不是「已接进 tick 循环」 |
| ⑥ | **PlayMode 未跑** | 本轮未跑(未跑就写未跑) |
| ⑦ | **`EntityDeathGates` 的 `lethal` 代理是 `Lethality > 0`** | GDD R1 表写 `lethal: bool`,而注册表字段是 `Lethality ∈ [0,3]` 整数 —— 这是**既有的字段错位**(`RegistrySchema.cs:557-558` 已自陈),R1-34 与 `F4OutcomeEvaluator` 都沿用该代理。归**注册表字段订正轮**,不在本轮裁决面 |

⇒ **能说的**:「F4 的死亡支与昏迷支现在是可求值的纯函数,且每条判据都被证伪轮钉住」。
⇒ **不能说的**:「F4 已接入模拟循环」「九态全部实现」。

### 7.1 「值已裁 ≠ 可求值」三件的结账

| # | 缺口 | Landed | Proven | Proven-by |
|---|---|---|---|---|
| ① | F4 求值体 | ✅ `Sim/F4OutcomeEvaluator.cs` | ✅ | `F4OutcomeEvaluatorTest` 22 条;§五 N1 反证(删窗口条件 ⇒ 3 条转红) |
| ② | `last_intervention` 聚合器 | ✅ `Sim/LastInterventionAggregator.cs` | ✅ | 同文件 5 条;§五 N2 反证(丢 `+ CARE_GAP` ⇒ 2 条转红) |
| ③ | registry 4b/14 校验 | ✅ `Sim/RegistrySchema.cs` R1-34 / R1-35 | ✅ | `RegistryCurveFloorTest` 14 条;§五 N3 / N4 / N5 三轮反证 |

⚠️ 三行的 `Proven` 均指「判据被钉住」,**不含**「已接进 tick 循环」(见上表 ⑤)。

---

## 八、复算命令

```bash
cd unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/test-editmode-f4-fix.xml" \
  -logFile "$PWD/Logs/test-editmode-f4-fix.log"
# 预期:total=4541 passed=4505 failed=0 skipped=36
python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('Logs/test-editmode-f4-fix.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"
```

五轮证伪的复算(各自备份 → 变异 → 跑 → `cp` 回 → sha256 比对,详见 §五):

```bash
# N1  F4OutcomeEvaluator.cs:死亡支窗口条件 → if (true)
#     预期 total=4539 passed=4500 failed=3   结果件 Logs/test-editmode-n1.xml
# N2  LastInterventionAggregator.cs:动作 + CARE_GAP → 动作
#     预期 total=4539 passed=4501 failed=2   结果件 Logs/test-editmode-n2.xml
# N3  RegistrySchema.cs:R1-34 判据 → if (false && ...)
#     预期 total=4539 passed=4501 failed=2   结果件 Logs/test-editmode-n3.xml
# N4  RegistrySchema.cs:R1-35 下界 → if (false && ...)
#     预期 total=4539 passed=4502 failed=1   结果件 Logs/test-editmode-n4.xml
# N5  RegistrySchema.cs:R1-35 下界 → 回退 AtLeast(RECOVER + 1) 近似式
#     预期 total=4541 passed=4504 failed=1   结果件 Logs/test-editmode-n5.xml
#         转红者 = test_r1_35_exempt_justAboveRecover_passes
```
