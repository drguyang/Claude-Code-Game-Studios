# 勘察 + 夹具件 —— `DIS_MALARIA` 的昏迷余量(0.2 ULP)

**日期**:2026-11-05 · **轮次**:sprint-24 · **commit**(回归时点):`c4d41a49` + 工作树
**触发**:热性惊厥 2×2 案子的对照半边依赖「疟疾会昏过去 / 惊厥不会」,而此前**无任何夹具**覆盖它
**权威**:GDD `disease-simulation.md` F4(昏迷分支)· R3.3(疟疾行)· ADR-005(`Fix` 域纪律)

> **一句话**:疟疾峰值位置**恰好等于** `COMA_THRESHOLD`(29491),比较用 `>=` 才成立 ⇒
> 昏迷支是靠「整数恰好落在线上」成立的,**不是靠余量**。已补 4 条夹具把它钉住。

---

## 一、数值(逐位)

| 量 | 值 |
|---|---|
| `a_peak` / `scale` | 45 / 100 = **0.45000000** |
| `COMA_THRESHOLD` | **29491** raw = 29491/65536 = **0.44999695** |
| 精确差 | **3.05×10⁻⁶** = **0.2 ULP**(1 ULP = 1/65536 = 1.52588×10⁻⁵) |
| 运行时 `Fix(45) / Fix(100)` | `scaled = 193273528320`,`ub = 6553600` ⇒ 商 **29491**,余 **1310720**(= 0.2) |
| 舍入 | `ROUND_HALF_AWAY_FROM_ZERO`;余数 0.2 ULP ⇒ **舍到 29491** |
| 比较 | `positionAgg.Raw >= COMA_THRESHOLD` ⇒ `29491 >= 29491` = **true** |

**三条判据的出处**(逐条可跑):
① 阈值与比较:`unity/Assets/Sim/StateMachine.cs:127`(`COMA_THRESHOLD = 29491`)· `:246`(`>=`)·
`unity/Assets/Sim/F4OutcomeEvaluator.cs:189`(`>=`)。
② 除法与舍入:`unity/Assets/Sim.Contracts/Fix.cs:139-163`(`operator /`,余数×2 ≥ 分母才进位)。
③ 位置 = `progress / Scale`:`unity/Assets/Sim/ProgressionEvaluator.cs:399`
(与 `GetVitals` / `EvaluateOutcome` 同式)。⚠️ `Evaluate` 返回的是 **progress 不是 position** ——
夹具必须自己做那一步除法,漏了会拿 2949120 去比 29491。

---

## 二、我原先说错的一处(如实登记)

我在口头汇报里说过「余量是 −3×10⁻⁶」并暗示可能**够不到**。**这个说法是错的**:
−3×10⁻⁶ 只是**精确有理数**层面的差,方向是「疟疾**高于**阈值」;而运行时的 Q16.16 除法
把 0.45 舍成 **29491**,**恰等于**阈值。⇒ **昏迷支真的可达**,不是擦边未达。

真正脆弱的地方不是「符号」,是「**零 ULP 净空**」:舍入后的值正好压在线上。

---

## 三、昏迷窗口只有 1–2 tick(本轮新发现)

疟疾是 `self_limit=true`,峰值是**尖的**不是平台:

| τ | 位置 raw | 在昏迷带 |
|---|---|---|
| `tau_peak − 1` | ≥ 29491(归一化上升段构造上 ≈1.0) | ✅ |
| **`tau_peak`(12000)** | **29491** | ✅ |
| `tau_peak + 1` | ≈ 29486(`exp(-1/3000)`) | ❌ |

⇒ 20 Hz 下只有 **50–100 ms** 的可判窗口。叠加 `OQ-8` 的「在场才模拟」
(`PATIENT_APPEARANCE_CAP = 24`,离屏不进 `Step`)⇒ **玩家不在场时这个窗口直接错过**。

---

## 四、脆弱面(任何一条都会让昏迷支静默消失)

| 改动 | 结果位置 | 后果 |
|---|---|---|
| `COMA_THRESHOLD` 29491 → **29492** | 29491 | **跌破** |
| `a_peak` 45 → **44** | 28835 | 跌破 |
| `scale` 100 → **101** | 29197 | 跌破 |

本轮补夹具前,**没有任何测试覆盖这三条中的任何一条**。

---

## 五、新增夹具(4 条)

`unity/Assets/Tests/EditMode/DiseaseSimulation/malaria_coma_margin_test.cs`

| 测试 | 钉什么 |
|---|---|
| `test_comaThreshold_isExactly29491` | 阈值常量 == **字面量 29491**(不与下面那条共用常量 ⇒ 防自指判据) |
| `test_malariaPeakOverScale_isFortyFiveOverAHundred` | `a_peak == 45<<16` · `scale == 100<<16` · 精确有理数 **严格高于**阈值 |
| `test_positionAtPeak_roundsToExactlyTheThreshold` | 运行时算出的峰值位置 == **字面量 29491** |
| `test_comaWindowIsNarrow_closesOneTickAfterPeak` | 峰值前一 tick 在线上 · 峰值后一 tick 跌破 · `plateau=false` ∧ `self_limit=true` |

⚠️ **读真表**(`disease_registry.json` 走真 `DiseaseRegistryBinder`)—— 要钉的正是「真数据算出来是多少」。
⚠️ **不使用被测常量做断言值**(`StateMachine.COMA_THRESHOLD`)**压在断言里** —— 那会让「常量与位置一起变」
时两边同动、恒真(自指判据)。常量由第 1 条单独钉。

---

## 六、证伪轮(同命令内 改→跑→还原 · SHA-256)

| # | 变异 | 实际红点 | 判定 |
|---|---|---|---|
| M1 | `COMA_THRESHOLD` 29491 → 29492 | `test_comaThreshold_isExactly29491` | ✅(**只红 1 条是对的** —— 见下) |
| M2 | `a_peak` 45 → 44 | 3 条(含峰值位置与精确比较) | ✅ |

**还原判据**:两份文件 SHA-256 与变异前逐字节相同。

### ⚠️ M1 的期望集我写错了(如实登记)

我原本期望 M1 同时红 `test_positionAtPeak_roundsToExactlyTheThreshold`。**没红,而且不该红**:
那条比的是**字面量 29491**,刻意不与常量共变(否则就是自指判据)。
改常量由**哨兵测试**抓,它红了。
⇒ 这不是「靶测漏网」,是**我的期望集把两条职责不同的测试混成一个靶**。
两条合起来才构成完整判据:哨兵守常量、位置守卫数据与舍入。

---

## 六之二、回归(三元组)

| 项 | 值 |
|---|---|
| **本轮 EditMode** | **`6919 / 6882 / 0 / 37`** |
| **基线 EditMode** | `6915 / 6878 / 0 / 37` @ `2026-11-05` · `-03` 轮 · `ea8b946d` |
| **增量等式** | `6919 − 6915 = 4` = 新增夹具数 · `6882 − 6878 = 4` · **failed 0** |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/ed-mal-full.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |
| **PlayMode** | **未重跑**,理由不是「省事」:`git diff --stat -- '*.cs' ':!*Tests*'` **空** ⇒ 生产代码逐字节未变,上一次 PlayMode(`341/337/0/2/2` @ `-03` 轮)**对本轮同样成立** |

---

## 七、本件**没有**证明的事

- **不证明玩家真能看到昏迷** —— 那要求病人在场且 tick 恰落在那 1–2 tick 窗口内。
- **不证明 2×2 案子可玩** —— 仍缺 8 的体征可读性、11 的处方、37 的立案(`TD-randomevents-03` 的端到端也已声明此点)。
- **不改任何数值** —— `a_peak = 45` 是既有裁定值,本件只钉不改。
