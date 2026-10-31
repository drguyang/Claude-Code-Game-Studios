# ADR-035: 用药伤害的 Kind 归属与「永不致死」机械门(禁忌 / 过量代价表的形状)

## Status

✅ **Accepted(2026-10-29)**

**本件只钉形状,不实现。** 表 / Kind / 门 / 夹具一律未落盘 —— §Validation V-1…V-10 **全部未跑(禁借绿)**。
⚠️ **2026-10-29 例外(唯一):S1 的普查已跑**(只读,未落任何盘)—— 见 §Risks R2 / R5。
🔁 **2026-10-29/30 状态推进(如实登记,覆盖上句的「未落盘」)**:T1 的 **S1 普查**(只读)· **S2 空表**
(`53_cost_table.json`)· **S3 绑定器 + 19 条夹具**(19/19 绿,证伪 3 组 6 变异)· **S4 只读视图 + 13 条夹具**
(13/13 绿,证伪 4 变异)· **S5 新 Kind 全链**(38 支 / 16·5·17,证伪 5 变异)· **S6 两处文本订正** ·
**S9 构建路径**(烘焙链 + 构建前门 + 探针轮五次构建)均已落。
⇒ 本件**已部分实现**;**S1–S9 全部已落(T1 形状件全数交齐)**;**V-1…V-10 全部已证**。
⚠️ 「全部已证」只到**形状 / 门 / 文本**这一层 —— **T2(禁忌支真跑)/ T3(过量支真跑)仍被上游债卡住**
(`TD-prescription-09` / `TD-sim-03` / `TD-sim-02`),`TD-medcons-15` 的实现半边仍 `open`。
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
>
> 🔁 **2026-10-31 窄修订(用户裁定「甲 + 属性判据」· sprint-21 `S21-10` · 结清 `TD-medcons-20`)**:
> 新增断言 **N-5**(代价表不得指向**终局型**伤情),并**为「药害为什么停在伤 / 后遗」补上正面理由**。
> 落点见 §Decision ③之三 · §Risks R3 · §Validation **V-11**。
> ⚠️ **本修订不重开 §Decision ①/②/④/⑤/⑥/⑦ 任何裁决**,也不改任何数值。

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
| **N-5** 🔁 | `rows[].injury_id` 所指的注册表行 **`terminal_state == false`**(不得是**终局型**伤情 —— 2026-10-31 新增,见 §③之三) | 构建期 `throw` |
| ~~**N-4′**~~ | 🔁 **2026-10-29 S1 普查后的实现形态**(见 §Risks R2):今日 `compounds` **在作者态与运行时 schema 两层都不存在** ⇒ N-4 的判据**无对象**。故 S3 实现的是 **canary**,不是原式的门:**注册表里一旦出现 `compounds` 字段(任一病种行),53 的 `CostTableBinder` 即 `throw`**,报文须写明「这是**到期复查提醒**,不是拒绝 —— 复查 N-4 在本轮真实数据上是否成立,成立则把 canary 换成原式 N-4」。**canary 有判别力**:它今天恒绿,但**第一次有人加 `compounds` 就必红** | 构建期 `throw`(带复查指引) |

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

### ③之三 N-5 —— 药害为什么停在「伤 / 后遗」,以及「不裁定状态终局」(2026-10-31 窄修订)

> **结清 `TD-medcons-20`。** 该债记的是:2026-10-29 那次订正废掉了 `OQ-53-8` 的**因果**
> (「致死 = 医者杀生获机制牙齿」),**结论还在、理由没有了**。
> 本节补的就是那条**能被复算的正面理由**,并把它机械化为 **N-5**。

#### 一 · 正面理由(结构判据)

**代价表不是伤害模型。**

- 53 的代价表是**离散查表**:`(harm_kind, band)` → 一行 → **一个已登记的伤情**。
  它**没有** HP 轴、**没有**连续量、**没有**伤害属性副本(§Decision ② 三条硬约束)。
- 25 之所以能走到昏迷,是因为它有一条**连续伤害轨**(HP 递减到 0),`INJ_COMA` 是那条轨的**终点**。
- 53 没有轨 ⇒ 它**只能诱发病理,不能裁定状态终局**。

⇒ 这不是「药效不够」,也不是「致死不真实」(`TD-medcons-20` 明令**不得**这么写),
而是**能力边界**:产生「终局」所需要的那条轨,不在 53 手里。

**与既有口径同源** —— GDD `medical-consequences.md:107-112` 早就写着
「53 的裁量只有回响轴,**不碰生死**」「**致死只走 9 的对因手柄门**」。
本节把它从「死亡」**扩到「终局状态(含昏迷)」**,并给出扩的理由:
**因为判据是「有没有那条轨」,而昏迷与死亡同属轨的终点。**

#### 二 · 机械化 = N-5(判据打在**行属性**上,不认名字)

| 项 | 内容 |
|---|---|
| 载体 | 9 的注册表行新增**必填**布尔 `terminal_state`(「终局型」= 病人被**送入**的状态,不是可挂载的病理) |
| 9 侧校验 | **R1-23**:`terminal_state == true` 仅允许 `kind == injury` |
| 53 侧断言 | **N-5**:`rows[].injury_id` 所指行的 `terminal_state` 必须为 `false` |
| 读取路径 | **侧信道**:该值**不进** `DiseaseRegistryEntry`、**不进** cooked、运行期不读(<br>`DiseaseRegistryBinder.BindResult.TerminalRowKeys` → `CostTableBinder.RegistryView`)。先例 = `treatable_by`「JSON 有、条目不携带」 |
| 为什么不按名字 | **名字判据**(`injury_id == "INJ_COMA"` 即拒)换名即漏;属性判据换名不漏 —— 换名也得先标 `terminal_state` |

#### 三 · 如实登记的两条局限(**不得**读成「已完整证明」)

1. **N-5 今日零输入可达。** 真实注册表 15 行 `terminal_state` **全为 `false`**
   —— 也就是说**今天生产数据上 N-5 不拦任何东西**。它与 `N-4′` 的 canary 同族:
   **它挡的是「下一个把 `INJ_COMA` 之类终局状态登记进来的人」**,不是当下的某一行。
   ⇒ 读到绿灯时**不得**记作「药害不造成昏迷已被数据证明」,只能记作「**门已就位、无输入**」。
2. **属性是作者自陈的。** 新登记一件终局型伤情的人,若把 `terminal_state` 写成 `false`,
   N-5 不会红。⇒ 它把「忘记想这件事」变成了**必须显式回答的一个问题**(缺键即硬失败),
   但**不能替代**「答对了」。这一点在 §Risks R3 同批登记。

#### 四 · 本修订**不覆盖**什么

**间接路径照旧存在**:`INJ_DRUG_HARM`(severity 4)仍可能把病人往危殆推,
最终由 9 的病程结算成 `INJ_COMA`。那是**经由伤情轨**发生的,不是代价表直接指过去。
⇒ **O-1(9 侧须给药源性昏迷时长上界或状态机出口约束)不因本修订而消解**,
不得拿 N-5 当它的替代。

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
| **R1** | **本件不解除「真跑」阻塞**(2026-10-29 收窄为三档)。**T1 形状件**:不受任何上游债阻塞 —— 空 `rows` 是惰性形态(S2),门与视图(S3/S4)、Kind 与 kindgen(S5)、文本(S6)、证伪(S7)**只依赖规则,不依赖材料**。**T2 禁忌支真跑**:须 `TD-prescription-09` 结案。**T3 过量支真跑**:再加 `TD-sim-02`。⚠️ **「ADR Accepted」仍 ≠「判据已过」** —— V-1…V-10 一条未跑 | 登记 `TD-prescription-09` / `TD-sim-02`;**T2 前**须 `-prescription-09` 结案;**T3 前**两条均结案。~~实现轮开工前须两条均结案~~(原文留档,已被本行取代) |
| **R2** | N-4 可能零输入可达(若 `compounds[]` 无此形态)⇒ 假绿 | **✅ 2026-10-29 已普查,结论 = 零输入可达,且是两层都空**:① `compounds` 在作者态 JSON **0 行**、`DiseaseRegistryEntry` **无此成员**(`grep -c "Compounds" RegistrySchema.cs` ⇒ 0)⇒ 连读都读不到;② R2.2 指定的「任何开放伤 → **感染**」,其 target 在 P0 八病种里**不存在** ⇒ 即便有 schema,校验 10 也拒收。**但设计意图确有该形态**(`disease-simulation.md:382` 说 `component_id` 可指伤情;`:430-431` 旗舰共病 = 非致死→致死的同型)⇒ **不是想多了**。**用户 2026-10-29 裁定:写成 canary**(见 §Decision ③ 的 N-4′):`compounds` 一出现即 `throw` 并指向本行。普查原件 = `production/qa/evidence/s1-compounds-census-2026-10-29.md` | 已在 S1 结(2026-10-29);canary 的落点在 S3,判据 = V-9 |
| **R3** | **`INJ_COMA` 合法(用户 2026-10-29 裁定)** ⇒ 白名单可含昏迷。专家稿明确指出:**「一剂药把病人治到昏迷」在玩家感知上与「已经走了」等价**(`AC-53-09` 的走查判据是「后果在病人身上**可见**」,不是「病人还能说话」) | **本件不推翻该裁定**,只登记残余风险与其缓解义务:见下方 **O-1** |
> 🔁 **2026-10-31(窄修订 · `S21-10`)**:上行的「残余风险」已**收窄一半** —— **代价表直接指向终局状态**这条路被 **N-5** 封死(§③之三)。
> ⚠️ **另一半仍开**:① **间接路径**(伤情 → 恶化 → 9 结算成昏迷)不在 N-5 面上,见 **O-1**;② **N-5 的属性是作者自陈的** —— 新登记终局型伤情却标 `false` 者不会被拦(只保证「必须显式回答」)。
| **R4** | 空表窗口内若有人把 `rows` 填了而忘了 `harm_kind_enabled`,`TryLookup` 恒 false ⇒ 静默跳过(与 W-4 完全同型) | §Decision ② 已把该形态列为**构建期拒收**;实现轮须有夹具 |
| **R5** | 🔁 **2026-10-29 S1 普查新揭 —— `N-1` 的可行集今日为空。「永不致死」会以「无行可填」的方式自动成立 = 真空真,不是门在工作**:注册表 **5 件伤情全部 `lethal = true`**,非致死行只有 3 个**病种**(`DIS_RHEUMATIC_FEVER` / `DIS_MEASLES` / `DIS_NEURASTHENIA`),而那三个被 **N-3** 排除 ⇒ 代价表的 `injury_id` **一列无可填** | 登记 **`TD-sim-03`**(owner = 9):**T2 的前置 = 注册表出现 ≥ 1 件 `lethal == false` 的伤情**(最自然落点 = `INJ_BLUNT`,GDD `:366` 标注「非致命路线的主力」,当前未建)。⚠️ **在 `-sim-03` 结案前,`N-1` 的绿灯一律按「无输入」读,不得记作「已证明药害不致死」**。普查原件同 §R2 |

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
🔁 **2026-10-30**:上句已不成立 —— **S1–S7 + S9 已落**,仅 **S8 未开工**(逐行看右列)。原文保留。
🔁 **2026-10-30 二次推进**:S7 已落(证伪轮 6 变异 / 9 红全部恰好);**仅 S8 未开工**,
且 S8 的前置条件(S3 + S7 落盘)**已满足** ⇒ `AC-5` 的表级半边现在**可以**改判了。

**分档(2026-10-29 收窄,判据见 §Risks R1)**:<br>
**T1 = S1–S7 + S9**(形状件 —— **零上游债、零数值**;S1 的普查是 N-4 必要性的前置,不是材料依赖。
**S9 于 2026-10-30 追加**,用户裁定「加 S9」—— 它兑现 V-5 字面上的「**构建** Failure」,见该行)<br>
**T2 = 填禁忌行并让它真跑**(须 `TD-prescription-09` 结案 **+ `TD-sim-03` 结案**(注册表须先有 ≥1 件 `lethal=false` 伤情,否则无行可填 ⇒ 真空真,见 §Risks R5)+ 内容轮给行内容 / 档数)<br>
**T3 = 启用过量档**(`harm_kind_enabled` 允许 `overdose`;再加 `TD-sim-02`)

> 🔁 **2026-10-30 T3 已落 —— §⑥ 的解除条件两条均已结案**(用户两项裁定:
> ① 9 侧走**独立状态量**、不进 `Progress`;② 「过量」按**同一味药**(`处置_id`)累积)。
> **9 侧**(结清 `TD-sim-02`):`ProgressionEvaluator.ComputePeakDrugLoad` ——
> `max_p Σ |drug_potency| × Decay`,纯函数、事件流产、**不进 F1 / 不进 trend / 不进存档**;
> 经 `VitalsDto.PeakDrugLoad` 输出(`disease-simulation.md` 的 F1 节已登记它与 F1 的分界)。
> **53 侧**:`OverdoseEvaluator`(定档纯函数,判据 = `load ≥ threshold[i]`,闭区间下界)
> + `OverdoseSettler`(**独立于** `ContraindicationSettler` —— 前者的驱动是「一条用药事件」,
> 后者是「一个 tick 的体内药量」,合并会让归因失效)+ 代价表新根键
> `overdose_band_thresholds`(升序绝对量,与 `drug_potency` 同量纲)。
> **取消两处挂起**:`CostTable53.RejectHarmKind` 的 `Overdose` 拒收分支、
> `CostTableBinder` 的 `harm_kind_enabled` 含 `overdose` 即拒 —— 改为「启用而无档位线 ⇒ 拒」。
> ⚠️ **判据链跑通 ≠ 实玩可撞见**:后者取决于 `drug_potency` / `half_life` 与阈值的关系,
> 数值轮已给值(`1` / `3/2`,约两剂 / 三剂)。

> 🔁 **2026-10-30 T2 全链已落(用户裁定「数据 + 53 结算 + 11 的禁忌判定」)** ——
> ① **代价表填入三行真实代价**(`bands.contraindication = 3` / `max_severity_band = 4`;
> 档 0 → `INJ_BLUNT`/2,档 1·2 → `INJ_DRUG_HARM`/3·4),产物 72 B;
> ② **`INJ_DRUG_HARM`**(药毒,id 16,自限型非致死)进注册表 —— ⚠️ 它是 **R2 枚举表之外**的第十二项,
> 词表收口须并入 `disease-simulation.md` 的 R2 表(归 9 的 GDD 轮);
> ③ **`bands` × `rows` 交叉校验**进绑定器(原 `bands` 是装饰字段);
> ④ **53 结算器** `Sim/Consequences/ContraindicationSettler.cs`(游标式、幂等、真表真查)+
> `CompositionRoot` 装配 + `BootRoot` 逐 tick 驱动;
> ⑤ **11 判定** `Sim.Contracts/ContraindicationRule.cs` 的 `ContraindicationEvaluator`
> (只读 `SignChannelMask` 整数位域,保 `AC-11-01` ②)+ 数据 `prescription_actions.json` 的
> `contraindication.rules[]`(经 ADR-014 烘焙,产物 127 B)+ `PrescribePorts.Vitals` 端口 +
> 装配层 `VitalsQueryOrNone`(无病程 ⇒ 零体征,不抛)。
> **判据形态 = 合取**(`(mask ∧ ChannelMask) == ChannelMask`)、**多命中取最高档**。
> Regression:EditMode `6059/6022/0/37` · PlayMode `238/234/0/2/2` @ 2026-10-30 · 原件
> `production/qa/evidence/t2-b-full-chain-2026-10-30.md`。
> ⚠️ **仍不可被玩家感知** —— `SignChannelMask` 在生产恒 0(9 的 F2 未接线)⇒ 判据恒不命中,
> 新立 **`TD-sim-04`**。**本条不因此补绿**:判据齐 ≠ 机制跑过。
>
> 🔁 **2026-10-30 当晚:F2 体征投影已接线(`TD-sim-04` closed)** ——
> 9 的 `signs[].appear_at`(Fix 字符串,**R1-37 两段判据**:域 `(0,1]` **且** `< A_peak/SCALE`)承载阈值,
> `ComputeVitals` 按 `position ≥ appear_at` 投影出 `SignChannelMask` / `SignCount`。
> 🔁 **2026-10-30 数值轮**:48 条阈值按**行相位派生**定稿(`appear_at = 相位分数 × 峰值`,四档 0.05/0.25/0.55/0.85),
> 消灭了旧「全局统一阈值」口径下的 **9 条死词条**(其中 `DIS_NEURASTHENIA` 全 4 条 ⇒ 掩码恒 0);
> 可达性判据即由此轮加入,**死词条从此构建期拒收**。
> ⚠️ **阈值原判「归 8」经实测不成立**(8 的 `diagnosis_signs.json` 无阈值字段),已在
> `disease-simulation.md` 的 `D-9-R` 行补注订正。端到端夹具
> `f2_sign_projection_test::test_realVitals_produceANonSentinelBand` 用**真** `DiseaseVitalsService`
> 跑出档 0(非哨兵)。Regression:EditMode `6063/6026/0/37` · PlayMode `238/234/0/2/2`
> @ 2026-10-30 10:05/10:01 CST。原件 `production/qa/evidence/t2-c-f2-sign-projection-2026-10-30.md`。
> ⚠️ **48 个阈值与各处曲线值全是草案** ⇒ 「药害在实际游玩中可被撞见」仍归数值轮。

> 🔁 **2026-10-30 进展(T2 前置轮 A,原文不删)** —— T2 的三条前置**各有进展、无一全部结清**:
> ① **`TD-sim-03` 已 `closed`** —— 用户裁定新增 `INJ_BLUNT`(自限型 / 非致死,id 15),
> 注册表非致死伤情 **0 件 → 1 件**;② **`TD-prescription-09` `open` → `partial`** ——
> `DrugTreatmentAppliedPayload` 追加**第八项** `ContraindicationBand`(`-1` = 未命中),
> **承载**已落,**判定**仍未实现(故字段今日恒 `-1`);③ `TD-sim-02` 未动。
> 同轮另落 53 代价表的**运行期读方**(`CostTable53CookedCodec` + provider 接线 + 12 条夹具),
> 即「表装进来没人要」这条已消。Regression:EditMode `6043/6006/0/37` · PlayMode `238/234/0/2/2`
> @ 2026-10-30 08:45/08:46 CST · `7ab13163`+工作树。原件
> `production/qa/evidence/t2-a-cost-read-side-and-11-payload-carrier-2026-10-30.md`。
> ⚠️ **T2 仍不可开工**:缺代价表的**真实行内容**(内容轮)+ 11 的**判定**。
> ⚠️ **一处须用户裁**:若将来的禁忌判定由**病种**派生,会与 `AC-11-01` ② 相抵,须先改该 AC。

| # | 工作 | 验收判据 |
|---|---|---|
| S1 | 普查 `compounds[]` | ✅ **2026-10-29 已跑** —— 结论 = **零输入可达(两层都空)**,形式 = **canary**(N-4′)。原件 `production/qa/evidence/s1-compounds-census-2026-10-29.md`;连带发现 **R5** |
| S2 | `53_cost_table.json` 骨架(空 `rows`) | ✅ **2026-10-29 已落** —— 内容严格照 §Decision ② 字面(`null` = 值归内容轮;不加 `_note`,与 53 族两张表一致)。**「惰性」是静态论证,不是回归数**:该文件**今日零读方**(`grep` 无命中)⇒ 无可执行路径可跑,未跑任何测试。原件 `production/qa/evidence/t1-s2-cost-table-skeleton-2026-10-29.md`。⚠️ 与 `data-files.md`「禁孤儿条目」有**有时限的偏离**,消解点 = S3 |
| S3 | `CostTableBinder`(N-1 / N-3 / **N-4′** + 单射 + 未知字段拒收 + `rows`/`harm_kind_enabled` 一致性) | ✅ **2026-10-30 已落** —— `CostTableBinder.cs` + **19 条夹具,19/19 绿**;证伪轮 **3 组 6 变异**,每组**恰好**红预期 2 条,SHA-256 逐字节还原。原件 `production/qa/evidence/t1-s3-cost-table-binder-2026-10-30.md`。⚠️ **V-5 仍未过**(本表无 CookedWriter / DataBakeMenu 入口 ⇒ 今天不存在「构建」路径,只在绑定器层证了 N-1);**连带实测订正 R5 / `TD-sim-03`**:R1-22 禁止**急性保持型**伤情非致死 ⇒ 合法落点须是自限型或平台型 |
| S4 | `CostTable53` 只读视图 + 构造器纵深防御 | ✅ **2026-10-30 已落** —— `CostTable53.cs`(Sim)+ `CostHarmKind`(Sim.Contracts,S5 的载荷同用)+ **13 条夹具,13/13 绿**;证伪 4 变异 ⇒ **恰好 4 红**。**L3 按「同一拒收」也查 `lethal`**(比 §Key Interfaces 草稿多一条,理由见证据件 §一)。原件 `production/qa/evidence/t1-s4-cost-table53-view-2026-10-30.md`。⚠️ **零读方**:无 CookedWriter / 菜单 / 装配点 |
| S5 | 新 Kind 三字段登记 + kindgen 重生成 + 枚举末尾追加 + `SimEventCodec.cs:84` 串同步 | ✅ **2026-10-30 已落** —— A1–A5 全过(`kindgen --check`:42 struct / 170 字段受判);**37 → 38**,分布 **16 / 5 / 17**,registry 与 `StreamRouting.g.cs` 两侧逐项相同。证伪 5 变异:M1/M2(删编解码分派)各 **2 红** · M3(`DefaultPathMarker` 加数字)**1 红** · M4(插中间)**2 红** · **M5(改路由)本地 0 红 = 无夹具**,守卫在 CI 的「重生成后一致性」步(已实测会红)。⚠️ **零写者**:53 的结算路径归 T2,b7 具名豁免为凭据。原件 `production/qa/evidence/t1-s5-treatment-harm-onset-2026-10-30.md` |
| S6 | 改 §Decision ⑤ 的两处文本 | ✅ **2026-10-30 已落** —— 两处:① `ConsequenceEmitter.cs:93` 的类 remarks(把「一个类的写入范围」与「53 的系统写者面」拆开,并把 ADR-035 §Decision ⑤ 补进文件头权威来源);② `story-003` 的 Control Manifest **Forbidden** 行(由系统级禁令收窄为「本 story 范围内」+ 日期注)。**V-8 已证**:两文件 grep 各 **0 命中**(原句只留在本件 C-2 行作历史记录)。⚠️ 本处只改文本,**不新增任何断言** —— 「卡面不再自相矛盾」是人工判读,不是机械门。原件 `production/qa/evidence/t1-s6-text-correction-2026-10-30.md` |
| S7 | **证伪轮** | M1 注入 `lethal=true` 行 ⇒ binder throw;M2 `injury_id` 指向致死行 ⇒ throw;M3 新 Kind 插中间 ⇒ 旧存档解码错(可做静态论证 + 一条夹具);**SHA-256 逐字节还原** — ✅ **2026-10-30 已落**。⚠️ **本行字面的三条在 S3 / S5 已跑过**(M1 与 M2 是**同一个判据**,都走 `lethal == true`;M3 = S5 的 M4),照抄重跑只多三行绿 —— 故本轮改为 ① 用覆盖表逐条指回既有实测,② 补上**从未被证伪的那一层**:S9 的烘焙 / 门链 **6 条变异**(产物整数外键 / ConfigVersion 覆盖两源 / 行序规范化 / 空表合法 / 门不吞错 / 门永不抛)⇒ **6 变异 / 9 红,全部恰好**;六次 SHA-256 逐字节还原,**零 `FALSIFY-` 残留**。**T1 全链合计 21 条变异 + 5 次探针构建**,覆盖表见原件 `production/qa/evidence/t1-s7-falsification-2026-10-30.md` §二。⚠️ **两处已知缺口如实登记**:S5 的 M5(路由)**本地 0 红**;S3/S4/S5 的判据**未重打** |
| S8 | `AC-5` 表级半边改判 | **仅在 S3 + S7 落盘后**才可把 PARTIAL 收窄;S5 前翻 = 门无宾语 — ✅ **2026-10-30 已落**(前置 S3 + S7 均已落)。改判 = **AC-5 由 PARTIAL 转 PASS(判据面)**:四层载体逐条可指(类型闭集 / 绑定器 N-1 / 运行期 L3 / **真机构建拒收**),并写明**残留不属本 AC**(「Given 禁忌代价表行」的真实实例仍缺 = `TD-sim-03` / T2)。连带:`TD-s13-02` **closed**(其唯一残留「表级半边无载体」消除);`TD-medcons-15` **维持 open**(只剩实现半边 T2/T3);**53 epic 仍不得收口**(DoD 其它条目未动)。原件 `production/qa/evidence/t1-s8-ac5-readjudication-2026-10-30.md` |
| S9 | **构建路径** —— 让 `CostTableBinder` 有一条**会被构建真的执行到**的链:<br>① `CostTable53Bake`(compute)+ `CostTableCookedWriter`(encode,承 ADR-014 §二 同源两次烘焙逐位一致)+ `DataBakeMenu` 入口(承 `DelayTableCookedWriter` / `BakeDelayTable` 先例);<br>② `AssemblyGates` 新增 `CostTableGate`(**构建前门**,`IPreprocessBuildWithReport`),对 repo 的 `53_cost_table.json` + `9` 的注册表跑 binder,`errs` 非空 ⇒ `BuildFailedException` | **探针轮三件套**(承 `CLAUDE.md` §构建前门):<br>① 注入 `lethal=true` 行 ⇒ **构建 `Result: Failure`**(不是「绑定器抛异常」);<br>② 报文**点名该行**(行号 / `injury_id`);<br>③ 栈帧给出该门的 `OnPreprocessBuild`;<br>还原判据 = SHA-256 逐字节 + 还原后同路径**绿**(两侧都要,禁只断「会红」)。<br>⚠️ **S9 未落时 V-5 / V-6 / V-7 只能记「绑定器层拒收」,不得记「构建 Failure」** —— 这正是 S3 / S4 证据件里登记的现状。<br>⚠️ **顺序**:S9 须在 **S7 之前** —— 证伪轮的靶场要含「构建门」这一面,否则那条变异无处可打。<br>🔁 **2026-10-30 实现期订正(原文不删)**:上句「② `AssemblyGates` 新增 `CostTableGate`」**不可照字面实现** —— 程序集边 `Editor.Tools.Bake → Editor.Tools.Gates` **已存在**(Bake 的 asmdef 引用了它),反向再引 = **循环引用**,Unity 直接拒绝编译;而门必须调用 `CostTableBinder`(住 Bake)。⇒ 实际落点 = `Editor.Tools.Bake/CostTableGate.cs`,**自带** `IPreprocessBuildWithReport`(Unity 按反射发现,与 `AssemblyGates.BuildGate` 并列),**不经** `AssemblyGates.RunAll()`。代价:门的清单位于两处,已在该文件头注明。**同轮另发现**:全仓**零** `BuildPipeline.BuildPlayer` 调用(CI 只跑测试)⇒ `IPreprocessBuildWithReport` 此前**没有任何执行者**;故 S9 同批新增探针轮载体 `Cli/CostTableProbeBuildCli.cs`(产物落系统临时目录 —— 首版放 `Library/` 被 Unity 直接拒,实测)。<br>✅ **2026-10-30 已落** —— 交付 `CostTableCookedWriter` / `CostTable53Bake` / `CostTableGate` / `CostTableProbeBuildCli` / `DataBakeMenu.BakeCostTable` + **10 条夹具**;**探针轮五次构建**:基线 `Success` → 门内无条件失败项 `Failure`(探针串 + 栈帧 + `utp` 止于 `Preprocess Player` 三件套齐)→ **V-5** 致死行 `Failure`(报文点名 `rows[0] … INJ-HEMORRHAGE` + `N-1`)→ **V-6** `Failure` → **V-7** `Failure` → **还原后 `Success`**;还原 SHA-256 逐字节一致。⚠️ **CI 不产出 player**,故本门今日只在**本机构建**生效。原件 `production/qa/evidence/t1-s9-build-path-2026-10-30.md` |

**必须等数值轮**:行键集合 · 档数 · `max_severity_band` · 实际选用的 `injury_id` · 是否复用 `DelayTable` 的延迟
(**⚠️ 若代价走 `ConsequenceResolved` 的延迟路径,须同时给 `CostInjury` 加一行 `delay_ticks`,
否则 `TryDelayTicks` false ⇒ 又一次静默跳过,与 W-4 同型**)。

## Validation Criteria

> ⚠️ ~~**V-1…V-9 全部未跑。Accepted 表示方向已裁,不表示任何判据已通过。禁借绿。**~~
> 🔁 **2026-10-30:V-1…V-10 全部已证**(原文保留)。⚠️ 但「已证」只覆盖**形状 / 门 / 文本** ——
> **T2 / T3 的「真跑」仍被 `TD-prescription-09` / `TD-sim-03` / `TD-sim-02` 卡住**,
> 不得把 V 全绿读成「药害机制已可用」。

- [x] **V-1**(**2026-10-30 已证 · S5**)registry 条目三字段齐(`stream: history` / `author` / `payload_schema`),A1–A5 全绿 —— `python3 tools/kindgen/kindgen.py --check` ⇒ 「A1–A5 全过 · 42 个载荷 struct / 170 个字段类型受判(非空跑)」
- [x] **V-2**(**2026-10-30 已证 · S5**)38 支 / 分布 **16 · 5 · 17**,registry 与生成物两侧逐项相同(`grep -c "^  - name: SimEvent.Kind\." design/registry/entities.yaml` ⇒ 38;`for s in History Case World; do grep -c "=> StreamId.$s," unity/Assets/Sim/StreamRouting.g.cs; done` ⇒ 16 / 5 / 17)
- [x] **V-3**(**2026-10-30 已证 · S5**)`SimEventCodec.cs` 闭集数串 = 38,与新总数一致
- [x] **V-4**(**2026-10-30 已证 · S5**)新成员位于追加区末尾 ⇒ **旧成员值零位移**;判据 = 黄金字节夹具 `test_simCodec_simEventHeader_matchesGoldenBytes` 绿,**且证伪轮 M4**(把成员插到 `InjuryOnset` 之后)令其**转红** —— 两侧都有,不是只断「绿」
- [x] **V-5**(**2026-10-30 已证 · S9**)N-1 变异(注入 `lethal=true` 行)⇒ 构建 Failure 且报文点名该行 —— 实跑 `Build Finished, Result: Failure.`,报文 `53_cost_table.json:rows[0] injury_id「INJ_HEMORRHAGE」的 lethal = true —— **N-1**`,栈帧 `CostTableGate/PreprocessBuild:OnPreprocessBuild`,`utp` 停在 `Preprocess Player`;还原后同路径 `Result: Success`
- [x] **V-6**(**2026-10-30 已证 · S9**)`rows` 非空而 `harm_kind_enabled` 为空 ⇒ 构建 Failure,报文点名 R4 /「静默跳过」
- [x] **V-7**(**2026-10-30 已证 · S9**)`harm_kind_enabled` 含 `overdose` ⇒ 构建 Failure,报文点名 `TD-sim-02`
- [x] **V-8**(**2026-10-30 已证 · S6**)§Decision ⑤ 两处文本已改,`grep` 不再出现「只写 `ConsequenceResolved`(世界流;53 不写病史流」的原句 ——<br>
      判据(两文件各 0 命中):`grep -c '只写 \`ConsequenceResolved\`(世界流;53 不写病史流' unity/Assets/Sim/Consequences/ConsequenceEmitter.cs production/epics/medical-consequences/story-003-delayed-emit-and-world-stream.md`<br>
      ⚠️ 该串仍**留在本件 §Problem Statement 的 C-2 行** —— 那是「原冲突长什么样」的历史记录,**不属于「两处文本」**;按「原文不删」纪律刻意保留
- [x] **V-9**(2026-10-29 新增 · **2026-10-30 已证**)**canary 有判别力**:往注册表任一病种行**注入一个 `compounds` 键** ⇒ `CostTableBinder` **必 `throw`** 且报文含「复查 N-4」;**反例**:不动注册表 ⇒ 构建照常绿(证明 canary 不会误伤正常构建)。⚠️ **只断「会红」不算完**:须同批给出「注入即红、还原即绿」两侧,还原判据 = SHA-256 逐字节
- [x] **V-10**(2026-10-29 新增 · **2026-10-30 已证**)**T2 前置可见**:`TD-sim-03` 结案前,`N-1` 的绿灯须能一眼看出是「无输入」—— 判据 = S3 的 binder 在「注册表零 `lethal=false` 伤情」时报**告警**(非 `throw`),报文点名 `TD-sim-03`;否则将来读到一片绿的人会以为「药害不致死已被证明」

- [x] **V-11**(2026-10-31 新增 · **同日已证**)**N-5 有判别力,且与 N-1 是两条独立判据** ——
      `unity/Assets/Tests/EditMode/MedConsequences/cost_table_binder_test.cs`:
      `test_n5_terminalInjury_isRejected`(把 `INJ_BLUNT` 的 `terminal_state` 翻成 `true` ⇒ 拒且报文点名 N-5)、
      `test_n5_isIndependentOfN1`(翻转前合法 / 翻转后被拒,且**拒收报文不含 N-1**)、
      `test_n5_realRegistry_doesNotFire`(真实 5 行全过,防过宽)。
      **证伪 M1**(`IsTerminal` 恒 `false`)⇒ 前两条**红**、第三条**绿** —— exact 2/1。
      ⚠️ **本判据不证明「药害不造成昏迷」** —— 只证明「门在工作」。零输入可达一事见 §③之三·三。

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
