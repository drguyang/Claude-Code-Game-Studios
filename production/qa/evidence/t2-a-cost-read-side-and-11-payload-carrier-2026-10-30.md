# T2 前置轮 A —— 53 代价表的**运行期读方** + 11 载荷的**禁忌载体** + 注册表**第一件非致死伤情**

**日期**:2026-10-30 · **批次**:`ADR-035` T2 前置(用户指令「现在实现」三条缺口)
**commit**:`7ab13163` + 工作树(未提交)
**性质**:实现轮。**未提交任何代码**;本件的全部数字均为本轮实跑。

---

## 一、本轮要补的三条缺口(用户原话的逐条对应)

| # | 缺口 | 本轮处置 | 结果 |
|---|---|---|---|
| ① | **零运行期读方** —— `CostTable53` 没有装配点,cooked 表装进来没人要 | 新建 `CostTable53CookedCodec` + provider 五处接线 + `DataCorePreloader` 一条 + 12 条夹具 | ✅ **已消** |
| ② | **零真实行** —— 注册表 5 件伤情全 `lethal=true`,合法的非致死伤情一行也填不出来 | 注册表新增 `INJ_BLUNT`(自限型 / 非致死)+ 重烘 + 三条逐行期望表同步 + `TD-sim-03` 结案 | ⚠️ **半消**:注册表有了合法落点,**代价表 `rows` 仍是空数组**(内容轮) |
| ③ | **机制正面路径从未跑过真实材料** | 见 ①②:材料(1 行非致死伤情)有了,表里仍无行 ⇒ **仍成立** | ❌ **未消**,归 T2 |

> ⚠️ **第 ③ 条必须照实读**:本轮**没有**让药害机制跑过一次真实结算。它需要
> ① 代价表里存在真实行(内容轮)+ ② 11 真的判定「命中禁忌」(`TD-prescription-09` 的判定半边)。
> 本轮只补了**结构**:表装得进来、载荷装得下、注册表有合法落点。

---

## 二、改动清单(逐件,含判据落点)

### ① 运行期读方(缺口 ①)

| 文件 | 改动 |
|---|---|
| `unity/Assets/Gameplay.Presentation/CostTable53CookedCodec.cs`(新) | cooked → `CostTable53.Table` 解码器。布局与写方 `Editor.Tools.Bake/CostTableCookedWriter.cs` **逐字对称**:`row_count: i32` + 逐行 `{harm_kind, band, injury_id, severity_band}: i32`。**全部纵深防御委托 `CostTable53.CreateTable`**(N-1 / kind / 单射 / §⑥ 过量挂起 / 槽外键),本件零重做 |
| `unity/Assets/Gameplay.Presentation/AddressablesDataProvider.cs` | 五处:`CostTableAddress` 常量 · `_costTable` 字段 · `Get<TDataSet>` 分派 · `Preload()` 调 `EnsureCostTable()` · 错误串补 `CostTable53DataSet`。`EnsureCostTable` 先 `EnsureDiseaseRegistry()`(L3 外键判据建在 9 的注册表上) |
| `unity/Assets/Gameplay.Presentation/DataCorePreloader.cs` | `DataCoreAddresses` 增 `CostTableAddress` |
| `unity/Assets/Tests/EditMode/MedConsequences/cost_table53_cooked_codec_test.cs`(新) | **12 条**:正向往返 2(盘上产物 = 现烘产物 / 空表惰性)+ 地址常量同值 1 + 六类损坏拒收 7(魔数 / 截断 / 负行数 / 未知 `injury_id` / 致死伤情 / 重复 `(kind,band)`)与反例 1 |

**⚠️ 一处刻意缺口,如实登记**:产物里**不含** `max_severity_band`(S9 定稿的布局),
⇒ 运行期 `CreateTable` 的 `maxSeverityBand` 实参只能是 `null`,**上界检查在运行期不发生**。
缺口仅在「产物被人为篡改且只把 `severity_band` 顶破上界」这一形态;要闭合须改产物布局
(加 4 字节)+ 重烘 ⇒ 改的是 S9 已结案的格式,**本轮未做**。文件头已注明。

### ② 注册表第一件非致死伤情(缺口 ②)

`assets/data/disease_registry.json` 新增 `INJ_BLUNT`(id 15,`kind: injury`,`lethal: false`,
**自限型** `self_limit: true` —— 形态由 `TD-sim-03` 的 2026-10-30 订正前置约束决定:
急性保持型非致死会被 **R1-22** 当场拒)。`signs` = `sign_moan` 单条(钝击不在五条 `sign_wound_*` 形态词里,
新造词须改 25 的伤情词表 + GDD 表,不属本轮)。

**数值是草案**(`_rowNote` 已标):`a_peak "20"` / `tau_rise "40"` / `tau_peak 120` /
`tau_fall "600"` / `severity 2` —— **归用户数值轮**,本轮只保证形态合法、能过 17 条区间校验。

**因此转红并已按「重新推」而非「改数字」修好的既有夹具(6 条)**:

| 夹具 | 原判据 | 为什么不能只改数字 | 修法 |
|---|---|---|---|
| `DiseaseRegistryDataTest.test_rowsPassTheRealValidator` | `rows.Count == 5` | 真表伤情行 5 → 6 | 改为 `Whitelist.Length + 1`,并在注释里写清「5 白名单 + 1 非致死」 |
| `..._test_allRows_areAcuteHoldType` | **全部**伤情行须急性保持型 | `INJ_BLUNT` **故意**是自限型;遍历全部 = 把设计判成违例 | 收窄到**白名单行**(判据真正要守的是「白名单能致死」),并补「白名单行不全 ⇒ 红」的靶场守卫 |
| `..._test_allRows_canReachDeathThreshold` | **全部**伤情行须达死线 | 同上(它是**非致死**伤情,达不到才是对的) | 同上 |
| `..._test_missingRow_isDetected` | 字面 `5` / `4` | 与「少一行能被抓」无关 | 改**相对**计数(`rows.Count - 1`) |
| `DiseaseRegistryBakePipelineTest.test_realSource_handleIsDerivedIntoClosedSet_perRow` | 逐行期望表 13 行 | 表行数变了门就红(设计如此) | 按 GDD 推 `INJ_BLUNT` 的 handle = `symptomatic_only` |
| `..._test_realSource_signsMatchGddVocabulary_perRow` | 逐行期望表 13 行 | 同上 | 按 GDD :473 推 `signs = [sign_moan]`,并在注释写明**为何不复用** `sign_wound_*` |

> ⚠️ 两条 `allRows_*` 是**收窄**,不是放宽:收窄后仍断言「白名单 5 行全部在靶场里」
> (`Assert.That(rows.Count, Is.EqualTo(Whitelist.Length))`),非白名单行的对应性质
> (必须 `Lethality == 0`)由 `test_dataFile_whitelistIsSubsetOfInjuryRows_andNonWhitelistInjuriesAreNonLethal` 守着。

### ③ 11 载荷的禁忌载体(`TD-prescription-09` 的**承载**半边)

| 文件 | 改动 |
|---|---|
| `unity/Assets/Sim.Contracts/Payloads/HistoryPayloads.cs` | `DrugTreatmentAppliedPayload` 追加**第八项** `ContraindicationBand: int` + 常量 `NoContraindication = -1`。**前七项一字未动** |
| `unity/Assets/Sim.Codec/PayloadCodec.History.cs` | tag **8** 编码 / 解码;`RequireCompleteMask` 由 `(1<<7)-1` → `(1<<8)-1` |
| `unity/Assets/Sim/Prescription/PrescribeFlow.cs` | 写者**显式**传哨兵(不走缺省),并在注释写明「载体有了、判定还没有」 |
| `design/registry/entities.yaml` | `payload_schema` 追 `contraindication_band: i32` |
| `design/gdd/prescription-and-medication.md` | `:183` 载荷表追行 + 段落注;`AC-11-03` 加 2026-10-30 注(**七项齐备的判据面不变**) |
| `unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs` | 新增 `ElevenSideOnlyFields = { "ContraindicationBand" }`(与 `TenSideOnlyFields` 对称)+ **陈旧性检查**(该名在 11 侧消失 ⇒ 报错)+ `RunAll` 摘要行 |
| `unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs` | 新增 tag-8 往返夹具(用 **`3` 而非 `-1`** —— 用哨兵往返分不出「没写」) |
| `unity/Assets/Tests/EditMode/PrescriptionMedication/prescribe_flow_test.cs` | 追加「流程显式写哨兵」断言 |

**为什么是「档序数」而不是「哪条禁忌」**(实现期就地裁定,未经用户逐条确认):
53 的代价表键是 `(harm_kind, band)`;若传禁忌条目下标,53 必须自持一份「条目 → 档」映射
= 复制 11 的数据(违 `AC-11-02` 的反推 + ADR-035 §① 的所有权)。传档序数 ⇒ 53 只查表。

**为什么删掉七参重载**(首版曾加,实测撤销):七参重载只能用 `: this(...)` 实现,那会在
**`Sim.Contracts`** 里产生一个 `call .ctor` 站点 ⇒ `AC-11-22` 的构造点白名单门**直接转红**
(首轮实跑 2 红)。放宽该门 = 门单方面放宽 AC 原文 ⇒ 改为**唯一八参 ctor**,14 个构造点全部显式表态。

**⚠️ 与 `AC-11-01` ② 的边界**:该条要求「体征逐位相同、病种不同 ⇒ 载荷逐字段相等」。
今日取值**恒为哨兵**(不依赖任何病人状态)⇒ 成立。将来若由 **病种** 派生则相抵,须先改 `AC-11-01`;
若由 **体征** 派生则自然兼容。**这一条须用户裁**(登记在 `TD-prescription-09` 的进展行)。

---

## 三、实跑判据(带三元组)

### 回归

| 套件 | 结果 | 截至时刻 | 批次 / commit |
|---|---|---|---|
| **EditMode 全量** | `6043 / 6006 / 0 / 37` | 2026-10-30 **08:45 CST** | `7ab13163` + 工作树 |
| **PlayMode 全量** | `238 / 234 / 0 / 2 / 2` | 2026-10-30 **08:46 CST** | 同上 |
| `DaYiJingCheng.Tests.Unit.Sim` | `393 / 393 / 0 / 0` | 2026-10-30 08:42 CST | 同上 |

**对照基线**(引用自 S9 批,非本轮实跑):EditMode `6029 / 5992 / 0 / 37` · PlayMode `238 / 234 / 0 / 2 / 2`。

**等式**(判据 = `total − 前次 = 新增条数`):`6043 − 6029 = 14`
= `CostTable53CookedCodecTest` **12** + `CostTableBinderTest` 的 V-10 正向夹具 **1** + `DrugEventTest` 的 tag-8 往返 **1**。
复算:`grep -c "^\s*\[Test\]" unity/Assets/Tests/EditMode/MedConsequences/cost_table53_cooked_codec_test.cs` ⇒ **12**。

**⚠️ 一条本轮抓到的方法论失误,如实记**:前两轮跑「三套件」时用的过滤器是
`DaYiJingCheng.Tests.DiseaseSimulation`,而注册表夹具的命名空间是
**`DaYiJingCheng.Tests.Unit.Sim`** ⇒ 那 6 条**一次都没进过靶场**,`INJ_BLUNT` 加进去后
**一直是红的**,直到全量 EditMode 才暴露。**「按目录猜命名空间」是假绿通道**。

### 机械门

| 门 | 结果 |
|---|---|
| `kindgen --check`(A1–A5) | ✅ `42 个载荷 struct / 171 个字段类型受判`(前值 170,`+1` = 新字段) |
| `debtcount --check` | ✅ 通过 |
| `cooked_drift_all_artifacts_test` | ✅ 重烘后转绿(含 `53_cost_table.cooked.bytes` 与 `disease_registry.cooked.bytes`) |
| `CatalogFreshnessGateTest` / `nav_chunk_slicing_test` | ✅ 「确保 data-core 组」37 条 + 「构建 Addressables 内容」后转绿 |
| `ProviderWiringGateTest` | ✅ 首轮抓到漏登 `DataCoreAddresses`,补齐后 117/117 |
| `PrescriptionWriterGates`(AC-11-22 构造点) | ✅ 撤销七参重载后转绿 |

### 与本件直接相关的新增夹具(逐条)

`CostTable53CookedCodecTest`(12):盘上=现烘 · 空表惰性 · 地址常量同值 · 魔数 · 截断 ·
负行数 · 未知 `injury_id` · 致死伤情 · 重复 `(kind,band)`(断言报文含「单射」)· 同伤情跨档合法 ·
报文保留 Sim 侧文字 · `registry == null`。
`CostTableBinderTest` 新增 V-10 正向(注入全致死注册表 ⇒ 必报 `TD-sim-03`);
`DrugEventTest` 新增 tag-8 往返 + 哨兵语义。

---

## 四、债册变动

| 债 | 变更 | 依据 |
|---|---|---|
| `TD-sim-03` | **`open` → `closed`** | 消解判据「≥ 1 件 `kind: injury` ∧ `lethal == false`」已满足:实跑 `6 5` / `['INJ_BLUNT']` |
| `TD-prescription-09` | **`open` → `partial`** | 承载半边已落;**判定半边仍缺** ⇒ 字段今日恒 `-1` |
| `TD-medcons-15` | `open` 维持 | T2 仍不可开工(缺真实行内容 + 11 判定) |

计数:`open 58 → 56` · `closed 77 → 78` · `partial 2 → 3` · **未闭 P0 面 34 → 33**。
`TD-COUNT` 块已由 `python3 tools/debtcount/debtcount.py` 就地重生成。

---

## 五、仍未做的(逐条,禁借绿)

1. **代价表 `rows` 仍是空数组** —— 填真实行是内容轮,须用户定「哪味药 × 哪档 → 哪件伤情」。
2. **11 无禁忌判定实现** —— 数据侧 `contraindications[]` 在 21a 里 **0 条**;判定逻辑归 11 的实现轮。
3. **53 的结算组件 + `CompositionRoot` 装配 + 端到端真装配夹具** —— 未开工。
4. **运行期 L3 的上界检查** —— 产物不含 `max_severity_band`(见 §二 ① 的缺口登记)。
5. **`INJ_BLUNT` 的数值** —— 草案,归用户数值轮。
6. **`AC-11-01` ② 的派生口径** —— 须用户裁(病种派生 ⇒ 须先改该 AC;体征派生 ⇒ 兼容)。
7. **本轮全部改动未提交**。

---

## 六、可复算命令

```bash
# 三件回归(XML 为准,不看退出码)
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ['unity/Logs/full-editmode-1030b.xml','unity/Logs/full-playmode-1030.xml',
          'unity/Logs/t2g-unit-sim.xml']:
    r=ET.parse(f).getroot()
    print(f, r.get('total'), r.get('passed'), r.get('failed'), r.get('skipped'))
PY

# 注册表现状(6 件伤情 / 5 件致死 / 非致死 = INJ_BLUNT)
python3 -I -c "import json;d=json.load(open('assets/data/disease_registry.json',encoding='utf-8'));inj=[x for x in d['diseases'] if x['kind']=='injury'];print(len(inj), sum(1 for x in inj if x['lethal']));print([x['key'] for x in inj if not x['lethal']])"

# kindgen / 债册
python3 tools/kindgen/kindgen.py --check
python3 tools/debtcount/debtcount.py --check

# 新增夹具条数
grep -c "^\s*\[Test\]" unity/Assets/Tests/EditMode/MedConsequences/cost_table53_cooked_codec_test.cs   # 期望 12
```
