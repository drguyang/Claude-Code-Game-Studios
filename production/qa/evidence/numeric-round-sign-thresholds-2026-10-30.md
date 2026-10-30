# 数值轮 —— 48 条体征阈值与曲线定稿

**日期**:2026-10-30 · **批次**:`ADR-035` T2 收口后的数值轮
**commit**:`2d769591` + 工作树(未提交)
**性质**:数值裁定 + 门加强。**本件全部数字均为本轮实跑**。

---

## 一、病灶:不是数值偏好,是量纲错误

`signs[].appear_at` 是**绝对** `position` 阈值,而 `position` 的**上界逐行不同**

```
position(t) = clamp(Progress(t) / SCALE_病种, 0, 1)      峰值 = A_peak / SCALE
```

上一版口径是「同一 `sign_key` 跨病种同阈」。因 `A_peak/SCALE` 从 **0.15**(`DIS_NEURASTHENIA`)
到 **1.00**(`INJ_STAB` / `INJ_HEMORRHAGE` / `DIS_DYSENTERY` / `DIS_TETANUS`)不等,
**9 条体征的阈值高于本行峰值 ⇒ 波形永远够不着**:

| 行 | 峰值 position | 死词条(阈值) |
|---|---|---|
| `DIS_MALARIA` | 0.45 | `sign_jaundice`(0.60) |
| `DIS_MEASLES` | 0.35 | `sign_koplik`(0.50) |
| **`DIS_NEURASTHENIA`** | **0.15** | **全部 4 条**(0.20 ⇒ 该行掩码**恒 0**) |
| `INJ_DRUG_HARM` | 0.30 | `sign_delirium`(0.60) |

`DIS_NEURASTHENIA` 是「豁免类(plateau ∧ lethal=false,永不痊愈)」的招牌病人 ——
掩码恒 0 意味着 **8 的脉案在这个病人身上完全失明**。这是「配了但没判别力」的第六次形态。

---

## 二、用户裁定(2026-10-30,三项均按推荐)

1. **阈值口径 = 按行相位派生**(而非全局统一调低);
2. **`INJ_BLUNT` / `INJ_DRUG_HARM` 两行草案曲线按现值定稿**;
3. **加死词条门** —— R1-37 由 `appear_at ∈ (0,1]` 加强为**[域 ∧ 可达性]** 两段判据。

---

## 三、新口径

```
appear_at[row, sign] = 相位分数(sign) × 峰值 position(row)
```

相位分数四档(**语义 = 波形上接近峰值的程度,不是时间进度**):

| 相位 | 分数 | 体征 |
|---|---|---|
| 即刻 | **0.05** | `wound_stab` / `wound_laceration` / `wound_hemorrhage` / `wound_claw` / `wound_bite` / `moan` · 阴性锚点 `lung_clear` / `neck_supple` / `abd_soft` / `no_organomegaly` |
| 早期 | **0.25** | `flush` / `pallor` / `fatigue` / `palpitation` / `insomnia` / `low_mood` / **`koplik`** |
| 中期 | **0.55** | `rash` / `diaphoresis` / `joint_swelling` / `curled` / `voice_weak` / `tachypnea` / `rales` / `purulent_stool` / `abd_tenderness` / `cold_clammy` / `pulse_rapid` / `hepatosplenomegaly` / `relative_bradycardia` / `orthopnea` / `dyspnea_speech` |
| 晚期特异 | **0.85** | `jaundice` / `delirium` / `trismus` / `neck_stiffness` / `opisthotonus` / `risus` |

⚠️ **一处医学序订正**:`sign_koplik` 是麻疹**出疹前**唯一特异所见(`disease-simulation.md:466`)
⇒ 归**早期 0.25**,**必早于** `sign_rash`(中期 0.55)。上一版两者都无此约束。

### 48 条定稿值

| 行 | 峰值 | 体征 → `appear_at` |
|---|---|---|
| `INJ_STAB` | 1.00 | `wound_stab` 1/20 · `moan` 1/20 |
| `INJ_LACERATION` | 0.60 | `wound_laceration` 3/100 · `moan` 3/100 |
| `INJ_HEMORRHAGE` | 1.00 | `wound_hemorrhage` 1/20 · `pallor` 1/4 · `voice_weak` 11/20 · `cold_clammy` 11/20 · `pulse_rapid` 11/20 |
| `INJ_CLAW` | 0.85 | `wound_claw` 21/500 · `moan` 21/500 |
| `INJ_BITE` | 0.75 | `wound_bite` 19/500 · `moan` 19/500 |
| `DIS_MALARIA` | 0.45 | `no_organomegaly` 11/500 · `diaphoresis` 31/125 · `hepatosplenomegaly` 31/125 · `jaundice` 191/500 |
| `DIS_RHEUMATIC_FEVER` | 0.40 | `flush` 1/10 · `joint_swelling` 11/50 |
| `DIS_HEART_FAILURE` | 0.67 | `lung_clear` 33/1000 · `pallor` 167/1000 · `tachypnea` 367/1000 · `rales` 367/1000 · `orthopnea` 367/1000 · `dyspnea_speech` 367/1000 |
| `DIS_TYPHOID` | 0.67 | `flush` 167/1000 · `relative_bradycardia` 367/1000 · `delirium` 567/1000 |
| `DIS_MEASLES` | 0.35 | `flush` 11/125 · `koplik` 11/125 · `rash` 24/125 |
| `DIS_DYSENTERY` | 1.00 | `abd_soft` 1/20 · `curled` 11/20 · `abd_tenderness` 11/20 · `purulent_stool` 11/20 |
| `DIS_TETANUS` | 1.00 | `neck_supple` 1/20 · `trismus` 17/20 · `opisthotonus` 17/20 · `neck_stiffness` 17/20 · `risus` 17/20 |
| `DIS_NEURASTHENIA` | 0.15 | `fatigue` 19/500 · `insomnia` 19/500 · `palpitation` 19/500 · `low_mood` 19/500 |
| `INJ_BLUNT` | 0.20 | `moan` 1/100 |
| `INJ_DRUG_HARM` | 0.30 | `pallor` 3/40 · `fatigue` 3/40 · `delirium` 51/200 |

**零死词条**;最紧余量 = `INJ_DRUG_HARM` 的 `delirium`(`0.255` vs 峰值 `0.30`,余量 `0.045`)。

---

## 四、曲线定稿(两条草案行)

`INJ_BLUNT` / `INJ_DRUG_HARM` 的 `_rowNote` 原首句「⚠️ **草案值,待用户数值轮调**」已由用户裁定解除,
改为「✅ 2026-10-30 数值轮定稿(原文首句…保留原文作历史)」。**曲线数值一分未改**:

| 行 | `a_peak` | `tau_rise` | `tau_peak` | `tau_fall` | 形态 |
|---|---|---|---|---|---|
| `INJ_BLUNT` | 0.20(< `CRITICAL` 0.30) | 2 s | 6 s | 30 s | 自限型 / 非致死 |
| `INJ_DRUG_HARM` | 0.30(= `CRITICAL`,< `DEATH` 0.58) | 2 s | 9 s | 45 s | 自限型 / 非致死 |

两者均满足 **R1-22**(禁急性保持型非致死 ⇒ 只能取自限型)与 **R1-23 / R1-24 / R1-25**。

⚠️ 其余 13 行曲线是用户 **2026-10-22** 已裁定的(`_note` 自陈「**不是占位值**」),本轮**未动**。

---

## 五、门:R1-37 两段判据

`unity/Assets/Sim/RegistrySchema.cs` 的 `RegistrySchemaValidator`:

```
① 绝对域   appear_at_raw ∈ (0, ONE]                       (原判据,保留)
② 可达性   appear_at_raw × SCALE_raw < A_peak_raw × ONE   (本轮新增)
```

② 只在 `Curve != null && Scale.Raw > 0` 时求值 —— 既有条目的 `Curve` 可缺席,不得因此造红。

⚠️ **起草期自查抓到一个量纲错误**:初稿写 `appear_at × ONE < A_peak × SCALE`,
两边各差一个 `ONE²/SCALE²` 因子 ⇒ 在 `appear_at = 0.75 / 峰值 0.50`(应拒)**恒真** ⇒
门会**假放行**。正确式是 `appear_at × SCALE < A_peak × ONE`(同乘正数 `SCALE_raw`)。
已就地订正并在注释里写明「不是另一个式子」。

新增夹具 `Tests/EditMode/DiseaseSimulation/registry_sign_threshold_gate_test.cs`(6 条)。

---

## 六、证伪轮(两属性各一条变异)

| 变异 | 改什么 | 预期转红 | 实测 | 还原 |
|---|---|---|---|---|
| **M1** | `if (false && entry.Curve != null && …)` —— 停用**可达性**判据 | `test_appearAtAbovePeak_rejected` · `test_appearAtExactlyAtPeak_rejected` | **2 红 / 4 绿**,恰为预期 | SHA-256 `73f2d765…6fcd` 逐字节 |
| **M2** | `if (false && (… ≤ 0 \|\| … > ONE))` —— 停用**绝对域**判据 | `test_appearAtAboveOne_rejected` · `test_appearAtZero_rejected` · `test_curveAbsent_skipsReachabilityButKeepsDomain` | **3 红 / 3 绿**,恰为预期 | 同上 SHA-256 |

**M1 暴露并修掉一处夹具设计缺陷**:首版 `test_appearAtAboveOne_rejected` 用带曲线的条目,
而 `appear_at = 1.50` 既违反域上界、又高于峰值 `0.50` ⇒ 停用域判据后**仍被可达性判据以同一个
`R1-37` 拒掉** ⇒ 该夹具对域判据**零判别力**(M1 下仍绿,伪装成「域判据也在守」)。
修法:域判据组改用 `Curve = null`(`DomainOnly(...)`)⇒ 可达性判据不参与,只钉绝对域。
**这是「两条判据共用一个错误码 ⇒ 必须用输入把它们分开」的直接教训。**

---

## 七、回归

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| **EditMode 全量** | `6069 / 6032 / 0 / 37` | 2026-10-30 **10:35:50 CST** | 数值轮 | `2d769591` + 工作树 |
| **PlayMode 全量** | `238 / 234 / 0 / 2 / 2` | 2026-10-30 **10:36:37 CST** | 数值轮 | `2d769591` + 工作树 |

**等式**:`6069 − 6063 = 6` = 新增的 6 条 `RegistrySignThresholdGateTest` 夹具。
**零回归**(PlayMode 与基线逐位一致)。

**连带重烘**(注册表 `ConfigVersion` 变化会让以它为源的产物漂移):

| 产物 | 大小 | ConfigVersion | 前值 |
|---|---|---|---|
| `disease_registry.cooked.bytes` | 3910 B / 15 行 | **`0x1D4C3B22`** | `0x97AD4C59` |
| `53_cost_table.cooked.bytes` | 72 B / 3 行 | **`0x5B6DCF16`** | `0x4CEFF03D` |
| Addressables catalog | **37 条** | — | 37 条 |

⚠️ 字节数不变是**预期**的:`appear_at` 从 `"1/20"` 改为 `"1/100"` 不改变载荷宽度(都是 8 字节 `Fix` raw)。

---

## 八、可复算命令

```bash
# ① 零死词条:每条 appear_at 都严格低于本行峰值 position
python3 -I - <<'PY'
import json
from fractions import Fraction as F
d=json.load(open('assets/data/disease_registry.json',encoding='utf-8'))
def v(s):
    s=str(s)
    return F(*map(int,s.split('/'))) if '/' in s else F(s)
bad=[(x['key'],s['sign_key'],str(v(s['appear_at'])),float(v(x['natural_progress']['a_peak'])/v(x['scale'])))
     for x in d['diseases'] for s in (x.get('signs') or [])
     if not v(s['appear_at']) < v(x['natural_progress']['a_peak'])/v(x['scale'])]
print('死词条:',len(bad),bad)          # 期望 0 []
print('体征总数:',sum(len(x.get('signs') or []) for x in d['diseases']))   # 期望 48
PY

# ② 回归(三元组见 §七)
python3 -I -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/full-editmode-1030q.xml','unity/Logs/full-playmode-1030q.xml']:
    r=ET.parse(f).getroot()
    print(f, r.get('total'), r.get('passed'), r.get('failed'), r.get('inconclusive'), r.get('skipped'))
"

# ③ 门真的在拒(定向跑 6 条)
#    Unity -batchmode -runTests -testPlatform EditMode \
#      -testFilter DaYiJingCheng.Tests.Unit.Sim.RegistrySignThresholdGateTest

# ④ 数据管线门
python3 tools/debtcount/debtcount.py --check
python3 tools/kindgen/kindgen.py --check
```

---

## 九、⚠️ 残留(如实登记)

1. **本轮只定「体征何时出现」,不定「病人多久好」** —— 曲线时长(τ)未动,仍是 2026-10-22 裁定的值。
2. **`INJ_DRUG_HARM` 仍在 R2 枚举表之外** —— 须并入 `disease-simulation.md` 的 R2 表(11 → 12),
   归 9 的 GDD 轮(本轮不动 GDD 结构)。
3. **`DIS_NEURASTHENIA` 的四条体征全为 `Subjective` 族** —— 掩码现在非 0,但**只覆盖一个通道**,
   8 的脉案在该病人上仍读不到体格所见。这是由 `a_peak 0.15`(豁免类须 < `CRITICAL` 0.30)与
   体征集共同决定的**设计事实**,不是缺陷;若将来要它可被体格检查,须改 `a_peak` 或体征集 —— 留待后续裁定。
4. **相位分数 0.05 / 0.25 / 0.55 / 0.85 是**本轮取的值**,不是从既有裁定推导出来的**;
   任何一档若要调整,须同批重烘注册表 + 代价表 + catalog 并重跑本件 §八。
