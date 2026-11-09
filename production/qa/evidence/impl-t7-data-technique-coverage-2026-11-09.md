# T7 补数据 —— 技法映射 · 蛇毒行 · 破伤风治疗项(2026-11-09)

> **范围**:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §7 **T7**。
> **上游**:T4(`7fbb56a7` 技法门 + 等级门)· T5a(`20d3e2bd`)· T5b(`cef0bdcc`)· T6(`17067116`)。
> **本件含一次裁决**:首轮落盘撞上 `OQ-10-4` 的冻结,呈报后用户裁定「**开,按你说的改 OQ-10-4**」
> ⇒ P0 急救动作 **2 → 4**,两门技法才落得了地。撞线证据保留在 §3,不删。

---

## 1. 改动清单

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `design/gdd/emergency-procedures.md` | 改 | **`OQ-10-4` 二次裁定**:P0 急救动作 2 → 4(§一 Amendment · `:847` 注 · OQ 表行) |
| 2 | `unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs` | 改 | 枚举 +`Debridement = 2` / `SnakebiteCare = 3`;头注与影子注就地加注 |
| 3 | `unity/Assets/Tests/EditMode/EmergencyProcedures/action_tables_bake_test.cs` | 改 | `test_dc4_enumContent_p0TwoActions` → `…p0FourActions`(2 → 4 + 两个新名) |
| 4 | `assets/data/disease_action_axis.json` | 改 | 处置轴 **5 → 7 门**;`treatable_by` **7 → 15 关系**(16 键) |
| 5 | `assets/data/disease_registry.json` | 改 | **16 → 17 行**:`TOX_ENVENOMATION`(id 18,`kind: poison`) |
| 6 | `unity/Assets/DataCooked/disease_action_axis.cooked.bytes` | 重烘 | 412 → **736 B**,`0xAEC288E6`(7 处置 + 15 关系) |
| 7 | `unity/Assets/DataCooked/disease_registry.cooked.bytes` | 重烘 | 4299 → **4686 B**,`0x7C7FF96D`(17 行) |
| 8 | `unity/Assets/DataCooked/53_*.cooked.bytes` | 重烘 | 53 代价表的 `ConfigVersion` 由「代价表源 + **注册表源**」一起派生(`CostTable53Bake.cs:91-95`)⇒ 改注册表必须重烘它 |
| 9 | `unity/Assets/Tests/EditMode/DiseaseSimulation/technique_coverage_test.cs` | **新** | 覆盖清单门(**8 条**) |
| 10 | `unity/Assets/Tests/EditMode/DiseaseSimulation/registry_bake_test.cs` | 改 | 5 → 7 门 / 7 → 15 关系 |
| 11 | `unity/Assets/Tests/EditMode/DiseaseSimulation/disease_registry_bake_pipeline_test.cs` | 改 | `handle` 期望表 +`TOX_ENVENOMATION` |
| 12 | `production/epics/disease-simulation/story-007-…md` · `adventure-catalog.md` · map §7 | 改 | **就地加注**(原文不删) |

---

## 2. 映射表

| 键 | 技法 | 依据 |
|---|---|---|
| `INJ_STAB` / `INJ_LACERATION` / `INJ_HEMORRHAGE` / `INJ_CLAW` | `0 止血包扎` | map §3.1 序章「压迫包扎 · 抬高患肢」;`0` 在**起步集 `{0,1}`** 内 ⇒ 立刻可投 |
| `INJ_BITE` / `INJ_BLUNT` | `2 清创缝合` | map §3.3 第四章「#8 战场清创 → 清创 · 缝合 → 咬伤 / 钝击」 |
| `DIS_TETANUS` | `2 清创缝合` | GDD `disease-simulation.md:445` 逐字「**10 清创 · 预防**」 |
| `TOX_ENVENOMATION` | `3 蛇伤处置` | map §3.1 第三章「蛇伤处置:制动 + 加压包扎」 |
| `INJ_DRUG_HARM` | *(不挂)* | 自限型药毒无处置(GDD `:659`)⇒ 落 `Undeclared`(放行 + 计数) |
| `DIS_NEURASTHENIA` | *(不挂)* | 豁免类病种(R3.6),P0 无任何处置对它有效 |

### 2.1 极性一律 `symptomatic`(不是随手选的)

`DiseaseHandle.Derive(key, hasCausalTreatment)` **只看轴里有没有 `causal` 行**:
填 `causal` 会把 `DIS_TETANUS` 从 `none` 改判成 `causal`,而 GDD `:294` 把 `none` **钉死**为
「仅 DIS_TETANUS」、`AC-43` 逐病种断言 —— 那是**改裁决**,不是补数据。取 `symptomatic` ⇒ 全部
`handle` 逐行不变(`test_tetanusAndVenom_carryTechniqueWithoutBreakingHandleSemantics` 守着这一条)。

### 2.2 一处**没有**正解的形态(如实登记)

GDD 说破伤风的手柄是「清创 · **预防**」—— 「发病前有效、发病后无效」。
`{causal, symptomatic}` **都不精确表达**它。本件取 `symptomatic` 是「不制造裁决冲突的落法」,
**不是语义正解**。正解要么给轴引入第三种极性,要么把预防挪进 `compounds[]`(schema 今日无此字段)——
两件都超出「补数据」范围 ⇒ **待裁**。

---

## 3. 撞线实录(首轮 5 红 —— 本件之所以要一次裁决)

**三元组**:`2026-11-09T12:16:59Z` · **T7 首轮批** · `17067116` + 工作树。
**EditMode 全量** `7244 / 7202 / 5 / 37`。

| # | 红在哪 | 报文要点 |
|---|---|---|
| 1 | `ActionTablesBakeTest.test_dc4_enumMatchesAxisOwner10Segment` | `9 的轴 owner=="10" 段 id=2 须有枚举成员(单一轴:轴增员 ⇒ 枚举须同步)` |
| 2 | `ActionAxisReaderGateTest.test_axis_artifactOwner10Set_matchesEmergencyActionEnum` | `Expected < 0, 1 > But was: < 0, 1, 2, 3 >` |
| 3 | `DiseaseRegistryBakePipelineTest.test_realSource_signsMatchGddVocabulary_perRow` | `Expected: 16 But was: 17` |
| 4 | `CostTable53CookedCodecTest.test_diskArtifact_matchesFreshBake` | `ConfigVersion 解码不等` |
| 5 | `cooked_drift_all_artifacts_test.drift[53_cost_table.cooked.bytes]` | 字节 index 12 不同 |

⇒ 1/2 是**裁决面**(轴 ↔ 枚举双向钉死,且枚举成员数被 `OQ-10-4` 冻结为 2),3/4/5 是**派生面**。
首轮因此**整体撤回**并呈报。用户裁定「开」后,本件按 §1 的清单重做。

> ⚠️ **教训留档**:「轴增员」在这个仓里**不是纯数据改动** —— 它同时是
> `EmergencyAction` 枚举的改动和 `OQ-10-4` 的改动。三条夹具把这三件事焊在一起,
> 这是设计意图(单一轴归 9),不是门太严。

---

## 4. 蛇毒行:哪些是派生的,哪些是裁定的

⚠️ **本行的曲线与体征是 T7 的派生值,未经用户逐值裁定。** 每处取值都能指回既有已裁口径:

| 量 | 值 | 出处 |
|---|---|---|
| 形态 | 急性保持型(`self_limit=false ∧ plateau=false`) | R1-22:非自限又 `lethal` 必须此型 |
| `A_peak/Scale` | `100/100 = 1.00` | Tuning §五 `:1712`「急性保持型 = 1.000」;R1-34 要求 ≥ DEATH 0.58 |
| `incubation` | 600(30 s) | 中毒起效慢于外伤(`INJ_*` 为 0),同 `INJ_DRUG_HARM` 的「更慢更久」 |
| `tau_peak` / `tau_rise` | 6000 / 1200 | 同上;R1-30 上升段余量 ≈ 0.989 ≫ ε_MIN |
| `appear_at` | `1/4`(0.25 早期)· `11/20`(0.55 中期)· `17/20`(0.85 晚期特异) | 「按行派生」规则 `_appearAtNote`:相位分数 × 峰值 |
| 体征 7 条 | `pallor` `fatigue` `diaphoresis` `tachypnea` `pulse_rapid` `cold_clammy` `delirium` | **全是 8 的既有词条,零新增** |

**数值轮可整行重定** —— 只保证:① 17 条区间校验全过;② 体征键零新增;③ 相位序医学正确。

---

## 5. 新夹具 `technique_coverage_test`(8 条)

| # | 判据 | 判别力(反证即红) |
|---|---|---|
| 1 | 每行**要么**有轴行,**要么**在豁免表里 | 造一行两者皆无 ⇒ 红 |
| 2 | 豁免表**不许陈旧** | 给 `INJ_DRUG_HARM` 挂上技法 ⇒ 那条变陈旧 ⇒ 红 |
| 3 | 豁免表**集合相等** | 随手加一条豁免 ⇒ 红 |
| 4 | 每条豁免**理由非空** | 理由改空串 ⇒ 红 |
| 5 | 轴的每个键都必须在注册表里 | 造悬空轴键 ⇒ 红 |
| 6 | 六件伤情各自指向**那一门** | `INJ_BITE` 改成 `0` ⇒ 红 |
| 7 | 破伤风 / 蛇毒各挂一门,且破伤风 `handle` 仍是 `none`、蛇毒仍是 `kind: poison` | 给破伤风填 `causal` ⇒ 红 |
| 8 | 轴 `owner=="10"` 段恰为 `{0,1,2,3}` | 再增员而不改裁决 ⇒ 红 |

**为什么这个门值得存在**:T4 的三态让「未声明」成为合法状态,代价是它**静默** ——
T4 落地时全库 9 行落 `Undeclared`,没有任何门会响。本门把它从**统计事实**变成**具名清单**。

---

## 6. 覆盖数字与复算

**改造前**:注册表 16 行,声明技法 7 行 ⇒ **未覆盖 9 行**。
**改造后**:注册表 17 行,声明技法 15 行 ⇒ **未覆盖 2 行**,两条都是**具名豁免**。

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -I - <<'PY'
import json
reg = json.load(open('assets/data/disease_registry.json', encoding='utf-8'))
ax  = json.load(open('assets/data/disease_action_axis.json', encoding='utf-8'))
rows = reg['diseases'] + reg['injuries']
keys = {r['key'] for r in rows}
declared = {k for k, v in ax['treatable_by'].items() if v}
print('注册表行数', len(rows), '| 处置轴', len(ax['actions']), [a['id'] for a in ax['actions']])
print('关系行', sum(len(v) for v in ax['treatable_by'].values()), '| 覆盖键', len(declared))
print('未覆盖', sorted(keys - declared))
print('owner10', [a['id'] for a in ax['actions'] if a['owner'] == '10'])
PY
# 期望:
#   注册表行数 17 | 处置轴 7 [0, 1, 2, 3, 10, 11, 12]
#   关系行 15 | 覆盖键 15
#   未覆盖 ['DIS_NEURASTHENIA', 'INJ_DRUG_HARM']
#   owner10 [0, 1, 2, 3]
```

---

## 7. 回归

**三元组**:`2026-11-09T12:46:34Z`(EditMode 结束)/ `12:48:37Z`(PlayMode 结束)· **全量** · `17067116` + 工作树。

| 套件 | 本次 | 对照基线(T6 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7246 / 7209 / 0 / 0 / 37`** | `7238 / 7201 / 0 / 37` | **+8 / +8** |
| PlayMode(全量) | **`371 / 366 / 0 / 2 / 3`** | `371 / 366 / 0 / 2 / 3` @ `2026-11-09T11:44:30Z` · T6 批 · `17067116` | **零变化** |

**等式核对**:EditMode `7246 − 7238 = 8` = 新增夹具条数 **8**(`technique_coverage_test`)。✔

### 7.1 中途两轮红(如实登记)

| 轮 | 结果 | 红在哪 | 处置 |
|---|---|---|---|
| 首轮(起草版,未改裁决) | `7244 / 7202 / 5 / 37` | 轴 ↔ 枚举钉死 ×2 · 注册表行数 ×1 · 53 版本漂移 ×2 | **整体撤回 + 呈报裁决**(§3) |
| 二次(改裁决后) | `7246 / 7208 / 1 / 37` | `test_realSource_signsMatchGddVocabulary_perRow` —— 另一张逐行期望表也要补 `TOX_ENVENOMATION` | 补表;**失败的是「漏补第二张表」,不是判据错** |
| 三次(最终) | `7246 / 7209 / 0 / 37` | — | — |

⚠️ **二次那轮值得记**:同一个测试文件里有**两张**逐行期望表(handle 一张、体征词表一张),
我只补了第一张。这正是「行数变了 ⇒ 本表须同步复核」这条断言存在的原因 ——
它把「漏补第二张表」变成了可见的红,而不是让新行悄悄逃过体征校验。

### 7.2 一处仅注释的后续编辑

最终 EditMode 跑完后,订正了 `disease_registry_bake_pipeline_test.cs` 里新加的一段注释
(原写「按通道位序排列」,实际是「按 `appear_at` 升序」)。**纯注释、零可执行变化**;
为免「跑完再改」的嫌疑,另跑了该 fixture 的过滤回归:
`32 / 32 / 0 / 0 / 0` @ `2026-11-09T12:49:28Z`(`DaYiJingCheng.Tests.Unit.Sim.DiseaseRegistryBakePipelineTest`)。✔

### 7.3 复算

```bash
python3 -I -c "
import xml.etree.ElementTree as ET
for p in ('unity/Logs/editmode-t7d.xml','unity/Logs/playmode-t7c.xml'):
    a=ET.parse(p).getroot().attrib
    print(p, a['total'], a['passed'], a['failed'], a.get('inconclusive'), a['skipped'], a['end-time'])
"
# 期望:
#   editmode-t7d.xml 7246 7209 0 0 37 2026-11-09 12:46:34Z
#   playmode-t7c.xml 371  366  0 2 3  2026-11-09 12:48:37Z
```

---

## 8. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| **清创缝合 / 蛇伤处置的**执行体** | **不存在**。`EmergencyActionRow`(极性 / potency / 幅度门 / 保持 tick)与 4 的交互接线都没做;`EmergencyManualDriver` 今日仍只有 `BandageRow` ⇒ **病人会因技法解锁被投放,但玩家还做不出这两个动作**。不得读成「清创可用」 |
| 这两门技法的**解锁触发点**(奇遇) | 未接线。第三章 / 第四章奇遇本身仍是 P1a |
| 蛇毒行的数值裁定 | **未裁** —— §4 全行标注「派生值」 |
| 「预防」极性 | **待裁** —— §2.2 |
| 毛地黄 / 金鸡纳两段奇遇 | **未落** —— map §3.2 仍草案,§9「用户确认位置」未结;文案归内容轮 |
| `disease_names.json` 补「蛇毒」 | **刻意不做** —— 该文件是 48 的病名闭合词表,转录自 GDD §R3(**病种**枚举);`TOX_ENVENOMATION` 属 R2。`LexiconRegistryCrossRefGate` 的 C2 明写「注册表有、词表没有」合法 |
