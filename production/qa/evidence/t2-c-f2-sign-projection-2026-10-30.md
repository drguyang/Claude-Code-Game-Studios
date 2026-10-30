# `TD-sim-04` 关账 —— F2 体征投影接线(9 侧)

**日期**:2026-10-30 · **批次**:`ADR-035` T2 收口后段
**commit**:`88b01cab` + 工作树(未提交)
**性质**:实现轮。**本件全部数字均为本轮实跑**。

---

## 一、债的原文与它的一处前提错误

`TD-sim-04` 原文:「9 的 F2 体征投影未接线 ⇒ `VitalsDto.SignChannelMask` 恒 0 ⇒ 11 的禁忌判据恒不命中」,
消解判据 = 「8 的词表 **+ 阈值**落地 ⇒ `ComputeVitals` 产出非零掩码,**且**有一条端到端夹具用**真实**
`DiseaseVitalsService` 跑出非零档」。

🔁 **「阈值归 8」这半句经实测不成立**:8 的 `assets/data/diagnosis_signs.json` 字段 =
`sign_id` / `display_词`(三档精度用词)/ `channel` / `reveal_by` / `tier_named` / `polarity` / `neg_weight`
—— **没有任何「体征从多重的病开始出现」的阈值**;8 的 GDD §Visual/Audio 定的是**读数精度层义**(V-8.1),
不是 `Project` 的阈值。⇒ 阈值改由 **9 的注册表**承载(见下),并在 `disease-simulation.md` 的 `D-9-R` 行
就地补注订正(原文不删)。

---

## 二、改动清单

| 件 | 改动 |
|---|---|
| `Sim/RegistrySchema.cs` | `SignEntry` 加 `Fix AppearAt`;新增校验 **R1-37**:`appear_at ∈ (0,1]` |
| `assets/data/disease_registry.json` | **48 条** `signs[]` 全部带 `appear_at`(Fix 字符串,如 `"1/20"` / `"3/5"`);按 `sign_key` 统一(同一体征跨病种同阈) |
| `Editor.Tools.Bake/DiseaseRegistryBinder.cs` | 键白名单加 `appear_at`;经 `ReadFix`(FixParse,禁 JSON 数字)解析 |
| `Editor.Tools.Bake/DiseaseRegistryCookedWriter.cs` | 每条体征追加 8 字节 `Fix raw`;**null 词条也写 8 字节占位**(定长步进) |
| `Gameplay.Presentation/DiseaseRegistryCookedCodec.cs` | 对称读取 i64 |
| `Gameplay.Boot/DiseaseVitalsService.cs` | **接线点**:`Signⱼ 出现 ⇔ position ≥ appear_atⱼ`;掩码 = 出现词条 `channel_mask` 按位或;计数 = 出现词条数 |
| `design/gdd/disease-simulation.md` | `D-9-R` 行补 2026-10-30 订正注(阈值归属 + tripwire 换守护对象) |

**投影语法**(ADR-035 无关,属 9 的 F2):

```
position(t) = clamp(Progress(t) / SCALE_病种, 0, 1)
Signⱼ 出现  ⇔  position(t) ≥ appear_atⱼ
mask = ⋁(appear_atⱼ ≤ position 的 channel_maskⱼ)   count = 满足条件的词条数
```

全整数域比较(`Fix` raw),**不读回** `VitalsDto` 的 float 字段(`disease-simulation.md:772`)。

**tripwire 保留但换了守护对象**:原守「9 没有阈值,谁产 signs 谁越界」;现守「求值器自产的
`result.Signs` 与注册表投影是两条平行来源,并存时不得静默择一」。

---

## 三、夹具(4 条,`Tests/EditMode/DiseaseSimulation/f2_sign_projection_test.cs`)

| 测试 | 钉什么 |
|---|---|
| `test_thresholdsGateSigns_perSign` | `INJ_DRUG_HARM` 三条体征:1/5 的两条出现(掩码 1\|64 = 65、计数 2),**3/5 那条结构性不可达**(该行 `a_peak 30 / scale 100` ⇒ position 上界 0.30) |
| `test_maskIsMonotoneInPosition_notInTick` | 掩码对 **position** 单调;position 未升时不得出现新通道 |
| `test_singleSignRow_projectsOnlyThatChannel` | `INJ_BLUNT` 只有 `sign_moan` ⇒ 掩码恰为通道 2 |
| **`test_realVitals_produceANonSentinelBand`** | **本债的关账判据**:真 `CompositionRoot.Assemble` + 真注册表行 + 真 `DiseaseVitalsService` ⇒ 掩码 65 ⇒ 11 的 `ContraindicationEvaluator` 返回**档 0**(非哨兵) |

⚠️ **首版两条是红的,如实记录**:① 驱动到 t=600 时 `position` 已从峰值 0.30 回落到 ~0.19 ⇒
「越过低阈值 0.20」的前提不成立;② 按 **tick** 断言「掩码只增不减」—— 而过了 τ_peak 掩码**正确地**回缩。
两处都是**判据写错**,不是实现错;改为「驱动到 τ_peak」+「对 position 单调」后转绿。

---

## 四、实跑

| 套件 | 结果 | 截至时刻 | commit |
|---|---|---|---|
| **EditMode 全量** | `6063 / 6026 / 0 / 37` | 2026-10-30 **10:05 CST** | `88b01cab` + 工作树 |
| **PlayMode 全量** | `238 / 234 / 0 / 2 / 2` | 2026-10-30 **10:01 CST** | 同上 |

对照:本轮开工前 EditMode `6059 / 6022 / 0 / 37`。**等式** `6063 − 6059 = 4` = 新增的 4 条 F2 夹具。

**连带重烘**(注册表 `ConfigVersion` 变化会让以它为源的产物漂移):
`disease_registry.cooked.bytes` = **3910 B / 15 行 / `0x97AD4C59`**(原 3526 B,`+384 = 48 × 8`);
`53_cost_table.cooked.bytes` = 72 B / 3 行 / **`0x4CEFF03D`**(其 `ConfigVersion` 由**两个源**派生);
Addressables 内容重建(37 条)。

---

## 五、⚠️ 关账口径(必须连读)

`TD-sim-04` **closed** 的含义 = 「**判据链能跑通且被真装配夹具钉住**」,**不**等于「药害在实际游玩中
可被撞见」。后者取决于:

1. **48 个 `appear_at` 值与各处曲线数值全是草案** —— 归用户数值轮。若实际病程走不到某体征的阈值,
   该体征在生产上仍是零出现;
2. 三档禁忌阶梯能否逐档命中,取决于病种体征**集**与阈值的组合,同样归数值轮。

> ✅ **2026-10-30 同日数值轮结清上述两条(原文保留)** —— 用户三项裁定:按行相位派生 · 两行草案曲线
> (`INJ_BLUNT` / `INJ_DRUG_HARM`)按现值定稿 · 加死词条门。
> **实测第 1 条不是"可能"而是"已经"**:上一版按 `sign_key` 统一阈值的口径下,**9 条体征结构性不可达**
> (`DIS_MALARIA` 的 `jaundice` · `DIS_MEASLES` 的 `koplik` · **`DIS_NEURASTHENIA` 全 4 条** ·
> `INJ_DRUG_HARM` 的 `delirium`)。其中 `DIS_NEURASTHENIA` 是「豁免类(永不痊愈)」的招牌病人,
> 掩码恒 0 ⇒ 8 的脉案对该病人**完全失明**。
> ⇒ 本节 §二 表格里的「按 `sign_key` 统一(同一体征跨病种同阈)」**已被取代**:现为
> `appear_at[row, sign] = 相位分数 × 峰值 position(row)`;R1-37 加强为两段判据(域 + 可达性)。
> 48 条零死词条,最紧余量 `INJ_DRUG_HARM` 的 `delirium` = `51/200` vs 峰值 0.30。
> 证据原件:`production/qa/evidence/numeric-round-sign-thresholds-2026-10-30.md`。

⇒ 任何下游引用「F2 已接线」时须带这半句。

---

## 六、可复算命令

```bash
# 回归
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ['unity/Logs/full-editmode-1030i.xml','unity/Logs/full-playmode-1030c.xml']:
    r=ET.parse(f).getroot()
    print(f, r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))
PY

# 48 条体征全部带 appear_at
python3 -I -c "import json;d=json.load(open('assets/data/disease_registry.json',encoding='utf-8'));print(sum(1 for x in d['diseases'] for s in (x.get('signs') or []) if 'appear_at' not in s),'条缺阈值')"

# 硬 0 已清除
grep -rn "signChannelMask: 0" unity/Assets/Gameplay.Boot   # 期望零命中

# 门
python3 tools/debtcount/debtcount.py --check
python3 tools/kindgen/kindgen.py --check
```
