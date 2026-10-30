# ADR-035 T2 全链落盘 —— 真实代价行 + 53 结算器 + 11 禁忌判定

**日期**:2026-10-30 · **批次**:`ADR-035` T2(用户裁定「数据 + 53 结算 + 11 的禁忌判定」)
**commit**:`dbdcf956` + 工作树(未提交)
**性质**:实现轮。**本件全部数字均为本轮实跑**。

---

## 一、落盘清单

### ① 代价表的真实行(数据)

`assets/data/53_cost_table.json`:

```jsonc
bands: { "contraindication": 3 }   max_severity_band: 4
档 0 → INJ_BLUNT      severity_band 2   轻
档 1 → INJ_DRUG_HARM  severity_band 3   中
档 2 → INJ_DRUG_HARM  severity_band 4   重
```

产物 `53_cost_table.cooked.bytes` = **72 B / 3 行 / ConfigVersion `0x5D9922A2`**。

### ② `INJ_DRUG_HARM`(药毒)进注册表(数据)

`id 16 · kind: injury · severity 3 · lethal: false · self_limit: true · trauma_half_life 60`
曲线 `a_peak "30" / tau_rise "40" / tau_peak 180 / tau_fall "900"`(草案值);
体征 `sign_pallor`(面色 1)/ `sign_delirium`(语声 2)/ `sign_fatigue`(主观 64)—— 均为**既有**体征键。

产物 `disease_registry.cooked.bytes` = **3526 B / 15 行 / ConfigVersion `0xD7497556`**。

⚠️ **它是 R2 枚举表(`disease-simulation.md:362-373`,自称「P0 · 11 项」)之外的第十二项** ——
数据行本轮已落,但**词表收口**须把该表改成 12 项,归 9 的 GDD 轮。本件如实登记,不代改。
⚠️ `TOX_ENVENOMATION` 用不了:它是 `kind: poison`(`disease-simulation.md:374`),而 N-1 要求外键必须是伤情。

### ③ `bands` × `rows` 交叉校验(门)

`CostTableBinder.ReadBands` 由「只校验类型」改为**返回档数表**,行层用 `CheckBandInRange`
拒绝 `band ≥ bands[kind]`。原形态下 `bands` 是纯装饰字段 —— 声明 3 档而写 `band: 7` 也能过,
而该行在运行期 `TryLookup` 永远 miss(**行装了但永不生效**,与 `§Risks R4` 同型)。
`bands` 写 `null` = 档数未定 ⇒ 不校上界(配反例夹具防误伤)。

### ④ 53 的结算器(代码)

`unity/Assets/Sim/Consequences/ContraindicationSettler.cs`(新):

- 游标式推进(与 9 的 `DiseaseVitalsService.ApplyNewEvents` 同形);
- 读 `DrugTreatmentApplied.ContraindicationBand`,哨兵 `-1` ⇒ 不写;查 `CostTable53` ⇒ miss 静默;
- 命中 ⇒ `IPayloadEncoder.Encode(EventKind.TreatmentHarmOnset, …)` + `IEventSink.Append`;
- **幂等**:去重键 `(tick, patient, injuryId, harmKind)` 在构造期从**已有流**扫描得到
  ⇒ 读档重放不重复发(承 §④ 有界性「每完成一次用药至多 1 条」);
- 零伤害属性副本(外键 + 档位引用),`AC-53-01` 不破。

**装配**:`CompositionRoot.Assemble` 末尾构造(表源 = `dataProvider.Load<CostTable53DataSet>()`,
无 provider ⇒ 空表),`BootRoot.StepVitals` 在体征 Step 后逐 tick 驱动。
**写者存在性门同批撤表**(`WriterExistenceGate`),判据①强制 —— `test_b7_waiverTable_noOverlapWithWrittenKinds` 绿即证明撤对了。

### ⑤ 11 的禁忌判定(代码 + 数据)

| 件 | 内容 |
|---|---|
| `Sim.Contracts/ContraindicationRule.cs`(新) | `ContraindicationRule{ChannelMask, Band}` + `ContraindicationEvaluator.Evaluate`(纯函数) |
| `prescription_actions.json` | 根级 `contraindication_band_count: 3`;`salicylic_acid` 加 `contraindication.rules[]` = `{(1,0),(3,1),(73,2)}` |
| `PrescriptionActionsBinder` | 解析 + 校验(掩码非零且 ⊂ 八通道闭集;`band ∈ [0, count)`;空 `rules` 视为作者意图不明 ⇒ 拒) |
| `PrescriptionActionsCookedWriter` / `…CookedCodec` | **尾段**布局(不动逐行布局);旧产物会读越界 ⇒ **抛**(fail-loud),不静默给「零规则」 |
| `PrescribeFlow` | `ResolveContraindicationBand` —— 判据的**唯一求值点**;填载荷第八项 |
| `PrescribePorts` | 新增 `IVitalsQuery Vitals`(可空;`null` = 无体征 ⇒ 未命中) |
| `VitalsQueryOrNone`(新,Gameplay.Boot) | 包装 `DiseaseVitalsService`:**无病程 ⇒ 零体征**(它原本会抛 `PatientCourseNotFoundException`);**只吞这一种**异常 |

**判据语义**:`(patientMask ∧ ChannelMask) == ChannelMask` —— **合取**(不是「任一」),
多规则命中取**最高档**。读 `SignChannelMask` 而非病种 ⇒ `AC-11-01` ②(体征相同、病种不同 ⇒ 载荷逐字段相等)**天然成立**;
读 `Position`/`Trend` 则撞「sim 禁读回 float」⇒ 不用。
**规则八**由夹具钉住:命中禁忌**不拦截、不减药效**(`test_contraindication_hit_doesNotBlockOrReduce`)。

产物 `prescription_actions.cooked.bytes` = **127 B / 1 行 / ConfigVersion `0x83059D54`**。

---

## 二、实跑判据

| 套件 | 结果 | 截至时刻 | commit |
|---|---|---|---|
| **EditMode 全量** | `6059 / 6022 / 0 / 37` | 2026-10-30 **09:35 CST** | `dbdcf956` + 工作树 |
| **PlayMode 全量** | `238 / 234 / 0 / 2 / 2` | 2026-10-30 **09:36 CST** | 同上 |

**等式**:`6059 − 6043 = 16` = 3(`bands` 门)+ 7(结算器)+ 6(11 的禁忌判据)。
（`6043/6006` = 本轮开工前的全量基线。）

**机械门**:`kindgen --check` 全过(42 struct / 171 字段)· `debtcount --check` 通过 ·
`cooked_drift_all_artifacts_test` 绿(三份产物均已重烘)· `CatalogFreshnessGateTest` 绿(37 条)·
`WriterExistenceGate` 11 条全绿。

---

## 三、债册变动

| 债 | 变更 | 依据 |
|---|---|---|
| `TD-sim-03` | `open` → `closed`(前一轮) | 注册表 ≥1 件非致死伤情 |
| `TD-prescription-09` | `partial` → **`closed`** | 承载 + 判定两半均落;「53 不可自建第二真源」的读法定死 |
| **`TD-sim-04`** | **新立 `open`**(P0 面 Y) | `SignChannelMask` 在生产恒 0(9 的 F2 未接线)⇒ 判据恒不命中 |
| `TD-medcons-15` | `open` 维持 | 机制仍未被玩家感知(见下) |

计数:`条目 138 → 139` · `open 57 · closed 79 · partial 2` · **未闭 P0 面 33**(分布 9/31)。

---

## 四、⚠️ 必须随「已落」一起读的话

**链是完整的,但玩家感知不到。** 因果链:

```
11 判据读 VitalsDto.SignChannelMask
      ↑ 生产恒为 0(9 的 F2 体征投影未接线,DiseaseVitalsService 自陈「不可能产出 signs」)
⇒ 判据恒返回 -1 ⇒ 载荷第八项恒哨兵 ⇒ 53 结算器恒不写事件
```

⇒ 本轮的六条 11 夹具全部靠 `StubVitals` **注入**掩码 —— 走的是注入路径,**不是生产路径**。
任何下游引用「药害机制已可用」时必须带上这句。缺口 = **`TD-sim-04`**(真源在 8 的体征词表 + 阈值)。

另有两条**如实登记**:① `INJ_DRUG_HARM` 是 R2 表外的第十二项,词表收口归 9 的 GDD 轮;
② 数值(`a_peak 30` / `tau_peak 180` / 三档掩码 `1/3/73`)全是**草案**,归用户数值轮。

---

## 五、可复算命令

```bash
# 回归
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ['unity/Logs/full-editmode-1030e.xml','unity/Logs/full-playmode-1030b.xml']:
    r=ET.parse(f).getroot()
    print(f, r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))
PY

# 三份产物现状
ls -l unity/Assets/DataCooked/53_cost_table.cooked.bytes \
      unity/Assets/DataCooked/disease_registry.cooked.bytes \
      unity/Assets/DataCooked/prescription_actions.cooked.bytes

# 门
python3 tools/kindgen/kindgen.py --check
python3 tools/debtcount/debtcount.py --check
```
