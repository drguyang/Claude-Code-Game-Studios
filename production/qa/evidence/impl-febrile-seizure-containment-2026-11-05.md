# 实现轮证据件 —— 52 急召容纳「儿童高热惊厥」(`DIS_FEBRILE_SEIZURE`)

**日期**:2026-11-05 · **轮次**:sprint-24 · **commit**(回归时点):`7f6af7e9` + 工作树
**权威件**:用户裁定(两项,见 §一)· `design/gdd/disease-simulation.md` R3.1 / R3.3 ·
`design/gdd/diagnosis-system.md` R-8.2 · `docs/tech-debt-register.md` 的 `TD-diagnosis-02` / `TD-sim-06` / `TD-emergency-01`

> **一句话**:按用户「登记债,增加必要内容进行容纳」+「放宽到 9,做惊厥」+「到场时还在抽」三条裁定,
> 把 **P0 病种上限 8 → 9**,新增自限型对照行 `DIS_FEBRILE_SEIZURE` 与体征词条 `sign_convulsion`,
> 使 52 急召具备一个「同一张脸、两个相反正确答案」的判断案子。
> **⚠️ 端到端仍恒假** —— 见 §七,禁借绿。

---

## 一、用户裁定(逐条)

| # | 裁定 | 日期 |
|---|---|---|
| ① | **登记债,增加必要内容进行容纳**(不是把三个候选推给 P1) | 2026-11-05 |
| ② | **P0 病种上限 8 → 9,做惊厥**(创意总监首选案) | 2026-11-05 |
| ③ | **`sign_convulsion` 走姿态通道**(「到场时还在抽」⇒ 体格所见),**不放宽 AC-8-34** | 2026-11-05 |

---

## 二、落盘清单(18 个文件)

### 数据(4)

| 文件 | 改什么 |
|---|---|
| `assets/data/disease_registry.json` | +`DIS_FEBRILE_SEIZURE`(id 17):`kind=disease` · `self_limit=true` · `lethal=false` · `plateau=false` · `a_peak="20"` · `tau_rise=60` / `tau_peak=300` / `tau_fall=1200` · `signs[]` = `sign_flush`(1) / `sign_convulsion`(4) / `sign_neck_supple`(4) |
| `assets/data/diagnosis_signs.json` | +`sign_convulsion`(姿态 · `tier_named=1` · 视诊 · 阳性体征 · 三档措辞) |
| `assets/data/disease_action_axis.json` | `treatable_by` +`DIS_FEBRILE_SEIZURE → [{action:10(柳树皮), polarity:"symptomatic"}]` |
| `assets/data/disease_names.json` | 病名闭集 8 → **9**(+热性惊厥) |

### 烘焙产物(4,均已重烘)

| 产物 | 行数 / 规模 | `ConfigVersion` |
|---|---|---|
| `diagnosis_signs.cooked.bytes` | **44 行** · 4044 B | `0x653DD991` |
| `disease_registry.cooked.bytes` | **16 行**(7 伤情 + 9 病种)· 4171 B | `0x583B1393` |
| `disease_action_axis.cooked.bytes` | 5 处置 + **7 关系** · 412 B | `0xA6CBE9DE` |
| `53_cost_table.cooked.bytes` | 124 B | 重烘(**见 §四**) |

### 文档(4)

| 文件 | 改什么 |
|---|---|
| `design/gdd/disease-simulation.md` | **六处**上限表述同批订正(§Quick Reference ④ · 规则九 · R3.1 · R3.3 增行 · OQ-3 · 边界表),全部**原文划线保留 + 加注** |
| `design/gdd/diagnosis-system.md` | R-8.2 姿态组 +1 行并并入 Lv1 档;§Context 的「伤情 5 + 病种 8 = 13 行」复算为 7 + 9 = 16 |
| `design/gdd/systems-index.md` | §风险表的上限表述同步 |
| `docs/tech-debt-register.md` | 三条债登记 + `TD-diagnosis-02` / `TD-sim-06` 推进为 `partial` + 八十二订 |

### 测试(6,全部只改期望值,**未新增夹具**)

`SignTableTest`(44 行 / 阳性 40 / 金标重钉 / PinnedSignIds +1)·
`examine_evaluator_test`(44)· `examine_driver_test`(44)·
`disease_registry_bake_pipeline_test`(体征期望表 +1 行、handle 期望表 +1 行)·
`registry_bake_test`(非空关系 6 → 7)· `tutorial_disease_name_source_test`(显示名 12 → 13)

---

## 三、这行数据的唯一理由(设计判据)

| 病种 | `a_peak` / `scale` | 与 `COMA`(29491/65536 = 0.44999695)的关系 |
|---|---|---|
| `DIS_MALARIA` | 45/100 = **0.45** | **≥ 阈值**(余量 −3×10⁻⁶)⇒ 可达昏迷 |
| `DIS_FEBRILE_SEIZURE` | 20/100 = **0.20** | **远低于**(也低于 `CRITICAL` 0.30)⇒ 永不危殆 |

同一个「正在抽的孩子」,**一个会昏过去,一个不会** —— 这 0.25 的差是 2×2 判断矩阵里
唯一靠数据看得见的差。若两行峰值相同,这个案子就退化成「看脸猜」。

---

## 四、`53_cost_table` 为什么必须重烘(不是回归)

首轮回归有 2 条红落在 53 代价表(`ConfigVersion 解码不等` + `cooked 漂移`)。
**病因不是本轮改坏了它**,而是该表的设计本身就依赖注册表:

```
CostTable53Bake.cs:11-13
// ⚠️ 两处输入:本表同时依赖 53_cost_table.json 与 9 的 disease_registry.json
//   (N-1 / N-3 / V-10 的宾语都在后者里)⇒ ConfigVersion 必须两个源一起派生
```

⇒ 加一行病种 ⇒ 53 的 `ConfigVersion` **理应**改变 ⇒ 产物必须重烘。已按
`MedConsTableBakeRunner.RunBatch` 重烘三表(53_cost_table / 53_delay_table / 53_outcome_table)。
**这条行为是它自己注释预告过的,不是意外。**

---

## 五、回归(三元组)

| 项 | 值 |
|---|---|
| **本轮 EditMode** | **`6909 / 6872 / 0 / 37`** |
| **本轮 PlayMode** | **`341 / 337 / 0 / 2 / 2`**(total/passed/failed/skipped/inconclusive)· XML @ `2026-11-05 10:00:32` · 退出码 **2 = PlayMode 正常退出码**,判据看 XML |
| **基线 EditMode** | `6909 / 6872 / 0 / 37` @ `2026-11-05` · `-06` 轮 · `7f6af7e9` |
| **增量等式** | `6909 − 6909 = 0` —— **本轮未新增 `[Test]`**,只改既有夹具的期望值 ⇒ 总数**不变是对的**,不是「没跑」 |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/editmode-full3.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

⚠️ **首轮与次轮各有红,已逐条归因,无一条是本轮改坏的**:
① `ExamineDriver` / `signsMatchGddVocabulary` / `handleIsDerived` / `has6TreatableBy` —— **变更哨兵**,按设计须同步改;
② `53_cost_table` 两条 —— **跨源 `ConfigVersion` 派生**,须重烘(见 §四);
③ `tutorial_disease_name_source_test` 两条 —— 病名闭集 12 → 13,**同类哨兵**。

---

## 六、证伪轮(同命令内 改→跑→还原 · SHA-256)

**驱动器**:`$CLAUDE_JOB_DIR/tmp/falsify_febrile.py` + `falsify_m3b.py`。
**判红 = 指定靶测出现在 `Failed` 集合里**(不是「总数变了」)。**每轮跑全量**(见下方教训)。

| # | 变异 | 期望红的靶点 | 实测 | 判定 |
|---|---|---|---|---|
| M1 | `sign_convulsion` 改名 | `SignTableTest.test_r82_existence_pinnedIdsCountsAndTiers` | fails=5,靶中 | ✅ |
| M2 | 注册表里 `convulsion` / `neck_supple` 顺序对调 | `DiseaseRegistryBakePipelineTest.test_realSource_signsMatchGddVocabulary_perRow` | fails=4,靶中 | ✅ |
| M3 | 处置轴删掉 `DIS_FEBRILE_SEIZURE` | `RegistryBakeTest.test_diseaseActionAxis_has6TreatableBy` | fails=3,靶中 | ✅ |
| M3′ | 同上,同时期望 `handleIsDerived` 红 | `…test_realSource_handleIsDerivedIntoClosedSet_perRow` | **未红** | ❌ **打偏**(见下) |
| **M3b** | 新行 `polarity` `symptomatic` → `causal` | `…test_realSource_handleIsDerivedIntoClosedSet_perRow` | fails=4,靶中 | ✅ |
| M4 | `sign_convulsion` 改贴**病史通道** | `SignTableTest.test_ac832_repoSeedBakes34Rows`(AC-8-34 白名单) | fails=26,靶中 | ✅ |

**还原判据**:三份数据文件 SHA-256 与变异前**逐字节相同**(`sha256sum -c pre-falsify-febrile.sha256` ⇒ 三行「成功」)。

### ⚠️ 本轮的两次「打偏」如实登记

1. **第一遍驱动器按文件名猜类名/命名空间,四条全 `NOT-RED`** ——
   `sign_table_test` 的真类名是 `SignTableTest`、`registry_bake_test` 是 `RegistryBakeTest`;
   而 `DiseaseRegistryBakePipelineTest` 住在 `Tests.Unit.Sim` 而非 `Tests.DiseaseSimulation`
   ⇒ 过滤器 `"DiseaseSimulation"` 对它是**零命中**(`fails=0`)。
   真实信号当时就存在(M1 红 3 条、M4 红 24 条),只是**靶名对不上** ⇒ 判成 `NOT-RED`。
   **修复**:靶名一律从实跑 XML 里取,过滤器改为跑全量(一个 filter 覆盖不了两个命名空间)。

2. **M3′ 的第二个靶点本来就推不动** —— `DiseaseHandle.Derive`(`Sim/DiseaseHandle.cs:101-104`)是
   `causal(轴) → care(硬编码) → none(硬编码) → symptomatic_only(兜底)`。
   删掉一个**本就 symptomatic** 病种的轴条目 ⇒ 落在兜底上 ⇒ 句柄**仍是 `symptomatic_only`**。
   ⇒ 变异等于没变。**改由 M3b(翻 polarity 为 causal)替代,已红。**

---

## 七、本件**没有**证明的事(禁借绿)

- **端到端仍恒假** —— `canSpawnPatient` 在 `CompositionRoot` 里仍恒 `false`(等 `TD-randomevents-03`
  的 key → 病种映射落地)。本件只证明**数据行的存在与一致性**,不证明**任何一个病人真的被生成出来**。
- **不证明 2×2 判断矩阵可玩** —— 那要求 8 的体征真能被读出(`READ_FLOOR`)、11 的处方真能被下达、
  37 真能立案。三者今日都不由本件覆盖。
- **`DIS_MALARIA` 的 0.45 余量未复核** —— 余量仅 **−3×10⁻⁶**,而 0.45 定点化 = 29491.2 → 取整 **29491 = 阈值本身**。
  「疟疾能不能昏过去」取决于阈值比较用 `≥` 还是 `>`,**本件未查**。
- **未跑任何真机 / PlayMode 之外的集成验证**。

---

## 八、顺带查出的一处静默陷阱(未立债,先记)

`DiseaseHandle.Derive` 的 `care` / `none` 是**硬编码集合**,只有 `causal` 读处置轴。
⇒ **新病种若本该有对因药却漏了轴条目,`handle` 会静默变成 `symptomatic_only`,不报错、
不影响任何既有夹具**(本轮 M3′ 就是这个现象的实证)。
建议:哪天加第 10 个病种时,同批补一条「轴条目存在性 ⊇ 病种表」的构建期断言。
