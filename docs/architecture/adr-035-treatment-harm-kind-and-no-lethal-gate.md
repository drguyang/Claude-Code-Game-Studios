# ADR-035: 用药伤害的 Kind 归属与「永不致死」机械门(禁忌 / 过量代价表的形状)

## Status

✅ **Accepted(2026-10-29)**

**本件只钉形状,不实现。** 表 / Kind / 门 / 夹具一律未落盘 —— §Validation V-1…V-8 **全部未跑(禁借绿)**。
Accepted 表示「方向已裁」,不表示任何判据已通过。

> ⚠️ **本件不解除 `TD-medcons-15` 的「真跑」阻塞。** 见 §Risks R1:禁忌支的材料(11 的载荷缺「命中禁忌」)
> 与过量支的材料(9 的 F1 缺毒性项)都还堵在上游,本件只把**形式**定死,使将来登记 / 实现不再反复改架构。
>
> 🔁 **2026-10-29 窄修订(用户裁定「收窄 R1」· 原文不删)**:上句原写「实现阻塞」,读起来是**一律卡死**,
> 这比实际依赖**严**。现按**三档**重述:
> **T1 形状件** —— 不被任何上游债阻塞(空 `rows` 即惰性:零数值、零材料,门 / 视图 / Kind / 证伪都只依赖**规则**);
> **T2 禁忌支真跑** —— 前须 `TD-prescription-09` 结案(11 的载荷要有「命中禁忌」的材料);
> **T3 过量支真跑** —— 再加 `TD-sim-02`(9 的 F1 毒性项)。
> ⇒ 全文出现的「两条均结案」**只在 T3 成立**,**不再**是全轮开工门。

## Date

2026-10-29

## Last Verified

2026-10-29(所有 `file:line` 均当日实读;未跑任何构建 / 测试 / 烘焙 / kindgen)

## Decision Makers

- **用户**(四项裁定,2026-10-29,逐条见 §Decision 首段)
- 起草依据:三份并行专家意见(系统设计 / 设计理论 / 技术架构),各持不同侧面,
  其分歧点与采纳理由见 §Alternatives Considered。三份原件 = `production/qa/evidence/review-tdmedcons-15-2026-10-29.md`

## Summary

`OQ-53-8` 已裁(2026-09-19):药物**禁忌 / 过量代价 P0 开,限定「伤 / 后遗」,永不作死亡代价**。
但 story-003 的 `AC-5` **只兑现了枚举域半边**(结局轴 / `NoEcho` 作 key ⇒ schema 错),
**代价表本身从未存在** —— 登记为 `TD-medcons-15`。

本件裁四件事:
1. **四层所有权拆分** —— 词表归 9 · 触发条件面归 11 · 表本体归 53 · 结算执行归 53;
2. **表的形状** —— 效果一律**外键引用** 9 的伤情词表,53 表内**零伤害属性副本、零后果数值**;
3. **「永不致死」= 构建期跨表机械断言**,落在 ADR-014 阶段 2 binder,**不新开门、不归设计纪律**;
4. **新增病史流 `Kind` `TreatmentHarmOnset`(写者 = 53)** —— 这**推翻** 53 现存的
   「只写世界流」约束(`ConsequenceEmitter.cs:93` 的卡面 Forbidden),故须本件立裁,不得由 GDD 修订偷偷做。

## Engine Compatibility

**不涉及任何引擎 API。** 纯 C# 接口契约、整数数据边界与构建期断言。
`Unity` 版本无关;`Sim` 程序集引用集一字不改(门 A / b2 门 / ADR-025 §① 全部继续成立)。
Engine Knowledge Risk **LOW**。

## ADR Dependencies

| ADR | 本件用到 | 状态 |
|---|---|---|
| **ADR-014** | §二 两阶段工具链(**阶段 2 = 全部构建期校验的唯一执行体**)· §五 `data-core` / `ConfigVersion` / ordinal append-only | Accepted |
| **ADR-024** | Kind 单一真源(`entities.yaml` 三字段)+ 断言 A1–A5 + 枚举**追加区**纪律 | Accepted |
| **ADR-010** | §七 `SaveVersion` / `ConfigVersion` 分离;ordinal 门的分工 | Accepted |
| **ADR-009** | 三问判据 / 三态分类 / 域归属表 / §六 有界性 | Accepted |
| **ADR-005 / ADR-006** | 整数定点域、零浮点、构造器边界 | Accepted |
| **ADR-025** | 程序集清单:`CostTable53` 住 `Sim`;Binder / CookedWriter 住 `Editor.Tools` 族 | Accepted |
| **ADR-029 / ADR-031** | 9 侧要读新载荷时的唯一编码 / 解码路径 | Accepted |
| **ADR-030** | **驳回「复用语义相邻 Kind」的判法先例**(Alt 2) | Accepted |
| **ADR-032** | `PatientOutcomeChanged` 是吸收态 / 逐病种终态 —— 本件**明确不复用**它的理由 | Accepted |

**本件不依赖任何未 Accepted 的 ADR。**

## Context

### Problem Statement

`TD-medcons-15`(`docs/tech-debt-register.md:1252`)原文:

> **W-4**:`AC-5` / `OQ-53-8`「禁忌 / 过量代价表**永不致死**形状门」只兑现枚举域部分
> (结局轴 / `NoEcho` 作 key ⇒ schema 错);**代价表本身未建**

不建的后果不是「少一张数据表」,而是 `AC-5` 的 Then 句「致死行在 P0 表 = 构建期拒收」
**没有宾语** —— 判据永远无法执行,也无法证伪。

### Current State(逐条可复跑)

| # | 事实 | 复算 |
|---|---|---|
| C-1 | 53 只有两张作者态表,无代价表 | `ls assets/data/ \| grep 53_` ⇒ 只有 `53_delay_table.json` / `53_outcome_table.json` |
| C-2 | 53 的写者面被卡面明文限制为**只写世界流** | `ConsequenceEmitter.cs:93`「只写 `ConsequenceResolved`(世界流;53 不写病史流 / 病例流 —— 卡面 Forbidden)」 |
| C-3 | 结局轴不可达(类型层已封闭) | `Outcome53.cs:239` 表构造器对 `PatientLives` / `PatientLoses` 抛 `ArgumentException`;夹具 `medcons_outcome_judge_test.cs:441` |
| C-4 | **9 的 F1 无毒性 / 副作用项**;超量被饱和截断 | `disease-simulation.md:788-791` 四项 = Base/Relapse + 共病加成 + 药物项(clamp)+ 战伤项;`:877` 校验 15「超 |界| ⇒ 饱和截断,**不拒收**」 |
| C-5 | **11 的载荷不带「是否命中禁忌」**;剂量只以**效应**形式在(`drug_potency` = `dose_potency`,剂量修正后的值),**不带原始档位** | `prescription-and-medication.md:968` `AC-11-03` 载荷七项 `{tick, 处置_id, 施予者, polarity, drug_potency, half_life, Seq}`;`:197` `drug_potency` 的释义;`HistoryPayloads.cs:227-241` 结构逐字段核对 |
| C-5′ | ⚠️ **「缺剂量」这句的边界**:11 的 P0 **不允许超量** —— `dose_range.hi` = 戥子物理限位(`prescription-and-medication.md:318-323`)⇒ 11 的 P0 面**本身不产生「超了多少」这个值**。「超量」若要有材料,须来自 **9 侧**(累积 / 毒性状态,`TD-sim-02`),不是给本载荷补 `dose` 字段 | 同上 |
| C-6 | `INJ_COMA` 明文「**不是死亡**」 | `disease-simulation.md:376-379` |
| C-7 | 9 是伤情 / 病名词表的**唯一**所有者 | `disease-simulation.md:670-674`「任何别的系统**不得自建病种表或伤情表**」 |
| C-8 | 现存 Kind **37 支**(15 病史 / 5 病例 / 17 世界) | `grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml` ⇒ 37;`StreamRouting.g.cs:13` 自陈「15 history / 5 case / 17 world = 37 支」 |
| C-9 | codec 的非法 Kind 报文**硬编码「37」** | `SimEventCodec.cs:84` `$"SimEvent.Kind={kindRaw} 不在 37 支闭集内"` |

### Constraints

- **`AC-53-01`**:`Outcome` 是**枚举**,53 **零后果数值字段**。
- **`AC-11-02`**:11 不得在自己的表里重定义 `contraindications` / 剂量 ⇒ 反推:53 也不得重建一份。
- **`OQ-11-5`**(已裁):11 **不拦不扣**;代价**必须在 53 结算**。
- **ADR-024 §③**:ADR-009 的 `Amendment 追加通道` **已退役**(「它正是幽灵引据的生产线」)⇒ 新 Kind 只能直登 registry + 本件。
- **ADR-024 §①**:`EventKind` 是**隐式值**枚举,新成员**只能追加在末尾**。插入中间 ⇒ 其后全部成员值 +1 ⇒ 历史存档静默读错 Kind。
- **实证后果**(不是设计担忧):`SimEventCodec.cs:39` 以 `(int)e.Kind` 落盘,`:83` 解码只做 `Enum.IsDefined` ⇒ 值位移后**仍然通过校验**,不抛。

### Requirements

1. 「永不致死」须是**机械可证伪**的保证,不能是注释 / 约定 / 运行期 clamp。
2. 53 不得持有伤情的属性副本,也不得持有后果分值。
3. 新增的 Kind 须满足 `ADR-024` A1–A5(唯一流别 / 载荷 ∈ 整数域 / 无重名 / author 必填 / 双向差集归零)。
4. 有界性须给出,且**与 tick 频率无关**(承 ADR-008 §六 形态)。

## Decision

**用户 2026-10-29 四项裁定(逐条照准)**:
1. 本轮**立 ADR 钉形状,不做实现**;
2. **「伤」支开**,新 Kind `TreatmentHarmOnset`;
3. **过量支挂起** —— 立两条上游债(9 的 F1 毒性项 / 11 的载荷),等它们;
4. **`INJ_COMA` 不排除,昏迷合法**(⚠️ 与两份专家意见相反,残余风险见 §Risks R3)。

### ① 四层所有权拆分

「这张表归谁」混读必错。四层各有所有者,判法承 ADR-021「容器即所有者」+ ADR-024「写者 = 所有者」:

| 层 | 是什么 | 所有者 | 判据 |
|---|---|---|---|
| **A. 伤情 / 病名词表** | 可以被「伤」指向的 id 集 | **9** | C-7 |
| **B. 触发条件面** | 哪些药 × 哪些禁忌 / 过量条件成立 | **11** | `OQ-11-5`;11 的 `drug_profile.contraindications` |
| **C. 表本体** | 行集 / 键域 / 效果选择 | **53** | 与 `53_delay_table` / `53_outcome_table` 同族 |
| **D. 结算执行** | 运行时判「这次算不算违禁」并施加 | **53** | `OQ-11-5` 已裁,不重开 |

**关键区分(本件显式钉住)**:**表的所有权(C)与结算的执行方(D)是两条独立结论,不互相推出** ——
今天恰好同属 53,不构成「凡表归 53 则执行必归 53」的通则。
**25 不是所有者**:它只是战斗路径的伤情**施加方**(`InjuryOnset` 写者);本路径无武器、无攻击者,25 不参与。

### ② 表的形状 —— 效果一律外键引用,53 零数值

**文件**:`assets/data/53_cost_table.json`(`[system]_[name].json`,与 `53_delay_table.json` 并列)。

```jsonc
{
  "schema_version": 1,
  "harm_kind_enabled": ["contraindication"],   // 枚举闭集;P0 只允许 contraindication(见 ⑥)
  "bands": { "contraindication": null },       // 档数,值归内容轮
  "max_severity_band": null,                   // 值归内容轮;0 = 只有整件伤、无程度档(合法)
  "rows": []                                   // 空 = 惰性,行为与今日逐位一致
}
```

行形态:

```jsonc
{
  "harm_kind": "contraindication",     // 枚举闭集
  "band": "<int>",                     // 档序数,(harm_kind, band) 单射
  "injury_id": "<INJ_* 符号>",         // **外键 → 9 注册表闭集**,非内联属性
  "severity_band": "<int>"             // 引用 9 域的伤情档位,**不是后果分值**
}
```

**三条硬性形状约束(构建期拒收,非纪律)**:
- **`injury_id` 必须外键** —— 表内**零** `lethal` / `magnitude` / 任何伤害属性副本。
  带副本 = 53 自建了第二张伤情表 ⇒ 直接违 C-7。
- **全整数域,本表零 `Fix` 字段** —— 程度用**整数档序数**,守 `AC-53-01`。
- **空 `rows` 合法且必须显式声明** —— `harm_kind_enabled: []` 表示「机制建了、内容未启用」;
  **`rows` 非空而 `harm_kind_enabled` 为空 ⇒ 构建期拒收**(防「静默空表 = 有门的假绿」)。

### ③ 「永不致死」= ADR-014 阶段 2 的跨表机械断言

**分三层,缺一即漏:**

| 层 | 内容 | 今日状态 |
|---|---|---|
| **L1 类型面** | 53 的出口类型**不表达死亡**:`Outcome` 的结局轴成员不进表(C-3) | ✅ 已有,夹具 `medcons_outcome_judge_test.cs:441` |
| **L2 表面(本件唯一新增点)** | 阶段 2 binder 的**跨表断言**:每行 `injury_id` ∈ 9 注册表闭集 **且** 该行 `lethal == false` | ❌ 待建 |
| **L3 运行时纵深防御** | `CostTable53.Table` 构造器复述同一拒收(照 `DelayTable53.cs` 的「构建期主校验 + Sim 侧纵深」双点形态) | ❌ 待建 |

**三条断言(编号沿用专家稿;N-2 已由用户裁定否决,故不存在)**:

| # | 断言 | 违反 ⇒ |
|---|---|---|
| **N-1** | `rows[].injury_id` 所指的注册表行 `lethal == false` | 构建期 `throw` |
| **N-3** | `injury_id` 不得是结局轴成员(符号级) | 构建期 `throw` |
| **N-4** | 白名单成员不得经 `compounds[]` 通向 `lethal == true` 的 `target_id`(防「非致命伤 → 复合 → 致命病」绕行) | 构建期 `throw` |

> ⚠️ **N-4 的必要性今日未证**:`compounds[]` 是否真能把非致命伤引向致命病,尚未普查。
> 实现轮须**先普查**;若确无此形态,N-4 应登记为「零输入可达的门」(本项目已抓过的假绿形态之一),
> 而不是当成已兑现的守卫。**禁借绿。**

**为什么不落在别处**:
- **不是 ADR-024 A 系**:A2 只查**字段类型 ∈ 整数域**,不查值域 / 语义 ⇒ 结构上表达不了「no lethal」。
- **不是新开门**:ADR-014 阶段 2 已是「全部构建期校验的**唯一执行体**」;再开一道 = 同一拒收逻辑两处实现 ⇒ 必然漂移。
- **不是设计纪律**:纪律不可证伪、不可复算。本项目已五次抓过「自述顶替门」的假绿。
- **不是运行期 clamp**:clamp 允许表里装着致死行,正确性依赖每次执行都对。

**证伪判据(实现轮的验收形态)**:把某行 `injury_id` 改指一个 `lethal == true` 的注册表行 ⇒
构建 **Failure** 且报文点名该行。**仅「构建绿」不构成证明**(门链无错时零输出,无法区分「门跑了」与「门没被调用」)。

### ④ 新增 `SimEvent.Kind.TreatmentHarmOnset`(病史流 · 写者 = 53)

**只覆盖「伤」支。** 「后遗」支**不需要**新 Kind —— 它复用既有 `Outcome.Residual`
(`53_outcome_table.json` 今天已在产该值);本件**不为同一概念造第二载体**。

**载荷(全整数域,A2 绿)**:

| 字段 | 类型 | 说明 |
|---|---|---|
| `tick` | i64 | 事件头(`SimEvent.Tick`),重复存储仅因 registry 要求载荷自述 |
| `patient_id` | i32 | 受药者 |
| `injury_id` | i32 | **外键 → 9 伤情注册表闭集** |
| `harm_kind` | i32 枚举 | `Contraindication \| Overdose` |
| `severity_band` | i32 | **引用 9 域的档位**,不是后果分值 |
| `seq` | i64 | 主机发号(ADR-005) |

**为什么用 `severity_band` 而不是 `magnitude`**:
9 的 trauma 累积读 onset 载荷的 `magnitude`(`disease-simulation.md:829`)。若本支直接给 `magnitude`,
53 就持有了**后果分值** ⇒ 违 `AC-53-01`。改法:**分值住在 9 的词表里**(`band → magnitude` 是 9 的数据),
53 只持**档位引用**。⇒ 9 侧消费本 Kind 时须自己查表换算(见 §Implementation Guidelines 的 9 侧义务)。

> 这一条是本件的**一处新裁**(既非 GDD 原文、也非任何既有 ADR 的推论):**「引用型档位 id」不算 `AC-53-01` 的
> 「后果数值字段」** —— 它不是后果的分值,是 9 域内的伤情档位。不成立则本件 ④ 整体不成立。

**为什么不复用三支既有 Kind**(逐条,承 ADR-030 的驳回判法):

| 候选 | 驳回理由 |
|---|---|
| `InjuryOnset` | 载荷 `{actor_id, target_id, injury_id, magnitude, dose_seq}` 语义 = **被谁打的**;药害无攻击者。**与 ADR-030 驳回 Alt 2 的判法逐字同型**(「会把病人出现伪装成有人打了他」) |
| `DrugTreatmentApplied` | 它是**因**不是**果**;author = 11,且已被 9 消费为 `polarity/potency/half_life` 三量。塞伤害污染 11 的写入面(11 明裁「不拦不扣」) |
| `PatientOutcomeChanged` | 是**吸收态 / 逐病种终态**(ADR-032,2026-10-24 收紧为「每个病种皆终态」才可折叠)。伤情 onset 非吸收态,塞进去 ⇒ `Folded(p)` 误判 ⇒ **误折不可逆** |

### ⑤ 53 的写者面扩张(显式新裁,推翻现存卡面禁止)

53 今日的写者面被**卡面 Forbidden** 限制为「只写世界流」(C-2)。本件**显式推翻**这一条:

> **53 的写者面 = 世界流(`ConsequenceResolved`)+ 病史流恰一支(`TreatmentHarmOnset`)。**
> 不写病例流;不得再扩第三支(扩 = 须另开 ADR)。

**同批必须改的文本**(否则代码与卡面自相矛盾):
- `ConsequenceEmitter.cs:93` 的 `remarks` 段;
- `production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md` 的 Control Manifest 段(`:30` 附近)。

**为什么不走 ADR-009 的 Amendment 追加通道**:ADR-024 §③ 已退役该通道。本件即是它的替代品。

### ⑥ 过量支挂起(用户裁定 ③)

`harm_kind` 枚举**保留** `Overdose` 成员(将来只追加一行数据,不动结构),但:

- `harm_kind_enabled` 在 P0 **只允许 `["contraindication"]`**;含 `overdose` ⇒ **构建期拒收**;
- 解除条件 = `TD-sim-02`(9 的 F1 缺毒性项)与 `TD-prescription-09`(11 载荷缺「命中禁忌」;剂量口径见 C-5′)
  **两条均结案** —— 本条解除的是**过量支能否真跑(T3)**,**不是**本件形状件能否开建(T1;
  三档口径见 §Risks R1,2026-10-29 收窄)。

**依据(实测,非估计)**:过量在今天**没有材料可结算** —— 9 侧 F1 无毒性项且超量被饱和截断(C-4),
11 侧载荷无「命中禁忌」且 P0 形态不产生超量值(C-5 / C-5′)。建了也只是把「没材料」变成「有表但没材料」。

### ⑦ 本件不实现

表 / Binder / CookedWriter / `CostTable53` / 新 Kind 登记 / kindgen 重生成 / 夹具 **一律未落盘**。
实现轮见 §Migration Plan。

## Architecture

```text
9 疾病与伤情模拟(伤情 / 病名词表唯一所有者)
   └─ 注册表: INJ_* × { lethal, ... }              ← 全部伤情语义住这里
        ▲ 外键引用(只引用,不复制)
        │
53 医疗后果与责任
   ├─ assets/data/53_cost_table.json                ← 本件定义的形状(行 = harm_kind × band)
   ├─ Editor.Tools.Bake/CostTableBinder.cs          ← 阶段 2: N-1 / N-3 / N-4 跨表断言
   ├─ Sim/Consequences/CostTable53.cs               ← 只读视图 + 构造器纵深防御(L3)
   └─ 写者: ConsequenceResolved(世界流) + TreatmentHarmOnset(病史流)
                 ▲ 材料
11 处方用药(不拦不扣;只出材料)
   └─ contraindicatoins 判定结果 / 剂量 —— ⚠️ 今日载荷里没有(见 R1)
```

## Implementation Guidelines

**实现轮须按此顺序,顺序不可换:**

1. **先普查 `compounds[]`**(N-4 的必要性);确认后再写绑定器。
2. **本件 ⑤ 的两处文本同批改**(代码 remarks + story 卡面 Forbidden),不要留到「顺手改」。
3. **新 Kind 一律追加在 `EventKind` 枚举末尾**(ADR-024);同批改 `SimEventCodec.cs:84` 的「37 支」字面串
   —— 否则新增后合法 Kind 会被报成「不在闭集内」,是**误导性诊断**。
   ⚠️ **该字面量共有 8 处**(7 手写 + 1 生成物),且 `drug_event_test.cs:658` **拿它当探针键** ——
   只改一处会让那条白名单完整性夹具**静默退化为恒绿**。全貌与修法方向见债册 `TD-persist-11`;
   本件的 S5 **只覆盖一次发生**,不覆盖这一类。
4. **kindgen 须实跑并贴输出**;判据 = registry active 数 = `StreamRouting` 自陈数(38 = 16 + 5 + 17),
   不接受手改生成物。
5. **9 侧消费义务**:`TreatmentHarmOnset` 到达时,9 须把 `severity_band` 经 `band → magnitude` 换算后
   并入 D_p。**换算表归 9**,不进 53。

## Key Interfaces

```csharp
// Sim/Consequences/CostTable53.cs —— 只读视图(形态照 DelayTable53)
public sealed class Table
{
    public bool IsEmpty { get; }
    public bool TryLookup(int harmKind, int band, out int injuryId, out int severityBand);
    // 构造器拒收:结局轴符号 / injury_id 不在 9 闭集 / 非单射 —— 与 binder 同判据(纵深防御)
}
```

新增 Kind:`SimEvent.Kind.TreatmentHarmOnset`(病史流 · author = 53)。

## Alternatives Considered

### Alternative 1:复用 `InjuryOnset`(代价 = 一条伤)

**驳回。** 载荷语义 = 「被谁打的」,药害无攻击者;与 ADR-030 驳回 Alt 2 同型。
另:专家稿曾建议「物化归 25」,同样驳回 —— 25 只是战斗路径的伤情**施加方**,
伤情词表归 9(C-7),让 25 持「档 → magnitude」表 = 给 9 的词表造第二真源。

### Alternative 2:只走「后遗」支,伤支推 P1a

**考虑过,否决。** 后遗复用 `Residual` 确实零新 Kind、零新 ADR(**本件 ④ 保留该支正是为此**);
但只做后遗 ⇒ `OQ-53-8` 的「伤」半边永不落地,`AC-5` 永远 PARTIAL。
用户裁定取「开伤支」:宁可现在付一份 ADR 的代价。

### Alternative 3:把 `TreatmentHarmOnset` 的写入放世界流(避免动 53 写者面)

**驳回。** 伤情真源是病史流的 onset 事件(`disease-simulation.md:712`);若让世界流承载,
9 的 D_p 就要**从两条流重建** ⇒ `Folded(p)`、CatchUp、存档折叠全部要改。
P1a 引入 `RegionOutcome` 时还要再动一次同一接缝。**这是最便宜的做法,也是最贵的错误。**

### Alternative 4:「永不致死」靠运行期 clamp / 设计纪律

**驳回。** clamp 允许表里装着致死行;纪律不可证伪。本项目已五次抓过「自述顶替门」的假绿。

### Alternative 5:把 N-1 挂进 ADR-024 的 A 系断言

**驳回。** A2 只查字段类型 ∈ 整数域,不查值域语义;且 A 系由 kindgen 生成物承载,
与「跨 9 注册表比对」是不同性质的工作。

## Consequences

### Positive

- `AC-5` 的 Then 句**首次取得宾语**(「致死行在 P0 表 = 构建期拒收」有了可指的表)。
- 53 的写者面扩张被**显式记录并限定**,而不是被某次实现顺手扩掉。
- 效果一律外键 ⇒ P1a 加新效果种类只动数据,不动架构。

### Negative

- **多一份 ADR 与一个 Kind 的长期成本**(registry / kindgen / codec 报文 / 域归属表 / 有界性,五处同步)。
- 表建了但**在两条上游债结案前没有任何真实行可跑** —— 存在「建了但空转」的窗口。

### Neutral

- 25 不参与本路径,不改 25 的任何既有裁决。

## Risks

| # | 风险 | 处置 |
|---|---|---|
| **R1** | **本件不解除「真跑」阻塞**(2026-10-29 收窄为三档)。**T1 形状件**:不受任何上游债阻塞 —— 空 `rows` 是惰性形态(S2),门与视图(S3/S4)、Kind 与 kindgen(S5)、文本(S6)、证伪(S7)**只依赖规则,不依赖材料**。**T2 禁忌支真跑**:须 `TD-prescription-09` 结案。**T3 过量支真跑**:再加 `TD-sim-02`。⚠️ **「ADR Accepted」仍 ≠「判据已过」** —— V-1…V-8 一条未跑 | 登记 `TD-prescription-09` / `TD-sim-02`;**T2 前**须 `-prescription-09` 结案;**T3 前**两条均结案。~~实现轮开工前须两条均结案~~(原文留档,已被本行取代) |
| **R2** | N-4 可能零输入可达(若 `compounds[]` 无此形态)⇒ 假绿 | 实现轮先普查;确无则登记为「零输入可达的门」,不记为已兑现守卫 |
| **R3** | **`INJ_COMA` 合法(用户 2026-10-29 裁定)** ⇒ 白名单可含昏迷。专家稿明确指出:**「一剂药把病人治到昏迷」在玩家感知上与「已经走了」等价**(`AC-53-09` 的走查判据是「后果在病人身上**可见**」,不是「病人还能说话」) | **本件不推翻该裁定**,只登记残余风险与其缓解义务:见下方 **O-1** |
| **R4** | 空表窗口内若有人把 `rows` 填了而忘了 `harm_kind_enabled`,`TryLookup` 恒 false ⇒ 静默跳过(与 W-4 完全同型) | §Decision ② 已把该形态列为**构建期拒收**;实现轮须有夹具 |

### O-1(由 R3 派生的义务,非新债)

**9 侧须为药源性昏迷给出时长上界或状态机出口约束**(例如「药源性增量不得把病人推过 `CRITICAL_THRESHOLD`」
或「豁免类伤情的状态机不得有 ≥ 危殆 的死亡出边」)。
⚠️ **今日未核实 9 是否已有此约束** —— 专家稿明确把它列为「未及核查」。
**在本条落实前,不得声称「永不作死亡代价」已完整成立** —— 它只覆盖「直接致死」,未覆盖「长期昏迷」。
归属:9 的数值 / 状态机轮。

## Performance Implications

无。本表只增加一次构建期跨表比对与 O(1) 运行期查表;不触及每帧路径。

## Migration Plan

**全部未落盘。** 实现轮的最小切片(顺序不可换)。

**分档(2026-10-29 收窄,判据见 §Risks R1)**:<br>
**T1 = S1–S7**(形状件 —— **零上游债、零数值**;S1 的普查是 N-4 必要性的前置,不是材料依赖)<br>
**T2 = 填禁忌行并让它真跑**(须 `TD-prescription-09` 结案 + 内容轮给行内容 / 档数)<br>
**T3 = 启用过量档**(`harm_kind_enabled` 允许 `overdose`;再加 `TD-sim-02`)

| # | 工作 | 验收判据 |
|---|---|---|
| S1 | 普查 `compounds[]` | 有/无形态各有结论;无 ⇒ R2 按「零输入可达」登记 |
| S2 | `53_cost_table.json` 骨架(空 `rows`) | 惰性:既有回归零变动 |
| S3 | `CostTableBinder`(N-1 / N-3 / N-4 + 单射 + 未知字段拒收 + `rows`/`harm_kind_enabled` 一致性) | 每条拒收 ≥1 夹具 + ≥3 条反例(防判据过宽) |
| S4 | `CostTable53` 只读视图 + 构造器纵深防御 | 绕过 Bake 直构表亦拒 |
| S5 | 新 Kind 三字段登记 + kindgen 重生成 + 枚举末尾追加 + `SimEventCodec.cs:84` 串同步 | A1–A5 全绿;37 → **38**(16 history);贴重跑输出 |
| S6 | 改 §Decision ⑤ 的两处文本 | 代码 remarks 与卡面 Forbidden 不再自相矛盾 |
| S7 | **证伪轮** | M1 注入 `lethal=true` 行 ⇒ binder throw;M2 `injury_id` 指向致死行 ⇒ throw;M3 新 Kind 插中间 ⇒ 旧存档解码错(可做静态论证 + 一条夹具);**SHA-256 逐字节还原** |
| S8 | `AC-5` 表级半边改判 | **仅在 S3 + S7 落盘后**才可把 PARTIAL 收窄;S5 前翻 = 门无宾语 |

**必须等数值轮**:行键集合 · 档数 · `max_severity_band` · 实际选用的 `injury_id` · 是否复用 `DelayTable` 的延迟
(**⚠️ 若代价走 `ConsequenceResolved` 的延迟路径,须同时给 `CostInjury` 加一行 `delay_ticks`,
否则 `TryDelayTicks` false ⇒ 又一次静默跳过,与 W-4 同型**)。

## Validation Criteria

> ⚠️ **V-1…V-8 全部未跑。Accepted 表示方向已裁,不表示任何判据已通过。禁借绿。**

- [ ] **V-1** registry 条目三字段齐(`stream: history` / `author` / `payload_schema`),A1–A5 全绿
- [ ] **V-2** kindgen 重生成后 `StreamRouting` 自陈 38 支(16 / 5 / 17);`grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml` ⇒ 38
- [ ] **V-3** `SimEventCodec.cs` 的闭集数串与新总数一致(改前 37)
- [ ] **V-4** 新成员位于枚举**末尾**;`git diff` 显示为零插入(旧成员值零位移)
- [ ] **V-5** N-1 变异(注入 `lethal=true` 行)⇒ 构建 Failure 且报文点名该行
- [ ] **V-6** `rows` 非空而 `harm_kind_enabled` 为空 ⇒ 构建拒收
- [ ] **V-7** `harm_kind_enabled` 含 `overdose` ⇒ 构建拒收(过量支挂起)
- [ ] **V-8** §Decision ⑤ 两处文本已改,`grep` 不再出现「只写 `ConsequenceResolved`(世界流;53 不写病史流」的原句

## GDD Requirements Addressed

| 需求 | 状态 |
|---|---|
| `AC-5`(story-003)/ `OQ-53-8` 的「永不致死形状门」 | **本件给出门的形状与宾语**;执行仍未落(见 V-5…V-7) |
| `AC-53-01`(`Outcome` 枚举 / 零后果数值字段) | 由 §Decision ② 的「全整数域 + 零 `Fix` + 外键」维持;`severity_band` 口径见 ④ 的**新裁** |
| `AC-11-02`(11 不重定义 `contraindications`) | §Decision ①B 把触发条件面判给 11,53 不重建 |
| `OQ-11-5`(11 不拦不扣;代价归 53) | §Decision ①D 承之,不重开 |

## Related

- **上游债**:`TD-sim-02`(9 的 F1 缺毒性项)· `TD-prescription-09`(11 载荷缺剂量 / 禁忌命中)· `TD-persist-11`(codec 硬编码闭集数)
- **本件所结**:`TD-medcons-15` 的**形状半边**(实现半边仍 open,见 R1)
- **专家意见原件**:`production/qa/evidence/review-tdmedcons-15-2026-10-29.md`(三份,含分歧与采纳理由)
- **先例**:ADR-030(驳回「复用语义相邻 Kind」的判法)· ADR-021(容器即所有者)· ADR-014 阶段 2(构建期校验唯一执行体)
